using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Sidescroller camera that follows a target on the X axis (and optionally Y),
    /// keeping its configured Z offset. Smoothed with SmoothDamp so quick direction
    /// changes don't jitter.
    /// </summary>
    [DefaultExecutionOrder(100)] // run after gameplay scripts have moved the target
    public class SidescrollerCameraFollow : MonoBehaviour
    {
        [Tooltip("The transform to follow. Usually the Paladin.")]
        [SerializeField] private Transform target;

        [Tooltip("World-space offset from the target. Z is the camera's distance back.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2f, -10f);

        [Tooltip("Follow on the X axis.")]
        [SerializeField] private bool followX = true;

        [Tooltip("Follow on the Y axis (turn off for a fixed horizon).")]
        [SerializeField] private bool followY = false;

        [Tooltip("Smoothing time for SmoothDamp. Smaller = snappier.")]
        [SerializeField] private float smoothTime = 0.15f;

        private Vector3 _velocity;

        /// <summary>
        /// Retarget the camera at runtime. In multiplayer each client calls this
        /// with its own locally-owned player so the camera follows "me", not the
        /// remote proxy. Snaps instantly to avoid a long pan from the origin.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                Vector3 snap = transform.position;
                if (followX) snap.x = target.position.x + offset.x;
                if (followY) snap.y = target.position.y + offset.y;
                snap.z = target.position.z + offset.z;
                transform.position = snap;
                _velocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = transform.position;
            if (followX) desired.x = target.position.x + offset.x;
            if (followY) desired.y = target.position.y + offset.y;
            desired.z = target.position.z + offset.z;

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }
    }
}
