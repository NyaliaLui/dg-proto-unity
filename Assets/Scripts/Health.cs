using System;
using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Server-authoritative hit-point container for a networked character. The
    /// current HP lives in a <see cref="NetworkVariable{T}"/> that only the
    /// server may write, so damage/heal resolve identically for every client.
    /// <see cref="Changed"/> and <see cref="Died"/> fire on ALL clients (driven
    /// by the variable's OnValueChanged) so HUDs, score, and death reactions
    /// stay consistent across the match.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class Health : NetworkBehaviour, IDamageable
    {
        [Min(1)]
        [SerializeField] private int maxHP = 20;

        // 0 until the server initialises it to maxHP in OnNetworkSpawn. Server
        // writes; everyone reads.
        private readonly NetworkVariable<int> currentHP =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int   CurrentHP  => currentHP.Value;
        public int   MaxHP      => maxHP;
        public float Normalized => maxHP > 0 ? Mathf.Clamp01((float)currentHP.Value / maxHP) : 0f;
        // Only "dead" once spawned and HP has actually reached zero — guards
        // against the pre-spawn default (0) reading as dead.
        public bool  IsDead     => _spawned && currentHP.Value <= 0;

        private bool _spawned;

        /// <summary>Raised whenever HP changes (damage or heal), on every client.</summary>
        public event Action<Health> Changed;
        /// <summary>Raised once, when HP reaches 0, on every client.</summary>
        public event Action<Health> Died;

        public override void OnNetworkSpawn()
        {
            _spawned = true;
            currentHP.OnValueChanged += OnHpChanged;

            // The server seeds the authoritative starting HP; clients receive it
            // through the spawn synchronisation.
            if (IsServer) currentHP.Value = maxHP;

            // Drive listeners with the current value immediately. OnValueChanged
            // isn't guaranteed to fire for the initial synchronised value, so the
            // HUD/score would otherwise miss the first update.
            Changed?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            currentHP.OnValueChanged -= OnHpChanged;
            _spawned = false;
        }

        private void OnHpChanged(int previous, int current)
        {
            Changed?.Invoke(this);
            if (current <= 0 && previous > 0) Died?.Invoke(this);
        }

        public void TakeDamage(int amount)
        {
            if (!IsServer) return;               // damage resolves on the host only
            if (amount <= 0 || currentHP.Value <= 0) return;
            currentHP.Value = Mathf.Max(0, currentHP.Value - amount);
        }

        public void Heal(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0 || currentHP.Value <= 0) return;
            currentHP.Value = Mathf.Min(maxHP, currentHP.Value + amount);
        }
    }
}
