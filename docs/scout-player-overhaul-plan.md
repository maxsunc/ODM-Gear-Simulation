# Scout player, dual swords and animation overhaul — approved direction

## Checkpoint first

User explicitly requested a full checkpoint of ALL current changes before replacing
player art/animation, including previously excluded edits. Navigation checkpoint is
c07d4295, following actual ODM integration checkpoint0f371f2c. Preserve the remaining
source/assets/model moves/material and local metadata as-is in the full checkpoint.
One locked Visual Studio .vsidx search cache cannot be indexed by Git and is excluded;
no project asset/source excluded, no push requested. This checkpoint records existing work, not a claim that previously
unrelated Ragdoll/prefab experiments have been fully validated. Latest automated
navigation/combat suite remains123/123 twice on the validated source snapshot.

## Approved visual direction

- Similar low-poly silhouette/readability to existing player; Scout-inspired uniform,
  jacket, harness, boots, ODM equipment and proper paired swords.
- Believable cloth/leather/metal material separation; Unity-compatible PBR maps,
  not Blender-only node materials. Separate sword meshes and explicit grip/blade markers.
- Audit actual player dimensions/bones/references first; match existing gameplay capsule,
  controls and ODM movement tuning. Keep original art/controller as a fallback.
- Rough silhouette/material preview for review before final skinning/animation polish.
- Blender verified through read-only get_scene_info at127.0.0.1:9876: responding,
  fresh scene with Cube/Light/Camera. No model changes made during this verification.

## Animation architecture

Authored Blender clips + Unity state blending + procedural arm IK, not one baked clip
for every hook direction. A single explicit player animation coordinator should consume
read-only motor/ground/hook/gas/velocity state, avoiding competing Animator writes.
Disable animation root motion; visual acrobatics must NOT rotate camera/controller or
inject movement impulses. Existing health/hold/safe-release/sword contracts remain.

- Ground idle; responsive run with both swords trailing behind.
- Free airborne rise/coast/fall blend. User confirmed FALL has BOTH swords overhead.
- Attached, non-gassing/coasting pose with hand aiming, including while grounded.
- Left-only gas uses LEFT hand; right-only uses RIGHT hand (confirmed); dual-hook gas.
- Match each latched hand to its ACTUAL anchor, not the current camera crosshair.
  Before shooting, aim toward intended target; clamp shoulders/use stable elbow poles.
- Single release variants; full dual-hook release backflip, then free-air/fall.
- Stopping gas while hooks remain attached is COAST, not detach/flip.
- Detaching one of two hooks releases that arm and blends into remaining-hook pose.
- Recognize near-simultaneous dual release; no backflip while grounded/without clearance.
- Regrapple/landing/attack/grab/death must interrupt or override appropriately. Animations
  never lock controls waiting for completion. Fade attacking-arm IK during sword cuts.

## Full-body attack remake — explicit user addition

Do NOT just speed up the old clip or animate only the arms. Current attack feels slow,
weak and static, particularly legs. Make it convincing BEFORE particles/trails/hit VFX.

- Brief, readable anticipation/coil, sharp acceleration through the cut, clear follow-through
  and responsive recovery; strong timing contrast instead of uniformly fast motion.
- Hips lead shoulders and arms; chest counter-rotation, weight shift, wrist/blade arcs.
- Ground attack: braced footwork and pivot; avoid planted-foot sliding.
- Air attack: leg tuck/scissor/counterbalance and body twist while preserving momentum;
  no fake planted stance or frozen legs. Blend from current gas/coast pose.
- Both swords need distinct readable paths, natural grip and no body/gear penetration.
- Preserve F hold/release input. Align actual sword-sweep active damage with the visible
  cutting portion, not the current blanket0.6s window; intentional timing changes need
  tests, not silent ODM rebalance. Pose must read without visual effects.
- Keep attack/aim/held-player ownership explicit; counterattack remains usable when held.

## Implementation order / acceptance

1. Audit actual player hierarchy/rig, multiple Animator references, sword meshes, hook
   launchers/anchors, camera root and controller dimensions; verify Blender session.
2. Scout/sword blockout preview; approve proportions/silhouette/material direction.
3. Clean deformation topology, rig, weights and Blender-to-Unity material/export path.
4. Opt-in visual integration; preserve gameplay root/capsule/physics/movement values.
5. Ground locomotion + hook IK; then gas/coast/fall/release and visual-only backflip.
6. Full-body ground/aerial sword attacks with synchronized physical blade sweeps.
7. Test left/right/dual anchors, rapid detach/regrapple, landing interruptions, high-speed
   flight and sword cuts, Titan holds/counters/releases/death. Run existing regression
   suite plus new animation/ownership/timing tests; do not claim modeled/rigged until done.
