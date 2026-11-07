# Titan motion overhaul — faster, less uniform movement

The user requested faster, more Titan-like, less robotic and less predictable motion
before starting attacks. This revises **all existing animated states**, not just the
Animator's global speed. Death remains physics-driven and is not artificially sped up.

## What changed

- New native **Idle / Walk / Run** clips on the existing Generic rig. Walk and run
  use distinct contact/pass/recovery key poses, not the same sine wave at two speeds.
- Walk cycle **1.02s** (previously1.6s), run **0.68s**, idle **2.6s**.
- Faster recovery than support, stronger running knee lift and bent-arm counter-swing,
  torso twist/forward lean, shoulder motion, pelvis sway and foot-support correction.
- Walk/run BlendTree uses measured planar root speed. Its cadence follows actual
  travel speed; a stopped chasing Titan does not keep running in place.
- Transitions shortened to90ms start /100ms stop. Global `Animator.speed` stays1.
- Head/neck/shoulders lead sharp turns by about85ms; the root then pivots briskly.
  Sharp corners reduce travel speed rather than skating sideways at full run speed.
- Arm severing produces a short chest recoil. Eye hits snap the head first, then
  stagger the hands into the eye-cover pose (roughly0.15–0.2s). Repeated eye hits
  restart the recoil and alternate the leading hand.
- One-leg collapse now **0.58s**, prone collapse **0.76s**. A short buckle is followed
  by a quick drop, a small damped impact overshoot and settling. Arms brace a little
  earlier and the head follows, rather than every bone smoothly arriving together.
- Existing final kneeling/prone poses, helpless flags, dismemberment and nape death
  remain intact. Death immediately cancels any alive motion overlay.

These are reproducible generated key-pose clips and runtime overlays, **not bespoke
mocap or finished hand-animated production clips**. They provide an improved tunable
motion foundation; art-direction review in motion is still needed.

## Controlled variation

`Assets/Titan/Prototype/TitanMotionStyle.cs` owns only motion styling, never target
selection or sight detection. Each Titan gets a local random stream:

- Per-instance cycle offset, cadence bias, turning bias and posture variation.
- Coherent pace segments lasting1.7–3.5s, rather than random noise each frame.
- Patrol pace factor0.90–1.08; chase factor0.94–1.20.
- Occasional brief chase hesitation targets0.52–0.66 for0.12–0.22s, followed by a
  committed segment. Acceleration and interpolation prevent instantaneous speed jumps.
- Corner factors0.68 on sharp turns and0.38 on very sharp turns.

The base speeds are now **4.6m/s patrol /9.5m/s chase**, with lab acceleration26m/s².
Chase bursts can request up to11.4m/s before cornering/braking. The human proxy's
14m/s sprint still allows escape. Sight, memory and destination validation are unchanged;
variation does not permit tracking hidden targets or walking without legs.

Use a nonzero `styleSeed` for a repeatable individual, or0 for an instance-derived
seed. `ResetStyle(seed)` is also available. `varyPace=false` disables pace modulation.
The random stream never consumes `UnityEngine.Random`, so styling does not perturb
other gameplay systems. Clip phase/cadence stay desynchronized between individuals.

## Files and ownership

- `Assets/Editor/TitanMotionClipBuilder.cs`: generated controller and contact-aware
  key-pose authoring. Reuses existing Idle/Walk/controller asset paths and GUIDs;
  adds `Assets/Art/Titans/Prototype/Animations/Run.anim`.
- `TitanPrototypeBuilder.BuildAssets()` delegates to that generator and attaches
  `TitanMotionStyle` plus the existing `TitanInjuryPose` to the body prefab.
- Animator parameters: `IsWalking`, `Gait`, `StrideRate`, `CycleOffset`.
- Style Update runs after AI to evaluate actual speed; style LateUpdate adds turning
  and posture, except while blind, injured or dead. The body owns eye reactions;
  injury posing has final priority and disables the Animator on leg loss.
- Agent steering still uses complete validated NavMesh paths. Styling owns root
  rotation only when an agent exists and restores its original updateRotation flag
  when disabled. Head direction comes from known path steering, not a hidden player.

### Foot support correction

Visual checks exposed oversized vertical foot boxes: the foot bones point diagonally
toward the toes, so directly fitting an axis-aligned box on those bone axes caused
visible hovering. Each foot now uses a correctly oriented child `FootPhysicsShape`
with a compound collider owned by the **same foot Rigidbody**. Damage triggers follow
that shape. There are still17 bodies,16 joints,14 damage zones and22 source bones.

The generated gait computes support from these correctly aligned foot shapes.
Tests also check actual world-space skinned soles, not just collider contact.
This is contact-aware **clip authoring**, not full runtime foot-plant IK. Slopes,
steps, production terrain, precise horizontal foot locking and landing reactions
still require future animation/IK work. The lab's NavMesh voxel-height offset can
also leave a small root-to-ground gap; it is not a promise of perfect terrain contact.

### Rebind safety

Severing now preserves the surviving local pose as well as the detached branch,
current Animator state/time and float parameters. A cut must not erase running hip
motion or reset the individual gait phase. The detached branch is never written by
the style or injury overlay after it leaves the body hierarchy.

## Try it

- `Assets/Scenes/TitanAISandbox.unity`: WASD moves human; Shift sprints. Watch the
  HUD's measured speed, run blend and pace factor while provoking a chase/turn.
- `Assets/Scenes/TitanPrototypeSandbox.unity`: **1** idle/walk, **7** chase-run preview.
- In both labs, **4/5** test quick mirrored knee/prone collapse, **6** eye recoil,
  **2/3** arm loss, **K** death, **R** fresh Titan.

Actual Unity walk/run pose comparison:
`ArtSource/Titan/Motion/TitanMotion_Unity.png` (poses, not a video).

## Verification

**28 passed /0 failed**, Unity **2021.3.25f1**, **2026-11-02 04:00:16–24 UTC**.
All21 previous foundation/AI/injury checks plus7 motion checks:

1. Gait and cadence respond to measured speed, not chase intent alone.
2. Local variation is bounded, smoothly changing, repeatable by seed, distinct across
   seeds and independent of Unity's global random state.
3. Walk/run are distinct fast clips with continuous loop endpoints, bounded pelvis
   motion, foot-collider support and visible sole clearance throughout sampled cycles.
4. Head leads a sharp root pivot; leg loss prevents subsequent root turning.
5. New collapse timing has buckle/drop/impact and finishes within the intended time.
6. Running cuts preserve surviving pelvis pose, severed pose, gait and cycle offset.
7. Eye hits recoil promptly and cover the eyes quickly.

The normal-physics scene test now allows the **authored bounded hip bob/contact**
translation while still checking horizontal root alignment and unchanged local
positions on the other ragdoll bones. The old two-sample walk test was replaced with
a full-cycle excursion check because randomized cycle offsets can make two arbitrary
samples symmetric despite a working gait.

Intact-death max speed12.54m/s, final joint gap .004m, hipsY .95m. Injury death max
speeds5.89m/s (prone collapse) and9.57m/s (kneeling). Wall routing still works
(4.23 simulated seconds to pass the obstacle in this run).
Tests remain in the disposable project via `tools/run_titan_validation.py --wait`.

## Still not implemented

Later sandbox slices now add [stomp](titan-stomp-combat.md) and
[grab/hold/release](titan-grab-combat.md) independently of the motion component.
The later [ground bite](titan-bite-combat.md) adds hand-to-mouth/contact damage too.
Aerial grabs, production eating art, real-player damage/hold/release, swept sword integration, authored attack
variants, final production animation/IK and multi-Titan crowd tuning remain undone.
Future attacks should have unpredictable **selection**, but readable wind-ups/contact
windows; motion variation must never randomly shorten a damage telegraph.
