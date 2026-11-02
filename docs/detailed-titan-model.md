# Detailed Titan — anatomical art pass

This documents the earlier anatomical pass. The active `ModularTitanPrototype.prefab`
now uses the [reference-led model](reference-titan-model.md) instead. The anatomical
source and pre-replacement Unity art/prefab remain backed up outside Assets.

## What changed

- Tapered torso with pectorals, clavicles, serratus, obliques, shoulder/back masses,
  quadriceps, hamstrings, calf and wrist tendons.
- Sculpted face with brow ridges, eyelids, recessed eyes/irises, cheek/jaw planes,
  nose/nostrils, ear rims, actual mouth opening and individually rounded teeth.
- Layered tapered hair geometry, palm/finger creases, knuckles, fingers/toes and nails.
- UV atlas and portable **2048 × 2048** base-color, tangent-normal and
  metallic/smoothness maps. The smoothness map uses alpha; metallic RGB is black.
- Subtle skin mottling and pore normal detail baked from procedural source materials.

This is a **more detailed stylized art pass**, not a photorealistic scan or final
production-retopologized character. Final silhouette/art-direction review is still
useful before spending time on finalized animations.

## Compatibility

- **22 bones**: all rest matrices copied exactly from the original rig, including
  bone names, lengths and rolls.
- **15 closed skinned mesh sections**; all were checked for zero open boundary edges.
- **165,304 triangles** in the exported source model.
- Same shoulder/hip cut boundaries, hand/mouth sockets, face/nape hit regions,
  17 ragdoll bodies and 16 joints.
- Existing Generic idle/walk controller regenerated for the imported hierarchy.
- Existing ragdoll, damage, temporary blindness and severed-leg orientation logic
  are reused: **no runtime combat/body code was changed for this model replacement**.
- Fingers have more geometry but remain weighted to the existing hand bone;
  individual finger animation, facial blendshapes and hair physics are not added.

The higher triangle count is intentional for this art pass. For many simultaneously
visible Titans, particularly in WebGL, add lower-detail LODs, simplify distant hair
and merge atlas-compatible material slots before treating it as production-ready.

## Files

- Source: `ArtSource/Titan/Detailed/TitanDetailed.blend`
- FBX export: `ArtSource/Titan/Detailed/TitanDetailed.fbx`
- Source maps: `ArtSource/Titan/Detailed/Textures/`
- Full-body preview: `ArtSource/Titan/Detailed/TitanDetailedPreview.png`
- Face preview: `ArtSource/Titan/Detailed/TitanDetailedFace.png`
- Actual Unity lab-camera capture: `ArtSource/Titan/Detailed/TitanDetailed_Unity.png`
- Geometry manifest: `ArtSource/Titan/Detailed/TitanDetailed_Manifest.json`
- Active Unity model: `Assets/Art/Titans/Prototype/TitanPrototype.fbx`
- Active Unity maps: `Assets/Art/Titans/Prototype/Textures/`
- Prefab: `Assets/Prefabs/Titans/ModularTitanPrototype.prefab`
- Lab: `Assets/Scenes/TitanPrototypeSandbox.unity`

The original blockout and its Unity generated asset folder/prefab were copied to
`ArtSource/Titan/BlockoutBackup/` before replacement. Keep that backup outside Assets:
it contains original Unity GUIDs and should not be imported as a second asset set.
The original Blender collection is also retained, hidden, beside `TitanDetailed_v2`.

## Authoring pipeline

1. Open the original source / existing Blender scene so `TitanPrototype_v1` is available.
2. Execute `tools/blender/build_detailed_titan.py` in Blender with
   `TITAN_OUTPUT_DIR` set to the absolute detailed source directory.
3. Execute `tools/blender/bake_detailed_titan.py` with the same output directory.
   It packs the skins into one atlas, bakes maps, and writes the FBX and .blend.
4. Copy the detailed FBX over the active Unity FBX **without replacing its .meta**;
   copy the three PNG maps into the active Unity texture folder.
5. Exit Play Mode and run `Tools → Titans → Build Modular Prototype Assets`.
6. Run `Tools → Titans → Validate Modular Prototype`, then use the disposable
   runtime validation runner described below.

The Unity authoring tool normalizes detailed skin/material names, imports maps
with the correct color space/normal-map type and binds the atlas to URP materials.
It explicitly copies new vertex, index and UV buffers into the generated mesh
assets while retaining their GUIDs. `EditorUtility.CopySerialized` alone was not
sufficient for replacing the blockout with different topology; checks now catch
stale generated mesh/UV buffers.

Rebuilding updates generated art assets, not hand-authored gameplay scenes. If the
sandbox is currently loaded, its scene contents are left untouched; its prefab
instance receives the updated prefab. Save independent art edits in a prefab
variant rather than the generated prefab if you intend to rebuild it later.

## Validation

```text
python tools/run_titan_validation.py --wait
```

Tests run in a disposable Unity 2021.3 project, not the live working project. The
suite includes animation, eye-stun recovery, four severable limbs, nape/ragdoll
physics, preserved leg orientation and baked skin positions at the cut, plus a new
source-to-generated geometry/UV comparison. The last test prevents a detailed FBX
from silently rendering with old blockout mesh buffers.

Latest result: **6 passed / 0 failed**, Unity 2021.3.25f1, 2026-11-01 23:43:56 UTC.
The five-second isolated ragdoll simulation reported a maximum speed of 16.85 m/s,
a final maximum joint-anchor gap of 0.024m and hips height of 1.11m.

Navigation, grabbing, stomping and eating remain the next gameplay phase; this
replacement does not imply those unfinished behaviors have been implemented.
