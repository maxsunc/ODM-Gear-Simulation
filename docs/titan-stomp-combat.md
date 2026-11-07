# Titan combat, first slice — telegraphed stomp

**Current gameplay follow-up:** [Player health and sized stomps](player-health-and-stomps.md)
repairs size geometry/chase stops, uses60HP/1.6s gameplay stomps with pooled dust
and rock debris, adds the real HP HUD and removes gameplay circles/debug console.
Latest follow-up REMOVES regeneration and adds10HP/s confirmed grab damage. The40HP lab timing and
verification below remain historical/standalone defaults, not current gameplay UI.

## Current ground-attack reaction tuning

`Assets/Titan/Prototype/TitanCombatDirector.cs` now exposes these Inspector settings:

| Field | Previous hard-coded delay | New default |
| --- | --- | --- |
| Ground Settle Duration | .12s | **.06s** |
| Failed Attack Retry Delay | .2s | **.05s** |
| Post Attack Decision Delay | .65s | **.2s** |

These control the pause **before selecting** a ground attack, not the stomp motion.
The post-attack pause begins after recovery; it is not an extra initial-approach wait.
Fresh gameplay Titans receive these defaults through their runtime-added director.
Select a live Titan's **Titan Combat Director** to try different values during Play;
those runtime Inspector edits do not persist after Play. Edit the script defaults
for a persistent change. Invalid non-finite values fall back to the new defaults;
negative values clamp to zero, without bypassing the actual stopped/braking check.

**Wait Reason** reports selection status in the Inspector, including braking,
planting, decision pauses, ongoing attacks and failed ground choices. It identifies
an active stomp cooldown when a ground choice fails; other failed choices list
possible eligibility blockers, not a confirmed diagnosis of the user's stall.

Stomp wind-up **.65s**, strike **.14s**, recovery **.5s**, gameplay damage **60HP**,
gameplay cooldown **1.6s**, attack chance, movement ownership, sight, facing, range,
physical ground/cover checks and injury cancellation are unchanged. Aerial grab
opportunities still bypass ground decision/settling delays. Multi-second approach
stalls can still reflect eligibility or cooldowns, not these short reaction timers.

Validation: **41 existing checks passed / 0 failed / 0 skipped** in the closed
Unity 2021.3.25f1 worker: `TitanStompPlayModeTests`,
`DirectorSelectsExclusivelyWithoutConsumingGameplayRandomAndPauseReleasesGrab`,
`AerialOpportunityBypassesAnExistingGroundDecisionRetryDelay`, and
`TitanRuntimeRegistrationPlayModeTests`. No test code or assertions changed.
This is a filtered regression run, not full-suite or hands-on approach-feel proof.
See `Library/TitanValidationRun.json` for the latest worker/XML/log paths.

## Historical first-slice scope

Implemented only in `Assets/Scenes/TitanAISandbox.unity`, through its runtime demo
setup. The reusable body prefab and passive body lab do **not** acquire attack AI.
Legacy gameplay scenes/player wiring are not modified by this phase.

## Behavior and controls

- An intact-legged Titan chases, brakes, plants, then may stomp a visible nearby
  ground target. The head/scene-local ray must also have fresh unobstructed sight.
- Yellow-to-red landing ring locks when the wind-up starts; it does not follow a
  dodging player. The foot has a4.1m placement reach and a clearly marked2.4m impact
  radius. Selection range6.4m works with the existing6m chase stop distance.
- Fixed **.65s wind-up**, **.14s strike**, **.5s recovery**, then **2.2s cooldown**.
  Motion-style pace variation never randomly shortens these telegraphs.
- Forty damage at one contact event. No continuous damage trigger or delayed coroutine
  survives cancellation. Long frames cannot skip or duplicate the impact event.
- Damage queries deduplicate compound colliders, reject targets above the low impact
  cylinder, respect solid cover, fail closed on buffer overflow, and only damage
  receivers belonging to this lab scene. Ground/sight queries ignore other scenes.
- Blindness, either leg cut, death, disabled attack/AI, or unavailable pre-impact target
  cancel immediately. Arm loss does not prevent a stomp. Detached bones are never
  posed. A killing stomp still completes recovery rather than snapping idle.
- AI keeps perception/memory running while an owner-checked movement lock prevents
  repathing. Disabling/destroying the owner cannot leave navigation permanently locked.

The later [grab slice](titan-grab-combat.md) adds selection/hold/escape; current
**T toggles auto combat**,9/0 requests left/right grab,E escapes. Timing below is
still stomp's contract.

**WASD / Shift** move/sprint; dodge the marker. Originally **T** toggled auto stomps;
**8** requests a left stomp (still requires valid sight/range). **P** pauses AI and
cancels the attack. **2/3** arms, **4/5** legs, **6** eyes, **K** nape death, **R** reset.
HUD shows attack stage/cooldown/result and proxy health. The proxy stops moving at
zero health; R resets health and Titan. No real player death/reload is wired here.

## Implementation

- `TitanStompAttack.cs`: opt-in state machine, fixed landing point, two-bone leg posing,
  planted support foot, body load/lean, arm counterbalance, and one bounded damage query.
- Animator remains enabled and supplies a fresh base each frame. Attack LateUpdate50
  overlays motion20; injury100 has final priority. Recovery blends the IK targets home,
  not solver strength, to avoid sinking the support foot as the pelvis rises.
- `TitanCombatTarget.cs`: sandbox-only receiver independent of `PlayerHealth` and the
  legacy assembly. It tracks HP, hit count and last impact, with a future adapter seam.
- `TitanPatrolChaseAI` exposes owner-checked movement-lock acquire/release. Attacks do
  not disable the brain, make the player visible through cover, or follow hidden targets.

## Verification

**40 passed /0 failed twice**, Unity2021.3.25f1, 2026-11-02 23:21:38–47 UTC
and23:24:32–40 UTC. All28 prior checks plus12 new checks.

The new tests exercise readable timing, exact-once and skipped-frame contact, dodging,
compound/AoE/airborne filtering, cover, blindness, both leg roles, arm loss, invalid
state, query saturation, mirrored foot support through recovery, navigation locking,
and nape death at multiple phase offsets. See
[titan-prototype-validation.md](titan-prototype-validation.md) for final run status.

Death handoff also rebases positional joint anchors from the current world pose:
non-physical animated spine/shoulder intermediaries can move physics bones away from
their old bind-pose anchors. Retaining those old anchors creates constraint error.

A phase/foot matrix exposed late velocity spikes with the original hard angular
stops in loaded contact configurations. Dynamic activation now uses compliant,
damped angular stops (spring=bodyMass*80,damper=bodyMass*12), retaining the original
angular ranges, masses, solver settings and angular cap. No linear velocity cap,
global physics changes, extra bodies or relaxed test thresholds were used.
All8 death cases now peak at12.01–12.27m/s; no-force handoff peaks at.000113m/s.
Internal collision exclusions are checked throughout simulation, not just at activation.

**Live visual playback review remains pending** because the harness Unity tools were
unavailable. The automatic tests validate geometry/timing/physics, not finished art
direction. Keep this new phase separate from the five committed snapshot chunks.

## Remaining work

This is procedural prototype attack motion, not a finished authored stomp clip.
Aborted attacks lack dedicated recovery clips. Production terrain/foot IK, sound/dust,
camera feedback, real-player damage integration, hand-to-mouth/eat, production selection
variants, corpse/player interaction and crowd/physics performance tuning remain.
