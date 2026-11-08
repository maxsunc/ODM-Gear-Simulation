# Forest rendering follow-up

User reported roughly **45 FPS** after the earlier cape fix and authorized further improvements. This follow-up is **local, uncommitted work on main** at capture; Git remains publication authority. No new live FPS improvement is claimed yet.

## Applied scope

- Three existing forest wall prefabs gain native renderer-only LOD groups: original near meshes unchanged, new simpler mid/far meshes from the approved source.
- Three existing tree prefabs use the same meshes/materials with earlier first/second transitions: 0.55/0.16 → **0.75/0.24**. Far-cull threshold unchanged.
- Original near FBXs, `.blend`, textures, materials, prefab/scene metas/GUIDs, both saved scenes, all physics/nav data, audio/user tuning, camera/player scripts and existing cape fix unchanged.
- No resolution, shadow, VSync, population, physics or animation downgrade. Town architecture is untouched.

## Evidence

- `authoring-report.json`, `forest-wall-lod-art.json`, native/Blender logs: owned Unity2021.3.25f1 / URP12.1.11 and Blender4.5, no Play/input/physics stepping/navigation bake/source scene saves during art/authoring.
- `forest-detail-before.json` / `forest-detail-after.json` and survey logs: same fixed 16:9 views, actual imported meshes, current LOD bias1.5. Estimated Forest geometry drops **5.40–6.00M → 2.25–2.62M triangles (56–58%)**. Walls alone **2.47–2.57M → 0.350–0.376M**. Material-submission proxies unchanged: not a claimed draw-call reduction. Town survey unchanged.
- `Before_*.png` / `After_*.png`: native Intel Iris Xe authoring renders for five matched views, 1280×720. `image-comparison.json`: **NearWall pixel-identical**; mid/far visual differences reviewed. Upward static camera ignores collision and may sit below terrain; resting Scout is unanimated. These are not gameplay screenshots, FPS measurements or traversal/collision checks.
- Physical graph verification: all **2,552 Forest / 617 Town colliders** match by canonical serialized properties, bindings, world matrices, layers and active state before/after loading modified prefabs. New distance models have no colliders. Original physical walls/trunks remain hookable independent of rendered LOD.
- `preservation-and-validation.json`: of **1,680 pre-existing protected Assets/Settings/original-art paths, 1,674 stay byte-identical**. Only the six intended visual prefab files changed. New output and preserved tuning/source hashes included.

## Existing checks: 3 passed / 2 failed — not full-suite success

`forest-distance-existing-regressions.xml/.log`: three passing existing full-map navigation/native7x10x spawn/player-only-respawn checks. Two failed old checks retained:

1. **Wall-height fixture** expects121.1m, gets211.924988m. `forest-distance-baseline-control.xml/.log` confirms **the same failure/value with the original prefabs**. Existing user scaling was preserved rather than reset.
2. **Tree/hook fixture** assumes every LODGroup is a tree with MeshCollider. New legitimate straight-wall LODGroup uses the original BoxColliders, so the fixture null-dereferences at line62. It passes in the original-prefab control. This is not reported as a passed hook test. No dummy collider, disabling hack or test/assertion changes were used. Physical invariance comes from the complete unchanged collider graph.

No new tests/assertion edits; earlier broad-suite failures unresolved. No full-project/build/WebGL/live FPS/GPU-counter/crowd/device certification. Geometry estimates omit occlusion, runtime Titans, terrain tessellation, shadow/additional passes and native submission batching. Static image review does not certify moving LOD transitions or subjective quality.

## Source and use

- `Assets/Art/Environment/GiantForest2/WallDistance/`: six new visual FBXs/metas.
- `Assets/Editor/ForestDistanceDetailBuilder.cs`: explicit guarded authoring, never auto-run.
- `tools/blender/build_forest_wall_lods.py`, `tools/build_forest_distance_detail.py`.
- Reusable survey `tools/profile_gameplay_performance.py`; `--keep-worker-assets --inventory-only` deliberately inventories unpublished owned outputs without replacing them.
- Full explanation/rollback: **`docs/forest-distance-performance.md`**.

Original prefab backups remain local in `Library/WorldLodOriginalPrefabs/`. Source publication copied only six prefab files and the new visual folder/metas. The user Editor was never stopped/saved/switched/reloaded or given synthetic input. Try fresh user Play to establish the next actual FPS and inspect transitions; there is no promised FPS target from a triangle-count estimate.
