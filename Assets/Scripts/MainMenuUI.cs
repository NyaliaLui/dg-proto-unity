using UnityEngine;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// The main-menu screen. Builds its own widgets procedurally (matching the
    /// project's <see cref="NotificationWindow"/> / <see cref="GameOverScreen"/>
    /// style) so the scene only needs a Canvas with this component plus an
    /// EventSystem.
    ///
    /// Flow: a "Find Match" button enters the queue via
    /// <see cref="MatchmakingController"/>; while searching it shows a status +
    /// Cancel; once an opponent is found the host networked-loads the gameplay
    /// scene, which unloads this menu automatically.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class MainMenuUI : MonoBehaviour
    {
        private static readonly Color Bg        = new Color(0.10f, 0.12f, 0.18f, 1f);
        private static readonly Color ButtonCol = new Color(0.20f, 0.45f, 0.85f, 1f);
        private static readonly Color CancelCol = new Color(0.55f, 0.20f, 0.20f, 1f);

        private GameObject _mainPanel;
        private GameObject _searchPanel;
        private Text _statusLabel;
        private MatchmakingController _controller;

        private void Start()
        {
            _controller = MatchmakingController.Instance;
            if (_controller == null) _controller = Object.FindAnyObjectByType<MatchmakingController>();

            BuildBackground();
            BuildMainPanel();
            BuildSearchPanel();
            ShowSearching(false);

            if (_controller != null) _controller.StateChanged += OnMatchState;
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.StateChanged -= OnMatchState;
        }

        private void OnFindMatch()
        {
            ShowSearching(true);
            if (_statusLabel != null) _statusLabel.text = "Searching for an opponent…";
            _controller?.FindMatch();
        }

        private void OnCancel()
        {
            _controller?.Cancel();
            ShowSearching(false);
        }

        private void OnMatchState(MatchmakingState s)
        {
            if (_statusLabel == null) return;
            switch (s)
            {
                case MatchmakingState.Searching: _statusLabel.text = "Searching for an opponent…"; break;
                case MatchmakingState.Matched:   _statusLabel.text = "Opponent found — starting match…"; break;
                case MatchmakingState.Failed:    _statusLabel.text = "Matchmaking failed."; ShowSearching(false); break;
                case MatchmakingState.Cancelled:
                case MatchmakingState.Idle:      ShowSearching(false); break;
            }
        }

        private void ShowSearching(bool searching)
        {
            if (_mainPanel != null) _mainPanel.SetActive(!searching);
            if (_searchPanel != null) _searchPanel.SetActive(searching);
        }

        // ----- procedural UI builders --------------------------------------

        private void BuildBackground()
        {
            var go = NewUI("Background", transform, Bg);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void BuildMainPanel()
        {
            _mainPanel = new GameObject("MainPanel", typeof(RectTransform));
            _mainPanel.transform.SetParent(transform, false);
            Stretch((RectTransform)_mainPanel.transform);

            NewText("Title", _mainPanel.transform, "PALADIN — CO-OP", 64, FontStyle.Bold,
                new Vector2(0f, 160f), new Vector2(900f, 120f));
            MakeButton(_mainPanel.transform, "Find Match", ButtonCol, new Vector2(0f, 0f), OnFindMatch);
        }

        private void BuildSearchPanel()
        {
            _searchPanel = new GameObject("SearchPanel", typeof(RectTransform));
            _searchPanel.transform.SetParent(transform, false);
            Stretch((RectTransform)_searchPanel.transform);

            _statusLabel = NewText("Status", _searchPanel.transform, "Searching for an opponent…", 40,
                FontStyle.Normal, new Vector2(0f, 80f), new Vector2(900f, 120f));
            MakeButton(_searchPanel.transform, "Cancel", CancelCol, new Vector2(0f, -60f), OnCancel);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static GameObject NewUI(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text NewText(string name, Transform parent, string content, int fontSize,
            FontStyle style, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.text = content;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private Button MakeButton(Transform parent, string label, Color bg, Vector2 anchoredPos,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(420f, 90f);
            go.GetComponent<Image>().color = bg;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            NewText("Label", go.transform, label, 36, FontStyle.Bold, Vector2.zero, new Vector2(420f, 90f));
            return go.GetComponent<Button>();
        }
    }
}
