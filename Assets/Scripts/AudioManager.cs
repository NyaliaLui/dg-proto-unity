using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace DgProto
{
    /// <summary>
    /// Central SFX router. Lazy-create singleton matching the
    /// <see cref="ScoreTracker"/> pattern. Exposes a flat Play API; resolves
    /// clips through a <see cref="SoundBank"/> ScriptableObject; routes the
    /// AudioSource through the AudioMixer's SFX group so master / SFX volumes
    /// can be tuned independently and persisted in PlayerPrefs.
    ///
    /// The mixer file <c>Assets/Resources/Audio/MainMixer.mixer</c> defines
    /// UI and Music groups as well, but the game doesn't currently route any
    /// audio through them — added back later when needed without touching the
    /// mixer asset.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // PlayerPrefs keys for volume persistence (0..1 floats, converted to dB).
        private const string PrefMaster = "audio.master";
        private const string PrefSfx    = "audio.sfx";

        // Mixer exposed-parameter names (must match the names in MainMixer.mixer).
        private const string ParamMaster = "MasterVolume";
        private const string ParamSfx    = "SFXVolume";

        private const float MinDb = -80f; // true mute at 0 linear

        // Resource paths (Resources/ subfolders are auto-loadable at runtime).
        private const string ResourceMixer     = "Audio/MainMixer";
        private const string ResourceSoundBank = "Audio/SoundBank";

        private static AudioManager _instance;

        /// <summary>Singleton accessor; lazily creates a hidden host on first use.</summary>
        public static AudioManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var existing = Object.FindAnyObjectByType<AudioManager>();
                if (existing != null)
                {
                    _instance = existing;
                    return _instance;
                }
                var go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
                return _instance;
            }
        }

        // Cached, resolved at runtime — not Inspector-wired since the singleton
        // is lazy-created.
        private AudioMixer _mixer;
        private AudioMixerGroup _sfxGroup;
        private SoundBank _bank;
        private AudioSource _sfxSource;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            // Load mixer + bank from Resources so the lazy-create path works.
            _mixer = Resources.Load<AudioMixer>(ResourceMixer);
            _bank  = Resources.Load<SoundBank>(ResourceSoundBank);

            if (_mixer != null)
            {
                var sfxMatches = _mixer.FindMatchingGroups("Master/SFX");
                if (sfxMatches != null && sfxMatches.Length > 0) _sfxGroup = sfxMatches[0];
            }

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            if (_sfxGroup != null) _sfxSource.outputAudioMixerGroup = _sfxGroup;

            // Apply persisted volumes (defaults 1.0 if unset).
            ApplyVolume(ParamMaster, PlayerPrefs.GetFloat(PrefMaster, 1f));
            ApplyVolume(ParamSfx,    PlayerPrefs.GetFloat(PrefSfx,    1f));
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---- Public API ---------------------------------------------------

        /// <summary>Play a one-shot SFX through the SFX mixer group.</summary>
        public void Play(SfxId id)
        {
            PlayOn(_sfxSource, id);
        }

        /// <summary>0..1 linear master volume; persisted to PlayerPrefs.</summary>
        public void SetMasterVolume(float v) { SetAndPersist(ParamMaster, PrefMaster, v); }
        public void SetSfxVolume(float v)    { SetAndPersist(ParamSfx,    PrefSfx,    v); }

        // ---- Internals ----------------------------------------------------

        private void PlayOn(AudioSource src, SfxId id)
        {
            if (src == null || _bank == null) return;
            float vol, jitter, maxDuration;
            var clip = _bank.GetRandom(id, out vol, out jitter, out maxDuration);
            if (clip == null) return;
            float pitch = (jitter > 0f) ? (1f + Random.Range(-jitter, jitter)) : 1f;

            // If a max-duration cap is set and shorter than the clip's effective
            // playback length, we can't use PlayOneShot (it can't be stopped
            // mid-play). Spawn a temporary AudioSource and Stop() it after
            // maxDuration seconds.
            float effectiveClipLen = clip.length / Mathf.Max(0.01f, pitch);
            if (maxDuration > 0f && maxDuration < effectiveClipLen)
            {
                var tmp = gameObject.AddComponent<AudioSource>();
                tmp.clip = clip;
                tmp.volume = vol;
                tmp.pitch = pitch;
                tmp.spatialBlend = 0f;
                tmp.playOnAwake = false;
                tmp.outputAudioMixerGroup = src.outputAudioMixerGroup;
                tmp.Play();
                StartCoroutine(StopAndDestroy(tmp, maxDuration));
                return;
            }

            float prevPitch = src.pitch;
            src.pitch = pitch;
            src.PlayOneShot(clip, vol);
            src.pitch = prevPitch;
        }

        private IEnumerator StopAndDestroy(AudioSource src, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (src != null)
            {
                src.Stop();
                Destroy(src);
            }
        }

        private void SetAndPersist(string param, string prefKey, float linear)
        {
            linear = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(prefKey, linear);
            ApplyVolume(param, linear);
        }

        private void ApplyVolume(string param, float linear)
        {
            if (_mixer == null) return;
            float db = (linear <= 0.0001f) ? MinDb : Mathf.Log10(linear) * 20f;
            _mixer.SetFloat(param, db);
        }
    }
}
