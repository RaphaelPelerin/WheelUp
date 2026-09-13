using System.Collections.Generic;
using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Pilote éjecté à la chute : il quitte la selle, part en pantin articulé (ragdoll) et roule sur la route,
    /// puis revient s'asseoir dès que le joueur relance la partie.
    ///
    /// Le modèle n'a ni colliders ni articulations sur son squelette : le pantin est fabriqué par le code au
    /// démarrage, sur onze os de l'humanoïde (bassin, buste, tête, bras et jambes). Chaque os reçoit une
    /// capsule taillée sur sa longueur réelle, sa part de la masse du corps, et une articulation limitée
    /// comme le fait l'assistant Ragdoll de Unity — coude et genou ne plient que dans un sens, l'épaule et la
    /// hanche ouvrent un cône. Tant que le pilote conduit, tout ce petit monde est cinématique et sans
    /// collider : c'est l'Animator qui mène, et rien ne coûte ni ne heurte quoi que ce soit. La chute rend la
    /// main à la physique, le respawn la lui reprend. Fabriquer le pantin au démarrage plutôt qu'au choc
    /// évite le à-coup pile au moment où l'action se joue.
    /// </summary>
    public class RiderEjection : MonoBehaviour
    {
        [Tooltip("Masse totale du pilote, en kg, répartie entre ses membres.")]
        public float mass = 72f;
        [Tooltip("Frottement de l'air sur chaque membre.")]
        public float airDrag = 0.05f;
        [Tooltip("Amortissement de la rotation des membres.")]
        public float spinDrag = 0.05f;
        [Tooltip("Vitesse maximale de séparation de deux membres imbriqués : bride les explosions de pantin.")]
        public float maxDepenetrationSpeed = 3f;

        /// <summary>Os du pantin : sa capsule, son corps physique et sa pose de repos.</summary>
        sealed class Part
        {
            public Transform Bone;
            public Rigidbody Body;
            public Collider Shape;
            public Vector3 RestPosition;
            public Quaternion RestRotation;
        }

        MotoRider rider;
        Animator animator;
        CharacterController bikeBody;
        // Place d'origine sur la moto, relevée avant le premier calage : le retour en selle la reprend telle quelle.
        Transform seat;
        Vector3 seatPosition;
        Quaternion seatRotation;
        Transform hips;
        SkinnedMeshRenderer[] skins;
        bool[] skinAlwaysUpdate;
        Part[] parts;
        bool ejected;

        /// <summary>Vrai tant que le pilote est à terre.</summary>
        public bool Ejected => ejected;
        /// <summary>Ce que regarde la caméra de chute : le bassin du pilote, à défaut sa racine.</summary>
        public Transform Focus => hips != null ? hips : transform;

        /// <param name="bike">Moto quittée : sa capsule ne doit pas repousser le corps à terre.</param>
        public void Setup(MotorcycleController bike, Animator riderAnimator)
        {
            rider = GetComponent<MotoRider>();
            animator = riderAnimator;
            if (bike != null) bike.TryGetComponent(out bikeBody);

            seat = transform.parent;
            seatPosition = transform.localPosition;
            seatRotation = transform.localRotation;

            skins = GetComponentsInChildren<SkinnedMeshRenderer>();
            skinAlwaysUpdate = new bool[skins.Length];
            for (int i = 0; i < skins.Length; i++) skinAlwaysUpdate[i] = skins[i].updateWhenOffscreen;

            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[RiderEjection] Pilote non humanoïde : pas de pantin, il restera sur la moto.", this);
                return;
            }

            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            BuildRagdoll();
        }

        /// <param name="velocity">Élan du corps au moment du choc, en m/s.</param>
        /// <param name="spin">Rotation d'ensemble du roulé-boulé, en degrés par seconde.</param>
        /// <param name="bikeShapes">Formes de la moto libérée : pilote et moto partent imbriqués, ils ne doivent pas se repousser.</param>
        public void Eject(Vector3 velocity, Vector3 spin, Collider[] bikeShapes = null)
        {
            if (ejected || seat == null || parts == null) return;
            ejected = true;

            // Plus personne ne tient le guidon : l'IK des bras et l'animation s'arrêtent, la physique prend
            // le relais sur les os. MotoRider rend au passage sa taille normale à la tête, masquée en vue 1re personne.
            if (rider != null) rider.enabled = false;
            if (animator != null) animator.enabled = false;
            // Détaché en gardant sa position dans le monde : le corps part d'où il était sur la selle.
            transform.SetParent(null, true);

            // Les os partent dans tous les sens : sans recalcul, le maillage disparaît dès que sa boîte
            // englobante d'origine sort du champ de la caméra.
            foreach (SkinnedMeshRenderer skin in skins) skin.updateWhenOffscreen = true;

            Vector3 omega = spin * Mathf.Deg2Rad;
            Vector3 pivot = Focus.position;
            foreach (Part part in parts)
            {
                part.Shape.enabled = true;
                // La moto roule sur un CharacterController, qui pousserait le corps devant lui comme une pelle.
                // À rappeler à chaque chute : Unity oublie la consigne quand un collider est réactivé.
                if (bikeBody != null) Physics.IgnoreCollision(part.Shape, bikeBody);
                if (bikeShapes != null)
                {
                    foreach (Collider shape in bikeShapes) Physics.IgnoreCollision(part.Shape, shape);
                }

                part.Body.isKinematic = false;
                // Les membres partent vite : sans détection continue, ils traverseraient la chaussée.
                part.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                part.Body.interpolation = RigidbodyInterpolation.Interpolate;
                // Corps lancé d'un bloc : chaque membre reçoit la vitesse que lui donne la rotation d'ensemble.
                part.Body.linearVelocity = velocity + Vector3.Cross(omega, part.Bone.position - pivot);
                part.Body.angularVelocity = omega;
            }
        }

        /// <summary>Retour en selle : la physique rend la main à l'Animator et le pilote reprend sa place.</summary>
        public void ReturnToSeat()
        {
            if (!ejected) return;
            ejected = false;

            foreach (Part part in parts)
            {
                part.Body.isKinematic = true;
                part.Body.interpolation = RigidbodyInterpolation.None;
                part.Shape.enabled = false;
                // Os remis dans leur pose : l'Animator, réveillé, ne les réécrit qu'à la frame suivante.
                part.Bone.SetLocalPositionAndRotation(part.RestPosition, part.RestRotation);
            }
            for (int i = 0; i < skins.Length; i++) skins[i].updateWhenOffscreen = skinAlwaysUpdate[i];

            transform.SetParent(seat, false);
            transform.SetLocalPositionAndRotation(seatPosition, seatRotation);
            if (animator != null) animator.enabled = true;
            // Nouveau calage complet : bassin sur la selle, puis mains sur le guidon.
            if (rider != null) rider.ReturnToSeat();
        }

        /// <summary>
        /// Onze os, chacun avec sa capsule et sa masse, reliés par des articulations limitées. Les angles
        /// repris ici sont ceux de l'assistant Ragdoll de Unity : ils donnent un corps qui plie comme un
        /// corps, sans membre qui parte à l'envers.
        /// </summary>
        void BuildRagdoll()
        {
            Transform chest = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);
            Transform neck = Bone(HumanBodyBones.Neck) ?? Bone(HumanBodyBones.Head);
            Transform head = Bone(HumanBodyBones.Head);
            Transform leftUpperArm = Bone(HumanBodyBones.LeftUpperArm);
            Transform rightUpperArm = Bone(HumanBodyBones.RightUpperArm);
            Transform leftUpperLeg = Bone(HumanBodyBones.LeftUpperLeg);
            Transform rightUpperLeg = Bone(HumanBodyBones.RightUpperLeg);

            if (hips == null || chest == null || neck == null)
            {
                Debug.LogWarning("[RiderEjection] Colonne vertébrale introuvable : pas de pantin.", this);
                return;
            }

            // Repère du corps au repos, lu sur le squelette lui-même (colonne et épaules) : les axes
            // d'articulation s'y rapportent. Passer par les os plutôt que par la racine de l'objet met à
            // l'abri des modèles dont la racine est tournée, comme il s'en importe souvent.
            Vector3 up = (chest.position - hips.position).normalized;
            if (up.sqrMagnitude < 0.5f) up = transform.up;
            Vector3 right = transform.right;
            if (leftUpperArm != null && rightUpperArm != null)
            {
                right = Vector3.ProjectOnPlane(rightUpperArm.position - leftUpperArm.position, up).normalized;
            }
            if (right.sqrMagnitude < 0.5f) right = transform.right;
            Vector3 forward = Vector3.Cross(right, up);

            var built = new List<Part>();

            // Bassin et buste : larges comme l'écart des hanches et des épaules, longs jusqu'à l'os suivant.
            // Un peu moins, en fait : la capsule est ronde là où un torse est ovale, plus plat que large.
            const float TorsoWidth = 0.85f;
            Part pelvis = AddCapsule(built, hips, chest, 0.16f, 0.3f, HalfSpan(hips, leftUpperLeg, rightUpperLeg) * TorsoWidth);
            Part torso = AddCapsule(built, chest, neck, 0.32f, 0.3f, HalfSpan(chest, leftUpperArm, rightUpperArm) * TorsoWidth);
            Join(torso, pelvis, right, forward, -20f, 20f, 10f);

            Part skull = AddHead(built, head, neck, 0.08f);
            Join(skull, torso, right, forward, -40f, 25f, 25f);

            AddArm(built, torso, false, right, forward);
            AddArm(built, torso, true, right, forward);
            AddLeg(built, pelvis, false, right, forward);
            AddLeg(built, pelvis, true, right, forward);

            parts = built.ToArray();
        }

        void AddArm(List<Part> built, Part torso, bool isRight, Vector3 right, Vector3 forward)
        {
            Transform upper = Bone(isRight ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            Transform lower = Bone(isRight ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            Transform hand = Bone(isRight ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

            Part arm = AddCapsule(built, upper, lower, 0.028f, 0.25f);
            Join(arm, torso, right, forward, -70f, 10f, 50f);
            // Le coude ne plie que dans un sens : sa torsion porte l'angle, il n'ouvre aucun cône.
            Part forearm = AddCapsule(built, lower, hand, 0.022f, 0.2f);
            Join(forearm, arm, forward, right, -90f, 0f, 0f);
        }

        void AddLeg(List<Part> built, Part pelvis, bool isRight, Vector3 right, Vector3 forward)
        {
            Transform upper = Bone(isRight ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg);
            Transform lower = Bone(isRight ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg);
            Transform foot = Bone(isRight ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);

            Part thigh = AddCapsule(built, upper, lower, 0.10f, 0.3f);
            Join(thigh, pelvis, right, forward, -20f, 70f, 30f);
            Part shin = AddCapsule(built, lower, foot, 0.07f, 0.25f);
            Join(shin, thigh, right, forward, -80f, 0f, 0f);
        }

        Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        /// <summary>Capsule couchée le long de l'os, de son articulation jusqu'à la suivante.</summary>
        Part AddCapsule(List<Part> built, Transform bone, Transform tip, float massShare, float radiusShare, float radius = 0f)
        {
            if (bone == null || tip == null) return null;

            Vector3 localTip = bone.InverseTransformPoint(tip.position);
            float length = localTip.magnitude;
            if (length < 0.001f) return null;

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = LongestAxis(localTip);
            capsule.center = localTip * 0.5f;
            capsule.radius = radius > 0f ? radius : length * radiusShare;
            capsule.height = Mathf.Max(length, capsule.radius * 2f);
            return Add(built, bone, capsule, massShare);
        }

        /// <summary>Boule posée au-dessus de l'articulation du cou, de la taille d'un crâne.</summary>
        Part AddHead(List<Part> built, Transform head, Transform neck, float massShare)
        {
            if (head == null || neck == null || head == neck) return null;

            Vector3 fromNeck = head.InverseTransformPoint(neck.position);
            float neckLength = fromNeck.magnitude;
            if (neckLength < 0.001f) return null;

            var sphere = head.gameObject.AddComponent<SphereCollider>();
            sphere.radius = neckLength;
            sphere.center = -fromNeck * 1.2f;
            return Add(built, head, sphere, massShare);
        }

        Part Add(List<Part> built, Transform bone, Collider shape, float massShare)
        {
            // Au repos, le pantin ne touche rien et ne coûte rien : l'Animator déplace des corps cinématiques.
            shape.enabled = false;

            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass * massShare;
            body.linearDamping = airDrag;
            body.angularDamping = spinDrag;
            body.maxDepenetrationVelocity = maxDepenetrationSpeed;
            // Un pantin tient mieux ses articulations avec un solveur un peu plus appliqué que par défaut.
            body.solverIterations = 10;
            body.isKinematic = true;

            var part = new Part
            {
                Bone = bone,
                Body = body,
                Shape = shape,
                RestPosition = bone.localPosition,
                RestRotation = bone.localRotation,
            };
            built.Add(part);
            return part;
        }

        /// <param name="twist">Axe de torsion de l'articulation, dans le repère du corps au repos.</param>
        /// <param name="swing">Axe du cône de débattement.</param>
        void Join(Part part, Part parent, Vector3 twist, Vector3 swing, float low, float high, float cone)
        {
            if (part == null || parent == null) return;

            var joint = part.Bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent.Body;
            joint.axis = LocalAxis(part.Bone, twist);
            joint.swingAxis = LocalAxis(part.Bone, swing);
            joint.lowTwistLimit = new SoftJointLimit { limit = low };
            joint.highTwistLimit = new SoftJointLimit { limit = high };
            joint.swing1Limit = new SoftJointLimit { limit = cone };
            joint.swing2Limit = new SoftJointLimit { limit = 0f };
            // Sans pré-traitement et avec projection, les membres ne s'étirent pas sous un choc violent.
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 15f;
        }

        /// <summary>Demi-écart entre deux os (hanches, épaules), mesuré dans l'espace d'un troisième.</summary>
        static float HalfSpan(Transform space, Transform left, Transform right)
        {
            if (left == null || right == null) return 0f;

            return Vector3.Distance(space.InverseTransformPoint(left.position),
                space.InverseTransformPoint(right.position)) * 0.5f;
        }

        /// <summary>Direction du monde ramenée à l'axe local le plus proche : un axe d'articulation est cardinal.</summary>
        static Vector3 LocalAxis(Transform bone, Vector3 worldDirection)
        {
            Vector3 local = bone.InverseTransformDirection(worldDirection);
            int axis = LongestAxis(local);
            var result = Vector3.zero;
            result[axis] = local[axis] < 0f ? -1f : 1f;
            return result;
        }

        static int LongestAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            if (x >= y && x >= z) return 0;
            return y >= z ? 1 : 2;
        }
    }
}
