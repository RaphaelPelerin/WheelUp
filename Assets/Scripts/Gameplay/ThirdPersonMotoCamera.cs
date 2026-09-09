using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>Caméra de poursuite simple à la 3e personne pour la conduite en free-roam.</summary>
    public class ThirdPersonMotoCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 3.2f, -6.5f);
        public float positionSmoothTime = 0.15f;
        public float rotationSmoothSpeed = 6f;
        public float lookHeightOffset = 1.2f;

        Vector3 velocity;

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.TransformPoint(offset);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

            Vector3 lookPoint = target.position + Vector3.up * lookHeightOffset;
            Vector3 lookDir = lookPoint - transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
            }
        }
    }
}
