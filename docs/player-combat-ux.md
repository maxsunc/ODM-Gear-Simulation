# Player combat UX follow-up

User approved dedicated slash volumes, short same-Titan impact-speed forgiveness,
longer simulated-fabric cape (not bone flutter), confirmed kill text and extra
sever/kill sounds from `Assets/Audio/blood1.wav` / `blood2.wav`. Also fix moving
Titan hooks and attacks during holds. Commit each validated stage locally; no push.
Unity **2021.3.25f1**, preserve approved Scout clips/Run, gameplay tuning, shared
Titan structure/physics, live Editor and later user scene edits.

## Stage 1 — moving anchors and held attack exclusion

- Each ODM hook stores the actual hit collider and collider-local point. Its world
  endpoint updates for translation, rotation, scale and animated limb motion; rope,
  thrust target and visual arm IK use that point. No player parenting or retargeting.
- Non-Titan moving scenery works too. Disabled/destroyed surfaces, a detached
  hooked branch or dead/disabled Titan release that hook only. Separate hooks remain
  independent. Gas-off coasting and controls/tuning unchanged.
- Successful real-motor capture synchronously cancels charging/anticipation/active
  slash. Shared input/damage gate rejects held, ReleasePending, suspended, grabbed
  or dead players, including forced native contacts with nonzero base damage.
- Cancelled coroutines cannot reopen the damage window. Resuming requires a fresh
  F key-down. Held visual state now overrides stale charging/attacking flags; old
  held attack art remains in the library but is not gameplay counter permission.
- Owned full suite: **226/228 pass**,2026-11-05 16:22:58–16:23:16UTC,18.712s.
  All four new integration cases pass. Only the two established protected Run
  cadence/stance failures remain unchanged; no tolerances or clips modified.

## Remaining authorized stages

Dedicated bounded slash + target-specific collision speed grace, confirmed kill
text and audio layer; then longer cape-only fabric simulation, native visual/stress
review. No full-speed/WebGL/performance claims until independently exercised.

The user's saved `GiantForest2.unity` was already modified after the forest commit.
It is neither rewritten nor staged by these runtime fixes. Baseline hash lives in
ignored `Library/CombatUXBaseline.json`; use saved-source guards for any art publish.
