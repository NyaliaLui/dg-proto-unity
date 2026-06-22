# Unity FBX Import Workflow — Root Transform Bake-Into-Pose

Unity-side companion to [`blender-mixamo-workflow.md`](blender-mixamo-workflow.md).
Covers the per-clip **Root Transform** settings that have to be set on every
animation clip imported into this project, and why.

## Why this matters here

This project's `Animator` has **`applyRootMotion = false`** and
`Assets/Scripts/PaladinController.cs` is the sole movement authority — it
writes `Rigidbody.linearVelocity.x` from input every Update and zeroes
velocity during attacks. Mixamo clips, however, carry baked **root
translation** (and sometimes rotation) on the hips. If that translation
isn't *baked into the pose* during import, two bad things happen:

1. The character's mesh visibly drifts away from its GameObject root
   during the swing (sliding "in place").
2. The next time the script writes a velocity, the visual feet snap back
   to the GameObject root.

The fix is to tick **Bake Into Pose** on the right axes so the limbs do
their motion *relative to a pinned root*, and the GameObject stays put
while the body still animates correctly. The script then has clean
authority over world-space position.

## Note for Claude (agent workflow)

**Always ask the user before setting Root Transform values on a new
animation clip.** The decision rule below is a starting hint, not a
substitute for confirmation — what the clip *visually* does in Blender
(spin, lift, lunge, in-place) determines which axes to bake, and only
the user knows the source motion. Surface the choice with
`AskUserQuestion`, listing the three Bake checkboxes (Rotation,
Position Y, Position XZ) so the user can pick which to enable. Do not
silently copy the settings from an existing clip; new clips are new
content.

## Decision rule (what to enable for a new clip)

| What the clip's source motion does | Enable "Bake Into Pose" on |
|---|---|
| Walks / runs forward (locomotion loop) | None — leave Off; script drives X velocity. Set **Loop Time = On**. |
| Standing attack with small weight-shift / step drift | **Position (XZ)** |
| Attack that lifts off the ground / jumps in place | **Position (XZ)** + **Position (Y)** |
| Attack that spins or changes facing | **Position (XZ)** + **Position (Y)** + **Rotation** |
| Real airborne jump (script handles Y via `AddForce`) | None — keep Position (Y) "Based Upon" = Original so the feet land at the authored ground level |

"Based Upon" defaults to use:

- **Position (Y) Based Upon = Original** — keeps the feet on the
  authored ground line.
- **Position (XZ) Based Upon = Center of Mass** — the Mixamo default.
- **Rotation Based Upon = Original** when Rotation Bake is On.

## Current state of `Assets/Paladin.fbx` (ground-truth snapshot)

Read directly from `ModelImporter.clipAnimations`. Restore by matching
these if a Blender re-export wipes them.

| Clip | Rotation Bake | Y Bake | XZ Bake | Y Based Upon | XZ Based Upon | Loop Time |
|---|---|---|---|---|---|---|
| Idle         | Off | Off | Off    | Original | Center of Mass | **On**  |
| Walk         | Off | Off | Off    | Original | Center of Mass | **On**  |
| Crouch       | Off | Off | Off    | Original | Center of Mass | **On**  |
| Jump         | Off | Off | Off    | Original | Center of Mass | Off     |
| Normal       | Off | Off | **On** | Original | Center of Mass | Off     |
| Backslash    | Off | Off | **On** | Original | Center of Mass | Off     |
| Kick         | Off | Off | **On** | Original | Center of Mass | Off     |
| Stab         | Off | Off | **On** | Original | Center of Mass | Off     |
| CrouchNormal | Off | Off | **On** | Original | Center of Mass | Off     |
| **Special**  | **On** | **On** | **On** | Original | Center of Mass | Off |

`Special` is the only clip with Rotation + Y baked because its source
motion in Blender includes a spin and a vertical translation; without
Rotation Bake the GameObject's yaw would drift, which collides with
`PaladinController.Flip()` writing yaw `±90°`.

## UI label ↔ `ModelImporterClipAnimation` field

When setting these from C# (see snippet below) the field names don't
match the Inspector labels one-to-one. Reference:

| Inspector label | Code field |
|---|---|
| Root Transform Rotation → Bake Into Pose | `lockRootRotation` |
| Root Transform Rotation → Based Upon (Original vs Body Orientation) | `keepOriginalOrientation` (`true` = Original) |
| Root Transform Position (Y) → Bake Into Pose | `lockRootHeightY` |
| Root Transform Position (Y) → Based Upon (Original / Center of Mass / Feet) | `keepOriginalPositionY` (`true` = Original; for Feet set `false` + `heightFromFeet`) |
| Root Transform Position (Y) → Offset | `heightFromFeet` |
| Root Transform Position (XZ) → Bake Into Pose | `lockRootPositionXZ` |
| Root Transform Position (XZ) → Based Upon | `keepOriginalPositionXZ` (`true` = Original; `false` = Center of Mass) |
| Loop Time / Loop Pose | `loopTime` / `loopPose` |
| Cycle Offset | `cycleOffset` |

## Programmatic restore (`execute_code` snippet)

When a Blender re-export blows the `.meta` clip settings away, this
snippet restores the Paladin table above. Paste into Unity MCP's
`execute_code` action.

```csharp
var imp = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Paladin.fbx");
var clips = imp.clipAnimations;

var baked = new System.Collections.Generic.HashSet<string> {
    "Normal", "Backslash", "Kick", "Stab", "CrouchNormal"
};
const string special = "Special";

for (int i = 0; i < clips.Length; i++)
{
    var ca = clips[i];
    int p = ca.name.LastIndexOf('|');
    string leaf = p >= 0 ? ca.name.Substring(p + 1) : ca.name;

    // Defaults shared by every clip in this project.
    ca.keepOriginalPositionY = true;   // Y Based Upon = Original
    ca.keepOriginalPositionXZ = false; // XZ Based Upon = Center of Mass
    ca.heightFromFeet = 0f;

    if (leaf == special)
    {
        ca.lockRootRotation = true;
        ca.keepOriginalOrientation = true;
        ca.lockRootHeightY = true;
        ca.lockRootPositionXZ = true;
    }
    else if (baked.Contains(leaf))
    {
        ca.lockRootRotation = false;
        ca.keepOriginalOrientation = false;
        ca.lockRootHeightY = false;
        ca.lockRootPositionXZ = true;
    }
    else // Idle / Walk / Crouch / Jump
    {
        ca.lockRootRotation = false;
        ca.keepOriginalOrientation = false;
        ca.lockRootHeightY = false;
        ca.lockRootPositionXZ = false;
        if (leaf == "Idle" || leaf == "Walk" || leaf == "Crouch")
            ca.loopTime = true;
    }
    clips[i] = ca;
}
imp.clipAnimations = clips;
imp.SaveAndReimport();
return "restored Root Transform settings on " + clips.Length + " clips";
```

## Re-import workflow after a Blender re-export

Pair with the note in `CLAUDE.md` that "FBX clip names get re-prefixed on
Blender re-export." Full recovery checklist:

1. Confirm `ModelImporter.animationType == Human`. If a re-export
   downgraded it to Generic, set it back to Human and reimport — Unity
   regenerates the `PaladinAvatar` sub-asset.
2. Inspect `imp.importedTakeInfos` vs `imp.clipAnimations[].takeName`.
   If they don't match, rewrite each `clipAnimations[]` entry's
   `takeName` to point at the actual take in the new FBX. Common
   patterns observed in this project: takes can be `Armature|<Leaf>`,
   `Paladin|Armature|<Leaf>`, or `Paladin|Paladin|<Leaf>`, and leaf
   names have been seen as `Normal_Attack`, `Normal`, etc.
3. Run the snippet above to re-apply per-clip Root Transform settings.
4. Re-link `Assets/Animations/Paladin.controller` motion states by
   trailing-segment match, with the alias map for renamed clips:
   `NormalAttack → Normal`, `SpecialAttack → Special`,
   `CrouchAttack → CrouchNormal`. Preserve state speeds (1.6 for
   locomotion, 1.8 for attacks).
5. Re-assign the new `PaladinAvatar` to the scene `Animator`.
6. Verify: `applyRootMotion = false` on the scene Animator, no console
   errors, Paladin animates and stays put during attacks.

## Cross-reference

- Blender-side workflow: [`blender-mixamo-workflow.md`](blender-mixamo-workflow.md).
- Project memory: `CLAUDE.md` (Models / animation section).
- The script that owns translation: `Assets/Scripts/PaladinController.cs`.
- Controller bound to scene Paladin: `Assets/Animations/Paladin.controller`.
