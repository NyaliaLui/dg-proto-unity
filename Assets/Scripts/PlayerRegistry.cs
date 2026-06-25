using System.Collections.Generic;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Registry of the live player Paladins, used by the host-driven enemy AI to
    /// pick a target. Single-player code found the one Paladin via
    /// <c>FindAnyObjectByType</c>; in co-op there are up to two, spawned at
    /// runtime, so enemies query this registry for the nearest living player and
    /// ignore downed teammates.
    ///
    /// Players register on network spawn and unregister on despawn (see
    /// <see cref="NetworkPlayerSetup"/>). The list only matters on the host
    /// (where enemy AI and the spawner run), but registering everywhere is
    /// harmless since the static list is per-process.
    /// </summary>
    public static class PlayerRegistry
    {
        private static readonly List<Health> Players = new List<Health>();

        public static IReadOnlyList<Health> All => Players;

        public static void Register(Health player)
        {
            if (player != null && !Players.Contains(player)) Players.Add(player);
        }

        public static void Unregister(Health player)
        {
            Players.Remove(player);
        }

        /// <summary>
        /// Nearest player (by X distance) whose Health is still alive, or null if
        /// every registered player is down / none are registered.
        /// </summary>
        public static Health GetNearestLiving(Vector3 from)
        {
            Health best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                if (p == null || p.IsDead) continue;
                float d = Mathf.Abs(p.transform.position.x - from.x);
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }
    }
}
