# Titan sword damage, hit feedback and sever launch

Unity **2021.3.25f1**. Scene opt-in implementation; no pipeline upgrade, animation
rebuild, motor tuning, global time/Physics change, shared Titan/player prefab edits,
commit or push during the effects task. Subsequently, at user request, attacks,
effects and the user's saved scene/hitbox changes were committed and PUSHED as
**b6447972**. Later runtime registration work is separate, uncommitted; see
`docs/titan-runtime-initialization.md`.

## Installed balance

`Tools > Titans > FreeRoam > Install sword health and impact effects` installs on the
current FreeRoam player and both existing Titans, including inactive scene objects.
It never changes Titan activation, placement, scale, navigation or combat-enable state.
Repeat for newly added Titans, or enable `useRegionalHealth` on their body component.
Shared prefabs and historical physics/combat labs retain the legacy profile (60 limb
HP, instant positive nape hit/eye reaction), so their original fixtures stay meaningful.
Production FreeRoam opts into the new regional profile before Play.

| Region | HP, independently per side |
| --- | ---: |
| Left arm / right arm | 1,000 each |
| Left leg / right leg | 2,500 each |
| Face (both existing eye volumes share one region) | 500 |
| Nape | 500 |

Nape hits accumulate; only reaching zero kills. Face reaching zero starts the existing
5-second blindness. Already-blind eyes give no more damage/numbers and do not extend
blindness; recovery restores face HP to500. Direct scripted `StunEyes` still exists.
Severed regions and dead Titans reject further sword hit receipts.

`ModularTitanBody.GetHealth/GetMaximumHealth` expose current/max HP. `ReceiveDamage`
returns a `TitanDamageResult`; original `ApplyDamage` remains a compatible void wrapper.
Receipt distinguishes requested damage/overkill from actual HP removed and carries
region, real contact point, slash direction, remaining HP and gameplay outcome.

### Speed damage

Scene DamageDealer settings:

```
baseDamage = 250
speedBonusStartsAt = 15 m/s
damagePerSpeed = 35
maximumDamageSpeed = 80 m/s
Damage = 250 + 35 * max(0, min(actualSpeed,80) - 15)
```

Examples: stationary/walking<=15 ->250;30 ->775;45 ->1300;80+ ->2525. A slow nape
needs two hits; fast nape can die in one; full-speed pass can sever a fresh leg.
Displayed number is the attack's damage INCLUDING overkill, rounded up, not HP clamped.
All values are Inspector tunable on the player's damage-window `DamageDealer`.

`TitanSwordFeedback` samples actual root displacement after the real FixedUpdate motors,
not sword/animation speed, camera rotation, or CurrentSpeed+ResidualSpeed added together.
It needs two continuous samples, rejects invalid dt/non-finite/teleport-speed>160m/s
motion, and suppresses carried/grabbed/release-pending/dead movement. After release,
fresh samples are needed. This does NOT change motor velocities or controls.

## Physical hit authority

Existing .075 anticipation / .12 active / .195 recovery =.39s unchanged, F hold/release
unchanged. Both actual cutting meshes still use the conservative .035m blade sweep,
<=.025m steps/max256, saturated query/teleport/scale fail-closed behavior.
Sweeps now supply a real region-surface contact point and swept steel-center direction.
New speed profile ignores old native hilt/gear triggers when the real blade sweep is
active; the legacy contact path remains for old scenes. All paths share one region
lease per swing, so multiple colliders/two blades do not duplicate HP loss or feedback.
Only accepted damage emits `DamageDealer.HitConfirmed`; misses/dead/missing/blocked
regions do not flash damage numbers or spray blood.

## Feedback

`TitanSwordFeedback` on scene Player owns the presentation; inspect it for shake/sound
intensity, blood/trail/number/hit-marker toggles, number lifetime, materials/audio refs.

- **White**: accepted damage without disabling the region.
- **Yellow**: actual limb sever or face blindness threshold.
- **Red**: actual nape killing blow, NOT every nape hit.
- Outlined bold numbers rise/fade over.85s. By default initial contact projection is
  screen-anchored/clamped for readability during high-speed fly-bys; optional world
  anchoring. Max32 active numbers. Small outcome-colored hit-confirm cross.
- Mild high-frequency render-only screen shake: light hit.09s,
  sever/blind.16s, kill.22s. Configurable0..1 intensity. Applied only in the player's
  camera render callbacks, restored afterwards/on disable. No camera/root transform,
  aim-control, motor or time-scale changes; automatic FOV projection remains live.
- Soft pale-gold tip ribbons emit only while actual damage window is enabled, then
  fade in.095s. They are visuals, not enlarged damage geometry.
- Small directional blood impacts on hits; larger sever/kill bursts. Sever additionally
  bursts at the cut root and follows the detached branch with.18s moving spray. World
  simulation, gravity, short fade; no permanent decals, organs, collision or fluids.
- Eight reusable particle systems, max96 each. Original unlit vertex-color shader
  `Assets/Titan/Integration/Shaders/TitanImpact.shader`; scene material reference keeps
  it included in builds. No external assets or runtime material generation/leak.
- Original deterministic provisional whoosh/thump/flesh/sever/kill audio under
  `Assets/Audio/TitanCombat`, built by `tools/build_titan_combat_audio.py`. These are
  first-pass procedural foley, not professionally recorded sounds. Existing assigned
  slash SFX take precedence; no duplicate whoosh. Highest-severity impact sound per
  frame, local audio source, no disturbance to gameplay random selection.

## Severed-limb launch

Regional profile adds explicit **bounded input**, not post-simulation velocity caps:
7m/s along actual slash +.65m/s up, mild spin up to.8rad/s. Spin reduces with branch
radius so orbital contribution stays<=2m/s at giant scale. Every detached body gets
coherent `v + omega cross radius` and matching spin; it is not a root-bone-only shove.
Current cut pose/Animator continuity remain preserved. Gravity, all masses, three-body
branch topology, joint limits, solver12/4, angular cap4, projection/preprocessing and
existing torso-clearance/death/continuity thresholds stay unchanged. Zero impulse
scripted severs do not invent a launch. Legacy profile retains historical impulse.

## Evidence and current scene state

Final owned disposable suite **167/169 passed**,2026-11-04 **03:51:08–27 UTC**,
PID46464,19.099s. Previous complete167/169 at03:47:22–39,PID50212,17.019s. Only two
known untouched retimed Run failures remain; tests were not weakened to hide them.
Final audit byte-matched175 runtime/test/art/effects files to the completed validation
copy; new asset/script metadata present. Twelve new cases cover regional thresholds/nape accumulation, blindness recovery,
capped speed/teleport/held suppression, dedup/outcome colors, pool/trail lifecycle and
real asset refs, render-only camera restoration/automatic FOV, sever physics.

All four branches at1x/3x/5x launch COM~(6.99,.57,0)m/s after first120Hz step; one-second
freefall maximum11.16–11.38m/s, below unchanged40m/s. No cut-pose snap, solver12/4 and
angular cap4 retained. Strict prior Titan arm/death/clearance regressions also pass.
No removed Editor-preview-scene physics tests were restored/run.

Actual Windows F, actual Scout blade sweeps against an owned frozen Titan sky fixture:
arm1000 ->750 ->500 ->250 ->0 across four250 hits; receipts1/2/3/4 (no duplicate blades),
first normal white, final sever yellow, native screenshot has blood/trail/number.
`ArtSource/Titan/Combat/Scout_SwordImpact_ConfirmedHits.png`; original shots/receipts in
`Library/TitanImpactReview/LeftArm_1..4`. This is controlled stationary input/hit/GUI
proof, NOT ordinary full-speed ODM traversal/combat approval. Nape/face health/colors
are automated-test evidence; native review stopped before those fixtures when foreground
input guard refused focus. No claim of completed native nape/face captures.
`tools/review_titan_sword_impacts.py` can reproduce when user is not playing; always
releases F and exits its owned Play session. Do not run over a user's live Play session.

Historical effects-task scene state (user subsequently saved it; included in pushed
b6447972): scene started clean; installer left intended setup dirty/unsaved. Before/after COPY
snapshots `Library/ScoutBackup/FreeRoam.before/after-impact-system.unity` differ ONLY by
new Player feedback component, DamageDealer speed settings and two regional-profile
prefab overrides (health defaults are1000/2500/500/500). No transform/activation edits.
User subsequently began a normal gameplay Play session; left running, no forced stop,
scene reload or save. **After leaving Play, save FreeRoam to persist the installed
scene setup.** Code, material, shader and audio are already on disk. Main-scene save
must not capture a review fixture or overwrite later user edits.

Remaining review: user aesthetic/volume/shake judgment, high-speed normal traversal,
held counters with new HP balance, scaled combat/corpse/player clearance under launch,
environment sword blocking, performance under many simultaneous effects. No slow
motion/hitstop, global postprocessing flashes, permanent blood decals or new player
ragdoll were added.
