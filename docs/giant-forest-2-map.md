# Giant Forest 2.0 — assembled playable-scene pass

The user approved the first model set and authorized building the map, with trees
much taller than Titans (about2–3x, varied). This supersedes the earlier models-only
boundary. **Unity2021.3.25f1 / URP12.1.11 preserved.** User subsequently requested
larger mixed rock sizes, walls2x taller, then a **local commit** of the forest work.
No push authorized. See `git log -1` for the resulting checkpoint.

## Open / review

- Scene: **`Assets/Scenes/GiantForest2.unity`** (same reserved GUID).
- [Native Unity map preview](../ArtSource/Environment/GiantForest2/MapReview/GiantForest2_MapPreview.png)
- [Real player camera](../ArtSource/Environment/GiantForest2/MapReview/Gameplay.png)
- [Entrance / scale](../ArtSource/Environment/GiantForest2/MapReview/RuntimeEntry.png)
- [Overview](../ArtSource/Environment/GiantForest2/MapReview/RuntimeOverview.png)
- [Gateway](../ArtSource/Environment/GiantForest2/MapReview/RuntimeGate.png)
- [Larger varied rocks](../ArtSource/Environment/GiantForest2/MapReview/RuntimeRocks.png)

The live Editor was already displaying GiantForest2 at publication. **No stop-Play,
scene reload, live scene save, synthetic input, or live physics simulation was sent.**
The tested worker's serialized scene/assets were copied to disk after saved-source
hash checks. Reopen the scene yourself when ready; an already-loaded scene can still
show the previous in-memory map. Preserve any later unsaved work as a separate copy
before accepting an external reload. No automatic scene switch is installed.

## Layout and scale

- Actual2048x2048m heightfield with gently rolling ground, shared loam/moss/trail
  textures, an open entry glade and winding central route.
- **208 giant trees, three silhouettes, individually varied target heights150–216m.**
  Native LOD0 world bounds measured **150.52–215.48m**. Against the largest saved
  Titan (6x ~72m), that is **2.09–2.99x**. Smaller3x/5x Titans are proportionally
  further dwarfed. Original editable art/studio proportions remain unchanged;
  map prefab instances provide the larger scale.
- Broad open lower trunks; most branches/leaf crowns remain high.92m nominal
  spacing with deterministic XY jitter, route/glade/actor clearances, varied yaw.
-1536m-square perimeter:254 straight24m bays, four corner piers and a48m gateway.
  **Walls now2x taller**: ~121.1m visible height (120m corner piers), same width,
  thickness and12m foundation plane. All visual AND collider heights scale together;
  gateway opening doubles vertically to ~78m, retaining26m width. This is not a
  blanket clearance promise for giant bodies under the curved arch.
-40 mixed-size rocks: **8 small (4–8m),16 medium (10–17m),16 large (22–32m target
  mesh heights)**. Actual saved bounds ~**4.28–30.15m**, with12% of mesh height
  embedded into the real ground. Private seeded variation, uniform per-rock scale,
  actual mesh collision/hookability retained. Enlarged rocks keep their old site
  where safe; otherwise bounded proposals avoid physical scenery/actors/each other,
  reserved route/glade/sites and walls. Native overlap query saturation rejects.
-9 fallen logs unchanged. Standalone ground-patch/corner prefabs are available;
  the continuous terrain is the real floor, not thousands of patch colliders.
- Six copied Titans retained:3x,5x,6x,6x,6x,6x; repositioned to reserved clear sites.
  Player starts near `(0,12,-550)` in the entry glade.

The old copied `Map Control` hierarchy and old `Cube` floor were removed ONLY in
GiantForest2. **Original FreeRoam remains byte-identical**, SHA256
`4409c07626f94874f4b2ea7908a4e14d0080677e6531f5aae7693288feda7bfa`.
Existing Scout/Titan FBXs, rigs, clips (including approved Run), shared prefabs,
combat, health, motors, gravity16/jump4/.1/.15, camera/input and source-map navigation
assets were not edited. Camera rig starting transforms follow the new spawn;
world-specific fog/sun/ambient/sky are saved only in the new scene. Existing shared
URP pipeline, quality, global Physics and source-map lighting assets are unchanged.

## Runtime-ready asset setup

- Imported owned environment folder: `Assets/Art/Environment/GiantForest2/`.
- Each tree has **three decreasing-geometry LODs**, one renderer/two material slots
  per level; LOD screen fractions `.55/.16/.025`, then cull. Alpha-clipped,
  double-sided foliage; shared instancing-enabled URP materials with vertex paint,
  albedo, tangent normal and roughness maps. No transparent blending for foliage.
- A separate static, CPU-readable **wood-only MeshCollider** per tree retains the
  real trunk/roots/branches (not leaf-card/bounds solid boxes). TerrainCollider is
  the actual heightfield. Wall collision uses core/foundation/walkway/parapet/pier
  proxies; gateway/log/rocks use static mesh collision.
- Hookable environment colliders are layer9, already included by BOTH copied ODM
  masks. No ordinary grapple/gas/sword controls were changed.
- Data / terrain layers: `Assets/Environment/GiantForest2/`.
- Added scene at END of BuildSettings, preserving existing scene order/indices.

## Map-specific navigation / spawning

`Assets/Navigation/Titans/GiantForest2/Titan_{3,5,6}x.asset` are independent bakes
against **2549 actual collider sources**, including the TerrainCollider and wood.
No renderer-bounds floor, inherited FreeRoam data, per-enemy or runtime bake.

| Scale | World radius / height | Agent type |
|---|---|---|
|3x|5.4 /36m|existing `-1372625422`|
|5x|9 /60m|existing `-334000983`|
|6x|10.8 /72m|new `1479372276`, `ODM Titan GiantForest2 6x`|

Only the new6x type was appended to project navigation settings; existing types
were not changed. All three data assets are owned by the new map. **FreeRoam still
has ONLY its original3x/5x data**; the global new type is not a6x FreeRoam bake.
Random spawning keeps its copied size weights/schedule/cap/effect settings, with a
new map-specific1400x300x1400 world volume centered `(0,100,0)` so proposals stay
inside the forest enclosure. Spawn still performs full real-rig/solid/player checks.
Existing combat sight/ranges/patrol tuning are preserved, not silently world-scaled.

## Validation and evidence

All execution in an owned full-project batch worker:
`C:/Users/maxsu/AppData/Local/Temp/ODM-GiantForest2-iwrxdlei`.

- **6/6 headless map cases pass**, graphics case intentionally skipped for the null
  device. Latest2026-11-05 **15:20:49–15:21:00UTC**,11.551s.
  - all208 LOD0 heights within2–3x largest Titan, distinct heights and decreasing LODs;
  - native ray hits on EVERY bole, correct physical floor/masks/no canopy boxes;
  -15 complete cross-forest paths across3x/5x/6x, solid-wall raycast obstruction;
  - all six saved actors ready, one correctly scaled agent each, preserved player
    tuning and tight settled floor pose with actual native jump-support query;
  - fresh3x/5x/6x spawns plus confirmed random warning→arrival; triangulation unchanged;
  - all259 wall/gate/pier roots exactly2x height, original foundation/width preserved;
    all40 rocks satisfy8/16/16 size tiers, native collider size and actual top-down hits.
- **1/1 native graphics case passes**,2026-11-05 **15:21:51–15:22:05UTC**,13.944s,
  Intel Iris Xe / DX11. Actual active Scout/Titan animations, map materials, normal
  scene pipeline and native frames; no replacement render pipeline or staged actors.
  Final player capture follows warmup/completed frames (initial async skin shader
  startup can render temporary red). Images are Camera.Render readbacks, so
  screen-space HUD is omitted. Only the whole-map overview temporarily disables fog;
  the saved playable scene retains its fog. No retouching, conceptual imagery or
  character replacement. Review sheet only labels/resizes these native images.
- Published **142 generated asset/data files + scene/meta byte-match** worker outputs;
  neither old FreeRoam navigation GUID appears in the new scene.
- Raw results/report and summary retained in `ArtSource/.../MapReview/` and owned
  worker logs. `Assets/Tests/GiantForest2` refuses non-owned/non-batch scene loading.

No complete full-speed ODM/combat traversal, crowds/corpse/terrain-carry stress,
WebGL player build or FPS/memory approval is claimed. The main Editor is currently
WebGL-targeted; worker validation/captures used desktop native Unity. Production
wind, impostors, baked occlusion/lightmaps/UV2 and foliage overdraw optimization are
not supplied. Shared50m URP shadow distance is unchanged. Static-collider/LOD/shared-
material setup is a first playable pass, not a performance certification. Prior
Titan suite222/224 (same two protected Run recipe failures) is historical evidence;
this art/map-only work did not alter runtime combat sources or rerun that full suite.

## Authoring / reproduction

`Assets/Editor/GiantForest2Builder.cs`: explicit scene-only authoring menu, never
InitializeOnLoad. Refuses Play, dirty/unsaved/other scenes. Menu:
**Tools > Environment > Giant Forest 2 > Build or rebuild this map**.
Rebuild replaces generated environment/layout; preserve manual changes first.
Before-build disk copy is retained under owned `Library/GiantForest2/`.

Owned workflow from project root (publication refuses saved-source changes):

```bash
python tools/build_giant_forest_map.py --action inspect
python tools/build_giant_forest_map.py --action prepare
python tools/build_giant_forest_map.py --action build
python tools/build_giant_forest_map.py --action test
python tools/build_giant_forest_map.py --action graphics
python tools/build_giant_forest_map.py --action publish
python tools/create_giant_forest_map_review.py
```

For authorized iterations on the CURRENT saved map, use `--action snapshot` to
copy/protect the saved scene, settings and generated assets in the owned worker,
then `--action resize`, `test`, `graphics`, `publish`. This scoped resize preserves
trees, terrain, logs, actor/camera poses and tuning instead of rebuilding the layout.
It rebakes all three affected navigation assets once in the Editor. Snapshot refuses
a locked/running owned worker; no live Editor scene state is requested or saved.

Prepare uses factory-background Blender4.5 and never changes the user's interactive
file or approved source blend. Shader template: `tools/templates/GiantForestLit.shader`.
Source-stage provenance: [first model review](giant-forest-2-art-review.md).
The user authorized committing the complete forest follow-up AFTER the rock/wall
iteration. Only owned forest assets/source/tests/helpers/docs and matching tracked
project references are included; IDE/Plastic/Python caches excluded. No push requested.
