# Scout held struggle / squeeze animation

Unity2021.3.25f1 / URP12.1.11. Visual-only; the existing physical grip, HP and
motor remain authoritative. Previous Titan decomposition is LOCAL `091fe640`.

## Current revision: seated in the hand / slower back-of-sword beats

After `bf77e54c`, the user requested a waist slightly inside the hand, a body
perpendicular to the forearm, back-of-sword attempts to beat the Titan's arm,
and slower movement. They also requested no new validation test code.

- Held now lasts **1.8s** rather than1.2s (50% longer, two-thirds the old cadence).
  Only Held.anim is rebuilt, with the original GUID/controller unchanged.
- The existing visual-only flip pivot places the Scout waist slightly into the
  palm's heel, with body-up perpendicular to the current forearm axis and facing
  toward the wrist. Hips/other bones retain their local lengths/positions/scales.
  This is a RENDER placement: the actual root/native WORLD-UP controller and CCD
  grip remain exactly under the existing hold motor. No player reparenting.
- One bracing arm and one deliberately cocked-back sword beat per loop. Late
  visual two-bone fitting lays the sword's back/flat diagonally across the
  wrist/heel surface, followed by recoil. It does not trigger Attack, combat
  damage, sounds, an extra slash, escape progress or an automatic release.
- Mounting refuses pivots containing foreign colliders/RBs, resets cape history
  once on entry, tapers out over.18s as feeding begins to preserve the original
  native mouth presentation, and retains the existing safe pending exit.
  Normal release blends the local visual offset away; disable/history reset
  restores the original position/rotation. No new physical/camera writer.
- Current Unity2021 compile and the EXISTING owned both-hand graphics preview
  completed. **No new/changed test code and no fresh full-suite run or claim.**
  See `ArtSource/Player/Scout/HeldGripRevision/` for reviewed PNG phases/GIFs.
  Initial `HeldStruggle/` evidence and counts below are historical to bf77e54c.

## Initial presentation (bf77e54c; superseded placement/cadence)

`Assets/Art/Player/Scout/Animations/Held.anim` is now a1.2s seamless struggle loop:

- Waist/hips stay pinned to the current real grip. Spine/chest curl forward with
  repeated strain/compression, elbows stay bent/braced close to the upper body,
  head bows and turns with a small shudder, legs bend and kick out of phase.
- No actual bone scaling, limb-length change, hip bob/translation, gameplay root
  motion, camera writer, CCT tilt, ragdoll takeover, parenting into the Titan,
  grip/reach changes, re-centering/regripping or collider/force/velocity writes.
  The hunched rotational silhouette sells pressure, not a literal new crush system.
- Original Scout model/atlas/bones/blades/cape/Animator controller are retained.
  `Held.anim` keeps its original GUID/state/duration and loop settings. No new slash
  animation or weapon damage is enabled. Existing F cancel/release/fresh F unchanged.
- `ScoutPlayerAnimation.IsStruggling` requires BOTH motor suspension and a living
  confirmed `TitanCombatTarget.IsHeld` lease owned by an actual TitanGrabAttack.
  Windup/proximity alone cannot start it. The ordinary Held transition remains.075s.
- On release, including QTE success/cancel followed by blocked `ReleasePending`,
  the struggle stops immediately. The existing Held visual freezes while waiting
  for the original safe exit; the controls/whole-gear clearance are NOT bypassed.
  Ordinary animation playback resumes when the existing motor actually releases.
- Restrained/dead states flush residual hook arm-IK weights immediately so hook
  aiming cannot fight the braced elbows. Unrestrained attack/flight IK interpolation
  is unchanged. Disable/history-reset clears the held receipt/aim weights too.
- Scaled pause pauses the loop and damage. Death takes visual priority and keeps
  the current animated pose for the existing independent native Scout corpse.

Damage remains10HP/s from actual confirmed hold, the QTE is still3s, and all
bite/contact/hold/escape/grab-only grace/respawn clocks remain untouched. Struggle
motion is cosmetic and not a new source of damage, timing, resistance or escape.

## Scoped authoring / safety

`ScoutPlayerBuilder.HeldStruggle` is an editable rotation recipe. New menu
**Tools > Player > Scout > Rebuild held struggle only** calls the dedicated
`RebuildHeld()` scope; no full Build or Run regeneration. It saves Held.anim only,
not the prefab, controller, importers, materials, other clips, scenes or Titans.
A marker/batch-only `RebuildHeldOwnedBatch()` supports isolated authoring.

`tools/build_scout_held.py` compiles/runs that scope in the marker-owned Unity2021
validation worker. Hash guard105 protected current sources/assets (all other Scout
clips, controller, prefab, FBX/imports/textures, capes, current user Titan/scene/
palettes/navigation/tuning plus physical grip/QTE/motor/player/nape/sword sources).
It checks the original Held.meta GUID and refuses concurrent protected edits, then
publishes ONLY Held.anim. Initial clip backup is kept in Library/HeldStruggle;
worker-only prefab/model instances are disposed, never saved into the live scene.
The runtime validation runner also compiles the current editor authoring source.

## Evidence

**`ArtSource/Player/Scout/HeldStruggle/validation-summary.json`**, fresh full XML,
saved CURRENT GiantForest2 XML, native graphics XML,8-frame Left/Right PNG sets and
`Left_Struggle.gif` / `Right_Struggle.gif` (original Unity completed-frame captures,
1.2s loop). Marker-owned disposable workers only, never main Editor input/Play stop/
scene switch/save/reload/physics automation. No scene/prefab publication or4x bake.

- Fresh baseline429pass/8fail/1skip of438 -> final **449pass/SAME8fail/1skip of458**,
  twice. **20 added default cases**, including two no-env gated graphics cases
  (those passes alone are not actual graphics proof).
- Sampled original rendered skin changes, elbows>45deg and hands near chest,
  both legs and head visibly move; every bone LOCAL position/scale stays constant.
  Loop seam/no-snap at120Hz and same root/controller state/root-motion policy.
- Actual confirmation-only start, unchanged10HP/s damage, no windup drain;
  immediate stop and animator freeze on blocked release, no post-release debt,
  resumed playback/history reset, residual hook IK cleared, scaled pause and death
  priority. No existing assertion or physical tolerance weakened.
- Twelve NEW actually animated real-player integrations: both hands at3/5/7/10x
  timeout/feed/death and both hands3/10x successful QTE with original drain. Entire
  Held loop runs during native grab/mouth stages, retaining **<8deg/120Hz** Titan
  arm continuity, **<.003m** same grip and **<.005m** later carry/mouth penetration.
  Death copies the moving pose; new corpse samples remain **<40m/s** for1.5s.
  Early takeover overlap is separately recorded, NOT certified clear.
- Separate native graphics **2/2**: actual3x current-palm CCD capture, both hands,
  original Scout visibly changes pose while the same physical grip stays fixed;
  paused eight-phase captures keep real minimal QTE/health HUD present. Rendering
  verifies >100 changed pixels and unchanged root within each animation sample;
  across settled native hand re-solves the existing <.003m grip bound remains.
- Saved CURRENT forest suite **7pass/1SAME unbaked4x configuration failure of8**.
  New case uses actual saved enabled Scout driver/controller and a native chase/
  aerial contact (no direct remote hold shortcut). Automatic Held loop advances
  and head moves during real grab damage with QTE still3s, unparented from Titan,
  same player/scene/nav IDs. Cancel stops struggle. Decomposition/player-only
  respawn and other map cases continue passing. No bake/config adjustment.
- Both workers match current runtime/test/builder source and Held clip/meta;
  all105 protected sources/assets unchanged, including protected Run1.16666663s,
  despite its historical recipe. The eight pre-existing asset/tuning fixture
  mismatches were not 'fixed' by reverting user settings or weakening assertions.

Reproduce full `tools/run_titan_validation.py` before
`python tools/capture_scout_held.py`. Reauthor with `tools/build_scout_held.py` only
with a freshly audited owned worker and protected baseline; never run full Build.
Subjective restrained/squeezed feel, clipping from arbitrary world scenery/crowds,
performance/device/WebGL and comprehensive long-duration10x corpse safety remain
ordinary playtests. No subjective pain/difficulty/force certification.

Selective LOCAL checkpoint only; no push without approval.
