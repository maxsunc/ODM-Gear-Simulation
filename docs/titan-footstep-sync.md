# Titan footstep synchronization follow-up

Unity2021.3.25f1 /URP12.1.11. User reported running impacts, and some walking
impacts, not matching the visible feet. HUD simplification was committed FIRST as
`da1e366c`; this is a separate audio-only runtime fix. No push authorization.

## Reproduced causes

`Assets/Titan/Integration/TitanAudioEmitter.cs` previously considered a foot landed
when its sole was within **.10m * Titan scale** of scenery. The animation authors
plant at **.035m * scale**, so this could emit during descent rather than at the
visible plant. Sampling the original clips showed roughly .10 cycle of walk / .125
cycle of run between the old event and the lowest sole pose (about85–100ms at
nominal unmodified cadence). Previous tests counted two events per cycle but did
not assert the event's actual sole position.

The second issue is voxelized NavMesh elevation: gameplay roots ride slightly
above terrain. The old test compared only with terrain Y, so even the LOWEST sole
could stay ABOVE the .10m threshold and lose every step. Real unchanged forest
bakes sampled with full-rig-clear native births:

| Size | Root above terrain (m) | New walk/run strikes in3 cycles |
|---|---:|---:|
|3x|.349803|6 /6|
|5x|.541523|6 /6|
|6x|1.457994|6 /6|
|7x|.3472681|6 /6|
|10x|.1079941|6 /6|

These are sampled placements, not universal terrain offsets. For example3x's
minimum sole=.349803+.035*3=.454803m, beyond the OLD .30m threshold. Its walking
and running could never satisfy the old check there. Moving across voxel heights
explains intermittent audible omissions even at unchanged gait.

A third small issue: losing ground armed a foot, so support returning underneath
an already planted foot could invent a strike without a fresh swing.

## Fix / retained constraints

- Require nearby real scene-local, non-trigger, non-Titan/non-player support as
  before, rejecting saturated ray results. No fake ground plane/physics collider.
- Accept only root-to-ground gaps<=.75*scale (covers observed baked offsets).
  Compare the animated sole with the HIGHER of real ground and root elevation to
  account for nav sampling height without changing either the rig or terrain.
- Land at .04m*scale: authored .035m sole pad +.005m sampling margin, instead of.10m.
- Retain .20m lift hysteresis, preventing repeated stance/bounce/rocking impacts.
- At low frame rates, a narrow minimum can fall BETWEEN samples. Only use an
  observed down→up turn after a genuinely lifted foot and previous clearance<=.06m
  as fallback. Not a timer, guessed beat, normalized-phase hardcode or prediction.
- Lost/invalid support resets per-foot history rather than manufacturing a lift.
- Keep every original safety gate: actual motion, Locomotion/transition, ready
  actor, no death/blindness/missing leg, hitch/teleport/invalid dt/scale rejection.
- Same supplied clips, source pitch/volume/spatial settings, SFX routing, palette,
  private randomness and one-shot playback. No sound files/imports were rewritten,
  retimed, cropped, normalized or renamed. No animation changes or animation events.
- No Animator/bone/root/motor/agent/physics/collision writer or global SyncTransforms
  added to the audio observer. This does not change gait cadence, approved tuning,
  physical grab/death limits or placement rules.

The existing footfall tests also now apply their requested CycleOffset AFTER
TickMotion, which otherwise overwrites it. Their two-per-cycle assertions remain
intact; they now genuinely exercise the intended offsets.

## Validation

18 added audio cases:9 geometry/phase/root-height combinations,7 real-Animator
30/60/120FPS &.35–1.65 cadence combinations (walk/run/blends, sizes1/3/5/7/10),
support loss/reacquisition and nearby airborne rejection/observer non-mutation.
An independently computed8-corner minimum verifies actual emitted sole position;
real Animator advance checks the first observable frame after a skipped minimum
and exactly2 strikes per completed cycle, not just a final sound count.

Control: previous unmodified emitter with these same tests **7pass/19fail of26**.
Fixed emitter **24pass/2existing palette failures of26**:17 formerly failing new
cases repaired; the observer non-mutation/airborne case also passes. Original
one-scream-vs-two fixtures remain failing unchanged; no user palette reset.

Full suite **310pass/7SAME existing failures/1graphics skip of318**, versus
pre-audio300:292/7/1. Original protected Scout Run, user face/stun/reference-speed
and one-scream fixture conflicts stay disclosed. No physical threshold relaxed.

Owned native CURRENT saved GiantForest2 suite: new all-five-size terrain/footstep
integration passes, as do original exact-route and7x/10x full-clearance birth cases.
The existing random-spawn fixture still asserts no SizeWarning, but user's new
saved4x choice is unbaked: **3pass/1preexisting configuration failure of4**. Running
unmodified prior map tests on this same saved scene reproduces **2pass/1same fail
of3**. No assertion weakened;4x is safely skipped by authorized fallback. Preserved
user's3/5/6/4 weights, initial5/max15 settings; did NOT bake/disable4x or re-enable
old7x/10x weights. Navigation assets remain unchanged and no bake was run.

All work ran in marker-owned disposable Unity2021 workers; no live Editor input,
scene/Play control or live physics automation. Saved scene, prefab tuning, art,
animations, original audio/imports/palettes, project physics/navigation/versions
are preserved. See `ArtSource/Audio/TitanFootsteps/` for regression/control XML,
terrain report and preservation summary.

This verifies event dispatch against actual evaluated soles, not subjective speaker
balance, device/DSP latency, WebGL output or crowd FPS. Supplied audio attacks have
about15–35ms lead-in and their original .5–.7s texture/tails; left unchanged. Please
use a fresh Play session and listen during walking, acceleration and running.
