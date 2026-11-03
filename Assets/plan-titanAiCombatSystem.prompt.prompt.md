# Plan: Titan AI & Combat System

## TL;DR

Add killable titans with limb-based damage, momentum-driven combat, and multiple titan attack types (grab+eat, stomp, direct eat) to the existing ODM gear simulator. Built as a state-machine AI with NavMesh navigation, per-limb destructible colliders, and a nape kill zone. Player gets a health system and can be killed by titan attacks. Damage scales with player momentum at time of sword strike. Titans already have a ragdoll system (`RagdollController.cs`) and an `Animator` set up — on death the existing `RagdollController.EnableRagdoll()` will be called to transition from animated to physics-driven ragdoll.

---

## Phase 1 — Foundation Scripts (no dependencies between files, all parallel)

### Step 1: Create `PlayerHealth.cs`

- New component on the Player GameObject
- Fields: `maxHP`, `currentHP`, `isDead`, `isGrabbed`, `grabbedByTitan` (ref)
- `TakeDamage(float)`, `Die()`, `OnGrabbed(Transform hand)`, `OnReleased()`
- `Die()` triggers existing `RagdollController.EnableRagdoll()`, disables `Movement`, `ODM_Movement`, `Attack`
- Death = scene reload (matches existing pattern in `GameElements.cs` where `player.y <= -25f` triggers reload)

### Step 2: Create `DamageDealer.cs`

- On the player's sword as a trigger collider — activated only during the 0.6s attack window from `Attack.cs`
- `OnTriggerEnter`: checks for `TitanLimb` or `TitanNape` on the collider
- Damage formula: `baseDamage + (playerSpeed * momentumMultiplier)` where `playerSpeed` comes from `ODM_Movement.currentSpeed + residualSpeed`
- Trigger collider (not raycast) — works with high-speed ODM fly-by slashes where raycasts would miss

### Step 3: Create `TitanLimb.cs`

- Attached to each destructible limb collider on the titan
- `LimbType` enum: `LeftArm, RightArm, LeftLeg, RightLeg, Eyes`
- Fields: `limbType`, `maxHP`, `currentHP`, `isDestroyed`
- `DestroyLimb()` notifies parent `TitanController.OnLimbDestroyed(LimbType)`, disables mesh + collider
- Uses the existing `Limbs.physicMaterial` from `Assets/3D Models/`

### Step 4: Create `TitanNape.cs`

- Small trigger collider (0.3-0.5m box) on back of neck — precision target
- Low HP: 1-2 high-momentum hits should kill
- On lethal damage → `TitanController.Die()`

---

## Phase 2 — Titan AI Core (depends on Phase 1)

### Step 5: Create `TitanState.cs`

Enum: `Idle, Patrol, Chase, GrabAttack, EatAttack, StompAttack, Stunned, Crawling, Blind, Dying, Dead`

### Step 6: Create `TitanController.cs` (main AI brain)

- NavMeshAgent-based patrol + chase, state machine in Update
- **References:** `NavMeshAgent`, `Animator` (already on titan), `RagdollController` (already on titan), `TitanLimb[]`, `TitanNape`
- **Detection:** `Physics.OverlapSphere` or distance check (throttled to every 0.25s)
- **State transitions:**
  - *Idle* → random timer → *Patrol* (pick random NavMesh point)
  - *Patrol* → player in detection range → *Chase*
  - *Chase* → in attack range → select attack (Grab/Stomp/Eat based on distance + intact limbs + randomness)
  - *GrabAttack* → hit player → *EatAttack* → player dies
  - *StompAttack* → AoE at foot → back to Chase
- **`Die()`:** Calls existing `RagdollController.EnableRagdoll()` on the titan to switch from animated to physics ragdoll, disables `NavMeshAgent`, sets state to *Dying* → *Dead*
- **`OnLimbDestroyed(LimbType)` handler:**
  - Arm → disable grab for that arm; both arms → no grab attacks
  - Leg → *Crawling* state, halve speed; both legs → immobile
  - Eyes → *Blind* state, stop NavMeshAgent, flail randomly

### Step 7: Create `TitanAttack.cs` (attack execution, separate from controller)

- `ExecuteGrab(hand)` → enable hand trigger, on player hit → parent player to hand
- `ExecuteEat()` → move player to mouth over 1-2s → instant kill
- `ExecuteStomp(foot)` → at animation impact: `Physics.OverlapSphere` → damage
- `ExecuteDirectEat()` → quick bite when player very close on ground

---

## Phase 3 — Unity Editor Setup (parallel with Phase 2 coding)

### Step 8: Import/Create Titan Model

- Import or create a titan humanoid model (scaled 10-15x player)
- Rig as Humanoid in import settings so Mecanim works
- Can start with Mixamo free humanoid for prototyping

### Step 9: Titan Collider & Limb Setup

Titans already have an `Animator` and `RagdollController` with limb Rigidbodies/Colliders/CharacterJoints wired up. Add the **damage-specific** trigger colliders on top of the existing ragdoll colliders:
- **Nape:** small BoxCollider (trigger) on back of neck bone → `TitanNape.cs`
- **Each arm/leg:** CapsuleCollider (trigger) as child of corresponding bone → `TitanLimb.cs`
- **Eyes:** SphereCollider (trigger) on head bone → `TitanLimb.cs`
- The existing `RagdollController` already manages the physics Rigidbodies/Colliders/Joints — no need to add new ones for ragdoll
- Use the existing `Limbs.physicMaterial` on the new trigger colliders

### Step 10: NavMesh & Layer Setup

- Bake NavMesh on FreeRoam scene terrain/ground
- Configure NavMeshAgent with titan-scale agent size
- Create "Titan" layer, assign to titan GameObjects
- **Add titan colliders to ODM_Movement's `targetLayer`** so players can grapple onto titans (core AoT gameplay — works with existing raycast system, zero code changes)

### Step 11: Titan Animator Controller

Titans already have an `Animator` component. Extend the existing controller with new states/parameters:
- **Add states:** Idle, Walk, Chase, GrabLeft, GrabRight, Eat, Stomp, Crawl, BlindFlail (no Death state needed — `RagdollController.EnableRagdoll()` disables the Animator and switches to physics)
- **Add parameters:** `isWalking`, `isChasing`, `isCrawling`, `isBlind` (bools) + `grabLeft`, `grabRight`, `eat`, `stomp` (triggers)
- Source new animations from Mixamo (free, retargetable to humanoid rigs)

### Step 12: Titan Prefab Assembly

- TitanRoot already has `Animator`, `RagdollController`, limb Rigidbodies/Colliders/Joints. Add: `NavMeshAgent`, `TitanController`, `TitanAttack`. Wire up the new trigger colliders (`TitanLimb`, `TitanNape`) and assign references
- Save to `Assets/Prefabs/`
- Place 2-3 in FreeRoam for testing

---

## Phase 4 — Combat Integration (depends on Phase 1 + 2)

### Step 13: Modify `Attack.cs`

- Enable sword trigger collider at attack start, disable after 0.6s window
- Reference `DamageDealer` and toggle it active/inactive

### Step 14: Modify `ODM_Movement.cs`

Add two public read-only properties:
```csharp
public float CurrentSpeed => currentSpeed;
public float ResidualSpeed => residualSpeed;
```
Used by `DamageDealer` for momentum damage calculation. No other changes.

### Step 15: Sword Collider Setup (Unity Editor)

- Add BoxCollider (trigger) to player's sword bone
- Attach `DamageDealer.cs`
- Set `baseDamage` ~10, `momentumMultiplier` ~2-3
- Starts disabled

---

## Phase 5 — Titan Attack Sequences (depends on Phase 2 + 4)

### Step 16: Grab Sequence

- Titan grab animation → hand trigger collider → on player hit: parent player to hand, disable movement, camera follows
- Optional escape QTE (mash F). If not escaped in ~3s → eat → death

### Step 17: Stomp Attack

- Stomp animation → Animation Event at impact frame → `Physics.OverlapSphere(foot, radius)` → damage player
- Optional: brief player ragdoll on hit

### Step 18: Direct Eat

- When player within mouth range and slow/grounded → quick snap animation → instant kill
- Rarer attack, punishes standing still

---

## Phase 6 — Polish (depends on all above)

### Step 19: Visual/Audio Feedback

- Steam particles on limb destruction
- Screen shake on stomp (reuse `camAnim` from ODM_Movement)
- Distance-based titan footstep audio
- Hit sound differentiation (limb vs nape)

### Step 20: Balance Tuning

- Nape dies in 1-2 high-speed hits
- Limbs take 2-4 hits
- Walking-speed attacks do minimal damage (encourages ODM usage)
- Grab escape difficulty
- Titan attack cooldowns

---

## Relevant Files

**Modify:**
- `Assets/Attack.cs` — Sword collider activation during attack window
- `Assets/ODM_Movement.cs` — Expose `currentSpeed`/`residualSpeed` as public properties

**Create:**
- `Assets/Titan/TitanController.cs`, `TitanAttack.cs`, `TitanLimb.cs`, `TitanNape.cs`, `TitanState.cs`
- `Assets/PlayerHealth.cs`, `Assets/DamageDealer.cs`

**Existing titan infrastructure (already set up):**
- `Assets/RagdollController.cs` — Already on titans. `EnableRagdoll()` called on titan death to switch from animated to physics ragdoll
- Titan `Animator` — Already on titans. Extended with new states/parameters for AI behaviors

**Reference patterns from:**
- `Assets/GameElements.cs` — Scene reload on death
- `Assets/Scripts/Audio.cs` — Reuse for titan sounds

**Unity Editor actions required:**
- Import/create titan model if not already available (Step 8)
- Add damage trigger colliders on top of existing ragdoll colliders (Step 9)
- Bake NavMesh on FreeRoam scenes (Step 10)
- Extend existing titan Animator controller with AI states + source new animations (Step 11)
- Add NavMeshAgent, TitanController, TitanAttack to existing titan prefab (Step 12)
- Add sword trigger collider to player (Step 15)

---

## Verification

1. Slash titan arm at various speeds → damage scales with momentum
2. High-speed nape slash → titan enters Dying state, ragdolls
3. Titan patrols between random NavMesh points autonomously
4. Player enters detection range → titan pursues
5. Titan grabs player → player parented to hand, movement disabled, then eaten
6. Stand near feet → stomp AoE damages player
7. Destroy one leg → crawling; destroy eyes → blind/stationary; destroy arm → no grab
8. Stationary attack vs full-speed ODM → dramatically different damage
9. Full loop: ODM → slash limbs → nape kill → ragdoll → no errors
10. Player death → ragdoll → scene reload

---

## Decisions

- **Momentum source:** `ODM_Movement.currentSpeed + residualSpeed` (CharacterController, not Rigidbody)
- **Collision method:** Trigger colliders (not raycasts) — reliable at ODM speeds
- **AI architecture:** Simple state machine, not behavior tree — appropriate for scope
- **Death = scene reload** — matches existing pattern, avoids respawn complexity
- **New scripts in `Assets/Titan/`** subfolder for organization
- **Grapple-to-titan: YES** — just add titan to `targetLayer`, works with existing raycast system with zero code changes

## Further Considerations

1. **Titan model source:** Recommend starting with Mixamo's free humanoid scaled up for prototyping, replace with proper model later
2. **Titan regeneration:** In AoT lore, titans regenerate. Skip for v1, add as difficulty modifier later
3. **Titan size variants:** (3m, 7m, 15m classes) — defer to v2, get one size working first
