using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Marker for static world obstacles (rocks, platforms). Obstacles are
    /// host-spawned NetworkObjects and are NOT reparented under a container on
    /// clients, so the platform-hopper AI locates them by this component rather
    /// than by walking a parent transform.
    /// </summary>
    public class Obstacle : MonoBehaviour { }
}
