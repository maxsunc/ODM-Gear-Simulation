# Bent-arm eye-hurt pose review

Open **after_EyeCover_3x_0deg.png** and **after_EyeCover_3x_40deg.png**.
Original / bald-bearded / blonde-smiling appearances, left to right.

Before: originalfa9c161c procedural pose, fixed unscaled aim points, nearly straight
crossed arms at3x (~4.1degree elbow fold) and settled forearm/palm torso overlap.
After: true two-bone reach, outward/forward elbows (~90.11degrees), upright palms
against the eyes, scale-aware targets, bounded entry/recovery. Follow-up moves
wrist depth1.30->1.18model units to close the visible palm gap, with no timing or
bend-plane changes. PreviousFloatingPalms/ retains the earlier86.25degree draft.

Owned Unity2021.3.25f1 native Editor graphics, actual skinned prefab and original
Idle clip; no live scene/Play/input/save operation. Native after views at1/3/5/10x,
front/40degree quarter, all3faces. No new/changed test files.

Final two EXISTING eye-reaction PlayMode checks pass unchanged (see final-eye-diagnostics.xml).
First/second failed drafts are retained, not final results: restoring the overlay in
Update exposed idle to coroutine/gameplay observers. Fixed by selective restoration
and reapplication together in LateUpdate. No assertion or timing tolerance changes.

Settled forearm/hand-vs-torso collider overlap0 for all24 final sampled configurations;
not a guarantee against every visual/scenery/transitional overlap. Entry/recovery
world bone step~7.0015deg/120Hz (<8), constant-channel recovery idle residual0.
Before's first-frame rotation metric uses immediate full-weight authoring placement,
NOT a comparable native runtime entry-speed measurement.

See docs/titan-eye-hurt-pose.md and validation-summary.json for exact scope/limits.
No full-suite, current-map, arbitrary scene/crowd/WebGL or comprehensive corpse safety
claim. Original art, saved prefab/scene, stun duration/damage/AI and133 protected
current hashes unchanged at art capture. Eye-pose change is included in the approved
local checkpoint; no push requested. The separate later user re-grab-grace1->4s
tuning is not part of the earlier pose evidence. See Git log for checkpoint SHA.
