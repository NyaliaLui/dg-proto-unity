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

        /// <summary>
        /// Rebinds the bar to a new Health source. The networked player Paladin is
        /// spawned at runtime, so the local player's <see cref="NetworkPlayerSetup"/>
        /// calls this once it knows which Paladin is "mine".
        /// </summary>
        public void SetTarget(Health newTarget)
        {
            if (target != null) target.Changed -= OnHealthChanged;
            target = newTarget;
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
