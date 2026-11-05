# Scout feeding → confirmed chomp → physical corpse / blood

Unity **2021.3.25f1**, URP12.1.11. User requested the real player going into the mouth,
ragdolling on the chomp and visible blood. Implemented after checkpoint **12901b9c**.
User subsequently authorized **commit and push** of this follow-up on
`titan-combat-updates`, also publishing12901b9c (previous originb6447972).
See latest docs/titan-work-checkpoint.md; prior local-only notes are historical.

## Result / setup

Existing scene Scout+PlayerHealth+TitanODMHealthSource automatically get ONE runtime
`ScoutPlayerRagdoll` via PlayerHealth.EnsureScoutRagdoll. No scene installer/save or
passive prefab/model/clip rebuild required. Non-Scout/legacy players keep their fallback;
prototype lab victims keep the previous mouth/grip/HP/release behavior.

Real-player feeding retains existing .7s transfer/.6s warning/.12s closure and100 bite
damage; no reach/capture radius/timing changes, no remote timed kill. Still requires
exclusive live hold, lease, capability, unchanged .55m body contact sphere and existing
scene-local carry/mouth obstruction checks. Escape/blindness/arm loss/death before
closure cancel as before. Nonlethal custom damage bleeds but doesn't kill/ragdoll.

At transfer the arm carries the same physical grip toward the teeth, smoothly yawing
and rolling the real held person sideways/slightly head-down around that SAME point.
Original grip radius and local grip coordinate never change; no regrip, parenting into
Titan bones, character scaling or remote body snap. Native CharacterController combat
geometry remains WORLD-UP even when the root/visual tilts; existing analytic shape and
full gear release clearance still apply. Held motors stay suspended, counters stay live.

A large palm's foot catch leaves a vertical body-to-grip offset; keeping that upright
at the mouth forced the palm through the chest. Feed orientation rotates this offset
inward/down so the palm remains above/outside the lips. The real Scout carry waypoint
also scales ONCE with Titan size; the old world8.2m waypoint went through a5x torso.
The original fixed fast reach/hinge/interception remains unchanged, not homing.

MouthSocket is attached to the LOWER JAW. Real feeding uses a latched HEAD-relative
closed aperture instead of chasing the open jaw down into the chest. During the bite
warning the character moves inward by default **.3 Titan-local metres** (field
mouthEntryDepth0.. .4, latched at selection). The small contact sphere follows the inner
bite point; never enlarged. At closure it coincides with that actual inner mouth/teeth
region. Telegraphed mouth ring follows it. Legacy/lab profile remains jaw-following,
no insertion or roll, exactly preserving historical fixtures.

## Health/death ownership

`TitanBiteImpact` carries attacker, real contact point/direction, measured carry velocity,
damage and predicted lethal flag. ResolveBite calls target.TakeBiteDamage only AFTER its
physical/clearance tests. Health source PrepareBite captures Scout pose BEFORE lethal
safe-release callbacks can move the controller. Existing PlayerHealth remains sole HP.
After actual HP loss CompleteBite emits blood exactly once; misses/blocked/escaped/
expired/dead/nonaccepted contacts emit none. Context clears even on callback exit.

PlayerHealth.Die captures normal death pose too, sets death before existing Died event,
stops live motors/attack, then activates presentation. Full current-pose controller exit
and ReleasePending remain independent. No cached hold-physics restoration ever writes
onto new corpse bodies. A separate corpse copy is necessary because safe release may
move the hidden motor root several metres; that must NOT yank the eaten skin out of
the mouth. Camera/controller root is not reparented or forcibly moved by presentation.
Original model/Animators/colliders/RBs are disabled/frozen on successful handoff (and
kept disabled if a later pending release restores cached flags). Scout visual driver
stops writing flips/IK. Live bodies have NO new colliders/physics before death.

The real Scout mesh/cape/gear/hilts/blades and43-bone pose are copied into an inactive,
scene-owned standalone root. Disabled clone Animator never Rebinds/plays a death clip;
all captured bone locals/model world pose are restored. Skin bone refs bind to copied
bones. No asset mutation or detached model sharing animated bones with the live rig.
Clone scripts/audio/trails disabled. Source visual with foreign physics refuses takeover.

**13 dynamic bodies /12 CharacterJoints**: Hips,Chest,Head,both upper/lower arms,
upper/lower legs,feet; other43-bone fingers/cape/hand branches follow their segments.
Closed capsule/sphere corpse shapes in true world dimensions despite FBX bone scale.
Joint anchors set at actual current pose AFTER native RB frames initialize; inactive
new RBs can initially report origin (fixed a10m anchor-gap regression during development).
No animation writes corpse bones, forces attached to Titans or full corpse pooling/repair.

Defaults on optional Edit Mode **Scout Player Ragdoll** component (add on player to
persist custom settings; otherwise auto runtime defaults):

- Corpse Lifetime8s (bounded1–30); original scene respawn remains3s, destroys old scene.
- Maximum Inherited Speed12m/s, bounded input only0–20, not post-simulation clipping.
- Bite Outward Speed2.5m/s,0–6; small outward slump so loose body leaves the jaw rather
  than dropping straight through Titan chest.
- Bite Tumble1.5rad/s,0–3, opposite chest/hip initial angular response so zero relative
  torque under uniform gravity doesn't leave a floating rigid-looking mannequin.
- Solver12/4,angular cap4,current-pose anchors,projection.015m/8deg; no global settings.

Corpse self-pairs and catching Titan pairs ignore contact (LOCAL Physics.IgnoreCollision,
not global layer rules), preventing overlap explosions between the teeth. World/other
Titans/corpses still collide. Catcher ignore lasts corpse lifetime; no jaw pinning or
full swallowing/deletion animation. Full-body scenery overlap/saturated64-hit query
rejects unsafe dynamic birth (no distant corpse teleport). Failed presentation exposes
Error and uses legacy fallback. Legacy RagdollController null Animator/root RB now guard
rather than throwing the previously reported unassigned-reference exception.

## Blood

Reuses existing assigned `TitanSwordFeedback.effectMaterial` and bounded8x96-particle
world-space blood pool; one80-particle bite burst with1.5 size/speed multiplier at inner
mouth point, directed out of lips. Respects bloodEnabled/feedback.enabled. No new
material instances/shaders/audio/time-scale/camera shakes or damage numbers. Existing
FreeRoam effects setup supplies material; without it the bite remains mechanically valid
but no blood is rendered. BiteBloodCount is diagnostic only, not damage authority.

## Validation / evidence / limits

Full owned disposable suite **222/224 PASSED TWICE**:
-2026-11-05 **03:20:07–27UTC**,PID5752,20.4350115s.
-2026-11-05 **03:22:24–45UTC**,PID14044,20.6246924s.
Only SAME2 pre-existing retimed-Run recipe assertions fail; never regenerated Run or
weakened its tests.13 new cases: both real ground hands/insertion/one corpse+blood,
nonlethal HP path,escape/new blocker no effects,actual corpse physics/current anchors,
auto adapter uniqueness/passive live rig,scenery-blocked corpse birth,4 supported
3x/5x left/right aerial feeding/grip/arm-clearance cases,guarded native graphics capture.

Real Scout3.6m CCT / visual2 / actual mesh/prefab/materials in owned LOCAL PhysicsScene.
3x/5x arm maxima **6.503/6.581deg per120Hz**, unchanged bound<8. Mouth/carry distal
forearm/palm penetration **0**, unchanged bound<.005m; same established clearance window
.9s onward through bite, not shoulder seam/catch takeover. Original radius/local grip
coordinate invariants <.003m throughout yaw+roll. Corpse anchor gap<.002m;1.5s simulation
maximum **17.55m/s** (<40), no velocity clipping after simulation.

**Known scope:** early fixed reach/takeover (<.9s) in these newly tested giant fixtures
still has pre-existing distal/spine overlap~1.97–3.08m, logged SEPARATELY, not claimed
clear or hidden by a looser threshold. Mouth fixes/roll are after capture; fast reach
was deliberately not redesigned. Full scaled ground reach, ordinary full-speed ODM,
worldbody/gear carry clearance, crowds and terrain remain separate gameplay review.

Dedicated graphics-enabled OWNED runtime URP **1/1 PASS**,2026-11-05
**03:22:09–10UTC**,1.1057197s. Actual3x aerial feeding, jaw closure, real13-body corpse,
red-pixel assertion (blood outside mouth), real dust-free meshes/teeth/materials.
`tools/capture_scout_bite.py` only runs owned project with no -nographics; no live Editor
input/Play automation. Batchmode uses completed camera frames, NOT WaitForEndOfFrame
(which doesn't resume in batchmode; earlier preview attempt timed out and owned process
was killed by Python subprocess timeout, never main Editor). Pipeline restored in finally.
Raw800px frames: Library/ScoutBiteReview, published3-frame panel/raw copies under
ArtSource/Titan/Combat/Scout_Bite_Chomp_Preview.png andScoutBite_*.png. Controlledfixture,
NOT ordinary map combat footage or final aesthetic approval. No player dismemberment,
mesh chewing/deformation, gore chunks or professional chomp audio added.

Final audit:202 runtime/test/art/effects/legacy files byte-matched the completed final
owned run; both new script GUIDs present/unique. No staged follow-up changes; only
Unity's ordinary serialized whitespace/line-ending warnings, code diff check clean.
Saved scenes/shared prefabs/art/animations/nav/assets/ProjectSettings left unchanged
from12901b9c. User Play not stopped/reloaded/saved or synthetically controlled. Only new
source/tests/docs/helper/preview changes; do not blanket-stage local Plastic/IDE caches.
