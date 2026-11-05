# Player gravity that uses the Inspector setting

Unity2021.3.25f1. Local/uncommitted after checkpointb6447972; preserve all existing
runtime/spawner work, user Movement.cs formatting and user-saved FreeRoam changes.

## Current follow-up: customizable jumping

See **docs/player-jumping.md**. Jump ascent now ALSO uses Gravity Constant, with editable
height4m/delay.1s/cooldown.15s and no frame-based lift. Latest **209/211 pass** (same2
Run failures),2026-11-05 02:03:56–02:04:15UTC.8 additional ballistic jump cases pass.
User has since saved gravity16 (not25000). Scene/user values preserved. Validation
section below records the earlier gravity-only milestone.

## Settings

Select the player in Edit Mode, **Movement > Gravity**:

- **Gravity Constant**: POSITIVE downward acceleration in m/s². Default9.8; try20–30
  for a heavier fall.0 adds no acceleration; existing falling velocity still coasts
  (zero acceleration isn't a stop). Negative values act as0. Nonfinite runtime values
  fall back to9.8 to avoid poisoning the CharacterController.
- **Maximum Fall Speed**: gravity-driven downward speed cap, default78.4m/s, minimum.1.
  This doesn't cap ODM thrust/inertia, release clearance or Titan Rigidbody speeds.

During the initial gravity task user's saved FreeRoam contained gravityConstant25000
(extreme, terminal speed in one tick). User subsequently saved **16** while reviewing
the jump follow-up; preserved that value. Changing a value during Play takes immediate
effect next physics tick but must be changed in Edit Mode to persist.
No scene save/reload/input injection/Play stop or global Physics setting change.

## Fixes and scope

`Assets/Movement.cs` previously exposed gravityConstant but never read it. Both free
fall and upward Titan-release braking hardcoded9.8. Completed jumps permanently set a
hidden1.5x gravityScale. Now both paths use the Inspector acceleration directly; that
sticky multiplier is gone. Initially retained the legacy jump lift; that has NOW been
replaced by the requested customizable ballistic jump (docs/player-jumping.md), so
Inspector gravity applies to ascent too. C control remains unchanged. Hook/gas/boost
acceleration, strafe, speed limit, inertia and visuals unchanged.

Native Below collision clears accumulated fall speed, including when ODM is disabled.
Stepping off another ledge starts from fresh falling speed, not retained terminal speed.
Native Above cancels upward release momentum; downward acceleration resumes next tick.
ResetGravity (already used on powered gas) clears both old fall and release-up velocity,
so reactivating gas doesn't compete with stale throw momentum. zeroGravity (ground
support helper) clears ONLY falling, preserving an upward throw clearing the floor.
Suspended/disabled CCT never integrates/accumulates gravity; release initializes incoming
velocity through the existing owner-token path. Per-frame gravity Debug.Log removed.

`Assets/ODM_Movement.cs`: existing ground probe can overlap floor before native CCT
contact. Previously it erased fall speed every render frame across that near-floor
gap. Only zero it on actual controller.isGrounded now; existing probe-based animation,
input and grounded orientation behavior are retained.

Read-only GravityAcceleration / GravityVerticalSpeed expose the gravity subsystem,
NOT total root velocity (ODMs forces are separate). GravityVerticalSpeed now includes
ballistic jump ascent as well as falling and upward Titan-release momentum.

## Previous gravity-only validation

Full disposable project suite **201/203 PASS**,2026-11-04 **22:31:17–22:31:36 UTC**,
PID52448,18.2195347s. ONLY same2 pre-existing approved-Run recipe assertions fail:
NarutoRunLoopsWithoutBoneSnapAndCadenceMatchesAuthoredFootTravel and
NarutoRunPlantsTheStanceFootAndTrailsArmsBehindForwardTorso. Never regenerated Run,
weakened physics thresholds, or changed global simulation settings.

8 new real CharacterController tests added to TitanODMIntegrationPlayModeTests:
Inspector acceleration + displacement and live edits;0 acceleration rest/coast;
independent terminal cap/extreme25000/negative/nonfinite input; actual landing then
walking off ledge; upward release/same live gravity/gas reset; held + disabled CCT no
accumulation/incoming release; completed legacy jump no hidden multiplier; real ceiling
contact then falling. Motors are temporarily disabled in the owned LOCAL PhysicsScene
and manually fixed-stepped without input; real native CCT Move/collisions are used.
Jump-state test unit-steps IEnumerator only (not gameplay timing evidence).
All8 pass and all prior health/grab/continuity/clearance/navigation/spawner cases pass.
All3 changed code/test files byte-matched the completed validation copy. Tests establish
regression evidence, NOT normal full-speed ODM feel or user aesthetic/tuning approval.
