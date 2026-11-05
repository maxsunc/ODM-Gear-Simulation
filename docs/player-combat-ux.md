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

## Stage 3 — longer, genuinely simulated fabric cape

- `ScoutCapeCloth` auto-installs once on the existing Scout driver. No scene/prefab
  reinstall needed next Play. Original FBX, packed Blender source, bones, material,
  body/gear/swords and ALL animation clips (including Run) are unchanged.
- New cape-only runtime mesh is **.98m shoulder-to-hem at source scale1**, versus
  original.62m: **58% longer**, about1.96m at scene visual scale2, near the knees.
 13x21 /273 particles,586 render vertices/1,000 triangles, two-sided cloth using
  the approved atlas. Original ten wing shapes interpolate on the moving fabric.
- Actual free-particle fabric: pinned shoulder row, fixed120Hz substeps (max6/frame),
  stretch/shear/bend constraints, non-compressive multiresolution strain limits,
  gravity, spatial gust forces, relative airflow and bounded inertial loading.
  Not two-bone flutter and not a sine-deformed rigid cape. CPU managed solver avoids
  a dependency on platform-native Unity Cloth support; WebGL performance still untested.
- Eight default solver iterations. Cached capsule transforms/matrices avoid per-node
  native Transform/Quaternion calls. Aerodynamic load bounded24m/s² (porous material),
  narrow collar strain constraint independent of character ragdoll velocity safety.
- Seven render-only body/gear proxies (broad/shallow torso cross-section, thighs,
  neck, canisters/back unit) keep fabric away from those forms. No native collider,
  Rigidbody, global Physics/wind/time changes or second Animator/root writer.
  **No cloth self-collision or scenery collision claim.** Fast visual transport uses
  a transported simulation frame; abrupt teleports, >120° frame discontinuities,
  invalid dt or >.2s hitch reset locally instead of throwing the mesh across the map.
- Death snapshots clone the current cape mesh into the independent corpse, bind its
  render transform to copied chest and reclaim its mesh with the corpse. Destroying
  live cloth cannot invalidate the corpse's mesh. Corpse fabric is a frozen folded
  snapshot following the physical chest, not a second ongoing cloth simulation.
- For persistent Inspector tuning, add ScoutCapeCloth to the PLAYER root in Edit
  Mode: capeLength (source metres), ambientWind, gustStrength, airDrag,
  solverIterations, bodyCollision. Auto-installer respects an existing component,
  including disabled; disabling restores the original passive cape. No new keys.
- Also corrected tangential foot-contact handling: ordinary grounded foot moves
  don't clear trusted horizontal walking samples, while blocking walls/landings
  still invalidate collision-speed grace.

### Final evidence / local publication

- **242 passed,2 established Run failures,1 native-graphics-only skip**, TWICE:
  2026-11-05 **17:15:11–17:15:40UTC**,28.611s;
  **17:17:58–17:18:26UTC**,28.505s. Unity2021.3.25f1 owned worker, no live Editor input.
  Twenty new non-graphics cases across this follow-up pass. Strict arm<8°/120Hz,
  mouth/carry distal penetration<.005m, corpse speed<40m/s assertions retained.
- Cape cases include pinned anchors, wind deformation with no cape-bone writes,
  bounded structural stretch<1.2,80m/s transported flight/turns/hitches, torso
  exclusion, no native physics additions, independent snapshot cleanup and an
  actual confirmed bite with a long-cloth corpse snapshot. Established character
  physics bounds unchanged; the cloth structural-stretch bound1.2 was retained.
- **1/1 native cape graphics pass**,17:15:51–17:15:54UTC/3.529s,
  **Intel Iris Xe/DX11**. Original Scout/approved Idle/GasBoth pose, original URP
  material/atlas, warm completed camera frames. Flight structural stretch1.06917.
  Native old bite/blood graphics regression also rerun1/1,17:18:37UTC/.894s.
- [Native cape sheet](../ArtSource/Player/Scout/CombatUX/Scout_LongCape_Unity.png),
  [wind GIF](../ArtSource/Player/Scout/CombatUX/Scout_LongCape_Wind_Unity.gif), raw selected
  native frames and fresh XML summaries in the same folder. These are owned render
  fixtures, not full-speed gameplay footage; camera readbacks omit screen-space HUD.
- Full ordinary forest traversal/combat feel, WebGL build/FPS/memory, crowded cloth/
  corpse/carry scenery clearance and audio loudness remain user playtest items.
- Local checkpoints: **56261300** moving hooks/hold cancellation;
  **314eb2ae** slash/impact grace/kill/audio; final cloth checkpoint see `git log`.
  **No push requested or performed.** User scene/cache changes excluded.

Reproduce: `python tools/run_titan_validation.py --wait` (or owned `--reuse PATH`),
then `python tools/capture_scout_combat_ux.py`. Optional `--filter NAME` is targeted
DIAGNOSTICS, not a full-suite result; the run-state JSON labels the filter explicitly.

The user's saved `GiantForest2.unity` was already modified after the forest commit.
It is neither rewritten nor staged by these runtime fixes. Baseline hash lives in
ignored `Library/CombatUXBaseline.json`; use saved-source guards for any art publish.
