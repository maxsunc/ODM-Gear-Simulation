# Scout model-only overhaul — first review pass

## Current status

The user **accepted this first overhaul as the new model baseline** and requested publication with the remaining project work. Consult Git history on `main` for the published checkpoint; local/uncommitted wording in raw receipts records state at capture time. This is an original-model refinement toward the user's supplied Eren/anime reference, not an exact likeness or a claim of finished production character art. The supplied FBX was inspected privately; none of its mesh, textures, skeleton or promotional images were copied into the shipped model.

Authoring used an isolated **Blender 5.2.3 LTS** background worker, explicitly approved by this task. Unity remains **2021.3.25f1 / URP 12.1.11**. Blender 4.5 already supported sculpting/modeling/animation; the version change is not an explanation for the original model's roughness. This pass constructs and refines game-mesh forms with Blender Python, rather than claiming an automated high-resolution sculpt or mocap.

## What changed

- Rebuilt the head with a more defined jaw and a nose integrated into the face surface, rather than stacked spherical features.
- Added curved almond-shaped eyes, iris/pupil detail, eyelid contours, restrained mouth detail and smaller ears.
- Replaced the simple hair cap/fringe with layered, curved volumetric locks and a fitted scalp/nape surface. No alpha hair cards or extra outline pass.
- Reworked the undershirt, cropped open jacket, lapels, pockets, collar, sleeve transitions and cuffs.
- Refined trouser/boot silhouettes, thinner harness webbing, hollow buckle frames and selected seam details.
- Added a **body-only 2048² PBR atlas**: base color, metallic/smoothness and normal textures. Uses the existing URP/Lit shader, not a new renderer feature or global lighting change.
- Preserved original palm/finger and hip-gear surface positions and skin influences in the DCC construction; colors on body-contained hardware/hands use the new body atlas.

This is still a first aesthetic review pass. Face/hair styling, shoulder transitions, cloth detail and the look under actual map lighting should be reviewed by the user before calling the art final.

## Installation and preserved assets

Within the **model-only pass**, the only modified existing asset is **`Assets/Prefabs/Player/ScoutVisual.prefab`**, with exactly two reference substitutions in the **ScoutBody** renderer:

1. Its mesh now references `Assets/Art/Player/Scout/ModelOverhaul/ScoutBody_Refined.asset`.
2. Its material now references `ScoutBody_Refined.mat` in the same folder.

The shared gameplay Player contains a nested ScoutVisual instance; the visual change therefore propagates through that shared prefab to both gameplay maps. No scene or shared gameplay Player prefab was rewritten. Existing scene instances are not forcibly reloaded or saved.

**Unchanged:**

- Original `ScoutPlayer.fbx`, its importer/meta/GUID, original `ScoutPlayer.blend` and original PBR textures/material.
- All 43 rest bones, hierarchy, current prefab bone transforms, bind poses, bone arrays and Animator bindings.
- Original cape renderer/mesh/material/UVs, cape code, 273-particle simulation, 120 Hz substeps and eight solver iterations. No cape replacement or retuning.
- Actual sword/hilt meshes, grip origins, launcher markers and physical blade sweep references.
- All animation clips/controller, including protected Run and Held; no pose retiming, root motion or animation-overhaul work.
- Movement, camera, physics/contact bounds, grabs/releases, damage timing, corpse ownership and respawn logic.
- Forest LOD/cape optimizations, user tuning and concurrent Titan AI/prefab edits present when this work started.

The native mesh is authored directly into the **original renderer's binding space**, derived from four rest-bone landmarks and checked against all available original body bone origins and original geometry bounds. It reuses the original bind-pose array and remaps vertex influences to the existing bone array. The new FBX is an editable/exportable art deliverable, **not** a replacement Unity rig import.

## Cost and limits

| Item | Original | Refined |
| --- | ---: | ---: |
| Whole character source triangles | 11,520 | 26,388 |
| Body triangles | 10,084 | 24,952 |
| Rest bones | 43 | 43 |
| Body material slots | 1 | 1 |
| Body atlas | Original shared 1024² | New body-only 2048² |

Native body mesh: **15,887 corner/UV-split vertices**. No additional renderer objects, animation writers or cloth particles are introduced. The cape and weapons retain the original shared material; there are now two distinct character materials overall, not one. This is an art-quality increase, **not a performance optimization**. The triangle and texture increase needs real-device profiling before Android performance can be promised. No resolution, population, solver or animation downgrade was used to compensate.

The maximum original rest-landmark fit residual was **1.4294e-8 mesh-local units**, and original-bounds conversion residual **1.6596e-8**. These are binding-space checks, not a substitute for moving combat/contact certification.

## Review images

Under **`ArtSource/Player/Scout/ModelOverhaul/`**:

- `Compare_Front.png` — matched Unity before/after full body.
- `Compare_Face.png` — matched Unity face/hair close-up.
- `Overview.png` — front, three-quarter, back, Run, flight and sword pose samples.
- Individual native `Before_*.png` and `After_*.png`.
- Four supplementary `Blender_*.png` studio renders.

These are **static authored pose samples**, not gameplay, input, moving animation, crowd, collision or FPS captures. They display the original short skinned cape because the runtime cloth component is not running in this static studio. The approved longer simulated gameplay cape has not been replaced. Do not judge its live drape from these pictures.

## Existing regressions — retain the failures

Same 60 selected existing cases on the original-model control and refined model:

**55 passed / 4 failed / 1 skipped**. Case identities, results and failure messages match exactly; no newly failing cases were observed in this selection.

Unchanged failures:

1. `ScoutPlayerPlayModeTests.NarutoRunLoopsWithoutBoneSnapAndCadenceMatchesAuthoredFootTravel`: expected 2 ± .05, actual .987232029.
2. `ScoutPlayerPlayModeTests.NarutoRunPlantsTheStanceFootAndTrailsArmsBehindForwardTorso`: expected −.0652145371 ± .004, actual −.0750658065.
3. `ScoutHeldStrugglePlayModeTests.HeldLoopHasNoSeamSnapAt120HzAndDoesNotModifyTheModelRoot`: expected <2°, actual 3.93702865°.
4. `ScoutHeldStrugglePlayModeTests.HeldLoopMovesRealSkinAndRestrainedLimbsWithoutTranslatingOrScalingAnyBone`: expected 1.2 s, current 1.8 s.

Skipped: `ScoutCapeClothPlayModeTests.NativeGraphicsShowLongerSimulatedCapeWithOriginalScoutArt` — the existing explicitly opt-in graphics check.

Selection covers existing Scout model/animation/aiming/attack cases, Held cases, cape cases and selected real hook/hold/health/release/respawn integrations. No new test code or assertion edits. The later publication check, including the current Titan AI defaults and its existing patrol/chase cases, is recorded separately under `ArtSource/Validation/ScoutModelCheckpoint/`. This does not supersede older broad-suite or Forest failures, and does not certify full gameplay, Android, all clothing intersections or every grab/corpse configuration.

Evidence: `original-control-regressions.xml/.log`, `existing-regressions.xml/.log`, `regression-comparison.json`, `native-authoring-report.json`, `authoring-report.json` and `preservation-and-validation.json` in the review folder.

## Editable source and reproducible workflow

- `ArtSource/Player/Scout/ModelOverhaul/ScoutPlayer_Refined.blend` — packed images, original rig/cape/weapons plus refined body; Blender 5.2 source. No live user file was saved/replaced.
- `ScoutPlayer_Refined.fbx` alongside it — complete art export; not installed as a new Unity rig.
- `tools/blender/overhaul_scout_model.py` — guarded body-only authoring into a marker-owned background worker.
- `Assets/Editor/ScoutModelOverhaulBuilder.cs` — explicit batch-only native mesh/material authoring and static captures; no auto-run or live menu action.
- `tools/build_scout_model_overhaul.py --action prepare|art|native|test|publish` — closed-worker orchestration. Initial `native` requires a closed worker created by `tools/run_titan_validation.py`, supplied with `--native-worker`. Do not reuse-refresh that worker after authoring without first archiving/copying the model outputs.
- `tools/create_scout_model_review.py` — Pillow comparison sheets from the actual native images, not generated imagery.

Local state/backups: `Library/ScoutModelOverhaul/`. The source preservation baseline covers **1,745 existing Assets/ProjectSettings/Scout-art paths**: **1,744 unchanged**, only ScoutVisual's body mesh/material references changed. Publication checks both this baseline and the two-reference-only prefab diff, and refuses to overwrite edited published art. Generated project/IDE files and unrelated dirty work are outside this asset baseline and must be preserved separately.

Do **not** run the original full `ScoutPlayerBuilder` or original v1 Blender generator to update this pass: those target the initial assets and can regenerate/revert visuals or clips. This model-only path deliberately does not rebuild animations.

## Rollback and next review

To revert only this art change, restore the ScoutBody renderer's original mesh subasset from `Assets/Art/Player/Scout/ScoutPlayer.fbx` and original material `Assets/Art/Player/Scout/Scout_PBR.mat`. Prefer a selective prefab diff/revert; copying an entire old prefab later could discard subsequent user edits. Original files and GUIDs are retained. Unreferenced new art can remain during review.

After Unity finishes importing, use a **fresh user-controlled Play session** to inspect both map lighting, face/hair silhouette, shoulders/elbows/knees, cape clearance, aiming/grips, sword poses, grabs, corpse appearance and respawn. Do not force-stop Play, reload/save scenes or use synthetic input. Animation improvements remain a separate, later task after the model direction is accepted.
