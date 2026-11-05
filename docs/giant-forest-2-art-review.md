# Giant Forest 2.0 — first model review (historical source-art stage)

**Update:** the user approved these models and authorized map assembly with taller,
varied trees. The built scene and native Unity captures are documented in
**[Giant Forest 2 map](giant-forest-2-map.md)**. Source meshes/studio dimensions below
are the ORIGINAL review proportions; map instances are enlarged to ~150–216m.
Earlier statements below about not importing/building describe that prior stage,
not the current delivered map.

User originally requested a fresh forest map on a copy of the current scene, but
**models first for judgement/iteration BEFORE map assembly**. That review stage stopped there.
This source-art work follows pushed checkpoint **3e9a738d** on titan-combat-updates.
User later authorized a LOCAL forest commit after map assembly and the larger-rock /
doubled-wall iteration. No new push requested; see current map contract / git log.

## Review here first

**[Six-view review sheet](../ArtSource/Environment/GiantForest2/Review/GiantForest2_ReviewSheet.png)**

Full-size views under `ArtSource/Environment/GiantForest2/Review/`:
- `Trees_Lineup.png`: three tree silhouettes, grey60m HEIGHT guide and gold3.6m PLAYER
  guide. These are ABSTRACT SCALE MANNEQUINS, not replacement Scout/Titan artwork.
- `Roots_Closeup.png`: actual fluted trunk/root geometry and bark texture.
- `Canopy_Closeup.png`: curved woody branches and transparent leaf-spray cards.
- `Wall_Modules.png`: straight bay, L-corner, real open-arch gateway.
- `Stone_Closeup.png`: bevelled limestone/stonework and base weathering.
- `Ground_Props.png`: ground-patch palette, snapped trunk, boulders.

All are genuine Blender Cycles renders of the delivered mesh/material assets, NOT
concept images or Unity/normal gameplay footage. Neutral studio/fill lighting is for
judging shape/detail; it does NOT prescribe map lighting/fog. Raw images preserved as
`*_Raw.png`; labels/overview only add captions and resize for the sheet, no retouching.

## Delivered source / 13 individual exports

Open `ArtSource/Environment/GiantForest2/GiantForest2_ReviewStudios.blend` in Blender
(4.5 source) for the camera views. Default view is tree lineup. Scene dropdown has
`GF2_View_*` for six studios and `GF2_Source_*` for each real editable source asset.
All maps packed. Source-only `GiantForest2_AssetReview.blend` has the editable source
scenes plus a gallery. Review instances do not change the source pivots/transforms.
Original live Blender scene (Cube/Camera/Light) left unchanged; all work executed in
owned factory-startup BACKGROUND workers, not over the user's open file.

Individual `.fbx` files in `ArtSource/Environment/GiantForest2/Exports/`; textures in
`Textures/`. **Not installed into Unity Assets or placed in a gameplay scene yet.**
Do not look for the new trees in the copied scene at this review stage.

| Model | Proposed scale / purpose | Evaluated export triangles |
|---|---|---:|
| Tree A Sentinel |120m bole class, ~121.92m overall incl. crown/root bounds; heavy straight trunk |66,886|
| Tree B Leaning |104m bole class, ~106.44m overall; slimmer, bent bole |66,886|
| Tree C Old Fork |114m class, ~114.24m overall; heavy fork/dead snag |67,012|
| Straight wall |24m repeat bay, ~60.55m including parapet, ~8m core |19,316|
| Corner wall |24m L centerline grid, same height |38,540|
| Gateway |48m module, actual26m wide x39m high arch crown clearance |27,680|
| Loam patch |32x32m, closed underside, ~.25m relief above boundary |5,182|
| Moss-bank patch |32x32m, closed underside, ~.9m maximum relief |5,182|
| Worn soil/trail patch |32x32m, closed underside, subtle central worn coloration |5,182|
| Snapped trunk |32m nominal section + jagged fibres, ~6m diameter |3,500|
| Boulder A/B/C |three angular silhouettes ~4.14/5.06/5.98m overall height |320 each|

These are review/detail meshes, NOT final forest population budgets. Trees have high
canopies and long clear lower trunks; roots/branches are intentional wooded forms,
not green sphere stand-ins. Foliage uses a generated4-spray1K RGBA atlas, double-sided
cards (not individually simulated leaves). Bark/stone/soil/endgrain have generated2K
basecolor/tangent-normal/roughness maps. Soil texture noise now tiles without the
initial visible square seams. First-pass vertex-painted moss/damp tones, not final
scanned materials. Root/branch solids interpenetrate naturally; NOT boolean-unioned
watertight solids or collision shapes. Endgrain overlay discs/leaf cards intentionally
open, unlike closed source solid components. Boulders intentionally faceted at this
stage. No tree sway/cloth/leaf simulation, LODs/impostors/lightmap UV2/colliders yet.

Each export is **ONE mesh with material submeshes**, not350+ separate Unity branch
renderers. Editable sources retain individual branches/blocks for changes. FBX albedo
texture maps explicitly bound (Blender vertex-paint Multiply node is not serialized by
FBX); UVs/paint/normals/maps retained. Later Unity URP material hookup should multiply
vertex COLOR for the same moss effect, apply correct color spaces/normal import,
alpha clipping/mips/double-sided leaf normals, and shared materials. No URP upgrade.

## Scene copy / protected work

Last saved active setup: `Assets/Scenes/FreeRoam.unity`. Created byte-identical
`Assets/Scenes/GiantForest2.unity`, with a NEW UNIQUE `.meta` GUID, without opening,
loading, saving or switching Unity scenes. Source/clone SHA256 at creation:
`4409c07626f94874f4b2ea7908a4e14d0080677e6531f5aae7693288feda7bfa`.

The copy STILL CONTAINS THE OLD MAP/player/Titans/spawner/runtime/nav references.
This is a working-scene reservation, not a new playable forest. No clearing/deletion/
placement, camera/fog/lighting/physics settings changes, nav rebake or BuildSettings
entry. Existing FreeRoam/shared prefabs/approved Titan+Scout/animations/combat untouched.
No live user Play stop/input/reload, no unsafe preview Physics automation.

**Future after art approval:** import to a new owned Assets/Art environment folder,
wire URP materials/prefabs, collision/LOD/UV2/instancing/performance, THEN build terrain/
forest/wall layout on GiantForest2. Editor installers currently scoped to older scene
paths need explicit new-scene opt-in. New geometry requires its OWN per-world/per-size
NavMesh assets/bake (do not overwrite FreeRoam3x/5x data). Existing copied nav only
matches the old copied geometry; it does not support the proposed new forest. Existing
6x baked-support limitation still applies until explicitly baked; no runtime bake.

## Checks performed (art/exports, not gameplay)

`asset-report.json` contains all13 bounds, source/evaluated triangle counts, one-mesh
export/material counts and units. `validation-report.json` verifies:
- all source UV/vertex paint finite and valid, all solid COMPONENTS closed;
- intentionally open leaf cards/endgrain overlays excluded from that closed test;
- all source texture images packed; no armatures/animation/physics introduced;
- **13/13 FBXs re-imported** in an owned Blender worker, one mesh each, UV/paint retained,
  albedo image refs present, metre-scale bounds roundtrip error <.002m (max ~.0000191m);
- no collider/navigation/running Unity approval implied by this.

Initial internal renders exposed Blender4.5 CustomData-pointer relocation corruption:
allocating a color layer after retaining a UV-layer handle could overwrite paint with
UV values. Fixed by reacquiring BOTH handles after allocation; checks now reject
corrupt arrays. Also cleaned up obvious wall-cap coplanar z-fighting with single-piece
L cap/gateway cap and inset core end faces, curved branch sweeps and ground texture
seams. These are internal first-pass corrections, not user-approved aesthetic choices.

Full Titan gameplay suite not rerun for an art-only/source scene-copy change; no C# or
existing gameplay content changed. Prior222/224 (same2 Run assertions) remains last
runtime evidence, NOT a claim for a newly built map. No full forest FPS or traversal
claim. Await user opinion on trunk/root proportions, canopy shape/density, stone vs
concrete wall style and ground palette before population/layout.

## Reproduction (owned workers only)

From project root in Git Bash, with Blender4.5 installed:

```bash
export GF2_PROJECT_ROOT="$(pwd -W)"
BLENDER='C:/Program Files/Blender Foundation/Blender 4.5/blender.exe'
"$BLENDER" --background --factory-startup --python tools/blender/build_giant_forest_assets.py
"$BLENDER" --background --factory-startup --python tools/blender/validate_giant_forest_assets.py
"$BLENDER" --background --factory-startup --python tools/blender/render_giant_forest_assets.py
python tools/create_giant_forest_review_sheet.py
```

Renderer supports `GF2_SHOT=Wall_Modules` (or other six view keys) for a single view.
Build refuses a loaded blend and interactive Blender. Renderer/validator load ONLY
owned source path. Blender source files have no external project dependencies other
than optionally unpacking bundled maps. Source scripts require Blender bundled NumPy;
caption helper requires Pillow. Native auto `.blend1` backups from this owned pass
moved to `Library/GiantForest2ReviewBackups`, not shipped as duplicate project sources.
Do not blanket-delete or stage IDE/Plastic/Python caches. No automatic commits/push.
