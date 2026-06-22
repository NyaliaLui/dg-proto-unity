using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Moves this transform a fraction of the camera's motion to create a
    /// parallax effect. Higher <see cref="parallaxFactor"/> = layer appears
    /// more distant (moves more with the camera). 0 = world-locked
    /// (foreground), 1 = camera-locked (infinitely far).
    /// </summary>
    [DefaultExecutionOrder(200)] // run AFTER SidescrollerCameraFollow (order 100)
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] private Transform target;          // null → auto-bind to Camera.main
        [Range(0f, 1f)] [SerializeField] private float parallaxFactor = 0.5f;
        [SerializeField] private bool affectX = true;
        [SerializeField] private bool affectY = false;

        private Vector3 _initialPos;
        private Vector3 _initialTargetPos;
        private bool _initialized;

        private void Start()
        {
            if (target == null && Camera.main != null) target = Camera.main.transform;
            if (target == null) return;
            _initialPos       = transform.position;
            _initialTargetPos = target.position;
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized || target == null) return;
            Vector3 delta = target.position - _initialTargetPos;
            Vector3 p = _initialPos;
            if (affectX) p.x += delta.x * parallaxFactor;
            if (affectY) p.y += delta.y * parallaxFactor;
            transform.position = p;
        }
    }
}
