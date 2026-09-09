using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Contrôleur de moto V1 (free-roam). Le déplacement passe par un CharacterController :
    /// collisions avec le décor et gravité, sans l'instabilité d'une vraie physique deux-roues.
    ///
    /// Deux mécaniques sont simulées à part, sur le pivot visuel :
    /// - le wheeling, modélisé comme un pendule inversé autour du point de contact arrière :
    ///   sous le point d'équilibre la gravité rabat la roue, au-dessus elle emporte la moto
    ///   en arrière, et le frein arrière est le seul moyen de se rattraper ;
    /// - la direction, qui passe par l'inclinaison (lean) : l'entrée pilote l'angle de
    ///   carrossage, et le taux de virage en découle via w = g*tan(lean)/v, ce qui donne
    ///   des virages serrés à basse vitesse et amples à haute vitesse, comme une vraie moto.
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

        [Header("Conduite")]
        public float maxSpeed = 14f;
        public float acceleration = 8f;
        public float brakingDeceleration = 18f;
        public float naturalDeceleration = 4f;

        [Header("Direction (inclinaison)")]
        [Tooltip("Angle de carrossage maximum, en degrés.")]
        public float maxLeanAngle = 38f;
        [Tooltip("Vitesse de mise sur l'angle, en degrés par seconde.")]
        public float leanResponse = 140f;
        [Tooltip("Vitesse à partir de laquelle l'inclinaison maximale est atteignable.")]
        public float leanFullSpeed = 6f;
        [Tooltip("Garde-fou sur le taux de rotation, en degrés par seconde.")]
        public float maxYawRate = 130f;
        [Tooltip("Autorité de direction conservée en plein wheeling (roue avant en l'air).")]
        public float wheelieSteerAuthority = 0.35f;

        [Header("Collision")]
        public float colliderHeight = 1.4f;
        public float colliderRadius = 0.35f;
        public float gravity = -25f;
        [Tooltip("Vitesse conservée après avoir percuté un mur.")]
        public float wallImpactSpeed = 1.5f;
        [Tooltip("Hauteur du sondage vers le sol au démarrage, pour ne jamais apparaître dans le décor.")]
        public float groundProbeHeight = 200f;

        [Header("Wheeling")]
        [Tooltip("Angle où la moto tient en équilibre : en dessous elle retombe, au-dessus elle part en arrière.")]
        public float wheelieBalanceAngle = 38f;
        [Tooltip("Couple de cabrage donné par les gaz.")]
        public float wheelieLiftTorque = 190f;
        [Tooltip("Couple du frein arrière, qui rabat la roue avant.")]
        public float wheelieBrakeTorque = 260f;
        [Tooltip("Couple de gravité autour du point d'équilibre.")]
        public float wheelieGravityTorque = 150f;
        public float wheelieDamping = 1.6f;
        [Tooltip("Angle au-delà duquel la moto part en arrière sans rattrapage possible.")]
        public float wheelieFallAngle = 62f;
        [Tooltip("Angle atteint une fois couchée en arrière.")]
        public float wheelieMaxAngle = 95f;
        public float wheelieMinSpeed = 1.5f;
        public float fallRotateSpeed = 150f;
        public float fallRecoveryTime = 1.8f;
        [Tooltip("Efficacité du freinage pendant un wheeling (seul le frein arrière porte).")]
        public float brakeFactorDuringWheelie = 0.35f;

        const float GravityConstant = 9.81f;

        const string LayerHandleLeft = "handle left";
        const string LayerHandleRight = "handle right";
        const string LayerFrontDamper = "front damper";
        const string LayerFrontBrake = "front brake";
        const string LayerRearWheelBrake = "rear wheel brake";
        const string ParamBrakeLamp = "Brake Lamp";

        CharacterController body;
        Animator animator;

        float currentSpeed;
        float verticalSpeed;
        float currentLean;
        float wheelieAngle;
        float wheelieAngularVelocity;
        bool isFallen;
        float fallTimer;

        // Entrées tactiles (HUD) et clavier (test PC), fusionnées à chaque frame.
        bool touchThrottle, touchBrake, touchWheelie;
        float touchSteer;
        bool keyThrottle, keyBrake, keyWheelie;
        float keySteer;

        bool Throttle => touchThrottle || keyThrottle;
        bool Brake => touchBrake || keyBrake;
        bool Wheelie => touchWheelie || keyWheelie;
        float Steer => Mathf.Clamp(touchSteer + keySteer, -1f, 1f);

        public bool IsFallen => isFallen;
        public float SpeedKmh => currentSpeed * 3.6f;
        public float WheelieAngle => wheelieAngle;
        public float LeanAngle => currentLean;
        /// <summary>Vrai quand la moto a dépassé le point d'équilibre : sans frein, elle part en arrière.</summary>
        public bool IsPastBalancePoint => wheelieAngle > wheelieBalanceAngle;

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

            if (bikePrefab != null && visualPivot != null)
            {
                var visual = Instantiate(bikePrefab, visualPivot);
                visual.transform.localPosition = new Vector3(0f, 0f, 0.55f);
                visual.transform.localRotation = Quaternion.Euler(visualEulerOffset);
                animator = visual.GetComponentInChildren<Animator>();
            }
        }

        void Start()
        {
            SnapToGround();
        }

        public void SetThrottle(bool held) => touchThrottle = held;
        public void SetBrake(bool held) => touchBrake = held;
        public void SetSteer(float value) => touchSteer = Mathf.Clamp(value, -1f, 1f);
        public void SetWheelie(bool held) => touchWheelie = held;

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
            UpdateSteering(dt);
            ApplyMovement(dt);
            UpdateVisual();
            ApplyAnimator();
        }

        /// <summary>Clavier pour tester sur PC : ZQSD/WASD ou flèches, espace pour cabrer.</summary>
        void ReadKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            keyThrottle = kb.wKey.isPressed || kb.zKey.isPressed || kb.upArrowKey.isPressed;
            keyBrake = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            keyWheelie = kb.spaceKey.isPressed;

            bool left = kb.aKey.isPressed || kb.qKey.isPressed || kb.leftArrowKey.isPressed;
            bool right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            keySteer = (right ? 1f : 0f) - (left ? 1f : 0f);
#else
            keyThrottle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow);
            keyBrake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            keyWheelie = Input.GetKey(KeyCode.Space);

            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            keySteer = (right ? 1f : 0f) - (left ? 1f : 0f);
#endif
        }

        void UpdateSpeed(float dt)
        {
            if (Brake)
            {
                // Roue avant en l'air : seul le frein arrière porte, le freinage est bien plus faible.
                float strength = brakingDeceleration * (wheelieAngle > 1f ? brakeFactorDuringWheelie : 1f);
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, strength * dt);
            }
            else if (Throttle)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * dt);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, naturalDeceleration * dt);
            }
        }

        /// <summary>
        /// Pendule inversé autour du point de contact arrière. Sous le point d'équilibre la gravité
        /// rabat la roue avant, au-dessus elle accentue le cabrage : seul le frein permet de revenir.
        /// </summary>
        void UpdateWheelie(float dt)
        {
            float torque = 0f;

            if (Wheelie && currentSpeed >= wheelieMinSpeed)
            {
                torque += wheelieLiftTorque;
            }

            if (Brake)
            {
                torque -= wheelieBrakeTorque;
            }

            // Négatif sous le point d'équilibre (la roue retombe), positif au-dessus (la moto part en arrière).
            torque -= wheelieGravityTorque * Mathf.Sin((wheelieBalanceAngle - wheelieAngle) * Mathf.Deg2Rad);

            wheelieAngularVelocity += torque * dt;
            wheelieAngularVelocity -= wheelieAngularVelocity * wheelieDamping * dt;
            wheelieAngle += wheelieAngularVelocity * dt;

            if (wheelieAngle <= 0f)
            {
                wheelieAngle = 0f;
                if (wheelieAngularVelocity < 0f) wheelieAngularVelocity = 0f;
            }

            if (wheelieAngle >= wheelieFallAngle)
            {
                TriggerFall();
            }
        }

        /// <summary>
        /// L'entrée pilote l'inclinaison, et le virage découle de l'angle pris : w = g*tan(lean)/v.
        /// Roue avant en l'air, l'autorité de direction chute.
        /// </summary>
        void UpdateSteering(float dt)
        {
            float speedFactor = Mathf.Clamp01(currentSpeed / Mathf.Max(0.01f, leanFullSpeed));
            float wheelieFactor = Mathf.Lerp(1f, wheelieSteerAuthority,
                Mathf.Clamp01(wheelieAngle / Mathf.Max(1f, wheelieBalanceAngle)));

            float targetLean = -Steer * maxLeanAngle * speedFactor * wheelieFactor;
            currentLean = Mathf.MoveTowards(currentLean, targetLean, leanResponse * dt);

            if (currentSpeed < 0.2f) return;

            float yawRate = GravityConstant * Mathf.Tan(currentLean * Mathf.Deg2Rad) / currentSpeed * Mathf.Rad2Deg;
            yawRate = Mathf.Clamp(yawRate, -maxYawRate, maxYawRate);
            transform.Rotate(Vector3.up, -yawRate * dt);
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
                currentSpeed = Mathf.Min(currentSpeed, wallImpactSpeed);
            }
        }

        void UpdateFall(float dt)
        {
            wheelieAngle = Mathf.MoveTowards(wheelieAngle, wheelieMaxAngle, fallRotateSpeed * dt);
            currentLean = Mathf.MoveTowards(currentLean, 0f, leanResponse * dt);
            UpdateVisual();
            ApplyMovement(dt);

            fallTimer -= dt;
            if (fallTimer <= 0f)
            {
                isFallen = false;
                wheelieAngle = 0f;
                wheelieAngularVelocity = 0f;
            }
        }

        void TriggerFall()
        {
            isFallen = true;
            fallTimer = fallRecoveryTime;
            currentSpeed = 0f;
            wheelieAngularVelocity = 0f;
            touchThrottle = false;
            touchWheelie = false;
        }

        void UpdateVisual()
        {
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

        void ApplyAnimator()
        {
            if (animator == null) return;

            float steer = Steer;
            SetLayerWeight(LayerHandleLeft, steer < 0f ? -steer : 0f);
            SetLayerWeight(LayerHandleRight, steer > 0f ? steer : 0f);

            float brakeWeight = Brake ? 1f : 0f;
            SetLayerWeight(LayerFrontBrake, brakeWeight);
            SetLayerWeight(LayerRearWheelBrake, brakeWeight);
            SetLayerWeight(LayerFrontDamper, Mathf.Clamp01(wheelieAngle / Mathf.Max(1f, wheelieBalanceAngle)));

            animator.SetBool(ParamBrakeLamp, Brake);
        }

        void SetLayerWeight(string layerName, float weight)
        {
            int index = animator.GetLayerIndex(layerName);
            if (index >= 0)
            {
                animator.SetLayerWeight(index, Mathf.Clamp01(weight));
            }
        }
    }
}
