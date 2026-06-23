using System;
using Unity.Netcode;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// The match orchestrator the menu talks to. It owns the
    /// <see cref="IMatchmaker"/> (currently <see cref="LocalMatchmaker"/>) and,
    /// once two players are connected on the host, drives the networked load of
    /// the gameplay scene so both clients transition into the match together.
    ///
    /// Lives on the persistent Bootstrap object alongside the NetworkManager, so
    /// it survives the menu→gameplay scene load. The host-side scene-load trigger
    /// is intentionally independent of HOW matchmaking happened (local vs UGS):
    /// both paths end with "the server has two connected clients."
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class MatchmakingController : MonoBehaviour
    {
        public static MatchmakingController Instance { get; private set; }

        [Tooltip("Name of the gameplay scene to load once a match forms. Must be in Build Settings.")]
        [SerializeField] private string gameplaySceneName = "SampleScene";

        [Tooltip("How many players make a full match.")]
        [SerializeField] private int playersPerMatch = 2;

        private NetworkManager _nm;
        private IMatchmaker _matchmaker;
        private bool _gameplayLoadStarted;

        /// <summary>Mirrors the matchmaker's state for the UI to render.</summary>
        public event Action<MatchmakingState> StateChanged;
        public MatchmakingState State => _matchmaker != null ? _matchmaker.State : MatchmakingState.Idle;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _nm = GetComponent<NetworkManager>();
            _matchmaker = GetComponent<IMatchmaker>();
        }

        private void OnEnable()
        {
            if (_matchmaker != null) _matchmaker.StateChanged += OnMatchmakerState;
            if (_nm != null) _nm.OnClientConnectedCallback += OnClientConnected;
        }

        private void OnDisable()
        {
            if (_matchmaker != null) _matchmaker.StateChanged -= OnMatchmakerState;
            if (_nm != null) _nm.OnClientConnectedCallback -= OnClientConnected;
            if (Instance == this) Instance = null;
        }

        // ---- UI-facing API -------------------------------------------------

        public void FindMatch()
        {
            _gameplayLoadStarted = false;
            _matchmaker?.FindMatch();
        }

        public void Cancel() => _matchmaker?.Cancel();

        // ---- Internals -----------------------------------------------------

        private void OnMatchmakerState(MatchmakingState s) => StateChanged?.Invoke(s);

        private void OnClientConnected(ulong clientId)
        {
            // Only the host loads the scene, and only once a full match is present.
            if (!_nm.IsServer || _gameplayLoadStarted) return;
            if (_nm.ConnectedClientsList.Count < playersPerMatch) return;

            _gameplayLoadStarted = true;
            // Networked load: NGO loads the gameplay scene on the host AND every
            // connected client, so both players enter the match in lockstep.
            _nm.SceneManager.LoadScene(gameplaySceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
