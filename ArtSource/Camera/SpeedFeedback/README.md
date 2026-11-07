# High-speed FOV and peripheral streaks

Current implementation is in `Assets/ThirdPersonCameraController.cs` and
`Assets/Player/Scout/PlayerSpeedStreaks.cs`; tuning/help: `docs/player-camera.md`.

## Defaults

- Airborne FOV: existing base 60 degrees plus up to 10 degrees, eased between
  20 and 65m/s. Existing maxFOV 90 is now a safety ceiling; the legacy saved
  speedThreshold 10 is retained hidden but does not drive the curve.
- Airborne streaks: 35-65m/s ramp, max opacity 0.16, outer 14% edge bands,
  0.2s fade-in / 0.15s fade-out. Independent enable toggles on Main Camera.
- Both read measured fixed-step player-root motion, not boost, hook, mouse or
  camera movement. Ground/held/dead/disabled/paused/focus/respawn/untrusted-motion
  lifecycle gates suppress inappropriate feedback. The player is never moved.
- Follow remains instant. Offset `(5,3,0)`, user scene/prefab tuning, zoom and
  collision implementation are preserved; wind/audio were not retuned.

## Native authoring previews

`tools/capture_speed_feedback.py` uses a CLOSED marker-owned Unity2021 worker.
It renders the actual runtime streak Graphic over existing saved frontend map
photos, without creating a gameplay player, running Play mode, input, native
physics, prefs writes, source scene saves or a new validation-test class.

- `Forest_Off_1920x1080.png` / `Forest_Forward_1920x1080.png`
- `Shiganshina_Off_1920x1080.png` / `Shiganshina_Forward_1920x1080.png`
- `Shiganshina_Lateral_1440x1080.png`

Forward examples sample peak strength with forward camera-space flow; the
lateral example samples rightward movement (streak flow left). These are
**static UI authoring previews, not gameplay screenshots or demonstrations of
moving FOV perspective**. The paired off/forward previews have unchanged pixels
in the protected central region. Native graphics: Intel Iris Xe. Source hashes
and capture scope are recorded in `source-art-summary.json` / `native-art.log`.

## Existing regressions and preservation

The current-source closed worker compiled and ran the existing selected
camera/respawn/held-motor/free-flight/audio regressions: **26 passed, 0 failed,
0 skipped**. Raw receipts: `focused-existing-regressions.{xml,log}`. These are
existing integration gates, not new FOV/streak-response/lifecycle assertions.
No tests or assertions were added or edited.

`preservation-and-validation.json` records 1585 pre-existing Assets/ProjectSettings
paths: only the intended camera source changed, 1584 unchanged. Existing source
scene/prefab/settings/test/audio bytes were preserved, including the user's
already-dirty Shiganshina scene. New graphic source/meta were added separately.

This work is local/unpublished at capture. Earlier PR receipts remain historical:
`ArtSource/Validation/MainPR/` has 445 passed / 12 failed / 1 skipped; that full
Titan-worker suite was not rerun or claimed green for this revision.

Fresh Play review is still required for both maps, perceived acceleration,
brightness/motion comfort, lateral/backward flight, landing, collision, holds,
death/respawn, focus and pause. Never treat these static pictures or the filtered
26 checks as full moving gameplay, build, performance, device/WebGL certification.
