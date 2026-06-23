using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Local/direct-connect matchmaker — the testable stand-in for UGS Lobby+Relay.
    ///
    /// It implements the same "quick-join or create" semantics UGS Lobby uses, but
    /// over loopback: when you <see cref="FindMatch"/>, it first tries to JOIN an
    /// existing host at <c>127.0.0.1</c>; if nobody is hosting (the connect attempt
    /// doesn't succeed within <see cref="connectTimeout"/>), it becomes the HOST and
    /// waits for an opponent. That mirrors Lobby quick-join-or-create exactly, so
    /// the swap to UGS later changes only this one class.
    ///
    /// Test it with Multiplayer Play Mode: each virtual player presses Find Match;
    /// the first becomes host, the second joins, and both reach <see cref="MatchmakingState.Matched"/>.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class LocalMatchmaker : MonoBehaviour, IMatchmaker
    {
        private const string LoopbackAddress = "127.0.0.1";
        private const ushort DefaultPort = 7777;

        [Tooltip("Seconds to wait for a join to succeed before deciding to host instead.")]
        [SerializeField] private float connectTimeout = 1.5f;

        private NetworkManager _nm;
        private UnityTransport _transport;
        private Coroutine _searchRoutine;
        private MatchmakingState _state = MatchmakingState.Idle;

        public MatchmakingState State => _state;
        public event Action<MatchmakingState> StateChanged;

        private void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
        }

        public void FindMatch()
        {
            if (_state == MatchmakingState.Searching || _state == MatchmakingState.Matched) return;
            if (_searchRoutine != null) StopCoroutine(_searchRoutine);
            _searchRoutine = StartCoroutine(FindMatchRoutine());
        }

        public void Cancel()
        {
            if (_searchRoutine != null) { StopCoroutine(_searchRoutine); _searchRoutine = null; }
            Unsubscribe();
            if (_nm != null && (_nm.IsListening || _nm.IsClient)) _nm.Shutdown();
            SetState(MatchmakingState.Cancelled);
            SetState(MatchmakingState.Idle);
        }

        private IEnumerator FindMatchRoutine()
        {
            SetState(MatchmakingState.Searching);

            // 1) Try to JOIN an existing host.
            _transport.SetConnectionData(LoopbackAddress, DefaultPort);
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.StartClient();

            float elapsed = 0f;
            while (elapsed < connectTimeout && !_nm.IsConnectedClient)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_nm.IsConnectedClient)
            {
                // Joined someone who was hosting → we're the second player.
                SetState(MatchmakingState.Matched);
                _searchRoutine = null;
                yield break;
            }

            // 2) Nobody was hosting → become the host and wait for an opponent.
            _nm.OnClientConnectedCallback -= OnClientConnected;
            _nm.Shutdown();
            // Let the transport fully tear the failed client down before re-starting.
            while (_nm.ShutdownInProgress) yield return null;
            yield return null;

            _transport.SetConnectionData(LoopbackAddress, DefaultPort);
            _nm.OnClientConnectedCallback += OnHostSawClient;
            _nm.StartHost();
            // Stay in Searching; we flip to Matched when a remote client joins.
            _searchRoutine = null;
        }

        // Joiner path: our own client connection completed.
        private void OnClientConnected(ulong clientId)
        {
            if (clientId == _nm.LocalClientId) SetState(MatchmakingState.Matched);
        }

        // Host path: a client other than ourselves connected → opponent found.
        private void OnHostSawClient(ulong clientId)
        {
            if (clientId != _nm.LocalClientId) SetState(MatchmakingState.Matched);
        }

        private void Unsubscribe()
        {
            if (_nm == null) return;
            _nm.OnClientConnectedCallback -= OnClientConnected;
            _nm.OnClientConnectedCallback -= OnHostSawClient;
        }

        private void SetState(MatchmakingState next)
        {
            if (_state == next) return;
            _state = next;
            StateChanged?.Invoke(next);
        }

        private void OnDestroy() => Unsubscribe();
    }
}
