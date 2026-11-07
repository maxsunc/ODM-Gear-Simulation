# Shiganshina district — original Blender architecture and playable Unity assembly

## Enter the map

On a **fresh Play session**, open `Assets/Scenes/Menu.unity`, then:
**Free Roam → Shiganshina → Deploy to Shiganshina**.
Giant Forest remains the default alternative. Options, music and hold-Tab stats
work through the same existing frontend/services. No fragile scene-index routing.

New scene: **`Assets/Scenes/ShiganshinaDistrict.unity`**
GUID: `b522ce25248da5d4c954d9a46fa87db5`.
It is appended to Build Settings; existing scene ordering/enabled flags are retained.
The previous `FreeRoam (Shiganshina.unity` is a separate legacy scene and unchanged.

This is an original **Shiganshina-inspired first playable district**, not a claimed
shot-for-shot/canonical street-plan reconstruction, ripped asset pack, or completed
AAA environment. Its recognizable composition is the rounded projecting district,
huge straight rear wall, fortified gates, red gabled half-timbered town, canal,
merchant quarter, civic square, bell/clock tower and windmills.

## Scale and layout

- **2048×2048m physical ground footprint**, matching the current GiantForest2
  terrain footprint. The curved inner playable enclosure is about **1400×1388m**,
  versus the forest's roughly1536×1536m square enclosure: similar axis dimensions,
  deliberately different outline, not identical enclosed area.
- **303 residential/shop/guild/warehouse buildings**, plus civic landmarks.
- **160 wall bays**,120m high, including the rear great-wall extensions; two
  fortified closed gates. These are actual meshes, not distant backdrop planes.
- Three **48m-wide canal bridges** connect the two halves of the town.
-100m main gate avenue,64m cross streets/boulevards for larger Titans;36m residential
  lots give narrower human-scale alleys and closely spaced rooftop transitions.
-95m bell/clock tower, civic hall, two windmills, fountain, merchant stalls and
  wooden overhead gantries provide distinct navigation/ODM landmarks.
- New player spawn is `(0,2,-585)`, looking north into the district. The actual
  existing player, gear, swords, camera helpers, external hook lines/particles,
  audio, health and player-only respawn setup are copied from current saved forest.
  No replacement player/motor/camera or root-motion controller was introduced.

## Blender model library

Editable packed native source:
`ArtSource/Environment/Shiganshina/Shiganshina_Architecture.blend`.
It contains the original archetypes and a separate linked street-composition studio.
All texture images are packed. The studio ground/camera/lights are NOT game exports.

**17 archetypes**, each exported as3 visual detail levels and a separate structural
collision FBX (**68 FBXs total**):

| Models | Purpose |
|---|---|
| House A Gable / B Tall / C Shop / D Wide / E Cottage / F Guild | Varied roof heights, shuttered windows, timber framing, brick chimneys; shop awnings and guild balcony |
| Warehouse | Larger waterfront lots and roof platforms |
| CivicHall | Northern plaza centerpiece with tower/porch |
| Belltower | Open bell chamber, spire, clock faces, high landmark |
| Windmill | Masonry tower and static modeled sail rig |
| Wall Bay32×120 | Modular masonry, coping, walkway and parapets |
| Gate Fortress | Fortified towers, real arch/spandrels, iron-bound closed gate, joining wings |
| Canal Bridge | Sloped closed deck, stone rails and supports |
| Rooftop Gantry | Physical timber crossbeam/perch; installed beside quays |
| Market Stall / Plaza Fountain / Crate | Street dressing library (crate supplied as reusable prefab) |

Portable1024² base-color/normal texture pairs for9 shared materials: plaster,
timber, terracotta tile, stone, brick, window recess, iron, cloth and pavers.
Four shared plaster tint variants break up rows. Unity uses native URP12 Lit;
no original texture/importer/shader changes and no new global render/physics setting.

Near roofs have actual rounded tile courses as well as tile textures. Distance
levels omit those courses and simplify facade details; far houses are380–872tri.
Detailed houses are4748–10964tri; warehouses11284tri. Structural house collision
is80–92tri, independent of renderer LOD. See `asset-report.json` for every count.
The importer logs separate-file `_LOD1/_LOD2` naming warnings; explicit prefab
LODGroups provide the actual three-level setup. These are not compilation failures.

## Native physical surfaces and navigation

- All architecture collision is static and on **environment layer9**, accepted by
  the existing ODM mask512 and unchanged **400m hook range**. No fabricated hook
  nodes, homing attachments, invisible anchor volumes or player parenting.
- Separate Blender collision meshes include closed walls and actual roof slopes,
  chimneys and supported balconies—not a roof-sized axis-aligned box. Small window,
  tile/cloth details are render-only; hooks hit the real underlying structure.
- Houses are closed exteriors, not enterable interiors. Gates are intentionally
  closed; no gate-opening animation/destruction in this pass. Windmills are static.
- The canal is an actual gap in the physical ground, with a stone bed8m below
  street level, quay walls, bridges and solid escape stairs. Water is a visual
  surface, not a hidden walkable bridge or a new swimming system.
- Separately shaped physical district/outskirts meshes prevent a square floor from
  accidentally authorizing outside-wall population. Outskirts, river bed and closed
  building/wall tops are marked Not Walkable for the **map-local bake**. Their real
  collision remains available to the player; buildings still obstruct Titan paths.
- Exact pre-baked **3×/5×/6×** data is stored under
  `Assets/Navigation/Titans/Shiganshina/`, using existing unchanged agent types:
  -3×: radius5.4 / height36 / ID`-1372625422`
  -5×: radius9 / height60 / ID`-334000983`
  -6×: radius10.8 / height72 / ID`1479372276`
-614 real physical bake sources;493 have non-walkable top/ground classifications.
  No renderer-bounds proxies, reduced giant clearance, shared agent-setting rewrite,
  per-spawn/world runtime bake or borrowed size profile.
- New-map random sizes3/5/6 have equal weights; inherited initial population5,
  maximum15 and supplied arrival effects/audio. This changes **only the new scene**.
  Forest3/5/6/4 choices and its unsupported4× warning are deliberately untouched.

## Isolated authoring / review

Unity **2021.3.25f1**, URP **12.1.11**, Blender **4.5**. No live Editor/Blender takeover,
forced Play stop, synthetic input or scene switch/save. All scene construction,
physical-source collection and baking occurred in a closed marker-owned worker:
`ODM-GiantForest2-iwrxdlei`, additionally marked `.shiganshina-owned` for this job.
The worker was refreshed from current saved source; older forest authoring state
is not authority to publish it. Do NOT invoke an old forest publication blindly.

Tools:
- `tools/blender/build_shiganshina_assets.py`: owned factory/background architecture,
  portable exports, packed editable studio and native Blender render.
- `tools/build_shiganshina_map.py --action prepare|art|build|capture|publish`:
  current-source snapshot, marker/lock guards, explicit unpublished `--revise`, native
  new-scene authoring/capture, then scoped publication. Existing published revisions
  require a deliberate new baseline; the tool refuses blind in-place rebuilds.
- `Assets/Editor/ShiganshinaDistrictBuilder.cs`: new-map-only batch builder/importer;
  no menu item or import-time scene operation. Saves a clone before edits. Removes
  only the cloned forest environment, never external gear renderers/hook references.
- `FrontendMenuBuilder`/frontend tools now author both destination choices and exact
  named routes. Saved Menu GUID/original archived UI/music are retained. Destination
  photo is derived from the actual native rooftop screenshot, not a concept image.

`ArtSource/Environment/Shiganshina/MapReview/` has native Unity overview, gate avenue,
roof route, canal/bridges, civic square, outer gate and top plan; Intel Iris Xe.
These are **static Edit-mode native art renders, NOT a recorded gameplay run**.
The Scout may be in its unsampled rest pose in the avenue plate; no animation asset
was changed. The packed Blender studio also has a native Cycles architecture plate.

First native draft review exposed oversized paving, ground strip normal artifacts,
arch/gate-join gaps and an overbroad scenery-root filter. Fixed before publication:
world-scaled paving UVs, flat hard-edge ground faces, closed semicircular doors,
solid spandrels/joining wings, and removal of ONLY the named cloned environment.
No hook lines/FX were deleted in the published scene.

**31 existing unchanged regression checks passed,0failed/0skipped:**
7world-navigation,14runtime-registration,9audio and1three-second player-only respawn.
Evidence `existing-regressions.xml`/`.log`. Those use their existing isolated fixture
scenes; they do **not** certify the new district's every route, crowd, roof collision,
held-player release, performance, Tab/menu keyboard interaction or complete launch.
No new or modified test code; latest runner state is FILTERED, not a full suite.
No fresh hands-on swing traversal, moving gameplay capture, standalone/WebGL/device
or full-map/corpse-safety certification. A user fresh Play feel/performance review
remains important before calling this final art/gameplay tuning.

Hash-guard publication preserved all pre-existing asset/setting bytes. After map
publication, the only deliberately revised pre-existing assets are GameMenu.cs,
FrontendMenuBuilder.cs, Menu.unity and append-only EditorBuildSettings.asset.
User forest scene, Titan prefab/face/eye assets, palettes/audio, movement/cameras,
current post-QTE tuning, old scenes and original art retain their starting bytes.

This map, menu revision and earlier stats feature are **local/uncommitted/unpushed**.
No commit/push requested in this task. Previous separate local eye checkpoint remains
`cc26cf0a`.
