# Regenerating player health and sized gameplay stomps

Unity **2021.3.25f1 / URP12.1.11**, unchanged project versions, assets and physics
settings. User requested ground stomp repair/stronger threat, a regenerating top-left
health bar instead of the runtime console, and removal of attack-circle indicators.

## Player-facing changes

- Existing **PlayerHealth** remains the sole HP/death/respawn authority. Default100HP
  is now visible in a compact top-left bar, with numeric current/max and a brief
  damage-color pulse. No new sandbox health pool or damage redirect.
- **10HP/second after5 seconds without damage** by default. Further valid damage
  restarts the delay. A long tick heals ONLY time beyond the delay; clamps at max.
- No healing while dead, paused, held or awaiting blocked release. Held/pending
  time resets the wait, so release is not an instant full-heal loophole. Invalid,
  nonpositive and nonfinite damage cannot heal/poison the real HP value.
- Gameplay stomps now deal **60HP** (two uninterrupted hits kill a100HP player),
  cooldown **1.6s**, with **75% ground-stomp preference** when both attacks are valid.
  Ground grab remains the fallback; the existing zero-warning aerial interception
  and three-key escape QTE are unchanged.
- Full **.65s body wind-up / .14s strike / .5s recovery** retained: no invisible
  instant hit. Landing point LOCKS at selection; it never homes after a dodge.
- All gameplay stomp, grab and mouth/bite circles disabled. Procedural body motion
  still communicates the attack. Runtime FreeRoam/Shiganshina console OnGUI panels
  removed entirely, including old saved showDiagnostics:true. Lab tools remain.
- One actual stomp-ground impact plays a supplied footstep clip via the same SFX
  routing/palette volume. Not on wind-up, cancellation, or a delayed second callback.
  No new audio file, import, pitch, palette-volume or normal footstep timing edit.

Tuning: `PlayerHealth.regenerationDelay`, `regenerationPerSecond`, `showHealthBar`;
manager **Ground combat**: `stompDamage`, `stompCooldown`, `groundStompChance`.
Native legacy Shiganshina setup uses the same defaults. Passive prefab/standalone
stomp lab still defaults to historical40HP/2.2s; only integrated gameplay is stronger.

## Why ground stomps stopped working at larger sizes

Stomp selection and posing had stayed in the12m/1x rig's WORLD units:6.4m selection,
4.1m foot reach,2.4m radius,8m-up/16m-down support probes and unscaled pose loads. A larger rig's feet and
body dimensions grew but these measurements did not. Navigation also still stopped
at6m instead of a proportionate distance, often bringing a giant too close for a
coherent planted attack.

Now scale the geometry with the actual uniform rig size:

| Measurement |1x|3x|5x|6x|7x|10x|
|---|---:|---:|---:|---:|---:|---:|
| Chase stop |6|18|30|36|42|60|
| Selection range |6.4|19.2|32|38.4|44.8|64|
| Locked foot reach |4.1|12.3|20.5|24.6|28.7|41|
| Ground impact radius |2.4|7.2|12|14.4|16.8|24|

Pelvis load, raised foot, backward foot load, arm brace and support-ground probe
lengths scale too, keeping the procedural pose proportional. Timings, rig root,
physics bodies/joints/limits/masses/solver, collision exclusions, grounded human
height and death ownership do NOT scale/change.

The selection height check now uses the actual human controller's WORLD-UP capsule
BOTTOM against found ground, rather than mistaking its centered root for feet.
The low damage column remains **1.8m WORLD height**, NOT multiplied by Titan size:
jumping over a stomp still works; a giant does not hit a flyer18m up. Existing
scene-local cover/sight, exact-once/compound dedup, overflow fail-closed behavior,
injury/blind/death cancellation and movement leases remain intact. No smaller agent
profiles, runtime navigation bake, wall-through damage, homing, player parenting,
knockback warp or post-physics velocity clipping introduced.

## Verification / scope

Owned workers only; never saved/reloaded/switched/stopped the live user's scene or
Play/input/physics. Saved GiantForest2 choices3/5/6/4, initial5/max15, both palettes,
passive Titan prefab, navigation assets/settings, art and animation files preserved.
No need to rebuild/save any scene to acquire the UI/runtime behavior on fresh Play.

24 added full-suite cases:
- Both feet3/5/6/7/10x: reachable locked point, actual planted foot agreement, support
  floor clearance with the SAME world bounds, unmoving root,60HP once, no high-flyer
  damage and no generated ring objects.
- Loaded3/5/10x death handoff for1.5s: original **speed<40m/s** retained (observed
  about13.5–14.1m/s). No artificial cap or relaxed bound; original1x longer matrices
  and all existing arm/mouth/player-corpse constraints remain.
- Real centered CCT/PlayerHealth bridge1/3/5/6/7/10x:60HP hit, second hit kills and
  produces ordinary independent corpse, NEVER an eaten sound/cue.
- Regeneration exact delay/crossing/re-hit/cap, pause/hold/pending/death, invalid
  damage and no postmortem regeneration.
- Stomp impact audio once/not on warning or cancellation.
- Actual native health HUD rendering (explicitly gated in headless suite).

Full suite **334pass / same7preexisting failures /1graphics skip of342**, vs
pre-change310/7/1 of318. Intentional balance fixture change: the existing runtime
spawn-director test explicitly now asserts damage60/cooldown1.6/remainingHP40 instead
of historical remainingHP60. This is the approved stronger damage, NOT a relaxed
physical assertion; old40HP standalone lab assertions remain unchanged.

Saved current-map native suite: **4pass/1existing configuration failure of5**,
vs prior3/1 of4. New end-to-end gameplay case makes the actual AI select a ground
stomp at EACH3/5/6/7/10x, takes real HP100→40, hears exactly one impact, confirms
no LineRenderers, verifies no heal before5s and then healing. Old no-SizeWarning
fixture still correctly flags user's NEW unbaked4x; fallback safely skips4x. No4x
bake/config edit was requested or performed, and the assertion is not weakened.

Native HUD **1/1** using a marker-owned non-batch Unity2021 GameView: full,60HP
stomp-equivalent damage, then REAL8 seconds of Update-based regeneration. Screenshots
in `ArtSource/Titan/StompHealth/`; no synthetic keyboard input or painted fake HUD.
Owned review: `python tools/capture_player_health.py` after a fresh full run.

Automated geometry/HP/rendering checks are not subjective difficulty certification,
50-enemy crowd FPS, WebGL/device latency, or complete long-duration10x corpse playtest.
Everything remains local; no new push authorization.
