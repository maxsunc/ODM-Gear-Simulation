# First performance pass: measured cape CPU, estimated environment load

The existing project work was committed/pushed to `main` as **becdf248** after confirming PR #1 had merged. The performance change/evidence here is subsequent **local, uncommitted work on main** at capture. Git history remains publication authority.

## Repeated CPU comparison

Closed owned Unity 2021.3.25f1 / URP12.1.11 worker; headless Editor Mono, **no Play, input, physics stepping, rendering or live FPS measurement**. Actual shared Player prefab, approved Scout/Run and current cape defaults. Simulated delta 1/60 s, 60 warm-up + 360 timed calls per mode. Presentation pose staging and binary trace writing are outside the timed `Simulate` interval. CPU detected: i7-13700H. Windows GPU inventory detected Intel Iris Xe; the survey itself uses **Null Device**, not GPU timings.

- `baseline.json` / `baseline-repeat.json`: original cape, source SHA `9f5945e8dd6f69e801984cd342d158781f5692d5355f939c77bc97b3effdd00b`.
- `optimized-bounds.json` / `optimized-repeat.json`: final cape, source SHA `cf6ccbc680bd52e67cee9f6e886aa10d67c11f102b038a956b74c0128f72808f`.
- Corresponding `performance-survey-*.log`: native logs for these runs.

Idle / 40 m/s forward / turning presentation each measured **8.66–8.79 ms original, 3.04–3.08 ms optimized** per call. Roughly 65% reduction / 5.7 ms saved, not a promised gameplay FPS improvement. Full sampled particle trajectories are **bit-identical in all four runs**: 294,840 vector positions / 884,520 float values across the three modes. Raw binary traces stay in the disposable worker Library; reproducible trace SHA256 values are in each report.

The fix caches constant limits/structural links and padded proxy dimensions; conservatively rejects clearly distant points before exact body-proxy projection; avoids repeated struct copying and multiplying corrections by zero/one. Solver frequency, iterations, source length, all seven body/gear proxies, constraints, mesh and animation stay unchanged. A live marker `ODM.ScoutCape.Simulate` was added. No native physics, camera, movement, saved quality, scene or art changes.

## Rendering inventory, not draw counters

`inventory-16x9.json` explicitly stages a **16:9** camera at the current saved player spawn, `(5,3,0)` offset and 12 m distance, with two view pitches and current 1.5 / hypothetical 1.0 LOD bias. Earlier timing reports used the batch Editor's implicit aspect for their incidental inventories; use the explicit report for quoted geometry values.

Estimated forest frustum mesh load: **5.40–6.00 million triangles**, of which walls are **2.47–2.57 million**, trees **2.90–3.40 million**. Estimated town load: **0.755–0.763 million**, but a higher **1,860–1,861 material-submission proxy**. Existing LOD bias 1.0 would reduce these estimates; it was **not applied to saved settings**. Walls in the forest have no distance LODs.

Estimation selects one LOD by projected size then tests renderer bounds against a frustum. It does not model occlusion/native batching, terrain tessellation, runtime-spawned actors, shadows or additional passes. It is not a GPU capture, actual triangle/draw-call counter, or normal-play frame-time test. Saved authoring scenes currently have no placed active Titans; the gameplay managers/spawner instantiate them at runtime, so authoring renderer/physics counts are not runtime population costs.

## Regression / preservation

`existing-regressions.xml` / `.log`: **33 passed, 0 failed, 1 skipped**, 34 selected existing cases, 2026-11-07 23:22:52–23:23:01Z, 9.3927318s. Includes all cape non-graphics checks, respawn/motor/hold/health/contact/audio and two Run contact checks. Graphics capture was explicitly opt-in and skipped. No tests/assertions were added/edited; earlier broad-suite failures remain unresolved.

`preservation-and-validation.json`: timing aggregates, sample hashes, current source/worker match, preserved Assets/Settings hashes and limitations. Of **1,622 pre-existing Assets/ProjectSettings paths**, **1,621 stayed byte-identical**; only `Assets/Player/Scout/ScoutCapeCloth.cs` changed. Full source manifests remain local in `Library/PerformanceSurvey/*-preservation.json`.

No live Editor stop/save/reload/switch/input, source asset publication by the survey, player preference changes or physics stepping occurred in the surveys. Existing regressions ran only in the separate closed owned test worker. No full-suite/build/WebGL/device certification or audible/feel/FPS success is claimed. Fresh user Play is needed; CPU or GPU dominance remains to be established in the reported normal workload.

Details, safe next steps and reproduction: **`docs/gameplay-performance.md`**, **`tools/profile_gameplay_performance.py`**. Native helper `OwnedPerformanceSurvey.cs` exists only inside the worker, never in source Assets/Tests.
