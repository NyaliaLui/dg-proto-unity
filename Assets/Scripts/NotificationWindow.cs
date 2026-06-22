using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// Modal notification window built procedurally at runtime. Pauses the game
    /// while shown (Time.timeScale = 0) and resumes on close.
    /// </summary>
    public class NotificationWindow : MonoBehaviour
    {
        private static readonly Color ButtonBlue = new Color(0.20f, 0.45f, 0.85f, 1f);
        private static readonly Color ButtonRed  = new Color(0.85f, 0.20f, 0.20f, 1f);
        private const float BorderThickness = 3f;

        private string _url;
        private float _prevTimeScale;

        /// <summary>Spawn a notification window in the current scene.</summary>
        public static NotificationWindow Show(string message, string primaryLabel, string primaryUrl, string closeLabel)
        {
            var go = new GameObject("NotificationWindow");
            var win = go.AddComponent<NotificationWindow>();
            win.Build(message, primaryLabel, primaryUrl, closeLabel);
            return win;
        }

        private void Build(string message, string primaryLabel, string primaryUrl, string closeLabel)
        {
            _url = primaryUrl;
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // --- Canvas root (Screen Space Overlay, on top of everything) ---
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists somewhere in the scene.
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // --- Dim overlay (full screen, also blocks input behind us) ---
            var dim = NewUI("Dim", transform, new Color(0f, 0f, 0f, 0.6f));
            var dimRt = (RectTransform)dim.transform;
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;

            // --- Border (thin black) ---
            var border = NewUI("Border", dim.transform, Color.black);
            var borderRt = (RectTransform)border.transform;
            borderRt.anchorMin = new Vector2(0.5f, 0.5f);
            borderRt.anchorMax = new Vector2(0.5f, 0.5f);
            borderRt.pivot     = new Vector2(0.5f, 0.5f);
            borderRt.sizeDelta = new Vector2(900f, 560f);
            borderRt.anchoredPosition = Vector2.zero;

            // --- Inner panel (light) — offset gives the thin-border look ---
            var panel = NewUI("Panel", border.transform, new Color(0.98f, 0.98f, 0.98f, 1f));
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = new Vector2( BorderThickness,  BorderThickness);
            panelRt.offsetMax = new Vector2(-BorderThickness, -BorderThickness);

            // --- Message text (centered, top half) ---
            var msg = NewText("Message", panel.transform, message, 36, FontStyle.Normal, Color.black);
            var msgRt = (RectTransform)msg.transform;
            msgRt.anchorMin = new Vector2(0f, 0.45f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.offsetMin = new Vector2( 40f, 20f);
            msgRt.offsetMax = new Vector2(-40f, -40f);

            // --- Buttons (stacked vertically in lower half) ---
            MakeButton(panel.transform, primaryLabel, ButtonBlue, new Vector2(0f, -60f),  OnPrimary);
            MakeButton(panel.transform, closeLabel,   ButtonRed,  new Vector2(0f, -180f), Close);
        }

        private void OnPrimary()
        {
            if (!string.IsNullOrEmpty(_url)) Application.OpenURL(_url);
            // Leave the notification up so the player can still close it after.
        }

        private void Close()
        {
            Time.timeScale = _prevTimeScale;
            Destroy(gameObject);
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
