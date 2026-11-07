# Random Titan population and arrival strike

Unity **2021.3.25f1 / URP12.1.11**. New opt-in scene spawner; does not change approved
Titan art/hitboxes, Scout animation, player controls, catch geometry, global Physics,
timescale, pipeline or shared prefabs. No new commit/push: HEAD/origin remainb6447972;
this and the preceding runtime-registration work are uncommitted for review.

## Current size fallback / GiantForest2 update

Unbaked positive-weight sizes are now SKIPPED with an Inspector warning, not allowed
to block other supported sizes. Selection renormalizes valid weights; ALL unbaked
still errors safely. Current GiantForest2 supports3x/5x/6x/7x/10x. See
[7x/10x navigation and safe fallback](giant-forest-navigation-sizes.md) for current
bake-only workflow, preservation guards and validation. Older implementation evidence
below is historical. No runtime/per-enemy bake.

## Enable in FreeRoam

After Unity imports/recompiles, EXIT Play and choose:
**Tools > Titans > FreeRoam > Install random Titan spawner**.

Select the new **Titan random spawner** object. The menu assigns the existing scene
manager, passive modular Titan prefab, strike materials, and already-baked size choices.
It refuses duplicates, wrong scene/Play, or ambiguous manager; COPY backups go to
`Library/TitanSpawnerBackup/FreeRoam.before/after-<UTC>.unity`. It leaves the real scene
**dirty/unsaved**; save it yourself. It never bakes or enables/moves/removes existing
Titans. Default cap is raised to at least active placed Titans+4, minimum6, so a scene
already containing6 placed enemies still has room for the initial random arrivals.
No main-editor installation was automated in this task: Unity MCP was unavailable
(no connected instance). No keyboard injection/forced Play stop or YAML overwrite.
The one-time menu installation is required before this scene starts spawning.

## Inspector controls

`TitanRandomSpawner`:

- **Sizes**: weighted scale choices, defaults3x and5x with equal weights. Scale3 means
  about36m tall,5 about60m. Add choices/change weights;0 weight disables an entry.
  This is random selection among editable categories, NOT arbitrary unbaked continuous
  scaling. Only choices with matching baked clearance profiles participate; missing
  profiles show SizeWarning and do not block supported choices.
- **Initial Spawn Count**4, **Initial Delay**2s. Initial burst is staggered, one warning
  at a time. After it, **Minimum/Maximum Spawn Interval**8–16s. Initial count0 skips
  the burst and waits an ordinary randomized interval after initial delay.
- **Maximum Alive**6 on a manually added component (installer adjusts as above).
  **Count Placed Titans**ON: registered living placed enemies and other pending spawns
  under this manager consume the cap. OFF: cap only this spawner's population/pending.
  Lowering cap stops/cancels excess pending requests; never silently deletes living NPCs.
- **Automatic Spawning**ON schedules/replenishes. OFF permits manual requests only;
  it doesn't cancel an already requested warning. Disable the component to cancel it.
- **Random Seed**0: fresh private RNG per Play; nonzero repeatable proposal sequence.
  Does not seed/consume UnityEngine.Random or combat RNG.
- **Minimum Player Distance**80m, **Maximum Player Distance**0=whole map. Horizontal
  distance also applies while the player is airborne. **Minimum Titan Separation**15m.
  Optional **Use Custom Bounds** restricts proposals to the supplied WORLD-space box.
- **Candidate Attempts**24, absolute1..128: bounded per request; failure retries after
  **Failed Attempt Retry Delay**2s, never a busy infinite loop or distant snap fallback.
- **Warning Duration**.65s; **Effects Enabled**, **Local Flash Light** and materials.
- **Spawn Sound**: OPTIONAL AudioClip. Drag your own imported WAV/OGG/MP3 here.
  No supplied audio clip is invented. Empty is silent. Volume.7,3D spatial audio,
 10m min/600m max distance (editable max). Sound fires ONLY on confirmed activation.
- **Corpse Lifetime**25s and **Maximum Retained Corpses**6: cleanup ONLY spawned-owned
  dead rigs, never other placed Titans/corpses.0 lifetime disables timed cleanup,
  but the count cap still applies. Existing detached-limb debris lifetime is unchanged.
  Disabling spawner pauses corpse timer observation/cleanup until it resumes.
- **Despawn Owned On Destroy**ON cleans up its actors if the spawner is removed;
  disabling only pauses creation/cancels warnings and leaves living owned enemies.

Custom Inspector includes live counts/errors and **Spawn one**, **Cancel preparing
strike**, **Despawn this spawner's Titans** buttons. Spawn one still respects limits.

## How distribution, safety and sizes work

`TitanSpawnSurfaceSampler` caches loaded walkable triangles inside the scene's baked
bounds once, proposes triangles weighted by surface area and uniform barycentric points.
Combined Unity triangulation can include overlapping size data, so it is a proposal
source, not a promise of perfectly uniform per-agent density. Every proposal is filtered
against the chosen profile/scene physics. No repeated whole-map object scan or bake.

A request chooses one weighted size + random yaw, then tries bounded positions. It
uses `TryPrepareSpawn`: inactive clone in owning scene, full matching NavMesh/capsule
AND actual posed solid rig clearance, plus player bounds/distance and actor spacing.
Actual rig AABB reserves the inactive warning footprint, including wide arms, against
other prepared actors. No clone fights, moves, collides or displays during warning.

At warning expiry, player/population/spacing are checked AGAIN, then manager rechecks
matching NavMesh and real full-body solids before activation. New obstruction/player
approach -> cancel and destroy the inactive reservation, stop cosmetic warning, no
strike/sound or invisible attacking enemy. Successful activation -> full live enemy
and confirmed strike/audio. No damage/forces from the lightning/flash themselves.

Source API: `TryQueueSpawn(out error)`, `CancelPending()`, `DespawnOwned()`,
`RestartSchedule()`. Manager now exposes `TryPrepareSpawn`, `TryActivatePreparedSpawn`
and prepared/live counts; every caller must activate or Despawn prepared results.
Existing atomic `TrySpawn` composes the same two methods. AABB reservations can be
conservative near rotated arms; fail safely rather than overlap delayed spawns.

Production data still supports3x/5x. Spawner choices automatically join the requested
sizes in **Rebake full-map navigation** (alongside existing profiles/placed/extra sizes).
For a new size, save in Edit Mode then rebake ONCE; unsupported positive-weight choice
is skipped with SizeWarning; scheduling continues for valid sizes. If none are
supported, it reports an error and waits until baked/corrected. No runtime bake,
no size borrowing, no per-spawn bake. Removing size choices doesn't delete existing
profiles. Static map changes still need their usual rebake. Distribution may include
disconnected walkable islands; it doesn't invent climbing/jumping or guarantee a path
from every spawn to a flying player. Later traversal/crowd clearance remains separate.

## Arrival presentation

Four faint gold jagged warning bolts, then bright additive lightning, short LOCAL
point-light flash, sparks, ground dust and rising steam. Height/width/particle size
scale with enemy size. No fullscreen postprocessing flash, shake, damage, collision,
global fog/light writes or parent into the Titan's detachable skeleton.

`TitanSpawnStrikeEffect` pool defaults4, hard maximum8; at most128 particles per system
(4 systems per slot). Old slots recycle; reducing pool size trims on next use. Sound
voices use each effect's3D source; recycled/disabled effects stop their own audio.
Materials: `Assets/Art/Effects/TitanSpawnLightning.mat` + existing `TitanSwordImpact.mat`.
New shader `ODM/Titan/SpawnLightning` is scene-material-referenced for builds; no runtime
Shader.Find/material allocation. Warning cancellation clears its particles/bolts.

## Evidence / limitations

Final full disposable suite **193/195 passed**,2026-11-04 **21:43:58–21:44:18 UTC**,
PID3784,19.584s; previous193/195 at21:40:22–42,PID17444. Only SAME2 untouched retimed
Run assertions fail.12 new cases all pass: weighted repeatability/global RNG unchanged;
inactive warning -> configured live enemy/shared unchanged nav; cap/placed ownership;
player/new-solid cancellation with no impact sound; bounded FX pool/materials/optional
clip/no physics/time/player-transform writes; own corpse replacement/cleanup; disable
cancels reservation; unsupported sizes feed next bake without hidden runtime bake;
impossible bounds finite attempts; inactive full-arm reservation overlap; shader import.

Graphics-enabled OWNED runtime URP capture separately **1/1 passed**,
**21:44:29–30 UTC**,.998s. Stock URP12 renderer in disposable fixture; pipeline restored
in finally, NEVER changed main project. Pixel assertion checks visible gold lightning
ABOVE the Titan (not just its lit body). Prepared invisible actor in warning, activated
on strike, actual dust/steam and normal AI movement afterwards. Raw640px images in
`Library/TitanSpawnerReview`; panel/raw preview copies in `ArtSource/Titan/Spawning`.
`tools/capture_titan_spawn_strike.py` reuses OWNED validation project with graphics;
full runner remains nographics (adds imageconversion module only for capture encoding).
Final audit byte-matched192 runtime/test/art/effects/Editor files to the completed
validation copy; all new GUIDs unique. Saved scenes/shared prefabs/approved art and
animation/navigation/project settings remain unchanged from pushedb6447972.
No unsafe Editor-preview physics; no synthetic user input/live Scene Play automation.

These are controlled fixtures, NOT ordinary full-speed ODM map/crowd performance or
final visual/audio approval. User clip isn't assigned or aurally reviewed. Full-map
random-density/performance, disconnected islands and naturally moving giant combat
need in-editor play review after one-time installation. No actual production bake or
main-scene save/installation was performed during these validations.
