# Titan checkpoint — compact continuation context

## User goal / current work

Build a modular Titan for the existing ODM/sword game (Unity **2021.3.25f1**, never
upgrade). Art, rig, detachable limbs and ragdoll are established. The user accepted
the reference-inspired revision, then approved **patrol → sight → chase** in an
isolated sandbox. That phase is implemented. User then requested a **single-leg
kneeling collapse with head down, and helpless prone collapse when both legs are
gone**, before proceeding to attacks. That phase is complete. User then requested
**faster, less robotic, less predictable motion across all animations** and approved
an overhaul. **That motion overhaul is now implemented and validated.**

User previously requested conversation compaction; this file preserves the working
state. No compaction command was executed by the assistant.

## Current: full-map FreeRoam navigation TEST ready; integration checkpoint committed

**0f371f2c** committed the prior actual ODM integration + user's3x/5x FreeRoam Titans
and138m/180deg sight defaults. Local only, no new push requested. NEW navigation
experiment is now checkpointed at the user's request before switching to player/scout
model, swords and animation planning. No new push requested; unrelated dirty tree still
excluded. Navigation commit follows0f371f2c (see Git log). No scout modeling begun yet.

Active scene **Assets/Scenes/FreeRoam.unity**, saved setup object
**FreeRoam Titan navigation TEST**. Editor-only full-map bake,338 actual physical
sources (332 MeshColliders), no mesh import/readability edits, no fake floor.
Bounds~2122x908x1595m including full2km map. Two owned baked assets in
`Assets/Navigation/Titans/FreeRoam/`: world radius5.4/height36 for3x and9/60 for5x.
Two named authored agent types added; original Humanoid0 radius.5/height2 unchanged.
Stable IDs/GUIDs on re-bake verified, type count3 before/after. Canonical.001 scales.
Native type IDs may be NEGATIVE: only-1 is missing. Profiles(-1372625422,-334000983).

New core: `TitanNavigationGeometry` physical-source collection (actual TerrainCollider
fallback for local-physics runtime tests), `TitanWorldNavigation` owned asset registration.
New default-assembly `TitanFreeRoamIntegration`; explicit Editor menu builder
`TitanFreeRoamNavigationBuilder`. **Enable Combat=false** for nav testing; toggle in
Edit Mode for combat. Sight Range138/FOV180/Patrol Radius200 editable on setup Inspector.
Original player controls unchanged. Health/motor/sword bridge wired scene-only.
Shared prefabs/rest rig/art/Physics settings not edited. Existing user placements/scales
preserved; three-unit startup projection only, no giant teleports across scenery.

IMPORTANT: user's5x spawn(20,-1.84,25) intersects **mediumTree (193)**. Actual physics
capsule overlap confirms tree.5x nav has no point within3/5/10m; nearest sample
(35.76,-1.36,13.18),19.71m away. Setup correctly shows spawn error instead of shrinking
clearance or forcing movement. Move that Titan to clear ground, e.g.X36/Z13; no rebake
needed just for repositioning onto current data.3x starts on mesh and chases naturally.
5x relocation for validation was ONLY temporary Play Mode; original saved pose retained.

**123/123 passed twice**, Unity2021.3.25f1, all116 prior +7 nav tests:
- **2026-11-03 19:03:46–19:04:02 UTC**, PID21108,16.006s.
- **19:05:38–54 UTC**, PID26368,16.248s.
New checks: whole-map static physics source filtering, cross-scene fail-closed,
actual TerrainCollider,500m sloped-mesh path around cover,16m gap passes3x/rejects5x,
exactly-once scaled avoidance, owned registration/re-enable preserving external data.
All existing<8deg/120Hz,<40m/s,physical contact/torso assertions retained.

Live unpaused native Editor review: both data registered,20532 triangulation indices.
3x61/68 and5x60/67 sampled grid destinations complete, seven disconnected/blocked
refused. Longest native complete routes1409m/13 corners and1418m/14 corners, native
actors actually moved>1.3km (not just path queries). First long run exposed double-
scaled avoidance: world clearance belongs to BAKE, agent component dimensions follow
transform scale. `Profile.ConfigureAgent` sets radius~1.8/height~12 locally, not5.4/9
again; regression added. Final distinct-destination native run arrived:3x147.41m
movement,1.87m remaining within3m stopping distance;5x204.93m movement,0m remaining;
both on mesh/PathComplete/no pending/velocity0 after reloading rebaked assets.

Proof **ArtSource/Titan/Navigation/FreeRoam_FullMap_MCP.png**, real map with temporary
native route overlay (cyan3x/orange5x); overlay/camera/RT removed, fog restored.
Backup `Library/TitanFullMapBackup/`. Editor returned clean/unpaused Edit Mode in
FreeRoam with saved3x/5x transforms intact. Read `docs/titan-freeroam-navigation.md`
for menu/rebake/controls/limitations. Moving geometry/new roots/scale changes require
explicit saved-scene re-bake. Not yet scaled combat/ragdoll/terrain-foot-IK/multi-floor/
full crowd approval. Do not claim every disconnected island reachable.

## Previous request: commit integration checkpoint, then test full-map FreeRoam navigation

User moved/copied the two modular Titans into `Assets/Scenes/FreeRoam.unity`, scale3x
and5x, and tuned AI defaults to sightRange138/FOV180. Preserve these choices. User
requested committing current integration then full-map navigation as a TESTING pass.
Checkpoint includes the actual scene/player/FBX dependencies (four GUID-preserving
model moves + Taking Item used in Shiganshina); unrelated Plastic/IDE/material/
Ragdoll/unused model/prefab changes excluded. Sight fixture pins its original38/120
cone rather than assuming game defaults; assertions unchanged. Latest checkpoint
**116/116 passed**, Unity2021.3.25f1,2026-11-03 **16:42:26–41 UTC**, PID39268.

FreeRoam currently has no scene integration/nav surface/AI on the newly copied Titans;
403 colliders,332 environment MeshColliders (unreadable CPU meshes), no Unity Terrain
components. Real map environment bounds roughly2064x1538m, some trees704m tall.
New navigation work must include actual MeshColliders + boxes, preserve actor
placements/scales, and avoid runtime whole-map mesh bakes that require changing all
mesh import settings. Prefer owned Editor-baked data with clear per-size clearance.
No additional push requested for this checkpoint.

## Previous: approved work PUSHED; real ODM integrated into requested Shiganshina scene

User approved, requested commit/push, then asked to integrate into existing
`Assets/Scenes/FreeRoam (Shiganshina.unity` and remove its old Titans. Explained that
motor integration means narrow hold-only suspension/resume hooks, not replacing or
retuning the ODM gear. **940daf1d** (`Add fast physical aerial grabs and clear torso
during mouth carry`) committed/pushed to **origin/titan-combat-updates**; HEAD and
upstream match. Prior prototype commits now pushed too. No merge/main/force push.

NEW integration is a separate uncommitted review slice. Actual requested scene saved,
only its two old Titan/TitanV2 instances removed (source assets retained); two approved
modular Titans near spawn target the existing real player. Existing player/body
prefabs/art/rest rig unchanged. Original scene backed up in ignored Library and
owned external temp before any edits; unrelated dirty RagdollController/FreeRoam/model/
material/Plastic/IDE work preserved. New scene-only sword damage window assigned to
previously unassigned Attack.damageDealer. F remains charge/release sword, E hook,
T music; NO lab F flight / E escape / T combat shortcuts added.

New default-assembly adapters: `Assets/Titan/Integration/TitanODMMotor.cs`,
`TitanODMHealthSource.cs`, `TitanODMSwordSweep.cs`, `TitanShiganshinaIntegration.cs`.
Prototype optional `TitanHealthSource` + bounded compound `TitanReleaseClearance`.
Movement/ODM get explicit owner-token suspension that stops old jump/boost/inertia
coroutines/hooks, preserves normal tuning, restores safe current pose/momentum.
Real ground holds also use motor/current-pose clearance, lab ground unchanged.
Velocity sampled after real FixedUpdates. Health is ACTUAL PlayerHealth, no duplicate
HP. External death releases before ragdoll; death and attack-disable cleanup stop
motion/weapon windows. Player prefab has no assigned human ragdoll; no new one claimed.
Sword sweeps use actual two mesh poses during original .6s window, per-region/swing
dedupe, and remain active while held without enabling body colliders. Gear-box-only
bite contact is rejected. Real release checks CCT AND solid gear boxes.

CRITICAL native observation: Unity2021 CCT capsule axis stays WORLD-UP even when the
visible/root transform tilts; only configured local CENTER rotates. Verified actual
45deg root: bounds size(1,3.6,1), center(0,1,0) -> root+(-.70711,.70711,0). Do NOT
use transform.up for a native CCT capsule. CCD/closest-point/release now match this;
actual visual root still freezes at physical TOI, no upright snap. Lab retains its
upright-root gate; real adapter supports observed root/center rotation. Ordinary
CapsuleColliders/gear boxes follow their own orientation as normal.

Owned nav: explicit real floor/box collision sources, **256x256m patch around origin**,
not full-map production nav. Both agents on NavMesh/Chase;156 triangle indices.
Added exact scene to Build Settings (disk + live) for existing3s death/respawn;
Menu/FreeRoam entries preserved. `Assets/Editor/TitanShiganshinaInstaller.cs` is explicit,
undoable, refuses wrong/dirty/duplicate setup; no auto scene rebuilding.

**116/116 passed twice**, Unity2021.3.25f1:
- **2026-11-03 15:52:12–27 UTC**, PID40516,15.169s.
- **15:55:56–15:56:11 UTC**, PID34836,15.561s.
All108 approved tests +8 (native CCT bounds/rotating-center CCD, real motor suspend,
ground current release, full gear clearance, tilted-root native headroom, real
health/external death, release momentum, crossed sword/per-region dedup). Old strict
<8deg/120Hz and<40m/s retained: fast6.45deg,moving6.64deg; forearm/palm torso0;
aerial loaded death11.67m/s; intact10.86m/s,gap.003m,hipsY1.03m. Runner now compiles
actual default-assembly adapter + narrow legacy dependencies; no preview physics.

Live scene MCP, unpaused, controlled actual `ODM_Movement.Throttle/FaceGrapplePoint`
(not lab motor), native.02s: selected.54s after pass start, physical catch.18s later,
root tilt8.61deg, one new capture/one bite, ACTUAL and bridge HP0, no parenting,
hooks cleared, no pending release. Sampled close scene frames reviewed.
Proof: `ArtSource/Titan/Combat/Shiganshina_ODM_Integration_MCP.png`.
Separate native death/respawn confirmed scheduled reload -> changed scene handle,
HP100/bridge100, controller enabled, two Titans + nav rebuilt. Test replay canceled
Invoke only in its own screenshot wrapper; separate respawn test did NOT cancel.
Final Editor clean/unpaused Edit Mode, requested scene; temp camera/RT removed.

Next: USER interactive fast rope/boost/held sword feel review, full-map nav, full-body
carry/terrain clearance (currently grip sweep + whole-gear release gate), crowd/corpse
stress, human ragdoll/fingers/escape balance polish. Do not claim production animation
or all-terrain approval. Before committing new integration, audit scene/player/model
GUID dependency closure: these included preexisting UNTRACKED assets; do NOT stage
entire unrelated dirty tree. Source player prefab remains unchanged locally.
See `docs/titan-shiganshina-integration.md` for contracts, controls and limitations.

## Previous approved aerial mouth-carry torso clipping fix (now committed940daf1d)

User approved the fast catch (looks great), but reported the arm crossing the body
on the way to the mouth, and asked what comes next. MCP reproduced a backwards
elbow and straight inward hand path: forearm/chest penetration1.43m, palm1.32m.
Now blend the elbow forward/outward ONLY after contact, and lift in front on a
.9m-forward/.35m-up transfer arc before closing toward the mouth. Existing fast
0/.22 catch, .25/.65 miss retry, .7s transfer and bite telegraphs remain unchanged.

Important extra root cause: some physical catches grip feet/side with the human
IN FRONT of the palm. A frozen WORLD-space offset forces the hand inside the chest
when that person's torso reaches the mouth. `TitanCombatTarget.SetAerialCarryYaw`
now lets only the current airborne owner turn the person smoothly around the SAME
grip into side-on carry; local grip point and root-to-grip length remain constant,
no parenting/recentering/regrip snap, capsule stays upright. `biteGripOffset` remains
latched but its world direction is yaw-rotated during transfer and retained after
release for arm recovery. Ground follow/restore is unchanged. Release velocity
includes the actual held root's orbital motion, not only socket translation.
Mouth approach.6m forward still requires actual victim contact with unchanged.55m
bite sphere. Long-tick validation samples the arc INCLUDING its changing yaw offset;
ordinary frames sweep the real grip; original conservative checks remain too.

**108/108 passed twice**, Unity2021.3.25f1, **2026-11-03 14:40:20–36 UTC** (PID34496)
and **14:44:01–16 UTC** (PID37808,15.493s). All106 previous checks +2 new for both hands/
two animation phases torso clearance, preserved local grip/radius/upright root,
and forward-arc cover on long ticks. Existing fast moving full catch/bite now also
asserts torso clearance. Actual nontrigger forearm/hand versus chest/spine via
`Physics.ComputePenetration`: **0** in four static cases and moving case, strict<.005m.
The shared shoulder seam is intentionally not tested as a separated body part.
Continuity limits remain<8deg/120Hz: fast6.68deg, moving6.31deg, old-timing5.32deg.
Aerial mouth death10.83m/s (<40 unchanged); intact11.26m/s,gap.007m,hipsY.86m;
ground mouth10.91/10.36/11.82/11.81m/s. No physics tuning, speed caps or weaker tests.

MCP inspected before/after working sandbox poses1.70/2.15/2.42s. Final forearm/palm
penetration0, same.175s capture, one capture/one bite, HP0, release complete, max6.18deg
across all3 selected arm bones. Proof:
`ArtSource/Titan/Combat/AerialMouthClearance_Comparison.png`. Temp review wrapper
`%TEMP%/titan_torso_review.py` generates close captures in ignored Library dirs.
Returned Editor to Edit Mode, unpaused, scene clean, camera/RT removed; no scene saved.
Main final import includes new analytic yaw-aware arc obstruction checks.

Next recommendation (user asked): review/checkpoint scoped aerial work, then build a
REAL-player integration lab, implement actual ODM motor suspension/release (ropes,
boost, coroutines, input/camera/look, safe current pose/momentum), wire real health/
death/respawn, then swept real swords and held-arm-cut counterplay, followed by
full-speed terrain/edge/cover/multiple-Titan stress/polish. Do not start more proxy
features instead of real gear wiring. No gameplay scene/player prefab changes yet.
Current aerial work remains uncommitted; ground commit28cb997a stays local/not pushed.
See `docs/titan-aerial-grab.md` for contract, limitations and next-step order.

## Previous: fast, frequent aerial reach — NO warning-arm preparation

Latest user feedback: grabs felt slow/infrequent. Explicitly requested NO raised
warning arm: ODM players already know standing/flying in front of a Titan is dangerous.
This supersedes the earlier aerial windup requirement, not ground/stomp/bite timings.

Implemented defaults: **0s aerial windup, .22s direct reach from the actual palm**,
no pre-grab warning ring. Miss recovery.25s, aerial cooldown.65s (~1.12s repeat miss
cycle versus several seconds). Successful carry/bite/recovery retain their existing
durations; absolute bite now3.14s because the catch starts sooner. Aerial selection
checks every eligible frame before ground retry/stationary gates. Seven bounded
prediction times allow current reachable flyers even if a full forecast exits range;
aim still commits once, actual hand-box/capsule CCD still REQUIRED, dodge still works.
No snap/homing/hidden target tracking or larger catch geometry added.

Fast reaches transport the ACTUAL selected anatomical hinge plane instead of rolling
into the old outward/down pole. Partial early catch must cache upper/lower joint
rotations and begin carry from that exact pose: re-solving the partial contact pose
with fractional reach weight twice caused12.61deg catch pops before this fix.
Keep these ownership/continuity guards; ground solver and rig remain untouched.

**106/106 passed twice**, Unity2021.3.25f1, **2026-11-03 14:02:10–26 UTC** (PID31208)
and **14:04:21–38 UTC** (PID30596). All101 prior checks +5 new for fast defaults,
no warning lift/ring, dodge, short retry, moving full-sequence continuity and
bypassing an active ground decision delay. Older explicit-time regressions pin
configurable.7/.3 values; saved-scene auto test now waits for true reach range.
Crossing test samples the actual new hand path, not the removed raised pose.
No assertion thresholds relaxed. Latest<8deg/120Hz: fast stationary6.43deg,
fast moving6.48deg; actual static catch.158s. Aerial loaded mouth death8.31m/s (<40).
Intact11.14m/s,gap.005m,hipsY.84m; all74 approved ground/bite/physics tests still pass.

MCP normal Play Mode / unpaused explicit120Hz real CCT replay: one NEW catch/one bite,
HP0, release complete; capture **.175s** after selection; all3 selected arm bones
max6.18deg from reach start through recovery. Viewed0ms/100ms/220ms/mouth frames:
no preparatory lift. Proof `ArtSource/Titan/Combat/AerialGrab_DirectReach_MCP.png`.
Prior proof with warning lift is historical, not the new behavior. Temp capture
`%TEMP%/titan_fast_aerial_capture.py` uses counter DELTAS because the lab may already
have completed an automatic ground grab before a manual review starts.

Editor returned to Edit Mode/unpaused, no scene saved; temporary camera/textures
removed. Current aerial work stays uncommitted; approved ground commit28cb997a
remains local/not pushed. No production ODM wiring done: still upright CCT proxy;
real rope/boost/controller/health integration is next. See `docs/titan-aerial-grab.md`.

## Previous aerial lab baseline (101 tests, before fast-reach feedback)

User said the ground bite/arm fix looks good and explicitly requested a commit,
then aerial grabbing. **Committed approved ground slice as28cb997a**, local only,
not pushed. Unrelated dirty assets/Plastic/IDE/model moves were excluded.

Aerial prototype is now implemented in the isolated lab. See
`docs/titan-aerial-grab.md`. **F** launches a reproducible airborne controller pass;
T auto combat,9/0 manual selected hand, E escape, normal limb/eye/death/reset controls.
Actual physical hand OBB versus configured human capsule CCD, with relative motion
and rotating-hand sweep; not an enlarged catch radius or detection-to-socket snap.
Prediction is sight-confirmed once, bounded1.05s/24m, then aim locks; actual arm
reach plus frontal shoulder-plane clearance required. Default .7s windup/.3s reach.
Standing chest carry8.2m then existing mouth/bite. Catch offset is preserved by moving
the arm; never parent a human to the skeleton. Early contact time constrains leases.

`TitanAerialMotor` is an explicit opt-in suspension/release contract.
`TitanSandboxFlight` is only the LAB implementation, NOT real ODM integration.
It restores from the current airborne pose, full-capsule exit/endpoint clearance,
explicit decayed incoming + hand velocity, gravity resumes/boost ends. If blocked,
ownership clears but safe physics restoration retries independently; removed target
component hands continuation to the motor. Production motor must outlive that gate.
Upright centered CCT supported; yaw okay, capsule tilt changes fail closed.
Real ODM ropes/boost/coroutines/health, rotating player shapes, held-sword policy
and production respawn remain the NEXT integration work. Legacy scripts inspected,
not modified; no gameplay scene/player-prefab wiring done or claimed.

**101/101 passed twice**, Unity2021.3.25f1: **2026-11-03 04:24:11–26 UTC** (PID9552)
and **04:26:31–46 UTC** (PID29000). All74 approved checks +27 additions. Final moving
proxy fixture uses the actual authored1.8m/.32m CCT and local runtime physics.
Strict<8deg/120Hz continuity: stationary6.39deg, moving5.64deg. Aerial mouth death
12.79m/s (<40 unchanged). Ground mouth deaths10.47/10.36/11.82/11.81m/s; intact
11.04m/s,gap.005m,hipsY.84m. No rig, art, mass, angular ranges or physics settings changed.

Unity MCP verified the working project, reproduced/fixed a moving shoulder-roll
singularity (90deg live /123.67deg new regression) missed by initial stationary
checks. Aerial uses outward/down pole; offset iterations restart from the SAME
animated base. Keep the approved ground pole separate. Full physical hand box also
fixes inscribed-sphere corner misses without enlarging contact geometry.
Final MCP moving capture -> carry -> mouth -> bite succeeded: one capture/one bite,
HP0, release complete, upper-arm max4.30deg. Viewed saved poses; proof:
`ArtSource/Titan/Combat/AerialGrab_MCP.png`. Temporary camera/textures removed;
Editor returned to Edit Mode, unpaused, scene clean, no scene saved. Normal
full-speed/finger/art polish is still not production approval.

MCP direct HTTP client is `%TEMP%/titan_mcp_review.py`, capture `%TEMP%/titan_aerial_capture.py`.
Use tool_search first; this session still exposed none, but server8080 is usable.
HTTP404 means expired temp MCP session: remove that OWNED .session, initialize,
select instance34b6b0a61e38ef2f. First command after Play transition may return null;
confirm a read-only ready query succeeds before a side-effecting replay. Keep
Editor unpaused. Do not blindly repeat a timed-out capture without checking state.

## Ground bite / arm-spasm fix — approved and committed28cb997a

User tested ground grab and reported spasming on the trip to the mouth, explicitly
asking for Unity MCP visual inspection. Did reproduce in the working Editor:
137.17deg shoulder jump/1/120s near3.14s. New continuity regression failed137.26deg
while all73 previous checks passed. Fixed explicit elbow-plane rotation frames in
`TitanGrabAttack.SolveArm`; source hinge uses the real hand, not offset grip.
Also fixed cumulative additive jaw opening on a constant Generic Animator channel:
reset to cached closed local jaw rotation before applying the current opening.
Do not reintroduce additive jaw accumulation or shortest-arc arm roll ambiguity.
No damage timing, capture radius, rig/model, physics limits or thresholds relaxed.

**74 passed /0 failed twice**, Unity2021.3.25f1, **2026-11-03 02:44:07–20 UTC**
(PID51064) and **02:49:24–37 UTC** (PID55304). New both-hand/four-phase120Hz test
measures shoulder/elbow/wrist rotation and bounded jaw opening. Max step2.26deg
(assertion<8deg); jaw<28.1deg. All73 prior checks pass. Mouth deaths max10.57/10.36/
11.82/11.81m/s; latest intact11.06m/s,gap.005m,hipsY.84m.

Unity MCP now actually used via localhost8080 JSON-RPC (normal tool_search still
exposed none). Started installed10.3.0 server, reconnected existing Editor bridge;
project hash34b6b0a61e38ef2f, working Editor PID57936. Captured BEFORE and AFTER in
normal sandbox Play Mode, manually stepping a frozen actor; no preview physics.
Both sides checked: live max2.13deg left/1.72 right, mouth/grip gap.35m. Adjacent
captured frames no longer flip. Comparison artifact:
`ArtSource/Titan/Combat/GrabArmContinuity_Comparison.png`.

Returned Editor to **Edit Mode, unpaused, TitanAISandbox clean**, no scene saves.
Temporary review camera and `Assets/Editor/TitanGrabReviewReconnectOnce.cs`/meta
removed. Only ignored Library review captures/logs remain. A connect-only bootstrap
request was used to restore MCP after removal; never run an asset build by accident.
Paused Play Mode caused unreliable MCP/WebSocket replies on Unity2021; future
captures should freeze actor ticks but keep Editor unpaused. Temporary HTTP client/
capture scripts are in `%TEMP%/titan_mcp_review.py` and `titan_grab_capture.py`.
Full animation/finger polish remains, but sampled visual review is no longer pending.
User subsequently approved the visual result and committed it as28cb997a; aerial
lab follow-up is described at the top of this checkpoint.

## Ground hand-to-mouth / bite foundation (committed28cb997a)

User clarified order: finish the ground grab's hand-to-mouth/bite first, THEN do
aerial ODM grabs. They specifically emphasize the arm must actually catch the player;
keep actual3D hand contact/reach as a hard requirement for aerial work.

Implemented in `TitanGrabAttack` as opt-in `enableBite` (standalone defaultfalse;
`TitanAISandboxDemo` sets true at runtime). After .83s ground contact, hold/lift1.5s,
transfer .7s, open-mouth warning .6s, close .12s, then one mouth-contact event;
.45s recovery/3s cooldown. Default damage100; latched timing/damage/radius/flag.
The actual arm IK reaches MouthSocket with both hands; jaw opens/closes. Feet plant
through rising. Orange/red mouth telegraph; E escapes through the last pre-hit frame.

Damage requires exclusive live ownership, actual disabled-collider geometry touching
.55m mouth sphere, valid selected arm/eyes/legs/AI/socket, unexpired lease, and clear
carry/mouth geometry. Grip path is swept during LateUpdate before target follow;
fast-forward checks lift/transfer segments too. Fail closed on saturated queries.
No delayed kill timer/coroutine or automatic death solely because holding elapsed.
Logical hold age prevents long ticks bypassing the victim's shorter lease.

Original cancellation/never-parent/state restoration remain. Lethal contact releases
but still recovers. Damage callbacks can kill/sever the Titan: snapshot restoration
must yield to current injury/ragdoll pose. Unsupported disabled mesh shapes fail
closed. Real player still needs an adapter. Proxy dies at contact then returns to its
original ground position; no swallowed mesh/corpse consumption or production respawn.

Pre-spasm-fix validation: **73 passed /0 failed twice**, Unity2021.3.25f1,
**2026-11-03 01:50:19–32 UTC** (PID49920) and **01:52:14–28 UTC** (PID26732).
All58 previous checks +15 additions, including normal-frame automatic playback and
saved-lab proxy bite/reset. Four mouth death cases max10.29/10.09/11.27/11.04m/s;
latest intact10.98m/s,gap.005m,hipsY.84m. No physics settings/limits/assertion thresholds
changed. Earlier69-check run exposed only a test cooldown sequencing mistake;
completed recovery before the next cooldown tick rather than weakening the gate.
See `docs/titan-bite-combat.md` and `docs/titan-prototype-validation.md`.

This foundation and the subsequent arm fix are now committed28cb997a; not pushed.
Main scenes, passive prefab, art/rest rig/capped cuts and legacy player untouched.
MCP review completed and user approved the ground visual result. Aerial lab is now
implemented; real ODM integration still needs explicit rope/boost/coroutine/momentum
and controller policies. Keep ground snap-back out of production aerial release.

## Previous request: commit combat and require mid-air ODM grabs

User approved committing the current combat slice and explicitly added a requirement:
**Titans must also grab airborne players swinging on ODM gear.** This is NOT yet
implemented by the ground-targeted grab (current selection rejects vertical offsets
above2.2m). Keep it in the next combat/player-integration plan; see the dedicated
mid-air interception section in `docs/titan-grab-combat.md`.

Plan height-aware3D reach, bounded sight-confirmed motion prediction followed by
committed/dodgeable aim, swept relative hand/player contact at ODM speeds, and the
same ownership/escape/cancellation safeguards. Real ODM ropes, boost, gravity and
release momentum need explicit adapter rules and collision-clear release. Do NOT
reuse the proxy's snap-to-pre-grab pose as a production aerial release, or solve
interception with hidden tracking, shortened wind-ups or a giant catch radius.
A standing Titan catching a flyer is the requirement; Titan jumping is separate.

Combat commits on `titan-combat-updates` (local, not pushed):
- `5bc07b40` Sandbox stomp/grab runtime, hold ownership, director, HUD and stable physics handoff.
- `ab988bf7` Combat/contact/ownership/scene-reset regressions (58 passing at checkpoint).
- Documentation/air-grab requirement committed separately with this checkpoint.
Unrelated dirty files remain excluded; no new runtime behavior changed for the air-grab note.

## Grab / hold / release foundation (committed)

User approved the stomp result and asked for the next step. Added standing,
sandbox-only `TitanGrabAttack`, exclusive bounded hold ownership in
`TitanCombatTarget`, and `TitanCombatDirector` for grab/stomp selection. At that
snapshot no bite/eat or real-player adapter; the new ground bite is above. See `docs/titan-grab-combat.md` for API/limits.

- Fixed .65s windup/.18s reach, one actual grip contact,1.5s bounded lift/hold,
  .45s recovery,3s cooldown. Aim is locked, not homing. Deep crouch/foot planting
  gets the current rig down to a rounded1.8m human; no finger animation.
- Human NEVER becomes a child of a hand/Titan. Collider/RB states are cached and
  restored; default release returns to its captured pre-grab WORLD pose. Moving-
  geometry clearance/real-player controller integration still need an adapter.
- E escape; own arm loss/eyes/either leg/death/disable/reset/AI pause release safely.
  Other arm loss does not cancel. Independent target lease defaults4s, catches a
  dead/stuck owner or invalid/detached socket. Standing only; no kneeling grab yet.
- Order: motion20 -> attacks50 -> target follow70 -> injury100. Never overwrite
  detached bones or stand up an injured/dead body on cancel.
- Selector uses seeded LOCAL System.Random (55% grab preference/fallback), waits
  for braking and spaces automatic attacks. Stomp standalone auto is disabled when
  director is added. Mutual exclusion also holds without a brain.
- Found rounded-controller edge misses: Unity's CCT query capsule is shrunken.
  Configured capsule geometry is explicitly checked (same1.05m hand radius), and
  stomp uses a CharacterController-compatible closest-point helper too. Tests now
  exercise actual controller-only victims, not just box stand-ins.
- Final review fixed an auto-update timing hazard: a director/input start earlier
  in this Update must NOT consume the delta from before it started. Both attacks
  skip automatic ticking on their selection frame; explicit TickAttack remains
  deterministic. New regression checks full telegraph and subsequent advancement.
- **58 passed /0 failed twice**, Unity2021.3.25f1, 2026-11-03 01:04:02–11 UTC and
  01:05:48–57 UTC. Latest PID20680. All40 previous tests plus18 new ones.
  Grab crouch/carry4 death cases max7.45/7.83/11.35/10.41m/s; snapshot pose preserved.
  All8 stomp deaths still pass. Latest intact max10.95m/s,gap.006m,hipsY.85.
- Actual saved AI scene integration resets WHILE HELD and verifies human survives,
  controller restored, old owner destroyed and no extra grab actors remain.
- New controls: **T auto combat**, **8 stomp**, **9/0 left/right grab**, **E escape**;
  existing limb/eye/nape/P/R and WASD/Shift unchanged. HUD now scrolls.

Stomp/grab runtime and tests are now committed as listed above; no push requested.
The five earlier snapshot commits remain below. No main scenes/prefab/art/rig generation modified.
Harness Unity tools unavailable; **live visual review remains pending**. Numeric
animation/physics tests are not proof of finished motion/art. Ground bite follow-up
is now above; mid-air support follows it and remains an explicit requirement, not
an optional polish item. Real-player/sword integration remains pending.

## Snapshot commits and stomp foundation (previous slice)

User requested committing the current work in chunks, then moving on. Five local
commits made (not pushed):
- `4816c15b` Unity2021-compatible MCP tooling.
- `2a4ee6c1` Titan art, historical backups outside Assets, Blender sources and previews.
- `69e1a3cc` Modular runtime, patrol labs, injury and responsive motion.
- `df22a580` Existing legacy combat scaffold plus modular sword-hit adapter; includes
  Attack.cs window and ODM speed accessors needed to compile that adapter. Legacy
  gameplay wiring is still unverified, not replaced by modular code.
- `6c98f1ca` Safe disposable tests and validation checkpoint (28 passing at snapshot).

Unrelated model moves/deletions, gameplay scenes, RagdollController.cs edits,
materials, IDE/generated project files, Plastic SCM state, old prefabs/plans and
old implementation-log docs deliberately remain uncommitted. Do not reset them.

The first combat slice was **sandbox-only stomp**, now included in the combat commits above:
`TitanStompAttack.cs`, `TitanCombatTarget.cs`, AI movement ownership, HUD/proxy health,
and12 additional tests (40 total). Fixed .65/.14/.5s telegraph/strike/recovery,
2.2s cooldown, fixed visible landing circle,40 damage once, scene-local geometry,
cover/airborne/AoE filters and immediate interruption. Passive body prefab unchanged.
Original slice used T for auto stomps; current T is auto COMBAT, with9/0 grab and
E escape added above. 8 remains left stomp; P/limb/eye/nape/reset remain.
See `docs/titan-stomp-combat.md`.

Expanded death tests cover8 deterministic phase/foot combinations and an unforced
(no gravity/no external contact) handoff. They exposed late wrist velocity spikes
with hard angular stops in folded, contacting poses. Fixed by **mass-proportional
compliant/damped angular stops on dynamic activation** (spring=mass*80,damper=mass*12).
Original angular ranges, masses, solver counts, angular speed cap4, joint count and
no-interpolation ownership retained. No global physics setting or linear speed cap.
Also rebase POSITIONAL connected anchors from the current world pose: animated
non-physical spine/shoulder intermediaries move physics bones off old bind anchors.
Experimental mass conditioning, friction, solver, preprocessing, angular-cap and
range changes were NOT retained. Stomp braces within its elbow hinge plane and
recovers by blending IK targets home; support feet do not sink as hips rise.

**40 passed /0 failed TWICE**: 2026-11-02 23:21:38–47 UTC and23:24:32–40 UTC,
Unity2021.3.25f1. Latest PID47384 in the same owned disposable project below.
Loaded-stomp death8 cases max speeds12.01–12.27m/s (formerly up to~100m/s);
unforced handoff max.000113m/s. Latest intact max10.89,gap.004m,hipsY.86;
injured prone5.06/kneel11.64m/s; wall routing4.23 simulated seconds.
All28 snapshot tests still pass. Stomp is now committed together with the grab runtime/tests above.

Working Editor was not running at turn start; opened project then. User later
reopened it: last read-only process/title check PID42496, TitanAISandbox, Unity2021.3.25f1,
WebGL target. Harness Unity tools unavailable; **live stomp visual playback is still
pending**, despite numerical animation/physics checks. No intentional pending
`TitanPrototypeBuild.request`. Inspect current state before any scene actions; do
not save gameplay scenes. New code imports on Editor refresh/focus.

## Earlier motion-overhaul phase

- `Assets/Titan/Prototype/TitanMotionStyle.cs`: measured-speed walk/run BlendTree
  parameters, per-instance cycle/cadence/turn/posture bias; seeded local RNG, coherent
  smooth pace segments and occasional short chase hesitations, not random frame jitter.
  Does not consume UnityEngine.Random or look up a hidden player. Fields styleSeed
  (0=instance seed), varyPace, reference speeds; public ResetStyle/TickMotion/TickTurning.
- `Assets/Editor/TitanMotionClipBuilder.cs`: reproducible Generic Idle2.6s, Walk1.02s,
  distinct Run.68s, key-pose contact/pass/quick recovery, bent running arms, torso
  twist/lean, hip sway/contact correction. Existing controller/Idle/Walk GUIDs retained;
  adds Run.anim. Params IsWalking/Gait/StrideRate/CycleOffset; 90/100ms transitions.
- Measured root speed drives gait/cadence; global Animator.speed remains1. Head leads
  sharp pivots85ms then root turns210deg/s walk or330run (individual variation).
  Sharp corners reduce pace. Motion owns agent.updateRotation, restores on disable.
- AI base speeds4.6 patrol/9.5chase, profile factors .90–1.08 patrol/.94–1.20 chase,
  brief .12–.22s hesitation targets .52–.66, segments1.7–3.5s. Lab acceleration26.
- Fast eye recoil then staggered coverage~.15–.2s; repeated hits alternate lead hand.
  Arm loss adds brief chest recoil. No styling writes while blind, leg-disabled or dead.
- Injury durations now **.58s knee / .76s prone**, with buckle/fast drop/impact/damping
  and staggered arm/head timing, not uniform slow smoothstep. Final poses unchanged.
- Foot collider orientation fixed: diagonal toe bones made old on-bone boxes oversized
  vertically, lifting the visible soles. Correctly oriented child FootPhysicsShape
  boxes now form compound colliders on the SAME foot Rigidbodies; zones follow them.
  Still17 bodies/16 joints/14 zones/22 original source bones. Tests check visible soles.
- Rebind now preserves the surviving pose, current state/time and float parameters,
  as well as detached transforms: running cuts must not reset hip motion/gait phase.
- Body prefab rebuilt; main gameplay scenes untouched. Original body lab is still
  non-navigating, but now has **7** chase-run preview as well as1 idle/walk.
- **28 passed /0 failed**, Unity2021.3.25f1, **2026-11-02 04:00:16–24 UTC**.
  All21 old tests +7 in TitanMotionStylePlayModeTests.cs. Rate/gait, deterministic
  bounded variation/global RNG independence, distinct looping supported clips,
  head-led pivot, fast fall shape, running cuts and quick eye recoil checked.
  Normal-physics integration allows bounded authored hip bob/contact motion while
  still checking horizontal alignment and other bones' unchanged local positions.
  Old two-sample stride test now checks a full cycle to avoid symmetric-phase flakiness.
- Latest runner completed PID50500, same disposable project/results paths below.
  Intact death maxspeed12.54,gap.004m,hipsY.95; injured5.89m/s prone/9.57knee.
  Wall-routing root passes obstacle in4.23 simulated seconds.
- Final checked Unity contact sheet: `ArtSource/Titan/Motion/TitanMotion_Unity.png`.
  Six raw Walk_0..2/Run_0..2 PNGs alongside it. These show poses, not a video.
- Final visual checks completed; Play Mode stopped. AI lab left open in EDIT MODE.
  Final structural validation PASS includes distinct idle/walk/run, motion and injury
  components. Inspect current dirty state before saving/changing scenes.
- Notes/tuning/limitations: `docs/titan-motion-overhaul.md`.
- Still generated/native prototype motion, not production mocap/full runtime foot IK.
  Physics death remains natural-time. Attacks/player integration remain next, not done.

## Earlier injury-animation phase

- New `Assets/Titan/Prototype/TitanInjuryPose.cs` attached by body prefab builder.
  Procedural eased pose blends on the existing rig (not imported animation clips).
  Originally1.2s surviving-knee/1.5s prone; current .58s/.76s impact-shaped transitions.
  Mirrored knees and rapid double-cut supported. Only attached bones are written.
- First leg loss disables Animator; injury controller owns bones while alive/injured.
  LateUpdate order100 overrides standing blind-hand posing. Death stops writing;
  the existing body takes over with joint-rebased ragdoll. Root/nav position stays fixed.
- Scene-local ground ray ignores self, dynamic debris, triggers and ceilings. Analytic
  collider support settles/lifts pelvis during folding arcs. Flat/gently stepped
  ground only; not a full terrain-contact IK solution.
- Body `IsHelpless` true when both legs gone; both grab capability flags then false.
  Single-legged kneeling retains arms for later attack work, but cannot walk.
- Both sandbox HUDs show injury state. Body prefab rebuilt preserving rig/art/GUIDs.
- Actual Unity captures checked:
  `ArtSource/Titan/Reference/TitanInjury_KneelingRight_Unity.png`,
  `ArtSource/Titan/Reference/TitanInjury_Prone_Unity.png`.
- **21 passed / 0 failed**, Unity2021.3.25f1, **2026-11-02 02:52:40–49 UTC**.
  Seven new checks in `TitanInjuryPosePlayModeTests.cs` plus all14 old tests.
  Mirrored knee/head/shin pose, smooth fall and floor support, second-cut current
  visual pose preserved, no writes to detached bones, missing arms/rapid cuts,
  helpless flags, blindness recovery and death from injured poses all pass.
  Max injured death speed: knee9.94m/s, prone9.72m/s; intact11.09m/s,gap.003m,hipsY.79m.
  Latest completed runner PID33784; same temp project/results paths below.
- Test pitfalls resolved: snapshot Transform positions, not stale Rigidbody native
  caches after manual animation; direct world skinning with bindposes/weights avoids
  BakeMesh+TransformPoint double-counting the 100x FBX scale; manually simulated
  physics waits for Unity frame-end deferred joint destruction after severing.
- Last live action stopped Play Mode after previewing the poses. AI lab remains open
  in edit mode; inspect dirty state before changing/saving. Temporary preview actors
  were runtime-only and are gone after exiting Play Mode.
- Full notes: `docs/titan-injury-animation.md`.

## Navigation foundation (earlier phase)

- New isolated scene: `Assets/Scenes/TitanAISandbox.unity`.
- `Assets/Titan/Prototype/TitanPatrolChaseAI.cs`: opt-in component requiring the
  body and NavMeshAgent. States Patrol, Chase, Investigate, Search, Blind,
  Immobilized, Dead, NavigationUnavailable. Explicit target ROOT, no player lookup.
- 38m range, 120-degree horizontal FOV, .15s scene-local raycast perception;
  ignores own/target hierarchy and triggers, solid scenery blocks sight.
- Only complete paths. Current base patrol4.6m/s, chase9.5m/s with motion variation,
  nominal chase stopping radius6m,
  .3s repath. Lost sight uses LAST SEEN position, not hidden target's live position.
  Memory14s; local turn/search4s; then nearest patrol route point.
- Blindness stops immediately, prevents visual detection and ages memory. Recovery
  reacquires normally. Arm loss allows walking; either leg loss immobilizes.
  Death disables navigation and uses the existing ragdoll.
- `TitanNavigationSurface.cs`: explicit floor/wall runtime NavMesh bake with built-in
  Unity2021 APIs, radius1.8m/height12m. Owns/removes only its own NavMeshDataInstance;
  no global bake settings changes or RemoveAllNavMeshData.
- `TitanSandboxTarget.cs`: ground-only controllable human PROXY, not the real player.
- `TitanAISandboxDemo.cs`: runtime agent/AI setup, HUD, input, reset/cleanup. Reset
  actors remain in the lab scene even when loaded additively; old agents disabled.
- `Assets/Editor/TitanAISandboxBuilder.cs`: menu
  `Tools → Titans → Build Patrol and Chase Sandbox`. Close AI lab before rebuilding.
  Saves only its new additive scene and never saves user's active/gameplay scene.
- Original body prefab and body lab remain PASSIVE (no AI components added).

Controls: **WASD** human world X/Z, **Left Shift** sprint, **Left/Right arrows**
orbit, **2/3** sever arms, **4/5** sever legs, **6** blind, **K** kill,
**P** pause/resume AI, **R** reset Titan and human. Buttons: hidden/open-lane target.
Yellow wall blocks sight/navigation; cyan markers show patrol route. Current lab also
has stomps (T toggle /8 manual), unlike this earlier navigation-only phase.

## Important bugs fixed in this phase

1. Unity2021 `NavMeshAgent.ResetPath()` clears isStopped. Stop now ResetPath FIRST,
   then isStopped=true. Tests verify blind root does not drift.
2. Visual inspection caught animated kinematic bone interpolation restoring stale
   WORLD poses while the nav root moved. The first root-only tests missed this!
   `ModularTitanBody.Initialize()` and `TitanPrototypeBuilder.AddBody()` now set
   **RigidbodyInterpolation.None**. Regenerated body prefab for that setting,
   preserving its model/rig/GUIDs. Dynamic activation already clears interpolation.
   New saved-scene integration test advances NORMAL physics and checks hips/root
   alignment and all ragdoll bones' local positions during real movement.

## Earlier navigation verification

**14 passed / 0 failed**, **2026-11-02 02:07:19–27 UTC**, Unity2021.3.25f1.
Six existing body/mesh/UV/stun/dismemberment/stable-death/leg-orientation tests plus
8 AI and saved-scene checks. Wall-routing test moves the root past the obstacle in
5.06 simulated seconds; patrol advances after2.30m travel. Ragdoll maxspeed11.37m/s,
finalmaxjointgap .007m, hipsY .79m.

- `Assets/Tests/TitanPrototype/PlayMode/TitanPatrolChasePlayModeTests.cs` (7 new tests)
- `.../TitanAISandboxScenePlayModeTests.cs` (1 scene startup/reset/visual-follow test)
- Existing `.../ModularTitanPlayModeTests.cs` (6 tests)
- Runner `tools/run_titan_validation.py` now copies both lab scenes.
- Run `python tools/run_titan_validation.py --wait` in disposable project only.
- Reused test project: `C:/Users/maxsu/AppData/Local/Temp/ODM-TitanValidation-fh_v2o9d/`
  (`test-results.xml`, `validation.log`). Latest completed process PID57180.
- Builder structural validation also passes:17 bodies,16 joints,4 branches,
  14 triggers, Generic idle/walk,3 attack sockets.
- Actual AI lab screenshot: `ArtSource/Titan/Reference/TitanAISandbox_Unity.png`.
  Recaptured and visually checked AFTER the interpolation fix; visible Titan
  now follows the root correctly (hips offset remains exactly (0,5.65,0)).
- AI lab left open in EDIT MODE after the final live visual check. Latest
  structural query also confirmed all17 authored bodies have interpolation=None.
  Inspect scene dirty state before changing/saving anything.

## Art / foundation retained

User disliked separate bulging anatomical primitives, supplied
`C:/Users/maxsu/Downloads/refImage.jpeg`; accepted reference-inspired **leftmost
stocky dark-haired Titan**. Continuous torso, rounded block limbs, integrated face,
wider head, short neck, grin, closed hair cap. 15 closed skins,22 exact original rest
bones,78,888 triangles,2K PBR atlases. No individual finger rig/facial blendshapes.

- `ArtSource/Titan/Reference/TitanReference.blend` / `.fbx` / `Textures/`
- `.../TitanReferencePreview.png` Blender T-pose
- `.../TitanReference_Unity.png` checked Unity idle-pose art preview
- Active model `Assets/Art/Titans/Prototype/TitanPrototype.fbx`
- Active texture filenames still `.../Textures/TitanDetailed_*.png`, with reference maps.
- Prefab `Assets/Prefabs/Titans/ModularTitanPrototype.prefab`
- Passive original lab `Assets/Scenes/TitanPrototypeSandbox.unity`
- Old art backups outside Assets: `ArtSource/Titan/DetailedBackupBeforeReference/`,
  `ArtSource/Titan/BlockoutBackup/`. Never import backup Unity GUIDs into Assets.
- `tools/blender/build_reference_titan.py`, configurable `bake_detailed_titan.py`.
- Canonical Unity skin names Titan_<part>, source supports TitanReference_/TitanDetailed_.
- Mesh replacement explicitly copies native geometry/UV buffers preserving GUIDs;
  CopySerialized left stale buffers and must not be restored as the only copy step.
- Whole branch shoulder/hip cuts only; detached leg pose is preserved through
  Animator.Rebind (do NOT hardcode a 180-degree flip).

## Boundaries / next steps

**Production consumption/respawn, real-player health/hold/release, swept sword hits
and actual ODM rope/boost integration remain undone.** Ground combat was visually
approved/committed. Physical3D aerial interception, release and bite now work in the
isolated controller-proxy lab, with automated and sampled MCP review; see top. Leg loss now animates into
kneeling/prone, but crawling, regeneration and getting back up remain undone.
Flat-lab airborne projection is not proof of
multi-floor navigation. Production level bake, dynamic corpse avoidance, crowd
behavior and authored animation polish are future work.

Next: optional user review via F in the lab; then implement/test the real ODM motor
adapter (rope/boost/coroutine suspension, momentum, rotating controller, health and
held sword policy). Do not claim full Titan AI or actual player/sword integration.

## Environment / safety

Root `C:/Users/maxsu/OneDrive/Documents/ODM Attempt # 1`.
Another Unity6 editor for elderash may be running: identify ODM by project path.
Heavily dirty tree predates our work; never revert unrelated changes. Five snapshot
commits plus the new combat commits listed above; unrelated changes remain uncommitted.
Never rerun removed Editor-preview-scene physics tests (crashed working Editor).
Use disposable runtime tests. Standard dedicated additive scene authoring is fine.
MCP Unity v10.3.0 endpoint localhost:8080; Blender socket JSON execute_code localhost9876.
Existing unrelated legacy `RagdollController` unassigned-animator error and stale
MCP disconnect logs may exist; don't claim the console globally clean.

Docs: `docs/titan-bite-combat.md`, `docs/titan-grab-combat.md`, `docs/titan-stomp-combat.md`, `docs/titan-motion-overhaul.md`, `docs/titan-injury-animation.md`,
`docs/titan-patrol-chase.md`, `docs/reference-titan-model.md`,
`docs/modular-titan-prototype.md`, `docs/titan-prototype-validation.md`.
