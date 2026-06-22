using System;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Global score and level state. One enemy kill = +1 point; the level
    /// advances by 1 for every <see cref="PointsPerLevel"/> points scored.
    /// Reset to 0/1 on scene load so the Restart flow starts fresh.
    /// </summary>
    public class ScoreTracker : MonoBehaviour
    {
        public const int PointsPerLevel = 5;

        private static ScoreTracker _instance;

        /// <summary>Singleton accessor; lazily creates a hidden host on first use.</summary>
        public static ScoreTracker Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var existing = UnityEngine.Object.FindAnyObjectByType<ScoreTracker>();
                if (existing != null)
                {
                    _instance = existing;
                    return _instance;
                }
                var go = new GameObject("ScoreTracker");
                _instance = go.AddComponent<ScoreTracker>();
                return _instance;
            }
        }

        [SerializeField] private int score;
        [SerializeField] private int level = 1;

        public int Score => score;
        public int Level => level;

        /// <summary>Raised whenever Score or Level changes.</summary>
        public event Action<ScoreTracker> Changed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Adds one point (one enemy kill) and recomputes the level.</summary>
        public void AddEnemyKillPoint()
        {
            score += 1;
            level = 1 + (score / PointsPerLevel);
            Changed?.Invoke(this);
        }

        /// <summary>Resets score to 0 and level to 1 (used on Restart).</summary>
        public void ResetProgress()
        {
            score = 0;
            level = 1;
            Changed?.Invoke(this);
        }
    }
}
