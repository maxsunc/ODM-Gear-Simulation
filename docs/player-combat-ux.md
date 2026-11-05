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

## Stage 2 — dedicated slash, impact grace, kill text and supplied audio

- Installed Scout + speed-damage scenes automatically use a dedicated slash volume;
  legacy blade labs remain unchanged. Root-relative box centre(0,.15,1.35), half
  extents(1.8,1.2,1.45) WORLD metres. Attack aim locks on F release; the box follows
  real player translation through the .12s active window, never the sword pose.
- Native overlap + box cast catches thin crossed zones, once per region/swing.
  Rear-only nape / front-only eyes; physical line-of-sight rejects walls and body
  obstruction. Size does not scale with Titan size. Reach<=4.5m, teleport rejection,
  query64 / occlusion32 saturation fails closed. Inspector-selected debug volume.
- Real native CCT contact records that Titan's last valid continuous root speed for
  .18s. Only a committed attack close to the contact can use it; preserved damage
  curve and native body collision. Four bounded receipts; no global speed bank.
  Lower-speed repeated pushing cannot refresh the high-speed timestamp. Walls,
  teleport, cancel, hold, death and invalid sampling clear history. Other Titans
  cannot borrow it. No artificial impulses, post-simulation clipping or passthrough.
- Confirmed player kill shows outlined gold `+Titan Kill` upper-centre for1.5s.
  Bounded64 recent IDs prevent duplicate notifications; no score-system change.
- Original blood1(3.049s)/blood2(2.022s), mono48k WAV bytes/import settings preserved.
  A tiny Resources palette references them without editing existing saved scenes.
  Alternating clips layer over existing sounds ONLY on actual arm sever / kill;
  same-frame outcomes coalesce, not on face blindness, ordinary damage or misses.
- Owned full suite **235/237 pass**,2026-11-05 16:35:51–16:36:11UTC,20.282s. Nine
  new cases include rear nape hits1/3/5/6x, occlusion/front rejection, native thin-zone
  crossing, teleport/saturation, real CCT slowdown/target-specific expiry, cancellation/
  wall/hold rejection, audio references/layering and kill-toast lifetime. Only the
  established two protected Run failures remain. All strict physics tests retained.

## Remaining authorized stage

Longer cape-only fabric simulation and native visual/stress review. No full-speed/
WebGL/performance claims until independently exercised.

The user's saved `GiantForest2.unity` was already modified after the forest commit.
It is neither rewritten nor staged by these runtime fixes. Baseline hash lives in
ignored `Library/CombatUXBaseline.json`; use saved-source guards for any art publish.
