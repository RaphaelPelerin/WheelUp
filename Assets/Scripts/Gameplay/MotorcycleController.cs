using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Contrôleur de moto V1 (free-roam) : déplacement cinématique simple (pas de Rigidbody/WheelCollider,
    /// aucun des packs importés ne fournissait de physique fonctionnelle) + mécanique de wheeling
    /// (cabrage, point d'équilibre, chute) découplée visuellement du déplacement réel.
    /// La hauteur au sol est fixe : pas de collision avec le décor pour cette première version.
    /// </summary>
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
        public float turnSpeed = 90f;

        [Header("Wheeling")]
        public float wheelieMaxAngle = 35f;
        public float wheelieSpeed = 60f;
        public float wheelieReturnSpeed = 90f;
        public float wheelieFallAngle = 48f;
        public float wheelieMinSpeed = 1.5f;
        public float fallRecoveryTime = 1.5f;

        const string LayerHandleLeft = "handle left";
        const string LayerHandleRight = "handle right";
        const string LayerFrontDamper = "front damper";
        const string LayerFrontBrake = "front brake";
        const string LayerRearWheelBrake = "rear wheel brake";
        const string ParamBrakeLamp = "Brake Lamp";

        float currentSpeed;
        float wheelieAngle;
        bool isFallen;
        float fallTimer;

        bool inputThrottle;
        bool inputBrake;
        float inputSteer;
        bool inputWheelie;

        Animator animator;

        public bool IsFallen => isFallen;
        public float SpeedKmh => currentSpeed * 3.6f;

        void Awake()
        {
            if (bikePrefab != null && visualPivot != null)
            {
                var visual = Instantiate(bikePrefab, visualPivot);
                visual.transform.localPosition = new Vector3(0f, 0f, 0.55f);
                visual.transform.localRotation = Quaternion.Euler(visualEulerOffset);
                animator = visual.GetComponentInChildren<Animator>();
            }
        }

        public void SetThrottle(bool held) => inputThrottle = held;
        public void SetBrake(bool held) => inputBrake = held;
        public void SetSteer(float value) => inputSteer = Mathf.Clamp(value, -1f, 1f);
        public void SetWheelie(bool held) => inputWheelie = held;

        void Update()
        {
            float dt = Time.deltaTime;

            if (isFallen)
            {
                fallTimer -= dt;
                if (fallTimer <= 0f) isFallen = false;
                UpdateWheelieVisual();
                return;
            }

            UpdateSpeed(dt);
            UpdateSteering(dt);
            UpdateWheelie(dt);
            ApplyAnimator();
        }

        void UpdateSpeed(float dt)
        {
            if (inputBrake)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakingDeceleration * dt);
            }
            else if (inputThrottle)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * dt);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, naturalDeceleration * dt);
            }

            transform.position += transform.forward * currentSpeed * dt;
        }

        void UpdateSteering(float dt)
        {
            if (currentSpeed < 0.05f) return;
            float speedFactor = Mathf.Clamp01(currentSpeed / (maxSpeed * 0.5f));
            transform.Rotate(Vector3.up, inputSteer * turnSpeed * speedFactor * dt);
        }

        void UpdateWheelie(float dt)
        {
            bool canWheelie = inputWheelie && currentSpeed >= wheelieMinSpeed;

            wheelieAngle = canWheelie
                ? Mathf.MoveTowards(wheelieAngle, wheelieMaxAngle, wheelieSpeed * dt)
                : Mathf.MoveTowards(wheelieAngle, 0f, wheelieReturnSpeed * dt);

            if (wheelieAngle >= wheelieFallAngle)
            {
                TriggerFall();
            }

            UpdateWheelieVisual();
        }

        void UpdateWheelieVisual()
        {
            if (visualPivot == null) return;
            visualPivot.localRotation = Quaternion.Euler(-wheelieAngle, 0f, 0f);
        }

        void TriggerFall()
        {
            isFallen = true;
            fallTimer = fallRecoveryTime;
            currentSpeed = 0f;
            wheelieAngle = 0f;
            inputThrottle = false;
            inputWheelie = false;
        }

        void ApplyAnimator()
        {
            if (animator == null) return;

            SetLayerWeight(LayerHandleLeft, inputSteer < 0f ? -inputSteer : 0f);
            SetLayerWeight(LayerHandleRight, inputSteer > 0f ? inputSteer : 0f);

            float brakeWeight = inputBrake ? 1f : 0f;
            SetLayerWeight(LayerFrontBrake, brakeWeight);
            SetLayerWeight(LayerRearWheelBrake, brakeWeight);
            SetLayerWeight(LayerFrontDamper, Mathf.Clamp01(wheelieAngle / wheelieMaxAngle));

            animator.SetBool(ParamBrakeLamp, inputBrake);
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
