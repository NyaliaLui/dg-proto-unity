using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DgProto
{
    /// <summary>
    /// Spawns a random Obstacle from <see cref="obstaclePrefabs"/> on every
    /// Nth ground GrassBlock (sorted by world X). The instantiated obstacle
    /// keeps its prefab's Y/Z; only X is overridden to land on the block.
    ///
    /// Re-rolls on Awake so every playthrough is different. A
    /// [ContextMenu] Respawn entry lets you re-roll in the Editor too.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [Tooltip("Pool of obstacle prefabs. Pick uniformly at random for each spawn slot.")]
        [SerializeField] private GameObject[] obstaclePrefabs;

        [Tooltip("Spawn an obstacle on every Nth ground GrassBlock (sorted by X).")]
        [SerializeField] private int spawnEveryNth = 5;

        [Tooltip("Non-zero = deterministic seed. 0 = fresh randomness each call.")]
        [SerializeField] private int randomSeed = 0;

        [Tooltip("Re-spawn obstacles automatically on Awake (i.e. each time Play starts).")]
        [SerializeField] private bool spawnOnAwake = true;

        private void Awake()
        {
            if (spawnOnAwake) Respawn();
        }

        [ContextMenu("Respawn")]
        public void Respawn()
        {
            if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
            {
                Debug.LogWarning("[ObstacleSpawner] No prefabs assigned — nothing to spawn.");
                return;
            }

            ClearChildren();

            // Collect ground blocks (skip the vertical stacks at the ends).
            var blocks = new List<GameObject>();
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!go.name.StartsWith("GrassBlock_")) continue;
                if (go.name.Contains("Stack")) continue;
                blocks.Add(go);
            }
            blocks.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            var rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

            for (int i = spawnEveryNth - 1; i < blocks.Count; i += spawnEveryNth)
            {
                var block  = blocks[i];
                var prefab = obstaclePrefabs[rng.Next(obstaclePrefabs.Length)];
                if (prefab == null) continue;

                var spawnPos = new Vector3(
                    block.transform.position.x,
                    prefab.transform.position.y, // prefab embeds its intended height
                    prefab.transform.position.z);

                var instance = Instantiate(prefab, spawnPos, prefab.transform.rotation, transform);
                instance.name = prefab.name;
            }
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c);
                else                       DestroyImmediate(c);
            }
        }
    }
}
