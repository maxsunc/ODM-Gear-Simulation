# Shared gameplay prefab migration

**Local implementation, not committed/pushed at capture. Git history is authoritative.**

Ten common roots in the current saved Giant Forest 2 and Shiganshina scenes now link to the same prefab assets under `Assets/Prefabs/SharedGameplay/`: Player, MainCamera, PlayerFollower, LeftHook, RightLine, HUD, EventSystem, GameManager, MusicSource and TitanSpawner. Each map contains exactly one instance of every shared asset. The retired Cinemachine rig remains scene-local.

Usage, override caveats and exact paths: `docs/shared-gameplay-prefabs.md`.

## Evidence

- `native-migration.log`: completed guarded Unity 2021.3.25f1 / URP12.1.11 authoring in a closed worker, without Play, input, physics stepping or navigation baking.
- `migration-summary.json`: readable prefab/root mapping, scene object/component counts and exact retained property overrides.
- `migration-report.json.gz`: full native per-owner before/after digest report, compressed without altering its contents. **12,970 forest owners and 9,971 town owners matched after conversion and after saving/reopening.** Includes assets, references, authored transforms/component settings, tags/layers, active state and hierarchy. Root player/camera topology is unchanged.
- `shared-prefab-map-regressions.xml` / `.log`: **3 existing tests passed, 0 failed/skipped**, 2026-11-07 21:02:45–55Z, 10.6890413s. Covers all five forest navigation routes/walls, 7x/10x native agents/spawns without rebake, and real player-only death/respawn preserving world actors/navigation/music.
- `preservation-and-validation.json`: exact source hashes, preserved scene/rig/script GUID assets, source path preservation, instance counts and limitations.

The original saved scene files are backed up locally in `Library/SharedGameplayPrefabsOriginalScenes/`. Original scene metas/GUIDs, map assets, nav data, runtime scripts and current user tuning stayed unchanged. Source snapshots included the user's pending scene/audio/camera edits. RenderSettings, LightmapSettings, NavMeshSettings and OcclusionCullingSettings blocks stayed text-identical.

## Integrity scope

Native prefab identity/file IDs necessarily change; references are compared by canonical hierarchy/component keys or asset GUID/local ID, not transient IDs. Native screen-space root Canvas layout is display/editor-driven and excluded; authored child UI transforms plus Canvas/Scaler settings are checked. Unity can normalize the old FBX's editor-only BoneRenderer ordering; its presence/data and all runtime component ordering are checked. Native Unity YAML blank-value trailing spaces are retained, not presented as a clean generic whitespace-lint result.

Common parameters inherit from the prefab rather than redundant value overrides. Scene bindings/spawn positions/map-specific bounds/size choices remain local. Existing lightning volume values are **forest 0.1 / town 0.25**; town's override was deliberately retained, not silently unified. Material references with equal asset identity were left inherited.

## Safety and limits

No live user Editor was stopped, saved, switched or reloaded; source publication copied only the two saved scene files and added prefabs. Existing PlayMode checks ran only in the closed owned worker, with **worker-only company/product identifiers** isolating PlayerPrefs from the user's game. No tests/assertions were added or edited.

No full-suite, standalone/WebGL build, Shiganshina live traversal/crowd or device/performance/subjective-audio certification is claimed. Previously recorded broad-suite failures remain unresolved. Fresh user Play on both maps remains appropriate, particularly after Unity reloads the updated saved scene. Preserve any unsaved Editor work instead of discarding it to reload.
