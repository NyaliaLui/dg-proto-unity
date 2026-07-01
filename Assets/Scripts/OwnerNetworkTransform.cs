using Unity.Netcode.Components;
using UnityEngine;

namespace DgProto
{
    /// <summary>
    /// Owner-authoritative <see cref="NetworkTransform"/>: the client that owns
    /// this object is the authority over its transform, rather than the server.
    ///
    /// We use this for the player Paladin because the game is co-op (no
    /// player-vs-player fairness to defend), so letting each player simulate
    /// their own movement locally and replicate it keeps controls responsive
    /// without prediction/rollback machinery. The host stays authoritative over
    /// the shared world (enemies, damage, score) — just not over a teammate's
    /// own avatar.
    /// </summary>
    [DisallowMultipleComponent]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
