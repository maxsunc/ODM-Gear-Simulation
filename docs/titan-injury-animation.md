# Titan leg-loss injury animations

The user requested an alive injury response before the attack phase:

- **One leg gone:** collapse onto the remaining knee, fold its shin behind the body,
  bow the torso/head and hold the kneeling pose. Mirrored for either surviving leg.
- **Both legs gone:** fall forward from the current pose into a low, helpless pose,
  with the upper back raised, head bowed and arms spread. The REAL nape is now more
  exposed for an airborne finishing approach instead of lying flat behind the
  shoulders. Cutting both legs rapidly also works; no forced standing reset.

These are procedural animated pose blends on the existing 22-bone rig, not imported
motion-capture clips or a Humanoid retarget. They are deliberately separate from
**death ragdoll physics**. Detached branches remain dynamic and are never posed by
this component.

## Try it

Use either `Assets/Scenes/TitanAISandbox.unity` or the original
`Assets/Scenes/TitanPrototypeSandbox.unity`.

1. Press **4** or **5** to sever one leg. Watch the fall into the surviving-leg kneel.
2. Press the other leg key to transition to prone.
3. **6** still applies blindness; recovery cannot make the injured Titan stand/walk.
4. **K** still kills through the nape and transfers the current pose to ragdoll.
5. **R** spawns a fresh intact Titan.

The lab HUDs report `KneelingOnLeft`, `KneelingOnRight`, or `Prone` and the AI lab
reports when the pose is still transitioning.

Current before/after actual Unity captures:

- `ArtSource/Titan/ProneNape/before_Rear.png`, `before_Side.png` (old flat pose)
- `ArtSource/Titan/ProneNape/after_Rear.png`, `after_Side.png` (raised/exposed nape)

Historical injury-phase captures (old flat prone pose is superseded):
- `ArtSource/Titan/Reference/TitanInjury_KneelingRight_Unity.png`
- `ArtSource/Titan/Reference/TitanInjury_Prone_Unity.png`

## Implementation and ownership

`Assets/Titan/Prototype/TitanInjuryPose.cs` is attached by the reproducible body
prefab builder. It subscribes to `LimbSevered`, takes a snapshot of the current
attached skeleton and blends to the corresponding injury pose. Default durations:
**0.58 seconds to kneel**, **0.76 seconds to fall prone**, following the
[motion overhaul](titan-motion-overhaul.md). A short buckle, fast drop, impact and
damped settling replace the original slow uniform1.2s/1.5s smooth blends.

The component caches the exact imported rest transforms before normal animation
starts. Targets use the Titan root's current heading, not a hardcoded world heading.
The root remains fixed, allowing the AI to stay immobilized on its NavMesh position.
Only the pelvis and surviving bone transforms animate down into the pose.

On first leg loss, the Animator is disabled so it cannot overwrite injury bone
transforms. `TitanInjuryPose` owns those transforms while alive and injured; its
late-update ordering also gives the injury/supporting-arm pose priority over the
standing eye-cover pose. Blindness gameplay still applies. Death makes the injury
controller stop writing immediately; the existing body rebases joints and hands
the exact current pose to PhysX. Reset is a fresh prefab instance.

The target pose is built temporarily and the current pose restored synchronously,
so cutting the second leg never teleports the body to a new pose at the cut instant.
Each write checks that a bone is still a child of the Titan root. Removed arms/legs
are excluded even when referenced by the original cached skeleton.

Ground finding uses a scene-local downward ray, ignores triggers, self, dynamic
severed debris and hits above the root. Collider support extents are computed from
current transform matrices rather than stale `Collider.bounds`. The pelvis is
settled/lifted as needed to keep intact solid shapes clear of the floor through
folding arcs. This is not full uneven-terrain IK; flat/gently stepped lab ground is
the validated case. Walls or slopes inside the extended prone footprint can still
require future contact-aware posing.

`ModularTitanBody.IsHelpless` becomes true immediately after both legs are removed.
`CanGrabLeft/Right` are false in that condition, even with both arms intact. A
single-legged kneeling Titan retains its arms/capability flags for later attack
work, but still cannot walk. No attacks are added in this phase.

## Raised-nape follow-up

The player could still hit the old prone nape, but usually had to land on the Titan,
losing velocity and needing multiple hits. The chosen fix is **pose only**: no new
finisher/attack animation, free kill, enlarged/remote damage target or damage bonus.

`BuildProne` now uses pelvis pitch78deg instead of90, Spine-6/Chest-4,
Neck+6/Head+26. This produces a low inclined back and downward head, not a standing
or crawling Titan. Prone elbows aim wider at x+/-3.1/y1/z2.6; palms x+/-3.6/y.6/z4.2
in the existing size-relative root space. IK still only rotates existing bones;
no limb length change, new bones, root translation, collider-size/center edit or
saved prefab/scene rebuild. Missing arms are still skipped. One-leg kneels,
.58/.76s timing, FallWeight/settle curve and ownership remain unchanged.

At3x the actual nape center rose **5.48m ->9.16m**, while the bowed head remains
below it. The intended benefit is clearer rear/above fly-by access while retaining
ODM speed, not a guaranteed one-hit kill. Actual sword timing/reach/cover/front-vs-
rear checks and the trusted speed-damage curve are untouched. The existing
helpless/no-walk/no-grab state and500 regional nape HP remain.

Evidence: **`ArtSource/Titan/ProneNape/validation-summary.json`**, full XML and native
before/after images. All work in marker-owned disposable Unity2021 workers; no
live Editor input/stop/save/reload/scene switch or physics automation.

- Fresh baseline377pass/8fail/1skip of386; final **401pass/SAME8fail/1skip of410**,
  twice. **24 added cases**, no new full-suite failures.
-16 size/arm-state combinations (1/3/5/10x, both arms/left/right/neither), both leg
  cut orders/headings, partial kneel capture, no root motion/detached-bone writes,
  actual collider AND world-space skin ground clearance, identical nape shape/HP,
  unobstructed real nape approach from rear/above.
- Native actual sword contact at1/3/5/10x still applies the original100 fixture
  damage once per swing (500->400), not a synthetic ReceiveDamage shortcut.
- Rapid and settled second-leg cuts as well as mid-collapse continuity: strict
  arm step<8deg at120Hz. Existing grab/carry/grip/penetration bounds unchanged.
- Five-second native injury-death tests include3/5/10x full new pose; peak<40m/s
  remains strict (about9.35/11.97/16.64m/s respectively in first complete run).
  Existing kneel/mid-collapse death tests pass too. No velocity clipping, masses,
  joints, solver, angular caps or global Physics setting changes.
- Native graphics before/after1/1 each, same3x model/camera/light/ground. For clear
  comparison ONLY detached leg roots/skins are hidden in this owned render fixture;
  production severing/physics unchanged. No hitbox/debug marker added to gameplay.
- Intentional old posture expectations changed from horizontal torso/head<2 to a
  low inclined torso/head<3.2. Ground/skin/speed/grip/arm tolerances NOT weakened.
- Protected user prefab/scene/palettes/navigation/settings/Run and sword/Scout
  sources remain byte-identical to task start. Subjective ease/kill consistency,
  crowd FPS and comprehensive long-duration10x corpse safety remain playtests.

Reproduce final full run via `tools/run_titan_validation.py`, then
`python tools/capture_prone_nape.py`. Optional `--label before` verifies the owned
worker's injury runtime against the original `ef405492` revision (not current
runtime); all other script sources must match current. Never use the live project
as the worker. No navigation bake, scene save, asset rebuild or push.

## Historical verification

The later [motion overhaul](titan-motion-overhaul.md) passes **28 tests**, including
all checks here and the faster injury timing/impact curve.

Original injury-phase verification: **21 passed / 0 failed**, Unity **2021.3.25f1**, **2026-11-02 02:52:40–49 UTC**.
Includes all 14 prior navigation/foundation checks and seven new injury checks:

- Mirrored left/right surviving-knee poses, head bow, folded shin and no root motion
- Smooth transition without an immediate pelvis jump
- Ground clearance of intact colliders throughout the fall and final world-space skin
- Second leg cut preserves its folded visual orientation; subsequent pose writes
  never affect that severed branch
- Rapid double-leg cut with both arms missing still produces an alive helpless pose
- Blindness recovery cannot restart walking/standing animation
- Nape death from kneeling and during prone collapse stops pose animation and
  produces a stable ragdoll

Five-second injury-death maximum speeds: **9.94m/s** from kneeling and **9.72m/s**
during prone collapse. Intact death regression: max speed11.09m/s, final joint gap
.003m, hipsY .79m. The two final injury poses were visually checked in Unity.

Tests run through `tools/run_titan_validation.py` in a disposable project. Manual
physics stepping waits for Unity's deferred removal of severed root joints before
simulation. Ground skin checks use direct bind-pose/bone-weight world-space skinning
because `BakeMesh` followed by `TransformPoint` double-counts scale for these 100x
FBX skin transforms; stale native Rigidbody pose caches are not used as visual pose
snapshots.

## Next phase

Telegraphed, cancellable stomp/grab attacks, followed by real player/sword integration.
Attack selection must respect leg/arm availability, blindness, injury transitions,
and the both-legs-gone helpless state. Crawling, getting back up, regeneration and
injured attack clips remain unimplemented.
