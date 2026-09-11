using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using WheelingMoto.Data;

namespace WheelingMoto.Gameplay
{
    /// <summary>Position de la moto dans le wheeling, pour l'affichage.</summary>
    public enum WheelieZone
    {
        Flat,
        Rising,
        Balance,
        Critical
    }

    /// <summary>
    /// Contrôleur de moto V1 (free-roam). Le déplacement passe par un CharacterController :
    /// collisions avec le décor et gravité, sans l'instabilité d'une vraie physique deux-roues.
    ///
    /// Commandes : GAZ accélère seulement ; LEVER accélère aussi et lève la roue avant ;
    /// FREIN freine, rabat la roue avant en wheeling, lève l'arrière s'il est tenu fort à bonne vitesse,
    /// et fait reculer une fois à l'arrêt.
    ///
    /// Trois mécaniques sont simulées à part, sur le pivot visuel :
    /// - le wheeling, autour du point de contact du pneu arrière, en trois zones : sous la zone
    ///   d'équilibre la gravité rabat la roue ; dans la zone elle ne dérive que faiblement, mais les
    ///   bosses de la route perturbent l'angle en permanence (tenue au dosage LEVER / FREIN) ;
    ///   au-delà, la moto part en arrière et seul le frein la rattrape ;
    /// - la roue avant (stoppie), autour du contact du pneu avant, sur le même principe : FREIN lève
    ///   l'arrière, le relâcher le repose ; au-delà de la zone d'équilibre rien ne rattrape la moto,
    ///   elle passe par-dessus la roue avant ;
    /// - la direction, qui passe par l'inclinaison (lean), prise progressivement (ressort amorti,
    ///   plus lent à haute vitesse) et limitée à basse vitesse ; le virage découle de l'angle pris via
    ///   w = g*tan(lean)/v, et le guidon affiche le braquage correspondant, avec contre-braquage.
    ///   Dès que la roue avant quitte le sol, la direction ne passe plus que par le poids du corps.
    ///
    /// Deux sources d'entrée cohabitent : les boutons tactiles (mobile) et le clavier (test PC).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MotorcycleController : MonoBehaviour
    {
        [Header("Modèle")]
        public GameObject bikePrefab;
        public Transform visualPivot;
        [Tooltip("Si le modèle apparaît tourné dans le mauvais sens une fois en jeu, ajuster l'axe Y ici (ex: 180).")]
        public Vector3 visualEulerOffset = Vector3.zero;
        [Tooltip("Hauteur de l'origine du modèle au-dessus du contact des pneus. Honda C125 : axes à 0,11 m, pneus Ø 0,56-0,58 m.")]
        public float modelGroundOffset = 0.173f;
        [Tooltip("Position avant/arrière de l'axe de la roue arrière dans le modèle : pivot du wheeling.")]
        public float rearContactZ = -0.44f;

        [Header("Départ")]
        [Tooltip("Point de départ de la moto (position et direction). Vide : la moto part de là où MotoRoot est posé dans la scène.")]
        public Transform spawnPoint;
        [Tooltip("Cherche au démarrage le milieu d'une route droite près du point de départ, et s'y aligne.")]
        public bool spawnOnRoad = true;
        [Tooltip("Mot présent dans le nom des matériaux de chaussée (Demo City : asphalt_2_tracks, asphalt_4_tracks...).")]
        public string roadMaterialKeyword = "tracks";
        [Tooltip("Rayon de recherche d'une route autour du point de départ, en mètres.")]
        public float roadSearchRadius = 150f;

        [Header("Pilote (vue 1re personne)")]
        [Tooltip("Place les yeux automatiquement à partir de la selle du modèle. Décoché : firstPersonEyeOffset est utilisé tel quel.")]
        public bool autoFirstPersonEye = true;
        [Tooltip("Début du nom des pièces de selle dans le modèle (SEAT_1, SEAT_2... sur la Honda C125).")]
        public string seatPartPrefix = "SEAT";
        [Tooltip("Distance entre l'assise et les yeux, le long du buste. Plus faible = vue plus basse. Réglable en jeu.")]
        public float sittingEyeHeight = 0.4f;
        [Tooltip("Inclinaison du buste vers l'avant, en degrés : plus penché = tête plus basse et plus en avant. Réglable en jeu.")]
        public float riderTorsoLean = 18f;
        [Tooltip("Distance entre l'avant de la selle et les hanches du pilote.")]
        public float hipsBehindSeatFront = 0.22f;
        [Tooltip("Position manuelle des yeux par rapport à MotoRoot : utilisée si le calcul automatique est désactivé ou si aucune selle n'est trouvée.")]
        public Vector3 firstPersonEyeOffset = new Vector3(0f, 1.45f, -0.12f);
        [Tooltip("Regard vers la route roue au sol, en degrés vers le bas. Plus faible = vue plus droite, qui plonge moins sur la moto.")]
        public float firstPersonHeadPitch = 12f;

        [Header("Pilote (personnage)")]
        [Tooltip("Modèle humanoïde du pilote (SKM_Mannequin du pack MotoInteractionAnims). Vide : pas de pilote.")]
        public GameObject riderPrefab;
        [Tooltip("Contrôleur d'animation de la position de conduite (AS_Idle_Riding).")]
        public RuntimeAnimatorController riderAnimator;

        [Header("Feux")]
        [Tooltip("Matériau émissif des optiques (phare, feu arrière, clignotants). Référencé ici pour que la variante émissive du shader soit incluse dans les builds.")]
        public Material lightMaterial;

        [Header("Conduite")]
        public float maxSpeed = 15.5f;
        public float acceleration = 10.5f;
        public float brakingDeceleration = 20f;
        public float naturalDeceleration = 4f;
        [Tooltip("Multiplicateur d'accélération avec LEVER : gaz en grand pour lever la roue.")]
        public float liftAccelerationBoost = 1.45f;

        [Header("Marche arrière")]
        public float reverseMaxSpeed = 3f;
        public float reverseAcceleration = 3f;
        [Tooltip("Rotation en marche arrière à vitesse maximale, en degrés par seconde.")]
        public float reverseTurnRate = 55f;

        [Header("Direction (inclinaison)")]
        [Tooltip("Angle de carrossage maximum, en degrés.")]
        public float maxLeanAngle = 40f;
        [Tooltip("Vitesse (m/s) à partir de laquelle l'inclinaison maximale est atteignable. Une vraie moto penche peu à basse vitesse.")]
        public float leanFullSpeed = 7f;
        [Tooltip("Temps de mise sur l'angle à basse vitesse (ressort amorti : entrée et sortie de courbe progressives).")]
        public float leanSmoothTimeLowSpeed = 0.22f;
        [Tooltip("Temps de mise sur l'angle à vitesse maximale : plus long, l'effet gyroscopique stabilise la moto.")]
        public float leanSmoothTimeHighSpeed = 0.42f;
        [Tooltip("Vitesse de bascule maximale, en degrés par seconde.")]
        public float maxLeanRate = 110f;
        [Tooltip("Plafond de confort sur le taux de rotation, en degrés par seconde.")]
        public float maxYawRate = 70f;

        [Header("Direction en wheeling")]
        [Tooltip("Autorité de direction conservée roue avant en l'air : on ne dirige plus qu'avec le poids du corps.")]
        public float wheelieSteerAuthority = 0.8f;
        [Tooltip("Cabrage (degrés) à partir duquel la roue avant ne touche plus : réduction de direction complète.")]
        public float wheelieSteerFadeAngle = 10f;
        [Tooltip("Ralentissement de la bascule roue avant en l'air (1 = aussi vive qu'au sol).")]
        public float wheelieLeanSlowdown = 1.1f;
        [Tooltip("Rotation ajoutée roue levée, en degrés/s à plein braquage : le pilote fait pivoter la moto au poids du corps, assez pour prendre une rue à 90°.")]
        public float wheelieYawAssist = 22f;

        [Header("Guidon (animation)")]
        [Tooltip("Butée de direction du guidon (43° sur la Honda C125). Limite aussi le virage au pas.")]
        public float maxSteeringLock = 43f;
        [Tooltip("Empattement, pour le braquage géométrique atan(empattement / rayon). Honda C125 : 1,247 m.")]
        public float wheelbase = 1.247f;
        [Tooltip("Contre-braquage à l'entrée et à la sortie des courbes : degrés de guidon par degré/s de bascule.")]
        public float countersteerGain = 0.05f;
        [Tooltip("Braquage visible ajouté en virage, à l'inclinaison maximale (degrés) : le guidon suit nettement la courbe.")]
        public float handlebarLeanAngle = 14f;
        [Tooltip("Braquage du guidon en wheeling, dans le sens opposé au virage (degrés).")]
        public float handlebarWheelieAngle = 22f;
        [Tooltip("Temps de lissage des mouvements du guidon.")]
        public float handlebarSmoothTime = 0.12f;
        [Tooltip("Pièce du modèle qui tourne avec la direction (axe de colonne). Honda C125 : axis___handle.")]
        public string handlebarPartName = "axis___handle";

        [Header("Freins et fourche (animation)")]
        [Tooltip("Vitesse d'actionnement du levier et de la pédale de frein : pose complète en 1/x seconde.")]
        public float brakeLeverSpeed = 9f;
        [Tooltip("Plongée de la fourche au freinage, roue avant au sol (0 = aucune, 1 = compression complète).")]
        public float forkBrakeDive = 0.8f;
        [Tooltip("Choc encaissé par la fourche quand la roue avant retombe, par degré/s de vitesse de chute.")]
        public float forkLandingKick = 0.1f;
        [Tooltip("Raideur du ressort de fourche : haute = retour rapide.")]
        public float forkStiffness = 170f;
        [Tooltip("Amortissement de la fourche : bas = rebonds visibles.")]
        public float forkDamping = 13f;
        [Tooltip("Détente de la fourche roue avant en l'air, en fraction de la compression complète.")]
        public float forkWheelieExtension = 0.25f;

        [Header("Collision")]
        public float colliderHeight = 1.4f;
        public float colliderRadius = 0.35f;
        public float gravity = -25f;
        [Tooltip("Vitesse conservée après avoir percuté un mur.")]
        public float wallImpactSpeed = 1.5f;
        [Tooltip("Hauteur franchissable, en mètres : un trottoir passe, un muret non.")]
        public float stepHeight = 0.22f;
        [Tooltip("Pente maximale gravissable, en degrés.")]
        public float maxClimbSlope = 38f;
        [Tooltip("Rayon du sondage qui arrête l'avant et l'arrière de la moto contre les murs (la capsule n'en couvre que le milieu).")]
        public float wallProbeRadius = 0.25f;

        [Header("Suivi du sol")]
        [Tooltip("Temps de lissage de l'assiette de la moto sur les pentes.")]
        public float groundAlignSmoothTime = 0.1f;
        [Tooltip("Pente maximale suivie par l'assiette, en degrés.")]
        public float maxGroundPitch = 30f;
        [Tooltip("Lissage vertical de la moto affichée : absorbe les petits à-coups du contrôleur sur les jointures du sol.")]
        public float heightSmoothTime = 0.06f;
        [Tooltip("Hauteur du sondage vers le sol au démarrage, pour ne jamais apparaître dans le décor.")]
        public float groundProbeHeight = 200f;

        [Header("Wheeling - zones d'angle")]
        [Tooltip("Début de la zone d'équilibre, en degrés.")]
        public float wheelieSweetMin = 34f;
        [Tooltip("Fin de la zone d'équilibre : au-delà, la moto part en arrière (zone critique).")]
        public float wheelieSweetMax = 42f;
        [Tooltip("Angle de chute : basculement arrière irrécupérable.")]
        public float wheelieFallAngle = 68f;

        [Header("Wheeling - couples (degrés/s²)")]
        [Tooltip("Levée donnée par LEVER roue au sol et en montée.")]
        public float wheelieLiftTorque = 195f;
        [Tooltip("Levée donnée par LEVER une fois dans la zone d'équilibre : modérée, pour pouvoir doser.")]
        public float wheelieHoldTorque = 42f;
        [Tooltip("Frein arrière dans la zone de montée et d'équilibre.")]
        public float wheelieBrakeTorque = 175f;
        [Tooltip("Frein arrière en zone critique : appliqué en plus d'un arrêt net de la montée.")]
        public float criticalBrakeTorque = 420f;
        [Tooltip("Rappel vers le sol quand la moto est à plat ou presque.")]
        public float lowAnglePull = 125f;
        [Tooltip("Dérive aux bords de la zone d'équilibre (nulle en son centre). Faible = zone tolérante.")]
        public float sweetSpotDrift = 34f;
        [Tooltip("Poussée arrière à l'angle de chute.")]
        public float criticalPush = 145f;
        [Tooltip("Rappel vers le sol quand la moto roule trop lentement pour tenir la roue.")]
        public float stalledPull = 90f;
        [Tooltip("Amortissement : bas = moto vive qui dépasse sa cible, haut = moto docile.")]
        public float wheelieDamping = 2f;
        public float wheelieMinSpeed = 1.5f;
        [Tooltip("Perturbation due aux bosses de la route, roue avant levée : empêche de tenir le wheeling sans corriger.")]
        public float wheelieBumpTorque = 30f;
        [Tooltip("Fréquence des bosses, en Hz.")]
        public float wheelieBumpFrequency = 1.7f;

        [Header("Roue avant (stoppie)")]
        [Tooltip("Vitesse (m/s) sous laquelle FREIN ne tient plus l'arrière levé : il retombe.")]
        public float stoppieMinSpeed = 4f;
        [Tooltip("Début de la zone d'équilibre sur la roue avant, en degrés.")]
        public float stoppieSweetMin = 16f;
        [Tooltip("Fin de la zone d'équilibre : au-delà, la moto passe par-dessus la roue avant, rien ne la rattrape.")]
        public float stoppieSweetMax = 24f;
        [Tooltip("Angle de chute vers l'avant.")]
        public float stoppieFallAngle = 45f;
        [Tooltip("Levée de l'arrière par FREIN à pleine vitesse, en degrés/s² : proportionnelle à la vitesse, il faut freiner fort en roulant vite.")]
        public float stoppieLiftTorque = 300f;
        [Tooltip("Levée par FREIN une fois dans la zone d'équilibre : modérée, pour pouvoir doser.")]
        public float stoppieHoldTorque = 40f;
        [Tooltip("Retour de l'arrière vers le sol quand on relâche FREIN.")]
        public float stoppieReleaseTorque = 120f;
        [Tooltip("Rappel vers le sol sous la zone d'équilibre.")]
        public float stoppieLowPull = 110f;
        [Tooltip("Dérive aux bords de la zone d'équilibre (nulle en son centre).")]
        public float stoppieDrift = 30f;
        [Tooltip("Bascule vers l'avant au-delà de la zone d'équilibre, jusqu'à la chute.")]
        public float stoppieCriticalPush = 220f;
        [Tooltip("Décélération sur la roue avant, frein tenu : plus douce qu'au sol, pour laisser le temps de doser.")]
        public float stoppieBrakeDeceleration = 3f;
        [Tooltip("Autorité de direction conservée roue arrière en l'air.")]
        public float stoppieSteerAuthority = 0.4f;

        [Header("Chute et respawn")]
        [Tooltip("Durée du basculement visible avant la remise à plat.")]
        public float fallAnimationTime = 0.3f;
        public float fallRotateSpeed = 150f;
        [Tooltip("Angle atteint pendant l'animation de chute.")]
        public float fallVisualAngle = 95f;
        [Tooltip("Vitesse de la moto au respawn.")]
        public float respawnSpeed = 0f;
        [Tooltip("Efficacité du freinage pendant un wheeling (seul le frein arrière porte).")]
        public float brakeFactorDuringWheelie = 0.35f;

        /// <summary>Déclenché au moment du basculement arrière, avant la remise à plat.</summary>
        public event Action Fell;

        const float GravityConstant = 9.81f;
        const float BumpNoiseSeed = 7.31f;
        const float HandlebarMaxRate = 300f;
        const float ThrottleRate = 5f;
        const float StoppieLiftFadeAngle = 6f;

        CharacterController body;
        Transform riderHead;
        Transform handlebarPart;
        Quaternion handlebarRestRotation;
        MotoRig rig;
        Transform visualModel;
        Vector3 rigSeatLocal;
        Transform frontWheel, rearWheel;
        Quaternion frontWheelRest, rearWheelRest;
        float wheelSpin;
        float groundPitch, groundPitchVelocity;
        float rearGroundOffset, rearGroundOffsetVelocity;
        float smoothedHeight, smoothedHeightVelocity;
        bool heightInitialized;
        readonly RaycastHit[] probeHits = new RaycastHit[8];
        PosedPart[] frontBrakeParts, rearBrakeParts, forkParts;
        Bounds seatBounds;
        bool hasSeat;

        float frontBrakeWeight;
        float rearBrakeWeight;
        float forkCompression;
        float forkVelocity;
        float throttle;
        Vector3? riderEyeOverride;

        float currentSpeed;
        float verticalSpeed;
        float currentLean;
        float leanVelocity;
        float currentYawRate;
        float handlebarAngle;
        float handlebarVelocity;
        float wheelieAngle;
        float wheelieAngularVelocity;
        float stoppieAngle;
        float stoppieAngularVelocity;
        Vector3 pivotRestPosition;
        bool isFallen;
        bool fallForward;
        float fallTimer;
        // Après un respawn, LEVER doit être relâché : sinon un bouton resté enfoncé relancerait la chute.
        bool wheelieNeedsRelease;

        // Entrées tactiles (HUD) et clavier (test PC), fusionnées à chaque frame.
        bool touchThrottle, touchBrake, touchLift;
        float touchSteer;
        bool keyThrottle, keyBrake, keyLift;
        float keySteer;

        bool Lift => touchLift || keyLift;
        bool Accelerate => touchThrottle || keyThrottle || Lift;
        bool Brake => touchBrake || keyBrake;
        float Steer => Mathf.Clamp(touchSteer + keySteer, -1f, 1f);

        public bool IsFallen => isFallen;
        public float SpeedKmh => Mathf.Abs(currentSpeed) * 3.6f;
        /// <summary>Vitesse en m/s, négative en marche arrière.</summary>
        public float SignedSpeed => currentSpeed;
        public float WheelieAngle => wheelieAngle;
        /// <summary>Basculement sur la roue avant, en degrés (0 : roue arrière au sol).</summary>
        public float StoppieAngle => stoppieAngle;
        /// <summary>Pente du sol sous la moto, en degrés (positive en montée).</summary>
        public float GroundPitch => groundPitch;
        /// <summary>Vrai si la dernière chute est partie vers l'avant, par-dessus la roue avant.</summary>
        public bool LastFallForward => fallForward;
        /// <summary>Part de la roue arrière en l'air, de 0 à 1.</summary>
        internal float RearWheelLift => RearWheelUp();
        public float LeanAngle => currentLean;
        /// <summary>Tête du pilote : suit le cabrage et l'inclinaison de la moto.</summary>
        public Transform RiderHead => riderHead;
        /// <summary>Colonne de direction du modèle : les poignées tournent avec elle.</summary>
        internal Transform HandlebarPart => handlebarPart;
        /// <summary>Levier de frein avant, de 0 (relâché) à 1 (serré).</summary>
        internal float FrontBrakeLever => frontBrakeWeight;
        /// <summary>Poignée d'accélérateur, de 0 (fermée) à 1 (en grand, avec LEVER).</summary>
        internal float Throttle => throttle;
        /// <summary>Point d'assise du pilote sur la selle, dans l'espace du pivot visuel.</summary>
        internal Vector3 SeatContactPoint => rig != null
            ? rigSeatLocal
            : new Vector3(seatBounds.center.x, seatBounds.max.y, seatBounds.max.z - hipsBehindSeatFront);
        /// <summary>Buste penché d'origine du pilote, propre au modèle (posture), en degrés.</summary>
        internal float RiderForwardLean => rig != null ? rig.RiderForwardLean : 0f;
        /// <summary>Levier d'embrayage à gauche (boîte manuelle) : le pilote le couvre de deux doigts.</summary>
        internal bool HasClutchLever => rig != null && rig.HasClutchLever;
        /// <summary>Commande de frein tenue (feu stop).</summary>
        internal bool BrakeHeld => Brake;
        /// <summary>Vitesse de cabrage, en degrés par seconde (positive quand la roue monte).</summary>
        internal float WheelieAngularVelocity => wheelieAngularVelocity;
        /// <summary>Compression de la fourche (négative en détente).</summary>
        internal float ForkCompression => forkCompression;
        /// <summary>Direction demandée, de -1 (gauche) à 1 (droite).</summary>
        internal float SteerInput => Steer;
        /// <summary>Part de la roue avant en l'air, de 0 à 1.</summary>
        internal float FrontWheelLift => FrontWheelUp();

        /// <summary>Centres des poignées du gréement, dans l'espace de la colonne de direction, et leur rayon réel.</summary>
        internal bool TryGetRigGrips(out Vector3 left, out Vector3 right, out float radius)
        {
            left = Vector3.zero;
            right = Vector3.zero;
            radius = 0f;
            if (rig == null || handlebarPart == null || visualModel == null) return false;

            Vector3 grip = rig.RightGrip;
            right = handlebarPart.InverseTransformPoint(visualModel.TransformPoint(grip));
            left = handlebarPart.InverseTransformPoint(visualModel.TransformPoint(new Vector3(-grip.x, grip.y, grip.z)));
            radius = rig.GripRadius * rig.Scale;
            return true;
        }

        /// <summary>Yeux de la vue 1re personne fournis par le pilote (ancrés à son buste), dans l'espace du pivot visuel.</summary>
        internal void SetRiderEye(Vector3 localEye) => riderEyeOverride = localEye;

        public WheelieZone CurrentWheelieZone
        {
            get
            {
                if ((isFallen && !fallForward) || wheelieAngle > wheelieSweetMax) return WheelieZone.Critical;
                if (wheelieAngle >= wheelieSweetMin) return WheelieZone.Balance;
                if (wheelieAngle >= 1f) return WheelieZone.Rising;
                return WheelieZone.Flat;
            }
        }

        /// <summary>Position de la moto sur la roue avant, mêmes zones que le wheeling.</summary>
        public WheelieZone CurrentStoppieZone
        {
            get
            {
                if ((isFallen && fallForward) || stoppieAngle > stoppieSweetMax) return WheelieZone.Critical;
                if (stoppieAngle >= stoppieSweetMin) return WheelieZone.Balance;
                if (stoppieAngle >= 1f) return WheelieZone.Rising;
                return WheelieZone.Flat;
            }
        }

        // bikePrefab pointe sur un pack Asset Store non versionné : la référence est vide sur un poste
        // qui ne l'a pas importé. On retombe alors sur le modèle de la moto de base, chargé depuis Resources.
        GameObject LoadCatalogModel()
        {
            var moto = MotoCatalog.Default;
            if (moto == null || string.IsNullOrEmpty(moto.ModelResourcePath)) return null;

            var model = Resources.Load<GameObject>(moto.ModelResourcePath);
            if (model == null)
            {
                Debug.LogWarning($"Modèle introuvable pour {moto.Name} : Resources/{moto.ModelResourcePath}");
            }
            return model;
        }

        void Awake()
        {
            // En tout premier : la caméra et le calage au sol (Start) partent ainsi de la bonne position.
            if (spawnPoint != null)
            {
                // Seule la direction est reprise : la moto démarre droite, SnapToGround la pose ensuite sur le sol.
                transform.SetPositionAndRotation(spawnPoint.position, Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f));
            }

            if (!TryGetComponent(out body))
            {
                body = gameObject.AddComponent<CharacterController>();
            }
            body.height = colliderHeight;
            body.radius = colliderRadius;
            body.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
            body.slopeLimit = maxClimbSlope;
            body.stepOffset = stepHeight;

            GameObject visual = null;
            if (visualPivot != null)
            {
                GameObject prefab = bikePrefab != null ? bikePrefab : LoadCatalogModel();
                rig = MotoRigs.Find(prefab);
                if (rig != null)
                {
                    // Gréement connu : il fixe la taille réelle de la moto, la position des pneus et l'empattement.
                    rearContactZ = rig.RearContact.z * rig.Scale;
                    wheelbase = (rig.FrontContact.z - rig.RearContact.z) * rig.Scale;
                }

                // Pivot du wheeling posé sur le contact du pneu arrière. Le CharacterController flotte
                // de sa skin width au-dessus du sol : on la retranche pour que les pneus touchent la route.
                pivotRestPosition = new Vector3(0f, -body.skinWidth, rearContactZ);
                visualPivot.localPosition = pivotRestPosition;
                visualPivot.localRotation = Quaternion.identity;

                if (prefab != null)
                {
                    visual = Instantiate(prefab, visualPivot);
                    if (rig != null)
                    {
                        // À l'échelle réelle, le contact du pneu arrière posé sur le pivot du wheeling.
                        visual.transform.localScale = Vector3.one * rig.Scale;
                        visual.transform.localPosition = -rig.RearContact * rig.Scale;
                        visual.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        visual.transform.localPosition = new Vector3(0f, modelGroundOffset, -rearContactZ);
                        visual.transform.localRotation = Quaternion.Euler(visualEulerOffset);
                    }
                    MotoPainter.Apply(visual, MotoCatalog.Default.Name);

                    Transform model = visual.transform;
                    visualModel = model;
                    RemovePhysics(model);
                    if (rig != null) RigModel(model);
                    else RigC125(model);
                }
            }

            // Ancrée sous le pivot visuel : la tête suit la moto en position (cabrage et inclinaison compris).
            // L'orientation du regard est calculée par la caméra, qui garde les yeux sur la route.
            Transform headParent = visualPivot != null ? visualPivot : transform;
            riderHead = new GameObject("RiderHead").transform;
            riderHead.SetParent(headParent, false);
            if (rig != null && visual != null)
            {
                rigSeatLocal = headParent.InverseTransformPoint(visual.transform.TransformPoint(rig.SeatContact));
                hasSeat = true;
            }
            else
            {
                hasSeat = visual != null && TryMeasureParts(visual.transform, headParent,
                    n => n.StartsWith(seatPartPrefix, StringComparison.OrdinalIgnoreCase), out seatBounds);
            }
            if (hasSeat)
            {
                Debug.Log($"[MotorcycleController] {(rig != null ? rig.Name : "Modèle mesuré")} : assise à {SeatContactPoint.y:0.00} m, " +
                    $"empattement {wheelbase:0.00} m.", this);
            }
            else if (autoFirstPersonEye)
            {
                Debug.LogWarning($"[MotorcycleController] Aucune pièce \"{seatPartPrefix}*\" dans le modèle : position des yeux manuelle.", this);
            }
            riderHead.localPosition = RiderEyePosition();
            riderHead.localRotation = Quaternion.identity;

            SpawnRider(visual);
        }

        /// <summary>Colliders et corps rigides du modèle neutralisés : c'est le CharacterController qui porte la moto.</summary>
        static void RemovePhysics(Transform model)
        {
            foreach (Rigidbody rb in model.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
                Destroy(rb);
            }
            // Désactivés tout de suite : Destroy n'agit qu'en fin de frame, après le calage sur la route (Start).
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;
                Destroy(c);
            }
        }

        /// <summary>
        /// Gréement d'un modèle connu : pièces techniques masquées, colonne de direction (un pivot incliné de
        /// la chasse, sous lequel passent les pièces qui braquent) et roues qui tournent avec la vitesse.
        /// </summary>
        void RigModel(Transform model)
        {
            foreach (string suffix in rig.HiddenParts)
            {
                Transform hidden = MotoRigs.Part(model, suffix);
                if (hidden != null) hidden.gameObject.SetActive(false);
            }

            var steering = new GameObject("SteeringPivot").transform;
            steering.SetParent(model, false);
            steering.localPosition = rig.SteeringPivot;
            steering.localRotation = Quaternion.Euler(-rig.SteeringRake, 0f, 0f);
            foreach (string suffix in rig.SteeringParts)
            {
                Transform part = MotoRigs.Part(model, suffix);
                if (part != null) part.SetParent(steering, true);
                else Debug.LogWarning($"[MotorcycleController] Pièce \"*{suffix}\" introuvable : elle ne tournera pas avec le guidon.", this);
            }
            handlebarPart = steering;
            handlebarRestRotation = steering.localRotation;

            frontWheel = MotoRigs.Part(model, rig.FrontWheel);
            rearWheel = MotoRigs.Part(model, rig.RearWheel);
            if (frontWheel != null) frontWheelRest = frontWheel.localRotation;
            if (rearWheel != null) rearWheelRest = rearWheel.localRotation;

            // Après la colonne de direction : le faisceau du phare, rattaché à l'optique, tourne avec le guidon.
            gameObject.AddComponent<MotoLights>().Setup(this, model, rig, lightMaterial);
        }

        /// <summary>Honda C125 : le prefab n'a pas d'Animator, guidon, freins et fourche sont posés par le code (LateUpdate).</summary>
        void RigC125(Transform model)
        {
            handlebarPart = FindPart(model, handlebarPartName);
            if (handlebarPart != null) handlebarRestRotation = handlebarPart.localRotation;
            else Debug.LogWarning($"[MotorcycleController] Pièce \"{handlebarPartName}\" introuvable : le guidon ne tournera pas.", this);

            // Poses complètes relevées dans les clips d'origine de la Honda C125
            // (Animations/front brake, rear wheel brake, front damper).
            frontBrakeParts = new[]
            {
                Pose(model, "C125_handle/locator___handle/axis___handle/F_5/front_brake/axis___front_brake", euler: new Vector3(0f, 27f, 0f)),
            };
            rearBrakeParts = new[]
            {
                Pose(model, "F_29/axis___pedal", euler: new Vector3(0f, 0f, -10f)),
                Pose(model, "swing_arm/axis_swing_arm/locator/F_18/axis___brake_cam", euler: new Vector3(15f, 0f, 0f)),
                Pose(model, "swing_arm/axis_swing_arm/locator/locator___inverse/locator___brake_rod", position: new Vector3(0f, 0.004f, -0.015f)),
            };
            forkParts = new[]
            {
                Pose(model, "C125_handle/locator___handle/axis___handle/front_damper", position: new Vector3(0f, 0.035f, 0f)),
                Pose(model, "C125_handle/locator___handle/axis___handle/front_damper_scale", scale: new Vector3(1f, 0.915f, 1f)),
            };
        }

        /// <summary>Pilote humanoïde assis sur la selle ; bras, mains et doigts sont posés par MotoRider.</summary>
        void SpawnRider(GameObject visual)
        {
            if (riderPrefab == null) return;
            if (visual == null || handlebarPart == null || !hasSeat)
            {
                Debug.LogWarning("[MotorcycleController] Pilote non placé : il faut le modèle de moto, sa selle et son guidon.", this);
                return;
            }

            GameObject rider = Instantiate(riderPrefab, riderHead.parent);
            rider.name = "Rider";
            rider.transform.localPosition = SeatContactPoint;
            rider.transform.localRotation = Quaternion.identity;

            Animator riderAnim = rider.GetComponentInChildren<Animator>();
            if (riderAnim != null)
            {
                if (riderAnimator != null) riderAnim.runtimeAnimatorController = riderAnimator;
                riderAnim.applyRootMotion = false;
                // Toujours animé : en vue 1re personne le corps sort du champ, mais les bras doivent suivre.
                riderAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            rider.AddComponent<MotoRider>().Setup(this, visual.transform, riderAnim);
        }

        /// <summary>Yeux de la vue 1re personne : ancrés au buste du pilote dès qu'il y en a un (voir MotoRider).</summary>
        Vector3 RiderEyePosition() => riderEyeOverride ?? DefaultEyePosition();

        /// <summary>Position des yeux dans l'espace de la tête (pivot visuel) : calculée sur la selle, sinon manuelle.</summary>
        internal Vector3 DefaultEyePosition()
        {
            if (autoFirstPersonEye && hasSeat)
            {
                // Hanches posées sur l'avant de la selle, buste penché vers les poignées.
                float lean = riderTorsoLean * Mathf.Deg2Rad;
                return SeatContactPoint + new Vector3(0f, Mathf.Cos(lean) * sittingEyeHeight, Mathf.Sin(lean) * sittingEyeHeight);
            }

            // Le pivot ne fait que tourner sur place : l'offset MotoRoot se convertit par simple soustraction.
            return firstPersonEyeOffset - (visualPivot != null ? visualPivot.localPosition : Vector3.zero);
        }

        internal static Transform FindPart(Transform root, string partName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, partName, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return null;
        }

        /// <summary>Pièce dont la hiérarchie se termine par <paramref name="path"/>, chemin tel qu'écrit dans les clips.</summary>
        static Transform FindPath(Transform root, string path)
        {
            string[] names = path.Split('/');
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                Transform current = t;
                int i = names.Length - 1;
                while (i >= 0 && current != null && current.name == names[i])
                {
                    current = current.parent;
                    i--;
                }
                if (i < 0) return t;
            }
            return null;
        }

        PosedPart Pose(Transform model, string path, Vector3? position = null, Vector3? euler = null, Vector3? scale = null)
        {
            Transform part = FindPath(model, path);
            if (part != null) return new PosedPart(part, position, euler, scale);
            Debug.LogWarning($"[MotorcycleController] Pièce \"{path}\" introuvable : elle ne sera pas animée.", this);
            return null;
        }

        /// <summary>Boîte englobante des pièces du modèle dont le nom passe <paramref name="match"/>, dans l'espace de <paramref name="space"/>.</summary>
        internal static bool TryMeasureParts(Transform model, Transform space, Func<string, bool> match, out Bounds seat)
        {
            seat = default;
            bool found = false;

            foreach (Transform part in model.GetComponentsInChildren<Transform>())
            {
                if (!match(part.name)) continue;

                foreach (Renderer r in part.GetComponentsInChildren<Renderer>())
                {
                    Bounds local = r.localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = local.center + Vector3.Scale(local.extents,
                            new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                        if (found) seat.Encapsulate(p);
                        else seat = new Bounds(p, Vector3.zero);
                        found = true;
                    }
                }
            }

            return found;
        }

        void Start()
        {
            if (!spawnOnRoad || !TryPlaceOnRoad()) SnapToGround();
        }

        /// <summary>Départ au milieu d'une route droite proche, dans le sens de la plus longue ligne droite.</summary>
        bool TryPlaceOnRoad()
        {
            // Les colliders de la ville viennent d'être créés (CityColliderBaker, en Awake) : synchronisés avant de sonder.
            Physics.SyncTransforms();
            if (!RoadSpawnFinder.TryFind(transform.position, roadMaterialKeyword, roadSearchRadius, body, out RoadSpawn spawn))
            {
                Debug.LogWarning("[MotorcycleController] Aucune route trouvée autour du point de départ : départ sur place.", this);
                return false;
            }

            body.enabled = false;
            transform.SetPositionAndRotation(spawn.Position + Vector3.up * 0.05f, Quaternion.Euler(0f, spawn.Yaw, 0f));
            body.enabled = true;
            Debug.Log($"[MotorcycleController] Départ au milieu de la route : chaussée de {spawn.Width:0} m, " +
                $"{spawn.StraightLength:0} m de ligne droite devant.", this);
            return true;
        }

        public void SetThrottle(bool held) => touchThrottle = held;
        public void SetLift(bool held) => touchLift = held;
        public void SetBrake(bool held) => touchBrake = held;
        public void SetSteer(float value) => touchSteer = Mathf.Clamp(value, -1f, 1f);

        void Update()
        {
            float dt = Time.deltaTime;
            ReadKeyboard();

            if (isFallen)
            {
                UpdateFall(dt);
                return;
            }

            UpdateSpeed(dt);
            UpdateWheelie(dt);
            if (!isFallen) UpdateStoppie(dt);
            if (isFallen) return;

            UpdateSteering(dt);
            ApplyMovement(dt);
            UpdateWheelSpin(dt);
            UpdateGroundAlignment(dt);
            UpdateVisual();
            UpdateHandlebar(dt);
            UpdateBrakesAndFork(dt);
        }

        /// <summary>Pose des pièces mobiles du modèle, après un éventuel Animator qui les réécrirait.</summary>
        void LateUpdate()
        {
            if (handlebarPart != null)
            {
                handlebarPart.localRotation = handlebarRestRotation * Quaternion.Euler(0f, handlebarAngle, 0f);
            }
            // Roues : rotation autour de leur axe (X local du maillage), après la colonne de direction pour l'avant.
            if (frontWheel != null) frontWheel.localRotation = frontWheelRest * Quaternion.Euler(wheelSpin, 0f, 0f);
            if (rearWheel != null) rearWheel.localRotation = rearWheelRest * Quaternion.Euler(wheelSpin, 0f, 0f);
            ApplyPose(frontBrakeParts, frontBrakeWeight);
            ApplyPose(rearBrakeParts, rearBrakeWeight);
            ApplyPose(forkParts, forkCompression);
        }

        /// <summary>Clavier pour tester sur PC : ZQSD/WASD ou flèches ; espace = LEVER.</summary>
        void ReadKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            keyThrottle = kb.wKey.isPressed || kb.zKey.isPressed || kb.upArrowKey.isPressed;
            keyBrake = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            keyLift = kb.spaceKey.isPressed;

            bool left = kb.aKey.isPressed || kb.qKey.isPressed || kb.leftArrowKey.isPressed;
            bool right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            keySteer = (right ? 1f : 0f) - (left ? 1f : 0f);
#else
            keyThrottle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow);
            keyBrake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            keyLift = Input.GetKey(KeyCode.Space);

            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            keySteer = (right ? 1f : 0f) - (left ? 1f : 0f);
#endif
        }

        void UpdateSpeed(float dt)
        {
            if (Brake)
            {
                if (currentSpeed > 0.05f)
                {
                    // Roue avant en l'air : seul le frein arrière porte, le freinage est bien plus faible.
                    // Sur la roue avant, il est adouci pour laisser le temps de doser l'équilibre.
                    float strength = brakingDeceleration * (wheelieAngle > 1f ? brakeFactorDuringWheelie : 1f);
                    strength = Mathf.Lerp(strength, stoppieBrakeDeceleration, RearWheelUp());
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, strength * dt);
                }
                else if (wheelieAngle < 1f && stoppieAngle < 1f)
                {
                    // À l'arrêt, maintenir le frein fait reculer lentement.
                    currentSpeed = Mathf.MoveTowards(currentSpeed, -reverseMaxSpeed, reverseAcceleration * dt);
                }
            }
            else if (Accelerate)
            {
                // En marche arrière, les gaz commencent par arrêter la moto. Avec LEVER, gaz en grand.
                float rate = currentSpeed < 0f ? brakingDeceleration : acceleration * (Lift ? liftAccelerationBoost : 1f);
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, rate * dt);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, naturalDeceleration * dt);
            }
        }

        void UpdateWheelie(float dt)
        {
            if (!Lift) wheelieNeedsRelease = false;
            bool fastEnough = currentSpeed >= wheelieMinSpeed;
            bool critical = wheelieAngle > wheelieSweetMax;

            float torque = WheelieGravityTorque(wheelieAngle);

            if (wheelieAngle > 1f)
            {
                torque += WheelieBumpTorque();
            }

            if (Lift && !wheelieNeedsRelease && fastEnough && stoppieAngle <= 0f)
            {
                torque += WheelieLiftTorqueAt(wheelieAngle);
            }

            if (!fastEnough && wheelieAngle > 0f)
            {
                torque -= stalledPull;
            }

            if (Brake)
            {
                if (critical)
                {
                    // Impact immédiat : la montée est stoppée net, puis le couple rabat franchement la roue.
                    if (wheelieAngularVelocity > 0f) wheelieAngularVelocity = 0f;
                    torque -= criticalBrakeTorque;
                }
                else
                {
                    torque -= wheelieBrakeTorque;
                }
            }

            float previousAngle = wheelieAngle;
            wheelieAngularVelocity += torque * dt;
            wheelieAngularVelocity -= wheelieAngularVelocity * wheelieDamping * dt;
            wheelieAngle += wheelieAngularVelocity * dt;

            if (wheelieAngle <= 0f)
            {
                // La roue avant retombe : la fourche encaisse le choc, d'autant plus fort que la chute est rapide.
                if (previousAngle > 0f && wheelieAngularVelocity < 0f)
                {
                    forkVelocity -= wheelieAngularVelocity * forkLandingKick;
                }
                wheelieAngle = 0f;
                if (wheelieAngularVelocity < 0f) wheelieAngularVelocity = 0f;
            }

            if (wheelieAngle >= wheelieFallAngle)
            {
                TriggerFall(false);
            }
        }

        /// <summary>Forte pour décoller la roue, modérée une fois dans la zone d'équilibre, pour pouvoir doser.</summary>
        float WheelieLiftTorqueAt(float angle)
        {
            float blend = Mathf.InverseLerp(wheelieSweetMin * 0.5f, wheelieSweetMin, angle);
            return Mathf.Lerp(wheelieLiftTorque, wheelieHoldTorque, blend);
        }

        /// <summary>Bosses de la route : bruit continu, plus marqué à haute vitesse.</summary>
        float WheelieBumpTorque()
        {
            float noise = Mathf.PerlinNoise(Time.time * wheelieBumpFrequency, BumpNoiseSeed) * 2f - 1f;
            float speedFactor = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(currentSpeed / Mathf.Max(0.1f, maxSpeed)));
            return noise * wheelieBumpTorque * speedFactor;
        }

        /// <summary>
        /// Couple de gravité selon l'angle, continu aux bornes des zones :
        /// négatif sous la zone d'équilibre (la roue retombe), quasi nul dedans (tenue au dosage),
        /// positif et croissant au-delà (la moto part en arrière).
        /// </summary>
        float WheelieGravityTorque(float angle)
        {
            if (angle < wheelieSweetMin)
            {
                float t = (wheelieSweetMin - angle) / Mathf.Max(1f, wheelieSweetMin);
                return -Mathf.Lerp(sweetSpotDrift, lowAnglePull, t);
            }

            if (angle <= wheelieSweetMax)
            {
                float t = (angle - wheelieSweetMin) / Mathf.Max(1f, wheelieSweetMax - wheelieSweetMin);
                return Mathf.Lerp(-sweetSpotDrift, sweetSpotDrift, t);
            }

            float over = (angle - wheelieSweetMax) / Mathf.Max(1f, wheelieFallAngle - wheelieSweetMax);
            return Mathf.Lerp(sweetSpotDrift, criticalPush, Mathf.Clamp01(over));
        }

        /// <summary>
        /// Roue avant (stoppie), sur le modèle du wheeling. FREIN tenu à bonne vitesse bascule la moto vers
        /// l'avant, d'autant plus fort qu'elle roule vite, puis tient modérément une fois dans la zone
        /// d'équilibre ; le relâcher repose l'arrière. Les bosses perturbent l'angle, et trop lente, la moto
        /// ne tient plus : l'arrière retombe. Au-delà de la zone d'équilibre, rien ne la rattrape.
        /// </summary>
        void UpdateStoppie(float dt)
        {
            float torque;
            if (stoppieAngle > stoppieSweetMax)
            {
                // Passé le point d'équilibre, aucune commande n'agit : la moto passe par-dessus la roue avant.
                float over = Mathf.InverseLerp(stoppieSweetMax, stoppieFallAngle, stoppieAngle);
                torque = Mathf.Lerp(stoppieCriticalPush * 0.5f, stoppieCriticalPush, over);
            }
            else
            {
                torque = StoppieGravityTorque(stoppieAngle);

                bool canLift = wheelieAngle <= 0f && currentSpeed >= stoppieMinSpeed;
                if (Brake && canLift)
                {
                    float speedFactor = Mathf.InverseLerp(stoppieMinSpeed, maxSpeed, currentSpeed);
                    float blend = Mathf.InverseLerp(stoppieSweetMin * 0.5f, stoppieSweetMin, stoppieAngle);
                    torque += Mathf.Lerp(stoppieLiftTorque * speedFactor, stoppieHoldTorque, blend);
                }
                else if (stoppieAngle > 0f)
                {
                    // Frein relâché, l'arrière se repose ; trop lente, la moto ne tient plus sur la roue avant.
                    torque -= Brake ? stalledPull : stoppieReleaseTorque;
                }

                if (stoppieAngle > 1f)
                {
                    torque += WheelieBumpTorque();
                }
            }

            stoppieAngularVelocity += torque * dt;
            stoppieAngularVelocity -= stoppieAngularVelocity * wheelieDamping * dt;
            stoppieAngle += stoppieAngularVelocity * dt;

            if (stoppieAngle <= 0f)
            {
                stoppieAngle = 0f;
                if (stoppieAngularVelocity < 0f) stoppieAngularVelocity = 0f;
            }

            if (stoppieAngle >= stoppieFallAngle)
            {
                TriggerFall(true);
            }
        }

        /// <summary>Couple de gravité sur la roue avant : rappel vers le sol sous la zone d'équilibre, dérive faible dedans.</summary>
        float StoppieGravityTorque(float angle)
        {
            if (angle < stoppieSweetMin)
            {
                float t = (stoppieSweetMin - angle) / Mathf.Max(1f, stoppieSweetMin);
                return -Mathf.Lerp(stoppieDrift, stoppieLowPull, t);
            }

            float inZone = (angle - stoppieSweetMin) / Mathf.Max(1f, stoppieSweetMax - stoppieSweetMin);
            return Mathf.Lerp(-stoppieDrift, stoppieDrift, inZone);
        }

        /// <summary>
        /// En marche avant, l'entrée pilote une inclinaison cible, limitée à basse vitesse ; la moto la prend
        /// progressivement (ressort critique amorti, plus lent à haute vitesse), et le virage découle de
        /// l'angle réellement pris : w = g*tan(lean)/v, borné par la butée de guidon au pas.
        /// Dès que la roue avant quitte le sol, le guidon n'a plus d'effet : on ne dirige qu'avec le poids
        /// du corps, d'où une autorité réduite et une bascule plus lente.
        /// En marche arrière, pas d'inclinaison : direction directe, inversée comme tout véhicule qui recule.
        /// </summary>
        void UpdateSteering(float dt)
        {
            if (currentSpeed < -0.05f)
            {
                currentLean = Mathf.SmoothDamp(currentLean, 0f, ref leanVelocity, leanSmoothTimeLowSpeed, maxLeanRate, dt);
                float reverseFactor = Mathf.Clamp01(-currentSpeed / Mathf.Max(0.01f, reverseMaxSpeed));
                currentYawRate = -Steer * reverseTurnRate * reverseFactor;
                transform.Rotate(Vector3.up, currentYawRate * dt);
                return;
            }

            float frontWheelUp = FrontWheelUp();
            float speedFactor = Mathf.Clamp01(currentSpeed / Mathf.Max(0.01f, leanFullSpeed));
            float wheelieFactor = Mathf.Lerp(1f, wheelieSteerAuthority, frontWheelUp)
                * Mathf.Lerp(1f, stoppieSteerAuthority, RearWheelUp());
            float targetLean = -Steer * maxLeanAngle * speedFactor * wheelieFactor;

            float smoothTime = Mathf.Lerp(leanSmoothTimeLowSpeed, leanSmoothTimeHighSpeed,
                Mathf.Clamp01(currentSpeed / Mathf.Max(0.1f, maxSpeed)));
            smoothTime *= Mathf.Lerp(1f, wheelieLeanSlowdown, frontWheelUp);
            currentLean = Mathf.SmoothDamp(currentLean, targetLean, ref leanVelocity, smoothTime, maxLeanRate, dt);

            if (currentSpeed < 0.2f)
            {
                currentYawRate = 0f;
                return;
            }

            // Au pas, le rayon ne peut pas descendre sous celui permis par la butée de guidon.
            float lockLimit = currentSpeed * Mathf.Tan(maxSteeringLock * Mathf.Deg2Rad) / Mathf.Max(0.1f, wheelbase) * Mathf.Rad2Deg;
            float limit = Mathf.Min(maxYawRate, lockLimit);
            float yawRate = GravityConstant * Mathf.Tan(currentLean * Mathf.Deg2Rad) / currentSpeed * Mathf.Rad2Deg;
            currentYawRate = -Mathf.Clamp(yawRate, -limit, limit);
            // Roue levée, le pilote fait pivoter la moto au poids du corps : de quoi tourner à angle droit.
            currentYawRate += Steer * wheelieYawAssist * frontWheelUp * Mathf.Clamp01(currentSpeed / 3f);
            currentYawRate = Mathf.Clamp(currentYawRate, -maxYawRate, maxYawRate);
            transform.Rotate(Vector3.up, currentYawRate * dt);
        }

        void ApplyMovement(float dt)
        {
            if (body.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }
            else
            {
                verticalSpeed += gravity * dt;
            }

            LimitAgainstWalls(dt);
            Vector3 motion = transform.forward * currentSpeed + Vector3.up * verticalSpeed;
            CollisionFlags flags = body.Move(motion * dt);

            if ((flags & CollisionFlags.Sides) != 0)
            {
                currentSpeed = Mathf.Clamp(currentSpeed, -wallImpactSpeed, wallImpactSpeed);
            }
        }

        /// <param name="forward">Vrai pour une chute par-dessus la roue avant, faux pour un basculement arrière.</param>
        void TriggerFall(bool forward)
        {
            isFallen = true;
            fallForward = forward;
            fallTimer = fallAnimationTime;
            currentSpeed = 0f;
            wheelieAngularVelocity = 0f;
            stoppieAngularVelocity = 0f;
            Fell?.Invoke();
        }

        /// <summary>Court basculement visible, puis remise à plat immédiate sur place.</summary>
        void UpdateFall(float dt)
        {
            if (fallForward) stoppieAngle = Mathf.MoveTowards(stoppieAngle, fallVisualAngle, fallRotateSpeed * dt);
            else wheelieAngle = Mathf.MoveTowards(wheelieAngle, fallVisualAngle, fallRotateSpeed * dt);
            UpdateVisual();
            ApplyMovement(dt);

            fallTimer -= dt;
            if (fallTimer <= 0f)
            {
                Respawn();
            }
        }

        /// <summary>
        /// La moto reste où elle est, remise à plat : le CharacterController n'a jamais quitté le sol
        /// (la chute est purement visuelle), donc la caméra qui le suit n'est pas perturbée.
        /// </summary>
        void Respawn()
        {
            isFallen = false;
            wheelieAngle = 0f;
            wheelieAngularVelocity = 0f;
            stoppieAngle = 0f;
            stoppieAngularVelocity = 0f;
            currentLean = 0f;
            leanVelocity = 0f;
            currentYawRate = 0f;
            handlebarAngle = 0f;
            handlebarVelocity = 0f;
            forkCompression = 0f;
            forkVelocity = 0f;
            currentSpeed = respawnSpeed;
            wheelieNeedsRelease = true;
            UpdateVisual();
        }

        void UpdateVisual()
        {
            // Recalculée à chaque frame : les réglages du pilote se testent en jeu, sans relancer.
            if (riderHead != null) riderHead.localPosition = RiderEyePosition();
            if (visualPivot == null) return;
            // Pivot posé sur le sol sous le pneu arrière, et hauteur lissée : les à-coups du contrôleur disparaissent.
            float heightLag = heightInitialized ? smoothedHeight - transform.position.y : 0f;
            Vector3 rest = pivotRestPosition + Vector3.up * (rearGroundOffset + heightLag);

            if (stoppieAngle > 0f)
            {
                // Sur la roue avant, la moto bascule autour du contact du pneu avant, posé sur la pente.
                Vector3 frontContact = new Vector3(0f, 0f, wheelbase);
                visualPivot.localPosition = rest + NoseUp(groundPitch) * frontContact - NoseUp(groundPitch - stoppieAngle) * frontContact;
                visualPivot.localRotation = Quaternion.Euler(-(groundPitch - stoppieAngle), 0f, currentLean);
            }
            else
            {
                visualPivot.localPosition = rest;
                visualPivot.localRotation = Quaternion.Euler(-(groundPitch + wheelieAngle), 0f, currentLean);
            }
        }

        /// <summary>Pose la moto sur la première surface sous elle, pour ne jamais démarrer coincé dans un bâtiment.</summary>
        void SnapToGround()
        {
            Vector3 origin = transform.position + Vector3.up * groundProbeHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeHeight * 2f))
            {
                body.enabled = false;
                transform.position = hit.point + Vector3.up * 0.05f;
                body.enabled = true;
            }
        }

        /// <summary>Repère du point de départ dans la vue Scene : cercle et flèche dans la direction de départ.</summary>
        void OnDrawGizmos()
        {
            if (spawnPoint == null) return;

            Vector3 origin = spawnPoint.position;
            Vector3 forward = Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 tip = origin + forward * 2f;

            Gizmos.color = new Color(0.25f, 0.9f, 0.35f);
            Gizmos.DrawWireSphere(origin, 0.5f);
            Gizmos.DrawLine(origin, tip);
            Gizmos.DrawLine(tip, tip - forward * 0.5f + right * 0.35f);
            Gizmos.DrawLine(tip, tip - forward * 0.5f - right * 0.35f);
        }

        /// <summary>Part de la roue avant en l'air : 0 roue au sol, 1 au-delà de l'angle de réduction de direction.</summary>
        float FrontWheelUp()
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Mathf.Max(0.1f, wheelieSteerFadeAngle), wheelieAngle));
        }

        /// <summary>Part de la roue arrière en l'air (roue avant) : 0 au sol, 1 au-delà de quelques degrés.</summary>
        float RearWheelUp()
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, StoppieLiftFadeAngle, stoppieAngle));
        }

        /// <summary>
        /// Angle de guidon. En roulant, il découle du virage réellement pris (braquage géométrique
        /// atan(empattement / rayon)), plus un braquage visible proportionnel à l'inclinaison, pour que le
        /// guidon suive nettement la courbe. S'y ajoute un bref contre-braquage : pour pencher à droite,
        /// on pousse d'abord le guidon vers la gauche.
        /// Roue avant en l'air, le guidon part dans le sens opposé au virage (le pilote dirige au poids du corps).
        /// À l'arrêt, le pilote tourne le guidon selon l'entrée ; en marche arrière aussi.
        /// </summary>
        void UpdateHandlebar(float dt)
        {
            float speed = Mathf.Abs(currentSpeed);
            float target;

            if (currentSpeed < -0.05f)
            {
                target = Steer * maxSteeringLock * 0.6f;
            }
            else
            {
                float kinematic = speed > 0.2f
                    ? Mathf.Atan(wheelbase * currentYawRate * Mathf.Deg2Rad / speed) * Mathf.Rad2Deg
                    : 0f;
                // Inclinaison négative = virage à droite = guidon à droite (angle positif).
                float leanTurn = -currentLean / Mathf.Max(1f, maxLeanAngle) * handlebarLeanAngle;
                float countersteer = countersteerGain * leanVelocity * Mathf.Clamp01((speed - 3f) / 5f);
                float manoeuvre = Steer * maxSteeringLock;
                float ground = Mathf.Lerp(manoeuvre, kinematic + leanTurn + countersteer, Mathf.Clamp01(speed / 1.5f));

                float wheelie = -Steer * handlebarWheelieAngle;
                target = Mathf.Lerp(ground, wheelie, FrontWheelUp());
            }

            target = Mathf.Clamp(target, -maxSteeringLock, maxSteeringLock);
            handlebarAngle = Mathf.SmoothDamp(handlebarAngle, target, ref handlebarVelocity, handlebarSmoothTime, HandlebarMaxRate, dt);
        }

        /// <summary>
        /// Freins, fourche et poignée de gaz. Levier et pédale suivent le frein (les doigts du pilote tirent le levier).
        /// La fourche est un ressort amorti : elle plonge au freinage roue au sol, encaisse le retour de la roue avant
        /// (impulsion donnée dans UpdateWheelie) et se détend un peu roue en l'air.
        /// </summary>
        void UpdateBrakesAndFork(float dt)
        {
            bool braking = Brake && currentSpeed > -0.05f;
            float frontWheelUp = FrontWheelUp();
            float rate = brakeLeverSpeed * dt;
            frontBrakeWeight = Mathf.MoveTowards(frontBrakeWeight, braking ? 1f : 0f, rate);
            rearBrakeWeight = Mathf.MoveTowards(rearBrakeWeight, braking ? 1f : 0f, rate);
            throttle = Mathf.MoveTowards(throttle, Lift ? 1f : Accelerate ? 0.6f : 0f, ThrottleRate * dt);

            float dive = forkBrakeDive * frontBrakeWeight * Mathf.Clamp01(currentSpeed / 3f);
            float target = Mathf.Lerp(dive, -forkWheelieExtension, frontWheelUp);
            // Sur la roue avant, tout le poids est sur la fourche : compression complète.
            target = Mathf.Lerp(target, 1f, RearWheelUp());
            forkVelocity += ((target - forkCompression) * forkStiffness - forkVelocity * forkDamping) * dt;
            forkCompression += forkVelocity * dt;
            if (forkCompression > 1f || forkCompression < -forkWheelieExtension)
            {
                // Fin de course : la fourche talonne ou arrive en détente complète.
                forkCompression = Mathf.Clamp(forkCompression, -forkWheelieExtension, 1f);
                forkVelocity = 0f;
            }
        }

        static Quaternion NoseUp(float degrees) => Quaternion.Euler(-degrees, 0f, 0f);

        /// <summary>
        /// Assiette sur les pentes : hauteur du sol sous chaque pneu, d'où l'inclinaison de la moto et la hauteur du
        /// pneu arrière (pivot du wheeling). Lissées, comme la hauteur du contrôleur, pour une image sans tremblement.
        /// </summary>
        void UpdateGroundAlignment(float dt)
        {
            Vector3 rear = transform.TransformPoint(0f, 0f, rearContactZ);
            Vector3 front = transform.TransformPoint(0f, 0f, rearContactZ + wheelbase);
            float targetPitch = 0f;
            float targetOffset = 0f;
            if (body.isGrounded && GroundHeight(rear, out float rearY) && GroundHeight(front, out float frontY))
            {
                targetPitch = Mathf.Clamp(Mathf.Atan2(frontY - rearY, wheelbase) * Mathf.Rad2Deg, -maxGroundPitch, maxGroundPitch);
                // À plat, le sol est à une skin width sous MotoRoot : décalage nul.
                targetOffset = Mathf.Clamp(rearY - transform.position.y + body.skinWidth, -0.6f, 0.6f);
            }
            groundPitch = Mathf.SmoothDamp(groundPitch, targetPitch, ref groundPitchVelocity, groundAlignSmoothTime, Mathf.Infinity, dt);
            rearGroundOffset = Mathf.SmoothDamp(rearGroundOffset, targetOffset, ref rearGroundOffsetVelocity, groundAlignSmoothTime, Mathf.Infinity, dt);

            float y = transform.position.y;
            if (!heightInitialized)
            {
                smoothedHeight = y;
                heightInitialized = true;
            }
            smoothedHeight = Mathf.SmoothDamp(smoothedHeight, y, ref smoothedHeightVelocity, heightSmoothTime, Mathf.Infinity, dt);
            // Au-delà de quelques centimètres (descente d'un trottoir), la moto suit sans retard.
            smoothedHeight = Mathf.Clamp(smoothedHeight, y - 0.15f, y + 0.15f);
        }

        /// <summary>Hauteur du sol sous un point, sans compter la capsule de la moto.</summary>
        bool GroundHeight(Vector3 point, out float y)
        {
            y = 0f;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.8f, Vector3.down, probeHits, 2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (probeHits[i].collider == body || probeHits[i].distance >= nearest) continue;
                nearest = probeHits[i].distance;
                y = probeHits[i].point.y;
            }
            return nearest < float.MaxValue;
        }

        /// <summary>
        /// La capsule du contrôleur ne couvre que le milieu de la moto : devant (roue avant) et derrière, un sondage
        /// arrête la moto contre les murs. Il passe au-dessus de la hauteur de trottoir, qui reste franchissable.
        /// </summary>
        void LimitAgainstWalls(float dt)
        {
            if (Mathf.Abs(currentSpeed) < 0.01f) return;

            float sign = Mathf.Sign(currentSpeed);
            float travel = Mathf.Abs(currentSpeed) * dt;
            float overhang = Mathf.Max(0f, wheelbase * 0.5f + 0.3f - colliderRadius);
            Vector3 origin = transform.position + Vector3.up * (stepHeight + wallProbeRadius + 0.05f);
            int count = Physics.SphereCastNonAlloc(origin, wallProbeRadius, transform.forward * sign, probeHits,
                colliderRadius + overhang + travel, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = probeHits[i];
                if (hit.collider == body || hit.distance <= 0f) continue;
                // Une pente gravissable n'est pas un mur.
                if (Vector3.Angle(hit.normal, Vector3.up) < maxClimbSlope) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            if (nearest == float.MaxValue) return;

            float free = Mathf.Max(0f, nearest - colliderRadius - overhang);
            if (free < travel)
            {
                // Contact : la moto s'arrête contre le mur au lieu de le traverser ou de monter dessus.
                currentSpeed = free <= 0.01f ? 0f : sign * free / Mathf.Max(dt, 0.0001f);
            }
        }

        /// <summary>Tour de roue : distance parcourue divisée par le rayon réel.</summary>
        void UpdateWheelSpin(float dt)
        {
            if (rig == null) return;
            float radius = Mathf.Max(0.05f, rig.WheelRadius * rig.Scale);
            wheelSpin = Mathf.Repeat(wheelSpin + currentSpeed / radius * Mathf.Rad2Deg * dt, 360f);
        }

        static void ApplyPose(PosedPart[] parts, float weight)
        {
            if (parts == null) return;
            foreach (PosedPart part in parts)
            {
                part?.Apply(weight);
            }
        }

        /// <summary>
        /// Pièce du modèle animée par le code, interpolée entre sa pose de repos (poids 0) et la pose complète
        /// d'un clip d'origine (poids 1). Hors de [0, 1], la pose est extrapolée (détente de la fourche).
        /// </summary>
        sealed class PosedPart
        {
            readonly Transform part;
            readonly Vector3 restPosition, posedPosition;
            readonly Quaternion restRotation, posedRotation;
            readonly Vector3 restScale, posedScale;

            public PosedPart(Transform part, Vector3? position, Vector3? euler, Vector3? scale)
            {
                this.part = part;
                restPosition = part.localPosition;
                restRotation = part.localRotation;
                restScale = part.localScale;
                posedPosition = position ?? restPosition;
                posedRotation = euler.HasValue ? Quaternion.Euler(euler.Value) : restRotation;
                posedScale = scale ?? restScale;
            }

            public void Apply(float weight)
            {
                part.localPosition = Vector3.LerpUnclamped(restPosition, posedPosition, weight);
                part.localRotation = Quaternion.SlerpUnclamped(restRotation, posedRotation, weight);
                part.localScale = Vector3.LerpUnclamped(restScale, posedScale, weight);
            }
        }
    }
}
