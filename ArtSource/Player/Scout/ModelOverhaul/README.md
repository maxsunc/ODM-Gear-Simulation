# Scout model-only overhaul — review assets

The user accepted this first art pass as the new model baseline and requested publication. Git history on `main` is the publication authority; local/uncommitted statements in raw receipts describe capture-time state. See **`docs/scout-model-overhaul.md`** for current installation, costs, editable workflow, rollback and limits.

## Start here

- **Compare_Front.png**: old/new, matched Unity 2021/URP studio setup.
- **Compare_Face.png**: old/new head close-up.
- **Overview.png**: refined model with static samples of the existing Run, flight and sword poses.
- Individual `Before_*.png` / `After_*.png` are native Unity captures; `Blender_*.png` are supplementary studio renders.

The model now has a more defined face, layered hair, cropped jacket/lapels/pockets/collar, tailored limbs/boots and more restrained harness details. This is still a first aesthetic review, not an exact Eren likeness or finished production-art certification. No supplied reference mesh, textures, skeleton or promotional artwork were redistributed here.

## What did not change

Original cape/atlas and runtime cloth settings, all 43 rest bones and bindings, actual weapon/grip geometry, all existing animation clips/controller, motion ownership, physics/contact bounds, combat timing and gameplay settings. Original source FBX/blend and GUIDs remain intact. Only ScoutVisual's body mesh and material references changed.

**Static cape caveat:** these pictures show the original short skinned cape, not the approved longer runtime-simulated cape. The simulator is not running in the static studio. No cape replacement was installed.

## Data and evidence

- 26,388 whole-character source triangles versus 11,520 before; body 24,952 triangles / 15,887 native corner vertices.
- New body-only 2048² portable PBR atlas; one body material slot, no new renderers or outline passes. Cape/weapons keep the original material. Real-device cost has not been measured.
- `ScoutPlayer_Refined.blend`: Blender 5.2 editable collection with packed textures.
- `ScoutPlayer_Refined.fbx`: full art export for editing, not a replacement Unity rig import.
- `authoring-report.json` / `native-authoring-report.json`: DCC preservation hash, binding-space fit and mesh statistics.
- `preservation-and-validation.json`: 1,745 existing paths audited, 1,744 unchanged; only ScoutVisual modified with two reference substitutions.
- Existing selected regressions: **55 passed / 4 failed / 1 skipped**, exactly matching the original-model control. `regression-comparison.json` records all four retained failure messages. No new tests/assertion changes.

No full-suite, moving gameplay/collision, live cloth, input, performance, Android or subjective visual acceptance is claimed. Both maps inherit the shared visual, but actual map-lighting/traversal review is still needed. No live Editor/Blender scene was saved, switched or driven by synthetic input.
