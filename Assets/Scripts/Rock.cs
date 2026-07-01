using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Destructible rock (a host-spawned NetworkObject). Damage resolves on the
    /// server; when HP hits zero the host network-spawns a reward Droppable where
    /// the rock stood and despawns the rock for everyone.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class Rock : NetworkBehaviour, IDamageable
    {
        [SerializeField] private int hp = 6;
        [Tooltip("Networked reward prefab spawned when the rock is destroyed.")]
        [SerializeField] private GameObject droppablePrefab;

        public void TakeDamage(int amount)
        {
            if (!IsServer) return;          // rocks break on the host only
            hp -= amount;
            if (hp <= 0) Die();
        }

        private void Die()
        {
            if (droppablePrefab != null)
            {
                // Drop the reward at ground level where the rock stood.
                var pos = new Vector3(transform.position.x, 0f, transform.position.z);
                var drop = Instantiate(droppablePrefab, pos, Quaternion.identity);
                drop.GetComponent<NetworkObject>().Spawn();
            }

            var no = GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn();
            else Destroy(gameObject);
        }
    }
}
