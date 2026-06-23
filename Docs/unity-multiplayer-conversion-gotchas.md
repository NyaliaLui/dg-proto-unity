# Unity Multiplayer Conversion — Gotchas & Required Inputs

Hard-won lessons from converting this single-player mobile prototype into a
**2-player online co-op** game (Milestone 1: Netcode for GameObjects foundation;
Milestone 2: Find Match menu + UGS Lobby/Relay matchmaking). Read this before
doing further multiplayer work here, or before repeating the conversion on
another single-player Unity project.

---

## For Claude: prompt the user for these (the agent cannot do them)

Two inputs are **blocking and human-only**. Stop and ask (via `AskUserQuestion`)
rather than guessing or barrelling ahead:

1. **UGS cloud project link.** Real cross-device matchmaking needs the project
   linked to a Unity Gaming Services cloud project — which requires the user's
   Unity account. Prompt them to: sign into their account/org in the editor and
   **create/link a UGS cloud project**, and **enable Relay + Lobby + Anonymous
   Authentication** in the dashboard (linking alone isn't enough; the services
   must be on). The agent cannot sign in or toggle dashboard services. **Verify**
   the link by reading `ProjectSettings/ProjectSettings.asset` → `cloudProjectId`
   is non-empty *before* writing any cloud code. Until then, build/test against
   the local-connect stand-in.

2. **Match design decisions.** These drive the whole architecture, so confirm
   them up front — don't assume: **mode** (co-op vs PvP), **authority/topology**
   (client-host vs dedicated server), **match-end rule**, **downed-player
   handling**, and **disconnect handling**. Example of why it matters: co-op has
   no player-vs-player fairness to defend, which lets each player be
   *owner-authoritative* over their own movement and skips prediction/rollback
   entirely — a huge scope cut a PvP duel wouldn't allow.

---

## Milestone 1 — Netcode for GameObjects foundation

- **Install NGO via the Package Manager API**, not by guessing a version string:
  `Client.Add("com.unity.netcode.gameobjects")` resolves the latest Unity
  6-compatible version (we got **2.13.0**, which pulls transport 2.6.0). The
  resolve is async and triggers a domain reload; the `manifest.json` write lags,
  so **verify on disk**, not just immediately in-editor.
- **`NetworkTransform` / `NetworkAnimator` live in the `Unity.Netcode.Runtime`
  assembly** (namespace `Unity.Netcode.Components`), not a separate one. There is
  **no shipped `ClientNetworkTransform`** — for owner-authoritative movement,
  subclass `NetworkTransform` and override `OnIsServerAuthoritative() => false`
  (see `OwnerNetworkTransform`).
- **Don't rewrite the player controller into a `NetworkBehaviour` wholesale.**
  Gate it from the outside: the owner keeps `PaladinController` enabled and claims
  the camera; the **remote proxy disables the controller AND sets
  `Rigidbody.isKinematic = true`** so the local physics step doesn't fight the
  replicated transform (see `NetworkPlayerSetup`).
- **Scene object → player prefab:** the Paladin was a Model-prefab instance of the
  FBX with added components. `PrefabUtility.SaveAsPrefabAsset` on the configured
  instance produces a prefab (variant) with a valid `NetworkObject`
  `GlobalObjectIdHash`.
- **`NetworkAnimator` must sit on the same GameObject as the `Animator`** (the
  root here).
- **Camera:** in online play each client follows its **own** local player — no
  split-screen. Set the follow target for the owner, ideally *after* the gameplay
  scene loads (see M2 spawn-after-load).
- **Testing needs a second client:** **Multiplayer Play Mode**
  (`com.unity.multiplayer.playmode`) is a **separate package** — it is *not* the
  "Multiplayer Center" (that's onboarding/guidance only) and isn't installed by
  default.
- **Gitignore MPPM's per-developer scratch:** `Assets/UserChoices.choices` (+meta)
  and `ProjectSettings/VirtualProjectsConfig.json` — machine-specific, don't commit.

---

## Milestone 2 — matchmaking & menu

- **Put matchmaking behind an interface (`IMatchmaker`).** The local stand-in and
  the cloud implementation then swap with **zero changes** to the menu or match
  lifecycle. This single seam made the UGS swap a one-component change — the
  highest-leverage decision in the whole conversion.
- **"Quick-join or create" maps to local try-join-else-host:** connect as a client
  to `127.0.0.1`; if nobody answers within a short timeout, become the host and
  wait. That mirrors UGS Lobby's quick-join-or-create exactly, so the contract is
  identical when you swap in the cloud version.
- **Do NOT use connect-time auto-spawn for players.** With `NetworkConfig.PlayerPrefab`
  set, NGO spawns the player the instant a client connects — which happens while
  everyone is still on the **menu** scene (the Paladin falls through empty space
  and the camera can't retarget). Instead set `PlayerPrefab = null` and **spawn
  manually after the gameplay scene's networked load completes**
  (`NetworkManager.SceneManager.OnLoadEventCompleted`). Keep the prefab registered
  in the NetworkPrefabs list so `SpawnAsPlayerObject` works (see `MatchSpawner`).
- **One persistent `NetworkManager`** lives on a bootstrap object in the **menu**
  scene (NGO marks it DontDestroyOnLoad) and must **not** also exist in the
  gameplay scene — two NetworkManagers is a hard error. Move it to the menu.
- **Load the match with `NetworkManager.SceneManager.LoadScene(..., Single)`** (the
  *networked* load — brings host + all clients together), not
  `UnityEngine.SceneManagement.SceneManager.LoadScene`.
- **EventSystem + new Input System:** the project uses the Input System package, so
  the menu's EventSystem needs **`InputSystemUIInputModule`**, not the legacy
  `StandaloneInputModule`, or buttons won't respond.
- **Non-networked systems diverge across clients.** `EnemySpawner` uses un-seeded
  `Random` + `Time.time`, so two clients would see different enemies — we
  temporarily disabled it for M2 testing; it must become host-authoritative (M4).
  General rule: any RNG- or time-driven system diverges until the host owns it.
- **`EditorBuildSettings.scenes` edits need `AssetDatabase.SaveAssets()`** to flush
  to disk (git won't see the change otherwise). Order: MainMenu index 0, gameplay
  scene index 1.

---

## UGS / cloud

- **Windows `EPERM: operation not permitted, rename` in `Library/PackageCache`** on
  a package install is a **transient file lock** (antivirus or the search indexer
  grabbing freshly-extracted files). **Just retry** — it worked on the second
  attempt. If it persists, exclude the project's `Library` folder from Windows
  Defender (it's fully regenerable). OneDrive is a red herring *unless* the project
  actually sits under the OneDrive folder (this one doesn't).
- **Unity 6 Services UI is gated behind sign-in + the services packages.** Project
  Settings → Services looks empty until you're signed in **and** a UGS package is
  installed. The "cloud icon" people look for is the **account/cloud button in the
  top-right of the editor toolbar**, not in Project Settings. Installing the UGS
  packages surfaces the "link project" prompt.
- **`RelayServerData` construction is version-sensitive — verify the ctor by
  reflection before writing it.** For relay **1.2.0**:
  `new RelayServerData(allocation, "dtls")` and
  `new RelayServerData(joinAllocation, "dtls")` (namespace
  `Unity.Networking.Transport.Relay`), then
  `UnityTransport.SetRelayServerData(data)`.
- **`RelayService.Instance.CreateAllocationAsync(maxConnections)` — `maxConnections`
  EXCLUDES the host.** For a 2-player match, pass **1**.
- **Lobby heartbeat:** the host must call `LobbyService.Instance.SendHeartbeatPingAsync(lobbyId)`
  about every **15 s** or the lobby is reaped and joiners can't find it.
- **Pass the Relay join code through lobby Data** (`DataObject` with `Member`
  visibility); the joiner reads `lobby.Data[key].Value` after QuickJoin.
- **Smoke-test the cloud path with a single client** before needing two devices:
  init → anonymous sign-in → CreateAllocation + CreateLobby + StartHost, then
  `Cancel` to delete the lobby/allocation. If a service isn't enabled, the error
  names it.

---

## Versions captured (this project)

Unity **6000.4.6f1** · NGO **2.13.0** · transport **2.6.0** · Multiplayer Play
Mode **2.0.2** · services.core **1.18.0** · authentication **3.7.1** · lobby
**1.3.0** · relay **1.2.0**.

## Cross-reference

- Blender → Unity import workflow: [`blender-mixamo-workflow.md`](blender-mixamo-workflow.md),
  [`unity-fbx-import-workflow.md`](unity-fbx-import-workflow.md).
- Project memory / code map: `CLAUDE.md`.
