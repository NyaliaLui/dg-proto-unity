using UnityEngine;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// Drives a filled uGUI Image from a <see cref="Health"/> component.
    /// Updates reactively via the Health.Changed event.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField] private Image fill;

        private void Start()
        {
            if (target != null)
            {
                target.Changed += OnHealthChanged;
                Refresh();
            }
        }

        private void OnDestroy()
        {
            if (target != null) target.Changed -= OnHealthChanged;
        }

        private void OnHealthChanged(Health h) => Refresh();

        private void Refresh()
        {
            if (fill != null && target != null)
                fill.fillAmount = target.Normalized;
        }
    }
}
