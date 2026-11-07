# Player camera: responsive zoom, render-time follow and collision

Implementation: `Assets/ThirdPersonCameraController.cs` (Unity 2021.3.25f1).

## Preserved authoring

Both gameplay maps retain their intentional world-space pivot offset `(5, 3, 0)`,
12m starting distance and 2–20m requested zoom range. No scene, prefab or camera
script GUID was changed. Mouse look sensitivity/inversion, pitch limits and the
existing speed-based FOV settings/curve remain unchanged. No second camera owner,
player transform writer, root interpolation, global Physics setting or timestep
change was added.

## Behaviour

- Wheel zoom reads raw `Input.mouseScrollDelta.y`: **1.25m per notch**, including
  fractional trackpad deltas, rather than the old scaled-axis ~0.2m. No frame-time
  multiplier or small-delta cutoff. `zoomSmoothTime` defaults to **0.08s**.
- Input is read in `Update`; pose is applied in **`LateUpdate`**, execution order
  150, after existing movement/hold/reaction presentation. Target translation is
  followed directly, removing the old extra position lerp and its speed-dependent
  trailing. This does not interpolate or modify the player/visual rig itself.
- Physics-cadence target-speed sampling and FOV smoothing remain in `FixedUpdate`.
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

## Verification and remaining review

The closed marker-owned Unity worker compiled the implementation and ran 26
existing audio/respawn/held-motor/free-flight integration checks: **26 passed,
0 failed, 0 skipped**. No tests or assertions were added or edited. Receipts and
source-preservation details are in `ArtSource/Camera/Response/`.

These existing checks verify established integration gates, not the subjective
feel of wheel zoom or a new collision-specific camera suite. A fresh Play review
is still needed: both maps, fine/fractional scrolling, fast swings, turning beside
walls/trees/roofs/Titans, close obstacles below minimum zoom, obstruction clearance,
held/release, death/respawn and J-menu scrolling. No live user Play session was
controlled or stopped for this work.
