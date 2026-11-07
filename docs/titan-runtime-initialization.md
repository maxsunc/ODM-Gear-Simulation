# Reusable Titan initialization and spawn API

Unity **2021.3.25f1**, scene opt-in. No Unity/pipeline upgrades, animation rebuilds,
new control bindings, global Physics changes, physical catch widening, or runtime bake.

## Checkpoint first

At user request, committed **b6447972236dca8a2e34d5cf7eec733078420406** and pushed
`origin/titan-combat-updates` (remote confirmed). It includes mirrored Scout attacks,
regional damage/effects and the user's then-saved FreeRoam setup, extra placed Titans,
perception changes and enlarged face/nape trigger edits. Earlier local checkpoints
through059dd52e were pushed too.75 paths, no>95MiB/sensitive-name flags; locked`.vs`
cache and Python bytecode excluded. No force push. The runtime work BELOW is subsequent,
**uncommitted review work**, not part of that pushed checkpoint.

## Following step: random population (implemented, opt-in)

Random weighted-size spawning with staged safe reservations, strike FX/optional user
sound, population/placement limits and own corpse cleanup is now implemented separately;
see **docs/titan-random-spawning.md**. Manager TrySpawn now composes TryPrepareSpawn +
TryActivatePreparedSpawn; inactive full-rig bounds reserve warnings. New spawner choices
join bake-size collection. Latest193/195 (same2 Run failures) plus1/1 owned runtime
URP capture. Main scene installation is a one-time menu, not yet automated. Still
uncommitted afterb6447972. Earlier scope notes below describe the initializer milestone.

## Using it now

On **FreeRoam Titan navigation TEST / Titan Free Roam Integration**:

- **Auto Register Scene Titans** defaults ON: discovers active placed bodies once,
  then receives queued `ModularTitanBody.BecameActive` events for new/re-enabled bodies.
  No recurring full-map scan, no need to add every enemy to the old Titans array.
- **Enable Combat** controls the owned stomp/grab/bite/director stack; changing it
  live cancels existing attacks before disabling, and can re-enable safely.
- **Use Regional Health** defaults ON for newly initialized bodies. Existing custom
  HP values are respected; registration/re-enable NEVER heals or resets wounds.
- **Configure Titan Layer / Titan Layer** defaults ON/9 for normal grapple targets.
  Changes only the registered actor hierarchy, never global layer-collision settings.
- **Show Diagnostics**: active/registered/failure counts and individual failure reasons.
  Inactive placed actors stay inactive; enable them to queue registration. The old
  explicit Titans list still works, including when auto registration is OFF.

After Unity compiles the changes, start a fresh Play session. New plain passive prefab
instances in this scene initialize on the next manager Update, after the caller has
assigned their transform/scale/scene. For scripted spawning prefer the atomic API:

```csharp
// sceneManager is the scene's TitanFreeRoamIntegration reference.
// prefab: Assets/Prefabs/Titans/ModularTitanPrototype.prefab (passive source).
if (!sceneManager.TrySpawn(prefab, desiredPosition, Quaternion.Euler(0, yaw, 0), 3f,
        out ModularTitanBody titan, out string error))
    Debug.LogWarning(error);

// Later, for an actor owned by this manager:
sceneManager.Despawn(titan);
```

`TryRegister(ModularTitanBody,out string)` supports explicit custom spawner registration
and retries after correcting failed placement. Duplicate calls on a ready actor do not
warp it, restart attacks, reassign/duplicate components or reset health. Cross-scene
bodies, another owner's actors and existing external AI/attacks are rejected instead of
being silently taken over. Move a body into the desired scene BEFORE registration.

`TrySpawn` clones into an owned INACTIVE staging root in the correct scene, sets scale,
validates/configures it, detaches inactive, then activates. No clone's agent or attack can
run against the wrong size/player before configuration. Failure returns false/null,
cleans its failed clone, and leaves existing actors/data untouched. Despawn suspends,
cancels/releases holds, unregisters and destroys only owned actors. Existing detached
limb debris still follows the body's historical lifetime (not immediate debris cleanup).
No timed/wave spawning UI or spawn-distribution design was added in this step.

## Navigation: once per world/size, NOT per enemy

Production scene retains its existing **3x and5x** baked profiles. Additional enemies
at those scales share the registered data. Raw1x or arbitrary random scales have NO
matching profile yet; fail visibly rather than borrowing smaller clearance.

For a future new size, add it to **Additional Bake Scales**, save in Edit Mode and use
**Tools > Titans > FreeRoam > Rebake full-map navigation** ONCE. This setting declares
sizes only: it never bakes in Play. Bake size collection unions explicit/discovered
scene actor sizes, existing profiles and additional sizes, canonicalized to.001. Thus
removing the last placed Titan of a size does not delete that spawner's existing profile,
and no placeholder Titan is needed for future sizes. Static map collision changes
still need a rebake; spawning/despawning/moving an actor on clear existing data does not.
Choose from baked size categories rather than generating infinitely many random sizes.
No bake/scene save was invoked during this implementation; nav assets/IDs unchanged.

Placement uses only a bounded requested-radius projection (default3m) on the matching
agent type. It then checks the world-sized navigation capsule AND each actual solid
rig primitive, including outstretched arms beyond that capsule, in the owning scene's
PhysicsScene. Self solids/triggers ignored; scenery, player, other enemies and corpses
NOT ignored.64-entry saturated queries fail closed; no distant placement fallback.
Tiny.01m shape inset avoids treating mere floor contact as penetration. This is spawn
validation, NOT a claim of solved full-body clearance throughout later traversal.

Unsupported/nonuniform/nonfinite scale, tilted/nonfinite rotation, off-mesh ground,
solid overlap, invalid source/target or duplicate ownership returns explicit error.
After failure, fix the point/profile then retry; no surprise continuous spawn retries.
Initialize a fresh actor at its FINAL scale; changing a registered scale is rejected on
re-registration/resume and suspends its navigation without rebuilding/healing it.

## Ownership and lifecycle

`TitanRuntimeActor` owns exactly one NavMeshAgent, perception brain, stomp, grab/bite and
director. Agent is added while inactive, configured before enabling, then bound to its
existing profile; local radius1.8/height12 apply giant world scale ONCE. Brain precedes
attacks so their Awake caches find the correct dependencies. Movement/attack tuning,
physical contact/telegraphs/reach/bite semantics remain unchanged.

Disabling an actor/manager or navigation suspends owned navigation/attacks and releases
its victim; re-enable resumes after safe placement validation, preserving HP/injuries.
If something now obstructs the actor, resumption can fail visibly until corrected.
Dead rigs are not resurrected; unowned severed rigs are not adopted. This supports
healthy activation/deactivation, NOT reconstruction/pooling of dismembered ragdolls.
Death, limb repair, full corpse pooling and replacement player-root retargeting remain
separate lifecycle designs. Other scenes remain passive unless their own manager opts in.
Lifecycle event listeners are unsubscribed on disable and reset at Play subsystem start.

## Validation

Owned disposable Unity2021 runtime suite **181/183 passed TWICE**,2026-11-04
**15:27:52–15:28:09 UTC**,PID19600,16.665s and **15:30:27–44 UTC**,PID47984,16.912s.
Final audit byte-matched178 runtime/test/art/effects/Editor files to the completed run.
Only the SAME two approved-retimed Run
assertions fail; no gait or strict physics checks weakened.14 new cases cover:

- late plain clones / placed actors with empty list;3x/5x profile + scale-once avoidance;
- atomic spawns, shared unchanged triangulation, crowd/off-mesh/invalid scale rejection;
- wide actual arm/scenery rejection outside the navigation cylinder;
- duplicate registration, wounds retained, combat toggling;
- actor/manager/navigation suspend-resume and ownership protection;
- inactive actors/explicit-only policy and scene isolation;
- future-size declaration/retention without hidden runtime baking;
- initialized-scale mutation rejection;
- spawned director actually selecting a stomp and inflicting40 damage once;
- actual grab contact/lease followed by despawn release; no dead-actor resurrection.

Earlier179/181 and180/182 runs had only the same2 Run failures before the final extra
case. Tests use newly owned nav settings/data removed afterwards; no live-editor scene
simulations, user Play controls, main scene reload/saves or production navigation bake.
Main scene, shared prefab (including user's enlarged hitboxes), art, animation and
NavMesh assets remain byte-unchanged from pushed checkpoint during this step.
Full-speed scaled/crowd combat, player/terrain/corpse clearance in natural gameplay and
user aesthetic/balance review are still outstanding. Runtime registration tests are
not comprehensive giant-combat or whole-gameplay approval.
