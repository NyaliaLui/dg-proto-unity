using System;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Hit-point container for a character. Raises <see cref="Changed"/> on any
    /// HP change and <see cref="Died"/> once HP reaches zero.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Min(1)]
        [SerializeField] private int maxHP = 20;

        // -1 = uninitialised; set to maxHP in Awake. Serialized so it's
        // inspectable at runtime.
        [SerializeField] private int currentHP = -1;

        public int   CurrentHP  => currentHP;
        public int   MaxHP      => maxHP;
        public float Normalized => maxHP > 0 ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;
        public bool  IsDead     => currentHP <= 0;

        /// <summary>Raised whenever HP changes (damage or heal).</summary>
        public event Action<Health> Changed;
        /// <summary>Raised once, when HP reaches 0.</summary>
        public event Action<Health> Died;

        private void Awake()
        {
            if (currentHP < 0) currentHP = maxHP;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return;
            currentHP = Mathf.Max(0, currentHP - amount);
            Changed?.Invoke(this);
            if (currentHP == 0) Died?.Invoke(this);
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            Changed?.Invoke(this);
        }
    }
}
