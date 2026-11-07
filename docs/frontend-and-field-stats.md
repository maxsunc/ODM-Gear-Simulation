# Main menu and hold-Tab field record

## Current map extension

The later Shiganshina task extends this same frontend with **Giant Forest / Shiganshina**
destination selectors, dynamically updated title/photo/loading/deploy text, and exact
`Assets/Scenes/ShiganshinaDistrict.unity` routing. Forest remains the default. The new
map is appended to Build Settings; the legacy maps are still not offered. See
`docs/shiganshina-district.md` for map assets and current31-check filtered regression
receipt. The forest-only statements/evidence below describe the original frontend
revision, not a restriction on the now-expanded selector.

## Player-facing changes

- In gameplay, **hold Tab** to display the field record; releasing Tab hides it.
  It is a read-only overlay: no time-scale change, cursor unlock, input suspension,
  camera override, or gameplay raycaster. Losing application focus hides it too.
- **This run:** Titan kills, deaths, kills this life, limbs severed, eye stuns,
  actual applied damage, run time and time alive.
- **Career:** total Titan kills, total deaths and best kill streak, saved locally
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
The stats overlay never does this. Existing in-game J/audio UI remains intact.

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

New stats/menu work and the later Shiganshina extension remain local and
uncommitted/unpushed pending user feedback.
The earlier eye/grace checkpoint was committed locally as `cc26cf0a` before this
feature; no push was performed.
