# Main menu and hold-Tab stats

## Current minimal in-game revision

Both Giant Forest and Shiganshina use the same runtime-built interfaces, without
resaving either scene or changing its user tuning.

- **Hold Tab:** a compact charcoal card titled **Stats**, containing only **Kills**
  (current run), **Total kills** (career), **Damage done** (actual applied damage
  this run), and **Limbs severed** (this run). Four aligned label/value rows, larger
  numbers, quiet dividers and one restrained accent. No clocks, death/streak/eye
  rows, duplicated kill headline, section labels or footer hints. No pause/cursor
  changes or raycaster; it hides while J has paused the game.
- **J menu:** `GamePauseMenu` replaces only the old panel at runtime. Map name,
  Resume/Main menu/Quit, Master/SFX/Music sliders and a small resume hint. The old
  movement-tuning panel and separate IMGUI audio box are no longer displayed.
  The menu pauses and restores the prior time scale/cursor state. J resumes;
  Escape cancels confirmation or resumes. Leaving/quit confirms and saves, Menu
  uses the named async destination, and Editor Quit never stops Play. The actual
  saved legacy panel remains intact, inactive. No movement settings are rewritten.
- Wind audio from the preceding request is also installed; see
  [player-flight-wind.md](player-flight-wind.md).

Native static previews in `ArtSource/UI/InGameMinimal/` cover both maps at
1920x1080 and1440x1080. These show actual runtime uGUI over saved map photos,
zero authoring counters and100% authoring audio values—not live input/kill/audio
proof. `tools/capture_ingame_ui.py` only uses the closed owned Unity2021 worker.

Current focused regression run: **26 passed/0 failed/0 skipped**, covering the
existing audio mixer, player-only respawn, real motor suspension, health and
held-damage gates. An earlier broader audio/ODM run was **165 passed/2 failed**:
`EscapeQteBlockedExitCancelsBiteImmediatelyAndKeepsSuspensionUntilSceneryClears`
and `EscapeQteGraceBlocksAllHandsForOneSecondButNeverBlocksDamage` still assert
**1s**, while the already-published target code uses the approved **4s** regrab
grace. Neither tests nor the4s setting was changed. Both XML/log receipts are
retained with the previews. No new tests; these are not full-suite, wind-audition
or hands-on new-menu interaction certifications.

## Current map extension

The later Shiganshina task extends this same frontend with **Giant Forest / Shiganshina**
destination selectors, dynamically updated title/photo/loading/deploy text, and exact
`Assets/Scenes/ShiganshinaDistrict.unity` routing. Forest remains the default. The new
map is appended to Build Settings; the legacy maps are still not offered. See
`docs/shiganshina-district.md` for map assets and current31-check filtered regression
receipt. The forest-only statements/evidence below describe the original frontend
revision, not a restriction on the now-expanded selector.

## Player-facing changes

- In gameplay, **hold Tab** to display Stats; releasing Tab hides it.
  It is a read-only overlay: no time-scale change, cursor unlock, input suspension,
  camera override, or gameplay raycaster. Losing application focus hides it too.
- **Visible:** kills, total kills, damage done and limbs severed only. The original
  death/life/eye/time counters continue recording internally; they are not shown.
- **Stored career data:** total Titan kills, total deaths and best kill streak, saved locally
  across normal application restarts. Totals begin with this feature; past kills
  cannot be reconstructed from old gameplay. This is single-player/local storage.
- A run is one loaded gameplay world. Player-only respawn keeps run totals and the
  world, resets kills-this-life/time-alive only after successful safe respawn.
  Loading a new gameplay scene starts another run; career totals remain.
- Clocks use scaled gameplay time. Existing J-menu pause stops those clocks;
  viewing the Tab record does not introduce a pause.

## Menu

`Assets/Scenes/Menu.unity` now contains an editable uGUI frontend: dark forest
backdrop, gold/cream typography, Free Roam destination card, Options, loading
screen, and quit confirmation. The original camera/light/music objects remain.
The original canvas/callback data is retained inactive as **Legacy Menu UI
[archived]**, with its `SceneManagement` component disabled to avoid duplicate UI.
All91 original serialized objects survive. Original AudioSource bytes, including
clip/pitch/volume, are unchanged.

The initial Free Roam revision offered **Giant Forest only** (now also Shiganshina),
and loads the forest asynchronously by exact path:
`Assets/Scenes/GiantForest2.unity`, GUID `b518c3e1bf644076a3fc6994130348c0`.
It was already enabled in Build Settings. No fragile build-index routing and no
Build Settings edit were necessary. The user-facing primary name is **Giant
Forest**; the physical filename/GUID are deliberately retained to protect current
scene edits and references. The old FreeRoam/Shiganshina/FreeRoam2 scenes remain
legacy assets, not playable choices in this menu.

Options apply immediately and save on this device:
- Master / SFX / Music through the existing `GameAudio` mixer. Original source
  gains, pitches, clips, mixer buses, and the M mute/restore shortcut are retained.
- Look sensitivity `.25x–3x`, default1x multiplying—not rewriting—authored camera
  sensitivity. Both existing camera scripts use it.
- Invert vertical look. The alternate camera's authored inversion remains its
  default until the user explicitly chooses this option.
- Borderless fullscreen / windowed, applied in standalone builds only (no Editor
  window takeover), and vertical sync.
- Quit asks for confirmation, saves preferences/totals, and uses Application.Quit.
  In Editor it displays a note rather than forcibly stopping Play. Quit is disabled
  in WebGL, where closing the page is managed by the browser.

The new menu normalizes time scale/cursor **only in the actual Menu scene**.
The stats overlay never does this. The latest shared in-game J menu replaces the
legacy J/audio presentation at runtime as described above.

## Ownership and receipts

- `Assets/UI/GameStats.cs`: persistent service, weak per-Titan kill deduplication,
  counters, debounced career saves, read-only hold-Tab canvas. No corpse scan.
- `Assets/DamageDealer.cs`: passes only accepted `ReceiveDamage` receipts to stats
  before the existing HitConfirmed event. Counts a kill only for Killed + a dead
  body belonging to the real player's scene. Limb/eye hits are not kills; direct
  arbitrary Titan death/despawn/cleanup is not player credit. AppliedDamage, not
  overkill Damage, supplies damage totals.
- `Assets/PlayerHealth.cs`: binds the real movement-backed player, records guarded
  Die once per life before existing death callbacks, and resets life-only counters
  in successful CompleteRespawn. No health/respawn timing or ownership changes.
- `Assets/UI/GameMenu.cs`: menu navigation, options, named asynchronous deployment.
- `Assets/UI/GameOptions.cs`: separate `ODM.Options.*.v1` preferences; does not
  change movement, combat, physical settings or serialized camera baselines.
- Career keys: `ODM.Stats.Kills.v1`, `ODM.Stats.Deaths.v1`,
  `ODM.Stats.BestStreak.v1`. Existing audio preference keys are reused unchanged.
- `Assets/UI/FrontendUI.cs`: common uGUI styling, safe fitted1920x1080 layout.
- `Assets/UI/Frontend/ForestBackdrop.png`: cropped/desaturated derivative of our
  existing owned native `MapReview/RuntimeRocks.png`, not changed environment art.

## Authoring and evidence

No live Editor takeover, Play stop, input injection, scene switch/save, physics
automation, new tests, or test modifications. Unity2021.3.25f1 / URP12.1.11 retained.

`tools/build_game_frontend.py --action build` snapshots current runtime and GUID-
resolved saved Menu dependencies into the marker-owned validation worker, then
runs `FrontendMenuBuilder.BuildOwned`. After explicit review:
`python tools/build_game_frontend.py --action publish` publishes only Menu.unity,
with source-input/concurrent-edit hashes and unchanged scene GUID enforced.
For later revisions, build with `--revise` to explicitly replace only the generated
frontend in the disposable worker. It never rebuilds the actual forest or prefabs.
A filtered test run replaces the worker Assets snapshot: rebuild before recapture.

`tools/capture_game_frontend.py` renders actual uGUI natively in the owned Editor,
without entering Play. It does not write preferences or synthesize gameplay.
`ArtSource/UI/Frontend/` contains Free Roam/Options1920x1080, Options1440x1080,
and the actual stats canvas over a forest photo plate. **The zero counters in that
plate are authoring defaults, not a runtime kill/death test or a live forest capture.**
Native renderer: Intel Iris Xe. Font/layout review passed16:9 and4:3.

Reused **16 existing unchanged** PlayMode checks:9GameAudio cases; confirmed hit
collider deduplication; kill toast/finisher receipt; regional nape health; real-health
external death; three-second player-only respawn; respawn reset/tuning preservation;
and repeated void-death/new-life behavior. **16passed/0failed/0skipped.**
Evidence: `existing-regressions.xml`; latest runner state is filtered, NOT a full
suite. These are regression checks, not exhaustive new statistics/menu interaction
coverage. No fresh full-map launch, standalone fullscreen/quit, restart persistence,
physical keyboard Tab play-through, or device/WebGL behavior certification.

At feature snapshot555 protected current asset/source paths were checked; only
intentional PlayerHealth and Menu changes differ, with the other553 unchanged,
including the user's forest, prefab, palettes, audio, Titan rig/art and eye pose.
DamageDealer and the two camera edits are separately source-reviewed/compiled.

The original stats/frontend and Shiganshina extension were published in `1d8840ab`,
including the earlier eye/grace checkpoint `cc26cf0a`. Faster attack decisions and
dedicated stomp audio were subsequently published in `93d8a511`. The current
minimal in-game UI and flight-wind changes were uncommitted at native-art capture;
subsequent publication is recorded in Git history.
