using System.Collections.Generic;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Periodically spawns enemies around the Paladin, up to a maximum alive at
    /// once. Each spawned enemy is given one of the three behavior scripts
    /// (patrol, chaser, platform-hopper) chosen at random.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        // Yaw angles that face a spawned enemy toward +X (right) and -X (left).
        private const float FacingRightYaw = 90f;
        private const float FacingLeftYaw = -90f;

        [Tooltip("Behavior-less Enemy prefab (model + Health + collider + Animator).")]
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

        [Tooltip("Left blank → auto-finds the PaladinController in the scene.")]
        [SerializeField] private Transform player;

        private float _nextSpawnTime;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Start()
        {
            if (player == null)
            {
                var pc = Object.FindAnyObjectByType<PaladinController>();
                if (pc != null) player = pc.transform;
            }
            _nextSpawnTime = Time.time + spawnInterval;
        }

        private void Update()
        {
            // Drop references to enemies that have died.
            _spawned.RemoveAll(e => e == null);

            if (Time.time < _nextSpawnTime) return;
            _nextSpawnTime = Time.time + spawnInterval;

            if (_spawned.Count >= maxEnemies) return;
            SpawnOne();
        }

        private void SpawnOne()
        {
            if (enemyPrefab == null || player == null) return;

            float side = Random.value < 0.5f ? -1f : 1f;
            float dist = Random.Range(minSpawnDistance, maxSpawnDistance);
            float x = Mathf.Clamp(player.position.x + side * dist, groundMinX, groundMaxX);

            bool rightOfPlayer = x > player.position.x;
            var rot = Quaternion.Euler(0f, rightOfPlayer ? FacingLeftYaw : FacingRightYaw, 0f);

            var enemy = Instantiate(enemyPrefab, new Vector3(x, 0f, 0f), rot, transform);
            enemy.name = "Enemy";

            // Award one score point when this enemy dies.
            var health = enemy.GetComponent<Health>();
            if (health != null)
            {
                health.Died += OnEnemyDied;
            }

            // Randomly assign one of the three behaviors.
            switch (Random.Range(0, 3))
            {
                case 0:  enemy.AddComponent<EnemyController>();      break; // patrol
                case 1:  enemy.AddComponent<ChaserEnemy>();          break; // chaser
                default: enemy.AddComponent<PlatformHopperEnemy>();  break; // hopper
            }

            _spawned.Add(enemy);
        }

        private void OnEnemyDied(Health h)
        {
            h.Died -= OnEnemyDied;
            ScoreTracker.Instance.AddEnemyKillPoint();
        }
    }
}
