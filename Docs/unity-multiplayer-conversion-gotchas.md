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

## Milestone 3 — synchronized countdown & gated start

- **Synchronize the countdown with an authoritative timestamp, not local timers.**
  The host sets `matchStartTime = NetworkManager.ServerTime.Time + N` into a
  `NetworkVariable<double>` (server-write, everyone-read); every client renders
  `Mathf.CeilToInt(matchStartTime - NetworkManager.ServerTime.Time)`. Because all
  clients read the same synced clock, their 3→2→1 displays agree within a frame
  and they unlock at the same real-world instant. Three independent
  `WaitForSeconds(1)` loops would drift and start players at different moments —
  the classic bug.
- **Use `NetworkManager.ServerTime.Time` as the shared clock**, never `Time.time`
  (unsynced per device). `ServerTime` is valid on clients once connected.
- **Gate input by spawning controllers DISABLED, then enabling at "GO".** Don't
  enable the owner's controller on spawn (`NetworkPlayerSetup` leaves it off);
  the countdown owner enables only **this client's own** player
  (`NetworkManager.LocalClient.PlayerObject`) when the timer hits zero. Remote
  proxies stay disabled regardless. This guarantees nobody moves or attacks
  before GO without touching the controller's internals.
- **Use a scene-placed NetworkObject for match-wide state.** `MatchController`
  sits in the gameplay scene and auto-spawns on the networked load (on host + all
  clients) — simpler than instantiating and registering a prefab. A sentinel
  value (`matchStartTime == 0`) cleanly means "countdown not begun yet".
- **Drive the countdown start after players exist.** `MatchSpawner` spawns the
  players first, then calls `MatchController.BeginCountdown()` (server-only), so
  the clock never starts before there's someone to gate.
- **Testing a short countdown through MCP/automation is latency-blind.** Tool
  round-trips (+ scene load) easily exceed a 3 s countdown, so polling always
  arrives after "GO". To actually observe/screenshot a mid-countdown frame,
  temporarily bump the duration (e.g. 60 s), capture, then revert. The
  disabled→enabled controller transition is the reliable proof the gate fired.

---

## Milestone 4 — host-authoritative world (enemies, health, score, melee)

- **Lazy-create singletons can't become `NetworkBehaviour`s.** `ScoreTracker`
  used to `AddComponent` itself onto a hidden GameObject on first access — but a
  `NetworkVariable` only works on a spawned `NetworkObject`. Convert such
  singletons to a **scene-placed `NetworkObject`** (auto-spawns on the networked
  load, like `MatchController`) and make `Instance` *find-only*. Give numeric
  state a sensible **constructor default** (`new NetworkVariable<int>(1, …)` for
  level) so the HUD reads a sane value before the spawn.
- **Initialise server-authoritative state in `OnNetworkSpawn`, not `Awake`.**
  `Health` seeds `currentHP = maxHP` only `if (IsServer)`, inside
  `OnNetworkSpawn`. Writing a `NetworkVariable` before spawn throws.
- **`OnValueChanged` does NOT fire for the initial synchronized value** on a
  freshly-spawned object. Fire your `Changed`-style event **manually once** in
  `OnNetworkSpawn` or the HUD/score miss their first update.
- **Guard "is dead" against the pre-spawn default.** `NetworkVariable<int>`
  starts at `0`, which a naive `IsDead => hp <= 0` reads as *dead before spawn*.
  Gate it on a `_spawned` flag set in `OnNetworkSpawn`.
- **Keep server authority out of scattered `IsServer` checks — gate by *where
  the script lives*.** The three enemy AI behaviours are **not on the prefab**;
  the host `AddComponent`s one *after* `NetworkObject.Spawn()`. So the AI only
  ever exists on the host, drives movement/animation there, and clients get pure
  replication. No per-method `IsServer` guards needed inside the AI.
- **Enemy prefab networking:** `NetworkObject` + `NetworkTransform`
  (server-authoritative default — the host owns enemies) + `NetworkAnimator` on
  the same GameObject as the `Animator`. When adding `NetworkAnimator` from code
  on a prefab-contents root, **wire its animator via the serialized field**
  (`SerializedObject.FindProperty("m_Animator")`), not just the public property.
- **NGO auto-registers new `NetworkObject` prefabs.** Saving a prefab with a
  fresh `NetworkObject` makes the prefab post-processor add it to
  `DefaultNetworkPrefabs.asset` for you — verify before adding it again. To add
  programmatically use `NetworkPrefabsList.Add(new NetworkPrefab{ Prefab = … })`
  (the backing `List` field is inaccessible; `PrefabList` is read-only).
- **Replicate animation triggers via `NetworkAnimator.SetTrigger`,** not
  `Animator.SetTrigger`. Float/bool params auto-sync, but **triggers don't** —
  the enemy punch/jump only animate on the host until routed through the
  `NetworkAnimator`.
- **Despawn, don't `Destroy`, a networked enemy** — and only on the server.
  The AI's death handler calls `NetworkObject.Despawn()` (safe because the AI is
  host-only). Calling `Destroy` on a spawned `NetworkObject` from a client is an
  error.
- **Server-authoritative melee WITHOUT rewriting the controller.**
  `PaladinController` stays a `MonoBehaviour`; a sibling **`PlayerMelee`
  `NetworkBehaviour`** exposes `RequestHit(...)` → `ServerRpc` → the host runs the
  `OverlapBox` and applies damage. Because only the **owner's** controller is
  enabled, exactly one request fires per swing from the right client; on the host
  the RPC short-circuits to a direct call. Co-op has no anti-cheat need, so we
  **trust the client's reported swing box** and just **skip Paladins in the
  overlap** so friendly-fire/self-hits can't happen.
- **Gate every host actor that targets players on "GO", not just input.**
  Enemies must not spawn or act during the countdown (players are input-locked),
  so `EnemySpawner` checks `MatchController.HasStarted`. General rule: anything
  host-authoritative that attacks/chases a player waits for match start.
- **Replace single-player `FindAnyObjectByType<Player>()` with a registry.** It
  assumed exactly one player; co-op has up to two spawned at runtime.
  `PlayerRegistry` (populated by `NetworkPlayerSetup` on spawn/despawn) lets
  enemies target the **nearest *living*** player and ignore downed teammates.
- **Runtime-spawned players break serialized HUD references.** `HealthBarUI`'s
  `target` was a scene reference to the old in-scene Paladin (now null). The
  local player's `NetworkPlayerSetup` rebinds it via `HealthBarUI.SetTarget` on
  spawn (owner only).
- **Individual death ≠ match end (co-op).** A single Paladin going down must NOT
  pop the game-over screen — that was single-player behaviour. Game-over moved
  out of `PaladinController.OnPlayerDied` (it just downs the player now);
  both-down → match end is Milestone 5.
- **Deferred from M4 (documented, not silently dropped):** obstacle layout is
  still per-client and un-seeded — grass-block X positions are deterministic so
  the divergence is minor (only rock-vs-platform choice differs, so a hopper can
  land slightly off on a client); rocks/droppables aren't networked yet (they
  break host-only); no teammate health bar. Fold these into M5/M6.

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
