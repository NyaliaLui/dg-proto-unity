using UnityEngine;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// Drives a filled uGUI Image from a <see cref="Health"/> component.
    /// Updates reactively via the Health.Changed event.
    ///
    /// The networked player Paladins spawn at runtime, so the target is bound by
    /// the local player's <see cref="NetworkPlayerSetup"/> on spawn rather than
    /// serialized. A bar flagged <see cref="IsTeammateBar"/> shows the OTHER
    /// player's health and stays hidden until a teammate exists.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField] private Image fill;
        [Tooltip("If set, this bar tracks the non-local player and hides until one exists.")]
        [SerializeField] private bool isTeammateBar;

        private CanvasGroup _group;

        public bool IsTeammateBar => isTeammateBar;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (target != null)
            {
                target.Changed += OnHealthChanged;
                Refresh();
            }
            ApplyVisibility();
        }

        /// <summary>
        /// Rebinds the bar to a new Health source. The networked player Paladins
        /// are spawned at runtime, so <see cref="NetworkPlayerSetup"/> calls this
        /// once it knows which Paladin maps to this bar.
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
            ApplyVisibility();
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

        // The teammate bar is invisible until a teammate is bound; the local bar
        // is always visible.
        private void ApplyVisibility()
        {
            if (_group == null) return;
            _group.alpha = (isTeammateBar && target == null) ? 0f : 1f;
        }
    }
}
