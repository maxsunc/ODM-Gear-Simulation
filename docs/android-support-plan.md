# Android support plan — deferred

## Status and goal

Planning notes only. **No Android implementation, platform switch, build, input migration or gameplay retuning has been performed for this plan.** Revisit when the user is ready.

Goal: make the existing game playable on an Android phone while retaining PC support and the current gameplay/physics behavior. Start with a sideloaded test APK, not a Google Play release.

**First question on resuming:** which phone/tablet model and Android version will be used for testing? Confirm 64-bit support, available memory and device graphics capabilities before finalizing settings.

## Project baseline

- Unity **2021.3.25f1**, URP **12.1.11**. Do not upgrade automatically.
- Published project checkpoint at planning: `dcef23c3f91012bd347a81e13a93f7a846e81d84` on `main`.
- Android Build Support was not present in the editor installation when this plan was written. At the later Scout-model publication review, the AndroidPlayer module, SDK, NDK and OpenJDK directories were present under this editor, and Unity had regenerated the tracked C# projects with Android tooling references. This is toolchain availability, **not implemented mobile controls or a tested Android build**. Recheck compatibility when resuming.
- Movement, grapple, gas, sword, camera, animation aiming and escape challenge currently contain direct keyboard/mouse input reads.
- Current camera preference: original eased fixed-step follow and dynamic FOV, improved scroll zoom, speed-streak integration removed, **camera collision disabled** via the shared MainCamera prefab toggle.
- Forest visual LODs and the cape CPU optimization are already included in the published checkpoint.

## 1. Install Android build tools

In **Unity Hub → Installs → Unity 2021.3.25f1 → Add modules**, install:

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

Use the toolchain appropriate to this Unity version. Initial device testing does not require an automatic Unity upgrade. Compatibility with the chosen phone must still be verified. Current Google Play target-API and publishing requirements are a separate investigation; do not assume the old editor/toolchain meets them.

Proposed first-build settings, subject to device confirmation:

- Directly installable **APK**, not an Android App Bundle.
- Landscape orientation and appropriate safe-area handling.
- **ARM64 / IL2CPP** for a device supporting 64-bit apps.
- **OpenGL ES 3** as the initial graphics API; assess Vulkan separately if useful.
- Development Build for device logs and Profiler connection; leave Deep Profiling off for representative measurements.
- Choose minimum/target API levels against the installed compatible SDK and actual test device, rather than assuming existing defaults are suitable.
- Enable USB debugging on the device for deployment/log collection, or install the APK manually.

Do not switch the user's live editor platform or interrupt their Play session without coordination. Platform switches can trigger extensive reimports. Use an isolated, marker-owned worker where appropriate.

## 2. Introduce a shared gameplay input layer

Keep one movement/combat implementation. Keyboard/mouse and mobile touch should supply the same logical action states:

```text
Keyboard + mouse ─┐
                  ├─ Gameplay input → existing movement, hooks and combat
Touch controls ───┘
```

Represent movement, look and action **pressed / held / released** states explicitly. A UI `OnClick` alone is insufficient for hold-to-charge sword attacks and held grapple/gas actions.

Likely integration points to review:

- `Assets/Movement.cs`: movement axes and jump.
- `Assets/ODM_Movement.cs`: independent hooks, gas, extra boost and steering.
- `Assets/Attack.cs`: fresh sword press, charge hold and release.
- `Assets/ThirdPersonCameraController.cs`: camera look and zoom.
- `Assets/Player/Scout/ScoutPlayerAnimation.cs`: aiming presentation must read the same logical hook state, not remain keyboard-only.
- `Assets/Titan/Prototype/TitanGrabEscape.cs`: six-action escape input and input quarantine.
- `Assets/UI/GamePauseMenu.cs`, `GameStats.cs`, `GameMenu.cs`: touch access, Android Back behavior and menu navigation.
- Review other input consumers, including audio/music shortcuts, before calling the port complete.

The project currently uses Unity's legacy Input Manager. A wholesale migration to the newer Input System is **not required for this first milestone**. Choose the smallest maintainable approach after reviewing assembly boundaries and execution order. The Titan prototype assembly cannot directly reference Assembly-CSharp; preserve that integration boundary.

## 3. Prototype touch controls

Initial layout proposal—not a final approved control scheme:

| Action | Proposed control |
| --- | --- |
| Walk / steer | Left virtual joystick |
| Camera / aim | Drag an unoccupied area on the right |
| Left and right hooks | Separate hook buttons |
| Gas | Gas button |
| Extra boost | Separate boost control |
| Jump | Jump button |
| Sword | Hold to charge, release to strike |
| Pause / stats | Top-corner buttons |
| Camera distance | Small zoom-in / zoom-out buttons initially |

### Hook ergonomics

Holding two hooks, gas, steering and camera movement simultaneously may be awkward with two thumbs. Consider a **mobile-only tap-to-attach / tap-again-to-release option**. This is a proposed input interaction, not permission to change grapple physics or silently change desktop controls. Test and obtain user feedback before treating it as the default.

### Touch ownership and lifecycle

- Support simultaneous touches with stable per-finger ownership.
- Button/joystick touches must not also rotate the camera.
- Scale look gestures sensibly across phone resolutions and aspect ratios.
- Respect display safe areas, notches and system gesture regions.
- Clear held/latched input when paused, backgrounded, focus-lost, disabled, killed, held or respawned as appropriate to existing gameplay ownership.
- Prevent stale touches from firing hooks or swords after returning from a menu or grab escape; require fresh input where existing rules require it.
- Preserve mouse/keyboard behavior on PC and avoid mouse-emulation double activation on touch devices.
- Retain current camera offset, follow/FOV behavior and collision-off preference unless the user requests changes.

## 4. Adapt the grab escape and HUD

The existing challenge uses **W/A/S/D/Q/E**. A joystick overlay alone will not make it playable on Android.

- Present six corresponding touch actions while the challenge is active.
- Feed touch states into the existing logical challenge, rather than bypassing it.
- Preserve **three fresh presses within three seconds**, sequence validation, wrong-press/chord rules and release/input quarantine.
- Do not add automatic escapes, held-button repeats, general damage immunity or modified regrab grace.
- Make the current prompt readable and physically reachable without covering the relevant view.

Review menu, HUD, stats, audio/options sliders and pause screens for phone-size readability and practical touch targets. Verify Android Back behavior and background/resume handling. No keyboard should be required to finish the first milestone.

## 5. Profile on the actual Android device

Desktop FPS is not an Android performance prediction. The user-confirmed Editor Inspector repaint bottleneck is absent in an Android player, but mobile GPU limits, CPU cost, memory pressure and thermal throttling remain unknown.

Measure:

- Sustained frame rate and frame-time stability after several minutes.
- CPU versus GPU workload; do not infer one from geometry estimates alone.
- Memory use, loading time and background/resume behavior.
- Heat and thermal throttling.
- Traversal and combat around multiple Titans, including cape, hooks, audio, effects and corpses.
- Both maps after the initial Forest build is playable.

Compare representative development captures with non-development builds before claiming final performance.

Do **not** silently reduce resolution, Titan population, physics timestep/solver quality, contact bounds, animation fidelity, cape particle/substep/iteration counts or gameplay tuning. If necessary, propose an explicit Android-specific **visual** quality profile separately, with user approval, leaving PC settings intact.

## First playable milestone

Produce one test APK where the user can:

1. Launch the game and navigate the menu by touch.
2. Load Giant Forest.
3. Move, steer, aim and adjust camera distance.
4. Use either hook independently, both hooks, gas and boost.
5. Jump and perform the existing hold/release sword attack.
6. Complete the existing grab-escape challenge by touch.
7. Die and respawn without changing the preserved world.
8. Pause/resume, inspect stats, return to the menu and background/resume safely.

Then refine the controls on the actual phone before broader mobile polish and Shiganshina validation.

## Validation and scope limits

- Follow the existing preference: **no new validation test code or assertion edits** without a changed instruction. Reuse relevant existing checks and coordinate real-device manual review.
- Preserve GUIDs, shared prefab structure, deliberate per-map overrides and exclusive motion ownership.
- No new motor, automatic aiming/homing, physics clipping cheats, input-driven remote damage or player parenting into detachable rigs.
- Existing scoped passes do not erase documented broader-suite failures. Android support will not be considered certified merely because an APK builds or a desktop test passes.
- No standalone Android build, device measurement or touch usability result exists for this plan yet.

## Resume order

1. Confirm phone model and Android version.
2. Recheck/install the compatible Android modules.
3. Review the input seam and agree on the first touch layout, especially hook latching.
4. Implement shared logical input and touch UI without changing the motor/combat rules.
5. Include escape, menus, lifecycle cleanup and animation input consumers.
6. Build a development APK, test on-device and profile.
7. Iterate from measured device results; discuss any visual quality compromises explicitly.
