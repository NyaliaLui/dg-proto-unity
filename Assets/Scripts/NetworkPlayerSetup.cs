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

        // Stable per-player tints, indexed by OwnerClientId, applied on every
        // client so the two Paladins are always distinguishable. Index 0 (host)
        // keeps its original look; index 1 (joiner) is tinted blue.
        private static readonly Color[] PlayerTints =
        {
            Color.white,
            new Color(0.45f, 0.65f, 1f),
        };

        private Health _health;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PaladinController>();
            if (body == null) body = GetComponent<Rigidbody>();
            _health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            bool isLocalPlayer = IsOwner;

            // Register with the player registry so host-driven enemy AI can target
            // this Paladin (nearest living player).
            PlayerRegistry.Register(_health);

            // Stable color tint by owner, so both players can tell the Paladins
            // apart on every client.
            ApplyPlayerTint(OwnerClientId);

            // Controllers spawn DISABLED for everyone. The remote proxy stays
            // disabled (it's driven by the replicated transform); the local
            // player's controller is enabled by MatchController when the
            // pre-match countdown reaches zero, so nobody can act before "GO".
            if (controller != null) controller.enabled = false;

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

            // Bind HUD bars: the local player drives the main bar, the other
            // player drives the teammate bar (on every client).
            BindHealthBar(isLocalPlayer);
        }

        public override void OnNetworkDespawn()
        {
            PlayerRegistry.Unregister(_health);
        }

        private void ApplyPlayerTint(ulong ownerId)
        {
            var tint = PlayerTints[(int)(ownerId % (ulong)PlayerTints.Length)];
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mats = smr.materials; // instance copies — won't touch shared assets
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].HasProperty("_Color")) mats[i].color = tint;
            }
        }

        // Owner → the main (non-teammate) bar; non-owner → the teammate bar.
        private void BindHealthBar(bool isLocalPlayer)
        {
            if (_health == null) return;
            foreach (var bar in Object.FindObjectsByType<HealthBarUI>(FindObjectsSortMode.None))
            {
                if (bar.IsTeammateBar == !isLocalPlayer) bar.SetTarget(_health);
            }
        }
    }
}
