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
