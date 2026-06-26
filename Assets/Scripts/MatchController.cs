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

        // Flips true (server-write) once both Paladins are down. Replicated so
        // every client shows the game-over screen at the same moment.
        private readonly NetworkVariable<bool> _matchOver =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Tooltip("Social link shown on the match-end screen.")]
        [SerializeField] private string facebookUrl = "https://www.facebook.com/profile.php?id=61572357196698";

        private bool _started;
        private bool _gameOverShown;
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

        public override void OnNetworkSpawn()
        {
            _matchOver.OnValueChanged += OnMatchOverChanged;
            if (_matchOver.Value) ShowGameOver(); // covers a late spawn into an ended match
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (!_started)
            {
                TickCountdown();
                return;
            }

            // Match running: the host watches for "both Paladins down" → match end.
            if (IsServer && !_matchOver.Value && AllPlayersDown())
            {
                _matchOver.Value = true;
            }
        }

        private void TickCountdown()
        {
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

        // ----- match end (both players down) -------------------------------

        // True only when there is at least one registered player and every one of
        // them is down. A single death leaves the teammate playing on.
        private static bool AllPlayersDown()
        {
            var players = PlayerRegistry.All;
            if (players.Count == 0) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && !p.IsDead) return false;
            }
            return true;
        }

        private void OnMatchOverChanged(bool previous, bool current)
        {
            if (current) ShowGameOver();
        }

        private void ShowGameOver()
        {
            if (_gameOverShown) return;
            _gameOverShown = true;

            int finalScore = ScoreTracker.Instance != null ? ScoreTracker.Instance.Score : 0;
            AudioManager.Instance.Play(SfxId.GameOver);
            GameOverScreen.Show(
                "Both Paladins have fallen! Final score: " + finalScore +
                ". Show support for work like this by following the comic book on social media.",
                "Facebook",
                facebookUrl,
                "Return to Menu",
                ReturnToMenu);
        }

        // Restart for co-op: tear down the network session and go back to the menu
        // (NOT a local scene reload — the gameplay scene has no NetworkManager).
        private void ReturnToMenu()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsClient)) nm.Shutdown();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
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
            _matchOver.OnValueChanged -= OnMatchOverChanged;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
