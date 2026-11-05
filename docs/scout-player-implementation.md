# Scout player — first implementation / review slice

## Latest: real bite/death physical presentation

See **docs/titan-scout-bite-presentation.md**. Scout now feeds side-on into mouth and
hands off captured real skin pose to independent13-body/12-joint corpse on lethal
chomp; accepted HP loss drives existing blood pool once. PlayerHealth automatically
adds passive runtime ScoutPlayerRagdoll (no new live physics), safe controller exit
cannot move corpse. Clone Animator disabled, captured bone pose preserved, no passive
visual prefab/FBX/clip changes; live Scout driver stops on death. Existing3s respawn,
C/F/hook/gas controls/tuning unchanged. Non-Scout fallback kept with legacy null guards.
222/224 twice (same2 Run failures),native3x graphics1/1. User authorized committing
this follow-up and pushing the branch (including12901b9c); see latest checkpoint.
Old initial-slice scope below is historical. No main scene/
Editor Play/input automation. Early reach/scaled-ground/full-speed feel separate.

## Historical initial scope and status

Pre-overhaul full checkpoint **16517e6c**, after navigationc07d4295 and ODM integration
0f371f2c. User explicitly approved the Scout/run/grapple/fall version and authorized a
checkpoint: **059dd52e** committed locally before attack changes (122 paths; locked.vs
cache excluded). No push. New mirrored attack work AFTER that checkpoint is **uncommitted
for review**. Unity remains
**2021.3.25f1**. Installed/saved ONLY in `Assets/Scenes/FreeRoam.unity`. Shiganshina and
shared `Assets/Prefabs/Player.prefab` remain unchanged. Tools menu can explicitly opt
Shiganshina in later. The user's3x/5x Titans, navigation data and placements are retained;
5x still starts in its previously reported tree overlap (not a new player-model issue).

This is a functional first art/motion pass, not final character-art, cloth, terrain IK,
all-terrain clipping or full-speed gameplay approval. No new particles/trails/VFX added.

## Assets / reproduction

- `tools/blender/build_scout_player.py`: own `ScoutPlayer_v1` collection ONLY, never
  clears user objects. Executed through local Blender addon by `tools/blender_request.py`.
  Project root injected by client (not tied to a hard-coded author machine path).
- `ArtSource/Player/Scout/ScoutPlayer.blend`: self-contained owned collection library,
  packed images; does not replace/save the user's open Blender main file.
- `Assets/Art/Player/Scout/ScoutPlayer.fbx`: **11520 triangles,43 rest bones**, Generic rig
  with torso/limbs, individual finger chains and two cape bones. Weighted ring geometry
  across elbow/knee/shoulder transitions; source unit height~1.83m, visual scale2 for
  existing3.6m gameplay capsule. This is NOT a Humanoid-retargeted or human ragdoll rig.
- Brown cropped jacket, light trousers, leather harness/boots, green removable cape,
  hip-mounted gear, canisters, blade magazines and paired pistol-grip swords. Separate
  rigid `ScoutBlade_L/R` cutting meshes and `ScoutHilt_L/R`, parented to corresponding
  hand bones with actual grip-centered origins; no distant pivot/collision enlargement.
- One shared1024 PBR atlas: BaseColor(sRGB), MetallicSmoothness(non-color, smoothness A),
  normal texture. Skin/cloth/leather/metal palette regions have different response.
  Exported PNGs + explicit **URP/Lit** material for this project's existing URP pipeline;
  not a Blender-only node shader and no render-pipeline/version upgrade.
- `Assets/Prefabs/Player/ScoutVisual.prefab`: visual rig only, no movement/physics/health.
- `Assets/Editor/ScoutPlayerBuilder.cs`: reproducible Unity-authored Generic pose-curve
  clips/controller from imported rest axes; not a claim of mocap/Blender animation takes.
  Blender file contains the model/rest rig.24 editable clips live under
  `Assets/Art/Player/Scout/Animations/`. Rebuilding overwrites these owned generated clips;
  copy hand-edited variants before changing the recipe. GUIDs are retained on re-build.
  Constant channels compacted:225575 ->22090 keys,21 samples per4200 bindings,
  max sampled channel delta5.69e-8; source art folder113MB ->16MB. No pose retiming.

Run:
```
python tools/blender_request.py --file tools/blender/build_scout_player.py
python tools/blender_request.py --file tools/blender/render_scout_preview.py
```
Then **Tools > Player > Scout > Build visual assets and animations** in Unity Edit Mode.
No Editor-preview-scene physics tests or simulation used for this feature.

## Current checkpoint / following runtime work

User-authorized checkpoint **b6447972** is now committed and PUSHED on
titan-combat-updates: includes mirrored attacks, sword damage/effects and user-saved
scene/weakpoint edits. Later reusable Titan initialization is local/uncommitted; see
docs/titan-runtime-initialization.md (181/183 twice,14 new tests, same2 Run failures).
No Scout art/animation/controller changes during the runtime-registration step.

## Previous: sword damage and feedback integration

Added scene-only `TitanSwordFeedback` and DamageDealer speed profile, HP thresholds,
white/yellow/red confirmed-hit numbers, render-only camera shake, actual tip trails,
blood bursts/moving sever spray and original provisional SFX. No animation assets
rebuilt/retimed; approved mirrored wind-up/cut, F timings, controls and physical
blade sweep authority retained. Whole severed branch now gets coherent cut momentum.
See **docs/titan-sword-damage-effects.md** for values/Inspector/validation/limitations.
Latest167/169 (same2 Run failures),2026-11-04 03:51:08–27 UTC. Native F confirmed
arm damage/sever; no claim of full-speed ODM proof or aesthetic approval. Setup
installed on FreeRoam but scene unsaved while user in Play; save after Play, don't
force-stop user session. New work remains uncommitted after059dd52e.

## Previous: both arms load opposite the slash (including F-held wind-up)

User correctly rejected the previous almost-symmetric behind-neck chamber: mirroring
it did not clearly move BOTH hands to the opposite starting side. Changed authoring:
- Base charge/attack: BOTH hands behind LEFT shoulder, finish slash RIGHT.
- `_Right` mirrored variant: BOTH hands behind RIGHT shoulder, finish slash LEFT.
  Historical `_Right` / MirroredAttack / review attackSide refer to LOAD side, NOT
  finish direction. New preview labels and ToLeft/ToRight filenames name actual swing.
- Same directional pose used throughout F-hold and release anticipation; early arc
  keeps the loaded side rather than resetting to symmetric hands-above-head.
- Near arm folds~125deg, opposite arm bends~60deg and reaches across behind the neck
  to the same shoulder. Earlier both-arms>110deg assertion intentionally changed to
  reflect this cross-body reach; deep near fold retained, stronger both-hands/swing
  opposition assertions added. No Titan physics/continuity thresholds changed.
- Unity automatic quaternion tangents initially drifted~.2-.5deg before stroke. Added
  KeepWindupFlat after quaternion continuity so initial.065s exactly matches charge,
  rather than relaxing new matching test. Hold F and first slash frames now agree.

Final **155/157 passed,same2 known Run failures**,2026-11-04 **02:29:24–40 UTC**,
PID33772,16.721s. New3-context tests check BOTH hands> .08m opposite swing relative
neck, same charge/initial attack pose at0/.03/.06s, early arc preserves side, finish
both on other side. Existing curve/mirror/full-extension/selection tests retained.
All runtime/test/animation/controller bytes match completed disposable run; noncombat
clips/controller SHA256 unchanged. No root/motor/scene/physical damage timing changes.

Actual F-hold tests prove native ChargeAir variants before release: right-load handX
+.45549/+.41748 world m, left-load-.41748/-.45549, then correct cached-side release,
active damage and normalizedTime*.39 matching age (~.14040/.13088s). Hand IK0/root0,
no transition at charge snapshot. Controlled motor-suspended sky fixture, NOT normal
ODM traversal proof. Updated input helper checks charge pose as well as release.
Preview **Scout_Directional_Windup_Unity.png**, **Scout_Slash_ToLeft.gif**,
**Scout_Slash_ToRight.gif**, **Scout_Mirrored_Attack_Unity.png** under ArtSource/Player/
Scout. GIFs are authored skin-bake renders with review pauses; paired wind-ups are
actual F-hold native captures. Old Scout_Slash_Left/Right.gif are historical superseded
load-named first-pass previews. Scene remains clean/unpaused Edit Mode; before/after
copies identical. Work still uncommitted after approved059dd52e; no new push/commit.

## Previous: mirrored two-arm curled wind-up / swift arcing slash

After local approved Scout checkpoint **059dd52e**, user requested arms curled behind
neck/back -> swift arc -> fully extended arms, mirrored left/right, randomly chosen.
Implemented new charge/attack pairs for Ground/Air/Held: existing base names represent
left-load/right-finish, six new `_Right` clips/controller states mirror the entire body.
Controller now28 states. Both arms participate in BOTH variants; not single-arm attacks.

- Charge has~125deg elbow fold, hands behind neck/shoulder plane, steel back/down.
- Distinct chamber(.065s), overhead arc(.10), front cut(.14), extended side finish(.185).
  Nonuniform C1 Hermite phase interpolation carries velocity through arc waypoints
  rather than stopping at each pose. Fully extended finish holds through clip end.39s.
- Full-body hip/chest twist, head counter-look, grounded brace/air leg scissor, held
  context retained; exact sagittal reflection swaps sides, mirrors yaw/roll/hip offset.
- `Attack.ChargeStarted` event chooses cosmetic side ONCE per F press. Separate
  System.Random,50/50, repeats allowed; does not perturb Unity random used elsewhere.
  Driver resolves charge + slash to SAME cached side across contexts; no reroll on
  release, no Update fallback restarting the right slash as the base left state.
- Existing F hold/release, attack.39s = anticipation.075 + damage.12 + recovery.195
  unchanged, same physical blade sweep/dedup. No buffering/combo/VFX/root impulses.
  Attack/charge still suppress hand aiming; normal hook IK resumes afterwards.
- **Tools > Player > Scout > Rebuild mirrored attack poses only** / RebuildAttacks()
  regenerates ONLY12 combat clips. All Run/Fall/grapple/release/idle/land clips + metadata
  verified SHA256 byte-unchanged; passive model/prefab/material, scene/player tuning
  untouched. Full build still regenerates all owned clips, don't overwrite user run.

Final owned Unity2021 suite **152/154 passed,same2 known retimed-Run failures**,
2026-11-04 **02:10:30–47 UTC**,PID14608,16.607s. New tests pass: three contexts'
folded neck chamber, actual steel's curved arc, both arms fully extended through finish;
full-body mirror agreement<.005m, charge-time selection stable across all contexts,
seeded50/50 with repeats/global RNG preservation, single charge-start event/timing.
All prior regressions retained; unchanged Run assertions still fail, not weakened.
Runtime/test/ALL animation/controller files byte-match completed disposable project;
new6 clip metas present. No Editor-preview-scene physics tests used.

Actual Windows F input (controlled unpaused Play sky fixture) exercised BOTH sides,
seeding cosmetic RNG only for deterministic coverage:
- Right: chargedRight=true, same release side, age.1456405s, normalized.37343696*.39
  matches age, actual AttackAir_Right, damage active, no transition, hand IK0/root0.
- Left: chargedRight=false, age.1344721s, normalized.34480032*.39 matches age, actual
  AttackAir, damage active, no transition, hand IK0/root0. Always releases F/exits Play.
This proves native input synchronization, NOT normal-input full ODM combat clearance.
No claim of comprehensive shoulder/cloth/gear/terrain clearance or aesthetic approval.

Review: **ArtSource/Player/Scout/Scout_Mirrored_Attack_Unity.png**,
**Scout_Slash_Left.gif**, **Scout_Slash_Right.gif**.25-frame authored Unity skin-bake
GIFs have initial/final review pauses, not gameplay video. Reproduce via
`tools/capture_scout_attacks.py`; two-side input check `tools/review_scout_attack_input.py`.
Scene left CLEAN/unpaused Edit Mode, original placements preserved; before/after scene
copies byte-identical. No new save/reload or post-attack commit/push. Approved checkpoint
remains059dd52e; these new attacks are reviewable working-tree work.

## Previous: gradual Fall entry / Inspector blend-duration control

User requested smooth previous-pose -> Fall instead of immediately raising swords,
and asked where to adjust animation speed. Fall entry now uses **.5s native fixed-time
crossfade** (was.075s), from whichever animation is current. No added delay to physics,
hook input, attack or landing; high-priority states interrupt as before. New serialized
`fallBlendDuration` on ScoutPlayerAnimation defaults.5, runtime minimum.05. Static policy
retains default; runtime GetPoseTransitionDuration applies the actual Inspector value.
No clips/controller/assets rebuilt or retimed; all animation folder files SHA256 unchanged.

### User controls: transition smoothness vs actual animation speed
- In **Hierarchy > Player > Scout Player Animation > Animation blending > Fall Blend
  Duration**, edit seconds. Larger = slower entry (.75 is softer; .3 is snappier).
  Set in Edit Mode and save scene to retain your chosen override, not only during Play.
- For actual clip playback, open **Assets/Art/Player/Scout/Animations/ScoutPlayer.controller**
  in Animator, select the state, change its **Speed** in Inspector (1 normal, .5 half,
  2 double). The state speed multiplies the runtime Animator speed; editing global
  `animator.speed` instead is overwritten by this driver's Update.
- Fall is a mostly held pose: slowing its clip does NOT soften entry; use Fall Blend
  Duration. Run also has measured-speed cadence in RunPlaybackSpeed (and known retimed
  clip mismatch); attack playback is tied to physical hit timing, don't retime those
  independently and assume synchronization. Dual recline entry remains.75s unchanged.

Final validation **147/149 passed,same2 known Run timing failures**,2026-11-04
**01:46:00–17 UTC**,PID45036,16.833s. New tests pass: tunable field default/custom/min
and unchanged urgent transition durations; actual Animator entry from8 prior poses
(single/dual hook, rise/coast, release endpoints), no immediate bone jump, still blending
at.1s and reaches authored overhead-sword/downward-gaze Fall. Runtime/test bytes match.
Native controlled single-hook -> Fall review: hand heights~.4m initially, ~1.3m at.18s,
~1.9m at.28s, ~2.09m final by~.5s, root/pivot angles0. Motors suspended sky fixture,
NOT ordinary-input all-terrain flight proof.27-frame **Scout_Fall_Smooth_Transition.gif**
in ArtSource/Player/Scout; tools/review_scout_fall_transition.py.

Scene clean/unpaused Edit Mode retained; no main scene save/reload. Before/after COPY
snapshots differ ONLY by new default `fallBlendDuration:0.5` serialization line. All
existing user scene data preserved, no blanket save. No commit/push.

## Previous: falling with nearly vertical crossed swords and downward gaze

User requested both swords directly overhead, slightly crossed, head looking down.
Changed Fall recipe ONLY: raised upper arms, inward forearms, hand roll for readable
steel; tips overlap slightly in front projection with depth separation so steel stays
apart. Actual cutting mesh long-axis direction~19.56deg from vertical (not just raised
hands). Neck/head flex makes gaze~38deg downward. Existing torso/leg/cape pose retained.
`RebuildFall()` / **Tools > Player > Scout > Rebuild fall pose only** regenerates ONLY
Fall.anim; full/flight scoped builder preserved. Every other animation/controller/meta
file SHA256 byte-unchanged, including Run, dual/single grapples, releases/backflip.
No runtime state/IK/input/motor/root/camera changes; normal aim/attack/hold priorities
still override default falling arms when appropriate. New Fall is used by ordinary
freefall and last-single-hook detach via the existing state machine.

Final validation **145/147 passed, same2 known Run timing failures**,
2026-11-04 **01:36:15–32 UTC**,PID40012,17.191s. New passing actual-mesh check samples
Fall through whole loop: both blades<20deg from up, cutting blades above head,
slight projected crossing near tips with.02–.12m source depth separation, gaze down,
no root bindings/movement. Initial iteration's tips only nearly met; corrected pose
rather than weakening crossing check. Run timing failures untouched.

Native front/profile static skin-bake proof **ArtSource/Player/Scout/
Scout_Fall_Overhead_Unity.png**, tool `tools/capture_scout_fall.py`; no physics stepping,
not actual-input gameplay video. Scene was CLEAN at entry this task (user saved since
last pass); remains clean unpaused Edit Mode, before/after snapshot copies identical
in Library/ScoutBackup/FreeRoam.before/after-fall-swords.unity. No scene save/reload,
no commit/push. This supersedes previous dirty-scene status, not user saved edits.

## Previous: gradual, milder recline while both hands keep anchor aim

User liked feet-first flight but found entry too sudden and final pose too flat. Changed
ONLY dual flight body clips + presentation blending; no root/camera/motor/control changes.
`ScoutPlayerAnimation` now enters GasBoth/AttachedBoth with a **.75s native Animator
crossfade**, rather than.075s. Gas/coast visual swaps during this entry are deferred
until its fade finishes, then settle over.20s. Only the tiny pose swap waits: physical
gas/thrust/hooks and late independent hand IK update immediately. Landing, single-hook,
Fall, attacks, holds/death interrupt normally; dual release/backflip unchanged.

Gas hips now-62deg instead of-78; coast-56 instead of-70. With spine/chest, torso settles
~22deg(gas)/28deg(coast) above horizontal rather than almost flat. Both legs still extend
forward, boots lead, head remains looking ahead. Late IK still runs AFTER body animation,
pointing each arm/hand at its own actual anchor THROUGHOUT the entry, not a fixed clip aim.
Scoped RebuildFlight regenerates only those2 clips. Run/dual releases/single-hook poses
verified SHA256 unchanged. Unsaved scene before/after snapshots are byte-identical;
left dirty unpaused Edit Mode, no scene save/reload. No new commit/push.

Final suite **144/146 passed, same2 known Run failures**,2026-11-04 01:13:22–39 UTC,
PID28860,17.349s. New tests pass: entry duration/deferred gas policy, actual native
Animator20ms-step body continuity (<5deg step, still near upright at.1s), gas-off during
entry -> coast settle, both hand/arm aim errors<1deg throughout controlled transition,
root isolation. End-pose assertion intentionally updated for less-flat requested pose.
Run checks NOT weakened or repaired by overwriting approved/user1.1667s run work.
Changed runtime/test/clip files byte-match completed disposable project.

Native unpaused timed sky review: torso~21 ->32 ->39 ->53 ->68deg from upright over
~.75s, each hand aim error settles below.25deg by~.15s and essentially0 thereafter,
root/pivot angles0. Gas off at.2s continues body transition, settles to~62deg torso
at~1s, no restarting/snap. Controlled fixture with motors disabled, NOT normal-input
whole-game flight proof.27-frame previews:
- **ArtSource/Player/Scout/Scout_DualHook_Smooth_Recline.gif**
- **Scout_DualHook_Smooth_Recline_GasToggle.gif** (and matching end-pose PNGs)
- `tools/review_scout_recline.py [--gas-toggle]`; old dual-hook snapshot utility now
  waits long enough for the slow entry, rather than treating .35s as the final pose.

## Previous: feet-first dual-hook flight / no single-hook release recoil

User accepted the powered run, then requested: left/right hook releases return straight
to Fall (no recoil); dual-hook flight nearly supine with both feet leading; dual release
UNCHANGED. Implemented in `ScoutAnimationBrain` / scoped builder recipe:
- Last single-hook detach selects/stays in Fall until regrapple/landing/attack/hold/death;
  this is visual-only, even when still ascending. Partial dual detach keeps the remaining
  hook pose; near-simultaneous dual release still takes its original dual-release path.
- GasBoth/AttachedBoth recline hips~78/70deg back, extend both legs ahead with modest knee
  flex, and lift the head to look ahead. Independent actual-anchor arm IK retained.
- ReleaseLeft/ReleaseRight removed from generated22-state controller. Old standalone
  `.anim` files remain unreferenced for rollback, not used by runtime/controller.
- **Run, ReleaseBoth, ReleaseFlip, GasLeft/Right, AttachedLeft/Right byte-unchanged** from
  start-of-task snapshots. Rebuilt ONLY GasBoth/AttachedBoth, preserving unrelated states,
  parameters, model/material/prefab and authored assets. New menu **Tools > Player > Scout
  > Rebuild dual-hook flight poses only**. Full build still regenerates ALL owned clips;
  do NOT use it to update these poses over manually edited/retimed animation work.

Scene was ALREADY DIRTY at entry. Original unsaved scene preserved, NOT saved/reloaded.
Before/after save-AS-COPY backups in Library/ScoutBackup/FreeRoam.before/after-feet-first.unity
are byte-identical (1985763 bytes); remains dirty unpaused Edit Mode, not a clean-scene
claim. No Play fixtures/cameras/placements persisted. User may have retimed Run while
reviewing animations: current length**1.1666666s**, earlier recipe expected.56s. LEFT IT
UNTOUCHED, not silently overwritten to make old gait tests green.

Latest full validation **142/144 passed,2 FAILED**,2026-11-04 00:40:11–28 UTC,
PID38988,16.499s. All changed-behavior tests PASS (direct single fall, interruptions,
feet-first gas/coast geometry, independent anchor IK and dual release/flip). The failures
are existing `NarutoRunPlantsTheStanceFootAndTrailsArmsBehindForwardTorso` and
`NarutoRunLoopsWithoutBoneSnapAndCadenceMatchesAuthoredFootTravel`, whose old timing
assumptions no longer match the unchanged retimed Run. Do NOT report144/144, weaken
those checks, overwrite approved/user run work, or claim cadence is still calibrated.
Run timing/cadence reconciliation is separate work requiring user direction.

Native unpaused controlled pose review: both feet~2.76m(gas)/2.68m(coast) ahead of head,
left/right aim weights~1, root/pivot angle0; one remaining hook maintains attached pose;
last single release returns Fall with aim weights~0. Motor-disabled sky fixture, NOT
normal-input whole-map traversal proof. Preview **ArtSource/Player/Scout/
Scout_DualHook_FeetFirst_Unity.png**; utility `tools/review_scout_dual_hook.py`.
No motor/physics/root/camera authority changes. No new push/commit.

## Previous accepted power-stride revision — user feedback on tucked legs / flat soles

User found the previous gait still lacked rear extension and running power. Revised
LOWER BODY again (upper Naruto silhouette retained): asymmetric .22m front reach,
long rear drive to~.66m behind hips, nearly extended leg, high heel recovery, and rear
sole plane turning nearly parallel to the leaned torso. Not merely faster leg playback.

Support now30% per leg with a half-cycle side offset: **no double support**, clear
flight between each alternating step. Plant heel then roll around the toe; compensate
ankle forward/lift for that actual contact pivot instead of flattening the boot or
letting the toe skate. Hip compression -> extension -> flight rise follows the push.
Rear swing sole pitch~128deg matches the body plane at full extension, then recovers
before the next footstrike. This is authored flat-ground motion, not terrain IK.

Contact stroke .86m (1.72m at scene visual scale2), .56s authored cycle, and .30 stance
produce~10.238m/s at1x playback; cadence still follows measured movement with NO motor
changes. Actual W test in normal FreeRoam: **Run,grounded,true,speed15.00001,
Animator1.465117,hooksfalse**. The longer stroke avoids the old rapid bicycle shuffle.

**141/141 passed twice**,2026-11-04 00:08:00–15 UTC,PID10548,15.853s; and
00:09:54–00:10:11 UTC,PID13532,16.221s.71 code/art/clip/prefab files byte-matched.
Saved clean unpaused Edit Mode in FreeRoam, original root(0,0,0), capsule3.6 unchanged.
All original Titan/
ODM/attack assertions retained. Gait assertions deliberately revised for a rolling TOE
rather than a permanently flat/linearly moving ankle. Added strict120-phase checks:
no double-foot support, real flight, nominal floor nonpenetration, long rear reach,
leg extension ratio>.92 and rear-facing sole plane nearly parallel to torso. Existing
loop/cadence test measures actual toe travel through toe roll, not an arbitrary ratio.

Updated `Scout_NarutoRun.gif/png`; added **Scout_PowerRun_Rear.gif/png** in
ArtSource/Player/Scout. Side/rear views expose full rear extension and sole orientation.
Capture utility derives preview translation from the SAME stride constants (not old
hard-coded timing). Render-only translation, not root motion/gameplay recording. This
is another review revision, not user visual approval or all-terrain clipping approval.

## Previous attempt — Naruto-style run (user requested more leg power)

User requested a more natural Naruto-inspired ground walk/run. Run now leans~38 degrees
through pelvis/spine/chest, counterbalances head/neck to look ahead, and keeps nearly
straight arms and both blades trailing. Subtle shoulder/cape motion instead of arm pumping.
Replaced independent thigh/knee sine waves with a cyclic foot path + anatomical two-link
leg solve:42% support phase (even backwards foot travel),58% lifted recovery, heel settle,
push-off ankle lift/foot roll, early foot retraction and continuous loop endpoints.
Pelvis bob no longer carries the planted ankle off the nominal flat ground.

Cadence derives from actual .8m authored foot stroke, .56s cycle, stance fraction and
visual model scale instead of arbitrary speed/9. This changes Animator playback ONLY;
controller speed, gravity, inputs and attack/grapple priorities remain unchanged. Real W
input in ordinary FreeRoam (NO disabled-motor sky fixture) observed **Run,grounded=true,
15.00001m/s,Animator speed2.205,hooks=false**. Returned to original saved edit placement.

Two new tests cover actual sampled ankle contact/travel, torso lean/trailing hands,
recovery lift, loop continuity and speed matching measured from the clip. All137 earlier
retained. **139/139 passed twice**,2026-11-03 21:44:25–41 UTC (PID34668,16.059s)
and21:46:11–28 UTC (PID23208,16.890s). Final gait runtime/clip/test/prefab byte-matched.
Preview `ArtSource/Player/Scout/Scout_NarutoRun.gif` / `.png`, generated by
`tools/capture_scout_run.py`. GIF uses explicit temporary preview translation on a flat
render floor; NOT baked root motion or a claimed terrain/gameplay video. Still no
terrain-adaptive foot IK or comprehensive boot/gear clipping approval.

## Animation / gameplay boundary

`Assets/Player/Scout/ScoutAnimationBrain.cs`: presentation-only selection from grounded,
velocity, hook mask, gas, attack, hold and death state. No movement commands.
`ScoutPlayerAnimation.cs`: single explicit owner for the new visual Animator, plus
late arm IK before existing sword sweeps(order90 vs110). Animation root motion OFF.
Movement/ODM gain ONLY optional explicit visual-Animator binding + read-only hook/gas/
anchor getters; null keeps original behavior. Existing old bone writers stopped; old
mesh/trail renderers hidden, **not** legacy solid gear colliders/physics or camera.

- Ground idle, fast trailing-blade run (cadence follows observed planar speed), landing.
- Air rise/coast/fall; fall raises both blades overhead with leg counterbalance.
- Separate left/right/dual gas and attached-no-gas clips, mirrored single-arm release
  reactions and dual release. Hand IK targets EACH actual latched anchor independently.
  While aiming a missed/unattached hook, use intended camera ray target. Grounded and
  non-gassing aiming works too. Bounded shoulders/stable elbow poles, no400m arm reach.
- Stopping gas with hooks still attached coasts; removing one hook keeps remaining side.
- Full dual release / near-simultaneous detach can play a **.48s visual-only backflip**
  above speed4 with clear3.6m envelope. Whole envelope endpoint + travel sweep, ignore
  own hierarchy only; saturated queries fail closed. Obstructed/slow/grounded release
  uses ordinary release. Regrapple/landing/attack/hold/death interrupt; never control-lock.
  Flip pivot is beneath gameplay root and does NOT rotate camera/controller/velocity.
- Animated hip launch markers replace only rope visual emitters, matching new gear.
  Camera raycast selection, rope inputs, hook forces, gravity, boost and tuning unchanged.
- Held and release-pending motor suspension both yield aiming/flip to ownership. F sword
  counter remains available in held poses. Death wins. No new health pool or parenting.

## Full-body attack remake

Scene instance opt-in timing: **.075s anticipation +.12s active cut +.195s recovery =.39s**
after F release. Ground / air / held charge and strike clips, hips/chest counter-rotation,
sharp cut then follow-through; grounded bracing and aerial leg tuck/scissor instead of
frozen legs. Current controls still HOLD F to coil, RELEASE F to strike.

`Attack.cs` adds configurable timing, charging/attacking observations and release event.
Defaults remain original **0/.6/0**, preserving non-Scout players/scenes. OnDisable/death
clears flags/coroutine/window. Damage stays physical through existing shared
DamageDealer/TitanODMSwordSweep and per-swing region deduplication. New blade meshes,
not hilt/whole gear, are sweep sources; .035m blade radius unchanged. Input buffering,
combos, hitstop, particles, trails and new impact audio are not implemented here.

## Reversible scene opt-in

Saved gameplay scene, Edit Mode:
- **Tools > Player > Scout > Install on current gameplay player** (refuses duplicates).
- **Tools > Player > Scout > Restore legacy player visuals**.

Caches original Animator bindings, blade meshes, rope emitter transforms and attack
settings; legacy renderers/bone writers restored. Explicit prefab property overrides
persist hidden meshes, model scale/offset/layers and bindings after scene reload.
Backup `Library/ScoutBackup/FreeRoam.unity`. Restore/reinstall verified: original
CharacterAnimated Animator, .6s window, two original obj_48459721 blades; capsule3.6/.5,
moveSpeed15, ODM acceleration50/maxSpeed80 retained. Reopening saved Scout scene confirms
zero old hidden renderers enabled, modelscale2/offsetY-1.8, new launcher markers, .12s cut.
No global Physics/NavMesh settings, Titan rest rig/art/body prefab changed.

## Verification / evidence

All123 prior tests retained +14 Scout regressions: hook-side/gas/coast selection,
partial/near-simultaneous/full release, flip obstruction/slow fallback and interruptions,
held counter/death priority, clip root-motion isolation and actual skin/leg deformation,
actual weapon hand parenting, real damage-window lifecycle, legacy defaults, distinct
single release reactions, independent two-anchor IK and full swept flip-envelope gate.
**137/137 passed twice**, Unity2021.3.25f1,2026-11-03:
- **21:17:40–56 UTC**, PID27388,16.739s.
- **21:19:50–21:20:08 UTC**, PID34396,17.463s.
Final runtime/compacted assets:71 prototype/integration/Scout/test/art/clip/prefab files
plus6 legacy source files byte-matched the completed disposable project at handoff.

Live unpaused real scene controlled sky fixture (motors disabled for isolation): left,
right, dual aim weights~1 on correct arms; stopping gas stays AttachedBoth. Backflip
visually rotates while gameplay-root angle stays0. Real **Windows F press/release in
focused Unity Game view**, NOT direct coroutine invocation: latest attack age.156894s,
Animator(normalized.402293*clip.39)=.156894s, damage active, root angle0, no flip.
Invoking the coroutine from MCP outside PlayerLoop can defer Animator entry and is NOT
valid evidence of real-input attack timing; use the actual-key proof for synchronization.
This is animation/IK/input proof, NOT a new normal-ODM traversal or all-terrain approval.
Previous actual ODM physics/hold regressions still run unchanged.

- `ArtSource/Player/Scout/Scout_Front.png`, `Scout_Back.png`: Blender studio art previews.
- `Scout_Unity_Overview.png`: actual Unity poses + native live IK/flip/F cut.
- `Scout_AerialAttack.gif`: sampled authored .39s clip, preview pauses added before/after;
  not a full-speed gameplay recording or claimed hit/terrain verification.
- Temporary cameras, render textures, render settings and runtime placements removed /
  restored. Editor returned to saved unpaused Edit Mode in FreeRoam.

Remaining: user art/timing review, shoulder/facial/hand/clothing refinement, full-speed
rope-to-cut-to-regrapple polish, foot planting/terrain adaptation, whole-human/gear/cape
clearance, held-counter balance, proper human ragdoll, clip hand-authoring/secondary
motion, audiovisual impact and performance profiling. Do not call this final mocap,
physics cloth, articulated finger animation or comprehensive collision-free traversal.
