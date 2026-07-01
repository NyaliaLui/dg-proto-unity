using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Online matchmaker built on the Unity 6 <b>Multiplayer Services</b> package
    /// (the Sessions API) — the modern replacement for calling UGS Lobby + Relay
    /// directly (see the retired <c>UgsMatchmaker</c>). Same <see cref="IMatchmaker"/>
    /// contract, so the menu and match lifecycle are untouched.
    ///
    /// A <see cref="ISession"/> bundles the lobby + Relay allocation + transport
    /// wiring and, with <c>WithRelayNetwork()</c>, also <b>starts the
    /// NetworkManager itself</b> (host for the creator, client for the joiner) —
    /// so unlike the old flow we never call <c>StartHost</c>/<c>StartClient</c>,
    /// manage a Relay allocation/join-code, or run a lobby heartbeat.
    ///
    /// Pairing is "open quick-join or create": <see cref="IMultiplayerService.MatchmakeSessionAsync(QuickJoinOptions, SessionOptions)"/>
    /// finds and joins any open session, and creates one (becoming host) if none
    /// is found within the timeout. This needs no UGS Matchmaker dashboard setup —
    /// it reuses the already-enabled Lobby/Relay/Auth services.
    ///
    /// "Matched" still means "connected with an opponent present"; the host then
    /// drives the networked scene load (via <see cref="MatchmakingController"/>,
    /// which keys off NGO's connected-client count, independent of this class).
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class SessionMatchmaker : MonoBehaviour, IMatchmaker
    {
        private const int MaxPlayers = 2;

        [Tooltip("Seconds to search for an open match before hosting one instead.")]
        [SerializeField] private float searchTimeoutSeconds = 3f;

        private NetworkManager _nm;
        private ISession _session;
        private MatchmakingState _state = MatchmakingState.Idle;
        private bool _servicesReady;
        private bool _cancelRequested;

        public MatchmakingState State => _state;
        public event Action<MatchmakingState> StateChanged;

        private void Awake() => _nm = GetComponent<NetworkManager>();

        public void FindMatch()
        {
            if (_state == MatchmakingState.Searching || _state == MatchmakingState.Matched) return;
            _ = FindMatchAsync();
        }

        public void Cancel()
        {
            _ = CancelAsync();
        }

        // ---- core flow -----------------------------------------------------

        private async Task FindMatchAsync()
        {
            try
            {
                _cancelRequested = false;
                SetState(MatchmakingState.Searching);
                await EnsureServicesAsync();
                if (_cancelRequested) { await TearDownAsync(); return; }

                // Quick-join any open session; if none is found within the timeout,
                // create one and become the host. WithRelayNetwork() makes the
                // session start NGO (host/client) and configure the relay transport.
                var quickJoin = new QuickJoinOptions
                {
                    CreateSession = true,
                    Timeout = TimeSpan.FromSeconds(searchTimeoutSeconds),
                };
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = false }
                    .WithRelayNetwork();

                _session = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, options);

                if (_cancelRequested) { await TearDownAsync(); return; }

                // The session already started NGO. Watch for the connection that
                // confirms a full match.
                _nm.OnClientConnectedCallback += OnClientConnected;

                // Joiner: we connected to an existing host → matched. Host: stay
                // Searching until an opponent connects (handled in OnClientConnected).
                if (!_session.IsHost && _nm.IsConnectedClient)
                {
                    SetState(MatchmakingState.Matched);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SessionMatchmaker] FindMatch failed: " + e);
                SetState(MatchmakingState.Failed);
                await TearDownAsync();
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (_session == null) return;
            if (_session.IsHost)
            {
                // A client other than ourselves connected → opponent found.
                if (clientId != _nm.LocalClientId) SetState(MatchmakingState.Matched);
            }
            else
            {
                // Our own client connection completed.
                if (clientId == _nm.LocalClientId) SetState(MatchmakingState.Matched);
            }
        }

        // ---- cancel / cleanup ---------------------------------------------

        private async Task CancelAsync()
        {
            _cancelRequested = true;
            await TearDownAsync();
            SetState(MatchmakingState.Cancelled);
            SetState(MatchmakingState.Idle);
        }

        private async Task TearDownAsync()
        {
            if (_nm != null) _nm.OnClientConnectedCallback -= OnClientConnected;

            if (_session != null)
            {
                // Leaving auto-cleans the lobby + Relay allocation (no manual
                // DeleteLobby / heartbeat as in the old Lobby+Relay flow).
                try { await _session.LeaveAsync(); }
                catch (Exception) { /* session may already be gone */ }
                _session = null;
            }

            if (_nm != null && (_nm.IsListening || _nm.IsClient)) _nm.Shutdown();
        }

        // ---- services init -------------------------------------------------

        private async Task EnsureServicesAsync()
        {
            if (!_servicesReady)
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                _servicesReady = true;
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void SetState(MatchmakingState next)
        {
            if (_state == next) return;
            _state = next;
            StateChanged?.Invoke(next);
        }

        private void OnDestroy()
        {
            if (_nm != null) _nm.OnClientConnectedCallback -= OnClientConnected;
        }
    }
}
