# Titan combat, first slice — telegraphed stomp

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
