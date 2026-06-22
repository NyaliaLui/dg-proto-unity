using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// ScriptableObject mapping each <see cref="SfxId"/> to one or more
    /// AudioClips. Multiple clips per id are picked at random for variety
    /// (e.g. footsteps, attack swings). Create the asset via
    /// Assets → Create → DgProto → Sound Bank.
    /// </summary>
    [CreateAssetMenu(menuName = "DgProto/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public SfxId id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0f, 0.5f)] public float pitchJitter = 0.05f; // ± pitch around 1.0
            [Tooltip("If > 0, playback stops after this many seconds (truncates long clips). 0 = play to end.")]
            [Range(0f, 10f)] public float maxDuration = 0f;
        }

        [Tooltip("One Entry per SfxId. Designer-driven — drag clips in. " +
                 "Empty arrays produce no sound (silent fallback).")]
        [SerializeField] private Entry[] entries;

        /// <summary>
        /// Returns a random clip for the given id, or <c>null</c> if no clips
        /// are assigned. Also outputs the per-entry volume, pitch jitter, and
        /// max-duration cap (0 = no cap, play to end of clip).
        /// </summary>
        public AudioClip GetRandom(SfxId id, out float volume, out float pitchJitter, out float maxDuration)
        {
            volume = 1f;
            pitchJitter = 0f;
            maxDuration = 0f;
            if (entries == null) return null;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e == null || e.id != id) continue;
                volume = e.volume;
                pitchJitter = e.pitchJitter;
                maxDuration = e.maxDuration;
                if (e.clips == null || e.clips.Length == 0) return null;
                int idx = Random.Range(0, e.clips.Length);
                return e.clips[idx];
            }
            return null;
        }
    }
}
