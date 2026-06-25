using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Host-authoritative enemy spawner. Only the server spawns and simulates
    /// enemies; each spawned enemy is a NetworkObject that replicates to clients
    /// via its NetworkTransform / NetworkAnimator. The behaviour scripts (patrol,
    /// chaser, platform-hopper) are added on the host only, so the AI runs in one
    /// place and its results are mirrored to everyone.
    ///
    /// Spawns near a randomly-chosen living player, up to a maximum alive at once.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        // Yaw angles that face a spawned enemy toward +X (right) and -X (left).
        private const float FacingRightYaw = 90f;
        private const float FacingLeftYaw = -90f;

        [Tooltip("Behavior-less Enemy prefab (NetworkObject + model + Health + collider + Animator).")]
        [SerializeField] private GameObject enemyPrefab;

        [Tooltip("Seconds between spawn attempts.")]
        [SerializeField] private float spawnInterval = 2f;

        [Tooltip("Maximum number of enemies allowed alive at once.")]
        [SerializeField] private int maxEnemies = 3;

        [Header("Spawn placement")]
        [SerializeField] private float minSpawnDistance = 8f;
        [SerializeField] private float maxSpawnDistance = 15f;
        [SerializeField] private float groundMinX = -135f;
        [SerializeField] private float groundMaxX = 145f;

        private float _nextSpawnTime;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private MatchController _match;

        private static bool IsServer =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Start()
        {
            _nextSpawnTime = Time.time + spawnInterval;
            _match = Object.FindAnyObjectByType<MatchController>();
        }

        private void Update()
        {
            // Enemies are host-authoritative — clients receive replicated objects.
            if (!IsServer) return;

            // Don't spawn (or let enemies act) before the synchronized countdown
            // reaches "GO" — otherwise enemies would attack players who can't yet
            // move. MatchController owns that gate.
            if (_match != null && !_match.HasStarted) return;

            // Drop references to enemies that have despawned/died.
            _spawned.RemoveAll(e => e == null);

            if (Time.time < _nextSpawnTime) return;
            _nextSpawnTime = Time.time + spawnInterval;

            if (_spawned.Count >= maxEnemies) return;
            SpawnOne();
        }

        private void SpawnOne()
        {
            if (enemyPrefab == null) return;

            // Spawn relative to a random living player; if everyone's down, hold.
            var reference = PickReferencePlayer();
            if (reference == null) return;

            float px = reference.transform.position.x;
            float side = Random.value < 0.5f ? -1f : 1f;
            float dist = Random.Range(minSpawnDistance, maxSpawnDistance);
            float x = Mathf.Clamp(px + side * dist, groundMinX, groundMaxX);

            bool rightOfPlayer = x > px;
            var rot = Quaternion.Euler(0f, rightOfPlayer ? FacingLeftYaw : FacingRightYaw, 0f);

            var enemy = Instantiate(enemyPrefab, new Vector3(x, 0f, 0f), rot);
            enemy.name = "Enemy";

            // Server-spawn so the enemy replicates to every client.
            var netObj = enemy.GetComponent<NetworkObject>();
            netObj.Spawn();

            // Award one shared score point when this enemy dies (host-side).
            var health = enemy.GetComponent<Health>();
            if (health != null) health.Died += OnEnemyDied;

            // Behaviours run on the host only (added after the spawn). Movement and
            // animation reach clients through the enemy's NetworkTransform /
            // NetworkAnimator.
            switch (Random.Range(0, 3))
            {
                case 0:  enemy.AddComponent<EnemyController>();      break; // patrol
                case 1:  enemy.AddComponent<ChaserEnemy>();          break; // chaser
                default: enemy.AddComponent<PlatformHopperEnemy>();  break; // hopper
            }

            _spawned.Add(enemy);
        }

        // Reservoir-samples one living player so spawns aren't biased toward the
        // first-registered player.
        private static Health PickReferencePlayer()
        {
            var players = PlayerRegistry.All;
            Health pick = null;
            int liveCount = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p.IsDead) continue;
                liveCount++;
                if (Random.Range(0, liveCount) == 0) pick = p;
            }
            return pick;
        }

        private void OnEnemyDied(Health h)
        {
            h.Died -= OnEnemyDied;
            if (ScoreTracker.Instance != null) ScoreTracker.Instance.AddEnemyKillPoint();
        }
    }
}
