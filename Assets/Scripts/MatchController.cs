using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace DgProto
{
    /// <summary>
    /// Drives the synchronized pre-match countdown and the gated start. Lives as
    /// a scene NetworkObject in the gameplay scene, so it spawns on the host and
    /// every client during the networked load.
    ///
    /// The countdown is synchronized the correct way: the host (authority) picks a
    /// single start moment on the shared network clock and replicates it; every
    /// client renders its 3→2→1 against that one clock and unlocks input at the
    /// same instant. This avoids the classic bug of three independent local
    /// <c>WaitForSeconds</c> timers drifting apart so players start at different
    /// real-world moments.
    ///
    /// Player controllers spawn DISABLED (see <see cref="NetworkPlayerSetup"/>);
    /// this component enables each client's own player exactly when the countdown
    /// hits zero.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MatchController : NetworkBehaviour
    {
        [SerializeField] private float countdownSeconds = 3f;

        // 0 = not started. Server writes; everyone reads. Rendered against
        // NetworkManager.ServerTime so all clients agree within a frame.
        private readonly NetworkVariable<double> _matchStartTime =
            new NetworkVariable<double>(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool _started;
        private Text _label;
        private Canvas _canvas;

        /// <summary>
        /// True once the countdown has reached "GO" on this instance. On the
        /// server this gates host-authoritative systems (e.g. enemy spawning) so
        /// nothing acts against the players before the match actually starts.
        /// </summary>
        public bool HasStarted => _started;

        /// <summary>Server-only: begin the countdown from now + countdownSeconds.</summary>
        public void BeginCountdown()
        {
            if (!IsServer) return;
            _matchStartTime.Value = NetworkManager.ServerTime.Time + countdownSeconds;
        }

        private void Update()
        {
            if (!IsSpawned || _started) return;

            double start = _matchStartTime.Value;
            if (start <= 0d) return; // countdown not begun yet

            double remaining = start - NetworkManager.ServerTime.Time;
            if (remaining > 0d)
            {
                ShowLabel(Mathf.CeilToInt((float)remaining).ToString());
            }
            else
            {
                _started = true;
                ShowLabel("GO!");
                EnableLocalPlayer();
                StartCoroutine(HideAfter(0.6f));
            }
        }

        // Enable only THIS client's own player; remote proxies stay disabled.
        private void EnableLocalPlayer()
        {
            var po = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            if (po == null) return;
            var controller = po.GetComponent<PaladinController>();
            if (controller != null) controller.enabled = true;
        }

        // ----- countdown UI (built lazily on each client) ------------------

        private void ShowLabel(string text)
        {
            if (_canvas == null) BuildUI();
            if (_label != null && _label.text != text) _label.text = text;
            if (_canvas != null && !_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        private void BuildUI()
        {
            var go = new GameObject("CountdownCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // above the HUD
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var textGo = new GameObject("CountdownText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(600f, 300f);

            _label = textGo.GetComponent<Text>();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.fontSize = 200;
            _label.fontStyle = FontStyle.Bold;
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(5f, -5f);
        }

        public override void OnNetworkDespawn()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
