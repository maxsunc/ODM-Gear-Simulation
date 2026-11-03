# Titan phase 2a — patrol, sight and chase

An **opt-in AI layer** for the reference-model Titan. It does not replace the old
Titan AI, attach itself to the body prefab, modify the player, or alter gameplay
scenes. The original body/dismemberment lab remains separate and passive.

## Try it

Open `Assets/Scenes/TitanAISandbox.unity` and enter Play Mode.

- **WASD**: move the teal 1.8m human proxy along world X/Z.
- **Left Shift**: sprint.
- **Left/Right arrows**: orbit the overview camera.
- **6**: eye slash / five-second blindness.
- **2 / 3**: sever left/right arm (walking remains available).
- **4 / 5**: sever left/right leg (walking stops).
- **K**: nape kill and ragdoll.
- **P**: pause/resume AI.
- **R**: reset Titan, human and detached branches.
- On-screen buttons place the human behind the wall or in the open left lane.

The human starts behind the tall yellow wall. The Titan follows the cyan patrol
markers, turns toward its next waypoint and can spot the human as it rounds the
wall. Move into the open to provoke a chase; move behind the wall to break sight.
The HUD reports awareness state, current visibility, last-seen position and route
status. The later [stomp phase](titan-stomp-combat.md) adds an opt-in attack after
braking: a fixed visible landing marker, readable wind-up and one40-HP impact.
The [grab slice](titan-grab-combat.md) adds exclusive bounded hold/escape/release and
an auto selector. **T** toggles auto combat, **8** requests stomp, **9/0** left/right
grab, **E** escapes. **P** also interrupts attacks.

The proxy has sandbox HP and can be held, but still no ODM gear, swords or actual
player wiring. Its controls/ownership and health are not changes to your real player.

## Behavior

`Assets/Titan/Prototype/TitanPatrolChaseAI.cs` is independent of attacks:

| State | Behavior |
| --- | --- |
| Patrol | Follow assigned world-space points, pause, then advance cyclically. |
| Chase | Follow the last visually confirmed target position, with a stopping radius. |
| Investigate | After losing sight, travel to the last seen position, not the hidden player's live location. |
| Search | Stop and turn/look briefly; reacquire visible targets or return to the nearest patrol waypoint. |
| Blind | Immediately clear the route and stop; no visual detection. Memory continues aging. |
| Immobilized | Either missing leg prevents walking. The later [injury animation](titan-injury-animation.md) kneels on the remaining knee or lies prone when both legs are gone. |
| Dead | Navigation disabled; the existing body owns ragdoll/death. |
| NavigationUnavailable | Stop safely if the agent is disabled or not on navigation data. |

Sight requires an active explicitly assigned target root, 3D distance within 38m,
horizontal 120-degree FOV, and a clear ray from the eyes to the human's sight
point. Physics raycasts use the Titan's own scene, ignore triggers and ignore only
the Titan's and assigned target's hierarchies. Solid scenery blocks sight. The
bounded raycast buffer fails closed if full. Detection is sampled every .15s.

After the [motion overhaul](titan-motion-overhaul.md), base navigation speeds are
4.6m/s patrol and9.5m/s chase, with controlled pace/corner variation, a distinct run,
and a6m nominal chase stop radius. Paths are refreshed at .3s intervals. Only complete paths are accepted;
unreachable patrol points are skipped after a brief wait. Unreachable investigation
goals lead to a local search rather than running into a wall forever. Target memory
expires after 14s, and local search lasts four seconds. A replacement target must be
detected independently; it does not inherit the previous target's detection.

Blindness recovery reacquires sight normally. If the target remains hidden, the
Titan investigates only remembered information, or returns to patrol if that memory
has expired. Losing an arm does not stop walking; losing either leg does. Pausing
the component stops its NavMeshAgent and walking animation immediately.

## Setup and ownership

- `TitanNavigationSurface.cs`: an **explicit-source lab-only** runtime bake using
  built-in Unity 2021 `NavMeshBuilder`. Sources are the floor and wall, not the
  animated Titan, human, decorative markers or unrelated scene objects.
- Bake and agent dimensions: radius1.8m, height12m, climb0.6m, slope40 degrees.
  Settings are copied and overridden locally, not written to global agent settings.
- The lab registers and removes only its own `NavMeshDataInstance`. It never calls
  `NavMesh.RemoveAllNavMeshData` or changes another scene's bake.
- `TitanAISandboxDemo.cs` builds/configures the agent and AI at runtime. No AI
  components are added to the body prefab. Reset instances stay in the lab's scene, even when
  the lab is loaded additively, and old agents/branches are cleaned up.
- `TitanSandboxTarget.cs`: temporary human proxy movement only.
- `Assets/Editor/TitanAISandboxBuilder.cs`: reproducible scene authoring through
  `Tools → Titans → Build Patrol and Chase Sandbox`. Close the AI lab before
  rebuilding it. Only the dedicated additive lab scene is saved; the user's active
  scene is restored and never saved by this builder.

For eventual gameplay integration, use the level's own appropriately sized baked
NavMesh and configure `NavMeshAgent` + `TitanPatrolChaseAI` on a prefab variant or
spawned Titan. Assign the player **root transform** explicitly, the sight offset,
patrol route and obstruction mask. Do not add the lab surface or proxy controller
to the real player or level automatically.

## Boundaries of this phase

- Ground navigation only. Airborne targets can be pursued via ground projection,
  but this flat lab does not establish correct multi-storey ODM pursuit behavior.
- Runtime bake is for small static lab geometry; moving blockers, corpse avoidance,
  large levels, streaming and multi-Titan crowd tuning remain future work.
- The [motion overhaul](titan-motion-overhaul.md) adds distinct native walk/run clips,
  velocity-matched cadence, head-led pivots and bounded variation. Full runtime
  foot-plant IK, production animation polish and attack clips remain unfinished.
- Stomp/cancellation and proxy HP are a separate opt-in component described in
  [the stomp phase](titan-stomp-combat.md), not logic inside the navigation class.
  Standing grab/hold/release and [hand-to-mouth/bite](titan-bite-combat.md) also stay
  outside navigation. Aerial/kneeling grabs, production consumption, real-player
  damage, swept swords, crawling and player integration
  remain unfinished.

## Regression tests

The subsequent [injury-animation phase](titan-injury-animation.md) passes **21 tests**,
including all checks below. Leg loss now has animated kneeling/prone poses.

Navigation-phase verification: **14 passed / 0 failed**, Unity **2021.3.25f1**, **2026-11-02 02:07:19–27 UTC**.
This includes the six existing foundation checks and eight new AI/scene checks.
The wall-routing test moves the actual Titan past the obstacle in **5.06 simulated
seconds**; patrol advances after **2.30m** of root travel. The default-physics scene
check confirms the visible skeleton stays attached to that moving root.
Ragdoll regression: maximum speed **11.37m/s**, final joint gap **0.007m**, hipsY **0.79m**.

Run only in a disposable Unity project:

```sh
python tools/run_titan_validation.py --wait
```

The runner now copies both isolated lab scenes and all prototype tests, not the
user's gameplay scenes. New checks cover FOV/range/occlusion, hidden-target memory,
search/return, navigation stopping during blindness/leg loss/death, missing/unreachable
navigation, actual root movement and waypoint advancement, complete wall-avoiding
paths, memory expiry/target replacement, and authored scene startup/reset cleanup.
The six earlier body/mesh/UV/dismemberment/ragdoll checks remain included.

A Unity 2021-specific bug found by these tests was fixed: `NavMeshAgent.ResetPath()`
clears the stopped flag, so stopping now clears the path **before** setting
`isStopped = true`. Blindness is tested for actual absence of root drift as well
as state flags.

A separate visual check caught another issue not exposed by root-only navigation
assertions: Rigidbody interpolation could restore old world-space bone positions
while the agent moved the root, leaving the visible Titan behind. Animated
kinematic bones now use **no physics interpolation** in both runtime initialization
and the prefab builder. The saved-scene integration test now advances normal
physics and checks hips/root alignment and every ragdoll bone's local position
during real locomotion. The body prefab was regenerated for this physics setting,
with its model, rig and asset GUIDs preserved. Dismemberment and ragdoll regression
tests are rerun alongside navigation tests.
