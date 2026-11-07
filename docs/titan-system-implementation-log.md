# Titan AI & Combat System — Implementation Log

This document captures the full conversation history of planning and implementing a Titan AI system for an Attack on Titan ODM gear simulator in Unity. It demonstrates multi-phase planning, codebase analysis, and systematic implementation using GitHub Copilot.

---

## Session 1: Planning Phase

### User Request

> This is a Unity C# project — currently just a realistic ODM gear simulator with attacking (no HP system). The next step in the game is to add titans and titan behavior. The idea is we want titans to walk around and the player is able to kill them if they attack on the nape. Titans should be able to grab (then eat), stomp, and straight eat a player. The player has an attack with the sword, so if they are able to destroy a limb (arm or leg) it will disable a titan somehow (for example: one leg destroyed → slow crawling titan, eyes destroyed → blind → stationary, arm destroyed → no more grabbing for that arm). The damaging should be momentum-based. Could you write up a plan for this and store it as an MD file? Implementing this into the codebase will be a difficult task, so take a look at the current codebase to see how you would integrate this. You will be able to command me to do anything in the Unity Editor to make it work.

### Copilot Response

Copilot launched parallel subagent explorations to thoroughly analyze the existing codebase:

1. **Movement & ODM Systems** — Read `Movement.cs`, `ODM_Movement.cs`, `Attack.cs`, `GameElements.cs`
2. **Camera & Scene Systems** — Read `ThirdPersonCam.cs`, `ThirdPersonCameraController.cs`, `CameraJitterFix.cs`, `SceneManagement.cs`, `MenuManager.cs`, `ShowFPS.cs`, `Zoom.cs`
3. **Project Structure** — Explored `Assets/Scripts/`, `Assets/Prefabs/`, `Assets/Models/`, `Assets/Scenes/`, `Assets/Animas/`, `Assets/Resources/`

#### Key Findings from Codebase Analysis

| System | Key Details |
|--------|-------------|
| **Movement** | `CharacterController`-based, gravity toggle, ground check via `Physics.OverlapBox` |
| **ODM Grappling** | Raycast-based hooks (Q/E or mouse), gas acceleration, momentum/inertia system with `currentSpeed` and `residualSpeed` |
| **Attack** | F-key hold attack with 0.6s animation window, `holdingAttack` animator bool |
| **Ragdoll** | `RagdollController.cs` with `EnableRagdoll()`/`DisableRagdoll()`, limb Rigidbody/Collider arrays |
| **Death Pattern** | Scene reload when `player.y <= -25f` (in `GameElements.cs`) |

Copilot then generated a comprehensive 6-phase implementation plan covering:
- Phase 1: Foundation scripts (PlayerHealth, DamageDealer, TitanLimb, TitanNape)
- Phase 2: Titan AI core (TitanState enum, TitanController state machine, TitanAttack)
- Phase 3: Unity Editor setup (colliders, NavMesh, layers, animator)
- Phase 4: Combat integration (modify Attack.cs, ODM_Movement.cs)
- Phase 5: Titan attack sequences (grab, stomp, eat)
- Phase 6: Polish (particles, audio, balance tuning)

---

### User Request

> For more context, we already have a ragdoll system implemented for titans and an animator. The idea is we'll trigger the ragdoll when titans die — it will use RagdollController. Update the plan accordingly.

### Copilot Response

Updated the plan across 7 sections to reflect existing titan infrastructure:

- **TL;DR** — Notes the existing ragdoll/animator infrastructure
- **Step 6 (TitanController)** — `Die()` now explicitly calls existing `RagdollController.EnableRagdoll()`, disables NavMeshAgent
- **Step 9 (Collider setup)** — Damage trigger colliders are added *on top of* existing ragdoll colliders, no new Rigidbodies/Joints needed
- **Step 11 (Animator)** — Extends the existing controller instead of creating a new one; removed Death animation state (ragdoll handles that)
- **Step 12 (Prefab)** — Adds new components to existing titan, not building from scratch
- **Relevant Files** — `RagdollController.cs` listed as existing titan infrastructure, not just a reference pattern

---

### User Request

> So how should my next steps proceed if I want to implement this? First I'll create a new context window, so how should I implement this plan? Tell me step by step.

### Copilot Response

Provided a detailed implementation roadmap organized into 5 rounds:

#### Round 1 — Code-Only (No Unity Editor Needed Yet)
1. Create `Assets/Titan/TitanState.cs` — Enum file
2. Create `Assets/Titan/TitanLimb.cs` — Limb health component
3. Create `Assets/Titan/TitanNape.cs` — Nape kill zone
4. Create `Assets/PlayerHealth.cs` — Player HP and death handling
5. Create `Assets/DamageDealer.cs` — Sword trigger with momentum damage
6. Modify `Assets/ODM_Movement.cs` — Expose `CurrentSpeed` and `ResidualSpeed` properties
7. Modify `Assets/Attack.cs` — Toggle DamageDealer during attack window

#### Round 2 — Titan AI (Depends on Round 1)
8. Create `Assets/Titan/TitanController.cs` — Full state machine
9. Create `Assets/Titan/TitanAttack.cs` — Grab, stomp, eat execution

#### Round 3 — Unity Editor Setup
10. Download titan animations from Mixamo
11. Create `Assets/Titan/` folder
12. Set up Animator controller with new parameters/states
13. Add damage trigger colliders to titan bones
14. Add NavMeshAgent, TitanController, TitanAttack to titan root
15. Create "Titan" layer, add to ODM targetLayer
16. Bake NavMesh on FreeRoam scenes
17. Set up player sword collider with DamageDealer
18. Add PlayerHealth to Player GameObject
19. Save titan prefab

#### Round 4 — First Playtest
20. Test patrol, chase, grappling, damage, nape kills, death sequences
21. Fix bugs from playtest

#### Round 5 — Polish
22. Steam particles on limb destruction
23. Screen shake on stomp
24. Audio feedback
25. Balance tuning

---

## Session 2: Implementation Phase

### User Request

> Thoroughly read over this plan and the codebase, and then based on this plan, create the scripts that will be needed. At the end, let me know what to do in the Unity Editor.

### Copilot Response

Created 11 todos and systematically implemented all scripts.

#### Scripts Created

| Script | Location | Purpose |
|--------|----------|---------|
| `PlayerHealth.cs` | `Assets/PlayerHealth.cs` | Player HP, grab state, death → ragdoll → scene reload |
| `DamageDealer.cs` | `Assets/DamageDealer.cs` | Sword trigger collider — momentum-scaled damage to limbs/nape |
| `TitanState.cs` | `Assets/Titan/TitanState.cs` | State enum (Idle, Patrol, Chase, attacks, Crawling, Blind, etc.) |
| `TitanLimb.cs` | `Assets/Titan/TitanLimb.cs` | Per-limb HP, destruction → notifies controller |
| `TitanNape.cs` | `Assets/Titan/TitanNape.cs` | Nape kill zone — lethal damage calls `TitanController.Die()` |
| `TitanController.cs` | `Assets/Titan/TitanController.cs` | AI brain: NavMesh patrol/chase, state machine, limb-loss reactions |
| `TitanAttack.cs` | `Assets/Titan/TitanAttack.cs` | Grab+eat, stomp AoE, direct eat sequences via coroutines |

#### Scripts Modified

- **`Attack.cs`** — Added `DamageDealer` reference; enables sword collider at slash start, disables after 0.6s window
- **`ODM_Movement.cs`** — Added `public float CurrentSpeed` and `public float ResidualSpeed` read-only properties

---

### Unity Editor Instructions

#### 1. Player Setup

- Add `PlayerHealth` component to your Player GameObject. Assign its `ragdollController` reference (if the player has one)
- Add a `BoxCollider` (trigger) as a child of the player's sword bone. Attach `DamageDealer` to it. Set `baseDamage` ~10, `momentumMultiplier` ~2.5. Start the GameObject **disabled** (unchecked in inspector)
- On the `Attack` component, drag the sword's `DamageDealer` into the new **Damage Dealer** field
- Make sure the Player GameObject has the **"Player"** tag (`TitanController` uses `FindGameObjectWithTag("Player")`)

#### 2. Titan Model & Rig

- Import a humanoid model (Mixamo free humanoid works) scaled 10-15x player size
- Set import rig to **Humanoid** so Mecanim retargeting works
- The titan should already have an `Animator` and `RagdollController` with limb Rigidbodies/Colliders/CharacterJoints

#### 3. Titan Damage Colliders (Layered on Top of Existing Ragdoll Colliders)

| Body Part | Collider Type | Component | Settings |
|-----------|---------------|-----------|----------|
| **Nape** | BoxCollider (trigger) ~0.3-0.5m | `TitanNape` | On back of neck bone |
| **Each Arm** | CapsuleCollider (trigger) | `TitanLimb` | Set `limbType` to LeftArm/RightArm |
| **Each Leg** | CapsuleCollider (trigger) | `TitanLimb` | Set `limbType` to LeftLeg/RightLeg |
| **Eyes** | SphereCollider (trigger) | `TitanLimb` | Set `limbType` to Eyes, on head bone |

Optionally assign the `Limbs.physicMaterial` from `Assets/3D Models/` to these colliders.

#### 4. Titan Prefab Assembly

- Add `NavMeshAgent` to the titan root. Set agent size appropriate for titan scale (radius ~3-5, height ~15)
- Add `TitanController` and `TitanAttack` components to the titan root
- On `TitanController`: wire up references — `NavMeshAgent`, `Animator`, `RagdollController`, all `TitanLimb[]` colliders, `TitanNape`, `TitanAttack`, and the body part transforms (`leftHand`, `rightHand`, `leftFoot`, `rightFoot`, `mouth`)
- On `TitanAttack`: set `playerLayer` to whatever layer the Player is on
- Save as prefab in `Assets/Prefabs/`

#### 5. NavMesh

- Open the FreeRoam scene → **Window > AI > Navigation**
- Bake a NavMesh on the terrain/ground with titan-scale agent size

#### 6. Layers

- Create a **"Titan"** layer, assign it to all titan GameObjects
- Add the Titan layer to `ODM_Movement`'s `targetLayer` mask — this lets players grapple onto titans with zero code changes

#### 7. Titan Animator Controller

Extend the existing titan Animator controller with these parameters:

| Type | Parameters |
|------|------------|
| **Bools** | `isWalking`, `isChasing`, `isCrawling`, `isBlind` |
| **Triggers** | `grabLeft`, `grabRight`, `eat`, `stomp` |

Add states: **Idle, Walk, Chase, GrabLeft, GrabRight, Eat, Stomp, Crawl, BlindFlail**

No Death state needed — `RagdollController.EnableRagdoll()` handles the death transition.

Source animations from [Mixamo](https://www.mixamo.com) (free, retargetable to humanoid rigs).

#### 8. Test

- Place 2-3 titan prefabs in the FreeRoam scene
- Verify: **patrol → chase → attack → limb destruction → nape kill → ragdoll death**

---

## Summary

This implementation session demonstrated:

1. **Codebase Analysis** — Parallel exploration of existing systems to understand integration points
2. **Phased Planning** — Breaking complex features into dependent phases with clear milestones
3. **Code Generation** — Creating 7 new scripts and modifying 2 existing ones
4. **Editor Instructions** — Detailed steps for Unity setup that complement the generated code

The titan system integrates with existing infrastructure:
- Uses `RagdollController` pattern from player for titan death
- Leverages `ODM_Movement.currentSpeed/residualSpeed` for momentum-based damage
- Matches scene reload pattern from `GameElements.cs` for player death
- Works with existing raycast grapple system via layer configuration
