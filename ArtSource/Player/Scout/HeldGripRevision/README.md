# Revised held grip / back-of-sword beats

Current revision after local `bf77e54c`. No new test code was created or changed.
Compiled in marker-owned Unity2021.3.25f1 and visually reviewed using the existing
both-hand native preview fixture (2/2 completed). No new full-suite claim.

- Render-only waist mounting slightly inside the palm near its heel.
- Visual body-up is perpendicular to current physical forearm direction; face
  points back toward the wrist. Native WORLD-UP CCT/root/grip remain untouched.
- Right arm slowly cocks back and beats the back/flat of its sword across the
  wrist/heel of the hand, then recoils. The other arm braces. No Attack event,
  damage query, new slash, escape benefit or automatic release is dispatched.
- Loop1.2 ->1.8seconds, animator speed remains1. Eight PNG phases per hand and
  GIFs225ms per phase show the slower rhythm, not realtime crowd performance.
- A short visual taper returns to the original native feeding pose on ToMouth;
  safe pending exit stays suspended. Release blends the visual offset away;
  disable/history reset restores it. Cape history resets on the mounting frame.
- Only passive visual pivots are eligible; foreign collider/Rigidbody children
  refuse mounting. No root/physics/parenting/camera/Titan-bone writes.

`Left_00.png` / `Right_00.png`: cocked-back pose.
`Left_04.png` / `Right_04.png`: back/flat beat at wrist.
`Left_Struggle.gif` / `Right_Struggle.gif`: full1.8s gesture.

The captures manually sample clip phases in a paused, actually captured3x Titan
hand. Original QTE/health HUD remains visible. Subjective grip feel and other
sizes/camera views/arbitrary scenery remain gameplay review, not certified by
these images. Historical full-suite results under ../HeldStruggle describe the
previous1.2s gesture; those results are NOT asserted for this revised pose.
