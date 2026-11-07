# Player-only respawn; preserve the running world

Unity2021.3.25f1 / URP12.1.11. Replaces the historical3-second scene reload.

## Player-facing behavior

- Death still produces the approved independent Scout ragdoll, blood, bloodSplat
  and boneCrush once. **After3 scaled seconds**, restore the player to full maxHP
  at the authored starting location, without reloading the map.
- The SAME player root/health/target objects return. Existing camera, Titan AI,
  registry, music and scene references never need rebinding. Injuries, dead Titans,
  spawner counters/schedules, navigation data and world progress are NOT reset.
- If the start overlaps a Titan/corpse/scenery, try a tiny upward clearance then
  a deterministic bounded4/8/12m nearby search. Nearby births need real static
  support, slope allowance and WHOLE world-up controller plus solid-gear clearance.
  Ceilings above the original foot/step level are NOT accepted as a new floor.
- No valid birth -> remain dead/suspended, retry every.25 scaled seconds. Never
  enable a live controller inside solids or silently reload as a fallback.
- No added invulnerability, health regeneration, bigger attacks or cheaper nape
  kills. Restore maxHP for a NEW life only; surviving damage still persists.
- J/pause keeps the timer paused. The existing death delay was not shortened.
- Old independent permanent corpse keeps its original bounded8s lifetime rather
  than disappearing with a scene reload. Repeated deaths create fresh feedback;
  each life is still exact-once. The old corpse cannot damage the new player remotely.

## Architecture and ownership

`Assets/PlayerRespawn.cs` is runtime-installed by `PlayerHealth.Start`, not serialized
into saved scenes/prefabs. It records spawn world position/rotation/parent/scale and
pristine first-seen child poses/active states. Living enable flags are refreshed
only outside holds, release-pending and temporary ragdoll ownership. The motor
captures the healthy flags BEFORE suspension. Authored walking permission is kept
separately from ODM gas/inertia's temporary `enabledMovement=false` flag.

`PlayerHealth.Die` remains the sole death/HP authority. Its old `ReloadScene` method
and LoadScene call are REMOVED. It invokes a private player-only retry after3s;
ordinary `CancelInvoke` still cancels the scheduled retry (used in owned tests).
`GameElements` void deaths now work repeatedly, without the old one-scene latch.
A player lacking PlayerHealth gets one diagnostic, NOT an unexpected whole-map reset.
Menu/explicit scene changes are unaffected.

Respawn is an explicit new-life teleport, never a grab escape or stomp get-up shortcut:

1. While DEAD, disable live collisions and freeze live rigidbodies. Probe a pristine
   pose atomically, with no frame, simulation or rendering in between; restore the
   hidden death transform in finally even when clearance fails. Query only this
   physics scene. Unsupported solids/saturated queries fail closed. Old corpses are
   real obstacles, not ignored scenery. No post-simulation clipping/global settings.
2. Retire dead hold/release-pending clocks, old grip/hand velocity and motor release
   continuations. Normal LIVE release/whole-gear sweep policy remains unchanged.
   Cancel a previous temporary knockdown without replaying its stale cached flags.
3. Clear walking jump/gravity/release velocities and cooldown, hooks/anchors/lines,
   gas/boost/inertia/coroutines, attack charge/window, QTE HUD/old attacker, sword
   speed grace/numbers/toast/shake and animation/cloth history. Fresh F still required
   by existing input edges; prompt keys held across respawn stay quarantined until UP.
4. Detach the old corpse reference (not its physical motion), clear any temporary
   combat proxy on death promotion/reset, and arm new-life blood/audio receipt.
   Restore pristine local transforms/active states, original renderer/animator/
   rigidbody/collider/script enable flags, then live HP/controls at the clear point.
   Intentionally disabled components/renderers remain disabled. Legacy non-Scout
   RagdollController fallback resets too, including its joint collision settings.
5. Reset existing camera-follow interpolation/speed history and snap enabled follow
   owners; no new camera-update authority or global Physics/time change. All normal
   move/jump/gravity/ODM speed/animation/damage tuning stays intact.

Reset helpers are narrowly scoped on Movement, ODM_Movement, TitanODMMotor,
TitanCombatTarget/TitanAerialMotor, ScoutStompReaction/ScoutPlayerRagdoll,
PlayerDeathFeedback, Attack, TitanGrabEscape and TitanSwordFeedback. Public prototype
lease reset refuses live/laboratory health; it requires a DEAD real health source.
An old pending callback/proxy may never move or hurt a revived target.

## Validation

Evidence **`ArtSource/Player/Scout/Respawn/validation-summary.json`**, full/native/map
XML and actual `Player_Dead.png`, `Player_Respawned.png`. Marker-owned disposable
Unity workers only; never main/live Editor Play input/stop/save/reload/scene switch
or physics automation. No scene/prefab/navigation/clip/palette regeneration.

- Fresh before401pass/8fail/1skip of410 -> final **413pass/SAME8fail/1skip of422**,
  twice. **12 added default-suite cases** (capture body separately gated by env).
- Real3-second clock, same scene/player/target IDs, zero sceneLoaded callbacks,
  HP100, full control restoration, powered-flight walking lock reset, hooks/jump/
  trusted speed cleared with gravity16 and ODM speed tuning preserved.
- Whole solid gear and WORLD-UP headroom fail-closed; no dead-root drift on failed
  birth; paused/no-early revival; bounded grounded fallback; unsupported solids
  never restore controls. Repeated void deaths, disabled-state preservation and
  downed-death corpse promotion/reset with no old remote-damage proxy.
- Saved CURRENT GiantForest2 native suite **5pass/1SAME unbaked4x warning failure
  of6**, including new real3-second respawn check: injured Titan keeps severed leg
  and damaged arm HP, dead Titan stays dead, same player/manager/spawner/music IDs,
  same AI target and native navigation triangle count, zero scene reload events.
  No4x bake or user size/tuning edits.
- Native graphics **1/1**: real3s delay, 0->100HP actual HUD rendering, visible
  independent corpse and restored Scout in the SAME running world. Physical corpse
  speed remains<40m/s. Zero HP intentionally has no colored fill; original live HUD
  render assertion remains on the100HP frame, not weakened.
- Existing arm<8deg/120Hz, grip<.003m, later carry/mouth penetration<.005m and
  corpse<40m/s tests unchanged. No early-takeover-clearance or comprehensive
  long-duration10x corpse certification.
- Protected original assets/user scene/tuning/palettes/navigation/settings/Run,
  current raised-nape pose and sword sweep/damage source hashes unchanged.
  Current C# bytes match both workers. Baseline failures are not repaired by
  reverting user assets or weakening assertions.

Reproduce full `tools/run_titan_validation.py` then
`python tools/capture_player_respawn.py`; saved-map validation uses the owned forest
snapshot/test tool, NEVER a rebuild/bake/publication. Subjective spawn/camera feel,
performance and device/WebGL behavior still need ordinary playtesting.

Local selective checkpoint only. No push authorization.
