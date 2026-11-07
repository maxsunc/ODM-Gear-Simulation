# Titan corpse steam, darkening and disappearance

Unity2021.3.25f1 / URP12.1.11. Runtime-only; no prefab/scene/art/import changes.

## Behavior

- Real `ModularTitanBody.Die` starts one `TitanCorpseDecomposition` AFTER the
  existing current-pose native ragdoll handoff. Direct death and lethal nape damage
  share that path. Repeated Die/BeginDeath cannot duplicate the effect.
- Pale, soft, noise-edged steam rises from CURRENT physical collider surfaces,
  rather than a static animated root. It follows a moving/falling corpse naturally.
- The original texture/normal/metal maps and Lit shader are preserved. Body tint
  smoothly darkens to55% of its original value over about1–6s. At6s it starts
  genuinely alpha-blending out, becomes invisible around17.5s and is removed at
  **18 scaled seconds**. Pause pauses aging and the particle simulation.
- Native body/limb colliders, rigidbodies, masses, joints, solver12/4, angular cap4,
  velocities and physics ownership are untouched throughout the effect. There is
  no shrinking, sinking, bone/root animation, force, collision suppression or
  post-step velocity clipping to fake decomposition. Physics objects are removed
  only with their completed corpse object.
- Severed arms/legs get their own independent steam/fade on severing. They do not
  remain forever when the owner dies or is removed, and the owner never recolors
  their already-owned material instances a second time. Their lifespan is the
  smaller of18s and a positive `detachedLifetime`. Zero now means the18s default,
  NOT permanent loose physical limbs. A shorter explicit limit stays respected.
  The still-living injured Titan does NOT fade/heal; it can die normally later.
- Player corpses/HP/respawn/effects are unchanged. No new Titan/player damage,
  invulnerability, nape assistance, attack timing, AI/nav movement or audio changes.

## Resource ownership

`Assets/Titan/Prototype/TitanCorpseDecomposition.cs` belongs to the prototype
assembly so every modular Titan is covered, including placed and spawned actors,
without a scene manager or legacy/default-assembly dependency. Installation is
runtime only in Die/Sever; a healthy uncut Titan has no effect/material copies.

Per effect, clone each distinct original skin material ONCE, assign shared clone
arrays only to that corpse's MeshRenderer/SkinnedMeshRenderer, keep original refs,
and destroy owned native materials on disposal. Removing just the observer restores
valid original renderer material refs. Shared approved assets/other Titans are never
recolored. Existing camera/sword/particle renderers are not treated as body skins.

At fade start only, the cloned URP Lit materials switch to transparent blending,
transparent render queue, ZWriteOff, and no shadow/depth passes so an invisible
body cannot keep opaque shadows/depth. Until that point original opaque rendering
is retained. `Resources/TitanCorpseFadeVariant.mat` retains the matching transparent
NORMALMAP/METALLICSPECGLOSSMAP URP12 variant; it is NOT assigned as a replacement
skin material. This is build inclusion preparation, not a WebGL build certification.

One cosmetic ParticleSystem per main corpse (capacity128),32 per loose branch:
maximum256 across a body plus four branches. Shared `TitanCorpseSteam.mat` and
`TitanCorpseSteam.shader`, world space, scaled time, private System.Random and
fixed non-global particle seed. No colliders, RBs, lights, sounds or camera writers.
Manual emission30/s body,7/s branch; per-update hitch catch-up budget capped, rising
velocity proportional to sqrt(size), puff dimensions proportional to Titan size
(clamped.25–10). Current solid collider ClosestPoint is sampled; no skinned-mesh
baking, recurring world searches or per-frame arrays/material allocation.

Root/scene disposal cleans the effect. Separately severed roots belong to their
own finite lifecycles in the SAME scene and scene unload removes them too. Existing
random-spawner `corpseLifetime`25s default is longer than the natural18s effect.
Its explicit shorter lifetime / hard retained-corpse capacity remain authoritative
emergency resource limits and can preempt the presentation; those protected user
settings and tests were not weakened to keep extra physics bodies around.

## Validation / evidence

`ArtSource/Titan/Decomposition/` contains fresh full baseline/final XML, actual native
screenshots and saved CURRENT GiantForest2 results; `validation-summary.json` gives
exact counts/timestamps/source hashes. `Library/TitanDecompositionSourceBaseline.json`
protects original art/materials/scene/nav/settings/palettes/Run/player/nape/sword sources.

- Before413pass/8fail/1skip of422 -> **429pass/SAME8fail/1skip of438, twice**;
  16 additional cases, including environment-gated native capture (no-env pass is
  NOT rendering proof). No existing physical thresholds/assertions changed.
- Death/sever eligibility and duplicate-death guard; monotonic darkness/alpha at
  1/3/5/10x, exact texture/shader preservation and original asset immutability;
  unchanged transforms/velocities/solver/collision flags; bounded no-physics steam,
  private RNG, current physical-bone emission after root-independent motion;
  invalid/zero delta, scaled pause, finite branch lifespan, complete root/branch/
  owned material/steam cleanup and observer-only material ref restoration.
  Surviving injured owner remains valid after a loose branch has disappeared.
- **Native graphics1/1**, real approved3x rig, native4s fall with speed<40m/s,
  same stationary physical corpse pose/camera for each rendering phase. Five real
  completed-frame GameView screenshots: Fresh/Dark/Steam/Fading/Gone. Pixel tests
  verify darkening, visible steam and actual background contribution through the
  fading skin—not just a float/particle counter. No missing-shader magenta. The
  fixture intentionally advances effect age without moving the settled pose for
  readable phase comparison; production aging is ordinary scaled Update.
- Saved-map native **6pass/1SAME unbaked4x configuration failure of7**. New real
  18s wait observes steam/darkening at4s, transparency at10s and actual corpse/PS
  removal at18s. Surviving Titan keeps severed leg and armHP950, player stays100HP,
  same player/scene/nav triangles. Existing player-only respawn test still passes.
  No4x bake/scene publication or spawn-size/tuning edits.
- Marker-owned disposable workers only; no live Play input/stop/scene switch/save/
  reload or live physics automation. Source bytes match both final workers.

Reproduce: full `python tools/run_titan_validation.py --reuse <owned> --wait`, then
`python tools/capture_titan_decomposition.py`; map snapshot/test only, never bake/
rebuild/publish for this change. Subjective steam density/fade pace, crowd/device/
WebGL performance and comprehensive long-duration10x corpse safety remain playtests.

Previous player-only respawn is LOCAL commit `ca20e655`; this feature is a separate
selective LOCAL checkpoint. Nothing pushed without approval.
