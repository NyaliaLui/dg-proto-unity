using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Real online matchmaker over Unity Gaming Services — the cloud counterpart
    /// to <see cref="LocalMatchmaker"/>, behind the same <see cref="IMatchmaker"/>
    /// contract so nothing else in the match flow changes.
    ///
    /// It implements the same "quick-join or create" idea, now across the
    /// internet: sign in anonymously, try to QUICK-JOIN any open lobby; if there
    /// are none, CREATE one (becoming the host) and wait. Either way a Relay
    /// allocation carries the traffic, so the two phones connect without public
    /// IPs or port-forwarding. The host writes its Relay join code into the
    /// lobby's data; the joiner reads it and connects to the same allocation.
    ///
    /// Requires the project linked to a UGS cloud project with Relay, Lobby, and
    /// Anonymous Authentication enabled.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class UgsMatchmaker : MonoBehaviour, IMatchmaker
    {
        private const string RelayJoinCodeKey = "relayJoinCode";
        private const string ConnectionType = "dtls";
        private const int MaxPlayers = 2;
        private const float HeartbeatInterval = 15f;

        private NetworkManager _nm;
        private UnityTransport _transport;
        private MatchmakingState _state = MatchmakingState.Idle;

        private string _hostedLobbyId;     // set when we created (host) a lobby
        private Coroutine _heartbeat;
        private bool _servicesReady;

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
                SetState(MatchmakingState.Searching);
                await EnsureServicesAsync();

                // 1) Try to JOIN any open lobby.
                Lobby lobby = null;
                try
                {
                    lobby = await LobbyService.Instance.QuickJoinLobbyAsync();
                }
                catch (LobbyServiceException)
                {
                    lobby = null; // none open → we'll host below
                }

                if (lobby != null)
                {
                    await JoinAsClient(lobby);
                }
                else
                {
                    await HostNewMatch();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[UgsMatchmaker] FindMatch failed: " + e);
                SetState(MatchmakingState.Failed);
            }
        }

        private async Task JoinAsClient(Lobby lobby)
        {
            string joinCode = lobby.Data[RelayJoinCodeKey].Value;
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            _transport.SetRelayServerData(new RelayServerData(joinAllocation, ConnectionType));

            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.StartClient();
            // Matched is confirmed once our local client connection completes.
        }

        private async Task HostNewMatch()
        {
            // maxConnections excludes the host itself → 1 peer for a 2-player match.
            var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };
            var lobby = await LobbyService.Instance.CreateLobbyAsync("Paladin Co-op", MaxPlayers, options);
            _hostedLobbyId = lobby.Id;
            _heartbeat = StartCoroutine(HeartbeatLoop());

            _transport.SetRelayServerData(new RelayServerData(allocation, ConnectionType));

            _nm.OnClientConnectedCallback += OnHostSawClient;
            _nm.StartHost();
            // Stay Searching until an opponent joins (OnHostSawClient flips to Matched).
        }

        // Host pings the lobby so it isn't reaped while waiting for an opponent.
        private IEnumerator HeartbeatLoop()
        {
            var wait = new WaitForSecondsRealtime(HeartbeatInterval);
            while (!string.IsNullOrEmpty(_hostedLobbyId))
            {
                _ = LobbyService.Instance.SendHeartbeatPingAsync(_hostedLobbyId);
                yield return wait;
            }
        }

        // ---- connection callbacks -----------------------------------------

        private void OnClientConnected(ulong clientId)
        {
            if (clientId == _nm.LocalClientId) SetState(MatchmakingState.Matched);
        }

        private void OnHostSawClient(ulong clientId)
        {
            if (clientId != _nm.LocalClientId) SetState(MatchmakingState.Matched);
        }

        // ---- cancel / cleanup ---------------------------------------------

        private async Task CancelAsync()
        {
            await TearDownAsync();
            SetState(MatchmakingState.Cancelled);
            SetState(MatchmakingState.Idle);
        }

        private async Task TearDownAsync()
        {
            StopHeartbeat();
            Unsubscribe();

            if (!string.IsNullOrEmpty(_hostedLobbyId))
            {
                try { await LobbyService.Instance.DeleteLobbyAsync(_hostedLobbyId); }
                catch (Exception) { /* lobby may already be gone */ }
                _hostedLobbyId = null;
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

        // ---- helpers -------------------------------------------------------

        private void StopHeartbeat()
        {
            if (_heartbeat != null) { StopCoroutine(_heartbeat); _heartbeat = null; }
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

        private void OnDestroy()
        {
            StopHeartbeat();
            Unsubscribe();
        }
    }
}
