using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Networked pickup. The host owns the pickup: when a Paladin overlaps it, the
    /// server despawns it (so it vanishes for everyone) and tells the picking
    /// player's client to show the reward popup. Every client plays the "appear"
    /// sound on spawn.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider))]
    public class Droppable : NetworkBehaviour
    {
        [SerializeField] private string notificationMessage =
            "Congratulations! You won 20% off on Dragon Groove merch. Use code TSTCDE at checkout.";
        [SerializeField] private string primaryButtonLabel = "Dragon Groove Store";
        [SerializeField] private string primaryButtonUrl   = "https://amazon.com";
        [SerializeField] private string closeButtonLabel   = "Close";

        private bool _picked;

        public override void OnNetworkSpawn()
        {
            AudioManager.Instance.Play(SfxId.DroppableSpawn);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Pickup is resolved authoritatively on the host. The host's physics
            // sees both the host's player and the replicated remote proxy.
            if (!IsServer || _picked) return;

            var pc = other.GetComponentInParent<PaladinController>();
            if (pc == null) return;
            var playerObj = pc.GetComponent<NetworkObject>();
            if (playerObj == null) return;

            _picked = true;
            ulong picker = playerObj.OwnerClientId;

            // Notify the picker. When the HOST itself is the picker, call locally
            // rather than via RPC — the despawn that follows would otherwise race
            // the host's own loopback RPC and drop the popup.
            if (picker == NetworkManager.LocalClientId) ShowReward();
            else ShowRewardClientRpc(picker);

            // Hide immediately, then despawn a frame later so the RPC is flushed
            // to the remote picker before its object is destroyed.
            StartCoroutine(HideAndDespawn());
        }

        private IEnumerator HideAndDespawn()
        {
            var rend = GetComponent<Renderer>();
            if (rend != null) rend.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            yield return null; // let the ClientRpc flush before the despawn message

            var no = GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn();
        }

        // Shows the reward popup on the picking player's client only; the other
        // player just sees the droppable disappear.
        [ClientRpc]
        private void ShowRewardClientRpc(ulong pickerClientId)
        {
            if (NetworkManager.Singleton.LocalClientId != pickerClientId) return;
            ShowReward();
        }

        private void ShowReward()
        {
            AudioManager.Instance.Play(SfxId.DroppablePickup);
            NotificationWindow.Show(notificationMessage, primaryButtonLabel, primaryButtonUrl, closeButtonLabel);
        }
    }
}
