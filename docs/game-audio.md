# Shared game audio mixer

Unity **2021.3.25f1** / URP12.1.11, no upgrades, scene/prefab rewrite or live Editor
input. Follow-up to `docs/player-combat-ux.md`; local commit permission continues,
no push authorized.

## Player controls

- Audio sliders **Master / SFX / Music** in the existing **J pause menu**, lower
  right. Also displayed in the existing **Menu** scene without editing its asset.
- **M** mutes/restores MUSIC ONLY, including from the main menu. Restores the last
  nonzero music level, even across sessions. **T** song switching stays unchanged.
- Levels are normalized0–1 gains *on top of authored source levels*. Existing quiet
  music (.05 in the game/.1 in Menu), gas volume/pitch changes, clip one-shot scales,
  spatial settings and individual source mutes remain intact.
- Settings use `ODM.Audio.{Master,SFX,Music,MusicRestore}.v1` PlayerPrefs, debounced
  .25s on UNscaled time, also flushed on pause/quit/J-menu close. Pausing gameplay
  does not freeze settings changes. A saved music choice overrides the legacy
  `GameElements.isMuted` startup flag; absent a saved choice, that flag still applies.

## Wiring / extension

**Assets/Resources/Audio/GameAudio.mixer** is a real Unity AudioMixer:

```
Master             exposed MasterVolume
  SFX              exposed SFXVolume
  Music            exposed MusicVolume
```

Exposed values are decibels:1→0dB,.5→-6.0206dB,0→-80dB. Gains multiply through the
Master; editing music does not touch the SFX parameter or AudioSource.volume.
Mixer update mode is UnscaledTime. No compression/EQ/reverb or loudness normalization
was added. Double-click the asset to inspect it in Unity's Audio Mixer window.

`Assets/Scripts/GameAudio.cs` automatically creates one persistent manager before
scene load, reloads saved settings, and routes scene audio, including inactive
sources. `GameElements.musics` defines game music; `SceneManagement.Awake` routes
Menu's existing autoplay soundtrack to Music. Everything else goes to SFX, including
jump, swords, blood, grapple/gas and optional spawn audio. Known runtime-created
sword/spawn sources route immediately. A once-per-unscaled-second fallback catches
additional prefab/third-party sources; explicitly Music-routed sources stay Music.

New sources should immediately call `GameAudio.Route(source)` (SFX) or
`GameAudio.Route(source,true)` (Music), before playing. Public setters are
`SetMasterVolume`, `SetSfxVolume`, `SetMusicVolume`; `ToggleMusicMute` and `Save` are
also available. No new scene installer, controls or source retuning required.

The explicit **Tools > Audio > Create game mixer if missing** utility is create-only,
Unity2021-version-pinned, never replaces an existing mixer and never saves a scene.
It uses Editor-only reflection because Unity exposes no public mixer creation API.
On failure it removes only its own newly created asset; save is asset-scoped.

## Additional blood audio

`blood1.wav` / `blood2.wav` now layer on **confirmed left/right LEG severs** too,
using the same alternation/coalescing as arm severs and kills. Existing sever/kill
sounds are retained. Ordinary leg damage, face blindness and misses still do not
trigger the additional layer. WAV bytes/import settings unchanged. All layers use SFX.

## Validation

Owned Unity2021 worker full PlayMode suite: **251 passed / 2 existing Run failures /
1 graphics-only skip**. Nine audio-specific cases pass, covering real Master-group
hierarchy, exposed dB values, independent gains/mutes, source-property preservation,
inactive routing, actual Menu-controller routing, preferences/reloaded mute restore,
paused gain/save behavior, immediate dynamic sword/spawn routing, duplicate-manager
host-object safety and one-track playlist safety. Existing finisher case now checks both legs plus ordinary-leg-hit rejection,
existing arm/kill behavior and duplicate same-frame kill coalescing. Strict established
character physics bounds, clips and gameplay tuning unchanged.

Fresh result summary: `ArtSource/Audio/GameAudio/validation-summary.json`.
Reproduce: `python tools/run_titan_validation.py --wait` (optional owned `--reuse PATH`).
This verifies real mixer structure, routing and parameter behavior, not measured DSP
output, subjective loudness/listening quality or WebGL audio-device certification.

User's existing GiantForest2 scene changes, generated IDE files and caches are
preserved/excluded. Source ProjectSettings/Packages, character art, animations and
prefabs unchanged. No live scene save/reload/Play stop or synthetic user input.
