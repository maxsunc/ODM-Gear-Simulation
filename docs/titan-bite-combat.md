# Titan ground grab → hand-to-mouth → bite

## Current real Scout presentation follow-up

**docs/titan-scout-bite-presentation.md** supersedes the old proxy-only art/death scope
below for real Scout players. Character feeds sideways/inward into teeth, corpse13-body
physical copy captured BEFORE safe release, blood only after real HP loss on closure.
Auto runtime Scout adapter, existing3s respawn; no live-Editor/scene/prefab edits.
Real carry waypoint scales once, head-relative aperture avoids open jaw pulling hand
into chest, preserve actual grip radius/coordinate through yaw+roll. Non-Scout/labs keep
historical policy. **222/224 twice** (same2 Run),13 new cases;actual3x graphics1/1.
Mouth/carry .9s-onward torso penetration0,6.581deg/120Hz arms,corpse17.55m/s max1.5s,
all unchanged strict thresholds. Early fixed reach overlaps logged separately/not fixed;
full ordinary ODM/giant ground behavior remains separate. Preview
ArtSource/Titan/Combat/Scout_Bite_Chomp_Preview.png. Follow-up after12901b9c; user
subsequently authorized commit and branch push. See latest work checkpoint.

## Historical contact/ownership milestone

Continuation of [grab/hold/release](titan-grab-combat.md), before the required
mid-air ODM interception variant. This document records the **sandbox ground milestone**,
Unity2021.3.25f1; that milestone did not change gameplay scenes/controllers/rig/art/physics.

Ground bite/arm fix **28cb997a** and aerial follow-up **940daf1d** are now PUSHED to
`origin/titan-combat-updates`. After approval the user requested the actual Shiganshina
scene: [real ODM integration](titan-shiganshina-integration.md), **116/116 twice**.
That separate uncommitted review slice connects actual PlayerHealth/gear/two swords.
Lab defaults below remain; real ground/air holds both use motor suspension and safe
current-pose release, not the proxy ground snap-back. No lab E-escape binding in gameplay.

## Arm-spasm fix and MCP visual review

User reported the arm spasming during hand-to-mouth. Reproduced in the working
Unity2021.3.25f1 Editor through MCP: **137.17 degrees in one1/120s sample** near3.14s.
A new both-hand/four-animation-phase regression failed at137.26 degrees although
all73 contact/physics checks still passed. Reach alone did not detect the bad roll.

Fixed selected-arm IK using explicit elbow-plane rotation frames instead of
shortest-arc Aim with undefined roll. Infer the source hinge from the anatomical
hand bone, not the offset grip socket. Also apply jaw opening from its cached closed
local rotation: a constant Generic Animator channel can leave procedural rotation
behind, so additive jaw tilt accumulated and spun the mouth/arm target. Closing or
cancelling restores only the alive animated jaw; injury/death handoffs remain untouched.
No attack timing, reach radius, model/rest rig or physics tuning was changed.

**74/74 passed twice**: 2026-11-03 **02:44:07–20 UTC** and **02:49:24–37 UTC**.
The new regression caps rotation steps at8 degrees/120Hz and jaw opening at28.1 degrees;
measured arm maximum **2.26 degrees**. Original73 checks still pass. Mouth death
peaks10.57/10.36/11.82/11.81m/s; intact11.06m/s,gap.005m,hipsY.84m.

MCP was offline initially; started the existing local10.3.0 server and reconnected
the installed Editor bridge. Verified the project/scene/Unity version, reproduced
and captured the failure, then captured fixed poses on both sides. Live sampled
upper-arm maxima2.13 degrees left/1.72 right; hand-to-mouth gap.35m. Reviewed adjacent
rendered frames; the previous flip is absent. Saved comparison:
`ArtSource/Titan/Combat/GrabArmContinuity_Comparison.png`.
This is a sampled visual pass, not final production animation/finger approval.
Temporary camera and reconnect script removed; returned to Edit Mode, scene clean,
no scene saved. Unity2021 MCP became unreliable across paused Play Mode; keep the
Editor unpaused and manually tick a frozen sandbox actor for future sampling.

## Try it

Play `Assets/Scenes/TitanAISandbox.unity`. **T** toggles automatic grab/stomp;
**9/0** requests left/right grab; **8** stomp; **E** escapes immediately.
**6** blinds, **2/3** cuts arms, **4/5** cuts legs, **K** kills the Titan,
**P** pauses AI, **R** resets the Titan and proxy. WASD/Shift and camera unchanged.

The existing cyan/red ground grab marker is unchanged. On capture, the Titan lifts
its actual selected hand toward chest height, rises from the crouch and brings
that hand to its jaw's **MouthSocket**. The jaw opens; an **orange mouth ring**
warns of the bite, turning red while the jaw closes. Escape or counter before closure.

`TitanAISandboxDemo` explicitly sets `Grab.enableBite=true`. A separately added
`TitanGrabAttack` defaults to the previous nonlethal hold/release; the reusable body
prefab still has no AI or attack components. No dependency on legacy PlayerHealth.

## Fixed timeline

All durations, feature flag, damage and mouth radius are latched at selection;
changing inspector values mid-attack does not shorten its current telegraph.

| Stage | Duration | Absolute time from selection |
|---|---:|---:|
| Ground windup | .65s | 0–.65s |
| Reach/contact | .18s | .65–.83s; grip contact at .83s |
| Lift/hold | 1.5s | .83–2.33s; first .55s lifts to chest |
| ToMouth | .7s | 2.33–3.03s; rise, planted feet, hand-to-mouth IK |
| BiteWindup | .6s | 3.03–3.63s; open jaw and orange warning |
| Bite | .12s | 3.63–3.75s; jaw closes; one contact event at end |
| Recovery | .45s | 3.75–4.20s; release and blend home |
| Cooldown | 3s | Starts after completion/cancellation |

A ground miss skips all captured phases. `enableBite=false` preserves the old
nonlethal1.5s hold/release. Automatic updates cannot consume time from before a
selection made earlier in the same Update; explicit TickAttack supports fast-forward.

## Real contact, not a delayed kill

At the contact timestamp, the attack samples its current Generic animation without
advancing its clock, solves the actual arm/grip and jaw pose, and follows the held
proxy to the real grip. It applies **100 HP once** only if:

- The intended target is alive and still exclusively held by this attack.
- The selected arm, both standing legs, vision, owner/AI and mouth remain valid.
- The target's actual supported collider geometry touches the **.55m sphere at
  MouthSocket**. A hand unable to reach the mouth produces a miss, not a remote kill.
- Scene-local carry/transfer/mouth queries find no obstructing solid scenery;
  saturated64-hit buffers fail closed.
- The independent victim lease is still valid. Logical time since grip contact
  also constrains the lease: a long tick cannot bite after a shorter timeout just
  because victim LateUpdate has not run yet.

No Invoke, coroutine, scheduled death callback or bite-after-release timer exists.
A long tick resolves each contact at most once and releases/completes correctly.

Held colliders remain disabled throughout. `TryGetHeldContact` computes configured
Box/Sphere/Capsule/CharacterController geometry in world space; it never relies on
empty disabled bounds, an AABB approximation for a rotated box, or briefly enabling
colliders. Unknown disabled mesh shapes fail closed and need a real-player adapter.
The same closest-point helper remains compatible with normal stomp contacts.

Rendered carry steps sweep a .45m grip volume before the target follows in LateUpdate.
Bite fast-forward also checks the two lift/transfer segments and final mouth volume.
These are conservative hand-path checks, **not full swept human-body collision/terrain
IK**; edge clipping and detailed body clearance still need production integration.

## Ownership and interruption

The original exclusive hold lease, no-parenting rule, collider/RB-state restoration
and independent escape/timeout remain. Default hold-to-bite time is2.92s, below the
victim's4s maximum lease. Longer configured sequences can safely time out instead.

Escape, blindness, grabbing-arm loss, either leg loss, Titan death, victim reset or
disable, owner/director disable, AI pause or target replacement stop the sequence
before contact and release. Other-arm loss does not prevent a surviving-arm bite.
No detached branch is posed, and injury/death never gets a standing-pose reset.

A lethal bite intentionally releases the victim and still completes recovery;
that release does not recursively cancel the attack. Damage callbacks can themselves
kill/sever the Titan: contact-pose restoration then yields to injury/ragdoll ownership
rather than overwriting the handoff with old animation. Movement stays locked until
recovery/cancel. Perception can continue; automatic actions remain mutually exclusive.

**Proxy policy remains:** release restores its captured pre-grab world pose. After a
lethal bite, the dead teal proxy is visible there until R resets it. No swallowed
mesh, corpse destruction, gore, respawn or production character death adapter is
claimed. It is the contact/ownership foundation of eating, not final consumption art.

## Validation and remaining work

**73 passed /0 failed twice**, Unity2021.3.25f1, in the owned disposable project:
2026-11-03 **01:50:19–32 UTC** and **01:52:14–28 UTC** (latest PID26732).
Four mouth-phase5s death simulations peak10.29/10.09/11.27/11.04m/s, below the
unchanged40m/s assertion. Latest intact10.98m/s,gap.005m,hipsY.84m.

The regression suite includes all58 prior checks and15 additions: actual mouth reach
with both hands, controller-only victims, fixed timing/nonlethal configuration,
late escape, interruption matrix, cover and rendered carry blockage, rotated disabled
shape geometry, short lease vs long tick, exactly-once long-frame damage, callback
handoff, foot support and other-arm survival, four mouth-phase death simulations,
normal-frame automatic playback, and saved-lab bite/reset integration. Existing
AI-pause/reset-while-held tests now also interrupt the mouth windup.

Run only in the owned disposable project via `tools/run_titan_validation.py`;
never restore the removed Editor-preview physics validator. Final numerical results
are recorded in [validation](titan-prototype-validation.md) and
[checkpoint](titan-work-checkpoint.md).

A sampled MCP visual pass for the arm fix is now recorded above. Full-speed feel,
art and production animation review still matter; numerical checks are not a substitute. This is
procedural blocking with mitten hands, not articulated fingers or production mocap.

**Aerial follow-up:** the [interception lab](titan-aerial-grab.md) now implements
actual3D hand reach, committed aim, swept hand-box/player-capsule contact and a
separate current-air-pose release. Real rope/boost/controller integration remains
next: do not reuse ground snap-back or fake a catch with a giant radius/homing.
See the motor contract, limits and108-pass evidence in `titan-aerial-grab.md`.
The aerial mouth path now keeps the elbow forward/outward and turns the held person
smoothly around the same grip, avoiding the chest while preserving physical contact.
Ground carry remains unchanged.
User subsequently requested no aerial warning-arm preparation: aerial catches now
reach directly in.22s, with shorter miss retry; ground/bite warning timings remain.
