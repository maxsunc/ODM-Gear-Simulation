# Aerial interception prototype — physical hand contact

The user approved the ground bite/arm fix. Committed locally as **28cb997a**;
not pushed. Aerial work below is the next, separately reviewable slice.

**Scope:** an actual3D hand interception in the isolated AI lab, using an airborne
CharacterController proxy. **Not yet the production ODM rope/boost/health adapter.**
The existing ground grab and passive body prefab remain unchanged in purpose.
No gameplay scene, player prefab, rest rig, imported art or physics tuning changed.

## Mouth-carry torso clearance

User approved the fast grab, then reported the arm crossing the body on the way to
the mouth. MCP reproduced the elbow folding behind/inside the chest: sampled
forearm/chest penetration1.43m, palm1.32m. Fixed only the post-contact aerial carry:

- Blend the elbow pole forward/outward during chest carry, not during the fast catch.
- Lift the hand in front of the chest before moving inward: smooth.9m forward/.35m
  upward clearance arc, unchanged.7s transfer duration.
- A foot/side catch may leave the person in front of the palm. A WORLD-fixed offset
  then pulls the palm inside the chest at the mouth. The held person now turns
  smoothly in yaw around the SAME grip point into a side-on carry. Exact local
  grip point and root-to-grip distance are preserved, with no parenting, recentering,
  instantaneous regrip or capsule tilt. Ground following is unchanged.
- Mouth approach is.6m ahead of the socket, still requiring the existing.55m bite
  sphere to touch actual victim geometry; neither radius nor damage rules enlarged.
- Release velocity now includes the held root's orbital movement as the wrist turns,
  not just socket translation. Existing incoming-velocity decay/clearance gate stays.
- Long-tick bite validation samples the forward arc INCLUDING its changing yaw offset;
  ordinary frames still sweep the actual grip. Conservative original checks remain.

MCP after: forearm/palm penetration0 in the sampled transfer/bite replay, same.175s
catch, one capture/one bite, HP0, release complete. Actual viewed frames1.70/2.15/2.42s
show the forearm in front instead of disappearing into the chest. The connected
shoulder seam is deliberately not treated as a dislocated-joint clearance requirement.
Proof: `ArtSource/Titan/Combat/AerialMouthClearance_Comparison.png`.
Fast0/.22 timings and.25/.65 miss retry remain unchanged. No rig/art/rest pose,
physics tuning, joint limits, gameplay scene or player-prefab edits.

## Approved direction: frequent, fast direct reach — no aerial warning lift

User explicitly requested faster/more frequent aerial catches and removed the
previous warning-arm requirement: being in front of the Titan is already dangerous.
Default is now **0 windup +.22s direct reach** from the actual selected palm, no
raised preparation point and no aerial aim-warning ring. The mouth/bite warning,
approved ground attack timings and physics/contact requirements remain unchanged.
Miss recovery .25s + cooldown .65s yields roughly **1.12s per repeatable miss**, not
the old multi-second wait. Aerial selection bypasses ground retry/stationary gates;
exclusive attack ownership and the aerial cooldown still apply.

## Try the airborne lab

Open `Assets/Scenes/TitanAISandbox.unity`, enter Play:

- **F**, or **Launch airborne crossing proxy**, launches the teal capsule on a
  straight boosted pass across the Titan. Auto combat must be ON (**T**).
- **9 /0** request the left/right aerial variant when the target is airborne;
  otherwise retain the ground grab. Requests can legitimately fail reach/sight.
- **E** escapes; **2/3** sever arms, **4/5** legs, **6** blind, **K** nape kill.
- **R** resets. HUD distinguishes aerial grab and reports blocked release clearance.
- The straight flight is a reproducible interception exercise, not a fake ODM
  rope simulation. Proxy speed is approximately13.8m/s; separate tests cross much faster.

The same held victim can continue through chest hold, hand-to-mouth and the
existing geometry-validated bite. There is no delayed death timer or swallowed mesh.

## Commitment and reach

`TitanGrabAttack.TryBeginAerialGrab` requires current confirmed sight plus a clear
scene-local ray to the visible target; passive bodies have no attack components.
The target must opt into a `TitanAerialMotor`, be airborne (>2.2m above Titan root),
and have a valid active controller. Unsupported targets are refused, **not** given
old ground snap-back release.

Position/velocity are sampled ONCE for aim prediction. Default horizon is now.22s;
configurable prediction remains bounded to1.05s and24m displacement. Seven bounded
time candidates prefer the reach-end forecast but allow a shorter/current reachable
point when the full-horizon forecast has already left range. Every chosen point
must lie inside the selected arm's measured shoulder/elbow/physical-palm reach,
and at least1m in front of the shoulder plane (this is a frontal interception variant).
Arm lengths are never extended. The director tries either surviving hand and brakes
navigation before reaching; it checks aerial opportunities every eligible frame,
even during a ground decision retry delay or before ground stationary-age gating.

Default absolute timeline (latched at selection):

| Stage | Time | Behavior |
|---|---:|---|
| Reach |0–.22s| Immediate direct committed reach; actual swept hand contact can capture once |
| Hold |.22–1.72s| Standing chest carry (8.2m, not the crouched ground carry's6.2m) |
| To mouth |1.72–2.42s| Real arm brings held torso toward mouth |
| Bite warning |2.42–3.02s| Orange warning, fixed jaw opening |
| Bite |3.02–3.14s| Contact-validated closure/damage |
| Recovery |3.14–3.59s| Release and return;.65s aerial cooldown follows |
| Miss only |.22–.47s| Short recovery, then.65s cooldown; ready again around1.12s |

A miss skips captured phases. After a catch the hand stops at the contact pose
until carry begins. The victim's ACTUAL early contact time, not.22s reach end,
starts the bite lease minimum-age check; a long tick cannot gain extra hold time.
Selection-frame delta is not consumed by the attack or freshly launched proxy.

`LockedPoint` never follows a dodging or hidden player. Later position samples
exist only for physical collision detection. This is not homing. Changes in a swing
trajectory can defeat the committed intercept. Target replacement/pause, blindness,
selected-arm loss, either leg loss, death and disable still cancel. Other-arm loss
alone does not. Injury/ragdoll owns surviving bones after interruption.

## Physical contact, including fast crossings

A radius around the detected player is NOT a capture condition. The actual enabled
nontrigger hand BoxCollider defines an oriented box. Its transform/half-extents
rotate with the posed arm. Aerial IK iterates the small rotating grip-to-palm offset
so the PHYSICAL palm, not an offset socket, aims at the forecast.

`TitanAerialContact` computes exact line-segment/OBB distance by splitting at box
face crossings and solving each piecewise quadratic. Capsule radius is then applied
for the configured human shape. Conservative advancement bounds relative translation
plus hand angular motion. Contact can occur with BOTH observed endpoints separated,
or while the hand rotates through a target between separated endpoint poses.

The attack samples its committed analytic hand arc at up to1/120s subdivisions,
sweeps the rotating box against the human's observed relative motion, then evaluates
the actual arm pose again at time of impact. It rechecks the actual box/capsule gap
before acquisition. Numerical tolerances are.00001/.0001m, not enlarged catch radii.
The solver has bounded iteration counts and fails closed. Scenery queries are
scene-local,64-hit bounded, saturation fails closed. A circumscribed hand sphere
is used conservatively for BLOCKING scenery only, never to enlarge capture.
The original MCP review exposed a123.67deg moving shoulder-roll singularity. The
initial fix used an outward/down pole; fast no-preparation reaches now transport
the arm's ACTUAL selected anatomical hinge plane toward the goal instead, avoiding
a preparatory roll into another pole. Grip-offset iterations still restart from
the same base. Early CCD contact now caches the exact caught upper/lower rotations:
re-applying fractional reach weight to that already-partial pose caused a12.61deg
catch-frame pop. Carry starts from the caught pose, smoothly takes over, and returns
to animated idle during recovery. Ground mode retains its approved solver/pole.
All continuity assertions remain strictly<8deg/120Hz, including the initial reach.

The human is moved back only to its physical CCD contact time when a fast crossing
ends beyond the hand. Its contact offset from the grip is preserved: there is no
additional center-to-socket snap and no skeleton parenting. Mouth posing compensates
the saved offset by moving the ARM. During transfer an owner-checked yaw rotates the
held root around the same grip point; the local attachment and offset LENGTH stay
fixed, rather than incorrectly freezing their world direction. Offset/yaw remain
latched through lethal release/recovery, preventing a wrist pop.

Current human support is a rigidly attached, upright centered CharacterController;
yaw is supported. Capsule tilt changes between observations fail closed. Compound
solid targets and rotating/tumbling humans need a richer controller adapter.

## Airborne hold and clearance-safe proxy release

`TitanAerialMotor` is the explicit suspension/release contract. `TitanSandboxFlight`
is its LAB implementation; a production motor must own its real ropes, boost,
coroutines, gravity and input. Suspending a script alone is not sufficient.

`TitanCombatTarget` retains exclusive ownership, disabled-collider/RB-state capture,
world-space follow, independent escape and bounded lease. On airborne release it
NEVER restores the pre-grab world pose. The proxy searches21 bounded outward/upward
candidates near its CURRENT held pose. It checks the full configured capsule and
sweeps its exit against world geometry. Only the catching Titan is excluded during
the exit sweep; endpoint clearance includes that Titan and all other actors.

If all candidates are blocked, ownership clears immediately but physics/controller
remain suspended (`ReleasePending`). Independent target/motor updates retry after
clearance changes; no delayed damage is possible. Removing the target component
transfers the pending release continuation to its surviving motor. Never parent a
human to a detached arm, and never keep following its socket after release.

On successful release original collider/RB flags restore. The proxy drops with
`incomingVelocity * exp(-2 * holdAge) + measuredHeldRootVelocity`, then gravity resumes;
boost ends. There is no linear-speed cap. Its incoming velocity is measured from
actual controller movement, not requested movement through a blocked wall.
Ground mode retains its original approved snap-back policy, separately.

Limitations: the carry path still sweeps the grip, not the entire human; world
penetration can therefore require the pending-clearance gate. The adapter must
outlive pending release. Do not destroy/replace the motor during a blocked lease.
Moving terrain, dynamic obstacles/crowds, steps and real death/respawn need further
integration. No production controller-restoration claim is made by the proxy.

## Validation

**108/108 passed twice**, Unity2021.3.25f1:
- **2026-11-03 14:40:20–36 UTC**, PID34496.
- **2026-11-03 14:44:01–16 UTC**, PID37808,15.493s.

All106 previous checks +2 new; static four-case and moving torso penetration0.
Latest maximum120Hz steps: fast6.68deg, moving6.31deg, configurable old-timing5.32deg;
all<8 unchanged. Loaded aerial mouth death10.83m/s (<40 unchanged); intact11.26m/s,
gap.007m,hipsY.86m. Final MCP moving catch/bite still.175s, all3 arm bones max6.18deg,
HP0, release complete, forearm/palm penetration0. No rig or physics tuning changed.

The torso fix adds a both-hand/two-animation-phase physical clearance regression,
strengthens the moving fast catch/bite with the same clearance assertion, preserves
the exact grip point/radius through held yaw, and checks long-tick arc obstruction.
Forearm and palm versus chest/spine use actual nontrigger collider geometry via
`Physics.ComputePenetration`, with<.005m tolerance; shared shoulder attachment is
not required to separate. Existing<8deg/120Hz and<40m/s assertions remain unchanged.
Final run evidence is in `titan-prototype-validation.md` and `titan-work-checkpoint.md`.

Previous fast-reach baseline: **106/106 passed twice**, Unity2021.3.25f1, owned disposable PlayMode project:
- **2026-11-03 14:02:10–26 UTC**, PID31208.
- **2026-11-03 14:04:21–38 UTC**, PID30596, duration16.708s.

All101 previous checks +5 new: zero-delay/current-palm start/no upward warning
excursion, fast dodge/miss, roughly1s retry, full moving fast catch/bite continuity,
and ignoring a still-active ground decision retry delay. Existing explicit legacy
timeline tests retain configurable.7/.3 timings but use the new direct path; the
saved-scene test now waits for real reach range rather than selecting from far away.
The crossing test follows the actual new hand pose, not the removed raised pose.
A currently reachable fast flyer is now eligible even if its full forecast exits
range; selection still cannot capture without contact.

Latest120Hz maxima: fast static6.43deg (contact.158s), fast moving6.48deg, configurable
legacy timelines4.86deg; all<8. Fast moving uses the authored1.8m/.32m CCT and owned
local runtime physics. Aerial mouth death **8.31m/s**, unchanged<40 assertion. Ground
mouth deaths10.46/10.36/11.82/11.81m/s; intact11.14m/s,gap.005m,hipsY.84m. No physics
tuning, angular-limit changes, velocity caps or weakened assertions.

**MCP visual spot check:** working Editor normal sandbox Play Mode, explicit120Hz
steps with actual CharacterController movement. One physical catch, one bite,
proxy HP0, release complete. New direct reach captured at **.175s after selection**;
all3 selected arm bones max **6.18deg/120Hz**, measured from the first reach frame
through carry/bite/recovery. Reviewed current-pose start,100ms reach,220ms captured
pose and mouth transfer. Proof: `ArtSource/Titan/Combat/AerialGrab_DirectReach_MCP.png`.
The older warning-arm proof remains historical at `ArtSource/Titan/Combat/AerialGrab_MCP.png`.
Temporary camera/textures destroyed; Editor returned to clean, unpaused Edit Mode,
no scenes saved. This is not final full-speed/finger animation or real-gear approval.
All74 approved ground/bite/physics tests remain included and unchanged in strength.
New checks cover both hands, physical corner/rotating-box contact, fast relative
crossings, fixed aim/dodge, real reach, missing motor, cover, layer masks, saturated
queries, escape, lease/long tick, blocked releases, component removal, disable,
limb/eye/death handoffs, bite, arm continuity and saved-scene normal-frame flight.
Never run the removed Editor-preview physics validator.

## Next: production ODM integration

Recommended order after user review of this carry correction:
1. Checkpoint/commit only this aerial slice and proof/docs when requested; keep the
   unrelated dirty tree out. No push requested.
2. Make a separate REAL-player integration lab/variant, not an edit of FreeRoam.
   Implement a default-assembly `TitanAerialMotor` adapter for actual controller,
   ropes, boost, gravity, input, camera/look and active coroutines. Suspend/resume
   explicitly; restore current safe world pose plus correct motion, never snap back.
   Invalidate attack motion history on real teleports/respawn/warps.
3. Connect actual player health/death/respawn and hold cleanup. Define rope cutting
   or reconnection, escape and held-sword policy; a held player should have deliberate
   counterplay rather than inheriting the proxy's blanket collider suppression.
4. Verify real sword references and add swept blade contacts at ODM speeds. Exercise
   nape kills, eye stun, limb cuts and cutting the catching arm to release the player,
   with per-swing deduplication and no tunneling/remote damage.
5. Stress the complete loop at full swing/boost speeds, above/side/low approaches,
   terrain/edges/cover and multiple Titans, then animation/finger/sound polish.
   Jumping Titans, crawling/regeneration and production swallowing are later abilities,
   not substitutes for finishing the real player/gear combat loop.

Inspected `Assets/Movement.cs` and `Assets/ODM_Movement.cs`; no legacy edits made:

- `Movement` owns private gravity/jump velocity and a running jump coroutine.
- ODM owns hook points/lines, boost/audio/particles, current/residual velocity,
  strafing, `Deaccelerate`/`InertiaCheck` coroutines and shared gravity flags.
- Disabling a MonoBehaviour does not stop its running coroutines. A held player
  needs explicit suspension APIs, not just `enabledMovement=false` or CCT disable.
- A default-assembly adapter can implement the prototype motor contract without
  coupling the isolated prototype assembly to `Assembly-CSharp`.
- Decide rope cutting/reconnection, release momentum and landing/input behavior,
  then test actual gear, health/death/respawn and rotating player-capsule behavior
  before wiring any gameplay prefab/scene. Include held sword-counter policy:
  the prototype currently suspends all victim colliders, including weapon triggers.
  Do not reuse ground snap-back.

Standing interception is implemented here; Titan jumping remains a separate ability.
