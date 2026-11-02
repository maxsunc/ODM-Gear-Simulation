# Titan leg-loss injury animations

The user requested an alive injury response before the attack phase:

- **One leg gone:** collapse onto the remaining knee, fold its shin behind the body,
  bow the torso/head and hold the kneeling pose. Mirrored for either surviving leg.
- **Both legs gone:** fall forward from the current pose into a flat, face-down,
  helpless pose. Cutting both legs rapidly also works; no forced standing reset.

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

Actual Unity captures:

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

## Verification

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
