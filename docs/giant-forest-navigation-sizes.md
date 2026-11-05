# GiantForest2: 7x / 10x navigation and safe size fallback

Unity **2021.3.25f1 / URP12.1.11**, current SAVED user scene. User requested baking
7x/10x and the quick fix after discovering an unsupported size stopped all spawns.
No full map rebuild, geometry edit, actor relocation, audio retuning or live Editor
control. Original 3x/5x/6x navigation assets remain byte-identical.

## Supported sizes

| Scale | World clearance radius | Height | Agent ID |
|---|---:|---:|---:|
|3x|5.4m|36m|-1372625422 (unchanged)|
|5x|9m|60m|-334000983 (unchanged)|
|6x|10.8m|72m|1479372276 (unchanged)|
|7x|12.6m|84m|-1923039037 (new)|
|10x|18m|120m|-902729914 (new)|

New assets: `Assets/Navigation/Titans/GiantForest2/Titan_{7,10}x.asset`.
Negative IDs are valid Unity agent IDs; only -1 means missing.

The first coarse10x bake (radius/4 voxels) disconnected an eastern route. New
append-only profiles use **radius/8** voxels instead:7x1.575m,10x2.25m. All **25/25**
full route checks now pass. Radius/height, slopes, climb limits, scenery collision and
strict complete-path checks were NOT reduced or bypassed to make a route pass.
Collected2549 actual scene geometry sources. Existing data/types were not rebaked.

Native owned PlayMode navigation suite **3/3 pass**,2026-11-05 20:47:37–20:47:46UTC:
- Five exact profiles, five complete forest routes EACH, no crossing solid walls.
- Real full-rig-clear 7x/10x births through the actual manager; ready native agents
  use their matching type, local radius1.8/height12 (not scale-squared dimensions).
- CURRENT saved spawner settings successfully warn/activate a real random spawn.
  Triangle count stays unchanged: no runtime bake.

## Quick fix / future sizes

`TitanRandomSpawner` now **skips positive-weight sizes with no matching baked
profile**, renormalizing selection over supported sizes only. It does NOT substitute
a smaller agent's data, disable collisions or spawn an unvalidated size. Other valid
sizes keep spawning. The runtime Inspector shows **SizeWarning** listing skipped
sizes. If ALL enabled sizes are unsupported, it still returns an actionable error
instead of attempting an unsafe spawn. Invalid NaN/negative/zero scale or negative
weight configuration remains rejected; zero weight still disables a valid entry.

Two new spawner cases pass: mixed1x/7x/10x with huge unsupported weights still produces
an actual1x arrival; supported3x/5x retain1:3 relative weights while7x is skipped.
Missing positive-weight sizes still join the next requested bake list. Existing
spawn cap, clearance, cancellation, private RNG, warning timing and SFX stay intact.
Full current-asset regression: **257pass/7existing-fail/1graphics-skip**,265 cases.
A separate pre-change-policy run against the SAME current user assets was
**255pass/7fail/1skip**,263 cases: no new failing tests. Six failure messages match
exactly; face bounds vary2.205–2.263 with the fixture's per-instance idle pose but
still fail the same user-enlarged-trigger assertion. Seven existing
fixture conflicts: protected Scout Run2; user's enlarged face bounds/12s stun/8–16
reference speeds3; user's one-scream palette vs two-clip fixture expectations2.
No user asset or old assertion was reset/relaxed to hide these.

## Safe workflow / publication

`tools/bake_giant_forest_sizes.py --action {snapshot,bake,test,publish}` uses only a
marker-owned disposable full-project worker. Snapshot takes CURRENT saved assets,
including user's scene edits, size weights, caps, bounds, quieter audio and palette
choices. Reusing an owned worker replaces only its disposable Assets, not the live
project. New metadata/source/bake equality and fresh-result guards precede publication.

Publication appends only the new7x/10x data and agent settings, plus replaces ONLY
the navigation component in the original scene bytes. All other scene bytes and all
older settings/data are verified unchanged; source changes during the bake abort it.
Exact pre-publication scene/NavMeshAreas backup is recorded under
`Library/GiantForest2NavigationBackup/`. Existing BuildSettings, map assets/materials,
FreeRoam, prefabs, audio palettes, physics and quality settings are not published.
Evidence: `ArtSource/Environment/GiantForest2/NavigationReview/`.

For later sizes, explicit Editor menu:
**Tools > Environment > Giant Forest 2 > Bake missing requested navigation sizes**.
Open the saved scene outside Play; this generates only missing data and marks the
scene dirty for YOUR save. Do not use **Build or rebuild this map** merely for adding
sizes. Data is Editor-baked once per category, never per enemy.

After this externally published bake, reload the saved GiantForest2 scene/start a
fresh Play session when ready so the live scene uses the new profiles. No automated
stop/reload/save/input was performed in the live Editor. Full50-Titan crowd/FPS,
10x grab/bite combat feel and WebGL certification are outside this navigation test.
New navigation/spawner work is a local incremental commit; no new push requested.
