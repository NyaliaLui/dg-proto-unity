# Blender → Mixamo Animation Merge Workflow

Reusable recipe for copying a Mixamo animation from a skinless source armature onto
a target character armature in Blender (via the Blender MCP server).

Assumes: Blender **5.1.x** (slotted Action system — the old `action.fcurves` API is
gone) and the standard **Mixamo skeleton** (`mixamorig:` bones).

## The recurring task
"A skinless Mixamo animation armature was added to the scene — copy its keyframes
into a named action on the target character."
- **Source** = skinless armature carrying one Mixamo action (named like `mixamo.com`,
  `mixamo.com.00N`, or `Armature|...|Layer0`).
- **Target** = the character armature (e.g. Paladin/XBot) with its skinned meshes.

## Proven recipe
1. `get_scene_info` → identify the source armature(s) and the target.
2. **Rest-pose alignment check (critical).** For each shared bone, compare
   `bone.matrix_local.to_quaternion()` via `rotation_difference().angle`. Fold the
   quaternion double-cover: `eff = min(deg, abs(360 - deg))`. Flag bones where
   `eff > 1.5°`.
3. Confirm every bone the action drives exists on the target.
4. **Pick the method:**
   - *Rest poses aligned* (0 significant mismatches) → **direct transfer**: reuse the
     source action on the target.
   - *Mismatched* → **retarget bake**: add Copy Rotation (WORLD/WORLD) to every target
     bone targeting the matching source bone, plus Copy Location (WORLD/WORLD) on
     `mixamorig:Hips`, then
     `bpy.ops.nla.bake(visual_keying=True, clear_constraints=True,
     only_selected=False, bake_types={'POSE'})`.
5. Make a clean action: rename `Target|AnimName`, set `use_fake_user=True`, remove
   empty slots, and set the data slot's `name_display` to the target object's name so
   its identifier becomes `OB<ObjectName>` (e.g. `OBPaladin`).
6. Delete the source armature object **and** its armature data.
7. Bind the target to the data slot and **verify a bone actually moves** (sample
   `rotation_quaternion` across frames) + take a viewport screenshot. Restore the
   target to its prior/Idle action.
8. Delete leftover orphan actions (`mixamo.com*`) left behind by imports.

## Hard-won gotchas
- **Empty-slot binding bug:** if an action's data slot identifier doesn't match the
  object name, Blender silently creates an empty `OB<Object>` slot and binds to *that*
  → the animation appears to do nothing. Fix: rename the data slot to match the object
  and strip empty slots. **Never trust a handle match alone — always confirm a bone moves.**
- **Rest-pose mismatch = mesh distortion:** a direct copy onto a differently-posed rest
  skeleton contorts the mesh (a Punch FBX with forearms ~155–172° off produced a
  twisted blob). Usual cause: the FBX was imported with a different "Automatic Bone
  Orientation" setting. Fix: retarget bake (rest-pose agnostic), or re-import with
  settings matching the target.
- **Quaternion double-cover:** identical orientations can report ~360°; always fold with
  `min(deg, 360 - deg)`.
- **Blender 5.1 API:** F-curves live in `action.layers[].strips[].channelbags[].fcurves`,
  each channelbag tied to a slot via `channelbag.slot_handle`. `Bone.select` is
  unavailable — bake with `only_selected=False` rather than selecting bones.
- **Name prefixes drift:** action names can gain an `Object|` prefix on save/reload —
  cosmetic only, bindings stay intact.
- **Mixamo root motion:** locomotion clips carry forward travel on the Hips location
  (Walk: local index 2 of `pose.bones["mixamorig:Hips"].location` traveled ~172 units →
  world −Y, ~1.7 u after the 0.01 armature scale). To walk in place, flatten that Hips
  translation channel (keep vertical bob/sway) or download the Mixamo "In Place" variant.

## Conventions
- Action naming: `Target|<Name>` (e.g. `Paladin|Walk`, `XBot|Idle`).
- `use_fake_user = True` so actions survive with no user.
- Exactly one slot per action, identifier `OB<ObjectName>`.
