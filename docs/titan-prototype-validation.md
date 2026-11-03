# Titan prototype validation

Latest validation: **58 passed /0 failed twice**, Unity **2021.3.25f1**, **2026-11-03
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
  stomp and standing grab/hold/release are tested above; bite/eat and actual player
  integration remain undone.
- Held-player ownership, sever-to-release behavior and combat escape sequences.
- Crawl behavior after losing legs, limb regeneration, sounds, effects and performance variants.

The lab buttons validate body APIs, not a finished enemy encounter.
