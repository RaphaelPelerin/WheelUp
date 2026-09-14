using System.Collections.Generic;
using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Moto rendue à la physique à la chute : elle quitte le contrôleur avec son élan (vitesse, cabrage en
    /// cours, inclinaison), glisse, se couche et roule comme une vraie moto sans pilote, puis se relève au
    /// respawn.
    ///
    /// Posé sur le pivot visuel, qui porte le modèle. Tout l'enjeu est la silhouette vue de face : une moto est
    /// fine en bas (les pneus), un peu plus large au milieu (moteur, repose-pieds) et large en haut (guidon).
    /// Couchée, elle repose sur le bout du guidon et le carter moteur, penchée à plus de 70°, les roues en l'air.
    /// D'où quatre formes, prises au plus près du modèle :
    /// - deux roues au pneu arrondi en coupe : debout, la moto ne touche la route qu'en deux points et ne tient
    ///   pas seule, la gravité la couche ;
    /// - le cadre (moteur, réservoir, selle), une boîte étroite qui laisse un peu de garde au sol ;
    /// - le guidon, une barre d'une poignée à l'autre, seule partie vraiment large.
    /// Les rétroviseurs, qui dépassent encore, sont ignorés : dans une vraie chute, ils cassent.
    /// Les pneus glissent peu, le cadre et le guidon raclent l'asphalte et freinent la glissade.
    /// Comme le pilote, tout est monté au démarrage, cinématique et sans collider actif tant qu'on roule.
    /// </summary>
    public class MotoRagdoll : MonoBehaviour
    {
        [Tooltip("Masse de la moto, en kg (Honda C125 : 110 kg).")]
        public float mass = 115f;
        [Tooltip("Hauteur du centre de gravité, en part de la hauteur de la moto : moteur et cadre sont en bas.")]
        public float centerOfMassHeight = 0.4f;
        [Tooltip("Largeur d'un pneu, en mètres.")]
        public float tyreWidth = 0.13f;
        [Tooltip("Largeur du bloc moteur et des repose-pieds, en part de l'écartement des poignées.")]
        public float frameWidthShare = 0.55f;
        [Tooltip("Frottement des pneus, imposé quel que soit le sol. Les roues physiques ne tournent pas : un frottement faible les fait rouler au lieu de freiner la moto comme si elles étaient bloquées.")]
        public float tyreFriction = 0.04f;
        [Tooltip("Frottement du cadre et du guidon qui raclent la route : la moto couchée s'arrête en quelques mètres.")]
        public float scrapeFriction = 0.5f;
        [Tooltip("Rebond sur un choc (0 : aucun).")]
        public float bounciness = 0.1f;
        [Tooltip("Vitesse maximale de dégagement quand la moto démarre imbriquée dans le sol ou un mur.")]
        public float maxDepenetrationSpeed = 2f;

        const int WheelSides = 16;

        Transform home;
        Vector3 restPosition;
        Quaternion restRotation;
        Rigidbody body;
        Vector3 massCenter;
        Vector3 inertia;
        BoxCollider frame;
        Collider[] shapes;
        bool free;

        /// <summary>Vrai tant que la moto est à terre, menée par la physique.</summary>
        public bool Free => free;
        /// <summary>Formes de la moto : le pilote éjecté les ignore, les deux corps démarrant imbriqués.</summary>
        public Collider[] Shapes => shapes ?? new Collider[0];
        /// <summary>Centre de la moto, dans le monde.</summary>
        public Vector3 Center => transform.TransformPoint(frame.center);

        /// <param name="model">Modèle instancié sous ce pivot : ses maillages donnent la taille des formes.</param>
        /// <param name="rig">Gréement du modèle (roues, selle, poignées) ; sans lui, les proportions sont estimées.</param>
        public void Setup(Transform model, MotoRig rig)
        {
            home = transform.parent;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;

            if (!MeasureModel(model, out Bounds bounds))
            {
                Debug.LogWarning("[MotoRagdoll] Modèle sans maillage : la moto ne tombera pas en physique.", this);
                return;
            }

            // Repères de la moto dans l'espace du pivot : contacts des pneus, poignées, selle.
            Vector3 rearContact, frontContact, rightGrip, leftGrip, seat;
            float radius;
            if (rig != null)
            {
                rearContact = Local(model, rig.RearContact);
                frontContact = Local(model, rig.FrontContact);
                rightGrip = Local(model, rig.RightGrip);
                leftGrip = Local(model, new Vector3(-rig.RightGrip.x, rig.RightGrip.y, rig.RightGrip.z));
                seat = Local(model, rig.SeatContact);
                radius = rig.WheelRadius * rig.Scale;
            }
            else
            {
                // Pas de gréement : proportions d'une moto ordinaire, prises sur la boîte englobante.
                radius = Mathf.Min(0.3f, bounds.size.y * 0.3f);
                float x = bounds.center.x;
                rearContact = new Vector3(x, bounds.min.y, bounds.min.z + radius);
                frontContact = new Vector3(x, bounds.min.y, bounds.max.z - radius);
                float gripY = bounds.min.y + bounds.size.y * 0.8f;
                float gripZ = bounds.min.z + bounds.size.z * 0.72f;
                rightGrip = new Vector3(x + bounds.size.x * 0.4f, gripY, gripZ);
                leftGrip = new Vector3(x - bounds.size.x * 0.4f, gripY, gripZ);
                seat = new Vector3(x, bounds.min.y + bounds.size.y * 0.65f, bounds.center.z);
            }

            float centerX = (rearContact.x + frontContact.x) * 0.5f;
            float ground = Mathf.Min(rearContact.y, frontContact.y);
            Vector3 rearAxle = rearContact + Vector3.up * radius;
            Vector3 frontAxle = frontContact + Vector3.up * radius;
            // Pneus au frottement le plus faible des deux surfaces : rouler ne dépend pas du sol touché.
            PhysicsMaterial tyres = Material("Tyres", tyreFriction, PhysicsMaterialCombine.Minimum);
            PhysicsMaterial scrape = Material("Scrape", scrapeFriction, PhysicsMaterialCombine.Average);
            var built = new List<Collider>();

            // Roues : pneus arrondis de la largeur réelle. Debout, la moto ne repose que sur deux points.
            built.Add(AddWheel("RearWheel", rearAxle, radius, tyres));
            built.Add(AddWheel("FrontWheel", frontAxle, radius, tyres));

            // Cadre : moteur, réservoir et selle, étroit comme les repose-pieds, avec un peu de garde au sol.
            // Il s'arrête avant la roue avant, qui braque, et à l'aplomb de l'axe arrière : dépassant derrière,
            // il plongerait dans la route dès que la moto est cabrée, et la repousserait sur sa roue avant au
            // lieu de la laisser partir en arrière.
            float gripHalfSpan = Mathf.Abs(rightGrip.x - leftGrip.x) * 0.5f;
            float frameHalfWidth = Mathf.Max(tyreWidth, gripHalfSpan * frameWidthShare);
            float frameBottom = ground + radius * 0.6f;
            float frameTop = Mathf.Max(seat.y, frameBottom + radius);
            float frameBack = rearAxle.z;
            float frameFront = Mathf.Max(frameBack + radius, frontAxle.z - radius * 1.1f);
            frame = gameObject.AddComponent<BoxCollider>();
            frame.center = new Vector3(centerX, (frameBottom + frameTop) * 0.5f, (frameBack + frameFront) * 0.5f);
            frame.size = new Vector3(frameHalfWidth * 2f, frameTop - frameBottom, frameFront - frameBack);
            frame.sharedMaterial = scrape;
            frame.enabled = false;
            built.Add(frame);

            // Guidon : d'une poignée à l'autre, bouts compris. C'est lui qui tient la moto couchée à distance du sol.
            const float BarRadius = 0.045f;
            var bars = gameObject.AddComponent<CapsuleCollider>();
            bars.direction = 0; // Axe X : de gauche à droite.
            bars.radius = BarRadius;
            bars.height = gripHalfSpan * 2f + BarRadius * 4f;
            bars.center = (rightGrip + leftGrip) * 0.5f;
            bars.sharedMaterial = scrape;
            bars.enabled = false;
            built.Add(bars);

            shapes = built.ToArray();

            body = gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.maxDepenetrationVelocity = maxDepenetrationSpeed;
            body.isKinematic = true;
            // Masse posée à la main : les formes étant désactivées au repos, le calcul automatique n'aurait
            // rien sur quoi s'appuyer. Centre de gravité bas, entre les roues ; inertie d'un bloc de la taille
            // du cadre (l'essentiel de la masse), sur toute la longueur de la moto. Appliquées à la libération
            // seulement (voir ApplyMassProperties).
            massCenter = new Vector3(centerX, ground + bounds.size.y * centerOfMassHeight, (rearAxle.z + frontAxle.z) * 0.5f);
            Vector3 s = new Vector3(frameHalfWidth * 2f, bounds.size.y, frontAxle.z - rearAxle.z + radius * 2f);
            inertia = mass / 12f * new Vector3(s.y * s.y + s.z * s.z, s.x * s.x + s.z * s.z, s.x * s.x + s.y * s.y);
        }

        /// <summary>
        /// Centre de gravité et inertie, posés sur la moto libérée, dans cet ordre. Mesuré sur PhysX (Unity
        /// 6000.5) : régler l'inertie après le centre de gravité ramène celui-ci à l'origine du pivot — le
        /// contact du pneu arrière au sol — et un corps cinématique n'en garde aucun. Posé au sol, le centre de
        /// gravité n'offre aucun bras de levier à la gravité : la moto se balançait sans fin sur ses roues au
        /// lieu de se coucher.
        /// </summary>
        void ApplyMassProperties()
        {
            body.inertiaTensorRotation = Quaternion.identity;
            body.inertiaTensor = inertia;
            body.centerOfMass = massCenter;
            if ((body.centerOfMass - massCenter).sqrMagnitude > 1e-6f)
            {
                Debug.LogWarning($"[MotoRagdoll] Centre de gravité refusé par la physique ({body.centerOfMass} au lieu de {massCenter}).", this);
            }
        }

        /// <param name="velocity">Vitesse de la moto au moment de la chute, en m/s.</param>
        /// <param name="spin">Rotation en cours, en degrés par seconde.</param>
        /// <param name="spinPivot">Point autour duquel la moto tournait (le pneu resté au sol).</param>
        /// <param name="bikeBody">Capsule du contrôleur, restée au point de chute : l'épave ne doit pas la heurter.</param>
        public void Release(Vector3 velocity, Vector3 spin, Vector3 spinPivot, Collider bikeBody)
        {
            if (free || body == null) return;
            free = true;

            // Détachée en gardant sa pose : la moto part exactement d'où elle était, cabrée ou penchée.
            transform.SetParent(null, true);

            foreach (Collider shape in shapes)
            {
                shape.enabled = true;
                if (bikeBody != null) Physics.IgnoreCollision(shape, bikeBody);
            }

            body.isKinematic = false;
            ApplyMassProperties();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Une moto qui tournait autour de son pneu arrière : son centre de gravité, lui, se déplaçait aussi.
            Vector3 omega = spin * Mathf.Deg2Rad;
            body.linearVelocity = velocity + Vector3.Cross(omega, body.worldCenterOfMass - spinPivot);
            body.angularVelocity = omega;
        }

        /// <summary>Moto relevée : la physique rend la main et le pivot retrouve sa place sous le contrôleur.</summary>
        public void Restore()
        {
            if (!free) return;
            free = false;

            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            foreach (Collider shape in shapes) shape.enabled = false;

            transform.SetParent(home, false);
            transform.SetLocalPositionAndRotation(restPosition, restRotation);
        }

        /// <summary>Point du gréement (espace du prefab) ramené dans l'espace du pivot, échelle comprise.</summary>
        Vector3 Local(Transform model, Vector3 rigPoint) => transform.InverseTransformPoint(model.TransformPoint(rigPoint));

        /// <summary>
        /// Roue en maillage convexe à seize pans, au pneu arrondi en coupe comme un vrai : debout, la moto ne
        /// touche la route qu'en un point par roue et ne peut pas tenir en équilibre sur la bande de roulement.
        /// Couchée, elle ne dépasse que de la demi-largeur du pneu.
        /// </summary>
        MeshCollider AddWheel(string name, Vector3 axle, float radius, PhysicsMaterial material)
        {
            // Profil du pneu : cinq anneaux d'un flanc à l'autre, le plus grand au centre de la bande de roulement.
            float half = tyreWidth * 0.5f;
            float[] offsets = { -half, -half * 0.5f, 0f, half * 0.5f, half };
            int rings = offsets.Length;
            var vertices = new Vector3[WheelSides * rings];
            for (int r = 0; r < rings; r++)
            {
                float x = offsets[r];
                float ringRadius = radius - half + Mathf.Sqrt(Mathf.Max(0f, half * half - x * x));
                for (int i = 0; i < WheelSides; i++)
                {
                    float angle = i * Mathf.PI * 2f / WheelSides;
                    vertices[r * WheelSides + i] = axle + new Vector3(x, Mathf.Cos(angle) * ringRadius, Mathf.Sin(angle) * ringRadius);
                }
            }

            var triangles = new List<int>(WheelSides * 6 * rings);
            for (int r = 0; r < rings - 1; r++)
            {
                int a = r * WheelSides, b = (r + 1) * WheelSides;
                for (int i = 0; i < WheelSides; i++)
                {
                    int next = (i + 1) % WheelSides;
                    triangles.AddRange(new[] { a + i, a + next, b + i, a + next, b + next, b + i });
                }
            }
            int last = (rings - 1) * WheelSides;
            for (int i = 1; i < WheelSides - 1; i++)
            {
                triangles.AddRange(new[] { 0, i + 1, i, last, last + i, last + i + 1 });
            }

            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            var wheel = gameObject.AddComponent<MeshCollider>();
            // Convexe avant d'affecter le maillage : un corps physique n'accepte que des formes convexes.
            wheel.convex = true;
            wheel.sharedMesh = mesh;
            wheel.sharedMaterial = material;
            wheel.enabled = false;
            return wheel;
        }

        /// <summary>Boîte englobante des maillages du modèle, dans l'espace du pivot.</summary>
        bool MeasureModel(Transform model, out Bounds bounds)
        {
            bounds = default;
            if (model == null) return false;

            bool found = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;

                Bounds world = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = world.center + Vector3.Scale(world.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = transform.InverseTransformPoint(corner);
                    if (found) bounds.Encapsulate(local);
                    else bounds = new Bounds(local, Vector3.zero);
                    found = true;
                }
            }
            return found;
        }

        PhysicsMaterial Material(string name, float friction, PhysicsMaterialCombine combine) => new PhysicsMaterial(name)
        {
            dynamicFriction = friction,
            staticFriction = friction,
            bounciness = bounciness,
            frictionCombine = combine,
            bounceCombine = PhysicsMaterialCombine.Average,
        };
    }
}
