# Forest distance-detail performance pass

Following the cape optimization, the user reported roughly **45 FPS** and authorized further fixes. This pass targets the Forest's rendering geometry, without changing player/Titan physics, camera behavior, population or lighting. It is local work on `main`; not committed/pushed at capture.

## Changes applied

Six existing shared art prefabs under `Assets/Art/Environment/GiantForest2/Prefabs/`:

- **Straight wall, corner wall and gateway:** added native `LODGroup`s referencing the original near renderer plus new mid/far visual meshes. Original near FBXs, materials, renderer transforms and prefab GUIDs stay intact.
- **Three trees:** kept every existing approved mesh/material and original far-cull threshold; changed only first/second LOD transitions from **0.55 / 0.16** to **0.75 / 0.24** screen-relative height. Detailed geometry is used a little less far away. Close trees retain the original full-detail mesh.

New wall distance FBXs/metas live under **`Assets/Art/Environment/GiantForest2/WallDistance/`**. They come non-destructively from the approved editable Blender source. Mid meshes omit tiny bevel geometry. Far meshes also omit small interior/side faces of individual masonry courses while retaining broad faces, UVs, vertex paint, structural cores, caps, pilasters and actual arch openings. They are visual-only and contain **no colliders**.

| Wall | Original near triangles | Mid | Far |
| --- | ---: | ---: | ---: |
| Straight | 19,316 | 5,268 | 2,148 |
| Corner | 38,540 | 10,540 | 4,300 |
| Gateway | 27,680 | 7,968 | 3,312 |

Wall transition heights: **0.60 / 0.18 / 0.00001**; the last tier effectively stays present through the existing far plane rather than culling away the enclosure. Actual distances depend on FOV, current LOD bias and the user's per-instance/nonuniform scales. This pass does **not** normalize wall scaling to an older fixture.

LODGroup switches renderers only. All original Box/Mesh/Terrain colliders remain enabled at their original poses and layers, independent of visual LOD. The original near-renderer hierarchy was not reparented. Neither hooks nor navigation use the new simplified visual meshes. `TitanNavigationGeometry` continues collecting PhysicsColliders, not render geometry.

## Measured workload proxy

Identical fixed 16:9 non-Play survey views, current Editor LOD bias **1.5**:

| View | Before mesh triangle estimate | After | Reduction |
| --- | ---: | ---: | ---: |
| Forest spawn/downward view | 5,402,864 | 2,248,446 | 58.4% |
| Forest canopy/upward view | 6,001,762 | 2,621,680 | 56.3% |

Wall portion drops from **2.47–2.57 million** to **0.350–0.376 million**. Material-submission proxies are unchanged (611 / 642), so this is a geometry/vertex workload reduction, **not a claimed draw-call reduction**. Shiganshina survey numbers are unchanged; its architecture was not modified. The previous cape fix already applies in both maps.

These are native asset/frustum/LOD calculations, not actual GPU counters or gameplay FPS. They omit terrain tessellation, runtime-spawned actors, shadows, occlusion and extra passes. Camera staging uses saved transforms and ignores the collision resolver; the upward static camera can sit below the heightfield. The resting Scout is unanimated. The authoring pictures/estimates must not be presented as a live traversal, camera-collision test or FPS prediction.

## Native visual review

A closed graphics worker on Intel Iris Xe rendered five matched before/after views at 1280×720: Spawn, Canopy, NearWall, MidWall and Gate. Original and optimized **NearWall images are pixel-identical**. Mid/far images preserve the wall/gateway silhouette and readable appearance; reduced tree detail is visible in distant crowns. Static comparisons do not certify transitions while moving, night/all lighting configurations or subjective gameplay readability.

Original source `.blend`, original exported FBXs, textures, shaders/materials, saved scene files/metas, camera/controller, shared gameplay prefabs, audio tuning, navigation assets, physics/quality settings and the existing cape fix stayed byte-identical to the fresh baseline. The builder compared **all 2,552 forest colliders and 617 town colliders**, including canonical serialized properties, object bindings, world matrices, layers and active state, before/after loading the updated prefabs. Both physical digests matched. No map was saved or navigation baked.

## Existing regression results — not a green full suite

Five selected existing full-map checks: **3 passed / 2 failed**. Passed:

- All five exact-clearance forest navigation routes and walls.
- Real 7x/10x spawns and matching agents, without rebake.
- Player-only death/respawn preserving world actors, navigation and music.

Failures are retained unedited:

1. **`WallsAreDoubleHeightAndRocksIncludeSmallMediumAndLargeHookableSizes`** expects 121.1 m, sees **211.924988 m**. An original-prefab control run fails at the **same value** before this change. The user's larger existing wall scale is preserved, not reset to satisfy an old fixture.
2. **`PhysicalHeightfieldWallsAndEveryTrunkCanBeHookedWithoutLeafSolidBoxes`** loops over **every LODGroup** and assumes it is a tree with a MeshCollider. The new straight-wall LODGroup has the existing BoxColliders, not a MeshCollider, so the test dereferences null at line 62. The original-prefab control passes this case. This is an exposed test assumption about the previously tree-only LODGroup set; no dummy collider was added and no assertions/tests were rewritten to conceal it. Hook/physical invariance is established here by the unchanged complete collider graph, not by claiming this test passed.

No new tests/assertion edits. Earlier broader-suite failures remain unresolved. No full-project, standalone/WebGL, transition, input, sound, crowd or device/performance certification is claimed. After this pass, the user reported approximately **100 FPS** in Play, then a movement-related drop to ~45 FPS. A later actual CPU capture implicated **Inspector repaint overhead**, and the user confirmed the Inspector workaround resolved that issue. These are user observations plus an Editor CPU capture, not a controlled standalone/GPU performance certification. See `docs/gameplay-performance.md` for the follow-up. Local/uncommitted wording in original receipts records state at capture; Git history is the publication authority.

## Tools, publication and rollback

- Guarded Editor authoring: **`Assets/Editor/ForestDistanceDetailBuilder.cs`**; no auto-run or live menu action.
- Blender 4.5 background source: **`tools/blender/build_forest_wall_lods.py`**.
- Orchestration: **`tools/build_forest_distance_detail.py --action art|build|publish`**.
- Survey: **`tools/profile_gameplay_performance.py --label NAME --inventory-only`**. While inspecting unpublished worker assets, add **`--keep-worker-assets`** explicitly; otherwise the survey refreshes from source.

The worker is marker-owned and closed between actions. Publication copies only six prefab files and the new wall-distance folder/metas, never a source scene or original `.unity.meta`. Original prefabs are backed up locally in `Library/WorldLodOriginalPrefabs/`. Evidence is under **`ArtSource/Performance/ForestDistanceDetail/`**. User Editor Play, selection, scene saves and reloads were never commandeered.

If the LOD tradeoff feels too visible, the tree's first/second thresholds can return to 0.55/0.16 without touching mesh or collision assets. Wall thresholds can be adjusted independently. No resolution downscale, VSync change, blur, forced frame cap, Titan count reduction, offscreen contact/animation disabling or global physics downgrade was used.
