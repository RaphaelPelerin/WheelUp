using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    /// FREIN freine, rabat la roue avant en wheeling, et fait reculer une fois à l'arrêt.
    ///
    /// Deux mécaniques sont simulées à part, sur le pivot visuel :
    /// - le wheeling, autour du point de contact du pneu arrière, en trois zones : sous la zone
    ///   d'équilibre la gravité rabat la roue ; dans la zone elle ne dérive que faiblement, mais les
    ///   bosses de la route perturbent l'angle en permanence (tenue au dosage LEVER / FREIN) ;
    ///   au-delà, la moto part en arrière et seul le frein la rattrape ;
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
        public float firstPersonHeadPitch = 8f;

        [Header("Pilote (personnage)")]
        [Tooltip("Modèle humanoïde du pilote (SKM_Mannequin du pack MotoInteractionAnims). Vide : pas de pilote.")]
        public GameObject riderPrefab;
        [Tooltip("Contrôleur d'animation de la position de conduite (AS_Idle_Riding).")]
        public RuntimeAnimatorController riderAnimator;

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

        CharacterController body;
        Transform riderHead;
        Transform handlebarPart;
        Quaternion handlebarRestRotation;
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
        bool isFallen;
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
        internal Vector3 SeatContactPoint => new Vector3(seatBounds.center.x, seatBounds.max.y, seatBounds.max.z - hipsBehindSeatFront);
        /// <summary>Vitesse de cabrage, en degrés par seconde (positive quand la roue monte).</summary>
        internal float WheelieAngularVelocity => wheelieAngularVelocity;
        /// <summary>Compression de la fourche (négative en détente).</summary>
        internal float ForkCompression => forkCompression;
        /// <summary>Direction demandée, de -1 (gauche) à 1 (droite).</summary>
        internal float SteerInput => Steer;
        /// <summary>Part de la roue avant en l'air, de 0 à 1.</summary>
        internal float FrontWheelLift => FrontWheelUp();

        /// <summary>Yeux de la vue 1re personne fournis par le pilote (ancrés à son buste), dans l'espace du pivot visuel.</summary>
        internal void SetRiderEye(Vector3 localEye) => riderEyeOverride = localEye;

        public WheelieZone CurrentWheelieZone
        {
            get
            {
                if (isFallen || wheelieAngle > wheelieSweetMax) return WheelieZone.Critical;
                if (wheelieAngle >= wheelieSweetMin) return WheelieZone.Balance;
                if (wheelieAngle >= 1f) return WheelieZone.Rising;
                return WheelieZone.Flat;
            }
        }

        void Awake()
        {
            if (!TryGetComponent(out body))
            {
                body = gameObject.AddComponent<CharacterController>();
            }
            body.height = colliderHeight;
            body.radius = colliderRadius;
            body.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
            body.slopeLimit = 50f;
            body.stepOffset = 0.4f;

            GameObject visual = null;
            if (visualPivot != null)
            {
                // Pivot du wheeling posé sur le contact du pneu arrière. Le CharacterController flotte
                // de sa skin width au-dessus du sol : on la retranche pour que les pneus touchent la route.
                visualPivot.localPosition = new Vector3(0f, -body.skinWidth, rearContactZ);
                visualPivot.localRotation = Quaternion.identity;

                if (bikePrefab != null)
                {
                    visual = Instantiate(bikePrefab, visualPivot);
                    visual.transform.localPosition = new Vector3(0f, modelGroundOffset, -rearContactZ);
                    visual.transform.localRotation = Quaternion.Euler(visualEulerOffset);

                    // Le prefab n'a pas d'Animator : guidon, freins et fourche sont posés par le code (LateUpdate).
                    Transform model = visual.transform;
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
            }

            // Ancrée sous le pivot visuel : la tête suit la moto en position (cabrage et inclinaison compris).
            // L'orientation du regard est calculée par la caméra, qui garde les yeux sur la route.
            Transform headParent = visualPivot != null ? visualPivot : transform;
            riderHead = new GameObject("RiderHead").transform;
            riderHead.SetParent(headParent, false);
            hasSeat = visual != null && TryMeasureParts(visual.transform, headParent,
                n => n.StartsWith(seatPartPrefix, StringComparison.OrdinalIgnoreCase), out seatBounds);
            if (hasSeat)
            {
                Debug.Log($"[MotorcycleController] Vue 1re personne : dessus de selle à {seatBounds.max.y:0.00} m, yeux à {RiderEyePosition().y:0.00} m.", this);
            }
            else if (autoFirstPersonEye)
            {
                Debug.LogWarning($"[MotorcycleController] Aucune pièce \"{seatPartPrefix}*\" dans le modèle : position des yeux manuelle.", this);
            }
            riderHead.localPosition = RiderEyePosition();
            riderHead.localRotation = Quaternion.identity;

            SpawnRider(visual);
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
            SnapToGround();
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
            if (isFallen) return;

            UpdateSteering(dt);
            ApplyMovement(dt);
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
                    float strength = brakingDeceleration * (wheelieAngle > 1f ? brakeFactorDuringWheelie : 1f);
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, strength * dt);
                }
                else if (wheelieAngle < 1f)
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

            if (Lift && !wheelieNeedsRelease && fastEnough)
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
                TriggerFall();
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
            float wheelieFactor = Mathf.Lerp(1f, wheelieSteerAuthority, frontWheelUp);
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

            Vector3 motion = transform.forward * currentSpeed + Vector3.up * verticalSpeed;
            CollisionFlags flags = body.Move(motion * dt);

            if ((flags & CollisionFlags.Sides) != 0)
            {
                currentSpeed = Mathf.Clamp(currentSpeed, -wallImpactSpeed, wallImpactSpeed);
            }
        }

        void TriggerFall()
        {
            isFallen = true;
            fallTimer = fallAnimationTime;
            currentSpeed = 0f;
            wheelieAngularVelocity = 0f;
            Fell?.Invoke();
        }

        /// <summary>Court basculement visible, puis remise à plat immédiate sur place.</summary>
        void UpdateFall(float dt)
        {
            wheelieAngle = Mathf.MoveTowards(wheelieAngle, fallVisualAngle, fallRotateSpeed * dt);
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
            visualPivot.localRotation = Quaternion.Euler(-wheelieAngle, 0f, currentLean);
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

        /// <summary>Part de la roue avant en l'air : 0 roue au sol, 1 au-delà de l'angle de réduction de direction.</summary>
        float FrontWheelUp()
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Mathf.Max(0.1f, wheelieSteerFadeAngle), wheelieAngle));
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
            forkVelocity += ((target - forkCompression) * forkStiffness - forkVelocity * forkDamping) * dt;
            forkCompression += forkVelocity * dt;
            if (forkCompression > 1f || forkCompression < -forkWheelieExtension)
            {
                // Fin de course : la fourche talonne ou arrive en détente complète.
                forkCompression = Mathf.Clamp(forkCompression, -forkWheelieExtension, 1f);
                forkVelocity = 0f;
            }
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
