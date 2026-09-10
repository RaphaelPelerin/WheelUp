using System;
using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Pilote humanoïde assis sur la moto. L'Animator joue la position de conduite (AS_Idle_Riding) ;
    /// ce script reprend ensuite, à chaque frame :
    /// - le buste, qui vit avec la moto : il compense le cabrage, jette son poids vers l'avant quand la roue
    ///   monte vite, se couche en zone critique, plonge au freinage et au retour de la roue, et se penche
    ///   dans les virages (surtout roue levée, où l'on dirige au poids du corps) ; la tête garde l'horizon ;
    /// - les bras, par IK deux os, jusqu'aux poignées du guidon (qui tournent avec la direction) ;
    /// - le poignet droit, qui roule autour de la poignée d'accélérateur avec les gaz ;
    /// - les doigts, refermés sur les poignées ; index et majeur droits posés sur le levier de frein,
    ///   qu'ils tirent au freinage.
    /// Le pilote est calé au démarrage : bassin sur la selle, puis avancé si ses bras n'atteignent pas le guidon.
    /// Les yeux de la vue 1re personne sont alors placés juste devant sa poitrine et ancrés à son buste :
    /// la caméra suit ses mouvements sans jamais entrer dans le corps. La tête est masquée dans cette vue.
    /// </summary>
    [DefaultExecutionOrder(100)] // Après MotorcycleController, qui pose guidon et levier en LateUpdate.
    public class MotoRider : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Hauteur du bassin au-dessus de l'assise.")]
        public float hipHeightAboveSeat = 0.1f;
        [Tooltip("Avancée maximale sur la selle si les bras n'atteignent pas le guidon.")]
        public float maxReachShift = 0.2f;
        [Tooltip("Distance entre la poitrine et les yeux de la vue 1re personne.")]
        public float eyeClearance = 0.06f;

        [Header("Vie du buste")]
        [Tooltip("Part du cabrage que le buste compense en se penchant vers l'avant (0 = collé à la moto, 1 = reste vertical).")]
        public float wheelieBodyLean = 0.5f;
        [Tooltip("Poids jeté vers l'avant quand la roue monte vite : degrés de buste par degré/s de cabrage.")]
        public float balanceReaction = 0.12f;
        public float maxBalanceReaction = 12f;
        [Tooltip("Buste couché vers l'avant en zone critique, en degrés.")]
        public float criticalLean = 14f;
        [Tooltip("Buste qui plonge quand la fourche s'écrase (freinage, retour de la roue), en degrés à compression complète.")]
        public float forkDip = 10f;
        [Tooltip("Buste penché dans les virages roue au sol, en degrés.")]
        public float turnLeanGround = 5f;
        [Tooltip("Buste penché dans les virages roue levée : on dirige au poids du corps, en degrés.")]
        public float turnLeanWheelie = 14f;
        [Tooltip("Part de l'inclinaison du buste que la tête rattrape pour garder les yeux sur l'horizon.")]
        public float headCompensation = 0.8f;
        [Tooltip("Temps de réponse du corps : plus long = mouvements plus amples et plus souples.")]
        public float bodySmoothTime = 0.12f;

        [Header("Mains")]
        [Tooltip("Nom des poignées dans le modèle de moto (Honda C125 : GRIP).")]
        public string gripPartName = "GRIP";
        [Tooltip("Inclinaison des mains sur les poignées : doigts vers l'avant et le bas, en degrés.")]
        public float gripPitch = 35f;
        [Tooltip("Rotation du poignet droit à pleins gaz, en degrés.")]
        public float throttleTwist = 30f;
        [Tooltip("Écartement des coudes vers l'extérieur.")]
        public float elbowOut = 0.25f;
        [Tooltip("Abaissement des coudes.")]
        public float elbowDown = 0.2f;

        [Header("Doigts (flexion en degrés : proximale, intermédiaire, distale)")]
        [Tooltip("Doigts refermés sur la poignée.")]
        public Vector3 wrapCurl = new Vector3(60f, 75f, 45f);
        public Vector3 thumbCurl = new Vector3(10f, 25f, 20f);
        [Tooltip("Index et majeur droits posés sur le levier, frein relâché.")]
        public Vector3 leverRestCurl = new Vector3(15f, 20f, 12f);
        [Tooltip("Index et majeur droits, levier serré à fond.")]
        public Vector3 leverPullCurl = new Vector3(40f, 50f, 30f);

        const float PalmThickness = 0.02f;
        const float MinGripRadius = 0.012f;
        const float MaxGripRadius = 0.03f;
        const float GripInsetFromEnd = 0.06f;
        const int CalibrationFrames = 3;
        const float HiddenHeadScale = 0.001f;
        // Zone sondée devant les yeux pour trouver la poitrine : assez étroite pour exclure bras et cuisses.
        const float ChestProbeHalfWidth = 0.12f;
        const float ChestProbeBelow = 0.2f;
        const float ChestProbeAbove = 0.12f;
        const float ChestProbeReach = 0.4f;

        enum Finger { Thumb, Index, Middle, Ring, Little }

        sealed class Arm
        {
            public bool isRight;
            public Transform upper, lower, hand;
            public float upperLength, lowerLength, palmLength;
            // Repère de la paume dans l'espace de la main : direction des doigts, normale côté paume.
            public Vector3 fingersLocal, palmNormalLocal;
            // Centre de la poignée, dans l'espace de la colonne de direction.
            public Vector3 gripLocal;
            public readonly Transform[][] fingers = new Transform[5][];
            public readonly Quaternion[][] restRotations = new Quaternion[5][];
        }

        MotorcycleController bike;
        MotoCameraRig cameraRig;
        Transform hips;
        Transform neck;
        Transform head;
        Vector3 headRestScale;
        Transform[] spineBones;
        float[] spineWeights;
        Arm leftArm, rightArm;
        float gripRadius;
        int calibrationCount;

        float bodyPitch, bodyPitchVelocity;
        float bodyRoll, bodyRollVelocity;

        // Yeux de la vue 1re personne, ancrés au haut du buste.
        Transform eyeAnchor;
        Vector3 eyeInAnchor;

        /// <summary>Appelé par MotorcycleController juste après l'instanciation, pose de repos encore intacte.</summary>
        public void Setup(MotorcycleController owner, Transform bikeModel, Animator animator)
        {
            bike = owner;
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[MotoRider] Le pilote doit être un modèle humanoïde avec Animator : bras non posés.", this);
                enabled = false;
                return;
            }

            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) headRestScale = head.localScale;
            BuildSpine(animator);
            leftArm = BuildArm(animator, false);
            rightArm = BuildArm(animator, true);
            cameraRig = FindAnyObjectByType<MotoCameraRig>();

            if (hips == null || leftArm == null || rightArm == null || !MeasureGrips(bikeModel))
            {
                Debug.LogWarning("[MotoRider] Squelette ou poignées introuvables : bras non posés.", this);
                enabled = false;
            }
        }

        /// <summary>Vertèbres qui se partagent l'inclinaison du buste, du bas vers le haut.</summary>
        void BuildSpine(Animator animator)
        {
            var bones = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.Spine),
                animator.GetBoneTransform(HumanBodyBones.Chest),
                animator.GetBoneTransform(HumanBodyBones.UpperChest),
            };
            float[] shares = { 0.4f, 0.35f, 0.25f };

            int count = 0;
            float total = 0f;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                count++;
                total += shares[i];
            }

            spineBones = new Transform[count];
            spineWeights = new float[count];
            for (int i = 0, k = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                spineBones[k] = bones[i];
                spineWeights[k] = shares[i] / total;
                eyeAnchor = bones[i]; // la plus haute trouvée
                k++;
            }
        }

        static Arm BuildArm(Animator animator, bool right)
        {
            var arm = new Arm
            {
                isRight = right,
                upper = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm),
                lower = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm),
                hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand),
            };
            if (arm.upper == null || arm.lower == null || arm.hand == null) return null;

            arm.upperLength = Vector3.Distance(arm.upper.position, arm.lower.position);
            arm.lowerLength = Vector3.Distance(arm.lower.position, arm.hand.position);

            // HumanBodyBones range les doigts par main : pouce, index, majeur, annulaire, auriculaire, 3 phalanges chacun.
            int first = (int)(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            for (int f = 0; f < 5; f++)
            {
                arm.fingers[f] = new Transform[3];
                arm.restRotations[f] = new Quaternion[3];
                for (int j = 0; j < 3; j++)
                {
                    Transform bone = animator.GetBoneTransform((HumanBodyBones)(first + f * 3 + j));
                    arm.fingers[f][j] = bone;
                    if (bone != null) arm.restRotations[f][j] = bone.localRotation;
                }
            }

            // Repère de la paume, relevé sur la pose de repos (doigts tendus).
            Transform index = arm.fingers[(int)Finger.Index][0];
            Transform middle = arm.fingers[(int)Finger.Middle][0];
            Transform little = arm.fingers[(int)Finger.Little][0];
            if (index == null || middle == null || little == null) return null;

            Vector3 fingersDir = (middle.position - arm.hand.position).normalized;
            Vector3 thumbSide = (index.position - little.position).normalized;
            Vector3 palmNormal = right ? Vector3.Cross(fingersDir, thumbSide) : Vector3.Cross(thumbSide, fingersDir);
            palmNormal = Vector3.ProjectOnPlane(palmNormal, fingersDir).normalized;

            arm.fingersLocal = arm.hand.InverseTransformDirection(fingersDir);
            arm.palmNormalLocal = arm.hand.InverseTransformDirection(palmNormal);
            arm.palmLength = Vector3.Distance(arm.hand.position, middle.position);
            return arm;
        }

        /// <summary>Centres des poignées, mesurés sur le modèle dans l'espace de la colonne de direction.</summary>
        bool MeasureGrips(Transform bikeModel)
        {
            Transform bar = bike.HandlebarPart;
            if (bar == null || !MotorcycleController.TryMeasureParts(bikeModel, bar,
                    n => string.Equals(n, gripPartName, StringComparison.OrdinalIgnoreCase), out Bounds grips))
            {
                return false;
            }

            gripRadius = Mathf.Clamp(Mathf.Min(grips.size.y, grips.size.z) * 0.5f, MinGripRadius, MaxGripRadius);
            Vector3 c = grips.center;
            if (grips.size.x > 0.25f)
            {
                // Une seule pièce pour les deux poignées : une main à chaque bout.
                leftArm.gripLocal = new Vector3(grips.min.x + GripInsetFromEnd, c.y, c.z);
                rightArm.gripLocal = new Vector3(grips.max.x - GripInsetFromEnd, c.y, c.z);
            }
            else
            {
                // Une seule poignée modélisée : l'autre est sa symétrique, le guidon étant centré sur la colonne.
                Vector3 mirrored = new Vector3(-c.x, c.y, c.z);
                leftArm.gripLocal = c.x < 0f ? c : mirrored;
                rightArm.gripLocal = c.x < 0f ? mirrored : c;
            }
            return true;
        }

        void LateUpdate()
        {
            Transform frame = transform.parent;
            if (bike == null || frame == null) return;

            // L'Animator peut mettre une frame ou deux à poser le corps : calage répété au démarrage.
            bool calibrating = calibrationCount < CalibrationFrames;
            if (calibrating) Calibrate(frame);

            PoseBody(frame, Time.deltaTime);
            PoseArm(leftArm, frame, 0f, 0f);
            PoseArm(rightArm, frame, bike.Throttle, bike.FrontBrakeLever);

            if (calibrating && ++calibrationCount == CalibrationFrames)
            {
                AnchorEyes(frame);
            }
            if (eyeAnchor != null && calibrationCount >= CalibrationFrames)
            {
                bike.SetRiderEye(frame.InverseTransformPoint(eyeAnchor.TransformPoint(eyeInAnchor)));
            }

            if (head != null)
            {
                bool firstPerson = cameraRig != null && cameraRig.CurrentView == CameraView.FirstPerson;
                head.localScale = firstPerson ? headRestScale * HiddenHeadScale : headRestScale;
            }
        }

        void Calibrate(Transform frame)
        {
            Vector3 seat = bike.SeatContactPoint + Vector3.up * hipHeightAboveSeat;
            transform.position += frame.TransformPoint(seat) - hips.position;

            // Animation faite pour une autre moto : si les bras n'atteignent pas le guidon, le pilote s'avance.
            float excess = Mathf.Max(ReachExcess(leftArm, frame), ReachExcess(rightArm, frame));
            transform.position += frame.forward * Mathf.Clamp(excess, 0f, maxReachShift);
        }

        float ReachExcess(Arm arm, Transform frame)
        {
            GripTarget(arm, frame, 0f, out Vector3 wrist, out _, out _);
            return Vector3.Distance(arm.upper.position, wrist) - 0.95f * (arm.upperLength + arm.lowerLength);
        }

        /// <summary>
        /// Yeux de la vue 1re personne : à la hauteur réglée sur la moto, avancés juste devant la poitrine
        /// (mesurée sur le maillage posé), puis ancrés au haut du buste pour en suivre les mouvements.
        /// </summary>
        void AnchorEyes(Transform frame)
        {
            if (eyeAnchor == null) return;

            Vector3 eye = bike.DefaultEyePosition();
            float defaultZ = eye.z;
            float chestFront = MeasureChestFront(frame, eye);
            if (chestFront > float.NegativeInfinity)
            {
                eye.z = Mathf.Max(eye.z, chestFront + eyeClearance);
            }
            eyeInAnchor = eyeAnchor.InverseTransformPoint(frame.TransformPoint(eye));
            Debug.Log($"[MotoRider] Vue 1re personne : yeux à {eye.y:0.00} m, avancés de {(eye.z - defaultZ) * 100f:0} cm pour rester devant la poitrine.", this);
        }

        /// <summary>Avant de la poitrine, dans l'espace du pivot : sommet le plus avancé du corps autour des yeux.</summary>
        float MeasureChestFront(Transform frame, Vector3 eye)
        {
            float front = float.NegativeInfinity;
            var baked = new Mesh();
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(baked, true);
                Transform t = skin.transform;
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 p = frame.InverseTransformPoint(t.position + t.rotation * v);
                    if (Mathf.Abs(p.x - eye.x) > ChestProbeHalfWidth) continue;
                    if (p.y < eye.y - ChestProbeBelow || p.y > eye.y + ChestProbeAbove) continue;
                    if (p.z > eye.z + ChestProbeReach) continue;
                    front = Mathf.Max(front, p.z);
                }
            }
            Destroy(baked);
            return front;
        }

        /// <summary>Buste et tête qui réagissent au wheeling, au freinage et aux virages.</summary>
        void PoseBody(Transform frame, float dt)
        {
            float wheelie = bike.WheelieAngle;
            float lift = bike.FrontWheelLift;
            float critical = Mathf.InverseLerp(bike.wheelieSweetMax, bike.wheelieFallAngle, wheelie);
            float balance = Mathf.Clamp(bike.WheelieAngularVelocity * balanceReaction, -maxBalanceReaction, maxBalanceReaction);

            float targetPitch = wheelie * wheelieBodyLean
                + balance * lift
                + criticalLean * critical
                + forkDip * Mathf.Max(0f, bike.ForkCompression);
            float targetRoll = bike.SteerInput * Mathf.Lerp(turnLeanGround, turnLeanWheelie, lift);

            if (dt > 0f)
            {
                bodyPitch = Mathf.SmoothDamp(bodyPitch, targetPitch, ref bodyPitchVelocity, bodySmoothTime, Mathf.Infinity, dt);
                bodyRoll = Mathf.SmoothDamp(bodyRoll, targetRoll, ref bodyRollVelocity, bodySmoothTime, Mathf.Infinity, dt);
            }

            // Tangage positif autour de la droite de la moto = buste vers l'avant ; roulis positif = vers la droite.
            Vector3 right = frame.right;
            Quaternion lean = Quaternion.AngleAxis(-bodyRoll, frame.forward) * Quaternion.AngleAxis(bodyPitch, right);
            for (int i = 0; i < spineBones.Length; i++)
            {
                spineBones[i].rotation = Quaternion.SlerpUnclamped(Quaternion.identity, lean, spineWeights[i]) * spineBones[i].rotation;
            }

            // La tête rattrape le cabrage que le buste n'a pas compensé : les yeux restent sur l'horizon.
            float headPitch = Mathf.Clamp(wheelie - bodyPitch, -15f, 40f) * headCompensation;
            Quaternion nod = Quaternion.AngleAxis(headPitch * 0.5f, right);
            if (neck != null) neck.rotation = nod * neck.rotation;
            if (head != null) head.rotation = nod * head.rotation;
        }

        /// <summary>Position du poignet et repère de la paume voulus pour tenir la poignée.</summary>
        void GripTarget(Arm arm, Transform frame, float throttle, out Vector3 wrist, out Vector3 fingersDir, out Vector3 palmNormal)
        {
            Transform bar = bike.HandlebarPart;
            Vector3 right = bar.right;
            Vector3 forward = Vector3.ProjectOnPlane(frame.forward, right).normalized;
            Vector3 up = Vector3.Cross(forward, right);

            // Paume sur le dessus de la poignée, doigts qui l'enveloppent par l'avant.
            float pitch = gripPitch * Mathf.Deg2Rad;
            fingersDir = forward * Mathf.Cos(pitch) - up * Mathf.Sin(pitch);
            palmNormal = -up * Mathf.Cos(pitch) - forward * Mathf.Sin(pitch);

            if (arm.isRight && throttle > 0f)
            {
                // Gaz : la main roule vers l'arrière autour de la poignée.
                Quaternion twist = Quaternion.AngleAxis(-throttleTwist * throttle, right);
                fingersDir = twist * fingersDir;
                palmNormal = twist * palmNormal;
            }

            Vector3 palmCenter = bar.TransformPoint(arm.gripLocal) - palmNormal * (gripRadius + PalmThickness);
            wrist = palmCenter - fingersDir * (arm.palmLength * 0.5f);
        }

        void PoseArm(Arm arm, Transform frame, float throttle, float lever)
        {
            GripTarget(arm, frame, throttle, out Vector3 wrist, out Vector3 fingersDir, out Vector3 palmNormal);

            Vector3 side = arm.isRight ? frame.right : -frame.right;
            Vector3 pole = (arm.upper.position + wrist) * 0.5f + side * elbowOut - frame.up * elbowDown;
            SolveTwoBone(arm, wrist, pole);

            // Main orientée sur la poignée : son repère de paume est aligné sur le repère voulu.
            Quaternion current = Quaternion.LookRotation(
                arm.hand.TransformDirection(arm.fingersLocal), arm.hand.TransformDirection(arm.palmNormalLocal));
            arm.hand.rotation = Quaternion.LookRotation(fingersDir, palmNormal) * Quaternion.Inverse(current) * arm.hand.rotation;

            // Tourner autour de cet axe amène les doigts vers la paume.
            Vector3 knuckleAxis = Vector3.Cross(fingersDir, palmNormal);
            Vector3 leverCurl = Vector3.Lerp(leverRestCurl, leverPullCurl, lever);
            for (int f = 0; f < 5; f++)
            {
                bool onLever = arm.isRight && (f == (int)Finger.Index || f == (int)Finger.Middle);
                Vector3 curl = f == (int)Finger.Thumb ? thumbCurl : onLever ? leverCurl : wrapCurl;
                Curl(arm, f, curl, knuckleAxis);
            }
        }

        /// <summary>IK analytique épaule-coude-poignet : le coude part du côté du pôle.</summary>
        static void SolveTwoBone(Arm arm, Vector3 target, Vector3 pole)
        {
            Vector3 root = arm.upper.position;
            Vector3 toTarget = target - root;
            float distance = Mathf.Clamp(toTarget.magnitude, 0.01f, (arm.upperLength + arm.lowerLength) * 0.999f);
            Vector3 direction = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole - root, direction).normalized;

            // Loi des cosinus : position du coude entre l'épaule et la cible.
            float cos = Mathf.Clamp(
                (arm.upperLength * arm.upperLength + distance * distance - arm.lowerLength * arm.lowerLength)
                / (2f * arm.upperLength * distance), -1f, 1f);
            Vector3 elbow = root + direction * (arm.upperLength * cos) + bend * (arm.upperLength * Mathf.Sqrt(1f - cos * cos));

            arm.upper.rotation = Quaternion.FromToRotation(arm.lower.position - root, elbow - root) * arm.upper.rotation;
            Vector3 reachPoint = root + direction * distance;
            arm.lower.rotation = Quaternion.FromToRotation(arm.hand.position - arm.lower.position, reachPoint - arm.lower.position)
                * arm.lower.rotation;
        }

        /// <summary>Doigt remis en pose de repos (tendu), puis fléchi phalange après phalange autour de l'axe des jointures.</summary>
        static void Curl(Arm arm, int finger, Vector3 angles, Vector3 axis)
        {
            Transform[] bones = arm.fingers[finger];
            for (int j = 0; j < bones.Length; j++)
            {
                if (bones[j] == null) continue;
                bones[j].localRotation = arm.restRotations[finger][j];
                bones[j].rotation = Quaternion.AngleAxis(angles[j], axis) * bones[j].rotation;
            }
        }
    }
}
