# Player flight wind

Uses the supplied **`Assets/Audio/windLoop.wav`**, GUID
`82697b511562eb443b5606ff3217c175`. Its bytes/importer are unchanged. Source metadata:
18.855s, stereo,48kHz,24-bit PCM; Unity2021 imported it successfully. A loop need not
match flight length. No trimming, amplification, resampling or baked seam edit was
performed; subjective loop/seam balance still needs user audition.

## Behavior

- `PlayerHealth.Start` installs at most one `PlayerWindAudio` on real movement/ODM/
  CharacterController-backed players. Both saved gameplay scenes get it on fresh
  Play without being resaved; data-only health fixtures do not acquire controllers.
- One scene/player-owned looping **2D** source, immediately routed through the
  existing **SFX/Master** buses. Doppler is off. Music and all existing sound gains,
  clips, preferences and sources are untouched.
- A read-only fixed-step root-displacement observer, ordered140 after the motors,
  supplies actual airspeed. Swinging, powered flight and free fall all qualify;
  holding a thrust key or being attached alone does not manufacture wind.
- Native CCT grounding and the existing Scout read-only Grounded result suppress
  the target volume on landing. The legacy `Movement.CheckGround()` is never called
  because it also writes the gameplay rotation.
- Smooth volume rises from8m/s to55m/s, capped at.35; small jumps/slow drops are
  silent or very quiet. Default full-range fade-in.35s/fade-out.25s. Pitch subtly
  varies.95–1.08 with speed. The loop is not restarted on each speed change.
- Death, grab/suspension, pending safe release, disabled controllers/components,
  pending respawn, game/application pause, focus loss and invalid/stale motion
  silence/reset the loop. Landing and speed loss use the ordinary fade-out.
- Two valid displacement samples are required. Nonfinite samples, fixed dt>.1s,
  render hitches>.2s, and implausible motion>160m/s fail closed for sound only.
  Successful player-only respawn explicitly resets observer history after relocation.
  These are audio rejection rules, never player velocity caps or physics changes.

## Tuning

Select **`Assets/Resources/Audio/PlayerWindAudio.asset`**:

| Setting | Default |
| --- | --- |
| Loop | supplied `windLoop.wav` |
| Maximum Volume | .35 |
| Minimum Speed | 8m/s |
| Full Volume Speed | 55m/s |
| Fade In / Out Duration | .35s / .25s |
| Minimum / Maximum Pitch | .95 / 1.08 |
| Maximum Trusted Speed | 160m/s, observer only |

Runtime component: `Assets/Player/Scout/PlayerWindAudio.cs`.
Configuration type: `Assets/Player/Scout/PlayerWindAudioPalette.cs`.
The generated source appears under the player as **[Player SFX] Flight wind**;
disabling the observer stops it. No persistent wind singleton/menu source exists.

## Verification scope

Unity2021.3.25f1 / URP12.1.11 retained. Closed marker-owned worker only; no live Play
stop, input, scene save/switch or native physics automation in the user's project.
No test code or assertions added/edited.

The final focused run passed26 existing audio/respawn/motor/health/hold checks. The
broader run passed165/167; its two failures still expect the historical1s regrab
grace rather than the approved4s in already-published target code. Receipts:
`ArtSource/UI/InGameMinimal/{focused,broader}-existing-regressions.{xml,log}`.
This is not a full suite, loop audition, new wind lifecycle/volume certification,
or keyboard/menu interaction certification. Fresh Play should review takeoff,
swing/coast, fast fall, landing, capture/escape, death/respawn and J pause/resume.
