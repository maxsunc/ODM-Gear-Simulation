# Titan prototype validation

LATEST real Scout feeding/chomp ragdoll/blood: **222/224 PASS TWICE**,same2 Run fails.
Unity2021.3.25f1,2026-11-05 **03:20:07–27UTC** PID5752/20.4350115s and
**03:22:24–45UTC** PID14044/20.6246924s.13 added cases: both ground hands/insertion/
exact-pose one corpse/one blood,nonlethal,escape,blocked mouth,13-body real corpse
stability/anchors,auto adapter uniqueness/passive live rig,unsafe world birth rejects,
3x/5x mirrored aerial feeds/same-grip invariants/arm continuity+mouth clearance,guarded
native graphics. Corpse max1.5s17.55m/s (<40),anchor<.002;new3x/5x arm6.581deg120Hz
(<8),mouth/carry penetration0 (<.005,existing .9s-onward window). Early reach overlap
logged separately, not claimed clear or fixed; no tolerance weakened. Need separate
full-speed ODM/scaled ground review. Original gameplay/physics tests retained.
Graphics OWNED3x runtime URP **1/1**,**03:22:09–10UTC**1.1057197s, actual body/teeth/
red-blood pixel check; raw800px/3-panel under ArtSource/Titan/Combat. NOT ordinary map
footage. See docs/titan-scout-bite-presentation.md. No main Editor Play/scene/input/save
or global Physics changes, no art/nav/settings changes. Follow-up after12901b9c;
user subsequently authorized commit and branch push (see latest work checkpoint).

Previous customizable ballistic jumping: **209/211 PASSED,same2 Run failures**.
Unity2021.3.25f1,2026-11-05 **02:03:56–02:04:15UTC**,PID39640,19.1112139s.
8 additional native CCT tests cover3 height/gravity/apex/landing variants,physics delay/
zero delay/cooldown/duplicates,air-trigger-wall rejection,walk-off-windup cancellation,
real ceiling,gas/disable/hold/death takeover,invalid height/zero gravity/settings,same
apex at.01/.02/.05 physics steps and extreme25000g real ceiling sweep. Previous
legacy-jump test adapted to ballistic jump retaining its post-jump gravity assertion.
First208/211 had a NEW fixture baseline error (spawn within native skin); settle
FROM ABOVE, unchanged .02 landing/.035 apex tolerances, final only2 known Run fails.
No global timestep/Physics writes or main scene/input/Play automation.3 source/test
files byte-match run. User currently saved gravity16 and jump4/.1/.15, preserved.
See docs/player-jumping.md; all preceding dirty work remains local afterb6447972.

Previous functional player gravity: **201/203 PASSED,same2 Run failures**.
Unity2021.3.25f1,2026-11-04 **22:31:17–22:31:36UTC**,PID52448,18.2195347s.
8 new real native CCT cases in TitanODMIntegrationPlayModeTests: live Inspector
acceleration/displacement;zero rest/coast;configurable terminal/extreme25000/invalid
runtime values;native landing/ledge fresh speed;upward release same gravity/powered
reset;held/disabled no integration;completed legacy jump no sticky multiplier;
native ceiling -> falling. All8 pass; all prior strict Titan/runtime/spawner cases
retained.3 changed code/test files byte-match full validation. See docs/player-gravity.md.
User Movement formatting and dirty scene preserved; current saved gravityConstant25000
untouched (warned to lower). No main scene/Play/input/save/global Physics changes.
Still local/uncommitted afterb6447972. Scoped actual falling/release fix; existing
scripted jump ascent/controls/thrust/inertia untouched, not ballistic jump redesign.

Previous random spawning / arrival strike: **193/195 PASSED,same2 Run failures**.
Unity2021.3.25f1,2026-11-04 **21:43:58–21:44:18 UTC**,PID3784,19.584s;
previous193/19521:40:22–42PID17444.12 new cases: weighted size/seed/global-RNG,
random walkable proposals/shared unchanged nav, staged inactive/full-clearance spawn,
player/blocker/cap cancellation/no fake impact, owned-only population/corpse lifecycle,
bounded FX/audio cleanup/no physics/time/root writes, weighted sizes in next bake,
bounded impossible placement and full-arm inactive reservation, shader asset import.
Graphics-enabled owned runtime URP separate **1/1 pass**,21:44:29–30UTC,.998s:
actual lightning-above-head pixel check plus warning/strike/steam frames. No main
scene/pipeline/Play automation; capture pipeline setup/restore ONLY owned project.
Details docs/titan-random-spawning.md. Installation is explicit user menu in Edit Mode
(MCP not connected); no production bake/main scene save. User clip unassigned.
Current main fixtures/rig/health/strict thresholds not weakened; only original2 Run
assertions fail. New code remains uncommitted after pushedb6447972.

Previous reusable runtime Titan registration: **181/183 PASSED TWICE,same2 Run failures**.
Unity2021.3.25f1,2026-11-04 **15:27:52–15:28:09 UTC**,PID19600,16.665s;
**15:30:27–44 UTC**,PID47984,16.912s.14 new runtime tests all pass: late/plain/placed
clones without list,3x/5x profile/scale-once, unchanged shared nav triangulation,
atomic spawn/despawn/crowd/offmesh/uniform scale, wide actual arm obstruction,
duplicate/foreign owner/scene guards, HP retained, actor/manager/nav/combat toggle,
inactive explicit policy, future bake-size union, changed initialized scale rejected,
actual spawned director40-damage stomp, genuine grab contact then despawn releases
victim, no corpse resurrection. Runtime/Editor bake UI compiled;178-file byte match.
Saved scenes/prefabs/art/nav/settings unchanged from pushedb6447972. No live editor
Play/scene automation or production bake. New runtime code is uncommitted review work.
See docs/titan-runtime-initialization.md; full-speed giant combat remains separate.

Previous regional sword health / effects / sever impulse: **167/169 PASSED,same2 Run failures**,
Unity2021.3.25f1,2026-11-04 **03:51:08–27 UTC**,PID46464,19.099s. Previous167/169
03:47:22–39,PID50212,17.019s.12 new cases: regional/nape/face HP boundaries and
recovery, capped real speed/teleport/held suppression, confirmed per-region dedup and
outcome colors, bounded pool/cut-only trails/real asset refs/cleanup, render-only
projection restore/live FOV, coherent branch launch all4 limbs x1/3/5 scale.
Sever max11.16–11.38m/s in1s120Hz freefall, no pose snap; unchanged40m/s bound and
solver12/4/angular4; all existing strict Titan continuity/torso/death checks pass.
Legacy fixtures intentionally retain old damage profile; production FreeRoam opts
into1000arm/2500leg/500face/500nape. No weakened Run or physics assertions.
Actual F/actual cutting mesh sky fixture arm1000->750->500->250->0, white normal and
yellow sever, one receipt/swing, native GUI/blood/trail screenshot. Native nape/face
review not completed, automated evidence only. NOT full ODM traversal proof.
New effects/audio included in disposable runner. Details docs/titan-sword-damage-effects.md.
Main scene setup remains unsaved while user runs Play: don't stop/overwrite session;
save FreeRoam after exiting Play. No new commit/push.

Previous opposite-side BOTH-arm wind-up: **155/157 PASSED,same2 Run failures**,
Unity2021.3.25f1,2026-11-04 **02:29:24–40 UTC**,PID33772,16.721s. Three new cases
Ground/Air/Held: both hands on load side opposite slash, charge matches released
attack at0/.03/.06s, early arc preserves side, both finish other side. Near125/cross60
elbow pose assertions intentionally replace old symmetric both125 expectation; no
strict Titan check weakened. Exact matching initially caught native quaternion
tangent drift; authoring corrected instead of loosening test. Native F charge AND
release checked both variants, correct hand-side signs/consistent selection, active
window/time sync. Controlled sky fixture NOT full ODM combat proof. Runtime/test/all
clips/controller byte-match. Noncombat/controller hashes unchanged; scene copies
identical/clean Edit Mode. Work uncommitted after approved059dd52e. See updated
Directional_Windup / ToLeft / ToRight previews, old load-named GIFs historical.

Previous mirrored curled-neck/arc/full-extension attacks: **152/154 PASSED,same2 Run failures**,
Unity2021.3.25f1,2026-11-04 **02:10:30–47 UTC**,PID14608,16.607s. New passing cases:
chamber/actual steel arc/both-arm extension inGround/Air/Held, whole-body mirror<.005m,
seeded stable per-charge variation/global RNG unchanged; single charge-start event.
Native actual F input both sides agrees with physical attack timeline: right.14564s,
left.13447s, normalizedTime*.39 matches age, damage active, same charged/released side,
hand IK0/root/pivot0. Controlled motor-suspended sky fixture NOT full ODM combat proof.
Runtime/test/all clips/controller byte-match. All noncombat clips/metadata hashes
unchanged. Clean scene before/after copies identical, no scene save/reload. New attack
work uncommitted after approved Scout checkpoint059dd52e; checkpoint local/no push.
Same2 Run timing failures against unchanged user clip remain, no weakened assertions.

Previous gradual/tunable Fall entry: **147/149 PASSED,same2 Run failures**,
Unity2021.3.25f1,2026-11-04 **01:46:00–17 UTC**,PID45036,16.833s. New passing tests:
Inspector fallBlendDuration default.5/custom.85/min.05 and unchanged priority timings;
actual native Animator entry from8 previous animation poses, no instant bone jump,
still visibly blending at.1s and completes overhead-sword/downward-gaze pose. Runtime/
test bytes match; every clip/controller/meta unchanged. Native timed single-hook ->Fall
review hand heights progress~.4/1.3/1.9/2.09m over~.5s, root/pivot0, controlled motor-
suspended fixture NOT ordinary-input flight approval. Clean Editor scene preserved,
COPY snapshots differ only by new default serialized fallBlendDuration line, no save/
reload of main scene. Same approved/user Run timing failures remain unmasked.

Previous overhead crossed-blade/downward-gaze Fall: **145/147 PASSED,same2 Run failures**,
Unity2021.3.25f1,2026-11-04 **01:36:15–32 UTC**,PID40012,17.191s. New actual-mesh
Fall test samples whole loop: each cutting mesh<20deg from up, above head, tips cross
slightly in front projection with separate depth, head gaze down/root bindings isolated.
Corrected near-meeting tips to actual crossing, no weakened assertion. All other clips/
controller/meta SHA256 unchanged, notably run/dual flight/releases. Scoped Fall rebuild;
no runtime controls/IK/motor/root changes. Native static front/profile proof not actual
input gameplay approval. Clean scene preserved with identical before/after copies.
Known2 run timing failures remain against retimed approved/user clip, not hidden.

Previous gradual/milder dual recline: **144/146 PASSED,same2 Run failures**,
Unity2021.3.25f1,2026-11-04 **01:13:22–39 UTC**,PID28860,17.349s. New passing tests:
.75s dual entry/deferred gas-to-coast policy; actual native Animator20ms-step continuity
(no body snap), hands independently tracking anchors through changing torso and gas
switch, root isolation. End-pose assertions updated for requested less-flat22/28deg
above-horizontal torso. Native controlled sky sequence agrees, hand aim~0 after~.15s,
gas off does not interrupt/restart entry. No physics/motor/root changes. Run, dual release,
single-hook clips byte-unchanged; dirty scene before/after copies identical; changed
runtime/test/clip files byte-matched. Same known Run timing checks still fail against
unchanged1.1667s clip, not masked or overwritten. See implementation doc / timed GIFs.

Previous feet-first dual-flight / direct single detach Fall: **142/144 PASSED,2 FAILED**,
Unity2021.3.25f1,2026-11-04 **00:40:11–28 UTC**,PID38988,16.499s. All changed-feature
checks pass: feet lead head/body reclined for GasBoth/AttachedBoth, no single recoil,
persistent Fall + interrupt priorities, original dual releases/near-simultaneous flip,
independent actual-anchor IK/root isolation. Runtime/changed clip/controller/test byte-match.
Two existing gait failures: NarutoRunPlantsTheStanceFootAndTrailsArmsBehindForwardTorso,
NarutoRunLoopsWithoutBoneSnapAndCadenceMatchesAuthoredFootTravel. Run clip unchanged
this task but now1.1666666s vs old recipe.56s; user/approved retiming left UNTOUCHED.
Do not weaken checks or overwrite run to hide mismatch; cadence reconciliation separate.
Run + dual releases + single-hook flight SHA256 unchanged. Dirty scene preserved exactly
(before/after unsaved snapshot byte comparison), no scene save/reload. Native controlled
pose proof is not normal-input traversal proof. See scout-player-implementation.md.

Previous powered rear-stride revision: **141 passed /0 failed twice**, Unity2021.3.25f1,
2026-11-04 **00:08:00–15 UTC**,PID10548,15.853s; **00:09:54–00:10:11 UTC**,PID13532,
16.221s.71 code/art/clip/prefab files byte-matched. No changed Titan/ODM/attack limits.
Added rear reach/extended leg/rear sole parallel-to-body checks and120-phase sole
contact/floor clearance: max one foot touching, real flight, each side has support.
Existing gait checks now inspect flat early support + rolling TOE travel, not a forced
flat ankle throughout push-off. Real normal W: Run/grounded,speed15.00001,Animator1.465117,
hooksfalse, motors enabled/unmodified. Tests are not user visual approval or terrain IK.

Previous Naruto ground-run refinement: **139 passed /0 failed twice**, Unity2021.3.25f1,
2026-11-03 **21:44:25–41 UTC**, PID34668,16.059s; **21:46:11–28 UTC**,PID23208,16.890s.
Final gait runtime/clip/test/prefab byte-matched the completed validation project. All137 retained +2 actual sampled-gait
regressions: support ankle height/even backwards travel, leaned torso/trailing hands,
recovery clearance, loop bone continuity, and measured clip foot travel vs runtime
cadence. Normal actual W input: Run/grounded,15.00001m/s,Animator2.205,hooksfalse; no
sky fixture/motor disabling. Motor speed/gravity/controls unchanged. Preview translation
is render-only, not gameplay root curves or terrain-foot-IK proof. See implementation doc.

Previous Scout/player first implementation: **137 passed /0 failed twice**, Unity
**2021.3.25f1**,2026-11-03 **21:17:40–56 UTC** (PID27388,16.739s) and
**21:19:50–21:20:08 UTC** (PID34396,17.463s). All123 prior retained +14 new state,
IK, real skin/leg deformation, root-isolation, distinct-release, swept flip-envelope,
physical-blade parenting and opt-in attack-timing/lifecycle regressions. Final compacted
clip channels (5.69e-8 sampled max delta, no intentional pose change). No weakened
Titan continuity/physics/contact assertions. Live native actual F input synchronized
age.156894 with normalized.402293*.39, damage active, root angle0. Other sky-fixture
IK/flip evidence is controlled animation review, NOT normal ODM traversal approval.
See `scout-player-implementation.md`. New player work uncommitted; full previous
checkpoint16517e6c. Shared Player prefab/Shiganshina/Titan assets/global physics unchanged.

Previous FreeRoam full-map navigation experiment: **123 passed /0 failed twice**, Unity
**2021.3.25f1**, **2026-11-03 19:03:46–19:04:02 UTC** (PID21108,16.006s) and
**19:05:38–54 UTC** (PID26368,16.248s). All116 prior +7 physical-source/terrain/
500m ramp-cover path/size-specific16m gap/exactly-once avoidance/owned-data tests.
No weakened original physics, continuity or contact assertions.
Live full-map338-source bake, two3x/5x profiles with stable IDs/GUIDs on re-bake.
Native complete paths1409m/1418m; actual actors travelled>1.3km. Corrected double-
scaled avoidance; final distinct-destination native arrival147m/205m, remaining1.87m/0,
both onMesh/PathComplete/velocity0. User5x original spawn intersects mediumTree193,
so validation relocation only in Play Mode; saved actor positions/scales preserved.
Details/known limits: `titan-freeroam-navigation.md`.
Checkpoint0f371f2c committed prior integration; navigation checkpoint committed at the
user's next request before scout character/animation planning (see Git log). No new push.

Previous real-ODM/Shiganshina integration: **116 passed /0 failed twice**, Unity
**2021.3.25f1**, **2026-11-03 15:52:12–27 UTC** (PID40516) and
**15:55:56–15:56:11 UTC** (PID34836,15.561s). All108 approved checks +8 actual
legacy/default-assembly integration/native-CCT/sword regressions. Strict<8deg/120Hz
and<40m/s unchanged: fast6.45deg,moving6.64deg; torso0; aerial death11.67m/s;
intact10.86m/s,gap.003m,hipsY1.03m. Real CCT world-UP axis verified against native
bounds under45deg root tilt, including rotated local center and blocked headroom.

Saved requested scene also exercised through MCP: actual gear Throttle/FaceGrapplePoint,
not sandbox flight; native.02s, one physical catch(.18s after selection,root tilt8.61deg),
one contact bite, actual PlayerHealth/bridge0, hooks disconnected, no parent/pending
release. Separate true3s scene reload produced HP100, enabled controller, two Titans
and rebuilt nav. Actual sword sweep crosses zones and deduplicates limb in new tests.
Owned navigation patch256x256m; full-map/interactive/full-body carry polish remains.
Proof `ArtSource/Titan/Combat/Shiganshina_ODM_Integration_MCP.png`.
Approved aerial snapshot940daf1d PUSHED; new scene integration uncommitted for review.
Full contracts/limits: `titan-shiganshina-integration.md`.

Previous aerial torso-clearance revision: **108 passed /0 failed twice**, Unity
**2021.3.25f1**, **2026-11-03 14:40:20–36 UTC** (PID34496) and
**14:44:01–16 UTC** (PID37808,15.493s). All106 prior checks retained. New both-hand/
two-phase carry test asserts actual forearm/palm colliders stay out of chest/spine
(<.005m), and exact local grip point/radius stay fixed through owner-controlled
upright yaw. Moving fast catch/bite now also asserts clearance. A second test blocks
long-tick bites with scenery on the forward carry arc. Both static/moving measured0.
Shared shoulder attachment is intentionally not required to separate.

MCP reproduced old forearm penetration1.43m/palm1.32m and viewed corrected close
transfer/mouth frames. Final forearm/palm0, one physical capture/one bite, HP0,
release complete; catch still.175s after selection, max6.18deg/120Hz for all3 arm bones.
Proof: `ArtSource/Titan/Combat/AerialMouthClearance_Comparison.png`. Forward/outward
elbow, front clearance arc and yaw around the existing grip replace the clipping
world-fixed-offset path. Bite sphere stays.55m, arm lengths/rig unchanged. Ground
follow/timings unchanged. Release includes orbital movement. Full body/terrain and
real ODM/controller/health integration remain pending; this is still an upright proxy.

Strict old thresholds retained: fast6.68deg, moving6.31deg, legacy-timing5.32deg
(all<8deg/120Hz). Aerial loaded-mouth death10.83m/s (<40); intact11.26m/s,gap.007m,
hipsY.86m; ground mouth10.91/10.36/11.82/11.81m/s. No global physics/joint/mass changes,
velocity caps or relaxed assertions. Editor returned clean Edit Mode, no scene saved.

Previous fast/no-warning aerial revision: **106 passed /0 failed twice**, Unity
**2021.3.25f1**, **2026-11-03 14:02:10–26 UTC** (PID31208) and
**14:04:21–38 UTC** (PID30596). User explicitly requested removing the aerial warning
arm and increasing speed/frequency. Default0s windup/.22s direct reach,.25s miss
recovery/.65s cooldown; aerial attempts bypass ground decision delay/stationary gates.
Physical contact, locked aim, exclusive ownership and injury/death handoff unchanged.

All101 prior checks +5 new: current-palm start/no warning lift/ring, quick valid
capture, fast dodge, ~1s retry, moving catch/bite continuity and ground-delay bypass.
Explicit old timeline tests retain configurable.7/.3 timing but follow the new direct
path. A currently reachable flyer is now eligible despite a full forecast leaving
range, not remotely captured. The CCD crossing setup samples the actual current hand
path rather than the removed raised pose. No physics or motion assertion weakened.

Transported anatomical hinge plane + cached exact early-catch rotations prevent roll
preparation and double-blending a partial caught pose (12.61deg regression before fix).
Latest<8deg/120Hz: fast static6.43deg, fast moving6.48deg; old timing4.86deg.
Static contact.158s. Aerial loaded-mouth death8.31m/s, strict<40 unchanged; intact
11.14m/s,gap.005m,hipsY.84m. Ground mouth10.46/10.36/11.82/11.81m/s.
MCP real moving upright CCT replay: one new catch/one bite, HP0, release complete,
contact.175s after selection, all3 selected arm bones max6.18deg through recovery.
Reviewed0/100/220ms and mouth frames; proof
`ArtSource/Titan/Combat/AerialGrab_DirectReach_MCP.png`. Editor returned to unpaused,
clean Edit Mode; no scene saved. Real ODM adapter/production animation still pending.

Previous aerial lab baseline: **101 passed /0 failed twice**, Unity **2021.3.25f1**,
2026-11-03 **04:24:11–26 UTC** (PID9552) and **04:26:31–46 UTC** (PID29000).
All74 approved checks +27 new, including above-head/side/low reachable poses,
actual rotating hand-box/capsule CCD, corner contact, fast relative crossings,
fixed aim/dodge, missing motor, cover/masks/saturation, independent escape/lease,
actual early-capture lease age, blocked-release recovery and target-component
removal, selected/other-arm and leg/eye/death ownership, bite, stationary/moving
full-sequence continuity and saved-scene automatic interception/reset.

Aerial continuity<8deg/120Hz: stationary **6.39deg**, moving **5.64deg**. MCP found a
moving shoulder-roll singularity missed by stationary checks (90deg live and
123.67deg regression); fixed aerial pole and animation-base offset iterations.
Final MCP live moving capture/carry/mouth/bite: one catch/one bite, HP0, no blocked
release, upper-arm max4.30deg. Sampled pose proof:
`ArtSource/Titan/Combat/AerialGrab_MCP.png`. This is a controller PROXY, not real ODM.
Aerial loaded-mouth death **12.79m/s**; ground mouth10.47/10.36/11.82/11.81m/s;
intact11.04m/s,gap.005m,hipsY.84m. Strict<40m/s unchanged; no velocity caps, angular
range/mass/solver/global-physics changes. See [aerial contract/limits](titan-aerial-grab.md).
User approved the previous ground art/motion and committed it as28cb997a (local,
not pushed); aerial work is separate/uncommitted. Real gear/controller/health and
production animation remain pending. Working Editor returned to clean Edit Mode.

Previous ground arm-continuity fix: **74 passed /0 failed twice**, Unity **2021.3.25f1**,
2026-11-03 **02:44:07–20 UTC** and **02:49:24–37 UTC**. The new both-hand/four-phase
120Hz rotation/jaw test reproduced137.26deg jumps with the prior code (73 others
still passed), then measured **2.26deg** after stable IK frames and non-accumulating
jaw rotation. Assertions remain<8deg/sample and jaw<28.1deg. MCP reproduced the
failure in the working Editor and captured/reviewed both fixed sides: no adjacent
pose flip, .35m hand/mouth gap. See `titan-bite-combat.md` and the comparison image
at `ArtSource/Titan/Combat/GrabArmContinuity_Comparison.png`. Mouth-phase death
maxima10.57/10.36/11.82/11.81m/s; intact11.06m/s,gap.005m,hipsY.84m. No physics tuning
or damage telegraphs changed. All73 previous checks pass; real-player/aerial work
and production animation remain pending. Editor returned to clean, unpaused Edit Mode.

Previous ground-bite validation: **73 passed /0 failed twice**, Unity **2021.3.25f1**,
**2026-11-03 01:50:19–32 UTC** and **01:52:14–28 UTC**, in the owned disposable project. All58 previous checks
plus15 additions cover actual arm-to-mouth reach with either hand, disabled shape
contact and rounded controllers, late escape/cancellation, cover/carry obstruction,
latched timing/damage, lease expiry vs long ticks, exact-once bites, callback death
handoff, foot support, four mouth-phase ragdolls, normal-frame automatic playback,
and saved-lab lethal bite/reset. Existing AI pause/reset tests now reach mouth windup.
See [ground bite](titan-bite-combat.md). No rig, physics settings or assertion limits
changed. Mouth-phase death maxima10.29/10.09/11.27/11.04m/s (four5s simulations);
latest intact10.98m/s,gap.005m,hipsY.84m. Live visual/art review and real-player/aerial
integration remain pending.

Previous grab validation: **58 passed /0 failed twice**, Unity **2021.3.25f1**, **2026-11-03
01:04:02–11 UTC** and **01:05:48–57 UTC**. The
[grab/hold/release slice](titan-grab-combat.md) adds18 checks to the40-test stomp
baseline. They cover fixed timing/contact, no homing, exclusive/bounded ownership,
escape and lifecycle/limb/eye/death release, physics-state restoration, cover,
rounded-controller geometry (also for stomp), foot support, AI/director arbitration
without gameplay RNG consumption, resetting the actual saved lab while held, and
not consuming pre-selection frame time during a newly started automatic attack.
Four crouch/carry death cases max7.45/7.83/11.35/10.41m/s; current pose preserved.
All40 previous checks still pass, including8 loaded-stomp deaths. Latest intact
max10.95m/s,gap.006m,hipsY.85m. Live visual review/real-player adapter remain pending.

Earlier stomp validation: **40 passed /0 failed twice**, Unity **2021.3.25f1**, **2026-11-02
23:21:38–47 UTC** and **23:24:32–40 UTC**. The sandbox-only
[stomp phase](titan-stomp-combat.md) adds12 tests to the28-test snapshot:
readable fixed timing, exact-once/long-frame contact, dodge/no homing, cover,
compound/AoE/airborne filtering, cancellation, mirrored foot support through recovery,
owner-checked navigation locking, an unforced pose handoff and8 death phase/foot cases.

The matrix exposed unstable hard angular stops during contacting folded poses.
Mass-proportional compliant/damped stops fix those cases without changing masses,
authored angular ranges, solver settings or the existing angular speed cap. No
linear speed cap/global physics setting/loosened assertion was used. Current-pose
positional joint anchors are also recomputed after animated spine/shoulder movement.
Loaded-stomp death max12.01–12.27m/s across8 cases; unforced handoff.000113m/s.
Latest intact max10.89m/s, joint gap.004m,hipsY.86; injured prone5.06/kneel11.64m/s.
Live stomp visual review and real-player integration remain pending.

Earlier motion validation: **28 passed / 0 failed**, **2026-11-02 04:00:16–24 UTC**, Unity
**2021.3.25f1**. The [motion overhaul](titan-motion-overhaul.md) adds7 motion checks:
velocity-matched walk/run, seeded variation, visible sole support and loop seams,
head-led pivots, fast impact collapses, pose/phase-preserving cuts and quick eye recoil.
All21 previous tests still pass. Normal-physics alignment now permits deliberate
bounded hip contact/bob motion while continuing to catch stale physics transform drift.
Latest intact ragdoll max speed12.54m/s, final joint gap.004m, hipsY.95m;
injured death max speeds5.89m/s (prone) and9.57m/s (kneeling).

Earlier injury validation: **21 passed / 0 failed**, **2026-11-02 02:52:40–49 UTC**, Unity
**2021.3.25f1**, including all previous checks plus seven
[leg-loss injury animation checks](titan-injury-animation.md).
Single-leg mirrored kneeling, second-leg prone collapse, current severed pose
preservation, floor clearance, blindness recovery and injured-pose death all pass.
Five-second injury-death max speeds: kneeling **9.94m/s**, prone collapse **9.72m/s**.
Latest intact death: max speed **11.09m/s**, final joint gap **.003m**, hipsY **.79m**.
Kneeling and prone poses were also visually checked in Unity.

Earlier navigation validation: **14 passed / 0 failed**, **2026-11-02 02:07:19–27 UTC**, Unity
**2021.3.25f1**, including the six foundation checks and eight new
[patrol/sight/chase and scene-integration checks](titan-patrol-chase.md).
Normal-physics locomotion now verifies skeleton/root alignment as well as navigation
root movement. Kinematic animated bones no longer use stale physics interpolation.
Latest ragdoll metrics: max speed **11.37m/s**, final joint gap **0.007m**, hipsY **0.79m**.

Earlier reference-art validation: **6 passed / 0 failed**, **2026-11-02 01:34:58 UTC**, Unity
**2021.3.25f1**, using the [reference-led model](reference-titan-model.md).

15 closed skins, 22 identical rest bones, 78,888 triangles, reference-sized box
colliders and adjusted head/eye/nape/mouth placement. All six tests listed below
pass on this replacement, including UV identity and detached-leg orientation.
Five-second ragdoll metrics: max speed **11.37m/s**, final max joint gap **0.004m**,
hips height **0.80m**. Actual Unity idle render also visually checked.

Earlier validation history follows.

## Blender geometry

- 15 skinned mesh sections / 22 bones.
- All 15 source meshes checked with BMesh: **zero open boundary edges**.
- Original Blender scene objects were not deleted; generated content is in `TitanPrototype_v1`.
- Source and render: `ArtSource/Titan/`.

## Unity structural checks — PASS

- 17 rigidbodies, 16 connected CharacterJoints.
- Four detachable branches, each with three bodies, three skins and three damage triggers.
- Every detached skin has only its own branch's weighted bones (unused FBX skeleton references trimmed).
- 14 total damage zones; face/nape volumes compensate for FBX bone scale.
- Native Generic idle/walk clips and Animator controller.
- Mouth and left/right grip sockets.
- Existing gameplay scene was not replaced or saved by the builder.

## Runtime PlayMode suite — 4 passed / 0 failed

Executed using `tools/run_titan_validation.py` in a disposable project, not the open
working project. Result XML timestamp: **2026-11-01 22:38:22–23 UTC**.

1. `IntactBodyHasWorkingAnimationAndKinematicPhysics` — PASS
2. `EyeStunRecoversAndOnlySurvivingHandsPose` — PASS
3. `AllFourBranchesDetachTheirOwnSkinsAndIgnoreRepeatHits` — PASS
4. `NapeKillSwitchesIntactBodyToStableRagdoll` — PASS

The final test manually advances a local runtime PhysX scene for **250 × 0.02s**:

- Maximum body linear speed: **17.98 m/s** (right upper leg, step 44).
- Maximum final joint-anchor gap: **0.016 m**.
- Final hips height: **1.10 m** above the floor.
- No invalid positions, floor tunneling, or internal ragdoll collision pairs.
- Animator disabled and all surviving bodies dynamic after the nape hit.

Earlier runs caught excessive motion from the animated-to-physics transition.
The body now clears kinematic interpolation/velocity state, rebases joint connections
at the current pose, reapplies internal collision exclusions, and uses giant-scale
angular damping. Shoulder limits include the resting arm pose rather than forcing
arms back toward the FBX T-pose.

## Crash and test isolation

An earlier Editor-side test crashed Unity 2021 in `ClosePreviewScene` while cleaning
up Animator bindings after modifying the skeleton. That implementation was removed.
Runtime tests now use proper PlayMode scene lifecycle in a separate disposable
project. No physics test runs automatically on import in the user's Editor.

## Follow-up: detached leg orientation — 5 passed / 0 failed

Validated again at **2026-11-01 22:58:41 UTC** with an additional regression test:
`SeveredLegsKeepTheirAnimatedPoseWhenAnimatorRebinds`.

The regression reproduced a **178.25-degree** left-thigh flip immediately at the cut.
`Animator.Rebind` was writing cached default LOCAL transforms after the branch had
been given a new parent. Detachment now preserves the complete detached hierarchy's
local positions, rotations and scales through the rebind, then activates physics.
No hard-coded 180-degree correction is used.

The new test checks both legs on a rotated Titan, compares all three bone world
poses and every baked skin vertex before/after cutting, and verifies that another
Animator frame does not rotate the removed branch. All five PlayMode tests pass.

## Detailed-model replacement — 6 passed / 0 failed

Validated at **2026-11-01 23:43:56 UTC** after replacing the active FBX with the
[detailed anatomical model](detailed-titan-model.md): 15 skins, 22 identical rest
bones, 165,304 triangles and three baked 2K PBR atlas maps.

Added `ImportedSkinsMatchSourceMeshGeometryAndUvs`, comparing the generated mesh
buffers against the imported FBX. The authoring tool now copies native geometry,
indices and UV data explicitly when replacing existing generated mesh assets.
All six runtime tests pass, including the preserved-leg-orientation regression.

Five-second ragdoll metrics: max speed **16.85m/s**, final maximum joint gap
**0.024m**, hips height **1.11m**. A separate actual Unity lab-camera capture was
checked for the correct detailed geometry and material/UV mapping.

## Not yet verified / not implemented

- Human visual approval of proportions, deformation and ragdoll appearance.
- Actual player sword inputs, high-speed swept hits, and momentum tuning in gameplay.
- Production-level navigation and real-player combat. Lab navigation/sight/pursuit
  stomp, standing grab/hold/release and ground hand-to-mouth/contact bite are tested
  above; aerial grabs, consumption art/respawn and actual player integration remain undone.
- Held-player ownership, sever-to-release behavior and combat escape sequences.
- Crawl behavior after losing legs, limb regeneration, sounds, effects and performance variants.

The lab buttons validate body APIs, not a finished enemy encounter.
