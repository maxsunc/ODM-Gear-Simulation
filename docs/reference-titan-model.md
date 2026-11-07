# Reference-led Titan art pass (v3)

The active Titan now follows the **leftmost dark-haired, stocky Titan** in the
user-provided `C:/Users/maxsu/Downloads/refImage.jpeg`. This is a reference-inspired
interpretation, not an exact replica. The existing rig proportions are retained.

## Visual changes

- One continuous closed torso surface with a broad belly and shallow chest relief.
  Muscles are no longer assembled from visibly separate bulging primitives.
- Rounded rectangular arm/leg/hand/foot sections, matching the supplied blocky style.
- A continuous sculpted face with inset eyes, a narrower grin, smaller teeth,
  a wider head, and a shorter exposed neck.
- One closed dark hair cap, broad swept bangs and subtle strand detail instead of
  dozens of thick disconnected hair tubes.
- Warm mottled skin with low-strength pore detail and baked 2K PBR textures.

The shoulder/hip branches remain genuinely separate capped skins for dismemberment;
small articulation seams at elbows, wrists, knees and ankles are intentional.
There are no individually animated fingers or facial blendshapes.

## Assets and compatibility

- Blender source: `ArtSource/Titan/Reference/TitanReference.blend`
- Export: `ArtSource/Titan/Reference/TitanReference.fbx`
- Source maps: `ArtSource/Titan/Reference/Textures/TitanReference_*.png`
- Blender preview: `ArtSource/Titan/Reference/TitanReferencePreview.png`
- Actual Unity idle-pose capture: `ArtSource/Titan/Reference/TitanReference_Unity.png`
- Geometry manifest: `ArtSource/Titan/Reference/TitanReference_Manifest.json`
- Active Unity FBX: `Assets/Art/Titans/Prototype/TitanPrototype.fbx`
- Active prefab: `Assets/Prefabs/Titans/ModularTitanPrototype.prefab`
- Test lab: `Assets/Scenes/TitanPrototypeSandbox.unity`

The canonical Unity atlas filenames remain `TitanDetailed_*.png` in
`Assets/Art/Titans/Prototype/Textures/`, but now contain the reference pass's maps.
This preserves existing asset GUIDs. The prefab, generated meshes and FBX also
retain their asset paths/GUIDs. Source skins use `TitanReference_<part>`; the
Unity builder normalizes these to `Titan_<part>`.

The model has **15 closed skin sections, 22 rest bones identical to the original,
and 78,888 source triangles** (down from 165,304 in the previous anatomical pass).
Unity may omit degenerate triangles during FBX import.

The runtime body/combat code was not changed. Authoring now detects the reference
model, uses box colliders/hit zones for its broader limbs and torso, and adjusts
head/eye/nape/mouth positions for the shorter exposed neck. The 17-body/16-joint
ragdoll structure, four detachable branches and 14 damage triggers are preserved.
The original capsule-based authoring remains available for the earlier models.

## Backups

The previous anatomical pass, active Unity art folder and prefab were copied into
`ArtSource/Titan/DetailedBackupBeforeReference/` before replacement. The earlier
blockout remains under `ArtSource/Titan/BlockoutBackup/` and its original source
files. Do not import the backup Unity folders into Assets: they contain the same
GUIDs as the active assets.

Blender collection `TitanReference_v3` is current; `TitanDetailed_v2` and
`TitanPrototype_v1` are retained and hidden in the working Blender session.

## Rebuild

Run scripts inside Blender with the original rest skeleton collection loaded.
The following variables are Python globals supplied to the scripts:

1. `tools/blender/build_reference_titan.py`:
   - `TITAN_PROJECT_ROOT`: absolute project root.
2. `tools/blender/bake_detailed_titan.py` (shared configurable baking pipeline):
   - `TITAN_COLLECTION = 'TitanReference_v3'`
   - `TITAN_ASSET_STEM = 'TitanReference'`
   - `TITAN_OUTPUT_DIR`: absolute `ArtSource/Titan/Reference` path.
   - `TITAN_FINGER_DESCRIPTION = 'Reference-style block mittens, hand weighted'`
3. Copy the exported FBX over the active FBX **without replacing its .meta**.
   Copy each source PNG over the corresponding canonical `TitanDetailed_*.png`.
4. Exit Play Mode, refresh Unity, then run
   `Tools → Titans → Build Modular Prototype Assets` and
   `Tools → Titans → Validate Modular Prototype`.
5. Run `python tools/run_titan_validation.py --wait`.

The reference builder reuses pure geometry/material helpers from
`build_detailed_titan.py` by parsing their function definitions; it does not execute
that script's anatomical model build. It does not clear unrelated Blender objects.

## Validation

**6 passed / 0 failed**, Unity 2021.3.25f1, **2026-11-02 01:34:58 UTC**:

- Animated/kinematic body configuration
- Source/generated geometry and UV identity
- Eye-blindness recovery and surviving-hand pose
- All four branches detach their own skins and ignore repeated hits
- Nape kill switches to a stable ragdoll
- Severed legs retain animated pose and baked skin positions through Animator rebind

Five-second isolated ragdoll metrics: maximum speed **11.37 m/s**, final maximum
joint-anchor gap **0.004 m**, hips height **0.80 m**.
The actual Unity idle-pose screenshot was visually checked as well.
Tests run in the disposable validation project, never in an Editor preview scene.

A later [patrol/sight/chase phase](titan-patrol-chase.md) is now implemented in a
separate AI lab. This art pass and the body prefab remain independent of that layer.
Grab, stomp, eating and actual player-combat integration remain unimplemented.
