# FreeRoam full-map navigation TEST

## Checkpoint / request

User requested committing the real ODM integration and their copied/scaled Titans,
then trying full-map navigation in `Assets/Scenes/FreeRoam.unity`.
Checkpoint **0f371f2c** (`Integrate real ODM combat and checkpoint scaled FreeRoam Titans`),
local on `titan-combat-updates`; no new push requested. Includes sight defaults138m /
180deg and the existing scene/player/FBX dependency closure. Unrelated Plastic/IDE/
material/Ragdoll/unused model/prefab work excluded. The new navigation experiment is
a separate checkpoint, committed at the user's next request before switching to scout
character/sword/animation planning. No new push requested; see Git log after0f371f2c.

## What changed

Scene object **FreeRoam Titan navigation TEST**:
- `TitanWorldNavigation`: owned pre-baked NavMeshData registration, removed on
  disable/unload without removing any other scene's navigation.
- `TitanFreeRoamIntegration`: real player target, existing patrol/perception/motion,
  per-size agents, editable Sight Range/Field Of View/Patrol Radius/Enable Combat.
- Default **Enable Combat=false** for navigation testing. Turn it on in EDIT MODE
  before Play to opt into existing stomp/grab/bite. Giant-scale combat distances,
  terrain foot IK and ragdoll physics are NOT all retuned/approved by this nav pass.
- Existing ODM controls/tuning retained. Scene-only health/motor/sword bridge added
  to FreeRoam's existing player, same contract as Shiganshina; no shared prefab edit.
- Saved Titan positions, rotations and scales3x/5x are not moved/resized.

Navigation is NOT the old256m box-only runtime bake. Editor collects **338 physical
sources**, including332 MeshColliders, under explicit Map Control/Cube roots. Excludes
triggers, disabled/inactive geometry, actors/controllers and Rigidbody-owned moving
geometry. Uses Unity's actual collider conversion, supports actual TerrainCollider
heightmaps too (this scene currently has NO Unity Terrain component). No renderer
bounds substituted for ground, no fake flat walkable plane, no 332 mesh import/read-
write settings changed. CPU-unreadable map meshes bake in the EDITOR; Play loads the
baked data instead of rebuilding a2km world at startup.

## Clearance and data

`Assets/Navigation/Titans/FreeRoam/Titan_3x.asset`: world radius5.4m / height36m.
`Assets/Navigation/Titans/FreeRoam/Titan_5x.asset`: world radius9m / height60m.
Each owns a different authored agent type in `ProjectSettings/NavMeshAreas.asset`:
**ODM Titan FreeRoam 3x**, **ODM Titan FreeRoam 5x**. Existing Humanoid0 radius.5 /
height2 preserved. Native type IDs can be NEGATIVE; only-1 means missing.

Build slope40deg, climb.6m*scale, voxel(radius/4). Baked bounds center
(-98.98,346.54,-351), size approximately2122x908x1595m, includes the entire physical
map (~2064x1538m, tall trees) with margin. Profiles canonicalized to.001 scale so
lossy-scale rotation roundoff cannot create duplicate3x types. Nonuniform/negative
scales fail explicitly; changing scale/map geometry requires rebake.

IMPORTANT: Bake dimensions are WORLD-sized; `NavMeshAgent` avoidance dimensions
follow its transform scale. Component radius stays~1.8 and height~12, so3x/5x world
avoidance is5.4/9m and36/60m, not accidentally16.2/45m radius. Regression added for
exactly-once scale. Nav filter uses the corresponding size TYPE: large bodies cannot
borrow smaller data through narrow gaps. Re-bake keeps data GUIDs/type IDs stable.

## Known placement issue — not silently hidden

The user's **5x Titan at(20,-1.84,25) intersects mediumTree (193)**. A real environment
capsule overlap confirms that tree; its collider bounds center(18.09,61.92,31.57),
extents(17.73,63.94,17.81). Correct5x nav has NO point within3,5 or10m of that spawn;
nearest sampled point is(35.76,-1.36,13.18),19.71m away.3x could fit much closer.

Setup deliberately does NOT shrink clearance or snap the giant20m through scenery.
It shows a spawn error and leaves that Titan inactive while the3x navigates normally.
**Move Titan2 onto clear ground** (aroundX36/Z13 is one tested option) before Play.
Moving an actor onto already baked clear ground does NOT require rebake; changing
scale/environment does. The5x was relocated ONLY in temporary Play Mode for validation;
its original saved transform is preserved. Startup projection is bounded to3m.

## Verification

All116 earlier checks retained +7 navigation regressions in owned disposable runtime
PlayMode (never Editor-preview-scene physics): explicit static physical-source filtering,
cross-scene rejection, actual TerrainCollider collection,500m sloped-mesh route around
cover,16m gap passes3x but rejects5x, exactly-once scaled avoidance, owned registration/
disable/re-enable without removing external data. Runner adds terrain/terrainphysics
built-in modules only in its disposable project.

**123/123 passed twice**, Unity2021.3.25f1,2026-11-03:
- **19:03:46–19:04:02 UTC**, PID21108,16.006s.
- **19:05:38–54 UTC**, PID26368,16.248s.
Source-matched final runs; latest evidence also in checkpoint/validation document.

Live Unity MCP, unpaused:
- Both full-map data registered;20532 combined triangulation indices.
- 3x natural startup on its mesh, Chase/Following complete path; actual player HP100.
- Regular ground-grid samples:3x61 complete /68 sampled,5x60/67; seven disconnected/
  blocked destinations fail complete-path requirement rather than taking partial paths.
- Longest tested complete native routes **1409m (13 corners)** and **1418m (14 corners)**
  to(-900,-1000). Native actors actually travelled >1.3km, not just numerical path queries.
- First run exposed double-scaled avoidance: corrected component dimensions and added
  regression. Final run also tests distinct destinations (same point cannot hold two
  giant bodies):3x travelled147.41m, stopped1.87m from goal (within3m stop distance);
  5x travelled204.93m, stopped0m from goal. Both onMesh/PathComplete/not pending/
  velocity0 after loading rebaked assets. Long traversal is not a full crowd/terrain/
  art/physics approval.
- Re-bake confirmed same type IDs + NavMeshData GUIDs, agent-type count3 before/after,
  unchanged original Humanoid dimensions. Native Play reloaded the rebaked data.

Temporary camera/RT/route overlays are removed; fog restored. Screenshot is the REAL
scene with temporary colored native route overlay, not an image of every walkable
polygon: cyan3x / orange5x. Proof `ArtSource/Titan/Navigation/FreeRoam_FullMap_MCP.png`.

## Use / rebake

1. Open **FreeRoam**, select **FreeRoam Titan navigation TEST**.
2. Move5x out of the tree. Tune **Sight Range** / **Field Of View** here permanently;
  138/180 defaults preserve the user's choices. Patrol Radius200m controls wandering,
   not vision or bake extent. Runtime Inspector brain changes reset on leaving Play.
3. Play. HUD shows state/path/error for each bound agent. Scene-view Gizmos draw cyan
   current path lines; select navigation setup for overall baked-bounds wire cube.
4. To fight, enable **Enable Combat** before entering Play. Original F swords/Q,E hooks/
   Space gas/Shift boost/C jump remain. T is music, not a lab combat toggle.
5. After map/scale changes: SAVE scene, then **Tools > Titans > FreeRoam > Rebake full-map
   navigation**. Installation command refuses duplicates/wrong/dirty scenes; re-bake
   only processes the explicitly assigned geometry roots. New world geometry outside
   Map Control/Cube must be added to Geometry Roots before rebaking.

Backup: `Library/TitanFullMapBackup/FreeRoam.unity` and original NavMeshAreas.asset.
Limitations: disconnected roofs/outside-boundary islands, no jumping/climbing links,
no runtime streaming/carving for moving trees, existing vertical airborne-goal projection
not yet a full multi-floor pursuit system, no automatic carry/full-body terrain IK or
scaled combat/ragdoll approval. No global Physics tuning or approved rig/art modifications.
