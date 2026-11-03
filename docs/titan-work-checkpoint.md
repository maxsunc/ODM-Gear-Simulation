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

## Latest request: commit combat and require mid-air ODM grabs

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

## Current grab / hold / release slice

User approved the stomp result and asked for the next step. Added standing,
sandbox-only `TitanGrabAttack`, exclusive bounded hold ownership in
`TitanCombatTarget`, and `TitanCombatDirector` for grab/stomp selection. No bite/eat
or real-player adapter yet. See `docs/titan-grab-combat.md` for API/limits.

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
animation/physics tests are not proof of finished motion/art. Next: review the lab,
then height-aware mid-air ODM interception grabs and hand-to-mouth/bite/eat with
readable contact/cancellation, followed by real-player/sword integration. Mid-air
support is an explicit requirement, not an optional polish item.

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

**Bite/eat, real-player health/hold/release, swept sword hits and actual ODM player
integration remain undone.** Sandbox stomp and standing grab/hold/escape/release
are now implemented/tested, with live visual review still pending. Leg loss now animates into
kneeling/prone, but crawling, regeneration and getting back up remain undone.
Flat-lab airborne projection is not proof of
multi-floor navigation. Production level bake, dynamic corpse avoidance, crowd
behavior and authored animation polish are future work.

Next: user/live visual review of stomp/grab and loaded-pose deaths; add required
mid-air ODM interception with swept contact and appropriate release, plus readable
hand-to-mouth/bite/eat built on current hold ownership; then real-player integration. Do not claim full Titan AI or player sword testing.

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

Docs: `docs/titan-grab-combat.md`, `docs/titan-stomp-combat.md`, `docs/titan-motion-overhaul.md`, `docs/titan-injury-animation.md`,
`docs/titan-patrol-chase.md`, `docs/reference-titan-model.md`,
`docs/modular-titan-prototype.md`, `docs/titan-prototype-validation.md`.
