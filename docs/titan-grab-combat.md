# Titan sandbox grab, hold and safe release

The historical sandbox combat slice after [stomp](titan-stomp-combat.md). Default
body prefab remains passive. Approved prototype work is pushed through940daf1d.
The later user-requested [real ODM/Shiganshina integration](titan-shiganshina-integration.md)
now connects actual PlayerHealth/gear/swords in that one scene (**116/116 twice**),
with motor suspension + safe CURRENT-pose release for real ground AND aerial holds.
It is a separate uncommitted review slice; proxy ground defaults below remain unchanged.

**Later ground follow-up:** the combat lab now opts into
[hand-to-mouth and contact-validated bite](titan-bite-combat.md). This document's
original timing table describes standalone `enableBite=false` mode; the lab adds
.7s transfer, .6s mouth warning and .12s jaw closure before the lethal contact.
E escape and all release/cancellation contracts remain. The subsequent
[aerial interception lab](titan-aerial-grab.md) now has real3D swept hand contact,
with separate airborne release. First real ODM integration is now implemented as
linked above; full-map/terrain/interactive counterplay polish remains.

## Try it

Open `Assets/Scenes/TitanAISandbox.unity`, enter Play, move into the open with WASD.
Shift sprints; arrows orbit the camera.

- **T** toggles automatic combat (grab OR stomp). Turning it off cancels both.
- **9 / 0** request left/right grab, subject to current range/sight/capabilities.
- **8** requests left stomp.
- **E** escapes the proxy's hold immediately. The HUD reports HELD.
- **2 / 3** cut arms; **4 / 5** cut legs; **6** blind; **K** nape kill.
- **P** pauses AI and interrupts attacks on their next tick.
- **R** releases the human, destroys the old Titan/debris and resets the lab.
- The HUD scrolls in smaller Game views. Human-placement buttons release before warping.

Grab's cyan aim ring is a fixed point at human-torso height; it becomes red during
reach. It is distinct from stomp's yellow/red ground landing circle. Dodge away:
there is no target-position homing after the initial commitment.

## Ownership and timing

`TitanGrabAttack` owns the standing grab sequence and selected arm only:

| Stage | Default duration | Behavior |
|---|---:|---|
| Windup | .65s | Deep crouch, planted feet, selected hand loads high |
| Reach | .18s | Hand moves toward the locked aim; exactly one contact event at end |
| Holding | 1.5s | Captured proxy lifted toward chest; first .55s is the lift |
| Recovery | .45s | Release, blend pose/plant targets home |
| Cooldown | 3s | No repeat grab |

A miss skips holding. Durations are latched at selection and never randomized by
attack choice. A long tick cannot skip contact or strand a victim after recovery.
Automatic updates skip the selection frame: its delta elapsed BEFORE the attack
started. A director/input start cannot consume an entire wind-up on a long frame.
Explicit `TickAttack(dt)` still supports deterministic fast-forward tests.
Selection range is 6.4m on the lab ground; contact still requires the actual grip's
1.05m-radius volume to intersect the victim. Selection alone is not a capture.

Contact evaluates the exact authored procedural pose at the contact timestamp,
then restores attached local transforms. It does not accumulate pose overlays or
rewind the Animator clock. Scene-local ray, swept-sphere and overlap checks reject
cover and saturated buffers. Only the intended victim can be captured.

Unity CharacterController queries use a shrunken proxy capsule; it is also not a
reliable target for Collider.ClosestPoint. `TitanCombatTarget.ClosestCombatPoint`
uses the configured upright capsule for combat geometry. The grab explicitly checks
that capsule as well as native overlap results; **the attack radius is not expanded**.
Stomp uses the same helper. Tests cover rounded-controller-only victims, both hands,
NavMesh-height offset and stomp damage, not just a rectangular stand-in.

`TitanCombatTarget` owns the victim-side exclusive, bounded hold lease:

- Only one owner can acquire a victim; only that owner can normally release it.
- A victim can escape independently. Its own disable, death or reset releases it.
- Owner disable/destruction, an invalid/detached socket, scene mismatch or lease
  expiry release it even if the attacker stops advancing its sequence.
- Default independent maximum lease is 4s (bounded .5–10s on acquisition).
- Cached collider enabled flags and rigidbody kinematic/gravity/collision flags and
  detection modes are restored; stale linear/angular momentum is not replayed.
- The proxy input controller does not move a held/disabled CharacterController.
- **Never reparent the victim into the skeleton.** Cutting a hand or destroying an
  owner cannot destroy the human with it. Follow happens in world space after posing.
- Callbacks see cleared ownership first, so cancellation/release is re-entrancy safe.

**Sandbox release policy:** restore the captured pre-grab world pose/rotation. This
is not a physical airborne drop, clearance-searched landing, thrown-player action,
or complete adapter for a real player with custom controller/physics filtering.
The lab is static; moving-geometry release clearance is future integration work.

## Cancellation and animation priority

Blindness, grabbing-arm loss, death, attack/director disable or loss of a valid victim
cancel and release. Losing the OTHER arm does not cancel the selected-hand grab.
Either leg loss conservatively cancels this **standing-only** attack; the injury
module then owns kneel/prone. Single-leg survivors retain body-level arm capability,
but attacks from kneeling need a separate authored pose/ownership phase.

Motion overlay runs at order20, attacks50, target follow70, injury100. Grab never
writes detached branches or restores a standing pose during cancellation/death.
Native Generic animation is the base; the grab is procedural IK/blocking, not a
finished imported clip or articulated finger animation. Both planted feet stay
supported through crouch, lift and recovery in the numerical tests.

## Selection/arbitration

`TitanCombatDirector` is added to lab actors at runtime after both attack components.
It is the sole auto-selection owner and turns off stomp's standalone auto selector.

- Only starts after the agent has braked/settled for .12s with current visible prey.
- Uses a local System.Random (explicit nonzero `decisionSeed` is repeatable), never
  UnityEngine.Random; favors grab 55%, falling back to a valid available action.
- Selects a surviving hand; foot choice is independent of arm availability.
- Waits .65s between completed/cancelled automatic sequences.
- Both attacks also reject each other's busy state even without a brain/director.
- Uses owner-checked AI movement locks; perception may continue but chase cannot
  restart navigation mid-action. No styling/attack query tracks hidden prey.

## Validation of the grab foundation

Current bite-expanded results are in [validation](titan-prototype-validation.md).

**58 passed / 0 failed twice**, Unity2021.3.25f1, in the owned disposable project:
2026-11-03 **01:04:02–11 UTC** and **01:05:48–57 UTC** (latest PID20680).

All40 previous checks plus18 new checks (including the selection-frame time guard): fixed timing, no homing, one contact,
long-frame completion, collider/RB restoration, foreign ownership, escape, lease,
disable/destruction/socket invalidation, limb/eye/death cancellation, cover,
mutual exclusion, controller-only shape geometry, foot support and scene reset.
The director's navigation lock, AI pause and gameplay-RNG isolation are exercised.

Four nape deaths from left/right crouch/carry poses simulate5s each. Latest maximum
speeds: **7.45, 7.83, 11.35, 10.41m/s**, all below unchanged40m/s assertion.
Current death pose is preserved before physics. All8 stomp-death cases still pass;
latest intact death10.95m/s, joint gap.006m, hipsY.85m.

Run `python tools/run_titan_validation.py --reuse /c/Users/maxsu/AppData/Local/Temp/ODM-TitanValidation-fh_v2o9d`.
Never use the removed Editor-preview physics test that crashed the working Editor.

At this58-test foundation checkpoint, live playback tools were unavailable.
Subsequent MCP ground-bite/arm review was completed and the user approved/committed
that work (28cb997a). The aerial lab now also has sampled MCP review. Tests and
stills are not final production animation approval. See `titan-work-checkpoint.md`.
No push requested.

## Required follow-up: mid-air ODM interception grab

The user explicitly requires grabs against **airborne players swinging on ODM gear**.
This is not implemented by the ground-targeted crouch variant: it still rejects
vertical offsets above2.2m. The new aerial variant/motor contract is documented in
[titan-aerial-grab.md](titan-aerial-grab.md); F launches its airborne LAB proxy.
The actual production ODM adapter is not wired yet; do not mistake the lab for that.

Plan this as a height-aware reach/interception variant: a standing Titan can catch
an airborne player inside its actual3D hand reach. A jumping Titan is a separate
ability, not implied by this requirement.

- Use sight-confirmed position/velocity for bounded interception prediction, then
  commit to a readable aim/trajectory. No hidden-player tracking or perpetual homing.
- Keep wind-ups/contact windows fixed and dodgeable. Fast relative ODM/hand motion
  needs swept contact tests, not only one end-frame overlap or a huge catch radius.
- Share exclusive hold ownership, escape, arm/eye/death cancellation and timeout
  safety with ground grabs; keep attacks mutually exclusive.
- Integrate with the real ODM controller: suspend rope/boost/movement deliberately
  while held and restore or safely replace those states on release. Define momentum
  and collision-clear release behavior. The current return-to-pre-grab-world-pose
  policy is **not** a production aerial release solution; don't snap flyers to ground.
- Add above/side/below and fast-crossing tests, missed/dodged interceptions, cover,
  target loss, limb-loss release, escape and safe momentum/controller restoration.

## Still pending

Production consumption art/respawn, escape/clearance-tested release, real ODM player
state/controller/damage adapter, real sword/swept-hit testing,
real-gear integration for the aerial interception prototype, kneeling grabs, production hand animation, runtime
terrain IK and crowd tuning.
Hand-to-mouth and readable mouth-contact damage are now an opt-in sandbox extension.
No asynchronous death timer/coroutine exists; released players cannot be bitten later.
