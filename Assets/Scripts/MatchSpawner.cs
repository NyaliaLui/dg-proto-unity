using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Server-authoritative player spawner. We deliberately do NOT use NGO's
    /// connect-time auto-spawn (NetworkConfig.PlayerPrefab is left empty) because
    /// that would spawn the Paladin while players are still on the menu — falling
    /// through empty space and retargeting a camera that doesn't exist yet.
    ///
    /// Instead we wait for the gameplay scene to finish its networked load on all
    /// clients, then spawn one Paladin per connected client at a spawn point. Each
    /// Paladin's <see cref="NetworkPlayerSetup"/> then runs in the gameplay scene,
    /// where the camera and ground actually exist.
    ///
    /// Lives on the persistent Bootstrap object next to the NetworkManager.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class MatchSpawner : MonoBehaviour
    {
        [Tooltip("The networked Paladin player prefab to spawn per client.")]
        [SerializeField] private GameObject paladinPrefab;

        [Tooltip("Scene whose load completion triggers player spawning.")]
        [SerializeField] private string gameplaySceneName = "SampleScene";

        [Tooltip("X positions players spawn at (cycled per player), on the ground at y=0.")]
        [SerializeField] private float[] spawnXs = { -2f, 2f };

        private NetworkManager _nm;

        private void Awake() => _nm = GetComponent<NetworkManager>();

        private void OnEnable()
        {
            if (_nm != null) _nm.OnServerStarted += OnServerStarted;
        }

        private void OnDisable()
        {
            if (_nm != null)
            {
                _nm.OnServerStarted -= OnServerStarted;
                if (_nm.SceneManager != null)
                    _nm.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            }
        }

        private void OnServerStarted()
        {
            // SceneManager only exists once networking has started.
            _nm.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }

        // Fires on the server after every client finishes loading the scene.
        private void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode,
            List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!_nm.IsServer || sceneName != gameplaySceneName) return;
            if (paladinPrefab == null) { Debug.LogError("[MatchSpawner] paladinPrefab not assigned."); return; }

            int i = 0;
            foreach (ulong clientId in _nm.ConnectedClientsIds)
            {
                float x = spawnXs.Length > 0 ? spawnXs[i % spawnXs.Length] : 0f;
                var go = Instantiate(paladinPrefab, new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
                i++;
            }

            // Players are in (controllers disabled). Kick off the synchronized
            // pre-match countdown, which unlocks input on "GO".
            var match = Object.FindAnyObjectByType<MatchController>();
            if (match != null) match.BeginCountdown();
            else Debug.LogWarning("[MatchSpawner] No MatchController in scene — countdown won't start and input stays locked.");
        }
    }
}
