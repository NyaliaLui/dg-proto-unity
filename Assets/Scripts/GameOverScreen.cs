using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// Modal game-over screen built procedurally at runtime. Pauses the game,
    /// offers a social link, and a Restart button that reloads the scene.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        private static readonly Color ButtonBlue = new Color(0.20f, 0.45f, 0.85f, 1f);
        private static readonly Color ButtonRed  = new Color(0.85f, 0.20f, 0.20f, 1f);
        private const float BorderThickness = 3f;

        private string _linkUrl;
        private System.Action _onRestart;

        /// <summary>
        /// Spawn the game-over screen. If <paramref name="onRestart"/> is supplied
        /// the Restart button runs it (e.g. the co-op match shuts the network down
        /// and returns to the menu); otherwise it reloads the active scene.
        /// </summary>
        public static GameOverScreen Show(string message, string linkLabel, string linkUrl, string restartLabel,
            System.Action onRestart = null)
        {
            var go = new GameObject("GameOverScreen");
            var screen = go.AddComponent<GameOverScreen>();
            screen._onRestart = onRestart;
            screen.Build(message, linkLabel, linkUrl, restartLabel);
            return screen;
        }

        private void Build(string message, string linkLabel, string linkUrl, string restartLabel)
        {
            _linkUrl = linkUrl;
            Time.timeScale = 0f;

            // --- Canvas (on top of everything) ---
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // --- Full-screen dim (also swallows input to the game behind) ---
            var dim = NewUI("Dim", transform, new Color(0f, 0f, 0f, 0.8f));
            var dimRt = (RectTransform)dim.transform;
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;

            // --- Thin black border ---
            var border = NewUI("Border", dim.transform, Color.black);
            var borderRt = (RectTransform)border.transform;
            borderRt.anchorMin = new Vector2(0.5f, 0.5f);
            borderRt.anchorMax = new Vector2(0.5f, 0.5f);
            borderRt.pivot     = new Vector2(0.5f, 0.5f);
            borderRt.sizeDelta = new Vector2(920f, 560f);
            borderRt.anchoredPosition = Vector2.zero;

            // --- Inner panel ---
            var panel = NewUI("Panel", border.transform, new Color(0.98f, 0.98f, 0.98f, 1f));
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = new Vector2( BorderThickness,  BorderThickness);
            panelRt.offsetMax = new Vector2(-BorderThickness, -BorderThickness);

            // --- Message (centered, top half) ---
            var msg = NewText("Message", panel.transform, message, 38, FontStyle.Bold, Color.black);
            var msgRt = (RectTransform)msg.transform;
            msgRt.anchorMin = new Vector2(0f, 0.45f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.offsetMin = new Vector2( 50f, 20f);
            msgRt.offsetMax = new Vector2(-50f, -40f);

            // --- Stacked buttons ---
            MakeButton(panel.transform, linkLabel,    ButtonBlue, new Vector2(0f, -60f),  OnLink);
            MakeButton(panel.transform, restartLabel, ButtonRed,  new Vector2(0f, -180f), OnRestart);
        }

        private void OnLink()
        {
            if (!string.IsNullOrEmpty(_linkUrl)) Application.OpenURL(_linkUrl);
            // Screen stays up so the player can still hit Restart afterward.
        }

        private void OnRestart()
        {
            // Restore time before reloading — LoadScene does not reset it.
            Time.timeScale = 1f;
            if (_onRestart != null) _onRestart();
            else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ----- helpers -----------------------------------------------------

        private static GameObject NewUI(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static Text NewText(string name, Transform parent, string content, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private Button MakeButton(Transform parent, string label, Color bg, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 90f);
            rt.anchoredPosition = anchoredPos;

            go.GetComponent<Image>().color = bg;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var lbl = NewText("Label", go.transform, label, 40, FontStyle.Bold, Color.white);
            var lblRt = (RectTransform)lbl.transform;
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }
    }
}
