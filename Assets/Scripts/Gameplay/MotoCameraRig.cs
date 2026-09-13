using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WheelingMoto.Gameplay
{
    public enum CameraView
    {
        Exterior,
        FirstPerson
    }

    /// <summary>
    /// Caméra de la moto, deux points de vue (bouton VUE, ou touches C / V sur PC) :
    /// - Extérieure : orbite derrière la moto, pilotable au glissement. Dès qu'on roule,
    ///   elle revient progressivement derrière la moto ; en marche arrière, elle passe de l'autre côté
    ///   pour montrer la direction de déplacement. Elle se rapproche plutôt que de traverser un mur.
    /// - Première personne : comme un vrai pilote, la tête suit la moto en position (elle monte avec
    ///   le wheeling), mais le regard reste sur la route : il ne garde qu'une petite part du cabrage
    ///   et redresse une partie de l'inclinaison. Il regarde vers l'intérieur des virages ;
    ///   champ de vision et vibrations augmentent avec la vitesse ; un glissement permet de regarder autour.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MotoCameraRig : MonoBehaviour
    {
        [Tooltip("Racine de la moto (MotoRoot).")]
        public Transform target;

        [Header("Vue extérieure")]
        public float distance = 6.5f;
        public float lookHeight = 1.2f;
        public float defaultPitch = 14f;
        public float minPitch = -5f;
        public float maxPitch = 60f;
        [Tooltip("Degrés parcourus pour un glissement sur toute la hauteur de l'écran.")]
        public float orbitSensitivity = 220f;
        [Tooltip("Délai après un glissement avant que la caméra revienne derrière la moto.")]
        public float recenterDelay = 1.2f;
        [Tooltip("Temps de recentrage à pleine vitesse ; plus long à basse vitesse.")]
        public float recenterSmoothTime = 0.45f;
        [Tooltip("Vitesse (m/s) à partir de laquelle la caméra se recentre.")]
        public float recenterMinSpeed = 0.5f;
        [Tooltip("Rayon utilisé pour empêcher la caméra de traverser les murs.")]
        public float collisionRadius = 0.3f;
        public float minDistance = 0.8f;
        public float exteriorFov = 60f;
        public float exteriorFovAtMaxSpeed = 68f;

        [Header("Vue première personne")]
        [Tooltip("Champ de vision à l'arrêt : large, pour voir le réservoir, le guidon et les mains sous la route.")]
        public float firstPersonFov = 78f;
        public float firstPersonFovAtMaxSpeed = 86f;
        [Tooltip("Regard plongé en plus à l'arrêt, en degrés : on voit la moto ; il se relève vers la route en roulant.")]
        public float standstillLookDown = 16f;
        [Tooltip("Vitesse (m/s) à partir de laquelle le regard est entièrement relevé vers la route.")]
        public float lookUpSpeed = 8f;
        [Tooltip("Part du cabrage compensée par le regard : 1 = yeux toujours sur la route, 0 = regard collé à la moto (vers le ciel en wheeling).")]
        [Range(0f, 1f)]
        public float wheeliePitchCompensation = 0.85f;
        [Tooltip("Part de l'inclinaison compensée par la tête du pilote (0 = horizon collé à la moto).")]
        [Range(0f, 1f)]
        public float headLevelling = 0.5f;
        [Tooltip("Rotation de la tête vers l'intérieur du virage, par degré d'inclinaison.")]
        public float lookIntoTurn = 0.45f;
        [Tooltip("Degrés parcourus pour un glissement sur toute la hauteur de l'écran.")]
        public float freeLookSensitivity = 140f;
        public float maxFreeLookYaw = 75f;
        public float maxFreeLookPitch = 35f;
        [Tooltip("Vitesse de retour du regard vers l'avant une fois le glissement relâché.")]
        public float freeLookReturnSpeed = 4f;
        [Tooltip("Vibration moteur et route à pleine vitesse, en mètres (0 : image stable).")]
        public float speedShake = 0f;
        [Tooltip("Plan proche réduit pour voir le guidon sans qu'il soit coupé.")]
        public float firstPersonNearClip = 0.05f;

        [Header("Vue de chute")]
        [Tooltip("Distance au pilote à terre.")]
        public float crashDistance = 4.5f;
        [Tooltip("Hauteur visée au-dessus du pilote.")]
        public float crashHeight = 0.9f;
        public float crashPitch = 18f;
        [Tooltip("Tour lent autour du pilote à terre, en degrés par seconde.")]
        public float crashOrbitSpeed = 12f;
        [Tooltip("Temps de glissement de la caméra jusqu'à sa place autour du pilote : elle ne saute pas au choc.")]
        public float crashBlendTime = 0.45f;
        public float crashFov = 55f;

        Camera cam;
        MotorcycleController bike;
        // Le jeu démarre toujours en vue 1re personne.
        CameraView view = CameraView.FirstPerson;
        float defaultNearClip;
        bool needsSnap = true;

        float orbitYaw;
        float orbitPitch;
        float orbitYawVelocity;
        float orbitPitchVelocity;
        float currentDistance;
        bool dragging;
        float lastLookInputTime = float.NegativeInfinity;

        float freeLookYaw;
        float freeLookPitch;
        float lookDown;
        float lookDownVelocity;

        // Vue de chute : le pilote à terre, la vue d'avant la chute mise de côté le temps du crash.
        Transform crashFocus;
        CameraView viewBeforeCrash;
        float crashYaw;
        float crashOrbitPitch;
        Vector3 crashVelocity;

        public CameraView CurrentView => view;
        /// <summary>Vrai tant que la caméra tourne autour du pilote à terre.</summary>
        public bool InCrashView => crashFocus != null;

        float SpeedFactor => bike != null
            ? Mathf.Clamp01(Mathf.Abs(bike.SignedSpeed) / Mathf.Max(0.1f, bike.maxSpeed))
            : 0f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            defaultNearClip = cam.nearClipPlane;
            lookDown = standstillLookDown;
            cam.nearClipPlane = view == CameraView.FirstPerson ? firstPersonNearClip : defaultNearClip;
            if (target != null)
            {
                bike = target.GetComponent<MotorcycleController>();
            }
        }

        public void ToggleView()
        {
            // Pendant une chute, la place du pilote est vide : on ne peut pas y mettre la caméra.
            if (crashFocus != null) return;

            view = view == CameraView.Exterior ? CameraView.FirstPerson : CameraView.Exterior;
            cam.nearClipPlane = view == CameraView.FirstPerson ? firstPersonNearClip : defaultNearClip;
            freeLookYaw = 0f;
            freeLookPitch = 0f;
            // De retour en vue extérieure, repartir derrière la moto plutôt que balayer depuis l'ancienne position.
            needsSnap = true;
        }

        public void SetLookDragging(bool value)
        {
            dragging = value;
            lastLookInputTime = Time.time;
            if (value)
            {
                orbitYawVelocity = 0f;
                orbitPitchVelocity = 0f;
            }
        }

        /// <summary>
        /// Chute : la caméra recule derrière le pilote à terre et tourne lentement autour de lui, le temps
        /// qu'il choisisse de repartir. La vue d'avant la chute est reprise au respawn.
        /// </summary>
        public void EnterCrashView(Transform focus)
        {
            if (focus == null || crashFocus != null) return;

            crashFocus = focus;
            viewBeforeCrash = view;
            // Départ de trois quarts arrière : on voit à la fois le pilote qui roule et la moto qu'il quitte.
            crashYaw = (target != null ? target.eulerAngles.y : transform.eulerAngles.y) + 35f;
            crashOrbitPitch = crashPitch;
            crashVelocity = Vector3.zero;
            currentDistance = crashDistance;
            cam.nearClipPlane = defaultNearClip;
        }

        /// <summary>Respawn : retour au point de vue d'avant la chute, posé derrière la moto relevée.</summary>
        public void ExitCrashView()
        {
            if (crashFocus == null) return;

            crashFocus = null;
            view = viewBeforeCrash;
            cam.nearClipPlane = view == CameraView.FirstPerson ? firstPersonNearClip : defaultNearClip;
            freeLookYaw = 0f;
            freeLookPitch = 0f;
            needsSnap = true;
        }

        public void AddLookInput(Vector2 screenDelta)
        {
            // Normalisé par la hauteur d'écran : même sensation quelle que soit la résolution du téléphone.
            Vector2 normalized = screenDelta / Mathf.Max(1f, Screen.height);

            if (crashFocus != null)
            {
                crashYaw += normalized.x * orbitSensitivity;
                crashOrbitPitch = Mathf.Clamp(crashOrbitPitch - normalized.y * orbitSensitivity, minPitch, maxPitch);
                lastLookInputTime = Time.time;
                return;
            }

            if (view == CameraView.Exterior)
            {
                orbitYaw += normalized.x * orbitSensitivity;
                orbitPitch = Mathf.Clamp(orbitPitch - normalized.y * orbitSensitivity, minPitch, maxPitch);
            }
            else
            {
                freeLookYaw = Mathf.Clamp(freeLookYaw + normalized.x * freeLookSensitivity, -maxFreeLookYaw, maxFreeLookYaw);
                freeLookPitch = Mathf.Clamp(freeLookPitch - normalized.y * freeLookSensitivity, -maxFreeLookPitch, maxFreeLookPitch);
            }

            lastLookInputTime = Time.time;
        }

        void LateUpdate()
        {
            if (target == null) return;

            ReadKeyboard();

            float dt = Time.deltaTime;
            if (crashFocus != null)
            {
                UpdateCrash(dt);
                return;
            }

            if (view == CameraView.Exterior)
            {
                UpdateExterior(dt);
            }
            else
            {
                UpdateFirstPerson(dt);
            }
        }

        void ReadKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.cKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame))
            {
                ToggleView();
            }
#else
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V))
            {
                ToggleView();
            }
#endif
        }

        /// <summary>Derrière la moto en marche avant, de l'autre côté en marche arrière.</summary>
        float TravelYaw()
        {
            bool reversing = bike != null && bike.SignedSpeed < -0.3f;
            return target.eulerAngles.y + (reversing ? 180f : 0f);
        }

        void UpdateExterior(float dt)
        {
            if (needsSnap)
            {
                orbitYaw = TravelYaw();
                orbitPitch = defaultPitch;
                orbitYawVelocity = 0f;
                orbitPitchVelocity = 0f;
                currentDistance = distance;
                needsSnap = false;
            }

            float speed = bike != null ? Mathf.Abs(bike.SignedSpeed) : 0f;
            bool mayRecenter = !dragging && speed > recenterMinSpeed && Time.time - lastLookInputTime > recenterDelay;
            if (mayRecenter)
            {
                // Plus on roule vite, plus la caméra se verrouille vite derrière la moto.
                float smoothTime = recenterSmoothTime / Mathf.Lerp(0.35f, 1f, SpeedFactor);
                orbitYaw = Mathf.SmoothDampAngle(orbitYaw, TravelYaw(), ref orbitYawVelocity, smoothTime);
                orbitPitch = Mathf.SmoothDamp(orbitPitch, defaultPitch, ref orbitPitchVelocity, smoothTime);
            }

            Vector3 pivot = target.position + Vector3.up * lookHeight;
            Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float wanted = distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(minDistance, hit.distance);
            }
            // Se rapproche instantanément pour ne jamais traverser un mur, s'éloigne en douceur.
            currentDistance = wanted < currentDistance ? wanted : Mathf.MoveTowards(currentDistance, wanted, 4f * dt);

            transform.SetPositionAndRotation(pivot + back * currentDistance, rotation);
            cam.fieldOfView = Mathf.Lerp(exteriorFov, exteriorFovAtMaxSpeed, SpeedFactor);
        }

        /// <summary>
        /// Le pilote est à terre : la caméra le cadre en troisième personne et tourne doucement autour de lui.
        /// Elle rejoint sa place en glissant depuis là où elle était au moment du choc — depuis les yeux du
        /// pilote en vue 1re personne — plutôt que d'y sauter d'une frame à l'autre.
        /// </summary>
        void UpdateCrash(float dt)
        {
            crashYaw += crashOrbitSpeed * dt;

            Vector3 pivot = crashFocus.position + Vector3.up * crashHeight;
            Vector3 back = Quaternion.Euler(crashOrbitPitch, crashYaw, 0f) * Vector3.back;

            float wanted = crashDistance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, crashDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(minDistance, hit.distance);
            }
            currentDistance = wanted < currentDistance ? wanted : Mathf.MoveTowards(currentDistance, wanted, 4f * dt);

            transform.position = Vector3.SmoothDamp(transform.position, pivot + back * currentDistance,
                ref crashVelocity, crashBlendTime, Mathf.Infinity, dt);

            Vector3 toPivot = pivot - transform.position;
            if (toPivot.sqrMagnitude > 1e-4f)
            {
                Quaternion look = Quaternion.LookRotation(toPivot);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-8f * dt));
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, crashFov, 1f - Mathf.Exp(-5f * dt));
        }

        void UpdateFirstPerson(float dt)
        {
            Transform head = bike != null && bike.RiderHead != null ? bike.RiderHead : target;

            if (!dragging && Time.time - lastLookInputTime > 0.15f)
            {
                float k = 1f - Mathf.Exp(-freeLookReturnSpeed * dt);
                freeLookYaw = Mathf.Lerp(freeLookYaw, 0f, k);
                freeLookPitch = Mathf.Lerp(freeLookPitch, 0f, k);
            }

            // Assiette de la moto : positive en wheeling, négative sur la roue avant (le regard plonge un peu).
            float wheelie = bike != null ? bike.WheelieAngle - bike.StoppieAngle : 0f;
            float lean = bike != null ? bike.LeanAngle : 0f;
            float headDown = bike != null ? bike.firstPersonHeadPitch : 0f;

            // À l'arrêt, le regard plonge sur la moto ; il se relève vers la route en prenant de la vitesse.
            float speed = bike != null ? Mathf.Abs(bike.SignedSpeed) : 0f;
            float targetLookDown = standstillLookDown * (1f - Mathf.Clamp01(speed / Mathf.Max(0.1f, lookUpSpeed)));
            lookDown = Mathf.SmoothDamp(lookDown, targetLookDown, ref lookDownVelocity, 0.35f, Mathf.Infinity, dt);
            headDown += lookDown;

            // Regard construit à partir du cap de la moto, et non de son assiette : les yeux restent sur la route.
            // Seule une petite part du cabrage est conservée (sensation de lever) ; la tête redresse une partie
            // de l'inclinaison et regarde vers l'intérieur du virage.
            // Sur une pente, le regard suit la route : il se lève en montée, plonge en descente.
            float slope = bike != null ? bike.GroundPitch : 0f;
            float pitch = headDown + freeLookPitch - wheelie * (1f - wheeliePitchCompensation) - slope;
            float yaw = freeLookYaw - lean * lookIntoTurn;
            float roll = lean * (1f - headLevelling);
            Quaternion rotation = target.rotation * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, roll);

            float speedFactor = SpeedFactor;
            float t = Time.time * 22f;
            Vector3 shake = new Vector3(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f, 0f)
                * (2f * speedShake * (0.15f + speedFactor));

            transform.SetPositionAndRotation(head.position + rotation * shake, rotation);
            cam.fieldOfView = Mathf.Lerp(firstPersonFov, firstPersonFovAtMaxSpeed, speedFactor);
        }
    }
}
