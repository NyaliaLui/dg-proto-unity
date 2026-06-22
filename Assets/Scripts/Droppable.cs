using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Pickup-able item that triggers a notification window when the Paladin
    /// walks over it, then removes itself from the scene.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Droppable : MonoBehaviour
    {
        [SerializeField] private string notificationMessage =
            "Congratulations! You won 20% off on Dragon Groove merch. Use code TSTCDE at checkout.";
        [SerializeField] private string primaryButtonLabel = "Dragon Groove Store";
        [SerializeField] private string primaryButtonUrl   = "https://amazon.com";
        [SerializeField] private string closeButtonLabel   = "Close";

        private bool _picked;

        private void OnTriggerEnter(Collider other)
        {
            if (_picked) return;
            if (other.GetComponentInParent<PaladinController>() == null) return;

            _picked = true;
            AudioManager.Instance.Play(SfxId.DroppablePickup);
            NotificationWindow.Show(notificationMessage, primaryButtonLabel, primaryButtonUrl, closeButtonLabel);
            Destroy(gameObject);
        }
    }
}
