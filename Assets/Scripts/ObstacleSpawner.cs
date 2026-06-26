using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DgProto
{
    /// <summary>
    /// Host-authoritative obstacle layout. The server picks a layout and
    /// network-spawns a random Obstacle on every Nth ground GrassBlock (sorted by
    /// X), so every client sees an identical world AND rock breaks / droppables
    /// replicate. Driven once by <see cref="MatchSpawner"/> after the gameplay
    /// scene's networked load completes (so all clients are present).
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [Tooltip("Pool of obstacle prefabs (each a NetworkObject). Picked uniformly per slot.")]
        [SerializeField] private GameObject[] obstaclePrefabs;

        [Tooltip("Spawn an obstacle on every Nth ground GrassBlock (sorted by X).")]
        [SerializeField] private int spawnEveryNth = 5;

        [Tooltip("Non-zero = deterministic seed. 0 = the host picks a fresh seed each match.")]
        [SerializeField] private int randomSeed = 0;

        private bool _built;

        /// <summary>Server-only: build and network-spawn the obstacle layout once.</summary>
        public void BuildLayout()
        {
            if (_built) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
            {
                Debug.LogWarning("[ObstacleSpawner] No prefabs assigned — nothing to spawn.");
                return;
            }
            _built = true;

            // Collect ground blocks (skip the vertical stacks at the ends).
            var blocks = new List<GameObject>();
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!go.name.StartsWith("GrassBlock_")) continue;
                if (go.name.Contains("Stack")) continue;
                blocks.Add(go);
            }
            blocks.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            int seed = randomSeed != 0 ? randomSeed : Random.Range(1, int.MaxValue);
            var rng = new System.Random(seed);

            for (int i = spawnEveryNth - 1; i < blocks.Count; i += spawnEveryNth)
            {
                var prefab = obstaclePrefabs[rng.Next(obstaclePrefabs.Length)];
                if (prefab == null) continue;

                var spawnPos = new Vector3(
                    blocks[i].transform.position.x,
                    prefab.transform.position.y, // prefab embeds its intended height
                    prefab.transform.position.z);

                var instance = Instantiate(prefab, spawnPos, prefab.transform.rotation);
                instance.name = prefab.name;
                instance.GetComponent<NetworkObject>().Spawn(); // replicates to clients
            }
        }
    }
}
