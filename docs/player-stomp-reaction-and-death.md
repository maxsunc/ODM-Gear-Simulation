# Player death blood/audio and recoverable stomp ragdoll

Unity **2021.3.25f1 / URP12.1.11**. Follow-up to persistent health and stomp debris.
No saved scene/player/Titan prefab regeneration, global Physics change, navigation
bake, animation-asset rebuild or live Editor automation.

## Behavior

**Every real PlayerHealth death**, not just eating:
- Independent Scout physical ragdoll, blood burst/brief spray, supplied
  `Assets/Audio/bloodSplat.mp3` AND `Assets/Audio/boneCrush.wav` once per death.
-2D personal SFX, routed through existing SFX mixer. New
  `Assets/Resources/PlayerImpactPalette.asset` exposes the clips, blood material,
  blood volume.85 and bone volume.8; original files/imports and Titan/sword palettes
  untouched. Actual audible mix/device timing is a playtest, not a dispatch test.
- Generic HP damage, confirmed bite, squeeze, lethal stomp and the void boundary
  all reach the SAME guarded `PlayerHealth.Die`. The old immediate void reload now
  calls Die and uses the existing3s respawn delay. Latest
  [player-only respawn](player-only-respawn.md) replaces the scene reload with a
  safe new life on the SAME player; world/Titans/music persist. Late legacy hand release cannot
  re-enable dead controls. Existing legacy RagdollController remains the fallback
  for non-Scout players; no duplicate live rig physics is injected.
- The already-approved real bite feedback remains, including its directional mouth
  blood. Universal death blood is additional body spray; the old bite-only sound
  is NOT also played. `EatenSoundCount` remains a compatibility/confirmed lethal
  bite counter, not permission for a second bloodSplat source.

**Surviving actual stomp hits**:
- Default9m/s planar impulse AWAY from the locked stomp point plus3m/s up,
  at most6m/s measured incoming motion. Human response is world-sized, NOT multiplied
  by Titan size. Uses native rigidbody forces and coherent initial tipping momentum,
  not animated falling or a controller teleport/slide through scenery.
- Temporary independent13-body/12-joint Scout ragdoll; motor, hooks, boost, jumps,
  sword input/damage and root rotators suspended through the existing motor owner.
  The disabled, noncolliding controller/camera root follows the physical hips;
  physics alone moves the visible body. No extra camera-transform writer.
- **At least2 scaled seconds** down, then wait for low body speeds, actual ground
  support, world-up CCT headroom AND all enabled solid gear shapes to fit. Stand
  exactly at the supported physical body's position; no lateral search through a
  wall. Low ceilings/gear obstruction keep `GetUpPending` with controls suspended.
- Return to existing locomotion visuals and cached enable states once safe. This is
  a physical knockdown/recovery, not a newly authored get-up animation clip. Reset
  visual/motor/sword speed history so recovery cannot bank teleport-speed damage,
  replay a stale hook-release flip or resurrect hooks/gas lines.
- Further stomps still damage the ONE real HP pool through actual ragdoll shape
  contacts (`TitanCombatProxy`); no knockdown invulnerability or duplicate HP.
  Re-hits reuse the SAME body and bounded input impulse, restarting minimum time.
- Death while down **promotes the SAME moving physical body** to a permanent corpse,
  emits universal death feedback and never gets up/restores live controls.
- Regeneration remains REMOVED;60HP stomp damage/1.6s cooldown and existing damage
  height/cover/reach/dedup remain. No changes to the3-second QTE or10HP/s hold drain.

Tuning on runtime/player `ScoutStompReaction`: `minimumDownTime=2`,
`knockbackSpeed=9`, `upwardSpeed=3`, `maximumGetUpSpeed=3`. Unsafe/nonfinite tuning
has bounded defaults. New birth input is bounded, never a post-simulation speed cap.

## Ownership and physical safety

- `TitanStompAttack` calls the explicit real `TakeStompDamage` ONLY after existing
  scene-local low-cylinder/cover/dedup checks. Generic damage/bites cannot accidentally
  trigger a temporary knockdown. A dodged/cancelled stomp creates no player reaction.
- `TitanHealthSource` optional before/after hooks keep laboratory targets' original
  health contract. `TitanODMHealthSource` bridges to the real health/reaction only.
- `ScoutPlayerRagdoll` reuses the established current-pose closed-shape native rig,
  solver12/4, angular limit4, joints, masses, native CCD, self-contact exclusions,
  independent frozen cape and strict scenery birth rejection. No skeleton parenting.
- The new coherent tipping response is a bounded BIRTH velocity/angular impulse;
  later motion is native. Original bite tumble/outward response and pre-release
  pose/grip ownership are preserved. A direct Die while held also captures the
  current holder/velocity BEFORE release can move the live root.
- Temporary birth may initially ignore only the stomping Titan's collider pairs;
  those contacts restore as soon as every physical shape is clear. Other scenery
  always collides. The whole configured controller/gear is checked before get-up.
- Component disable/destroy delegates pending safe recovery to the existing motor
  poll. Scene unload cleans the independent body; a dead player is never resumed.
- `PlayerDeathFeedback` owns only bounded render/audio tails (max160 blood particles,
 100-particle burst plus brief spray, seeded independently of Unity gameplay RNG).
  Sources/particles are scene-owned, not children of detachable bones/player physics.

## Verification

Evidence **`ArtSource/Player/Scout/StompDeath/validation-summary.json`**, adjacent
XML and actual non-batch GameView images. All automation used marker-owned disposable
Unity2021 workers, never live Play input/stop/save/reload/scene switch/physics.

- Current-user-tuning BEFORE policy run: old HEAD code/tests with CURRENT assets,
  **357pass/8fail/1skip of366**. Final full **377pass/SAME8fail/1skip of386**, twice;
  **20 added cases**. Existing Run/face/stun/palette/reference/turn fixtures remain.
- One concurrent-worker run had an extra existing
  `AuthoredLabInterceptsItsMovingAirborneProxyThenResets` miss; retained as
  `full-parallel-outlier-results.xml`. It passes on both subsequent complete solo
  runs. No interception timing/reach or assertion changed to hide it.
- Universal death generic/bite/squeeze/void/direct held Die, exact clips/SFX groups,
  once-only feedback/RNG, late legacy release, shared corpse promotion, and actual
  first-lethal stomps at1/3/5/10x.
- Native recovery, minimum time/pause, real knockback, repeated hits/proxy health,
  world-up ceiling, solid gear obstruction, native wall collision, disable/destroy
  recovery and original enable flags. Refused unsafe live births cannot arm a stale
  snapshot for a later unrelated death. No physical threshold relaxed.
- New reaction and first-lethal-stomp bodies retain **speed<40m/s**, tested1.5s for
  lethal stomps and up to8s recovery. Original arm<8deg/120Hz, same-grip<.003m and
  later carry/mouth penetration<.005m suites remain. Early takeover overlap is NOT
  certified clear. No comprehensive long-duration10x death certification.
- Native graphics **1/1**: real hit/knockdown, recovered40HP, blood emission pixels,
  then independent fallen corpse. Supplied clips really assigned/played; this is
  not a subjective speaker/DSP or WebGL build certification.
- Current saved-map native suite **4pass/1SAME unbaked4x warning failure of5**.
  Actual AI ground attack/real HP/debris/SFX and all3/5/6/7/10x routes/births remain.
  No4x bake or spawn-choice changes.
- User changed Titan tuning during work: current references16/45, turning75/35
  were preserved (not reverted to the earlier8/16,50/5). Current palette uses four
  B-series footstep WAVs/volume.25 and one scream/.3; preserved. Extra supplied B
  files remain user/unrelated assets, NOT staged by this task. New authorized
  boneCrush WAV/import stays byte-identical. Protected scene/navigation/settings/
  Run/palettes unchanged; current source and resource hashes matched both workers.

Reproduce with a fresh full worker then `python tools/capture_player_impact.py`.
No crowd FPS, subjective force/audio balance or device/build certification. All work
remains LOCAL; no push authorization.
