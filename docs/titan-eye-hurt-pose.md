# Bent-arm eye-hurt / blinded pose

User request: hands still cover hurt eyes, but bend the arms naturally rather than
holding almost straight/crossed arms through the body. Runtime change only in
`Assets/Titan/Prototype/ModularTitanBody.cs`; no clips, Blender art, rig, prefab,
scene, collider, HP, stun duration, AI, attack or sound edits.

## Change

Old CoverEye aimed upper arm and forearm independently at fixed WORLD-sized
points. These targets did not scale with the Titan and did not solve the real
arm lengths; on the sampled3x body the elbow fold was only~4.1degrees.

New pose uses the actual upper/forearm lengths and a scale-aware wrist target,
with an outward/forward elbow pole. Two-bone reach is clamped below full extension.
Palms are upright and in front of the eyes rather than wrist origins aimed inside
the face. Follow-up user feedback requested actual palm contact: wrist depth was
pulled back from1.30 to1.18model units, removing the visible forward gap while
keeping the same bend plane, palm height and spacing. Current sampled bend is
~90.11degrees at all reviewed sizes (previous floating-palm draft~86.25). It starts
lifting elbows during the original head recoil; final wrist coverage retains the
alternating25/75ms lead and existing cover ramp. Stun clock remains unchanged
(saved gameplay prefab12s, lab code default5s), as do damage/cancellation rules.

World rotations are limited to840degrees/s for EACH upper arm, forearm and hand,
using the last presented rotation (7degrees/120Hz before float roundoff). Fade-out
continues until the base animation is reached, instead of snapping on stun expiry.
Cached hand rest rotations give stable palms without a second animation owner.

Constant Animator channels can retain the previous overlay. Restoration is
selective in LateUpdate, immediately before reapplying the pose: only local
channels still equal to our last overlay within quaternion precision are restored.
Newly evaluated animation channels are retained. Do NOT restore arms in Update:
that exposed the idle pose to gameplay/coroutine observers between Update/LateUpdate.
This initial draft failed the unchanged fast-eye case; corrected before completion.
No new Update/root/position writer, global Physics setting, reparent, bone scaling,
mesh/material or native joint ownership changes. Dead/detached arms are excluded;
leg-loss injury pose remains the later exclusive owner. Zero/invalid delta does
not pose, so pause does not advance the overlay.

## Review / limits

`tools/capture_titan_eye_pose.py` runs a marker-owned native Unity2021.3.25f1
PreviewRenderUtility art session, never the live Editor. It temporarily restores
fa9c161c's original body source into the owned worker for --label before, or copies
current body source for --label after. Its helper exists ONLY in the worker, not
Assets/Tests or the main Editor folder. Original Idle is sampled, real StunEyes /
TickStatus / TickEyePose called, actual skinned bodies rendered.

Evidence: `ArtSource/Titan/EyeCover/`:
- Before original1x/3x, front/40degree quarter views, all3 faces.
- After1x/3x/5x/10x, front/quarter, all3 faces (24reviewed configurations).
- Settled native forearm/hand-vs-Hips/Spine/Chest collider overlap: before3x
  ~.04133model units; final0 in all24 configurations. This does not measure every
  mesh triangle, upper-arm shoulder seams, or every transitional/scene contact.
- Maximum observed presented bone step during entry/recovery~7.0015degrees/120Hz,
  below8; after expiry and1s recovery with NO Animator resampling (constant-channel
  retention case), idle residual0degrees. This is isolated pose review, NOT a new
  comprehensive native gameplay/corpse/hold-penetration safety certificate.
- Existing `EyeHitSnapsHeadThenCoversEyesQuickly` and
  `EyeStunRecoversAndOnlySurvivingHandsPose`: **2passed/0failed**, unchanged.
  Earlier failed drafts retained in separate XMLs; no assertions/tolerances changed.
-133 protected current paths including face variants, original materials/clip/
  prefab/scene/audio tuning unchanged. Current body source matches owned worker.

No new/changed test code and no fresh full-suite or saved-map claim. Current
Library/TitanValidationRun.json is FILTERED to these two cases. Native preview
is not proof for arbitrary scenery, all timing/order combinations, crowds/devices,
WebGL or long-duration corpse motion. Subjective pose feel remains fresh Play review.

Previous face work pushedfa9c161c. User approved this eye-pose revision for a LOCAL
checkpoint, together with their later re-grab-grace tuning1->4s in TitanCombatTarget.
Eye-pose evidence above predates that separate tuning; no new QTE timing validation
is claimed. Use Git log for the local checkpoint SHA. No push requested.
