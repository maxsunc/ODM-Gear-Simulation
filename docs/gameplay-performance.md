# Gameplay performance: first investigation

## Publication / scope

PR #1 was confirmed merged to `main` (`e33cbaa5`). Pending shared-prefab, speed feedback, footstep and user audio tuning work was committed/pushed to `main` as **`becdf2489ade12733d807328cb216686805d6669`**. Caches/Plastic/machine-specific build-output history were excluded. The performance work below is a subsequent local change on `main`, not part of that checkpoint.

The capture receipts below describe publication state **at capture time**; local/uncommitted wording is historical after publication. Git history on `main` is the authority for the current checkpoint.

The user's initial report was **30–40 FPS during normal playing**. We have not captured that live workload, its resolution, CPU/GPU timeline or Editor-versus-player context. No live Editor control/input/Play/scene save was used. These findings distinguish isolated CPU measurements from rendering estimates.

## Measured cape CPU cost and fix

Closed owned Unity 2021.3.25f1 worker, Editor Mono, headless/no rendering. Actual shared Player prefab/Scout/Run clip; 273-particle cape, 120 Hz substeps, eight iterations, all seven original body/gear proxies, original wind/settings. A benchmark call supplies `1/60` seconds; this is a **simulated delta time, not measured gameplay FPS**. Each idle / 40 m/s forward presentation / turning presentation case warms up for 60 calls and times 360 calls. Pose staging is outside the measured `Simulate` interval. There is no player motor input or physics stepping.

Repeated A/B runs:

| Case | Original mean ms/call | Optimized mean ms/call |
| --- | ---: | ---: |
| Idle | 8.66–8.72 | 3.04–3.08 |
| Forward presentation | 8.77–8.79 | 3.06–3.07 |
| Turning presentation | 8.72–8.73 | 3.05–3.05 |

Approximately **5.7 ms less CPU work per 60 Hz cloth call / 65% reduction** on this machine. Do not translate this directly into a promised FPS gain: GPU-bound frames may improve little, and standalone/IL2CPP costs differ from Editor Mono. Machine inventory detected an i7-13700H / Intel Iris Xe; the benchmark graphics device was explicitly **Null Device**, not a GPU measurement. Background workload/power state can affect timings.

`Assets/Player/Scout/ScoutCapeCloth.cs` now:

- Rejects particles outside conservative cached proxy bounds before expensive capsule/ellipsoid work; the original contact projection still handles every possible interior point.
- Caches padded proxy dimensions, fixed rest-distance attachment limits and the structural-only link list.
- Avoids copying the capsule struct per contact and multiplying corrections by known zero/one pin weights.
- Adds **`ODM.ScoutCape.Simulate`** as a live Profiler marker around automatic simulation.

No substep frequency, solver iterations, source cape length, mesh, constraints, collision shapes, animation clip, player/bone transforms, native physics or movement tuning were reduced/changed. All **294,840 sampled particle positions (884,520 float values)** across the three 360-frame trajectories are bit-identical to the original, including repeat runs. This establishes equivalence for those sampled scenarios, not universal platform/input certification.

Existing regressions: **33 passed / 0 failed / 1 skipped**, 34 selected cases, 9.3927318 seconds. Includes cape pins/stretch/wind/body proxies/80 m/s motion/hitches/corpse mesh ownership, plus existing respawn, hold/motor/health/contact/audio and two Run contact checks. The native cape graphics capture is opt-in and was skipped. No tests/assertions were added or edited; earlier full-suite failures remain unresolved.

## Rendering suspects, not GPU timings

The native survey inventories saved scenes without Play and stages a 16:9 camera at the player's saved spawn with the authored `(5,3,0)` offset / 12 m distance / 60-degree FOV. It estimates LOD selection from group size, distance, FOV and LOD bias, then checks renderer bounds against the frustum. Terrain tessellation, runtime-spawned Titans, shadows and additional passes are **not included**. Occlusion and native renderer batching are not modeled. These are workload proxies, **not actual triangles submitted by a measured frame or draw-call counters**. Older initial reports used the batch Editor's implicit aspect; `inventory-16x9.json` explicitly fixes/reports 16:9.

Current Editor quality is Very High, LOD bias **1.5**:

| Saved environment / two starting view pitches | Estimated mesh triangles | Material-submission proxy |
| --- | ---: | ---: |
| Giant Forest 2 | **5.40–6.00 million** | 611–642 |
| Shiganshina | **0.755–0.763 million** | 1,860–1,861 |

Forest breakdown: **2.47–2.57 million wall triangles** and **2.90–3.40 million tree triangles**. Trees already have LODs; repeated forest wall meshes do not. City uses more material/submesh submissions despite fewer triangles. SRP Batcher reduces setup cost but does not by itself merge all those draws. Scene files currently have no baked occlusion data. Runtime Titans add additional skinning, animation, nav/combat/physics and draw work; saved-scene authoring counts must not be mistaken for runtime population counts.

A hypothetical existing-LOD bias of **1.0** reduces these geometry estimates to **4.37–4.77 million** (forest) and **0.494–0.504 million** (town); submission proxies stay unchanged. This was only calculated in the survey: **no saved quality setting was changed**. Standalone's default quality is Ultra / LOD bias 2, which may keep detailed LODs farther away than the Editor; confirm the actual runtime quality before comparing.

The active URP asset `Assets/Renderer/Renderer.asset` already uses SRP Batcher, **50 m shadows, one cascade, 2048 shadow map, no soft shadows, 1x MSAA, no required depth/opaque texture**. Do not blame the legacy QualitySettings shadow fields alone: URP has its own controls. The main camera's far plane is 5,000 m. Graphics/quality assets, scene lighting, camera framing and physics settings were not modified in this pass.

## Follow-up after user Play feedback

The user reported a steadier **~45 FPS** after the cape fix and authorized another pass. **`docs/forest-distance-performance.md`** records the applied render-only wall LODs / earlier existing tree LOD transitions, static workload reduction and honest regression limitations. The user subsequently reported **~100 FPS**, then **~45 FPS after a BTW grapple-turn edit**; these are user observations, not controlled before/after profiler captures.

### Grapple-turn follow-up

Hash verification confirmed all six optimized Forest prefabs and every published wall-distance FBX/meta still match `Library/WorldLodBaseline.json`; the cape remains `cf6ccbc680bd52e67cee9f6e886aa10d67c11f102b038a956b74c0128f72808f`. Quality, URP, camera, physics/time settings and Town scene also match the pre-LOD baseline. Outside the intended visual prefabs, the source changes since that baseline were only `ODM_Movement.cs`, the shared Player prefab and the Forest scene. The movement diff adds a frame-rate-independent turn-speed setting, not expensive loops/allocations. Current turn/acceleration tuning was preserved.

Read-only inspection of the user Editor log found **10,998** `Animator is not playing an AnimatorController` warnings, with repeated `ODM_Movement.Gas` / `camAnim.SetBool("StartGas", true)` stacks and **145** warnings in a recent Play segment. The saved reference points to the inactive retired `ThirdPeronscam` rig. This is a real repeated-warning path and a plausible Editor overhead contributor; it predates the turn edit and is **not proof of the entire reported FPS regression**.

`ODM_Movement.SetCameraGas` now sends the legacy parameter only when the target Animator exists, is active/enabled, has a controller and is initialized. All four camera gas writes use that guard. No global logging suppression, camera movement ownership, grapple math, player animation, physics or user tuning changes. The user Editor was not controlled/stopped/reloaded/saved and no synthetic input was used.

Existing focused checks: **9 passed / 0 failed / 1 opt-in graphics skip**, ten selected cases, 5.101245 seconds. Cape/contact/hitch/corpse checks plus real hooks/suspension/release/health/moving-anchor checks; no new tests/assertion edits, no direct warning-output assertion or live FPS certification. Prior broader failures remain unresolved. Evidence: `ArtSource/Performance/GrappleWarningFollowup/`. Fresh user Play and same-view/population/resolution comparisons remain necessary; if still ~45, inspect Game/Scene-view load, Console, VSync and a real Profiler capture rather than reverting preserved optimizations or blaming the turn edit.

## Live mouse-motion capture: Inspector overhead

After reconnecting Unity MCP, the user reproduced **90–110 FPS stationary → ~45 FPS while rotating → 90–110 FPS after stopping**. The user also reports a similar drop when moving the character, but character movement was not part of this recording. Real user input only; the assistant enabled/stopped profiling, never started/stopped Play, changed selection, saved/switched a scene or supplied synthetic input. Deep Profile and allocation callstacks were off; the GPU profiler area was off.

Raw capture remains local as `Library/PerformanceSurvey/MouseMotionLive.raw` (~1.94 GB). A guarded closed Unity2021.3.25f1 worker loaded/analyzed it without source/editor changes. Native loading retained **300 frames / ~7.59 seconds**. Therefore the following groups are **fast/slow frame-duration cohorts**, not precisely annotated full stationary/moving phase averages:

| Captured cohort | Frames | Mean frame | InspectorWindow.Repaint, inclusive | Camera LateUpdate, inclusive |
| --- | ---: | ---: | ---: | ---: |
| 6–13 ms frames | 20 | 10.17 ms | 0 ms | .0374 ms |
| 18–35 ms frames | 264 | 24.68 ms | 12.48 ms | .0406 ms |

CPU render-pipeline submission averaged ~1.00 ms in both cohorts; this is **not GPU time**. The render thread spent substantial time waiting for commands from the main thread. Dominant observed extra work is **Editor Inspector repaint / IMGUI**, with **Player selected** in subsequent read-only live inspection. Camera input/collision is not the dominant measured cost. Inclusive nested markers must not be added together.

**User confirmation:** the user subsequently confirmed Inspector overhead was causing the movement-related drop after trying the Inspector workaround. Keep heavy inspectors closed/locked to lightweight assets during Editor gameplay testing. Do not silently alter selection/layout, graphics, physics, camera response or user tuning. Editor Inspector rendering is absent in standalone, but no standalone performance was measured/certified. No game-code change was applied for this finding; the mouse-polling hypothesis is no longer the leading explanation.

**Subsequent camera request:** the user then requested the original eased follow/FOV back, removal of added speed feedback, and finally camera collision disabled. Current behavior is documented in `docs/player-camera.md`; the collision-off toggle is set on the shared MainCamera prefab for both maps. This was a requested presentation change, not a necessary performance workaround. Current saved shared Player ODM defaults are acceleration **55**, maximum speed **80**, grapple turn **90 degrees/s**; Forest additionally retains local acceleration **65** and turn **70 degrees/s** overrides. These are user-authored values preserved for publication, not performance-driven motor downgrades.

Evidence: `ArtSource/Performance/MouseMotionLive/summary.json`, compressed native frame analysis and worker log. All 19 published optimized visual asset hashes still match. Prior regression failures and other live limitations remain unresolved.

## Next investigations / fixes in priority order

1. **Compare a repeatable live workload after the cape fix.** Confirm map, Game-view resolution, Editor versus standalone, active Titan count and VSync. Use Deep Profile **off**. In CPU Hierarchy inspect `ODM.ScoutCape.Simulate`, animation, physics and rendering. Large `Gfx.WaitForPresentOnGfxThread` can mean GPU/presentation/VSync waiting, not proof that the named C# script is slow.
2. **Distinguish pixel/GPU load from CPU load.** At the same view/population, compare lower resolution/render scale and VSync off as temporary user-controlled experiments. A large resolution-related improvement points toward GPU/fill/bandwidth, not necessarily one particular shader. Compare standalone without an actively rendering Scene view. Do not claim uncapped FPS is identical to smoother frame pacing.
3. **Render-only forest wall LODs — implemented in the follow-up:** current near meshes and exact hook/physical colliders are preserved; simpler mid/far visual meshes were added. This addresses the sizable wall load which reducing tree LOD bias cannot solve. No wall collision or nav geometry was removed/altered. See the follow-up document for measurements and the two unchanged legacy-test limitations.
4. **Optional Balanced graphics preset:** existing visual LOD bias near 1.0 and user-selectable render scale. This trades visual detail/pixels for performance, so do it explicitly rather than silently lowering approved art, skin weights or physics quality.
5. **Town draw setup / occlusion:** inspect Frame Debugger's actual draws/state changes. Consider spatially bounded static visual batching/instancing or appropriate baked occlusion, retaining independent colliders and usable rooftop geometry. Don't blindly combine the entire map into one uncullable mesh.
6. **Titan population / skinning / AI:** capture CPU/GPU cost at increasing populations. Perception/repath are already interval-limited. Preserve animation updates used for contacts, exclusive movement ownership, navigation sizes, ragdoll solvers and current 12-Titan cap; camera-invisible does not mean gameplay-inactive. Visual mesh LOD/skinning optimization requires bounds/detachment review before changing offscreen flags.

No global physics timestep/solver changes, disabled contacts, reduced body clearance, altered nav bakes, shortened combat or added follow smoothing are part of this optimization.

## Reproduction and evidence

Tool: **`tools/profile_gameplay_performance.py`**. Requires a *closed*, correctly marker-owned existing full-project worker. It snapshots current assets incrementally, writes its helper only inside the worker, refuses to run in Play, never publishes scenes/assets, and checks source Assets/ProjectSettings hashes afterward.

```text
python tools/profile_gameplay_performance.py --label original --cape-revision becdf248
python tools/profile_gameplay_performance.py --label optimized
python tools/profile_gameplay_performance.py --label environment --inventory-only
```

`--cape-revision` substitutes only the worker's cape file, never reverts source. `--fps 30|60|120` selects a simulation delta, not a target/measured frame rate. A new survey restores the worker's current source asset snapshot. No source art, saved map, preferences or physics settings are rewritten.

Evidence under **`ArtSource/Performance/FirstPass/`** includes raw native survey reports/logs, repeated before/after summaries, trajectory hashes, existing regression XML/log and preservation hashes. No full-project, live FPS/GPU, standalone/WebGL, device, performance target or fresh auditory certification is claimed.
