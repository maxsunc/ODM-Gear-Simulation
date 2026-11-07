# Player camera: zoom, render-time follow, collision and speed feedback

Implementation: `Assets/ThirdPersonCameraController.cs` (Unity 2021.3.25f1).

## Preserved authoring

Both gameplay maps retain their intentional world-space pivot offset `(5, 3, 0)`,
12m starting distance and 2–20m requested zoom range. No scene, prefab or camera
script GUID was changed. Mouse look sensitivity/inversion, pitch limits and the
normal `baseFOV` remain unchanged. The speed-feedback revision replaces the old
0-10m/s FOV ramp with the configurable high-speed curve below. No second camera
owner, follow delay, player transform writer, root interpolation, global Physics
setting, timestep or wind-audio change was added.

## Behaviour

- Wheel zoom reads raw `Input.mouseScrollDelta.y`: **1.25m per notch**, including
  fractional trackpad deltas, rather than the old scaled-axis ~0.2m. No frame-time
  multiplier or small-delta cutoff. `zoomSmoothTime` defaults to **0.08s**.
- Input is read in `Update`; pose is applied in **`LateUpdate`**, execution order
  150, after existing movement/hold/reaction presentation. Target translation is
  followed directly, removing the old extra position lerp and its speed-dependent
  trailing. This does not interpolate or modify the player/visual rig itself.
- Root displacement is measured in `FixedUpdate` after the motors, not from
  camera motion, orbit, wheel input or render-frame displacement. Two consecutive
  valid samples are required. FOV is eased in `LateUpdate` **before** collision
  checks, so near-plane protection uses the current lens.
- Scene-local nonallocating sphere casts and overlap checks replace the single
  ray. The sphere is at least `collisionRadius` (**0.3m**) and expands when needed
  to enclose near-plane corners. The saved collision layer mask is respected;
  triggers, the player's own hierarchy and the camera's hierarchy are excluded.
- Collision retracts the camera immediately, including below the requested 2m
  minimum if necessary. User zoom preference is not overwritten by obstruction.
  Clearing an obstruction eases outward using `positionSmoothing` (saved rate 8).
- The authored offset is never rewritten/recentred. Only when obstructed, the
  path from player centre to the offset pivot may temporarily shorten, preventing
  a camera boom from starting inside a wall. This temporary displacement also
  eases away on clearance, rather than popping sideways to the full offset.
- Saturated query buffers are treated as blocked. If no clear pivot is available,
  a previously clear camera position is retained when still clear; this is not
  a guarantee of a perfect view in fully embedded/dense moving geometry.
- Respawn/retarget reset clears zoom velocity, collision recovery and old safe
  positions. J-menu/unlocked-cursor input does not alter zoom or look; a paused
  camera does not continue its pose damping. The camera never owns the cursor.

## Speed feedback and Inspector tuning

Select **Main Camera -> Third Person Camera Controller**. Both maps use the same
runtime implementation; new fields have defaults without requiring a scene save.

| Header / field | Default / meaning |
| --- | --- |
| Speed FOV / Speed Fov Enabled | On; independently disable the lens effect. |
| Base FOV | Existing saved 60 degrees. |
| Fov Start Speed / Fov Full Speed | 20 / 65m/s, with a SmoothStep ramp. |
| Speed Fov Increase | +10 degrees: target 60-70 degrees with current saved base. |
| Max FOV | Existing saved 90 degrees, retained as a safety ceiling, not the target. |
| Fov Smooth Speed | Existing saved rate 3; exponential, render-time easing. |
| Peripheral Speed Streaks / Speed Streaks Enabled | On; independently disable streaks. |
| Streak Start Speed / Streak Full Speed | 35 / 65m/s. |
| Streak Opacity | 0.16 maximum, with softer per-streak feathering. |
| Streak Edge Band | Outer 0.14 of each edge; inner 72% width/height is untouched. |
| Streak Fade In / Out | 0.2 / 0.15 seconds for normal speed changes/landing. |
| Maximum Feedback Speed | 160m/s rejection threshold **for effects only**, never a velocity cap. |

`speedThreshold` is retained hidden for old scene/API serialization compatibility;
its saved value 10 no longer drives FOV. `positionSmoothing` remains collision
recovery only. There is no positional follow-smoothing field.

Both effects use actual **airborne** movement, including fast swings/coasting and
falls, not boost/hook key state. Grounding reads the existing CharacterController
and Scout state; it never calls the mutating `Movement.CheckGround()` query.

Nonfinite motion, fixed deltas >0.1s, render hitches >0.2s, stale samples >0.12s or
excessive displacement reset presentation. Death, held/release-pending, respawn,
disabled controls/controller, pause and focus loss immediately clear streaks and
restore normal FOV. Retarget/teleport reset invalidates samples and requires fresh
history. This does not add invulnerability, alter velocity or change audio.

`Assets/Player/Scout/PlayerSpeedStreaks.cs` is a noninteractive, scene-owned runtime
uGUI graphic: up to 28 feathered streaks / 252 vertices, no textures, shaders,
materials, particles, native bodies, raycasters or gameplay-Random consumption
added. A built-in UI material draws it behind normal HUD/Stats/pause canvases.
Forward flight flows outward, backward flight inward, and sideways/vertical flight
opposite the measured camera-space movement. Every stroke is clipped into an
edge band, with its feather width inset to avoid the protected centre. Viewport
and display follow the owner camera; offscreen/disabled cameras don't emit FX.
The camera owns cleanup; no extra scene objects need to be saved.

## Verification and remaining review

The speed-feedback revision compiled in the closed marker-owned Unity2021 worker
and reran 26 existing audio/respawn/held-motor/free-flight integration checks:
**26 passed, 0 failed, 0 skipped**. No tests or assertions were added or edited.
Current receipts, preservation details and static native streak previews are in
`ArtSource/Camera/SpeedFeedback/`. `tools/capture_speed_feedback.py` renders only
the actual UI graphic over existing map-photo sprites: no Play, input, native
physics, camera-controller actor simulation, prefs writes or source scene saves.
These previews do **not** demonstrate moving gameplay or the FOV perspective.

`ArtSource/Camera/Response/` and `ArtSource/Validation/MainPR/` retain earlier
receipts, including the pre-speed-feedback Titan-worker result **445 passed /
12 failed / 1 skipped**. A full suite was not rerun or claimed green here.

These existing checks verify established integration gates, not the subjective
feel of FOV/streaks, wheel zoom or new collision-specific camera assertions. A
fresh Play review is still needed: both maps, accelerating/decelerating across
thresholds, lateral/backward flight, landing, fine/fractional scrolling, fast swings,
turning beside walls/trees/roofs/Titans, close obstacles below minimum zoom,
obstruction clearance, held/release, death/respawn, focus changes and J-menu scrolling. No live user Play session was
controlled or stopped for this work.
