# Player footstep authoring notes and focused regression evidence

## Local revision

Adds the three supplied personal Scout footsteps, randomized without immediate repeats, synchronized to the existing Run gait's actual normalized Animator phase at walking/running playback speeds. Original audio files/importers, the protected Run clip, visual rig/controller, motor tuning, wind, camera work and user scene edits were preserved. Capture-time status: local, not committed/pushed; Git history is authoritative.

Configuration: `Assets/Resources/Audio/PlayerFootstepAudio.asset`. Implementation and limitations: `docs/player-footsteps.md`.

## Static native asset inspection

- `native-import-and-sole.log`: Unity 2021.3.25f1 imported AudioClip metadata and near-silence onset notes; protected visual-only Run clip sampled at 240 normalized phases.
- `run-sole-authoring.csv`: left/right sole heights from the protected clip, using its authored heel/toe sole points at visual scale 1. This is static animation authoring analysis, not physical floor collision evidence. Descending contact within the 0.025m plant pad starts at 0.9666667 left / 0.4666667 right. The left foot is already planted at the first sample, phase zero.
- `native-profile-and-attack.log`: confirms the actual Resources palette resolves all three imported clips, defaults, and decompressed sample data. The strong attacks begin approximately 68.8ms / 56.7ms / 94.0ms into these imported clips. Runtime playback skips to approximately 63.8ms / 51.7ms / 89.0ms, retaining 5ms before the attack, without modifying a file/importer.

Inspection ran in a closed `.titan-validation-owned` disposable Editor worker. It did not enter Play, step native physics, create a player controller, simulate user input, write preferences or save a source scene/clip. Worker-only inspector helpers are not project/runtime code. No auditory/device-output or gameplay synchronization claim follows from these static notes.

## Existing regressions

`focused-existing-regressions.xml` / `.log`: **28 passed, 0 failed, 0 skipped**, 2026-11-07 19:19:32–38Z, 5.5816828s. Includes existing audio mixer, real hold/health/impact, player-only respawn checks and the protected Run rear-leg/alternating-support checks. Source compiled in Unity 2021.3.25f1 / URP12.1.11. No tests/assertions were added or edited.

These tests ran only in the owned disposable worker. They do **not** directly assert the new footstep emitter's phase crossings, voice playback, randomness or lifecycle behavior. They are not a full-suite/build result and do not supersede the earlier recorded 445 passed / 12 failed / 1 skipped Titan-worker result.

`preservation-and-validation.json` records source/worker hashes and preservation: 1,593 pre-existing Assets/ProjectSettings paths, 1,591 unchanged; only intended existing changes are the PlayerHealth installation hook and PlayerRespawn reset hook. New scripts/palette are additive. Supplied MP3s and metas were included in the protected baseline and stayed byte-identical.

## Subsequent grounding compatibility fix

The source was revised after a report of completely inaudible footsteps. The original combined Scout/native `isGrounded` requirement could repeatedly reset the emitter after ODM's later controller moves cleared the last-Move support flag. The updated observer has a read-only short support sweep fallback, explicit startup audio-data requests, and Inspector Status/Footsteps Played diagnostics. User volume/tuning were preserved.

See `GroundingFix/` for the updated source hashes and separate 28-pass existing regression receipt. The initial source hashes and static notes above remain historical; they are not receipts for the revised runtime gate. Neither receipt directly asserts footstep playback or reproduces the reported silence in a live user session.

## Still requires fresh Play review

Audition walking/running at different speeds, first step/acceleration/stopping, jumping/landing, ground grapple transitions, holds/releases, death/respawn, J pause/focus and both maps. Verify mix balance against other SFX, comfort, and actual device/WebGL/DSP latency. Do not treat static imported sample offsets as sample-accurate live output timing certification.
