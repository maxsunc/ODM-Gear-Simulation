# Customizable, gravity-driven player jump

Unity2021.3.25f1. Implements the user's follow-up request to fix/customize jumping.
Source: `Assets/Movement.cs`; tests: `TitanODMIntegrationPlayModeTests.cs`.
Local/uncommitted after pushed checkpointb6447972. No scene/prefab/model regeneration,
forced Play stop/input, global Physics settings or approved Run animation changes.

## Inspector

Select player, **Movement > Jump**:

| Setting | Default | Meaning |
|---|---:|---|
| Jump Height | 4m | Requested height above takeoff on unobstructed, stationary support.0 disables jump. |
| Jump Delay | .1s | C press to takeoff, on physics time.0 launches next physics tick. |
| Jump Cooldown | .15s | Minimum between takeoffs; still requires landing, never a double-jump allowance. |

**Movement > Gravity > Gravity Constant** applies during BOTH ascent and descent.
Greater gravity keeps the chosen height but makes the jump quicker. Height uses launch
speed `sqrt(2 * gravity * height)`. Changing gravity AFTER takeoff changes the trajectory,
not retroactively its launch speed. Positive gravity required;0g rejects height-based
jumps rather than inventing an infinite ascent. Invalid/nonpositive height rejects jump;
invalid delay/cooldown use safe default. Existing Maximum Fall Speed still bounds only
unpowered downward gravity motion, NOT hook thrust/inertia or Titan Rigidbody speeds.

**C remains the jump key**. Choose settings in Edit Mode to persist them. During work,
user saved gravity16/height4/delay.1/cooldown.15 in FreeRoam; those settings were preserved,
not overwritten. Earlier gravity25000 notes describe the PREVIOUS gravity task, not the
latest user save. No main scene automation or installation required for these fields.

## Fixes

Removed IEnumerator Jump,55-unit scripted lift and120-render-frame decay. Old jump was
FPS-dependent and suppressed gravity on ascent. Now fixed-time launch/deceleration,
continuous gravity, native swept CharacterController collision and clean landing.

- One request/windup/flight at a time; repeated C presses can't stack lift. No automatic
  repeats or input buffering/coyote/air jumps added in this step.
- Short scene-local world-up foot sphere cast (skin tolerance) or native grounded flag,
  not the old .4m animation-overlap box, authorizes takeoff. Ignores triggers/self and
  rejects side walls/over-steep cast normals; saturated16-hit query refuses support.
- Support/control/gravity/height checked again at delayed takeoff. Walk off, power gas,
  hold, disable CCT/component or die -> pending jump can't launch later. Sound only at
  actual takeoff, no delayed coroutine can restart canceled motion.
- Powered gas already calls ResetGravity(false): immediately cancels pending/current
  jump and hands motion to existing ODM. No hook/gas/boost delay/control remapping.
- Horizontal walking unchanged; jump vertical motion no longer hidden inside the walk
  Move call. Idle no-op horizontal Move removed to preserve native support flags.
- Ascending motion integrates `v*dt-.5*g*dt²`, splitting at true apex if rise/fall share
  one fixed tick. Native ceiling cancels ascent; gravity resumes descent. Apex split
  still sweeps UP under extreme gravity so it can't skip a ceiling on a net-down step.
- Native landing clears jump/fall state; cool-down remains effective for short jumps.
  Falling keeps existing gravity behavior/terminal limit. No root motion/visual lift,
  camera transform changes, external forces, hold parenting or momentum clipping.

API `TryJump()` returns whether one request was accepted (may still be in delay).
Read-only JumpPending,JumpLaunchSpeed; GravityVerticalSpeed now includes jump ascent
but is NOT total ODM/root velocity. `_isJumping()` continues supporting the existing
ODM residual-direction handling. OnDisable/suspension cancel jump without healing or
repositioning; existing real held release momentum path remains separate.

## Evidence

Final full disposable suite **209/211 passed**,2026-11-05 **02:03:56–02:04:15 UTC**,
PID39640,19.1112139s. SAME2 known approved-Run recipe failures only. All previous strict
Titan/gravity/registry/spawner cases pass.8 additional cases cover:

1. Measured apex at3 height/gravity combinations, native landing and launch formula.
2. Physics-time delay,0 immediate delay,cooldown and duplicate-request rejection.
3. Air/trigger/wall support rejection and no stacked airborne jump.
4. Leaving ground during windup cancels, freefall continues.
5. Real low ceiling stops rise and returns to floor.
6. Powered reset,component disable,held suspension/death cancel requests; gas mid-ascent
   takes over without stored lift.
7.0/negative/nonfinite height,0 gravity,disabled motor/controller and safe timing fallback.
8. Apex consistency at.01/.02/.05s physics steps without global timestep writes;25000g
   native ceiling at3.99m is still contacted within a single tick.

Existing completed-jump/no-hidden-gravity-multiplier regression adapted from obsolete
manual IEnumerator stepping to real ballistic jump/landing, preserving its original
post-jump fall-acceleration assertion. First208/211 run had one NEW fixture error:
spawned CCT initially inside its .08m support skin, then compared settled landing to
that unsettled pose. Fixed fixture to settle FROM ABOVE before measuring; unchanged
.02m landing/.035m apex assertions. No physics threshold/tolerance weakened.

Owned local PhysicsScene/native CCT,deterministic manually invoked fixed steps,NO
keyboard injection. Does not establish ordinary full-speed ODM feel or aesthetic
approval. Main Editor Play/scene left alone; latest source/tests byte-match completed
validation. No new commit/push; preserve all preceding dirty runtime/spawner/user work.
