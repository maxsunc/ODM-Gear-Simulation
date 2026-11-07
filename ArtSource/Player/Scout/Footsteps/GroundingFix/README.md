# Footstep silence: grounding compatibility patch

The initial emitter required the native CharacterController `isGrounded` flag AND Scout grounding. This was unsafe for this project's multi-Move motor: ODM adds three moves after walking/gravity, and the flag only describes the last move. Clearing it could silence/reset the entire footstep history on every rendered frame even while the visual gait was running on the floor.

The updated emitter retains Scout grounding, rejects upward jump/release velocity, and accepts either the native support flag or a short read-only WORLD-UP sphere sweep at the capsule sole. It ignores self/triggers, uses the existing ground mask and walkable normals, and fails closed on saturated query buffers. No motor/controller/pose/Physics settings are changed. Unloaded clip data is explicitly requested on startup. Runtime Inspector Status and Footsteps Played fields were added for diagnosis.

The user's volume of **1**, Titan audio palette, both map scenes, and all other pre-existing Assets/ProjectSettings files were preserved. Baseline: **1,599 paths; 1,598 unchanged**, with only `Assets/Player/Scout/PlayerFootstepAudio.cs` changed by this patch. See `preservation-and-validation.json` for hashes.

Unity 2021.3.25f1 / URP12.1.11 compilation and the same **28 existing regression checks passed**, 2026-11-07 19:37:15–20Z, duration 5.1329381s. No tests/assertions were added or edited. XML and raw log are included. This is not a full-suite result, does not resolve earlier broad-suite failures, and does not directly assert the new support fallback or audio playback.

No live user Editor, Play state, input, scene or physics was manipulated. The cause was identified by source inspection; this receipt is not a live reproduction or auditory confirmation of the user's reported silence. A fresh Play session is required to install/reinitialize the emitter and confirm audible walking/running. If still silent, inspect Player → Player Footstep Audio → Runtime Diagnostics (Status / Footsteps Played).

Local revision, not committed/pushed at capture. Git history remains authoritative.
