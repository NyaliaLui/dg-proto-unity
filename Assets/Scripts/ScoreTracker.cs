using System;
using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Host-authoritative shared team score/level for the co-op match. One enemy
    /// kill = +1 point; the level advances by 1 for every
    /// <see cref="PointsPerLevel"/> points. Score and level live in
    /// server-write <see cref="NetworkVariable{T}"/>s so both players see one
    /// shared score; <see cref="Changed"/> fires on every client when either
    /// changes.
    ///
    /// Lives as a scene <see cref="NetworkObject"/> in the gameplay scene (it
    /// auto-spawns on the networked load), so <see cref="Instance"/> only finds
    /// it — it is never created at runtime (a NetworkBehaviour can't be).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ScoreTracker : NetworkBehaviour
    {
        public const int PointsPerLevel = 5;

        private static ScoreTracker _instance;

        /// <summary>Scene singleton accessor; finds the scene-placed instance.</summary>
        public static ScoreTracker Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = UnityEngine.Object.FindAnyObjectByType<ScoreTracker>();
                return _instance;
            }
        }

        // Server writes; everyone reads. Level defaults to 1 so the HUD reads a
        // sane value even before the networked spawn.
        private readonly NetworkVariable<int> score =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> level =
            new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int Score => score.Value;
        public int Level => level.Value;

        /// <summary>Raised whenever Score or Level changes, on every client.</summary>
        public event Action<ScoreTracker> Changed;

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        public override void OnNetworkSpawn()
        {
            score.OnValueChanged += OnScoreValueChanged;
            level.OnValueChanged += OnLevelValueChanged;
            Changed?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            score.OnValueChanged -= OnScoreValueChanged;
            level.OnValueChanged -= OnLevelValueChanged;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnScoreValueChanged(int previous, int current) => Changed?.Invoke(this);
        private void OnLevelValueChanged(int previous, int current) => Changed?.Invoke(this);

        /// <summary>Server-only: adds one point (one enemy kill) and recomputes the level.</summary>
        public void AddEnemyKillPoint()
        {
            if (!IsServer) return;
            score.Value += 1;
            level.Value = 1 + (score.Value / PointsPerLevel);
        }

        /// <summary>Server-only: resets score to 0 and level to 1 (used on rematch).</summary>
        public void ResetProgress()
        {
            if (!IsServer) return;
            score.Value = 0;
            level.Value = 1;
        }
    }
}
