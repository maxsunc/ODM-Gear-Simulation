# Player camera: original eased follow, improved scrolling, collision disabled

Implementation: `Assets/ThirdPersonCameraController.cs`, Unity 2021.3.25f1.

## Selective rollback requested by the user

The user asked to undo the camera changes except collision protection and improved scrolling. The reference for the earlier camera is **`1d8840ab:Assets/ThirdPersonCameraController.cs`**, before the responsive-follow revision in `4f445167` and speed-feedback revision in `becdf248`.

Restored:

- **Fixed-step positional easing/trailing**, rather than instant render-time positional follow. The original follow expression is `Vector3.Lerp(currentPosition, desiredPosition, Time.fixedDeltaTime * positionSmoothing)`; saved smoothing rate remains **8**. Rotation follows the authored orbit directly on that same fixed step; no extra rotation interpolation is introduced.
- **Original dynamic FOV:** measured target displacement per fixed step, linear speed ramp from `baseFOV` to `maxFOV`, reaching the maximum at `speedThreshold`. Saved values remain **60 → 90 degrees / 10 m/s**, with the original fixed-step `fovSmoothSpeed` easing, saved rate **3**. This again applies to grounded travel as well as flight, unlike the removed airborne-only high-speed effect.
- Removed camera integration for the later 20–65 m/s / +10-degree FOV curve and peripheral speed streaks. `PlayerSpeedStreaks.cs` and historical art/capture evidence remain as unused assets; the camera no longer creates or updates an overlay. Old serialized speed-effect fields in the untouched prefab are ignored because the runtime camera no longer declares them.

This is a **selective rollback**, not a byte-for-byte old-script checkout: retained collision and scroll improvements necessarily remain, together with existing respawn/retarget reset, pause-menu input blocking and shared sensitivity/inversion options. No legacy Cinemachine rig is reactivated.

## Preserved saved authoring and other work

Both gameplay maps retain the intentional **world-space offset `(5,3,0)`**, starting distance **12 m**, requested zoom range **2–20 m**, mouse sensitivity/inversion and pitch limits. No scene, prefab, script GUID, root, camera target or map-specific binding was rewritten. The shared MainCamera prefab still serves both maps.

Forest distance LODs, cape optimization, grapple turn-speed/tuning, audio, player movement, Titan animation/physics/navigation and wind remain unchanged. No player or rig transforms, root interpolation, global physics settings or fixed timestep are changed by the camera.

## Retained scrolling

- Raw `Input.mouseScrollDelta.y`, **1.25 m per notch**, including fractional trackpad deltas, with no old scaled-axis cutoff or frame-time multiplier.
- `zoomSmoothTime` **0.08 s**. Requested zoom remains independent of obstruction and is not overwritten by collision.
- Input is read in `Update`; follow/zoom/FOV advance in `FixedUpdate`, matching the restored follow cadence. Additional positional easing is intentional legacy behavior, so the total visual response is not identical to the removed instant-follow camera.
- Pause/J-menu/unlocked-cursor input does not change look or zoom; the camera does not own the cursor.

## Camera collision toggle — currently disabled

The user subsequently requested **camera collision off**. `cameraCollisionEnabled` defaults to false and is explicitly **0** on `Assets/Prefabs/SharedGameplay/MainCamera.prefab`, inherited by both gameplay maps. No scenes were rewritten. In **Main Camera → Third Person Camera Controller → Collision**, enable **Camera Collision Enabled** to restore the retained protection.

While disabled, camera sphere casts/overlaps, shoulder-path shortening, obstruction retraction and render-time safety checks are bypassed completely. The camera keeps its requested zoom and original positional easing and **can pass through walls, trees, Titans and other scenery**. Player collision, hook contacts, combat, navigation and native physics remain unchanged. No camera clip plane, offset, zoom, sensitivity or FOV settings were retuned.

The following retained collision implementation applies **only when the toggle is enabled**:

- Scene-local nonallocating sphere casts and overlaps; original layer mask; triggers and own player/camera hierarchy excluded.
- Minimum sphere radius **0.3 m**, enlarged for the current near-plane corners. FOV updates before physical clearance checks.
- Immediate collision retraction, even below minimum requested zoom; eased outward recovery.
- Player-centre-to-shoulder pivot protection, temporary obstruction-only pivot shortening and smooth recovery. The authored offset is never rewritten/recentred.
- Full buffers fail closed. An embedded pivot retains a prior safe position when still clear; this is not a perfect-view guarantee in dense moving geometry.
- **The actual eased camera position is checked after follow smoothing.** Two clear endpoints are not permission to interpolate through a wall/corner: the eased boom is recast and the final volume checked/retracted toward the known-clear pivot.
- A zero-time `LateUpdate` pass revalidates collision after render-time animation/moving obstacles, but does not advance follow, orbit, zoom or FOV again. This retains per-render safety without restoring the removed render-time follow.
- Respawn/retarget reset snaps safely to the current target and resets zoom velocity, old safe positions, speed history and FOV; it does not interpolate across the teleport.

## Verification / evidence

Selective-rollback evidence is under **`ArtSource/Camera/SelectiveRollback/`**; the subsequent collision-off change is recorded under **`ArtSource/Camera/CollisionDisabled/`**. Existing audio/respawn/held-motor/free-flight regression checks are reused; no tests/assertions are added or edited. These are integration checks, not direct assertions of the changed follow easing, FOV curve or collision/scroll feel. Earlier broad-suite failures remain unresolved.

Historical response/speed-effect images and receipts in `ArtSource/Camera/Response/` and `ArtSource/Camera/SpeedFeedback/` document superseded behavior, not the current feature set. `tools/capture_speed_feedback.py` can still render the unused standalone graphic for archival purposes; it does not demonstrate active gameplay effects.

**Use a fresh Play session after compilation** to review the rollback and ensure any overlay from an earlier hot-reloaded Play session is gone. Check both maps, high-speed follow, wheel zoom, walls/trees/roofs, obstruction recovery, hold/release, respawn and pause. The live user Editor was not stopped, reloaded, switched or saved, and no synthetic input was supplied for this work.

All selective rollback work is local on `main`, not committed/pushed at capture; Git remains publication authority.
