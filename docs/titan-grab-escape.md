# Player grab escape QTE

Unity **2021.3.25f1 / URP12.1.11**. User approved three prompts, wrong-key reset,
safe jump-away and one-second re-grab protection; explicitly chose **3 seconds**.

## Gameplay

- Starts only on a confirmed physical capture by `TitanGrabAttack`, never on a
  warning, missed reach, forecast, or mere proximity.
- Three random prompts from **W A S D Q E**, shown ONE at a time; adjacent prompts
  never repeat. Private System.Random does not consume Unity gameplay randomness.
- **3.0 seconds TOTAL**, measured from capture, scaled with game time/J pause.
  Wrong legal key resets progress to0, not time. Unsupported keys are ignored.
- Only fresh key-down edges count. Pre-held keys, auto-repeat and simultaneous
  six-button mashing cannot advance the sequence. Releasing between presses works.
- Gameplay holds now drain **10HP/second** through the real health bridge, including
  the QTE, with **no regeneration**. A low-health player can die from the squeeze
  before3s; the input window is not invulnerability. Damage persists across escapes.
  No pre-capture/warning/proximity drain. See [current health/stomps](player-health-and-stomps.md).
- On success BEFORE the deadline, bite and hold damage are cancelled synchronously. Titan blends
  its arm back through recovery; no late damage, bite blood, eaten cue or corpse.
- The existing whole-gear, WORLD-UP native CCT clearance/exit sweep must succeed
  before restoring physics. Blocked escape stays `ReleasePending`, with controls
  suspended independently of the Titan; the cancelled bite cannot punish waiting.
- On clear exit, the existing real motor receives **6m/s up +6m/s planar away**.
  This is a small bounded escape launch instead of a carry-speed slingshot. It uses
  normal CharacterController/inertia/gravity movement and native ceiling collision,
  not new parenting, collider ignores, warping through walls or a second root writer.
- **1 second re-grab immunity**, including other Titans/hands, starts at safe exit.
  Pending release does not consume it. It is NOT general damage immunity.
- Failure at>=3s leaves the normal carry→mouth→contact-validated bite to finish.
  Damage/death remain exclusively real PlayerHealth ownership. No delayed kill
  timer, forced death, resurrected HP or post-escape asynchronous bite callback.

HUD (user-requested minimal revision): only the large current key, three-segment
progress bar and shrinking time bar. Compact260x128 panel with32% background alpha;
no headings, instructions, numeric counts/time or outcome text. Wrong-key reset
flashes red; successful progress flashes green. No cursor capture, root motion,
shared GUI skin edits or audio retuning.
QTE movement/Q/E inputs remain quarantined through release until keys come UP,
so the final success key does not also move/strafe/fire an ODM hook. Fresh gameplay
inputs work normally after release. Existing F release/repress rule remains.

## Installation / ownership

`Assets/Titan/Prototype/TitanGrabEscape.cs` owns prompts/input/HUD; no controller,
rig, bone, native body or global Physics writer. `TitanGrabAttack` owns damage and
its single logical timeline. Only opted-in captured victims extend the existing
holding phase enough to allow3s BEFORE mouth transfer (unless real HP loss kills the victim first). Lift speed/reach/grip
and the transfer/bite/recovery durations are not retimed globally.

`TitanCombatTarget` keeps the exclusive lease, granting a bounded QTE budget
(max10s) and ignoring the pre-capture interval on the first QTE hold tick. A catch
frame hitch cannot consume the newly displayed opportunity. Late key presses fail
before input acceptance. Success freezes the carry pose time and begins recovery;
no change to surviving-bone injury/death ownership.

`TitanFreeRoamIntegration.Begin()` and `TitanShiganshinaIntegration.Start()` install
one component on their actual motor-bridged player. Saved GiantForest2/FreeRoam
players get it automatically in the next fresh Play. No scene/prefab regeneration
or saved-asset edits are necessary. Passive Titan prefabs and standalone labs retain
their old timing/E escape unless a player is explicitly opted in.

Lifecycle: owner/victim death, limb/eye cancellation, disable/destroy/unload release
and cancel the challenge. Death while release is pending cannot restore cached live
collider/body flags over corpse ownership or launch a dead player. Inactive-object
motor cleanup cannot start an inertia coroutine on an inactive GameObject.

## Size-related support fixes found during validation

- The old fixed4m-up/10m-down foot support probe could not reach the floor from a
  10x ankle. Support QUERY distances now scale with the rig (unchanged at1x). This
  does NOT enlarge the grab radius/reach or override any clearance/ground filtering.
- Unbounded Titan-scaled feeding depth at10x pulled the giant hand about.20–.23m
  through its chest. Depth is now bounded by HALF the HUMAN controller's world
  height (1.8m for this3.6m player). Existing3x/.9m and5x/1.5m feeding depths stay
  unchanged. Same native palm/grip, human offset, mouth radius and insertion contact
  remain; no regripping, homing or remote damage. Strict mouth/arm bounds unchanged.

## Validation / scope

Owned disposable workers ONLY; never stopped/reloaded/saved/controlled the live
Editor or sent live keyboard input. Existing saved scene, palettes, passive prefab,
NavMesh settings/data, art, animations, URP and Unity versions unchanged.

Full suite: **292pass /7preexisting failures /1graphics skip**,300 cases, vs fresh
pre-code **257pass /same7 failures /1skip**,265 cases.35 added cases (one native
HUD fixture gated by explicit environment variable); original assertions preserved.
The same7 existing failures are protected Scout Run2, user face/stun/reference-speed
fixtures3, and user's one-scream palette vs original two-clip expectations2.
No asset tuning or failed assertion was reverted/relaxed to make them disappear.

New coverage: start/long hitch, exact deadline, short lease, held/wrong/chord input,
pause/cancellation/disable, idempotent install/private RNG, real confirmed failure
bite, no late success effects, safe blocked release, exact grace/no damage immunity,
input quarantine, native ceiling and external death while pending. Left/right real
Scout QTE SUCCESS and TIMEOUT at **3x/5x/7x/10x** retain arm `<8deg/120Hz`, same-grip
error `<.003m`, and later carry/mouth penetration `<.005m` (.9s onward).
Additional non-QTE7x/10x cases retain these bounds. Early reach/takeover overlap is
still recorded separately (about1.97–6.17m across sizes), NOT hidden or certified clear.
Original corpse-speed `<40m/s` and old jump/physics/audio/combat tests remain intact.

Saved GiantForest2 native integration **3/3 pass**: one automatically installed QTE,
actual current random spawn, full-rig-clear7x/10x births and25 complete nav routes;
no navigation rebake. Native HUD **1/1 pass**, actual GameView screenshots from an
owned non-batch Unity2021 Editor (IMGUI needs a real GameView), no synthetic input.
See `ArtSource/Titan/GrabEscape/` for snapshots/XML/source preservation evidence.
No crowded50-Titan FPS, device/WebGL, subjective difficulty or audio certification.

Commands:
```
python tools/run_titan_validation.py --reuse OWNED_PROJECT --wait
python tools/run_titan_validation.py --reuse OWNED_PROJECT --filter EscapeQte --wait
python tools/capture_titan_grab_escape.py
```
Capture requires fresh matching owned source/full-run metadata. It requests real
completed-frame screenshots without batchmode WaitForEndOfFrame hangs and closes
its own worker when the test run finishes. The live Editor is never targeted.

Runtime component toggles: `showHud`; `automaticInput` (off only for deterministic
fixtures). Balance constants: `TitanGrabEscape.Window=3`, `RequiredPresses=3`;
launch/grace live in Target safe-release handling. No edits to user audio assets.
New feature is a LOCAL incremental commit; no new push authorization.
