# Player health, sized stomps and continuous grab damage

Unity **2021.3.25f1 / URP12.1.11**. Current follow-up supersedes the earlier
regeneration request: **health regeneration is removed**, stomps throw up debris,
and confirmed gameplay holds drain **10 HP/second**.

**Latest follow-up:** [Player stomp reaction and universal death](player-stomp-reaction-and-death.md)
adds temporary physical knockback ragdoll/safe recovery on surviving stomps, and
blood + bloodSplat/boneCrush sounds on EVERY death. Original HP/balance remains.

## Player-facing behavior

- Existing `PlayerHealth` remains the ONE100HP/death/3s-respawn authority. Compact
  top-left health bar/numeric current/max and damage pulse remain. Damage persists
  between attacks and escapes until ordinary death/player-only respawn; no passive heal.
  Latest [player-only respawn](player-only-respawn.md) keeps the running map/Titans/music
  and restores the SAME player at a clear start after3s, NOT a scene reload.
  Regeneration fields, Update ticker and `TickRegeneration` are removed, not merely
  hidden or set to zero. Invalid/nonpositive/nonfinite damage still cannot heal HP.
- Gameplay stomps deal **60HP**, **1.6s cooldown**, **75% ground-stomp preference**
  when both ground attacks are valid. Standalone labs retain40HP/2.2s defaults.
- Actual locked stomp impact triggers ONE supplied footstep sound and a new
  **dust puff / tumbling faceted rock-fragment burst**. No burst at wind-up,
  cancellation or a missed attack that never reaches impact. A completed ground
  strike can throw debris even if the player successfully dodged its damage area.
- Full **.65s body wind-up / .14s strike / .5s recovery** remain. Landing point stays
  locked; no homing foot or Titan-root relocation. Gameplay stomp/grab/bite circles and
  old FreeRoam/Shiganshina console remain removed. Body motion communicates danger.
- Only a **confirmed physical Titan capture** starts grab damage. No damage during
  warning/reach, misses, proximity, or the catch frame's pre-capture interval.
  Applies throughout QTE/holding/mouth transfer until release or death.
- Successful QTE cancels both bite and ongoing hold damage immediately, including
  while collision-safe exit is still `ReleasePending`. No damage debt on exit.
  Ordinary release, interruption, blindness, lost arm/owner and death also stop it.
- Damage is scaled-time; J pause does not drain health. No duplicate charging from
  the attack's logical clock plus the target's native LateUpdate. Repeated escapes
  keep the health already lost. With low HP a squeeze can kill BEFORE the QTE
  deadline; the3s window is not invulnerability. First full-health capture still
  leaves70HP after a full3-second attempt, before any later transfer/bite damage.

Inspector manager **Ground combat** tuning: `stompDamage`, `stompCooldown`,
`groundStompChance`, **`grabDamagePerSecond=10`** (zero disables hold drain).
Native legacy Shiganshina integration applies the same defaults. Standalone lab
`TitanGrabAttack.holdDamagePerSecond` defaults0; existing lab escape/HP contracts
are preserved. `PlayerHealth.showHealthBar` still controls the HUD.

## Cosmetic debris ownership

`Assets/Titan/Integration/TitanStompDebris.cs` observes `TitanStompAttack.Impacted`.
Runtime actor/Shiganshina installation only; no passive prefab or scene rewrite.

- A scene-owned pool of **four reusable slots/eight ParticleSystems**, lazily
  created on the first impact. Reuses/clears the oldest slot when busy. Up to72
  dust puffs/30 chips per burst; total configured capacity448 particles/scene.
- Scene-local world-space rendering, scaled spread and square-root-size energy.
  No parenting into the Titan; existing particles finish after attacker death or
  destruction.2s maximum particle lifetime; mesh/root cleaned on scene unload.
- Dust billboards and a tiny procedural flat-faced mesh, two new Resources
  materials and `ODM/Titan/StompDebris` shader. Original art/audio/imports and
  existing sword-impact shader/material are untouched. Private RNG, no consumption
  of gameplay Unity RNG. Pauses normally; no asynchronous attack or damage timer.
- No colliders, rigidbodies, native Cloth, lights, LineRenderers, scenery physics,
  navigation, root movement, camera shake/authority or global Physics changes.
  These are render-only fragments, not dangerous projectiles.

## Health/death ownership

`TitanCombatTarget.AdvanceHoldDamage` reconciles the existing attack/capture age
with hold age using one cumulative charged duration, bounded by the existing
lease. The attack never charges past its earlier bite/release point on a long
logical tick. Its existing real `TitanHealthSource` applies the loss; no second HP
pool. It cannot keep damaging a released or foreign-owner target.

Lethal non-bite damage during a hold snapshots the actual Scout pose/velocity and
catching Titan BEFORE release callbacks can move the live root. The independent
corpse keeps existing bounded initial velocity, joints and local catching-Titan
collision exclusions. No chomp impulse or fake bite contact for squeeze death;
latest universal death blood/bloodSplat/boneCrush feedback DOES play for all causes. Scenery-blocked births still fail closed; no safe-looking warp or post-step
clipping. Actual later lethal bites retain their existing blood/eaten feedback.

## Sized stomp geometry retained

| Measurement |1x|3x|5x|6x|7x|10x|
|---|---:|---:|---:|---:|---:|---:|
| Chase stop |6|18|30|36|42|60|
| Selection range |6.4|19.2|32|38.4|44.8|64|
| Locked foot reach |4.1|12.3|20.5|24.6|28.7|41|
| Ground impact radius |2.4|7.2|12|14.4|16.8|24|

Support probes and pelvis/foot/arm load offsets scale; human centered WORLD-UP CCT
bottom determines selection height. Damage column stays **1.8 WORLD meters**, NOT
scaled: jumping/high flyers remain safe. Cover/query-overflow/compound-dedup,
injury cancellation, same physical grip, leases and all original physical bounds
remain. No damage through walls, global physics changes, runtime navigation bake,
clearance borrowing, motor change or new player/Titan root writer.

## Verification and scope

Evidence: **`ArtSource/Titan/StompDebris/validation-summary.json`** and adjacent XML
and actual GameView PNGs. Disposable marker-owned Unity2021 workers only; no user
Play stop/input/scene switch/save/reload or live physics automation.

- Fresh BEFORE baseline with current user prefab tuning: **333pass/8fail/1skip of
  342**. New full suite **357pass/SAME8fail/1skip of366**, repeated;24 added cases.
  Earlier7-failure baseline predates user's turn-speed changes50/5, which add the
  existing head-turn fixture failure (expected>15, actual~1.31). Preserved that
  tuning rather than fixing tests by reverting it. Other failures remain protected
  Run, face enlargement,12s stun,8/16 motion references and one-scream palette.
- Grab: native contact/QTE,10HP/s shared-clock/pause/no double charge, pending exit
  stops drain, repeated escapes lose health, cancelled callbacks, ordinary squeeze
  death/independent corpse, and later real lethal bite still gives its correct cue.
  Both hands3/5/7/10x retain **arm<8deg/120Hz, same grip<.003m, later carry/mouth
  penetration<.005m** unchanged. Historical early takeover overlap is still separate
  and NOT certified clear. New early squeeze corpse1.5s:1x ground **8.11509m/s**;
  both hands3/5/10x aerial maximum **14.715m/s**, all below original **40m/s**.
- Debris: real exact-once impact/not warning/cancel, private RNG, bounded reuse,
  world-space/no physics/no ring objects, independent tail/cleanup. Native graphics
  **1/1**, actual dust/chips visibly change the SAME-pose FX-disabled image;
  final changed-pixel count recorded in XML. No magenta/cyan placeholders.
- Health HUD native **1/1**:100→40 then **REAL8 seconds Update/frame time with HP
  still40**, not the earlier regen screenshot. Actual Intel Iris Xe/DX11 GameView.
- Current saved-map suite **4pass/1SAME configuration failure of5**: actual AI
  stomp/real health/no circles/one impact sound, new debris burst and10HP/s runtime
  assignment across all five3/5/6/7/10x. Existing unbaked4x warning fixture remains
  red. No4x bake, spawn choice changes or weaker warning assertion.
-18 protected hashes unchanged, including saved scene, user prefab, palettes,
  original art/animation/audio/nav/settings. Both workers byte-match tested sources.

Earlier regen scenarios intentionally became persistent-health assertions and the
map/HUD expectations were updated to the requested no-heal behavior. No physical
safety thresholds were relaxed. Historical `ArtSource/Titan/StompHealth/` evidence
is retained as history, not current regeneration behavior.

Reproduce:
```
python tools/run_titan_validation.py --reuse <owned-validation-worker> --wait
python tools/capture_stomp_debris.py
python tools/capture_player_health.py
```
Subjective impact/threat, crowded-map FPS, device/WebGL behavior and long-duration
10x corpse safety remain playtests, not automated certifications. Local only;
**do not push without approval**.
