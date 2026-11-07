# Scout walking/running footsteps

## Configuration

`Assets/Resources/Audio/PlayerFootstepAudio.asset` references exactly the three supplied clips:

- `Assets/Audio/yarzur_ofc-st1-footstep-sfx-323053.mp3`
- `Assets/Audio/yarzur_ofc-st2-footstep-sfx-323055.mp3`
- `Assets/Audio/yarzur_ofc-st3-footstep-sfx-323056.mp3`

Default volume is **0.35**, with subtle **0.97–1.03** pitch variation. Selection uses a private cosmetic RNG and avoids immediately repeating the same clip when another loaded choice exists. Existing Master/SFX mixer controls apply; music, wind and Titan audio tuning are unchanged. These are personal 2D sounds, so camera zoom does not change their loudness.

`PlayerHealth.Start` installs one `PlayerFootstepAudio` on a real Movement/ODM/CharacterController player with an assigned Scout visual. No saved-scene or prefab edits are necessary. Start a **fresh Play session** to install it; an already-running player's Start is not rerun by a script import.

## Matching the feet

The Scout currently uses the same `Run` gait at different playback speeds for slower/faster ground motion. Audio observes the **actual Animator normalized phase**, including its playback speed and real clip length. It does not use a fixed walking/running timer or the authoring recipe's old `.56s` cycle constant.

Static native sampling of the protected **1.16666663s Run clip** found descending sole contact within its authored plant pad at normalized phases **0.9666667 left / 0.4666667 right**. These are the palette defaults. The existing clip, controller, rig, cadence and all animation curves remain untouched. The small entry window permits a first cue only when a newly dominant Run pose is already near a plant. Blend-outs to attacks/flight do not generate old-Run cues. Slow frames emit at most the latest plant, not a catch-up burst.

The supplied files have unequal leading silence. Native imported sample analysis found their first meaningful samples approximately **27.8ms / 25.1ms / 92.2ms** into the clips. With `Skip Leading Silence` enabled, the emitter reads the first 150ms of each loaded decompressed clip once, finds the attack at 10% of the early peak (minimum 0.005 amplitude), retains a 5ms pre-roll, and seeks its own playback voice to that point. This also skips the first two clips' quieter pre-impact scuffs, so their main attacks are not later than the third clip's. No MP3s or import settings are trimmed, normalized, re-encoded or modified. Unsupported/custom compressed/streaming data falls back to an untrimmed start.

Four owned non-looping voices bound overlap and preserve short tails. The ordinary stop can let an already-confirmed impact finish. New steps require grounded locomotion plus trusted measured planar movement. Airborne state, disabled movement/ODM components, controller or visual, death, grab/hold/release-pending, suspension, respawn, pause, lost focus, render hitch, stale samples and rejected position jumps stop voices and invalidate history. Successful respawn explicitly resets the emitter after relocation. No extra landing/jump cue is added.

The speed limit is an **audio sample rejection threshold**, never a velocity cap. No motor, Animator, bone, input, cursor, collision, grounding method or global Physics state is driven by this observer. It reads the existing Scout/CCT grounding states instead of the mutating `Movement.CheckGround()` method. It does not consume Unity's global gameplay RNG.

## Grounding compatibility fix

The initial emitter required both `controller.isGrounded` and `scout.Grounded`. That was too strict for this motor: ODM performs three extra `CharacterController.Move` calls after ordinary movement/gravity, and `isGrounded` reports only the last Move. Losing that transient flag could repeatedly silence the voices and invalidate all motion/phase history while the player was visibly running on the floor.

`HasStepSupport()` now requires the Scout's ground state and rejects upward jump/release velocity, accepts the native controller support flag when available, and otherwise performs a short read-only WORLD-UP sphere sweep at the capsule sole. It uses the existing ground layer mask, ignores self/triggers, requires a walkable normal, and fails closed on a saturated hit buffer. It does not move/snap the player or modify the controller/motor. The support dimensions follow the already-existing jump-support query. Startup also explicitly requests data for any unloaded assigned clips, without playing them.

During Play, select **Player → Player Footstep Audio → Runtime Diagnostics**: **Status** explains the current gate and **Footsteps Played** should increase while walking/running. An increasing counter with no sound points toward audio output/mixer settings; a stationary counter's Status helps locate a remaining gate. A missing runtime component requires a fresh Play session so the installation hook runs.

The user-set palette volume (1 at this fix's baseline) is preserved, as are their lightning/Titan audio and map edits. Original implementation defaults/receipts above are historical. `ArtSource/Player/Scout/Footsteps/GroundingFix/` records this source revision separately from the initial evidence.

## Evidence and remaining checks

`ArtSource/Player/Scout/Footsteps/` retains static imported-audio/sole analysis and the focused existing regression run. **28 existing checks passed**, covering mixer behaviour, player-only respawn, hold/health/impact integration and two protected Run pose checks. No new tests/assertions were added or changed. This is not a full-suite result and does not resolve the previously recorded broad-suite failures.

The static asset inspection used a closed marker-owned Unity 2021.3.25f1 worker, without Play, native physics stepping, source scene saves, user input or preferences. Existing PlayMode checks ran only in that disposable worker. No live user Editor session was manipulated.

A fresh Play review is still needed on both maps for walking/running synchronization and volume, acceleration/stops, jump/landing, grappling/holds, death/respawn and J pause/focus recovery. Device DSP/output latency and subjective mix balance have not been auditioned or certified by the asset inspection/regression run.
