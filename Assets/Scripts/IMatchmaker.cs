using System;

namespace DgProto
{
    /// <summary>
    /// Lifecycle of a matchmaking attempt, surfaced to the UI.
    /// </summary>
    public enum MatchmakingState
    {
        Idle,       // not searching
        Searching,  // in the queue / trying to find or host a match
        Matched,    // connected with an opponent; the match is about to load
        Failed,     // something went wrong
        Cancelled   // the player backed out
    }

    /// <summary>
    /// Abstraction over "find me an opponent and get us connected on the same
    /// NetworkManager session." It hides HOW that happens so the menu and match
    /// lifecycle don't care:
    ///   - <see cref="LocalMatchmaker"/> connects two clients over local/direct
    ///     transport (try-join-else-host) — the testable stand-in used now.
    ///   - A future UgsMatchmaker will do the same contract via Lobby quick-join
    ///     + Relay, so swapping it in touches nothing but the wiring.
    ///
    /// "Matched" means this client is connected (as host or joiner) with an
    /// opponent present; the host then drives the networked scene load.
    /// </summary>
    public interface IMatchmaker
    {
        MatchmakingState State { get; }

        /// <summary>Raised whenever <see cref="State"/> changes.</summary>
        event Action<MatchmakingState> StateChanged;

        /// <summary>Enter the queue: find an open match to join, else host one.</summary>
        void FindMatch();

        /// <summary>Leave the queue / tear down a half-formed connection.</summary>
        void Cancel();
    }
}
