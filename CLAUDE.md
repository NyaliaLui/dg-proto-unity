# CLAUDE.md — dg-proto-unity

Project memory for the **Paladin** prototype (a Dragon Groove comic tie-in): a 2.5D
sidescroller built in Unity. This file is auto-loaded each session — read it first.

## Stack

- **Unity** 6000.4.6f1, **URP**, **new Input System** (`activeInputHandler: 1`).
- Driven via the **Unity MCP** server (tools: `execute_code`, `manage_*`, `read_console`,
  `refresh_unity`, `manage_editor` for play/stop, `manage_camera` for screenshots, etc.).
- Main scene: `Assets/Scenes/SampleScene.unity` (registered in Build Settings — needed for
  the game-over Restart's `SceneManager.LoadScene`).

## Working with this project (Unity MCP workflow)

- After editing `.cs` files, call `refresh_unity` (force + compile) then `read_console`
  (errors) — expect **0 errors** before continuing.
- `execute_code` runs C# in the Editor. It uses the **codedom** compiler: no `using`
  directives, no local functions, fully-qualified type names (e.g. `UnityEngine.Object`),
  and some APIs are blocked without `safety_checks=false` (e.g. `AssetDatabase.DeleteAsset`).
- **Screenshots** via `manage_camera` are camera-rendered and **exclude Screen Space –
  Overlay** canvases (HUD, mobile controls, popups). Verify those numerically instead.
- Scene mutations made during **Play mode revert on stop** — only do persistent scene
  setup while stopped. Asset edits (controllers, prefabs, materials) persist.
- Runtime-created Materials/Sprites/Meshes must be **saved as assets** before referencing
  them from prefabs/scenes, or the reference serializes as missing (magenta material bug).

## Models / animation

- `Assets/Paladin.fbx` — player. Clips (current take prefix is
  `Paladin|Paladin|Paladin|Paladin|Paladin|`, normalized to `Paladin|Paladin|<Leaf>`
  in the importer's clipAnimations):
  Idle, Walk, Jump, Crouch, Normal, Backslash, Kick, Special, CrouchNormal,
  Thrust (replaced Stab). Controller: `Assets/Animations/Paladin.controller`
  (attack-state speeds are tuned per-state so each animation's wall-clock
  duration matches its accompanying SFX:
  NormalAttack 1.88, Backslash 1.95, SpecialAttack 2.51, CrouchAttack 1.95
  (NormalAttack/Backslash/SpecialAttack now finish before AttackSwing's
  700ms cap ends; CrouchAttack still matches it), Kick 2.40 (→ Kick.wav
  500ms), Thrust 1.16 (→ Stab.wav 1020ms wall-clock 515ms; SfxId.Stab
  kept so the Stab.wav binding stays intact); locomotion at 1.6).
- `Assets/XBot.fbx` — enemy. Clips: `XBot|XBot|Idle/Walk/Jump/Punch`.
  Controller: `Assets/Animations/Enemy.controller` (Idle/Walk/Punch/Jump).
- FBX clip names get re-prefixed on Blender re-export; resolve clips by the trailing
  segment after the last `|`, and re-link controller states after a reimport.
- See [`Docs/unity-fbx-import-workflow.md`](Docs/unity-fbx-import-workflow.md) for the
  Root Transform **Bake Into Pose** settings each Paladin clip needs (attacks bake XZ;
  `Special` bakes Rotation + Y + XZ). Includes a paste-ready `execute_code` snippet to
  restore them after a re-export wipes the `.meta`.

## Code map (`Assets/Scripts/`, namespace `DgProto`)

- **`PaladinController`** — A/D move, Space jump, Ctrl crouch. Melee combos:
  Q = NormalAttack→Backslash→Kick, E = SpecialAttack→Thrust, Ctrl+Q = crouch attack.
  0.2s post-animation combo window (`comboWindowDuration`) — press during a
  swing is ignored, press within the window after an animation chains to the
  next step, miss the window and the combo restarts. Hit is frame-synced at
  `hitNormalizedTime` 0.45; normal = 2 dmg, special = 3 dmg; special hits
  stun 0.3s. Q step 3 plays Kick.wav; E step 2 plays Stab.wav; all other
  combo moves play AttackSwing.
- **`Health`** (implements `IDamageable`) — Paladin 20 HP, enemies 6 HP; `Changed`/`Died` events.
- **`IDamageable`**, **`IStunnable`** — damage + stun interfaces (melee resolves via these).
- Enemy behaviors (all `IStunnable`): **`EnemyController`** (patrol→chase→attack),
  **`ChaserEnemy`** (chase + attack), **`PlatformHopperEnemy`** (hop onto nearest
  rock/platform, attack 3×, move to next).
- **`EnemySpawner`** — every 2s spawns the behavior-less `Assets/Prefabs/Enemy.prefab`
  near the Paladin, **max 3 alive**, and `AddComponent`s one of the 3 behaviors at random.
- **`ObstacleSpawner`** — random Rock / BrownPlatform on every 5th ground GrassBlock.
- UI: **`HealthBarUI`** (top-left red bar), **`NotificationWindow`** (droppable reward
  popup), **`GameOverScreen`** (on death — Facebook link + Restart→reload), **`MobileControlsVisibility`**
  (touch D-pad + action buttons, hidden on non-touch).
- World: **`SidescrollerCameraFollow`**, **`ParallaxLayer`** (MidTrees/BackgroundTrees/Clouds),
  **`Rock`** + **`Droppable`** (tan triangular-prism reward).
## Conventions (Unity 6 C# style guide — encapsulation pass complete)

- Allman braces, never omitted.
- **Serialized fields:** `[SerializeField] private`, **bare camelCase** names (preserve
  names when refactoring so Inspector/prefab values still bind).
- Non-serialized private fields: `_camelCase`. Locals/params: camelCase.
- PascalCase for public members / methods / consts / enums. `I`-prefixed interfaces.
  Verb-prefixed booleans (`isDead`, `IsStunned`). Explicit access modifiers everywhere.
- No public field access across scripts — communicate via methods/properties/events/interfaces.
- Replace magic numbers with named constants (e.g. `FacingRightYaw`/`FacingLeftYaw`).

## Deferred / next steps

- Architectural DRY: a shared `EnemyBase` (FaceDir / stun / death / animator helpers for
  the 3 enemy scripts + Paladin) and a shared procedural-UI builder for
  `NotificationWindow` + `GameOverScreen` (currently near-duplicate).

## Multiplayer (networking) work

Converting this single-player game to 2-player online co-op (NGO + UGS Lobby/Relay).
Progress: **M1** Netcode foundation + networked Paladin · **M2** Find Match menu + UGS
Lobby/Relay matchmaking + networked load · **M3** synchronized 3-2-1 countdown + gated
start · **M4** host-authoritative world (server-authoritative `Health` & `ScoreTracker`
via `NetworkVariable`; networked `Enemy.prefab` + host-only `EnemySpawner`/AI; server-side
melee via `PlayerMelee` ServerRpc; nearest-living-player targeting via `PlayerRegistry`;
enemies gated on `MatchController.HasStarted`). Next: **M5** match end (both Paladins down)
+ restart · **M6** disconnect grace + polish.
Before doing further multiplayer work — or repeating the conversion elsewhere — read
[`Docs/unity-multiplayer-conversion-gotchas.md`](Docs/unity-multiplayer-conversion-gotchas.md)
for the hard-won gotchas. **Two inputs are human-only — prompt the user (don't assume):**
(1) the **UGS cloud project link** (sign-in + create/link project + enable Relay/Lobby/Auth;
the agent can't), and (2) the **match design decisions** (mode / authority / match-end /
disconnect) that drive the architecture.

## Blender / Mixamo animation work

When merging Mixamo animations onto a character in Blender (via the Blender MCP
server), follow the workflow and gotchas in
[`Docs/blender-mixamo-workflow.md`](Docs/blender-mixamo-workflow.md). Key rule: after
binding an action, always verify a bone actually moves — a handle match alone does not
mean the animation plays (empty-slot binding bug).
