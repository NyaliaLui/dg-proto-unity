using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Server-authoritative melee resolution for a networked Paladin. The owning
    /// client's <see cref="PaladinController"/> decides WHEN and WHERE a swing
    /// connects (at the animation's contact frame) and calls
    /// <see cref="RequestHit"/>; this component ensures the actual overlap test
    /// and damage application run on the host, so enemy <see cref="Health"/>
    /// (also server-authoritative) stays consistent for everyone.
    ///
    /// Co-op has no PvP fairness to defend, so we trust the owner's reported
    /// swing box rather than re-deriving it on the server. Paladins are skipped
    /// in the overlap, so friendly fire (and self-hits) can never happen.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerMelee : NetworkBehaviour
    {
        [SerializeField] private LayerMask meleeMask = ~0;

        private readonly Collider[] _hits = new Collider[16];
        private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();

        /// <summary>
        /// Called on the owning client at the swing's contact frame. Routes the
        /// hit to the host (or resolves it directly when this instance already IS
        /// the host).
        /// </summary>
        public void RequestHit(Vector3 center, Vector3 halfExtents, int amount, float stunDuration)
        {
            if (IsServer)
            {
                ResolveHit(center, halfExtents, amount, stunDuration);
            }
            else
            {
                SubmitHitServerRpc(center, halfExtents, amount, stunDuration);
            }
        }

        [ServerRpc]
        private void SubmitHitServerRpc(Vector3 center, Vector3 halfExtents, int amount, float stunDuration)
        {
            ResolveHit(center, halfExtents, amount, stunDuration);
        }

        // Server-side: overlap the reported box and damage every non-player
        // IDamageable once. Special swings (stunDuration > 0) also stun.
        private void ResolveHit(Vector3 center, Vector3 halfExtents, int amount, float stunDuration)
        {
            int n = Physics.OverlapBoxNonAlloc(center, halfExtents, _hits, Quaternion.identity, meleeMask, QueryTriggerInteraction.Collide);
            _hitThisSwing.Clear();
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                if (col == null) continue;

                // Never damage a Paladin — friendly fire (and self-hits) are off.
                if (col.GetComponentInParent<PaladinController>() != null) continue;

                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || _hitThisSwing.Contains(dmg)) continue;
                _hitThisSwing.Add(dmg);
                dmg.TakeDamage(amount);

                if (stunDuration > 0f)
                {
                    var stunnable = col.GetComponentInParent<IStunnable>();
                    if (stunnable != null) stunnable.ApplyStun(stunDuration);
                }
            }
        }
    }
}
