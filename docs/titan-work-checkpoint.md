# Titan checkpoint — compact continuation context

## User goal / current work

Build a modular Titan for the existing ODM/sword game (Unity **2021.3.25f1**, never
upgrade). Art, rig, detachable limbs and ragdoll are established. The user accepted
the reference-inspired revision, then approved **patrol → sight → chase** in an
isolated sandbox. That phase is implemented. User then requested a **single-leg
kneeling collapse with head down, and helpless prone collapse when both legs are
gone**, before proceeding to attacks. That phase is complete. User then requested
**faster, less robotic, less predictable motion across all animations** and approved
an overhaul. **That motion overhaul is now implemented and validated.**

User previously requested conversation compaction; this file preserves the working
state. No compaction command was executed by the assistant.

## Current: approved combat UX / longer cloth cape — staged local commits

User approved dedicated hit volumes, same-Titan impact-speed grace, Option B true
cloth with LONGER cape, kill text, blood1/blood2 WAV layering and fixes for moving
Titan grapples/held slashing. **Commit as we go locally; no push requested.** Contract
and evidence: **docs/player-combat-ux.md**. Stage1 implemented/validated: collider-local
independent moving anchors, invalid/sever/death release, hard held/ReleasePending/
dead input + damage gates, synchronous capture cancellation and held visual priority.
Full owned suite **226/228**,2026-11-05 16:22:58–16:23:16UTC; only established two
Run failures unchanged. Stage2 now implemented: dedicated Scout native box sweep
(rear nape/front eyes + obstruction checks, fixed player reach, teleport/saturation
rejection), .18s same-Titan physical-impact speed grace (no wall/hold/teleport banking),
confirmed1.5s kill toast and supplied blood1/blood2 audio layer via small Resources
palette, no scene rewrites. Full suite **235/237**,16:35:51–16:36:11UTC2026-11-05;
only same Run failures. Stage3 now COMPLETE: ScoutCapeCloth render-only managed
particle fabric, .98m vs.62m source length (~58% longer), original atlas/wing forms,
273particles/586vertices/1000triangles, pinned shoulders + fixed120Hz constraint/
airflow/body-gear proxy simulation. Not bone flutter; no native Cloth dependency,
colliders/RBs/globalPhysics/root/bone/clip writes. Source art/prefab/Run unchanged.
Auto-add via Scout driver; add persistent component on player root for Inspector
tuning. Dead player stops cloth; corpse owns independent folded mesh snapshot,
following copied physical chest with proper lifetime cleanup. No self/scenerycloth
collision or WebGL/FPS certification. Native sheet/GIF:
ArtSource/Player/Scout/CombatUX/Scout_LongCape_Unity.png / Scout_LongCape_Wind_Unity.gif.
Final full suite TWICE **242passed/2knownRunfailures/1graphics-onlyskip**:
2026-11-05 17:15:11–40UTC/28.611s and17:17:58–18:26UTC/28.505s. Twenty new non-graphics
cases pass; all strict original physics bounds retained. Separate actual native
cape1/1pass17:15:51–54UTC,3.529s,Intel Iris Xe/DX11; actual gas-flight pose visual
transport80m/s maxclothstretch1.06917. Old bite/blood graphics also rerun1/1.
Stage1 localcommit56261300;Stage2 local314eb2ae;finalclothcheckpoint see gitlog.
No push. Existing user scene edits + caches remain excluded. Further ordinary
playtesting: fixed slash feel, audio levels, full-speed forest traversal, WebGL /
performance. Native screenshots are owned fixtures and omit screen-space HUD.
The user's GiantForest2 scene has NEW saved edits after346c85c0: preserve, don't
rewrite/stage it. Baseline recorded in Library/CombatUXBaseline.json. Live Editor
untouched. Source audio supplied Assets/Audio/blood{1,2}.wav now integrated without
replacing current effects. NEW approved follow-up: extra blood layer now ALSO on
confirmed left/right LEG severs (not ordinary hits/blindness/misses). One real mixer
at Assets/Resources/Audio/GameAudio.mixer: Master with SFX/Music children; existing
sources route without source volume/pitch/spatial changes. GameAudio persistent
auto-manager, main Menu + existing J-menu sliders; central M music-only mute restores
previous nonzero level, prefs persist independently and on unscaled-time debounce.
GameElements music arrays and Menu SceneManagement soundtrack route Music; remaining
scene sources + immediate sword/jump/ODM/spawn runtime audio route SFX. Source scene
assets remain untouched. Nine new audio cases + expanded leg finisher test pass;
latest full suite251passed/2knownRunfailures/1graphics-onlyskip, same strict bounds.
Full contract/evidence: docs/game-audio.md. No push; incremental local commit see gitlog.

## Previous: Giant Forest 2.0 — larger rocks / doubled walls / local commit authorized

User requested more varied, much larger rocks and walls2x their current height,
then explicitly authorized **committing what we have**. Completed scoped resize:
40 rocks now8 small4–8m /16 medium10–17m /16 large22–32m targets, native saved mesh
heights4.28–30.15m; uniform collision scales,12% ground embed, bounded scenery-clear
placement. All254 wall bays/gateway/four piers double HEIGHT ONLY (~121.1m visible,
120m piers); same widths/thickness/base12m, arch now~78m high/26m wide. Tree/terrain/
log/actor/camera/tuning preserved. Own3/5/6 navigation rebaked;15 routes still complete.
**6/6 headless map cases pass** (graphics intentionally skipped),2026-11-05
15:20:49–15:21:00UTC/11.551s; **1/1 native graphics pass**15:21:51–15:22:05UTC/13.944s.
New rock/wall native preview in ArtSource/.../MapReview, including RuntimeRocks.png.
Helper now supports explicit `snapshot` current-saved-source protected baseline and
`resize` scoped worker action. No live Play/scene reload/save/input. Local forest
checkpoint includes prior source art/models/map + iteration; **NO PUSH authorized**.
See git log for local commit SHA; latest published origin remains3e9a738d.

### Original assembled-map delivery (now includes scale iteration above)

User APPROVED first models and authorized building GiantForest2, with varied trees
about2–3x Titan height. Earlier models-only boundary superseded. Full contract:
**docs/giant-forest-2-map.md**. Native preview:
**ArtSource/Environment/GiantForest2/MapReview/GiantForest2_MapPreview.png**.
**Assets/Scenes/GiantForest2.unity** now has the new environment, not the old clone:
2048m heightfield,208 trees (actual native LOD0 heights150.52–215.48m,2.09–2.99x
largest6x/72m Titan), winding route/glade,1536m enclosure/254 wall bays/open gateway,
40 rocks/9 fallen logs. Three tree LODs, wood-only mesh collision, physical terrain,
layer9 hook masks, shared vertex-painted URP materials/alpha-clip foliage. Art in
Assets/Art/Environment/GiantForest2, terrain in Assets/Environment/GiantForest2.
Original editable art/studio scales unchanged; prefab instances supply larger sizes.
Own map-specific3x/5x/6x NavMesh assets in Assets/Navigation/Titans/GiantForest2,
2549 real collider sources,15/15 cross-forest paths complete. New6x agent1479372276
appended only; old agents/data unchanged. FreeRoam STILL only has3x/5x data.
Six copied actor scales3/5/6/6/6/6 and existing combat/HP/sight/player tuning preserved,
relocated to clear sites. Player `(0,12,-550)`. Spawner uses new bounded forest volume.
New scene appended to BuildSettings, no old index changes. Old Map Control/Cube removed
ONLY from this scene. Original FreeRoam still SHA2564409c076...da7bfa, unchanged.
Original delivery: **5/5 headless map cases PASS** (graphics intentionally skipped),14:33:40–50UTC;
**1/1 native graphics PASS**,14:34:20–28UTC2026-11-05, Intel Iris Xe/DX11; superseded
by the later6-case/native scale-iteration results above. All208 bole
rays, decreasing LODs, own data/wall blockage, all saved actors/real floor support,
new3/5/6 spawns + warning/arrival/no runtime bake.142 published generated files+scene/
meta byte-match worker. Native captures use actual animated actors/pipeline, no HUD;
overview alone temporarily fog-off. No full-speed traversal/FPS/WebGL build claim.
Full prior Titan222/224 suite NOT rerun for art/map-only work; same protected Run
failures historical. No runtime combat sources/approved character art/clips changed.
Owned full-project worker: AppData/Local/Temp/ODM-GiantForest2-iwrxdlei; helper
**tools/build_giant_forest_map.py**, Editor builder **GiantForest2Builder.cs**.
At publication the live Editor already had GiantForest2 open (title not dirty);
saved-source hashes matched. Published tested files ON DISK; did not stop Play,
reload/switch/save live scenes or send input/physics. User must reopen when ready;
preserve any later unsaved work as copy before external reload. Live Blender untouched.
No impostors/wind/baked occlusion/lightmaps/UV2/performance certification; shared URP
quality/shadow distance/global Physics unchanged. Published origin **3e9a738d**;
forest follow-up local commit explicitly authorized after the scale iteration.
No new push authorized. Caches left untouched.

## Previous: authorized Scout bite commit and branch push

After reviewing the completed follow-up, user requested **"commit and push this"**.
Commit the bite presentation/runtime/tests/docs/preview/helper and tracked C# source
references; push `titan-combat-updates` normally to origin. This also publishes prior
local checkpoint12901b9c (runtime spawning/arrival effects/gravity/jump/user-saved scene),
since verified remote is stillb6447972. Exclude .vs/, .plastic/ databases and Python
bytecode; leave them untouched. No new gameplay changes or validation claims in this
Git-only step. Use `git log -1` and origin branch SHA to verify completed publication.
This authorization supersedes earlier local-only/uncommitted/no-push status notes.

## Scout feeding / chomp ragdoll / blood

User requested going INTO mouth, ragdoll at chomp and blood. Implemented real-player
side-on same-grip feed/insertion, head-relative bite aperture, pre-release pose capture,
independent exact-pose Scout corpse13 bodies/12 joints, accepted-contact blood via
existing bounded sword FX pool. **docs/titan-scout-bite-presentation.md** full contract.
PlayerHealth sole HP, default100 bite at existing closure. Nonlethal blood/no corpse;
escape/block/expired lease no effects. No new capture size/timing/homing/parenting.

Scout PlayerHealth auto adds one passive runtime ScoutPlayerRagdoll; no install/scene
save required. Captures BEFORE lethal release, hides old visuals/colliders/animation,
world-independent physics clone so ReleasePending can't drag/reanimate body. Body self/
catching-Titan collision pairs ignored locally; world/others still collide. Birth full
scenery clearance rejects unsafe overlapping corpse, no teleport. Native current-pose
RB/joint initialization fixed10m inactive-RB-origin anchor issue. Initial inherited
input<=12m/s, outward2.5, angular fold1.5rad/s (editable), no post-sim velocity clipping.
Corpse8smax lifetime default, existing3s respawn; legacy null Animator/rootRB guards.

Real 3x/5x carry waypoint now scales once; real feed rotates vertical foot-catch offset
inward/down around same original grip, palm above/outside lips. Head-relative CLOSED
aperture avoids following opening lower-jaw socket DOWN into chest; .3 local insertion
through bite warning, sphere .55 unchanged. Labs retain old waypoint/yaw/jaw behavior.
No art/rig/clip/mass/nav/global Physics/camera settings/motor tuning or saved scene edits.

**222/224 passed TWICE**,2026-11-05 **03:20:07–27UTC** PID5752/20.435s and
**03:22:24–45UTC** PID14044/20.625s. ONLY same2 Run fails.13 additional cases all pass;
4 3x/5x side cases mouth/carry penetration0 (<.005), all-phase arm max6.581deg/120Hz
(<8),same grip coordinate/radius<.003;corpse1.5s max17.55m/s (<40),anchors<.002.
Clearance uses existing .9s-through-bite window; initial reach/takeover overlaps remain
separately logged ~1.97–3.08m (NOT claimed fixed or threshold relaxed). Protected fast
reach not redesigned. Full scaled ground/ordinary ODM still needs separate review.

Graphics OWNED runtime3x aerial fixture **1/1**,**03:22:09–10UTC**1.106s, actual skin/
teeth/physics/blood red-pixel assertion. `tools/capture_scout_bite.py`; ArtSource/Titan/
Combat/Scout_Bite_Chomp_Preview.png (+3 raw800px frames), Library/ScoutBiteReview raw.
Not ordinary map gameplay footage/final aesthetic approval. No live Play/scene/input
control; main scenes/prefabs/art/nav/settings byte-unchanged from12901b9c. No new audio,
player dismemberment or swallowing/deformation claim. Blood respects existing feedback
material/bloodEnabled. Implementation followed12901b9c; user subsequently authorized
its commit AND normal branch push (including12901b9c), as recorded above. Leave
IDE/Plastic caches alone.

## Previous: user-authorized checkpoint of all current project work

User requested committing everything currently ready, after runtime registration,
random population/arrival effects and gravity/customizable jumping. This commit bundles
those systems/tests/materials/preview tools/docs plus current user-SAVED FreeRoam edits
and tracked C# project source references. **Commit only; not pushed** (no push requested).
Use `git log -1` for this checkpoint SHA. Earlier local/uncommitted/not-installed notes
below describe their respective development milestones, not this checkpoint's status.

Excluded ONLY machine-local `.vs/`, Python `tools/__pycache__/` and changing `.plastic/`
workspace databases; left them untouched/uncommitted. No blanket revert, IDE metadata
cleanup, Unity stop/reload/save or forced input. Existing saved FreeRoam includes user
installed spawner/initialization settings and gravity16/jump4/.1/.15; also user-changed
some Titan scale values to6 and bladeRadius.2. Preserve those deliberately rather than
silently reverting to fixture values. Navigation assets still only3x/5x:6x requires
one explicit editor bake before that size can initialize. Runtime/physics fixture
passes do NOT assert these saved scene tuning/clearance choices are gameplay-approved.

Latest full owned validation209/211 (same2 known Run failures),2026-11-05
02:03:56–02:04:15UTC,PID39640.8 new jump cases pass. Last graphics strike render1/1
at2026-11-04 21:44:29–30UTC; audio user slot optional/unassigned. Never weaken strict
physics thresholds or regenerate approved Run. This is a snapshot, no new gameplay
feature change during commit. See docs/player-jumping.md,player-gravity.md,
titan-random-spawning.md,titan-runtime-initialization.md for details and evidence.

## Previous: customizable ballistic player jumping

User requested fixing/customizing jumping; **docs/player-jumping.md**. Movement.cs
replaces55/120-render-frame IEnumerator lift with physics-time ballistic launch
sqrt(2*g*height), Inspector **Jump Height4m / Jump Delay.1s / Jump Cooldown.15s**.
C remains the key, existing Gravity Constant now applies during ascent AND descent.
Requested height independent of gravity at launch; stronger gravity yields quicker
same-height jump.0g/invalid height rejects finite-height jump, no double jump/buffer/
coyote or changed hook/gas/boost/motor/camera/animation authority.

One pending/active jump, native/short scene-local nontrigger/self-filtered support
and recheck at actual takeoff. Walking off during delay,gas ResetGravity,hold,disable,
death cancel without delayed coroutine/phantom launch. Ceiling stops rise; native
landing clears state. Analytical ascent splits at apex, preserving real UP sweep even
if high gravity puts rise/fall in one tick. New timing/cooldown private state; read-only
JumpLaunchSpeed/JumpPending, existing GravityVerticalSpeed includes jump ascent.

**209/211 pass**,2026-11-05 **02:03:56–02:04:15UTC**,PID39640,19.1112139s, SAME2
Run failures only.8 additional native CCT cases + adapted obsolete legacy-jump regression
all pass, strict Titan/gravity/runtime/spawner checks retained. Initial208/211 had new
fixture error (unsettled capsule spawned within skin envelope); settled FROM ABOVE,
kept original .02m landing/.035m apex tolerances. No threshold weakening.

User saved FreeRoam gravity16/height4/delay.1/cooldown.15 during review (old25000 warning
historical). Left user scene/formatting/settings intact; no main Editor input/Play
stop/save/reload, no scene/prefab/art/nav/settings modification by this task. Source
Movement/ODM/test byte-match completed owned run. Previous dirty runtime/spawner work
preserved. No commit/push afterb6447972. Tests not full-speed ODM feel approval.

## Previous: functional player gravity Inspector setting

User requested a gravity constant that really affects falling. **docs/player-gravity.md**.
Movement.cs now reads Gravity Constant (m/s², positive downward; default9.8) for fall and
upward release braking, replaces unused field/hardcoded9.8 and removes sticky hidden
post-jump1.5x multiplier. Exposes existing78.4 fall-speed limit as Maximum Fall Speed.
Native landing clears retained fall speed; head contact cancels upward release. Existing
powered gas resets both old vertical components so no stale release-up fighting thrust;
ground helper preserves real upward throws. ODM foot probe only zeroes falling on real
CCT support, not early proximity. Existing scripted jump lift/55/.1s/C and hook/gas/
boost/inertia/visuals unchanged; no ballistic jump rewrite. Removes gravity log spam.

Preserved user's Movement formatting and dirty FreeRoam.scene; user's saved gravity
value is25000 (warned: reaches terminal on first tick; recommend9.8 or20–30). No main
Editor scene/Play/input/save changes or global Physics edits. All earlier uncommitted
runtime/spawner work remains. Still no new commit/push afterb6447972.

**201/203 pass**,2026-11-04 **22:31:17–22:31:36UTC**,PID52448,18.2195347s.8 new
real CCT gravity cases pass (live acceleration,zero/coast/terminal/invalid input,landing/
ledge,upward release/gas,hold/disabled,multiplier,ceiling); ONLY same2 Run failures.
Changed3 code/test files byte-match completed disposable validation. No thresholds
weakened/approved Run regeneration. Controlled motor/physics tests, not full ODM feel.

## Previous: random Titan population / configurable sizes / arrival strike

User requested random map spawning, editable random sizes, Titan-strike particle effect
and a sound slot (user will provide audio). Implemented scene-opt-in `TitanRandomSpawner`,
`TitanSpawnSurfaceSampler`, `TitanSpawnStrikeEffect`, additive shader/material and explicit
installer/Inspector. **docs/titan-random-spawning.md** contains full defaults/API/limits.
New work AND preceding initializer remain uncommitted after pushedb6447972; no new push.

- Weighted discrete sizes default3x/5x; no arbitrary unbaked scale borrowing/runtime bake.
  Spawner choices automatically join next editor bake-size union (existing types retained).
- Area-weighted cached loaded walkable-triangle proposals, final profile-filtered,
  horizontal player exclusion80m, optional max range/world bounds, spacing15m.24 bounded
  attempts/request,2s failure backoff. Initial4 after2s, then8–16s randomized intervals,
  one warning at a time. Cap6 on component/counts placed by default; installer cap is
  at least active placed+4 so current placed population doesn't suppress all arrivals.
- Manager TryPrepareSpawn inactive full configured rig + conservative full-arm AABB
  reservation; TryActivatePreparedSpawn rechecks real solids/NavMesh. Existing TrySpawn
  composes same path. Player approaches/new blocker/cap ->cancel, no strike/sound/fight.
- Faint golden sky bolts -> confirmed bright lightning/local point flash/sparks/dust/
  rising steam. No collider/force/fullscreen effects/global light/fog/time/input changes.
  Bounded4-slot effect/audio pool (max8), no global RNG/material leaks. Optional3D
  Spawn Sound field EMPTY awaiting user clip, volume/max-distance configurable.
- Own-only dead rig cleanup25s/max6 corpses and replenishment; never deletes placed
  Titans/corpses. Component disable cancels warning and pauses (keeps live NPCs), destroying
  spawner cleans its own actors by default. No repair/pooling of severed bodies.

**193/195 passed**,2026-11-04 **21:43:58–21:44:18 UTC**,PID3784,19.584s;
previous193/19521:40:22–42PID17444. SAME2 Run failures only;12 new cases all pass,
all prior strict Titan tests retained. Separate graphics-enabled OWNED runtime URP
capture **1/1**,21:44:29–30UTC,.998s, pixel-tested gold lightning ABOVE Titan. Preview
ArtSource/Titan/Spawning/TitanSpawn_Strike_Preview.png +3 frames, raw Library/
TitanSpawnerReview. Controlledfixture, NOT ordinary map traversal/crowd proof or final
art/audio approval. User audio clip not assigned/aurally reviewed.

**NOT installed into main scene yet**: Unity MCP offline/no connected instance, no
forced focus/key input, scene save/reload or YAML overwrite. After compile user chooses
Tools > Titans > FreeRoam > Install random Titan spawner in Edit Mode, then saves scene.
Installer creates Save-AS-COPY before/after backups, wires prefab/materials/manager,
refuses duplicates and preserves existing actors/nav assets/activation. Select new
Titan random spawner object for fields/live buttons/error counts. No production bake.
New sizes require ONE edit-mode bake; existing3x/5x immediately usable after install.

## Previous: pushed checkpoint + reusable runtime Titan initialization

User authorized commit/push THEN move to spawn-ready initialization. Completed
**b6447972236dca8a2e34d5cf7eec733078420406**, pushed and remote-confirmed on
origin/titan-combat-updates.75 paths: attacks/health/effects plus user's saved scene,
extra placed Titans, enlarged nape/eyes prefab triggers and sight tuning. Prior4
local checkpoints through059dd52e pushed too. Locked.vs cache/Python bytecode excluded.
New runtime work AFTER this checkpoint is UNCOMMITTED; no second commit requested.

**docs/titan-runtime-initialization.md**: rewritten TitanFreeRoamIntegration opt-in
scene registry, body neutral BecameActive/Inactive hooks, TitanRuntimeActor single-owner
stack, TitanSpawnPlacement bounded real-geometry query. Auto-discover placed actors once
and queue enabled/new bodies for next Update; no recurring full-world scan or fixed-list
requirement. Respect inactive scene actors; old explicit list also works auto-discovery
OFF. Full player/perception/nav/stomp/grab/bite/director setup with regional health,
grapple layer9, no duplicate components; no reset/heal/repose on duplicate registration.
TrySpawn(prefab,pos,yawRotation,scale,out body,out error) configures inactive in owning
scene then activates; Despawn releases/cancels/unregisters owned actor only. Reject
foreign/external-owner/unsupported/nonuniform/nonfinite/tilted/blocked/offmesh; old5x tree
spawn still must move onto clear ground. No distant fallback. Current production sizes
remain3x/5x:1x/arbitrary size requires a new profile ONCE, not per actor. Additional Bake
Scales + scoped builder unions future sizes/existing profiles/discovered+listed actors,
so no placeholder needed. No actual production bake/settings/scene save performed.
Actor/manager/nav disable suspends/re-enable validates/resumes, no corpse resurrection
or reconstruction of severed rigs; full corpse pooling not implemented. Combat toggle
can cancel/resume live. Timed/wave spawner itself remains separate.

**181/183 passed TWICE**,2026-11-04 **15:27:52–15:28:09 UTC**,PID19600,16.665s,
then **15:30:27–44 UTC**,PID47984,16.912s. ONLY same2 retimed Run failures, no weakened
checks.14 new cases cover late/placed/explicit/inactive/cross-scene ownership, profile
scale-once/shared unchanged data, safe spawn/despawn/crowd/full-arm scenery, HP retained,
combat/actor/manager/nav suspend/resume, future bake sizes, scale mutation rejection,
spawned director's actual40 damage stomp, physical grab/lease released on despawn and
dead no-resurrection. Final178-file byte match; saved scenes/prefabs/art/nav/settings
unchanged from pushed checkpoint. Main Editor Play/input untouched this step; runtime
validation only in disposable owned project, not preview scenes. New code takes effect
after normal Unity compilation/new Play session. No natural full-speed scaled-combat or
user aesthetic approval claim. New cs/meta files present; no forced live scene save.

## Previous: regional sword damage / impact effects / severed-limb launch

**Latest:** user approved nape health/speed-dependent killing, requested HP legs2500,
arms1000, face500, nape500; blood, outcome-colored numbers, shake, slash/hit feedback,
and a real sever kick instead of merely releasing a limb. Implemented scene-only
FreeRoam opt-in; shared prefabs/labs retain legacy health fixture. No clip rebuild,
root/motor/global Physics/time-scale changes, commit or push. Full details:
**docs/titan-sword-damage-effects.md**.

- Body useRegionalHealth=true on both existing scene Titans (inactive included),
  per-side limbs; face shares original eye region, blindness only at zero,5s then
  restores500. Nape accumulates500 before death. ReceiveDamage receipt + old wrapper.
- Dealer scene base250 +35*max(0,min(actualSpeed,80)-15): <=15:250,30:775,80:2525.
  Root FixedUpdate sampling,2 continuous samples, invalid/teleport>160/held/carried/
  release-pending/dead suppression. No sword-animation speed damage amplification.
- Actual cutting mesh sweeps remain authoritative (.035/.025/max256/fail-closed),
  now contact point/slash direction passed through shared per-region dedup/event.
  New profile ignores legacy hilt/gear native trigger damage when sweep active.
- White ordinary damage, yellow actual sever/blind, red killing nape. Screen-anchored
  outlined rising numbers (raw damage incl overkill), marker, render-only projection
  shake with exact restore/live automatic FOV, cut-only tip trails, directional
  blood bursts and.18s moving cut-branch spray.8x96 particle pool/32 numbers, toggles.
  Original procedural provisional SFX; existing slash audio takes precedence.
- Whole detached branch coherent velocity impulse7m/s along cut +.65 up, spin<=.8
  reduced by world radius to<=2m/s orbital contribution. No velocity clipping,
  preserved pose/solver12/4/angular cap4/masses/limits/global gravity.

**167/169 passed**,2026-11-04 **03:51:08–27 UTC**,PID46464,19.099s; previous167/169
03:47:22–39,PID50212. Only SAME2 retimed Run failures.12 new checks cover health,
face recovery, scaling/held/teleport, dedup/colors, pool/trail/asset refs/cleanup,
render-only shake/FOV,12 branch/scale combinations. Sever one-second max11.16–11.38
m/s (<40 unchanged), COM launch~(6.99,.57,0), no pose snap. All strict prior tests pass.
Owned disposable runtime only; no Editor-preview physics. New runtime/test/art bytes
must remain matched to latest validation; audio/material copying added to runner.

Actual F + actual Scout blade sweeps in controlled stationary sky fixture: arm
1000->750->500->250->0, one receipt per swing, white then yellow, blood/trail/GUI.
Preview ArtSource/Titan/Combat/Scout_SwordImpact_ConfirmedHits.png, raw Library/
TitanImpactReview/LeftArm_1..4. This is NOT full-speed normal ODM combat approval.
Native nape/face capture not completed (foreground safety guard refused); their
health/colors are automated evidence. New review helper refuses existing Play and
stops only its own session. User subsequently began normal gameplay Play; leave it
alone. Main setup was intentionally NOT saved over live Play: **after user stops,
save FreeRoam** to persist feedback component/dealer config/two body profile flags.
Before/after COPY snapshots differ only these intended settings; no transforms or
activation changes. Code/material/audio/shader saved. No fixture scene saves.
Installer menu Tools > Titans > FreeRoam > Install sword health and impact effects.
Uncommitted work remains after approved059dd52e. User aesthetic/volume/shake/real
high-speed/scaled combat review remains; no claims of final approval.

## Previous: Scout player / dual swords / full-body animations first implementation

**LATEST: directional F-held wind-ups — both arms load opposite the slash.**
User rejected nearly symmetric original chamber: both starting arms must differ too.
Base variant BOTH hands left -> swing right; `_Right` mirror BOTH hands right -> swing
left. Historical `_Right`/attackSide means LOAD side. Early arc retains this side;
charge and slash first.065s use exactly the same pose. Near elbow125deg/opposite
cross-body60deg reach (not both125, which stranded one hand on each side). Updated
fold assertions for intentional geometry, added stronger both-hand opposition tests.
Fixed auto-quaternion tangent anticipation drift instead of relaxing matching check.
Random selection/timings/input/root/motor/IK policy unchanged. Only combat clips baked;
noncombat clips/controller unchanged. No new commit; checkpoint remains059dd52e.

**155/157 passed,same2 Run failures**,2026-11-04 02:29:24–40 UTC,PID33772,16.721s.
New3-context tests: BOTH hands opposite finish, identical F-hold/attack0/.03/.06,
early arc retains load, fully extended finish goes other side. All previous tests
retained, strict Titan thresholds unchanged. Runtime/tests/all clips/controller
byte-match. Native actual F-hold right handXs+.4555/+.4175, left-.4175/-.4555m, release
matches side, damage active/time synced; IK0/root0. Controlled sky NOT full ODM proof.
Updated review_scout_attack_input.py captures/asserts BOTH charge and cut. Previews
Scout_Directional_Windup_Unity.png, Scout_Slash_ToLeft/ToRight.gif, updated mirrored
sheet. Old Left/Right GIFs historical load-named first pass. Scene clean/unpaused Edit
Mode and before/after copies identical. New work still uncommitted/no push.

**Previous: checkpoint approved Scout, then mirrored random two-arm arc attacks.**
User authorized commit of current approved art/run/grapple/fall. **059dd52e** —
Checkpoint approved Scout player, grapple poses and smooth falling.122 paths, local
only/no push; locked.vsidx excluded, no oversized/sensitive-name flags. All new attack
work AFTER059dd52e intentionally uncommitted for review, no further commit authorized.

New attacks Ground/Air/Held: both hands chamber behind neck/back(~125deg elbow curl),
C1 arcing cut back->overhead->front->side, both arms fully extended at.185s through.39s
finish. Whole-body sagittal mirror (6 new `_Right` charge/attack clips; controller28).
`Attack.ChargeStarted` chooses50/50 cosmetic System.Random side once per F press,
cache persists through hold/release/context swaps. No reroll/restart on release, no
Unity-global RNG disturbance. Physical.075/.12/.195=.39 timing, F controls, sweep/dedup,
held counter and IK priorities unchanged. Scoped RebuildAttacks/menu regenerates ONLY
12 combat clips; Run/Fall/grapple/releases/idle/land hashes unchanged. Root/camera/motor/
scene prefab/model/material unchanged. No VFX/combo/buffering.

**152/154 passed,same2 known retimed-Run failures**,2026-11-04 02:10:30–47 UTC,
PID14608,16.607s.5 new cases: actual curve/chamber/extension in3 contexts, whole-body
mirror, stable seeded selection/global RNG; existing event/timing check expanded.
Runtime/test/ALL clips/controller byte-match completed owned disposable project.
Actual F input BOTH sides verified native AttackAir/AttackAir_Right at age.13447/.14564,
normalizedTime*.39 matches age, same charged/release side, damage active, hand IK0,
root/pivot0. Controlled motor-suspended sky fixture, NOT full ODM combat/terrain proof.
Preview ArtSource/Player/Scout/Scout_Mirrored_Attack_Unity.png, Scout_Slash_Left.gif,
Scout_Slash_Right.gif (authored skin-bakes with review pauses, not gameplay video).
Tools capture_scout_attacks.py / review_scout_attack_input.py updated for both sides.
Scene CLEAN unpaused Edit Mode, before/after scene copies identical, no scene save/
reload. Approved checkpoint059dd52e + new attack dirty work. See implementation doc.

**Previous: smooth Fall entry + Inspector duration / animation speed instructions.**
User wanted gradual previous-animation->Fall, asked where animation speed is changed.
Fall now native **.5s fixed-time crossfade**, not.075; new `fallBlendDuration` Inspector
field on **Player > Scout Player Animation > Animation blending > Fall Blend Duration**,
higher seconds=slower (min.05). Runtime GetPoseTransitionDuration respects custom value.
Actual clip speed: open Assets/Art/Player/Scout/Animations/ScoutPlayer.controller, select
state, Inspector Speed (.5 half,2 double). Fall held pose -> use blend duration for entry;
global Animator.speed overwritten in Update; run cadence/attack-hit timing need care.
No movement/input/physics/priority/dual-entry/release changes; no animation assets rebuilt.

Final **147/149 passed,same2 Run timing failures**,2026-11-04 01:46:00–17 UTC,
PID45036,16.833s. New Inspector/default/custom/min + native8-source Fall crossfade checks
pass, other urgent transition timings unchanged. Runtime/test byte-match; animation
folder all hashes unchanged. Native controlled review smoothly raises hands from~.4m
to~2.09m over~.5s, root/pivot0. NOT normal-input traversal proof.27-frame preview
ArtSource/Player/Scout/Scout_Fall_Smooth_Transition.gif; review_scout_fall_transition.py.
Scene remains clean Edit Mode, no main save/reload. Before/after COPY snapshots only
add new default fallBlendDuration:.5 field, existing scene data unchanged. No commit/push.

**Previous: Fall pose — overhead lightly crossed swords, look down.**
User requested both swords directly above, slightly crossed; character looking down.
ONLY Fall.anim regenerated via new **Rebuild fall pose only** menu/RebuildFall(). Arms
raised/inward, wrist roll, slight depth offset; actual blade axes19.56deg from up,
tips cross near ends without steel interpenetration. Neck/head gaze~38deg down.
Torso/legs/cape retained. Runtime state/IK/motor/controls/root untouched. All OTHER
animation/controller/meta files SHA256 byte-unchanged incl run/dual flight/releases.

Final **145/147 passed, SAME2 known Run timing failures**,2026-11-04 01:36:15–32 UTC,
PID40012,17.191s. New actual-blade/fall pose test passes across loop: directions up,
blades above head, projected near-tip crossing with depth gap, gaze down/root isolation.
Initial nearly-meeting tips corrected rather than weakening test. Native static front/
profile proof ArtSource/Player/Scout/Scout_Fall_Overhead_Unity.png; capture_scout_fall.py.
No physics stepping; not actual-input traversal proof. Scene NOW CLEAN at task entry
(user saved since prior turn), clean unpaused Edit Mode retained; before/after snapshot
copies identical. No scene save/reload, no commit/push. See implementation doc.

**Previous: smooth, less-flat dual-hook recline with live independent arm aim.**
User liked laying down but requested slower upright->feet-first entry, less recline,
normal anchor-pointing arms. Enter dual flight using **.75s native Animator fade**.
Gas/coast pose swaps wait for entry to settle, then use.20s adjustment; controls/thrust/
hooks/late hand IK are NOT delayed. End hips-62/-56gas/coast, torso~22/28deg above
horizontal. Both legs still lead, head looks ahead. Other transitions/dual release/
backflip unchanged. Scoped only2 clips. Run, dual releases, single-hook pose hashes
unchanged. Before/after dirty-scene snapshot copies identical; remains dirty Edit Mode.

Final **144/146 passed, SAME2 Run failures**,2026-11-04 01:13:22–39 UTC,PID28860,
17.349s. All new tests pass incl actual Animator20ms-step entry/no snap, gas-toggle
settle + independent hand aim<1deg across blend, end-pose/root isolation. No tests
weakened to hide user-retimed Run1.1667s vs old.56 timing mismatch. Changed runtime/
clip/test bytes match disposable run. Native timed controlled pose review: progressive
~21/32/39/53/68deg torso from upright, anchor aim~0 afterinitial.15s, root/pivot0;
gas off midway continues entry then coasts to62deg. NOT normal-input gameplay proof.
27-frame previews ArtSource/Player/Scout/Scout_DualHook_Smooth_Recline.gif and
Scout_DualHook_Smooth_Recline_GasToggle.gif, source tools/review_scout_recline.py.
Full implementation doc updated. No save/reload of user scene; no commit/push.

**Previous: user accepted run; requested no single-hook release + feet-first dual flight.**
Single final left/right hook detach now goes straight to persistent Fall (no recoil),
until regrapple/landing/attack/hold/death. Partial dual detach keeps remaining hook pose;
near-simultaneous/full dual release path unchanged. GasBoth/AttachedBoth recline nearly
supine, both legs extended feet-leading; independent anchor hand IK retained. Root/camera/
controller/motor unchanged. Scoped **Rebuild dual-hook flight poses only** menu updates
ONLY those2 clips and removes ReleaseLeft/Right states (legacy clips unreferenced).
Run/ReleaseBoth/ReleaseFlip/single-hook gas/coast clips SHA256 BYTE-UNCHANGED.

**IMPORTANT validation:142/144 passed,2 FAILED**,2026-11-04 00:40:11–28 UTC,PID38988,
16.499s. ALL changed-feature tests + dual flip/independent IK pass. Two existing Naruto
gait assertions fail against unchanged Run clip now1.1666666s (previous recipe.56s),
retimed since previous checkpoint. Do NOT overwrite approved/user run, weaken tests or
claim all green. Run timing/cadence reconciliation deferred for separate user direction.
Changed runtime/clip/controller/test files byte-match disposable project.

Scene was DIRTY BEFORE TASK. Preserved UNSAVED edits, no reload/normal save. Before/
after save-AS-COPY backups Library/ScoutBackup/FreeRoam.before/after-feet-first.unity
are byte-identical1985763 bytes; Editor still dirty, unpaused Edit Mode. Native controlled
sky review: GasBoth/AttachedBoth feet2.76/2.68m ahead of head, independent hand weights~1,
root/pivot angle0; final single-hook release Fall. NOT normal-input full ODM proof.
Preview ArtSource/Player/Scout/Scout_DualHook_FeetFirst_Unity.png. Source/new tests/
scoped builder + review helper in implementation doc. All new work still uncommitted.

**Previous accepted: stronger lower-body power / rear extension per further user feedback.**
Previous gait was still judged tucked/weak despite numerical tests. Revised support to
30% per leg, half-cycle offset, real flight/no double support. Asymmetric front .22m,
full rear reach~.66m; nearly extended trailing leg and~128deg sole pitch so rear sole
plane nearly parallels body. Toe/heel-pivot compensation prevents sliding during roll.
Hip loading/push/flight, then heel recovery/forward reach, not a faster bicycle shuffle.
.86m contact stroke/.56s cycle; normal real W test: speed15.00001,Run/grounded,hooksfalse,
Animator1.465117. Motor/controls unchanged. **141/141 passed twice**,2026-11-04
00:08:00–15 UTC,PID10548,15.853s;00:09:54–00:10:11 UTC,PID13532,16.221s.71 code/art/
clip/prefab files byte-matched. Saved clean unpaused Edit Mode; original root(0,0,0),
capsule3.6 unchanged. +2 strict rear-extension/sole-plane and120-phase contact/flight
regressions. Old gait assertions adapted to actual planted toe rather than flat ankle;
all Titan/ODM/attack limits unchanged. Preview Scout_NarutoRun + Scout_PowerRun_Rear
GIF/PNG in ArtSource/Player/Scout. Owned render translation only, not gameplay video.
See implementation doc. Another review iteration, not assumed visual approval.

**Previous follow-up: user requested natural Naruto-style ground running.** Rebuilt Run:
~38-degree torso lean, head looking ahead, nearly straight arms/blades trailing, subtle
shoulder/cape motion. Actual stance/recovery foot path + two-link leg solve instead of
independent thigh/knee sinusoids; heel settle/push-off/loop continuity. Cadence matches
sampled planted-foot travel and model scale, NOT arbitrary speed/9. No motor tuning or
controls changed. Normal real W input in FreeRoam: Run/groundedtrue,15.00001m/s,
Animator2.205,hooksoff. No motor-disabling fixture for that check. Two new regressions,
**139/139 passed twice**,2026-11-03 21:44:25–41 UTC,PID34668,16.059s; and21:46:11–28
UTC,PID23208,16.890s. Final gait runtime/clip/test/prefab byte-matched; all137 retained.
Preview ArtSource/Player/Scout/Scout_NarutoRun.gif/png (temporary preview translation,
NOT gameplay-root motion or terrain approval). Clean unpaused Edit Mode, original root
position retained; new work still uncommitted. Full details in implementation document.

Pre-overhaul full checkpoint **16517e6c** (includes previously excluded user changes,
except locked .vsidx cache), after navigationc07d4295. NEW player work UNCOMMITTED for
review; no new push. Implemented/saved ONLY `Assets/Scenes/FreeRoam.unity`; original
shared Player prefab, Shiganshina and all approved Titan art/rig/physics/nav untouched.
See **docs/scout-player-implementation.md** and original approved plan.

Blender owned ScoutPlayer_v1 collection: **11520 triangles,43 bones**, tailored Scout
uniform/harness/green cape/ODM gear, separate grip-centered pistol-grip metal swords,
one1024 PBR atlas (baseColor/metallicSmoothness/normal), explicit existing URP/Lit.
Source packed `.blend`/previews in ArtSource/Player/Scout; FBX/material/textures in
Assets/Art/Player/Scout. Native Generic rig (not Humanoid/mocap/human ragdoll).
`ScoutVisual.prefab` passive visuals, **24 Unity-authored clips** recreated by
`Assets/Editor/ScoutPlayerBuilder.cs` from imported rig/rest axes. Blender contains
model/rest rig, not animation takes. Animation asset constant-channel compaction:
225575 ->22090 keys,21 samples/binding, max channel delta5.69e-8, folder113MB ->16MB.

`ScoutAnimationBrain` presentation-only hook/gas/air/ground/attack/hold/death priority;
`ScoutPlayerAnimation` owns new Animator and bounded independent late hand IK. Left/
right/dual gas/coast, run with trailing swords, raised-blade fall, separate left/right/
dual releases, interrupted .48s cosmetic dual-release flip with3.6m envelope endpoint+
travel clearance. Camera/controller/velocity NOT flipped. Existing motors get only
explicit optional Animator target + read-only hook/anchor/gas getters. Hip launcher
markers replace rope emitter positions ONLY; camera raycasts/input/forces unchanged.

Full-body ground/air/held charge + attack: hips/chest twist, active legs and follow-
through. F hold/release unchanged. Opt-in scene timing **.075 anticipation/.12 cut/
.195 recovery=.39s**, old default **0/.6/0** retained elsewhere. Physical new blade
sweeps still shared DamageDealer deduplication; no timer-only damage/new pool/parenting.
Hold OR pending-release motor suspension yields pose/IK/flip but allows held counter.

**137/137 passed twice** final runtime + compacted assets, Unity2021.3.25f1:
- **2026-11-03 21:17:40–56 UTC**, PID27388,16.739s.
- **21:19:50–21:20:08 UTC**, PID34396,17.463s.
All123 earlier retained +14 new tests (state/interruptions, actual skin/leg motion and
root isolation, independent generic IK, distinct release clips, blade parenting,
actual damage timing/defaults/disable, swept flip envelope). No weakened Titan limits.
Live native unpaused controlled sky fixture: correct left/right/dual IK weights~1,
AttachedBoth when stopping gas; flip pivot rotates while root angle0. ACTUAL Windows
F press/release in focused Game view: age.156894s =normalized.402293*.39; damage active,
root angle0. Direct coroutine calls from MCP outside PlayerLoop defer Animator entry;
not valid timing proof. Real-key proof matches. This is NOT new normal-ODM traversal
or comprehensive terrain/crowd/clipping approval. Native motor regressions still pass.

Menu restore/reinstall + scene RELOAD verified: legacy CharacterAnimated/.6s/two old
blades restored, then Scout modelscale2/offsetY-1.8/new emitters/.12 cut persisted;
zero hidden legacy renderers enabled; CCT3.6/.5, speed15, acceleration50/max80 unchanged.
Backup Library/ScoutBackup/FreeRoam.unity. Tools > Player > Scout > Build/Install/
Restore legacy visuals. Original model retained hidden; solid gear colliders retained.

Evidence ArtSource/Player/Scout/Scout_Unity_Overview.png, Scout_AerialAttack.gif
(authored clip preview, not gameplay video), Scout_Front/Back.png. All temporary
camera/RT/render settings/runtime placements cleaned. Editor clean unpaused Edit Mode
in FreeRoam. Temporary reconnect Editor helper removed. Model/face/shoulder/cloth/
foot-plant/fast-flight animation polish, lateral bank integration, full-body clearance,
human ragdoll and VFX still need review; do not call first pass final/fully collision-free.

## Previous: full checkpoint before Scout/player/sword animation overhaul

User approved the Scout character/dual-sword + authored-clips/procedural-hook-IK plan.
Confirmed right hook uses RIGHT hand; falling holds BOTH swords overhead. Added explicit
FULL-BODY sword attack remake: current attack is slow/weak and legs static; needs hip/
shoulder rotation, grounded footwork or aerial leg counterbalance, sharp cut and follow-
through, convincing before particles/VFX. Preserve existing ODM controls/physics feel.
Details and interruption/IK contracts: `docs/scout-player-overhaul-plan.md`.

User now explicitly authorized committing ALL remaining changes as a checkpoint,
including previously excluded source/assets/model moves/material/local metadata. This
supersedes earlier scoped exclusions for this checkpoint only; do not rewrite or fix
user experiments while checkpointing. No push requested. Existing tested navigation
checkpoint is **c07d4295**, after integration0f371f2c. No new character/attack authored
or integrated yet; no new testing claim for previously unrelated experiments.

## Previous: full-map FreeRoam navigation TEST ready; integration checkpoint committed

**0f371f2c** committed the prior actual ODM integration + user's3x/5x FreeRoam Titans
and138m/180deg sight defaults. Local only, no new push requested. NEW navigation
experiment is now checkpointed at the user's request before switching to player/scout
model, swords and animation planning. No new push requested; unrelated dirty tree still
excluded. Navigation commit follows0f371f2c (see Git log). No scout modeling begun yet.

Active scene **Assets/Scenes/FreeRoam.unity**, saved setup object
**FreeRoam Titan navigation TEST**. Editor-only full-map bake,338 actual physical
sources (332 MeshColliders), no mesh import/readability edits, no fake floor.
Bounds~2122x908x1595m including full2km map. Two owned baked assets in
`Assets/Navigation/Titans/FreeRoam/`: world radius5.4/height36 for3x and9/60 for5x.
Two named authored agent types added; original Humanoid0 radius.5/height2 unchanged.
Stable IDs/GUIDs on re-bake verified, type count3 before/after. Canonical.001 scales.
Native type IDs may be NEGATIVE: only-1 is missing. Profiles(-1372625422,-334000983).

New core: `TitanNavigationGeometry` physical-source collection (actual TerrainCollider
fallback for local-physics runtime tests), `TitanWorldNavigation` owned asset registration.
New default-assembly `TitanFreeRoamIntegration`; explicit Editor menu builder
`TitanFreeRoamNavigationBuilder`. **Enable Combat=false** for nav testing; toggle in
Edit Mode for combat. Sight Range138/FOV180/Patrol Radius200 editable on setup Inspector.
Original player controls unchanged. Health/motor/sword bridge wired scene-only.
Shared prefabs/rest rig/art/Physics settings not edited. Existing user placements/scales
preserved; three-unit startup projection only, no giant teleports across scenery.

IMPORTANT: user's5x spawn(20,-1.84,25) intersects **mediumTree (193)**. Actual physics
capsule overlap confirms tree.5x nav has no point within3/5/10m; nearest sample
(35.76,-1.36,13.18),19.71m away. Setup correctly shows spawn error instead of shrinking
clearance or forcing movement. Move that Titan to clear ground, e.g.X36/Z13; no rebake
needed just for repositioning onto current data.3x starts on mesh and chases naturally.
5x relocation for validation was ONLY temporary Play Mode; original saved pose retained.

**123/123 passed twice**, Unity2021.3.25f1, all116 prior +7 nav tests:
- **2026-11-03 19:03:46–19:04:02 UTC**, PID21108,16.006s.
- **19:05:38–54 UTC**, PID26368,16.248s.
New checks: whole-map static physics source filtering, cross-scene fail-closed,
actual TerrainCollider,500m sloped-mesh path around cover,16m gap passes3x/rejects5x,
exactly-once scaled avoidance, owned registration/re-enable preserving external data.
All existing<8deg/120Hz,<40m/s,physical contact/torso assertions retained.

Live unpaused native Editor review: both data registered,20532 triangulation indices.
3x61/68 and5x60/67 sampled grid destinations complete, seven disconnected/blocked
refused. Longest native complete routes1409m/13 corners and1418m/14 corners, native
actors actually moved>1.3km (not just path queries). First long run exposed double-
scaled avoidance: world clearance belongs to BAKE, agent component dimensions follow
transform scale. `Profile.ConfigureAgent` sets radius~1.8/height~12 locally, not5.4/9
again; regression added. Final distinct-destination native run arrived:3x147.41m
movement,1.87m remaining within3m stopping distance;5x204.93m movement,0m remaining;
both on mesh/PathComplete/no pending/velocity0 after reloading rebaked assets.

Proof **ArtSource/Titan/Navigation/FreeRoam_FullMap_MCP.png**, real map with temporary
native route overlay (cyan3x/orange5x); overlay/camera/RT removed, fog restored.
Backup `Library/TitanFullMapBackup/`. Editor returned clean/unpaused Edit Mode in
FreeRoam with saved3x/5x transforms intact. Read `docs/titan-freeroam-navigation.md`
for menu/rebake/controls/limitations. Moving geometry/new roots/scale changes require
explicit saved-scene re-bake. Not yet scaled combat/ragdoll/terrain-foot-IK/multi-floor/
full crowd approval. Do not claim every disconnected island reachable.

## Previous request: commit integration checkpoint, then test full-map FreeRoam navigation

User moved/copied the two modular Titans into `Assets/Scenes/FreeRoam.unity`, scale3x
and5x, and tuned AI defaults to sightRange138/FOV180. Preserve these choices. User
requested committing current integration then full-map navigation as a TESTING pass.
Checkpoint includes the actual scene/player/FBX dependencies (four GUID-preserving
model moves + Taking Item used in Shiganshina); unrelated Plastic/IDE/material/
Ragdoll/unused model/prefab changes excluded. Sight fixture pins its original38/120
cone rather than assuming game defaults; assertions unchanged. Latest checkpoint
**116/116 passed**, Unity2021.3.25f1,2026-11-03 **16:42:26–41 UTC**, PID39268.

FreeRoam currently has no scene integration/nav surface/AI on the newly copied Titans;
403 colliders,332 environment MeshColliders (unreadable CPU meshes), no Unity Terrain
components. Real map environment bounds roughly2064x1538m, some trees704m tall.
New navigation work must include actual MeshColliders + boxes, preserve actor
placements/scales, and avoid runtime whole-map mesh bakes that require changing all
mesh import settings. Prefer owned Editor-baked data with clear per-size clearance.
No additional push requested for this checkpoint.

## Previous: approved work PUSHED; real ODM integrated into requested Shiganshina scene

User approved, requested commit/push, then asked to integrate into existing
`Assets/Scenes/FreeRoam (Shiganshina.unity` and remove its old Titans. Explained that
motor integration means narrow hold-only suspension/resume hooks, not replacing or
retuning the ODM gear. **940daf1d** (`Add fast physical aerial grabs and clear torso
during mouth carry`) committed/pushed to **origin/titan-combat-updates**; HEAD and
upstream match. Prior prototype commits now pushed too. No merge/main/force push.

NEW integration is a separate uncommitted review slice. Actual requested scene saved,
only its two old Titan/TitanV2 instances removed (source assets retained); two approved
modular Titans near spawn target the existing real player. Existing player/body
prefabs/art/rest rig unchanged. Original scene backed up in ignored Library and
owned external temp before any edits; unrelated dirty RagdollController/FreeRoam/model/
material/Plastic/IDE work preserved. New scene-only sword damage window assigned to
previously unassigned Attack.damageDealer. F remains charge/release sword, E hook,
T music; NO lab F flight / E escape / T combat shortcuts added.

New default-assembly adapters: `Assets/Titan/Integration/TitanODMMotor.cs`,
`TitanODMHealthSource.cs`, `TitanODMSwordSweep.cs`, `TitanShiganshinaIntegration.cs`.
Prototype optional `TitanHealthSource` + bounded compound `TitanReleaseClearance`.
Movement/ODM get explicit owner-token suspension that stops old jump/boost/inertia
coroutines/hooks, preserves normal tuning, restores safe current pose/momentum.
Real ground holds also use motor/current-pose clearance, lab ground unchanged.
Velocity sampled after real FixedUpdates. Health is ACTUAL PlayerHealth, no duplicate
HP. External death releases before ragdoll; death and attack-disable cleanup stop
motion/weapon windows. Player prefab has no assigned human ragdoll; no new one claimed.
Sword sweeps use actual two mesh poses during original .6s window, per-region/swing
dedupe, and remain active while held without enabling body colliders. Gear-box-only
bite contact is rejected. Real release checks CCT AND solid gear boxes.

CRITICAL native observation: Unity2021 CCT capsule axis stays WORLD-UP even when the
visible/root transform tilts; only configured local CENTER rotates. Verified actual
45deg root: bounds size(1,3.6,1), center(0,1,0) -> root+(-.70711,.70711,0). Do NOT
use transform.up for a native CCT capsule. CCD/closest-point/release now match this;
actual visual root still freezes at physical TOI, no upright snap. Lab retains its
upright-root gate; real adapter supports observed root/center rotation. Ordinary
CapsuleColliders/gear boxes follow their own orientation as normal.

Owned nav: explicit real floor/box collision sources, **256x256m patch around origin**,
not full-map production nav. Both agents on NavMesh/Chase;156 triangle indices.
Added exact scene to Build Settings (disk + live) for existing3s death/respawn;
Menu/FreeRoam entries preserved. `Assets/Editor/TitanShiganshinaInstaller.cs` is explicit,
undoable, refuses wrong/dirty/duplicate setup; no auto scene rebuilding.

**116/116 passed twice**, Unity2021.3.25f1:
- **2026-11-03 15:52:12–27 UTC**, PID40516,15.169s.
- **15:55:56–15:56:11 UTC**, PID34836,15.561s.
All108 approved tests +8 (native CCT bounds/rotating-center CCD, real motor suspend,
ground current release, full gear clearance, tilted-root native headroom, real
health/external death, release momentum, crossed sword/per-region dedup). Old strict
<8deg/120Hz and<40m/s retained: fast6.45deg,moving6.64deg; forearm/palm torso0;
aerial loaded death11.67m/s; intact10.86m/s,gap.003m,hipsY1.03m. Runner now compiles
actual default-assembly adapter + narrow legacy dependencies; no preview physics.

Live scene MCP, unpaused, controlled actual `ODM_Movement.Throttle/FaceGrapplePoint`
(not lab motor), native.02s: selected.54s after pass start, physical catch.18s later,
root tilt8.61deg, one new capture/one bite, ACTUAL and bridge HP0, no parenting,
hooks cleared, no pending release. Sampled close scene frames reviewed.
Proof: `ArtSource/Titan/Combat/Shiganshina_ODM_Integration_MCP.png`.
Separate native death/respawn confirmed scheduled reload -> changed scene handle,
HP100/bridge100, controller enabled, two Titans + nav rebuilt. Test replay canceled
Invoke only in its own screenshot wrapper; separate respawn test did NOT cancel.
Final Editor clean/unpaused Edit Mode, requested scene; temp camera/RT removed.

Next: USER interactive fast rope/boost/held sword feel review, full-map nav, full-body
carry/terrain clearance (currently grip sweep + whole-gear release gate), crowd/corpse
stress, human ragdoll/fingers/escape balance polish. Do not claim production animation
or all-terrain approval. Before committing new integration, audit scene/player/model
GUID dependency closure: these included preexisting UNTRACKED assets; do NOT stage
entire unrelated dirty tree. Source player prefab remains unchanged locally.
See `docs/titan-shiganshina-integration.md` for contracts, controls and limitations.

## Previous approved aerial mouth-carry torso clipping fix (now committed940daf1d)

User approved the fast catch (looks great), but reported the arm crossing the body
on the way to the mouth, and asked what comes next. MCP reproduced a backwards
elbow and straight inward hand path: forearm/chest penetration1.43m, palm1.32m.
Now blend the elbow forward/outward ONLY after contact, and lift in front on a
.9m-forward/.35m-up transfer arc before closing toward the mouth. Existing fast
0/.22 catch, .25/.65 miss retry, .7s transfer and bite telegraphs remain unchanged.

Important extra root cause: some physical catches grip feet/side with the human
IN FRONT of the palm. A frozen WORLD-space offset forces the hand inside the chest
when that person's torso reaches the mouth. `TitanCombatTarget.SetAerialCarryYaw`
now lets only the current airborne owner turn the person smoothly around the SAME
grip into side-on carry; local grip point and root-to-grip length remain constant,
no parenting/recentering/regrip snap, capsule stays upright. `biteGripOffset` remains
latched but its world direction is yaw-rotated during transfer and retained after
release for arm recovery. Ground follow/restore is unchanged. Release velocity
includes the actual held root's orbital motion, not only socket translation.
Mouth approach.6m forward still requires actual victim contact with unchanged.55m
bite sphere. Long-tick validation samples the arc INCLUDING its changing yaw offset;
ordinary frames sweep the real grip; original conservative checks remain too.

**108/108 passed twice**, Unity2021.3.25f1, **2026-11-03 14:40:20–36 UTC** (PID34496)
and **14:44:01–16 UTC** (PID37808,15.493s). All106 previous checks +2 new for both hands/
two animation phases torso clearance, preserved local grip/radius/upright root,
and forward-arc cover on long ticks. Existing fast moving full catch/bite now also
asserts torso clearance. Actual nontrigger forearm/hand versus chest/spine via
`Physics.ComputePenetration`: **0** in four static cases and moving case, strict<.005m.
The shared shoulder seam is intentionally not tested as a separated body part.
Continuity limits remain<8deg/120Hz: fast6.68deg, moving6.31deg, old-timing5.32deg.
Aerial mouth death10.83m/s (<40 unchanged); intact11.26m/s,gap.007m,hipsY.86m;
ground mouth10.91/10.36/11.82/11.81m/s. No physics tuning, speed caps or weaker tests.

MCP inspected before/after working sandbox poses1.70/2.15/2.42s. Final forearm/palm
penetration0, same.175s capture, one capture/one bite, HP0, release complete, max6.18deg
across all3 selected arm bones. Proof:
`ArtSource/Titan/Combat/AerialMouthClearance_Comparison.png`. Temp review wrapper
`%TEMP%/titan_torso_review.py` generates close captures in ignored Library dirs.
Returned Editor to Edit Mode, unpaused, scene clean, camera/RT removed; no scene saved.
Main final import includes new analytic yaw-aware arc obstruction checks.

Next recommendation (user asked): review/checkpoint scoped aerial work, then build a
REAL-player integration lab, implement actual ODM motor suspension/release (ropes,
boost, coroutines, input/camera/look, safe current pose/momentum), wire real health/
death/respawn, then swept real swords and held-arm-cut counterplay, followed by
full-speed terrain/edge/cover/multiple-Titan stress/polish. Do not start more proxy
features instead of real gear wiring. No gameplay scene/player prefab changes yet.
Current aerial work remains uncommitted; ground commit28cb997a stays local/not pushed.
See `docs/titan-aerial-grab.md` for contract, limitations and next-step order.

## Previous: fast, frequent aerial reach — NO warning-arm preparation

Latest user feedback: grabs felt slow/infrequent. Explicitly requested NO raised
warning arm: ODM players already know standing/flying in front of a Titan is dangerous.
This supersedes the earlier aerial windup requirement, not ground/stomp/bite timings.

Implemented defaults: **0s aerial windup, .22s direct reach from the actual palm**,
no pre-grab warning ring. Miss recovery.25s, aerial cooldown.65s (~1.12s repeat miss
cycle versus several seconds). Successful carry/bite/recovery retain their existing
durations; absolute bite now3.14s because the catch starts sooner. Aerial selection
checks every eligible frame before ground retry/stationary gates. Seven bounded
prediction times allow current reachable flyers even if a full forecast exits range;
aim still commits once, actual hand-box/capsule CCD still REQUIRED, dodge still works.
No snap/homing/hidden target tracking or larger catch geometry added.

Fast reaches transport the ACTUAL selected anatomical hinge plane instead of rolling
into the old outward/down pole. Partial early catch must cache upper/lower joint
rotations and begin carry from that exact pose: re-solving the partial contact pose
with fractional reach weight twice caused12.61deg catch pops before this fix.
Keep these ownership/continuity guards; ground solver and rig remain untouched.

**106/106 passed twice**, Unity2021.3.25f1, **2026-11-03 14:02:10–26 UTC** (PID31208)
and **14:04:21–38 UTC** (PID30596). All101 prior checks +5 new for fast defaults,
no warning lift/ring, dodge, short retry, moving full-sequence continuity and
bypassing an active ground decision delay. Older explicit-time regressions pin
configurable.7/.3 values; saved-scene auto test now waits for true reach range.
Crossing test samples the actual new hand path, not the removed raised pose.
No assertion thresholds relaxed. Latest<8deg/120Hz: fast stationary6.43deg,
fast moving6.48deg; actual static catch.158s. Aerial loaded mouth death8.31m/s (<40).
Intact11.14m/s,gap.005m,hipsY.84m; all74 approved ground/bite/physics tests still pass.

MCP normal Play Mode / unpaused explicit120Hz real CCT replay: one NEW catch/one bite,
HP0, release complete; capture **.175s** after selection; all3 selected arm bones
max6.18deg from reach start through recovery. Viewed0ms/100ms/220ms/mouth frames:
no preparatory lift. Proof `ArtSource/Titan/Combat/AerialGrab_DirectReach_MCP.png`.
Prior proof with warning lift is historical, not the new behavior. Temp capture
`%TEMP%/titan_fast_aerial_capture.py` uses counter DELTAS because the lab may already
have completed an automatic ground grab before a manual review starts.

Editor returned to Edit Mode/unpaused, no scene saved; temporary camera/textures
removed. Current aerial work stays uncommitted; approved ground commit28cb997a
remains local/not pushed. No production ODM wiring done: still upright CCT proxy;
real rope/boost/controller/health integration is next. See `docs/titan-aerial-grab.md`.

## Previous aerial lab baseline (101 tests, before fast-reach feedback)

User said the ground bite/arm fix looks good and explicitly requested a commit,
then aerial grabbing. **Committed approved ground slice as28cb997a**, local only,
not pushed. Unrelated dirty assets/Plastic/IDE/model moves were excluded.

Aerial prototype is now implemented in the isolated lab. See
`docs/titan-aerial-grab.md`. **F** launches a reproducible airborne controller pass;
T auto combat,9/0 manual selected hand, E escape, normal limb/eye/death/reset controls.
Actual physical hand OBB versus configured human capsule CCD, with relative motion
and rotating-hand sweep; not an enlarged catch radius or detection-to-socket snap.
Prediction is sight-confirmed once, bounded1.05s/24m, then aim locks; actual arm
reach plus frontal shoulder-plane clearance required. Default .7s windup/.3s reach.
Standing chest carry8.2m then existing mouth/bite. Catch offset is preserved by moving
the arm; never parent a human to the skeleton. Early contact time constrains leases.

`TitanAerialMotor` is an explicit opt-in suspension/release contract.
`TitanSandboxFlight` is only the LAB implementation, NOT real ODM integration.
It restores from the current airborne pose, full-capsule exit/endpoint clearance,
explicit decayed incoming + hand velocity, gravity resumes/boost ends. If blocked,
ownership clears but safe physics restoration retries independently; removed target
component hands continuation to the motor. Production motor must outlive that gate.
Upright centered CCT supported; yaw okay, capsule tilt changes fail closed.
Real ODM ropes/boost/coroutines/health, rotating player shapes, held-sword policy
and production respawn remain the NEXT integration work. Legacy scripts inspected,
not modified; no gameplay scene/player-prefab wiring done or claimed.

**101/101 passed twice**, Unity2021.3.25f1: **2026-11-03 04:24:11–26 UTC** (PID9552)
and **04:26:31–46 UTC** (PID29000). All74 approved checks +27 additions. Final moving
proxy fixture uses the actual authored1.8m/.32m CCT and local runtime physics.
Strict<8deg/120Hz continuity: stationary6.39deg, moving5.64deg. Aerial mouth death
12.79m/s (<40 unchanged). Ground mouth deaths10.47/10.36/11.82/11.81m/s; intact
11.04m/s,gap.005m,hipsY.84m. No rig, art, mass, angular ranges or physics settings changed.

Unity MCP verified the working project, reproduced/fixed a moving shoulder-roll
singularity (90deg live /123.67deg new regression) missed by initial stationary
checks. Aerial uses outward/down pole; offset iterations restart from the SAME
animated base. Keep the approved ground pole separate. Full physical hand box also
fixes inscribed-sphere corner misses without enlarging contact geometry.
Final MCP moving capture -> carry -> mouth -> bite succeeded: one capture/one bite,
HP0, release complete, upper-arm max4.30deg. Viewed saved poses; proof:
`ArtSource/Titan/Combat/AerialGrab_MCP.png`. Temporary camera/textures removed;
Editor returned to Edit Mode, unpaused, scene clean, no scene saved. Normal
full-speed/finger/art polish is still not production approval.

MCP direct HTTP client is `%TEMP%/titan_mcp_review.py`, capture `%TEMP%/titan_aerial_capture.py`.
Use tool_search first; this session still exposed none, but server8080 is usable.
HTTP404 means expired temp MCP session: remove that OWNED .session, initialize,
select instance34b6b0a61e38ef2f. First command after Play transition may return null;
confirm a read-only ready query succeeds before a side-effecting replay. Keep
Editor unpaused. Do not blindly repeat a timed-out capture without checking state.

## Ground bite / arm-spasm fix — approved and committed28cb997a

User tested ground grab and reported spasming on the trip to the mouth, explicitly
asking for Unity MCP visual inspection. Did reproduce in the working Editor:
137.17deg shoulder jump/1/120s near3.14s. New continuity regression failed137.26deg
while all73 previous checks passed. Fixed explicit elbow-plane rotation frames in
`TitanGrabAttack.SolveArm`; source hinge uses the real hand, not offset grip.
Also fixed cumulative additive jaw opening on a constant Generic Animator channel:
reset to cached closed local jaw rotation before applying the current opening.
Do not reintroduce additive jaw accumulation or shortest-arc arm roll ambiguity.
No damage timing, capture radius, rig/model, physics limits or thresholds relaxed.

**74 passed /0 failed twice**, Unity2021.3.25f1, **2026-11-03 02:44:07–20 UTC**
(PID51064) and **02:49:24–37 UTC** (PID55304). New both-hand/four-phase120Hz test
measures shoulder/elbow/wrist rotation and bounded jaw opening. Max step2.26deg
(assertion<8deg); jaw<28.1deg. All73 prior checks pass. Mouth deaths max10.57/10.36/
11.82/11.81m/s; latest intact11.06m/s,gap.005m,hipsY.84m.

Unity MCP now actually used via localhost8080 JSON-RPC (normal tool_search still
exposed none). Started installed10.3.0 server, reconnected existing Editor bridge;
project hash34b6b0a61e38ef2f, working Editor PID57936. Captured BEFORE and AFTER in
normal sandbox Play Mode, manually stepping a frozen actor; no preview physics.
Both sides checked: live max2.13deg left/1.72 right, mouth/grip gap.35m. Adjacent
captured frames no longer flip. Comparison artifact:
`ArtSource/Titan/Combat/GrabArmContinuity_Comparison.png`.

Returned Editor to **Edit Mode, unpaused, TitanAISandbox clean**, no scene saves.
Temporary review camera and `Assets/Editor/TitanGrabReviewReconnectOnce.cs`/meta
removed. Only ignored Library review captures/logs remain. A connect-only bootstrap
request was used to restore MCP after removal; never run an asset build by accident.
Paused Play Mode caused unreliable MCP/WebSocket replies on Unity2021; future
captures should freeze actor ticks but keep Editor unpaused. Temporary HTTP client/
capture scripts are in `%TEMP%/titan_mcp_review.py` and `titan_grab_capture.py`.
Full animation/finger polish remains, but sampled visual review is no longer pending.
User subsequently approved the visual result and committed it as28cb997a; aerial
lab follow-up is described at the top of this checkpoint.

## Ground hand-to-mouth / bite foundation (committed28cb997a)

User clarified order: finish the ground grab's hand-to-mouth/bite first, THEN do
aerial ODM grabs. They specifically emphasize the arm must actually catch the player;
keep actual3D hand contact/reach as a hard requirement for aerial work.

Implemented in `TitanGrabAttack` as opt-in `enableBite` (standalone defaultfalse;
`TitanAISandboxDemo` sets true at runtime). After .83s ground contact, hold/lift1.5s,
transfer .7s, open-mouth warning .6s, close .12s, then one mouth-contact event;
.45s recovery/3s cooldown. Default damage100; latched timing/damage/radius/flag.
The actual arm IK reaches MouthSocket with both hands; jaw opens/closes. Feet plant
through rising. Orange/red mouth telegraph; E escapes through the last pre-hit frame.

Damage requires exclusive live ownership, actual disabled-collider geometry touching
.55m mouth sphere, valid selected arm/eyes/legs/AI/socket, unexpired lease, and clear
carry/mouth geometry. Grip path is swept during LateUpdate before target follow;
fast-forward checks lift/transfer segments too. Fail closed on saturated queries.
No delayed kill timer/coroutine or automatic death solely because holding elapsed.
Logical hold age prevents long ticks bypassing the victim's shorter lease.

Original cancellation/never-parent/state restoration remain. Lethal contact releases
but still recovers. Damage callbacks can kill/sever the Titan: snapshot restoration
must yield to current injury/ragdoll pose. Unsupported disabled mesh shapes fail
closed. Real player still needs an adapter. Proxy dies at contact then returns to its
original ground position; no swallowed mesh/corpse consumption or production respawn.

Pre-spasm-fix validation: **73 passed /0 failed twice**, Unity2021.3.25f1,
**2026-11-03 01:50:19–32 UTC** (PID49920) and **01:52:14–28 UTC** (PID26732).
All58 previous checks +15 additions, including normal-frame automatic playback and
saved-lab proxy bite/reset. Four mouth death cases max10.29/10.09/11.27/11.04m/s;
latest intact10.98m/s,gap.005m,hipsY.84m. No physics settings/limits/assertion thresholds
changed. Earlier69-check run exposed only a test cooldown sequencing mistake;
completed recovery before the next cooldown tick rather than weakening the gate.
See `docs/titan-bite-combat.md` and `docs/titan-prototype-validation.md`.

This foundation and the subsequent arm fix are now committed28cb997a; not pushed.
Main scenes, passive prefab, art/rest rig/capped cuts and legacy player untouched.
MCP review completed and user approved the ground visual result. Aerial lab is now
implemented; real ODM integration still needs explicit rope/boost/coroutine/momentum
and controller policies. Keep ground snap-back out of production aerial release.

## Previous request: commit combat and require mid-air ODM grabs

User approved committing the current combat slice and explicitly added a requirement:
**Titans must also grab airborne players swinging on ODM gear.** This is NOT yet
implemented by the ground-targeted grab (current selection rejects vertical offsets
above2.2m). Keep it in the next combat/player-integration plan; see the dedicated
mid-air interception section in `docs/titan-grab-combat.md`.

Plan height-aware3D reach, bounded sight-confirmed motion prediction followed by
committed/dodgeable aim, swept relative hand/player contact at ODM speeds, and the
same ownership/escape/cancellation safeguards. Real ODM ropes, boost, gravity and
release momentum need explicit adapter rules and collision-clear release. Do NOT
reuse the proxy's snap-to-pre-grab pose as a production aerial release, or solve
interception with hidden tracking, shortened wind-ups or a giant catch radius.
A standing Titan catching a flyer is the requirement; Titan jumping is separate.

Combat commits on `titan-combat-updates` (local, not pushed):
- `5bc07b40` Sandbox stomp/grab runtime, hold ownership, director, HUD and stable physics handoff.
- `ab988bf7` Combat/contact/ownership/scene-reset regressions (58 passing at checkpoint).
- Documentation/air-grab requirement committed separately with this checkpoint.
Unrelated dirty files remain excluded; no new runtime behavior changed for the air-grab note.

## Grab / hold / release foundation (committed)

User approved the stomp result and asked for the next step. Added standing,
sandbox-only `TitanGrabAttack`, exclusive bounded hold ownership in
`TitanCombatTarget`, and `TitanCombatDirector` for grab/stomp selection. At that
snapshot no bite/eat or real-player adapter; the new ground bite is above. See `docs/titan-grab-combat.md` for API/limits.

- Fixed .65s windup/.18s reach, one actual grip contact,1.5s bounded lift/hold,
  .45s recovery,3s cooldown. Aim is locked, not homing. Deep crouch/foot planting
  gets the current rig down to a rounded1.8m human; no finger animation.
- Human NEVER becomes a child of a hand/Titan. Collider/RB states are cached and
  restored; default release returns to its captured pre-grab WORLD pose. Moving-
  geometry clearance/real-player controller integration still need an adapter.
- E escape; own arm loss/eyes/either leg/death/disable/reset/AI pause release safely.
  Other arm loss does not cancel. Independent target lease defaults4s, catches a
  dead/stuck owner or invalid/detached socket. Standing only; no kneeling grab yet.
- Order: motion20 -> attacks50 -> target follow70 -> injury100. Never overwrite
  detached bones or stand up an injured/dead body on cancel.
- Selector uses seeded LOCAL System.Random (55% grab preference/fallback), waits
  for braking and spaces automatic attacks. Stomp standalone auto is disabled when
  director is added. Mutual exclusion also holds without a brain.
- Found rounded-controller edge misses: Unity's CCT query capsule is shrunken.
  Configured capsule geometry is explicitly checked (same1.05m hand radius), and
  stomp uses a CharacterController-compatible closest-point helper too. Tests now
  exercise actual controller-only victims, not just box stand-ins.
- Final review fixed an auto-update timing hazard: a director/input start earlier
  in this Update must NOT consume the delta from before it started. Both attacks
  skip automatic ticking on their selection frame; explicit TickAttack remains
  deterministic. New regression checks full telegraph and subsequent advancement.
- **58 passed /0 failed twice**, Unity2021.3.25f1, 2026-11-03 01:04:02–11 UTC and
  01:05:48–57 UTC. Latest PID20680. All40 previous tests plus18 new ones.
  Grab crouch/carry4 death cases max7.45/7.83/11.35/10.41m/s; snapshot pose preserved.
  All8 stomp deaths still pass. Latest intact max10.95m/s,gap.006m,hipsY.85.
- Actual saved AI scene integration resets WHILE HELD and verifies human survives,
  controller restored, old owner destroyed and no extra grab actors remain.
- New controls: **T auto combat**, **8 stomp**, **9/0 left/right grab**, **E escape**;
  existing limb/eye/nape/P/R and WASD/Shift unchanged. HUD now scrolls.

Stomp/grab runtime and tests are now committed as listed above; no push requested.
The five earlier snapshot commits remain below. No main scenes/prefab/art/rig generation modified.
Harness Unity tools unavailable; **live visual review remains pending**. Numeric
animation/physics tests are not proof of finished motion/art. Ground bite follow-up
is now above; mid-air support follows it and remains an explicit requirement, not
an optional polish item. Real-player/sword integration remains pending.

## Snapshot commits and stomp foundation (previous slice)

User requested committing the current work in chunks, then moving on. Five local
commits made (not pushed):
- `4816c15b` Unity2021-compatible MCP tooling.
- `2a4ee6c1` Titan art, historical backups outside Assets, Blender sources and previews.
- `69e1a3cc` Modular runtime, patrol labs, injury and responsive motion.
- `df22a580` Existing legacy combat scaffold plus modular sword-hit adapter; includes
  Attack.cs window and ODM speed accessors needed to compile that adapter. Legacy
  gameplay wiring is still unverified, not replaced by modular code.
- `6c98f1ca` Safe disposable tests and validation checkpoint (28 passing at snapshot).

Unrelated model moves/deletions, gameplay scenes, RagdollController.cs edits,
materials, IDE/generated project files, Plastic SCM state, old prefabs/plans and
old implementation-log docs deliberately remain uncommitted. Do not reset them.

The first combat slice was **sandbox-only stomp**, now included in the combat commits above:
`TitanStompAttack.cs`, `TitanCombatTarget.cs`, AI movement ownership, HUD/proxy health,
and12 additional tests (40 total). Fixed .65/.14/.5s telegraph/strike/recovery,
2.2s cooldown, fixed visible landing circle,40 damage once, scene-local geometry,
cover/airborne/AoE filters and immediate interruption. Passive body prefab unchanged.
Original slice used T for auto stomps; current T is auto COMBAT, with9/0 grab and
E escape added above. 8 remains left stomp; P/limb/eye/nape/reset remain.
See `docs/titan-stomp-combat.md`.

Expanded death tests cover8 deterministic phase/foot combinations and an unforced
(no gravity/no external contact) handoff. They exposed late wrist velocity spikes
with hard angular stops in folded, contacting poses. Fixed by **mass-proportional
compliant/damped angular stops on dynamic activation** (spring=mass*80,damper=mass*12).
Original angular ranges, masses, solver counts, angular speed cap4, joint count and
no-interpolation ownership retained. No global physics setting or linear speed cap.
Also rebase POSITIONAL connected anchors from the current world pose: animated
non-physical spine/shoulder intermediaries move physics bones off old bind anchors.
Experimental mass conditioning, friction, solver, preprocessing, angular-cap and
range changes were NOT retained. Stomp braces within its elbow hinge plane and
recovers by blending IK targets home; support feet do not sink as hips rise.

**40 passed /0 failed TWICE**: 2026-11-02 23:21:38–47 UTC and23:24:32–40 UTC,
Unity2021.3.25f1. Latest PID47384 in the same owned disposable project below.
Loaded-stomp death8 cases max speeds12.01–12.27m/s (formerly up to~100m/s);
unforced handoff max.000113m/s. Latest intact max10.89,gap.004m,hipsY.86;
injured prone5.06/kneel11.64m/s; wall routing4.23 simulated seconds.
All28 snapshot tests still pass. Stomp is now committed together with the grab runtime/tests above.

Working Editor was not running at turn start; opened project then. User later
reopened it: last read-only process/title check PID42496, TitanAISandbox, Unity2021.3.25f1,
WebGL target. Harness Unity tools unavailable; **live stomp visual playback is still
pending**, despite numerical animation/physics checks. No intentional pending
`TitanPrototypeBuild.request`. Inspect current state before any scene actions; do
not save gameplay scenes. New code imports on Editor refresh/focus.

## Earlier motion-overhaul phase

- `Assets/Titan/Prototype/TitanMotionStyle.cs`: measured-speed walk/run BlendTree
  parameters, per-instance cycle/cadence/turn/posture bias; seeded local RNG, coherent
  smooth pace segments and occasional short chase hesitations, not random frame jitter.
  Does not consume UnityEngine.Random or look up a hidden player. Fields styleSeed
  (0=instance seed), varyPace, reference speeds; public ResetStyle/TickMotion/TickTurning.
- `Assets/Editor/TitanMotionClipBuilder.cs`: reproducible Generic Idle2.6s, Walk1.02s,
  distinct Run.68s, key-pose contact/pass/quick recovery, bent running arms, torso
  twist/lean, hip sway/contact correction. Existing controller/Idle/Walk GUIDs retained;
  adds Run.anim. Params IsWalking/Gait/StrideRate/CycleOffset; 90/100ms transitions.
- Measured root speed drives gait/cadence; global Animator.speed remains1. Head leads
  sharp pivots85ms then root turns210deg/s walk or330run (individual variation).
  Sharp corners reduce pace. Motion owns agent.updateRotation, restores on disable.
- AI base speeds4.6 patrol/9.5chase, profile factors .90–1.08 patrol/.94–1.20 chase,
  brief .12–.22s hesitation targets .52–.66, segments1.7–3.5s. Lab acceleration26.
- Fast eye recoil then staggered coverage~.15–.2s; repeated hits alternate lead hand.
  Arm loss adds brief chest recoil. No styling writes while blind, leg-disabled or dead.
- Injury durations now **.58s knee / .76s prone**, with buckle/fast drop/impact/damping
  and staggered arm/head timing, not uniform slow smoothstep. Final poses unchanged.
- Foot collider orientation fixed: diagonal toe bones made old on-bone boxes oversized
  vertically, lifting the visible soles. Correctly oriented child FootPhysicsShape
  boxes now form compound colliders on the SAME foot Rigidbodies; zones follow them.
  Still17 bodies/16 joints/14 zones/22 original source bones. Tests check visible soles.
- Rebind now preserves the surviving pose, current state/time and float parameters,
  as well as detached transforms: running cuts must not reset hip motion/gait phase.
- Body prefab rebuilt; main gameplay scenes untouched. Original body lab is still
  non-navigating, but now has **7** chase-run preview as well as1 idle/walk.
- **28 passed /0 failed**, Unity2021.3.25f1, **2026-11-02 04:00:16–24 UTC**.
  All21 old tests +7 in TitanMotionStylePlayModeTests.cs. Rate/gait, deterministic
  bounded variation/global RNG independence, distinct looping supported clips,
  head-led pivot, fast fall shape, running cuts and quick eye recoil checked.
  Normal-physics integration allows bounded authored hip bob/contact motion while
  still checking horizontal alignment and other bones' unchanged local positions.
  Old two-sample stride test now checks a full cycle to avoid symmetric-phase flakiness.
- Latest runner completed PID50500, same disposable project/results paths below.
  Intact death maxspeed12.54,gap.004m,hipsY.95; injured5.89m/s prone/9.57knee.
  Wall-routing root passes obstacle in4.23 simulated seconds.
- Final checked Unity contact sheet: `ArtSource/Titan/Motion/TitanMotion_Unity.png`.
  Six raw Walk_0..2/Run_0..2 PNGs alongside it. These show poses, not a video.
- Final visual checks completed; Play Mode stopped. AI lab left open in EDIT MODE.
  Final structural validation PASS includes distinct idle/walk/run, motion and injury
  components. Inspect current dirty state before saving/changing scenes.
- Notes/tuning/limitations: `docs/titan-motion-overhaul.md`.
- Still generated/native prototype motion, not production mocap/full runtime foot IK.
  Physics death remains natural-time. Attacks/player integration remain next, not done.

## Earlier injury-animation phase

- New `Assets/Titan/Prototype/TitanInjuryPose.cs` attached by body prefab builder.
  Procedural eased pose blends on the existing rig (not imported animation clips).
  Originally1.2s surviving-knee/1.5s prone; current .58s/.76s impact-shaped transitions.
  Mirrored knees and rapid double-cut supported. Only attached bones are written.
- First leg loss disables Animator; injury controller owns bones while alive/injured.
  LateUpdate order100 overrides standing blind-hand posing. Death stops writing;
  the existing body takes over with joint-rebased ragdoll. Root/nav position stays fixed.
- Scene-local ground ray ignores self, dynamic debris, triggers and ceilings. Analytic
  collider support settles/lifts pelvis during folding arcs. Flat/gently stepped
  ground only; not a full terrain-contact IK solution.
- Body `IsHelpless` true when both legs gone; both grab capability flags then false.
  Single-legged kneeling retains arms for later attack work, but cannot walk.
- Both sandbox HUDs show injury state. Body prefab rebuilt preserving rig/art/GUIDs.
- Actual Unity captures checked:
  `ArtSource/Titan/Reference/TitanInjury_KneelingRight_Unity.png`,
  `ArtSource/Titan/Reference/TitanInjury_Prone_Unity.png`.
- **21 passed / 0 failed**, Unity2021.3.25f1, **2026-11-02 02:52:40–49 UTC**.
  Seven new checks in `TitanInjuryPosePlayModeTests.cs` plus all14 old tests.
  Mirrored knee/head/shin pose, smooth fall and floor support, second-cut current
  visual pose preserved, no writes to detached bones, missing arms/rapid cuts,
  helpless flags, blindness recovery and death from injured poses all pass.
  Max injured death speed: knee9.94m/s, prone9.72m/s; intact11.09m/s,gap.003m,hipsY.79m.
  Latest completed runner PID33784; same temp project/results paths below.
- Test pitfalls resolved: snapshot Transform positions, not stale Rigidbody native
  caches after manual animation; direct world skinning with bindposes/weights avoids
  BakeMesh+TransformPoint double-counting the 100x FBX scale; manually simulated
  physics waits for Unity frame-end deferred joint destruction after severing.
- Last live action stopped Play Mode after previewing the poses. AI lab remains open
  in edit mode; inspect dirty state before changing/saving. Temporary preview actors
  were runtime-only and are gone after exiting Play Mode.
- Full notes: `docs/titan-injury-animation.md`.

## Navigation foundation (earlier phase)

- New isolated scene: `Assets/Scenes/TitanAISandbox.unity`.
- `Assets/Titan/Prototype/TitanPatrolChaseAI.cs`: opt-in component requiring the
  body and NavMeshAgent. States Patrol, Chase, Investigate, Search, Blind,
  Immobilized, Dead, NavigationUnavailable. Explicit target ROOT, no player lookup.
- 38m range, 120-degree horizontal FOV, .15s scene-local raycast perception;
  ignores own/target hierarchy and triggers, solid scenery blocks sight.
- Only complete paths. Current base patrol4.6m/s, chase9.5m/s with motion variation,
  nominal chase stopping radius6m,
  .3s repath. Lost sight uses LAST SEEN position, not hidden target's live position.
  Memory14s; local turn/search4s; then nearest patrol route point.
- Blindness stops immediately, prevents visual detection and ages memory. Recovery
  reacquires normally. Arm loss allows walking; either leg loss immobilizes.
  Death disables navigation and uses the existing ragdoll.
- `TitanNavigationSurface.cs`: explicit floor/wall runtime NavMesh bake with built-in
  Unity2021 APIs, radius1.8m/height12m. Owns/removes only its own NavMeshDataInstance;
  no global bake settings changes or RemoveAllNavMeshData.
- `TitanSandboxTarget.cs`: ground-only controllable human PROXY, not the real player.
- `TitanAISandboxDemo.cs`: runtime agent/AI setup, HUD, input, reset/cleanup. Reset
  actors remain in the lab scene even when loaded additively; old agents disabled.
- `Assets/Editor/TitanAISandboxBuilder.cs`: menu
  `Tools → Titans → Build Patrol and Chase Sandbox`. Close AI lab before rebuilding.
  Saves only its new additive scene and never saves user's active/gameplay scene.
- Original body prefab and body lab remain PASSIVE (no AI components added).

Controls: **WASD** human world X/Z, **Left Shift** sprint, **Left/Right arrows**
orbit, **2/3** sever arms, **4/5** sever legs, **6** blind, **K** kill,
**P** pause/resume AI, **R** reset Titan and human. Buttons: hidden/open-lane target.
Yellow wall blocks sight/navigation; cyan markers show patrol route. Current lab also
has stomps (T toggle /8 manual), unlike this earlier navigation-only phase.

## Important bugs fixed in this phase

1. Unity2021 `NavMeshAgent.ResetPath()` clears isStopped. Stop now ResetPath FIRST,
   then isStopped=true. Tests verify blind root does not drift.
2. Visual inspection caught animated kinematic bone interpolation restoring stale
   WORLD poses while the nav root moved. The first root-only tests missed this!
   `ModularTitanBody.Initialize()` and `TitanPrototypeBuilder.AddBody()` now set
   **RigidbodyInterpolation.None**. Regenerated body prefab for that setting,
   preserving its model/rig/GUIDs. Dynamic activation already clears interpolation.
   New saved-scene integration test advances NORMAL physics and checks hips/root
   alignment and all ragdoll bones' local positions during real movement.

## Earlier navigation verification

**14 passed / 0 failed**, **2026-11-02 02:07:19–27 UTC**, Unity2021.3.25f1.
Six existing body/mesh/UV/stun/dismemberment/stable-death/leg-orientation tests plus
8 AI and saved-scene checks. Wall-routing test moves the root past the obstacle in
5.06 simulated seconds; patrol advances after2.30m travel. Ragdoll maxspeed11.37m/s,
finalmaxjointgap .007m, hipsY .79m.

- `Assets/Tests/TitanPrototype/PlayMode/TitanPatrolChasePlayModeTests.cs` (7 new tests)
- `.../TitanAISandboxScenePlayModeTests.cs` (1 scene startup/reset/visual-follow test)
- Existing `.../ModularTitanPlayModeTests.cs` (6 tests)
- Runner `tools/run_titan_validation.py` now copies both lab scenes.
- Run `python tools/run_titan_validation.py --wait` in disposable project only.
- Reused test project: `C:/Users/maxsu/AppData/Local/Temp/ODM-TitanValidation-fh_v2o9d/`
  (`test-results.xml`, `validation.log`). Latest completed process PID57180.
- Builder structural validation also passes:17 bodies,16 joints,4 branches,
  14 triggers, Generic idle/walk,3 attack sockets.
- Actual AI lab screenshot: `ArtSource/Titan/Reference/TitanAISandbox_Unity.png`.
  Recaptured and visually checked AFTER the interpolation fix; visible Titan
  now follows the root correctly (hips offset remains exactly (0,5.65,0)).
- AI lab left open in EDIT MODE after the final live visual check. Latest
  structural query also confirmed all17 authored bodies have interpolation=None.
  Inspect scene dirty state before changing/saving anything.

## Art / foundation retained

User disliked separate bulging anatomical primitives, supplied
`C:/Users/maxsu/Downloads/refImage.jpeg`; accepted reference-inspired **leftmost
stocky dark-haired Titan**. Continuous torso, rounded block limbs, integrated face,
wider head, short neck, grin, closed hair cap. 15 closed skins,22 exact original rest
bones,78,888 triangles,2K PBR atlases. No individual finger rig/facial blendshapes.

- `ArtSource/Titan/Reference/TitanReference.blend` / `.fbx` / `Textures/`
- `.../TitanReferencePreview.png` Blender T-pose
- `.../TitanReference_Unity.png` checked Unity idle-pose art preview
- Active model `Assets/Art/Titans/Prototype/TitanPrototype.fbx`
- Active texture filenames still `.../Textures/TitanDetailed_*.png`, with reference maps.
- Prefab `Assets/Prefabs/Titans/ModularTitanPrototype.prefab`
- Passive original lab `Assets/Scenes/TitanPrototypeSandbox.unity`
- Old art backups outside Assets: `ArtSource/Titan/DetailedBackupBeforeReference/`,
  `ArtSource/Titan/BlockoutBackup/`. Never import backup Unity GUIDs into Assets.
- `tools/blender/build_reference_titan.py`, configurable `bake_detailed_titan.py`.
- Canonical Unity skin names Titan_<part>, source supports TitanReference_/TitanDetailed_.
- Mesh replacement explicitly copies native geometry/UV buffers preserving GUIDs;
  CopySerialized left stale buffers and must not be restored as the only copy step.
- Whole branch shoulder/hip cuts only; detached leg pose is preserved through
  Animator.Rebind (do NOT hardcode a 180-degree flip).

## Boundaries / next steps

**Production consumption/respawn, real-player health/hold/release, swept sword hits
and actual ODM rope/boost integration remain undone.** Ground combat was visually
approved/committed. Physical3D aerial interception, release and bite now work in the
isolated controller-proxy lab, with automated and sampled MCP review; see top. Leg loss now animates into
kneeling/prone, but crawling, regeneration and getting back up remain undone.
Flat-lab airborne projection is not proof of
multi-floor navigation. Production level bake, dynamic corpse avoidance, crowd
behavior and authored animation polish are future work.

Next: optional user review via F in the lab; then implement/test the real ODM motor
adapter (rope/boost/coroutine suspension, momentum, rotating controller, health and
held sword policy). Do not claim full Titan AI or actual player/sword integration.

## Environment / safety

Root `C:/Users/maxsu/OneDrive/Documents/ODM Attempt # 1`.
Another Unity6 editor for elderash may be running: identify ODM by project path.
Heavily dirty tree predates our work; never revert unrelated changes. Five snapshot
commits plus the new combat commits listed above; unrelated changes remain uncommitted.
Never rerun removed Editor-preview-scene physics tests (crashed working Editor).
Use disposable runtime tests. Standard dedicated additive scene authoring is fine.
MCP Unity v10.3.0 endpoint localhost:8080; Blender socket JSON execute_code localhost9876.
Existing unrelated legacy `RagdollController` unassigned-animator error and stale
MCP disconnect logs may exist; don't claim the console globally clean.

Docs: `docs/titan-bite-combat.md`, `docs/titan-grab-combat.md`, `docs/titan-stomp-combat.md`, `docs/titan-motion-overhaul.md`, `docs/titan-injury-animation.md`,
`docs/titan-patrol-chase.md`, `docs/reference-titan-model.md`,
`docs/modular-titan-prototype.md`, `docs/titan-prototype-validation.md`.
