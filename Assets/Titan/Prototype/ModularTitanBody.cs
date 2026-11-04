using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum TitanHitRegion { LeftArm, RightArm, LeftLeg, RightLeg, Eyes, Nape }

[Serializable]
public class TitanRagdollBone
{
    public Rigidbody body;
    public Collider collider;
}

[Serializable]
public class TitanDetachableLimb
{
    public TitanHitRegion region;
    public Transform rootBone;
    public Rigidbody[] bodies;
    public Renderer[] renderers;
    public TitanHitZone[] hitZones;
    [NonSerialized] public bool severed;
    [NonSerialized] public float health;
}

/// <summary>
/// Titan body/physics foundation, independent of navigation and attack AI.
/// Animated bones stay kinematic with solid colliders for grappling. Damage zones
/// are separate triggers. Severing detaches the actual bone branch AND its skins.
/// </summary>
[DisallowMultipleComponent]
public class ModularTitanBody : MonoBehaviour
{
    public Animator animator;
    public Transform hips;
    public Transform head;
    public Transform leftUpperArm, leftLowerArm, leftHand;
    public Transform rightUpperArm, rightLowerArm, rightHand;
    public Transform mouthSocket, leftGripSocket, rightGripSocket;
    public TitanRagdollBone[] ragdollBones = new TitanRagdollBone[0];
    public TitanDetachableLimb[] limbs = new TitanDetachableLimb[0];
    public TitanHitZone[] hitZones = new TitanHitZone[0];

    [Header("Damage")]
    [Tooltip("Legacy lab health. Scene-installed regional health takes precedence.")]
    [Min(1)] public float limbHealth = 60f;
    [Tooltip("Enable for the new sword-combat balance; legacy physics labs retain their original damage fixture.")]
    public bool useRegionalHealth;
    [Min(1)] public float armHealth = 1000f, legHealth = 2500f, faceHealth = 500f, napeHealth = 500f;
    [Header("Sever impact (regional-health profile)")]
    [Range(0,12)] public float severLaunchSpeed = 7f;
    [Range(0,3)] public float severUpwardSpeed = .65f;
    [Range(0,1.5f)] public float severSpinSpeed = .8f;
    [Min(0.1f)] public float eyeStunDuration = 5f;
    [Min(0)] public float detachedLifetime = 30f;

    public bool IsDead { get; private set; }
    public bool IsBlind { get { return !IsDead && blindTimeRemaining > 0; } }
    public float BlindTimeRemaining { get { return blindTimeRemaining; } }
    public bool WalkingRequested { get { return wantsToWalk; } }
    public bool IsHelpless { get { return IsSevered(TitanHitRegion.LeftLeg) && IsSevered(TitanHitRegion.RightLeg); } }
    public bool CanGrabLeft { get { return !IsDead && !IsBlind && !IsHelpless && !IsSevered(TitanHitRegion.LeftArm); } }
    public bool CanGrabRight { get { return !IsDead && !IsBlind && !IsHelpless && !IsSevered(TitanHitRegion.RightArm); } }
    public bool CanWalk { get { return !IsDead && !IsBlind && !IsSevered(TitanHitRegion.LeftLeg) && !IsSevered(TitanHitRegion.RightLeg); } }
    public event Action<TitanHitRegion> LimbSevered;
    public event Action Died;
    public event Action<bool> BlindnessChanged;

    readonly HashSet<Rigidbody> detachedBodies = new HashSet<Rigidbody>();
    bool initialized;
    bool wantsToWalk;
    float blindTimeRemaining;
    bool regionalHealthInitialized;
    float faceRemaining, napeRemaining;
    float blindPoseWeight;
    float eyeReactionAge;
    int eyeReactionVariant;
    static readonly int Walking = Animator.StringToHash("IsWalking");

    void Awake() { Initialize(); }

    // Explicit initialization also allows isolated Editor physics validation without
    // entering Play Mode or running scripts in the user's gameplay scene.
    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        foreach (var limb in limbs) { limb.health = limbHealth; limb.severed = false; }
        EnsureRegionalHealth();
        foreach (var zone in hitZones) if (zone != null) zone.owner = this;
        for (int i = 0; i < ragdollBones.Length; i++)
        {
            var entry = ragdollBones[i];
            if (entry.body == null) continue;
            entry.body.isKinematic = true;
            entry.body.useGravity = false;
            entry.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // Animator/NavMeshAgent own these transforms. PhysX interpolation can
            // restore stale WORLD poses and leave the skin behind a moving root.
            entry.body.interpolation = RigidbodyInterpolation.None;
            entry.body.solverIterations = 12;
            entry.body.solverVelocityIterations = 4;
            if (entry.collider == null) continue;
            entry.collider.enabled = true;
        }
        IgnoreInternalCollisions();
    }

    void IgnoreInternalCollisions()
    {
        // Reapply after activating physics as well: collider/actor lifecycle changes
        // must not restore contacts at the overlapping anatomical seams.
        for (int i = 0; i < ragdollBones.Length; i++)
            for (int j = i + 1; j < ragdollBones.Length; j++)
                if (ragdollBones[i].collider != null && ragdollBones[j].collider != null)
                    Physics.IgnoreCollision(ragdollBones[i].collider, ragdollBones[j].collider, true);
    }

    void Update() { TickStatus(Time.deltaTime); }

    public void TickStatus(float deltaTime)
    {
        if (IsDead || blindTimeRemaining <= 0 || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        eyeReactionAge += deltaTime;
        blindTimeRemaining = Mathf.Max(0, blindTimeRemaining - deltaTime);
        if (blindTimeRemaining <= 0)
        {
            if (useRegionalHealth) faceRemaining = GetMaximumHealth(TitanHitRegion.Eyes);
            UpdateLocomotion();
            if (BlindnessChanged != null) BlindnessChanged(false);
        }
    }

    void LateUpdate()
    {
        if (IsDead || head == null) return;
        blindPoseWeight = Mathf.MoveTowards(blindPoseWeight, IsBlind ? 1 : 0, Time.deltaTime * (IsBlind ? 18 : 10));
        if (blindPoseWeight <= 0) return;
        // Fast recoil first, then staggered hand coverage. Repeated eye hits restart
        // the flinch and alternate the leading hand without touching severed arms.
        float flinch = IsBlind ? Mathf.Sin(Mathf.Min(1,eyeReactionAge / .055f) * Mathf.PI * .5f) * Mathf.Exp(-eyeReactionAge * 16) : 0;
        head.rotation = Quaternion.AngleAxis(-18 * flinch, transform.right) * head.rotation;
        float leftDelay = eyeReactionVariant % 2 == 0 ? .025f : .075f;
        float rightDelay = eyeReactionVariant % 2 == 0 ? .075f : .025f;
        CoverEye(leftUpperArm, leftLowerArm, leftHand, TitanHitRegion.LeftArm, blindPoseWeight * Mathf.Clamp01((eyeReactionAge-leftDelay)/.10f));
        CoverEye(rightUpperArm, rightLowerArm, rightHand, TitanHitRegion.RightArm, blindPoseWeight * Mathf.Clamp01((eyeReactionAge-rightDelay)/.10f));
    }

    void CoverEye(Transform upper, Transform lower, Transform hand, TitanHitRegion region, float weight)
    {
        if (upper == null || lower == null || hand == null || IsSevered(region)) return;
        float side = Mathf.Sign(transform.InverseTransformPoint(upper.position).x);
        Vector3 eye = head.position + transform.up * 0.65f + transform.forward * 0.77f + transform.right * side * 0.34f;
        Vector3 elbow = head.position - transform.up * 0.65f + transform.right * side * 1.7f + transform.forward * 0.55f;
        Aim(upper, lower, elbow, weight);
        Aim(lower, hand, eye, weight);
    }

    static void Aim(Transform bone, Transform child, Vector3 target, float weight)
    {
        Vector3 current = child.position - bone.position;
        Vector3 desired = target - bone.position;
        if (current.sqrMagnitude < 0.001f || desired.sqrMagnitude < 0.001f) return;
        Quaternion rotation = Quaternion.FromToRotation(current, desired) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, rotation, weight);
    }

    public bool IsSevered(TitanHitRegion region)
    {
        foreach (var limb in limbs) if (limb.region == region) return limb.severed;
        return false;
    }

    public void SetWalking(bool walking)
    {
        wantsToWalk = walking;
        UpdateLocomotion();
    }

    void UpdateLocomotion()
    {
        if (animator != null && animator.runtimeAnimatorController != null && !IsDead)
            animator.SetBool(Walking, wantsToWalk && CanWalk);
    }

    void EnsureRegionalHealth()
    {
        if (!useRegionalHealth || regionalHealthInitialized) return;
        regionalHealthInitialized = true;
        foreach (var limb in limbs) limb.health = limb.severed ? 0 : GetMaximumHealth(limb.region);
        faceRemaining = GetMaximumHealth(TitanHitRegion.Eyes); napeRemaining = GetMaximumHealth(TitanHitRegion.Nape);
    }

    public float GetMaximumHealth(TitanHitRegion region)
    {
        if (!useRegionalHealth) return region == TitanHitRegion.Eyes || region == TitanHitRegion.Nape ? 1 : limbHealth;
        switch (region)
        {
            case TitanHitRegion.LeftArm: case TitanHitRegion.RightArm: return Mathf.Max(1,armHealth);
            case TitanHitRegion.LeftLeg: case TitanHitRegion.RightLeg: return Mathf.Max(1,legHealth);
            case TitanHitRegion.Eyes: return Mathf.Max(1,faceHealth);
            case TitanHitRegion.Nape: return Mathf.Max(1,napeHealth);
            default: return 0;
        }
    }

    public float GetHealth(TitanHitRegion region)
    {
        Initialize(); EnsureRegionalHealth();
        if (region == TitanHitRegion.Nape) return IsDead ? 0 : useRegionalHealth ? napeRemaining : 1;
        if (region == TitanHitRegion.Eyes) return IsBlind || IsDead ? 0 : useRegionalHealth ? faceRemaining : 1;
        foreach (var limb in limbs) if (limb.region == region) return limb.health;
        return 0;
    }

    public void ApplyDamage(TitanHitRegion region, float damage, Vector3 point, Vector3 impulse)
    { ReceiveDamage(region,damage,point,impulse); }

    public TitanDamageResult ReceiveDamage(TitanHitRegion region, float damage, Vector3 point, Vector3 impulse)
    {
        Initialize(); EnsureRegionalHealth();
        var result = new TitanDamageResult();
        if (IsDead || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return result;
        if (!useRegionalHealth && region == TitanHitRegion.Eyes && IsBlind) { StunEyes(); return result; }
        float before = GetHealth(region);
        if (before <= 0) return result; // Missing limbs, corpses and already-blind eyes give no hit feedback.
        if(float.IsNaN(impulse.sqrMagnitude)||float.IsInfinity(impulse.sqrMagnitude))impulse=Vector3.zero;
        if(float.IsNaN(point.sqrMagnitude)||float.IsInfinity(point.sqrMagnitude))point=transform.position;
        float remaining = !useRegionalHealth && (region==TitanHitRegion.Nape||region==TitanHitRegion.Eyes) ? 0 : Mathf.Max(0,before-damage);
        var outcome = TitanDamageOutcome.Hit;
        if (region == TitanHitRegion.Nape)
        {
            napeRemaining = remaining;
            if (remaining <= 0) { Die(impulse); outcome = TitanDamageOutcome.Killed; }
        }
        else if (region == TitanHitRegion.Eyes)
        {
            faceRemaining = remaining;
            if (remaining <= 0) { StunEyes(); outcome = TitanDamageOutcome.Blinded; }
        }
        else foreach (var limb in limbs)
        {
            if (limb.region != region || limb.severed) continue;
            limb.health = remaining;
            if (remaining <= 0 && Sever(region,impulse)) outcome = TitanDamageOutcome.Severed;
            break;
        }
        result = new TitanDamageResult { Body=this, Region=region, Outcome=outcome, Damage=damage,
            AppliedDamage=Mathf.Min(before,damage), RemainingHealth=remaining, Point=point,
            Direction=impulse.sqrMagnitude>.000001f ? impulse.normalized : transform.forward };
        return result;
    }

    public void StunEyes()
    {
        if (IsDead) return;
        bool wasBlind = IsBlind;
        blindTimeRemaining = eyeStunDuration;
        eyeReactionAge = 0;
        eyeReactionVariant++;
        UpdateLocomotion();
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        if (!wasBlind && BlindnessChanged != null) BlindnessChanged(true);
    }

    public bool Sever(TitanHitRegion region, Vector3 impulse)
    {
        Initialize();
        if (IsDead) return false;
        foreach (var limb in limbs)
        {
            if (limb.region != region || limb.severed || limb.rootBone == null) continue;
            limb.severed = true;
            limb.health = 0;
            var detached = new GameObject(name + "_" + region + "_Detached");
            detached.layer = gameObject.layer;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(detached, gameObject.scene);
            detached.transform.position = limb.rootBone.position;
            detached.transform.rotation = limb.rootBone.rotation;
            var joint = limb.rootBone.GetComponent<CharacterJoint>();
            if (joint != null)
            {
                if (Application.isPlaying) Destroy(joint); else DestroyImmediate(joint);
            }
            limb.rootBone.SetParent(detached.transform, true);
            foreach (var renderer in limb.renderers)
                if (renderer != null) renderer.transform.SetParent(detached.transform, true);
            foreach (var zone in limb.hitZones) if (zone != null) zone.gameObject.SetActive(false);
            RebindAnimatorPreservingDetachedPose(detached.transform);
            Physics.SyncTransforms();
            RebaseJoints(limb.bodies);
            foreach (var rb in limb.bodies)
            {
                if (rb == null) continue;
                detachedBodies.Add(rb);
                MakeDynamic(rb);
            }
            IgnoreInternalCollisions();
            if (useRegionalHealth) LaunchSeveredBranch(limb,impulse);
            else if (limb.bodies.Length > 0 && limb.bodies[0] != null)
                limb.bodies[0].AddForce(Vector3.ClampMagnitude(impulse, 250), ForceMode.Impulse);
            if (Application.isPlaying && detachedLifetime > 0) Destroy(detached, detachedLifetime);
            UpdateLocomotion();
            if (LimbSevered != null) LimbSevered(region);
            return true;
        }
        return false;
    }

    void LaunchSeveredBranch(TitanDetachableLimb limb, Vector3 impulse)
    {
        if (impulse.sqrMagnitude < .000001f || float.IsNaN(impulse.sqrMagnitude) || float.IsInfinity(impulse.sqrMagnitude)) return;
        Vector3 center=Vector3.zero;float mass=0;
        foreach(var rb in limb.bodies) if(rb!=null) { center+=rb.worldCenterOfMass*rb.mass;mass+=rb.mass; }
        if(mass<=0)return;center/=mass;float radius=0;
        foreach(var rb in limb.bodies)if(rb!=null)radius=Mathf.Max(radius,Vector3.Distance(center,rb.worldCenterOfMass));
        Vector3 direction=impulse.normalized;
        Vector3 axis=Vector3.Cross(Vector3.up,direction);if(axis.sqrMagnitude<.001f)axis=transform.forward;
        Vector3 spin=axis.normalized*Mathf.Min(Mathf.Clamp(severSpinSpeed,0,1.5f),2/Mathf.Max(.1f,radius));
        Vector3 launch=direction*Mathf.Clamp(severLaunchSpeed,0,12)+Vector3.up*Mathf.Clamp(severUpwardSpeed,0,3);
        // A coherent rigid-motion impulse across ALL three bodies, not a violent
        // shove into just the upper bone. Bounded input spin, no velocity clipping,
        // unchanged masses, joint limits, solver, angular cap or global gravity.
        foreach(var rb in limb.bodies)if(rb!=null)
        {
            rb.AddForce(launch+Vector3.Cross(spin,rb.worldCenterOfMass-center),ForceMode.VelocityChange);
            rb.AddTorque(spin,ForceMode.VelocityChange);
        }
    }

    void RebindAnimatorPreservingDetachedPose(Transform detachedRoot)
    {
        if (!Application.isPlaying || animator == null || !animator.enabled) return;
        // Rebind writes default LOCAL transforms into the old cached bindings before
        // discarding them. After reparenting, those defaults have a different parent
        // space and can flip a leg 180 degrees. Preserve the entire detached hierarchy
        // (including its skins) through the rebind, before handing it to PhysX.
        // Keep the surviving hip bob/turn/recoil pose too, not just the cut branch.
        var cached = new List<Transform>(animator.GetComponentsInChildren<Transform>(true));
        cached.AddRange(detachedRoot.GetComponentsInChildren<Transform>(true));
        var transforms = cached.ToArray();
        var state = animator.GetCurrentAnimatorStateInfo(0);
        var floatParameters = new Dictionary<int, float>();
        foreach (var parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Float)
                floatParameters.Add(parameter.nameHash, animator.GetFloat(parameter.nameHash));
        var poses = new Pose[transforms.Length];
        var scales = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            poses[i] = new Pose(transforms[i].localPosition, transforms[i].localRotation);
            scales[i] = transforms[i].localScale;
        }
        animator.Rebind();
        foreach (var parameter in floatParameters) animator.SetFloat(parameter.Key, parameter.Value);
        UpdateLocomotion();
        if (state.fullPathHash != 0) animator.Play(state.fullPathHash, 0, state.normalizedTime);
        animator.Update(0);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].localPosition = poses[i].position;
            transforms[i].localRotation = poses[i].rotation;
            transforms[i].localScale = scales[i];
        }
    }

    public void Die() { Die(Vector3.zero); }

    public void Die(Vector3 impulse)
    {
        Initialize();
        if (IsDead) return;
        IsDead = true;
        blindTimeRemaining = 0;
        if (animator != null) animator.enabled = false;
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        foreach (var zone in hitZones) if (zone != null) zone.gameObject.SetActive(false);
        // CharacterJoint neutral frames were created in the FBX T-pose. Recreate
        // their connection frames at the CURRENT animated pose before simulation.
        Physics.SyncTransforms();
        var intactBodies = new List<Rigidbody>();
        foreach (var entry in ragdollBones)
            if (entry.body != null && !detachedBodies.Contains(entry.body)) intactBodies.Add(entry.body);
        RebaseJoints(intactBodies);
        foreach (var entry in ragdollBones)
            if (entry.body != null && !detachedBodies.Contains(entry.body)) MakeDynamic(entry.body);
        IgnoreInternalCollisions();
        var hipsBody = hips != null ? hips.GetComponent<Rigidbody>() : null;
        if (hipsBody != null) hipsBody.AddForce(Vector3.ClampMagnitude(impulse, 600), ForceMode.Impulse);
        if (Died != null) Died();
    }

    static void RebaseJoints(IEnumerable<Rigidbody> bodies)
    {
        foreach (var rb in bodies)
        {
            if (rb == null) continue;
            var joint = rb.GetComponent<CharacterJoint>();
            if (joint == null || joint.connectedBody == null) continue;
            var connected = joint.connectedBody;
            Vector3 anchor = joint.anchor;
            // Spine/shoulder intermediary bones are animated but have no Rigidbody.
            // Their rotations move the child joint away from its bind-pose anchor.
            // Reconnecting while retaining that old anchor injects constraint energy.
            Vector3 connectedAnchor = connected.transform.InverseTransformPoint(joint.transform.TransformPoint(anchor));
            joint.connectedBody = null;
            joint.connectedBody = connected;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = anchor;
            joint.connectedAnchor = connectedAnchor;
        }
    }

    static void MakeDynamic(Rigidbody rb)
    {
        var joint = rb.GetComponent<CharacterJoint>();
        if (joint != null)
        {
            // Dead joints yield at their angular stops instead of applying hard
            // corrective impulses against floor contacts. Keep the authored limits
            // and scale spring/damping with mass, not with the FBX's 100x scale.
            var stop = new SoftJointLimitSpring { spring = rb.mass * 80, damper = rb.mass * 12 };
            joint.twistLimitSpring = stop;
            joint.swingLimitSpring = stop;
        }
        rb.isKinematic = false;
        // Drop cached kinematic interpolation poses when switching ownership to PhysX.
        rb.interpolation = RigidbodyInterpolation.None;
        // Do not inherit a kinematic Animator's one-frame T-pose-to-idle velocity.
        // An explicit death/sever impulse is applied separately by the caller.
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.maxDepenetrationVelocity = 3f;
        // Giant-scale limbs should feel heavy, not whip around like small props.
        rb.maxAngularVelocity = 4f;
        rb.angularDrag = .85f;
        rb.drag = .12f;
        rb.useGravity = true;
        rb.detectCollisions = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.WakeUp();
    }
}
