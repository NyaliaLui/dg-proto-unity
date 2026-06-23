using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Lives on the networked Paladin player prefab. NGO spawns one instance per
    /// connected client. This component decides, per instance, whether it's "my"
    /// player or a replicated proxy of someone else's:
    ///
    /// - <b>Owner</b>: keep <see cref="PaladinController"/> active so this client
    ///   drives input + movement locally, and point the camera at this Paladin.
    /// - <b>Non-owner (remote proxy)</b>: disable local simulation — turn off the
    ///   controller and make the Rigidbody kinematic — so the replicated
    ///   <see cref="OwnerNetworkTransform"/> is the single source of truth and the
    ///   local physics step doesn't fight the incoming transform.
    ///
    /// This keeps <see cref="PaladinController"/> itself untouched for Milestone 1
    /// (we gate it from the outside rather than rewriting it as a NetworkBehaviour;
    /// the deeper authority refactor for combat/score is a later milestone).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerSetup : NetworkBehaviour
    {
        [SerializeField] private PaladinController controller;
        [SerializeField] private Rigidbody body;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PaladinController>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            bool isLocalPlayer = IsOwner;

            if (controller != null) controller.enabled = isLocalPlayer;

            if (body != null)
            {
                // Remote proxies are positioned entirely by the replicated
                // transform, so take them out of the physics simulation.
                body.isKinematic = !isLocalPlayer;
                body.interpolation = isLocalPlayer
                    ? RigidbodyInterpolation.Interpolate
                    : RigidbodyInterpolation.None;
            }

            if (isLocalPlayer)
            {
                var cam = Object.FindAnyObjectByType<SidescrollerCameraFollow>();
                if (cam != null) cam.SetTarget(transform);
            }
        }
    }
}
