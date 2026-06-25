using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// Center-of-screen "Level N" announcement. Hidden by default; flashes the
    /// current level on game start and each time <see cref="ScoreTracker"/>
    /// crosses a level threshold. Builds its own Text + CanvasGroup so the
    /// only scene wiring needed is parenting this GameObject under the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LevelAnnouncement : MonoBehaviour
    {
        private const int FontSize = 96;
        private const float PanelWidth = 800f;
        private const float PanelHeight = 160f;

        // Show sequence timing (seconds).
        private const float FadeInDuration  = 0.15f;
        private const float HoldDuration    = 1.0f;
        private const float FadeOutDuration = 0.5f;

        [SerializeField] private Text levelText;
        [SerializeField] private CanvasGroup group;

        private ScoreTracker _tracker;
        private int _lastShownLevel = -1;
        private Coroutine _showRoutine;

        private void Awake()
        {
            // Anchor stretched across the canvas with pivot at center so the
            // text sits in the middle of the screen at any aspect ratio.
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rt.anchoredPosition = Vector2.zero;

            if (group == null)
            {
                group = gameObject.GetComponent<CanvasGroup>();
                if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            if (levelText == null) levelText = BuildText();
        }

        private void Start()
        {
            _tracker = ScoreTracker.Instance;
            if (_tracker != null) _tracker.Changed += OnScoreChanged;

            // Wake the AudioManager early so the GameStart SFX overlapping with
            // the very first announcement isn't dropped.
            var audio = AudioManager.Instance;
            audio.Play(SfxId.GameStart);

            // Announce the starting level.
            Show(_tracker != null ? _tracker.Level : 1);
        }

        private void OnDestroy()
        {
            if (_tracker != null) _tracker.Changed -= OnScoreChanged;
        }

        private void OnScoreChanged(ScoreTracker t)
        {
            if (t.Level != _lastShownLevel)
            {
                // Skip the SFX on the very first show (game-start handles that);
                // afterwards every level change gets the LevelUp sting.
                if (_lastShownLevel >= 0) AudioManager.Instance.Play(SfxId.LevelUp);
                Show(t.Level);
            }
        }

        /// <summary>Plays the fade-in / hold / fade-out sequence for "Level N".</summary>
        private void Show(int level)
        {
            _lastShownLevel = level;
            if (levelText != null) levelText.text = "Level " + level;

            if (_showRoutine != null) StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            // Fade in
            float t = 0f;
            while (t < FadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / FadeInDuration);
                yield return null;
            }
            group.alpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(HoldDuration);

            // Fade out
            t = 0f;
            while (t < FadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / FadeOutDuration);
                yield return null;
            }
            group.alpha = 0f;
            _showRoutine = null;
        }

        private Text BuildText()
        {
            var go = new GameObject("LevelText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = go.GetComponent<Text>();
            txt.text = "Level";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = FontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            // Black drop-shadow for legibility against the parallax sky.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(4f, -4f);

            return txt;
        }
    }
}
