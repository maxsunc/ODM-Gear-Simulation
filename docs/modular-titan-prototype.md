# Modular Titan — Phase 1: body, rig, damage zones and ragdoll

## Scope

This establishes the modular body and physics pipeline before adding the full enemy AI.
Patrol, sight and chase are now available in the separate [AI lab](titan-patrol-chase.md);
the body prefab and this original body lab intentionally remain passive.
The active Unity prefab now uses the [reference-led art pass](reference-titan-model.md),
with a continuous stocky torso and rounded block limbs inspired by the user's image.
The blockout and earlier anatomical pass are preserved as backups outside Assets.
This is still stylized prototype art, not a final production-retopologized character.
The legacy `Titan.prefab`, `TitanV2.prefab`, existing Titan AI scripts, player prefab,
and gameplay scenes are intentionally not rebuilt or replaced.

## Assets

- Current Blender source: `ArtSource/Titan/Reference/TitanReference.blend`
- Current Unity render: `ArtSource/Titan/Reference/TitanReference_Unity.png`
- Previous anatomical source: `ArtSource/Titan/Detailed/TitanDetailed.blend`
- Original blockout source: `ArtSource/Titan/TitanPrototype.blend`
- Unity model: `Assets/Art/Titans/Prototype/TitanPrototype.fbx`
- Playable body prefab: `Assets/Prefabs/Titans/ModularTitanPrototype.prefab`
- Standalone lab: `Assets/Scenes/TitanPrototypeSandbox.unity`
- Generated materials, trimmed skin meshes, animation clips/controller:
  `Assets/Art/Titans/Prototype/`

The model is approximately **12 metres tall**, with **22 bones** and **15 separate
skinned mesh sections**. Each mesh is closed (no open boundary edges). Arm and leg
cuts are presently **whole-branch cuts at the shoulder/hip**, not arbitrary mesh slicing.
Both the remaining socket and severed branch have geometry across the cut surface.
Head and jaw are separate; the jaw has its own bone. The reference pass uses block
mitten-like hands with shallow finger lines, not individually articulated fingers.

### Bone layout

```text
TitanRig
└─ Hips
   ├─ Spine → Chest → Neck → Head → Jaw
   │          ├─ LeftShoulder → LeftUpperArm → LeftLowerArm → LeftHand
   │          └─ RightShoulder → RightUpperArm → RightLowerArm → RightHand
   ├─ LeftUpperLeg → LeftLowerLeg → LeftFoot → LeftToes
   └─ RightUpperLeg → RightLowerLeg → RightFoot → RightToes
```

Animation uses Unity's **Generic** rig and native key-pose curves. The later
[motion overhaul](titan-motion-overhaul.md) adds distinct idle/walk/run clips,
velocity-matched cadence, head-led pivots and controlled variation. It does **not**
yet provide a validated Humanoid
avatar or retarget the old Mixamo clips. Those are separate next-stage tasks.

## Try it

Save your current scene if necessary, then open `TitanPrototypeSandbox.unity` and
enter Play Mode. No gameplay scene needs to be changed.

| Key / on-screen button | Action |
| --- | --- |
| 1 | Toggle idle / in-place walk |
| 7 | Chase-run animation preview |
| 2 / 3 | Slice left / right arm |
| 4 / 5 | Slice left / right leg |
| 6 | Eye slash: 5-second stun, surviving hands cover the eyes |
| K | Nape hit: switch the remaining body to ragdoll |
| R | Spawn a fresh prototype and clear its detached branches |
| A / D, arrow keys | Orbit the preview camera |

The cyan capsule represents a 1.8m person. Lab buttons deliberately call the same
body damage API as sword hit zones, but **are not a test of the actual player's
sword input, high-speed collision detection, or enemy attacks**.

## Runtime design

### `ModularTitanBody`

Independent body/physics component; no dependency on the old unfinished AI.

- Alive: Animator enabled, **17 kinematic physics bodies**, solid grapple/collision
  volumes, separate damage triggers.
- Death: Animator and optional NavMeshAgent disabled, all remaining bodies become
  dynamic. **16 limited CharacterJoints** connect the full intact body. Cached
  kinematic interpolation/velocity is cleared and joint connection frames are rebased
  to the current animation pose; dynamic bodies use no interpolation and giant-scale
  angular damping.
- Sever: detach the actual three-bone arm/leg branch, its three skins and physics
  bodies; remove the branch's root joint; disable its damage zones; allow the
  severed branch to simulate independently. Rebind the live Animator to prevent it
  from continuing to animate removed bones, preserving every detached bone/skin's
  local pose through the rebind so its default transforms cannot flip the leg.
  Physics is activated only after that pose is restored. Detached parts expire after 30 seconds.
- Each generated skin mesh contains only the bones with **non-zero weights**.
  This avoids references to the entire original skeleton on detached parts.
- Internal ragdoll collider pairs are ignored to avoid explosive contacts at seams.
  Bodies still collide with the environment. Self-collision can be refined later.
- Eye damage is a **temporary state**, not destruction of the head mesh. Its timer
  refreshes on another slash; it stops walking/grab capability, poses surviving arms
  over the eyes, then restores capabilities when it expires.
- Leg loss now triggers [injury animation](titan-injury-animation.md): kneel on the
  surviving leg with head bowed; both missing legs produce a helpless prone collapse.
  Crawling, regeneration and movement recovery remain unimplemented.
- `CanGrabLeft`, `CanGrabRight`, `CanWalk`, `IsBlind`, `IsDead`, `IsHelpless` are
  available to AI. Both missing legs disable grabbing as well as walking.
- `LimbSevered`, `BlindnessChanged`, `Died` events are available for attack cancellation,
  releasing held players, audio and effects when the new AI is connected.

Public operations:

```csharp
body.SetWalking(true);
body.ApplyDamage(TitanHitRegion.LeftArm, damage, worldPoint, impulse);
body.ApplyDamage(TitanHitRegion.Eyes, damage, worldPoint, Vector3.zero);
body.ApplyDamage(TitanHitRegion.Nape, damage, worldPoint, impulse);
```

Positive nape damage kills immediately. Each arm/leg has 60 shared HP; multiple
trigger volumes on one limb damage the same region. `Sever` and `Die` are idempotent.
`MouthSocket`, `LeftGripSocket`, `RightGripSocket` are ready for attack animation work.

### `TitanHitZone` / existing `DamageDealer`

Four limbs have three damage volumes each; the face and nape have one each: **14 zones**.
Damage triggers remain separate from solid ragdoll colliders. Face/nape volumes
compensate for Blender FBX's 100x bone transform scale and are sized in metres.

`DamageDealer.cs` now recognizes these zones before checking legacy `TitanLimb` /
`TitanNape`, preserving the old interfaces. A modular region is damaged at most once
per activation of the sword attack object. The existing attack toggles that object
per swing. The real player scene still needs inspection/wiring and play testing.
Trigger-only fast fly-by sword collision is not guaranteed; add swept blade checks
in the combat integration phase rather than assuming triggers cannot tunnel.

## Regeneration and verification

The current reference-model build/bake steps are in [reference-titan-model.md](reference-titan-model.md).
To regenerate only the original blockout (not the active reference model):

```text
blender --background --python tools/blender/build_modular_titan.py -- ArtSource/Titan
```

Copy the newly exported `ArtSource/Titan/TitanPrototype.fbx` into
`Assets/Art/Titans/Prototype/`, then in Unity:

- `Tools → Titans → Build Modular Prototype Assets`
- `Tools → Titans → Validate Modular Prototype`

Run the actual PlayMode tests **outside the live working project**:

```text
python tools/run_titan_validation.py --wait
```

The runner copies only the prototype and its tests into a disposable project under
Windows Temp and launches Unity 2021.3.25f1 in batch mode. It prints the XML result
and log paths. Set `UNITY_2021_EDITOR` if your Editor is installed elsewhere.
The tests are also visible in Unity Test Runner as `ODM.TitanPrototype.PlayModeTests`;
use the disposable project so entering Play Mode cannot run your gameplay scripts.

The builder only updates the **new generated prototype assets and sandbox**.
It creates/saves/closes a separate additive scene and restores the previously active
scene. It never saves existing gameplay scenes. Do not put hand-authored changes in
the generated prefab/controller/materials if you intend to rebuild; create a variant.

The PlayMode tests create their own **local PhysX scene** in the disposable project.
They check native animation, nonlethal/duplicate hits, all four detached branches,
capability flags, temporary blindness recovery, nape death, and an intact ragdoll
falling onto a floor for five seconds. They do not enter Play Mode or advance physics
in your working project's gameplay scenes.

An earlier Editor-preview-scene test crashed Unity 2021 during Animator cleanup
(`ClosePreviewScene`). That test has been removed; physics tests are deliberately
not run automatically on import or in an Editor preview scene.

Latest verification results: [titan-prototype-validation.md](titan-prototype-validation.md).

## Next stages

1. **Review proportions / art direction**: refine head, hands, joint silhouettes and
   topology; add UVs/textures, fingers and finalized deformation. Keep cut boundaries.
2. **Animation**: final idle/walk/chase, left/right grabs, hand-to-mouth carry, bite,
   left/right stomp, eye reaction, injured/crawl. Decide native animation vs Humanoid
   retargeting before replacing the prototype Generic clips.
3. **AI**: patrol, FOV/occlusion sight, chase, investigation and search are implemented
   in the separate AI lab. Grab/stomp selection now respects surviving capabilities.
   Next: height-aware mid-air grabs against ODM-swinging players (explicit user
   requirement), production navigation, kneeling attacks and real-player integration.
4. **Attack sequences**: animation-event contact windows, actual hand/mouth overlap
   checks, deduplicated stomp damage, hold/escape/eat, cancel immediately on death,
   arm loss or eye stun. Never let a delayed coroutine kill a released player.
5. **Player combat integration**: inspect real sword references, add swept hit tests
   for ODM speeds, momentum tuning, disabled-player physics while held, release
   ownership, nape precision and death/respawn tests.
6. **Polish**: steam, stump surfaces/decals, sounds, impact feedback, corpse lifetime,
   regeneration if desired, size variants and performance testing.

The separate AI lab patrols/chases its proxy and now has a sandbox-only
[telegraphed stomp](titan-stomp-combat.md) and [grab/hold/release](titan-grab-combat.md).
The passive body prefab still does not acquire AI/attacks by itself. **Eating and
actual player-combat integration remain unimplemented.** Do not assume those work because legacy scripts expose
similarly named methods.
