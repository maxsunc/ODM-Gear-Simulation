# Shiganshina / real ODM integration

## Request and Git checkpoint

User approved the fast aerial/corrected mouth carry, requested commit + push, then
integration into `Assets/Scenes/FreeRoam (Shiganshina.unity`, removing its old Titans.
Approved prototype slice committed **940daf1d** (`Add fast physical aerial grabs and
clear torso during mouth carry`) and pushed to **origin/titan-combat-updates**.
No merge into main, force push or unrelated model/Plastic/IDE changes staged.
This NEW scene integration is a separate, currently uncommitted review slice.

## What “motor adapter” means

Not a replacement/rebalance of the ODM gear. Normal Movement/ODM_Movement code and
input still drive the real player. Opt-in `TitanODMMotor` adds ownership-checked
suspension and current-pose release at an actual Titan catch:
- Stop existing jump/boost/inertia/stuck-check coroutines, clear old hooks and
  velocities, stop gas audio/particles/trail, suppress normal controller writes.
- Do NOT parent the human into the Titan skeleton. Keep real camera free-look;
  temporarily suspend only an enabled legacy rotator that also rotates this player.
- Release only after configured controller AND enabled solid gear shapes have a
  clear endpoint and exit sweep. Blocked exits stay suspended independently of
  Titan ownership; retry through the target/motor continuation.
- Restore incoming (decayed with hold age) + actual carried-root velocity, including
  wrist-yaw orbital motion. New upward-release channel hands back to ordinary
  gravity; planar movement reuses existing ODM inertia. Stale hooks do not reconnect
  automatically: release/repress the hook input to regrapple.
- Sample real displacement AFTER legacy FixedUpdates, not per render frame (avoids
  zero/spiked velocity predictions when display refresh exceeds physics frequency).
- Both aerial AND real-player ground holds suspend controls and release safely from
  CURRENT pose. The lab's approved ground snap-back remains unchanged.

Normal moveSpeed15 / acceleration50 / maxSpeed80 / strafeSpeed50 / range400 /
resistanceInertia.8 remain unchanged. Hook/gas/boost/attack inputs remain unchanged.
Suspension/release paths run only for the explicitly integrated player. Death and
attack-disable cleanup also safely stop legacy coroutines/windows elsewhere; normal
free-flight movement math is not replaced.

## Scene changes and preservation

- Removed ONLY scene instances with exact old prefab paths/names `Titan` and `TitanV2`.
  Did not delete their source prefabs, models or the separate `Taking Item` object.
- Added two approved modular body instances near spawn, at(-12,-1.84,18) and
  (20,-1.84,25), facing the existing player. Physics colliders use existing layer9
  so original ODM layer mask can hook them. Shared passive body prefab unchanged.
- `TitanShiganshinaIntegration` runtime opts those instances into navigation,
  perception/chase, exclusive stomp/grab/bite, motion and injury capabilities.
- Scene player gets real-health bridge, combat target and ODM motor; no lab flight
  proxy/controller/HUD shortcuts. Existing player PREFAB is not overwritten.
- Existing `Attack.damageDealer` was unassigned. Connected a scene-only damage window
  to both existing sword meshes; original F charge/release and .6s window remain.
- Owned runtime NavMesh uses explicit level box colliders in a **256x256m patch around
  world origin**, not a fake walkable plane or a whole-map production bake. Complete
  paths only. Leaving the patch can leave a Titan unable to pursue farther; full
  level navigation/streaming remains future work. Does not remove others' nav data.
- Added this exact scene to LOCAL Editor Build Settings for real 3s health respawn,
  preserving the existing Menu/FreeRoam entries. Persisted the added entry in
  `ProjectSettings/EditorBuildSettings.asset` as well as the live Editor setting.
- Scene installer is explicit/undoable and refuses wrong scene/dirty scene/duplicate
  installation. Original scene backed up before installation in
  `Library/TitanIntegrationBackup/FreeRoam-Shiganshina.before.unity`, also an owned
  external temp backup. Other scenes/materials/preexisting RagdollController edits
  were not overwritten.

## Physical controller details (important)

The real player has a ROOT-centered CCT, height3.6/radius.5, solid gear boxes, and
`FaceGrapplePoint` tilts its ROOT. In Unity2021 the **native CharacterController
capsule axis stays WORLD-UP despite root tilt**; the configured local CENTER does
rotate with the root. Verified live at45deg: size(1,3.6,1), and for center(0,1,0),
world center=root+(-.70711,.70711,0). Do not invent a tilted collision capsule from
`transform.up` just because the visible character tilts.

Contact CCD, disabled-CCT closest-point math and compound release clearance use the
native world-up axis. Observed root rotation still interpolates the center offset
and freezes the actual visual root at physical TOI; no upright-snap of the model.
Ordinary CapsuleColliders/gear boxes retain their own transform orientation.
Lab motors retain their old upright-root-only gate; actual gear explicitly opts in
to changing root rotation. Bounds/sphere are never used to enlarge the catch.

## Real health, death, swords

`TitanHealthSource` is optional; labs still own sandbox HP. `TitanODMHealthSource`
reads/writes actual `PlayerHealth`, so there is no competing health pool. Bites must
touch the actual human CCT, not just a gear box. Lethal contact releases cached hold
physics BEFORE external death/ragdoll ownership. `PlayerHealth.Died` also releases
for outside damage before ragdoll. Legacy parenting `OnGrabbed/OnReleased` is NOT used.
Death stops gravity/jump/gear coroutines and controls; Attack disable closes its
window. Existing PlayerHealth reload/respawn is retained. The current player prefab
has no assigned ragdoll receiver; this does not claim to add a new human ragdoll rig.

`TitanODMSwordSweep` samples the two actual rigid blade meshes' observed positions/
rotations during the existing attack window, using a narrow3.5cm capsule and <=2.5cm
travel steps, bounded256. Teleports/scale changes/saturated queries fail closed.
Shares `DamageDealer.ProcessContact` and its per-swing/per-Titan-region deduplication
with legacy trigger entry. No sword collider needs enabling under disabled held-body
physics, so F sword input still works while held; limb/eyes/nape callbacks keep their
existing release/death rules. Held escape balance/animation remains for player review.
E is STILL the right hook, not sandbox escape; T is STILL music, not combat toggle.

## Verification and limits

Disposable Unity2021 runtime suite compiles the actual legacy/default-assembly
adapter dependencies; no Editor-preview-scene physics tests. New reflection tests
exercise real Movement/ODM suspension, original tuning, real ground current-pose
release, full gear-box release gate, native-upright CCT headroom under root tilt,
single health source/external death, upward/planar release and actual sword crossing/
per-region deduplication. Native bounds + rotating-center CCD regression also checks
endpoints that miss but an intermediate physical capsule center crosses the hand.
All prior108 strict continuity/physics tests remain required.

Live saved scene: owned nav ready,156 triangle indices; both agents on NavMesh and
Chase, real player HP100, original camera target Player. Controlled REAL gear
`Throttle` + `FaceGrapplePoint` (not sandbox flight) driven at the scene's native
.02s fixed step demonstrated one capture(.18s after selection, root tilt8.61deg) then contact bite of real PlayerHealth,
no parent, hooks cleared, release complete. This deterministic sampled proof is not
full-speed interactive rope/camera/terrain/finger-art approval.

Actual native respawn also checked: death scheduled reload, then new scene handle,
HP100/bridge100, controller enabled, two Titans and nav rebuilt. No test-time cancel
of the reload for this separate respawn check. Temporary screenshot camera/RT removed;
Editor returned to clean/unpaused Edit Mode in the requested scene.

**116/116 passed twice**: Unity2021.3.25f1,2026-11-03 **15:52:12–27 UTC**
(PID40516,15.169s), **15:55:56–15:56:11 UTC** (PID34836,15.561s). All108 prior +8 new.
Fast6.45deg,moving6.64deg (<8deg/120Hz unchanged), forearm/palm torso penetration0;
aerial loaded death11.67m/s (<40 unchanged); intact10.86m/s,gap.003m,hipsY1.03m.
Numerical details also in `titan-prototype-validation.md` and checkpoint.
Proof: `ArtSource/Titan/Combat/Shiganshina_ODM_Integration_MCP.png`.

Still pending: interactive fast swing/boost tuning review, full-body carry clearance
(grip is swept; whole human/gear is checked on release), full level nav, crowd/corpse
stress, sword/environment blocking polish, held escape balance, production fingers/
human ragdoll/swallowing. Motor must outlive a blocked release; replacing/destroying
it mid-pending-release is unsupported. Gameplay prefab/model replacement needs its
own dependency/respawn validation.

## Try it

Open exact scene `Assets/Scenes/FreeRoam (Shiganshina.unity` and Play.
WASD move; C jump; mouse buttons or Q/E hooks; Space gas; Shift boost;
hold/release F swords; J existing menu; T existing music. No lab F-launch/E-escape.
Two nearby Titans automatically see/chase/attack; orbit/fly past their arms or slash
actual regions. Real death reloads the scene after3s. Review the new slice before a
second scoped commit; its scene/player/model dependencies include preexisting untracked
assets and must be audited, not blanket-staged with unrelated work.
