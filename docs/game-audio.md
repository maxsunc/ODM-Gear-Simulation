# Shared game audio mixer

Unity **2021.3.25f1** / URP12.1.11, no upgrades, scene/prefab rewrite or live Editor
input. Follow-up to `docs/player-combat-ux.md`. Mixer follow-up originally local;
user subsequently authorized the pre-SFX checkpoint push through **4d425ace**.
New NPC SFX implementation remains a separate local commit.

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

## Confirmed grab sound

`Assets/Audio/titanGrab.mp3` plays once on a successful ground or aerial capture,
for either hand and placed/spawned Titans. `TitanGrabAttack.CaptureConfirmed`
fires only after `TryHold` succeeds and the capture/QTE setup is complete; reach,
miss, carry, repeated hold updates, QTE and feeding do not replay it.

`TitanAudioPalette` subscribes once per Play (including domain reload disabled),
uses a short-lived spatial source at the capturing hand through the existing SFX
mixer, and retains the original clip/pitch/importer. Resource palette fields
`grab` and `grabVolume` (default1) expose the clip/level. The original.528s cue
cleans up after its tail; scene/parent removal also disposes it. No gameplay or
other palette tuning changed. Unity2021 compile and the existing both-hand native
preview completed; no new/changed test code or full-suite claim. Device playback
and preferred loudness remain a user check. Included in the user-requested publish
checkpoint after the stationary Search-look revision; unrelated palette tuning is
left uncommitted.

## Supplied Titan SFX follow-up

`Assets/Resources/TitanAudioPalette.asset` references the seven new supplied clips,
without renaming, converting or changing their imports:

| Event | Clips / behavior |
|---|---|
| New blindness | Random `titanScream.wav` (the first supplied scream) or `titanScream2.mp3`; once on entry, not ordinary face hits or repeated already-blind damage. |
| Walk/run footfalls | Random `titanFootstep1/2/3.mp3` on each lifted sole's return to real support. |
| Successful random-spawn arrival | `titanLightning.mp3` after actual activation, never during warning or cancellation. |
| Confirmed lethal bite | `bloodSplat.mp3` once; no cue on nonlethal, blocked or escaped bites, or generic deaths. |

`TitanRuntimeActor` installs a runtime-only `TitanAudioEmitter` on registered placed
and spawned gameplay Titans. Passive prefab and legacy standalone labs are not
rewritten. Scream + left/right foot sources are spatial SFX; the personal eaten cue
is independent 2D SFX, not parented to the player, catcher or corpse. All route to the
same Master/SFX mixer immediately. Existing blood/kill/slash layers remain.

**Synchronization follow-up:** see [Titan footstep sync](titan-footstep-sync.md)
for the corrected plant threshold, baked-terrain elevation handling and real
frame-rate regression evidence. No supplied clip/import/palette retuning.

Footsteps observe animated foot-box soles and scene-local ground queries, with
lift/plant hysteresis. No approximate time interval, modified clip/events, root motion,
Animator/bone writes, global Physics synchronization or collision/force changes.
This naturally follows authored walk/run blending, randomized cycle offsets and
cadence. Idle, blindness, missing legs, unready actors, teleports/hitches and absent
support cannot manufacture steps. Emitters use their own RNG, never Unity's gameplay
RNG. Sources stop on disable/death and are cleaned up with the emitter.

Tune `screamVolume`, `footstepVolume`, `eatenVolume` on the palette; spawn loudness
still uses the spawner's existing `soundVolume`/distance fields. An assigned
`spawnSound` still overrides the default. Turn off `useDefaultSpawnSound` for an
intentionally silent empty slot. Global balancing stays in J/main-menu SFX.

Pre-feature checkpoint **4d425ace** includes the user's exact saved taller-wall scene,
Titan prefab tuning and new clips; pushed to `origin/titan-combat-updates` BEFORE any
SFX code changes. No further scene/prefab edits, navigation bake or live Editor input.

### Current SFX validation

Before implementation, current saved prefab already produced **248 pass / 5 fail /
1 graphics skip**: two protected Scout Run failures plus three fixture conflicts with
user-authored enlarged face triggers, 12s blindness and changed gait speed references.
The saved tuning and original assertions were preserved, not reverted/relaxed.

After integration: **257 pass / same 5 fail / 1 graphics skip**, 263 cases total.
Nine added SFX cases pass: exact asset references/routing; real blind-event filtering
and private RNG; four real animated walk/run/cycle-offset/1x/3x/5x sole tests;
idle/blind/injured/hitch/no-ground exclusion; lethal-bite deduplication/independence;
and actual spawner's default lightning with visuals disabled. Existing real left/right
Scout bite, escape, obstruction and nonlethal fixtures additionally assert the new eaten
cue. Strict existing arm/late-mouth/corpse-speed regression assertions remain unchanged.
Evidence: `ArtSource/Audio/GameAudio/titan-sfx-validation-summary.json`.

Listening/loudness balance, live full-map feel and WebGL audio-device behavior still
need player playtesting; automated routing/cue dispatch is not measured DSP output.

## Original mixer validation (before the user's newer prefab tuning)

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
