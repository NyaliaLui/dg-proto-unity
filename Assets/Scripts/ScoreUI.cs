using UnityEngine;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// Top-right HUD readout of <see cref="ScoreTracker"/>'s Score. Builds its
    /// own Text child procedurally so no Inspector wiring is needed beyond
    /// parenting this GameObject under the HUD canvas. The Level value is
    /// announced separately by <see cref="LevelAnnouncement"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ScoreUI : MonoBehaviour
    {
        private const int FontSize = 28;
        private const float PanelWidth = 220f;
        private const float PanelHeight = FontSize + 6f;
        private const float Padding = 16f;

        [SerializeField] private Text scoreText;

        private ScoreTracker _tracker;

        private void Awake()
        {
            var rt = (RectTransform)transform;
            // Anchor / pivot top-right corner of the parent canvas.
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rt.anchoredPosition = new Vector2(-Padding, -Padding);

            if (scoreText == null) scoreText = BuildLine("ScoreText", 0f);
        }

        private void Start()
        {
            _tracker = ScoreTracker.Instance;
            if (_tracker == null) return; // no ScoreTracker in this scene
            _tracker.Changed += OnChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_tracker != null) _tracker.Changed -= OnChanged;
        }

        private void OnChanged(ScoreTracker t) => Refresh();

        private void Refresh()
        {
            if (_tracker == null) return;
            if (scoreText != null) scoreText.text = "Score: " + _tracker.Score;
        }

        private Text BuildLine(string name, float yOffset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(0f, FontSize + 6f);
            rt.anchoredPosition = new Vector2(0f, yOffset);

            var t = go.GetComponent<Text>();
            t.text = name;
            t.alignment = TextAnchor.MiddleRight;
            t.color = Color.white;
            t.fontSize = FontSize;
            t.fontStyle = FontStyle.Bold;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            // Add a black shadow component for legibility over the parallax sky.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return t;
        }
    }
}
