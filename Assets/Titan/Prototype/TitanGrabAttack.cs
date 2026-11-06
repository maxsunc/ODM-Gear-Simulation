using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public enum TitanGrabStage { Idle, Windup, Reach, Holding, Recovery, ToMouth, BiteWindup, Bite }

/// <summary>Standing sandbox grab with optional hand-to-mouth/bite. Fixed contact
/// windows and exclusive bounded hold; no legacy player or delayed kill coroutine.</summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody))]
public class TitanGrabAttack : MonoBehaviour
{
    public bool automaticTick = true;
    public bool drawTelegraph = false;
    [Min(0)] public float holdDamagePerSecond; // Gameplay opt-in; legacy laboratory holds retain their original contract.
    [Min(.3f)] public float windupDuration = .65f;
    [Min(.1f)] public float reachDuration = .18f;
    [Min(.3f)] public float holdDuration = 1.5f;
    [Min(.2f)] public float recoveryDuration = .45f;
    [Min(.2f)] public float cooldownDuration = 3;
    [Min(1)] public float attackRange = 6.4f;
    [Min(.1f)] public float contactRadius = 1.05f;
    public LayerMask sceneryMask = ~0, victimMask = ~0;
    [Header("Aerial interception (requires explicit target motor)")]
    [Min(0)] public float aerialWindupDuration=0;
    [Min(.1f)] public float aerialReachDuration=.22f;
    [Min(.1f)] public float aerialMissRecoveryDuration=.25f;
    [Min(.1f)] public float aerialCooldownDuration=.65f;
    public bool IsAerial { get; private set; }
    Vector3 previousVictimPosition, previousVictimUp, aerialStartPalm, aerialPlaneDirection, aerialPlaneBend;
    Quaternion previousVictimRotation;
    [Header("Opt-in sandbox bite (latched at selection)")]
    public bool enableBite;
    [Min(.3f)] public float mouthTransferDuration = .7f;
    [Min(.3f)] public float biteWindupDuration = .6f;
    [Min(.08f)] public float biteDuration = .12f;
    [Min(0)] public float biteDamage = 100;
    [Min(.1f)] public float biteRadius = .55f;
    [Tooltip("Scout-only feeding depth in Titan-local metres. Latched on selection; labs retain their original mouth pose. Contact sphere moves to the actual inner bite point, never grows.")]
    [Range(0,.4f)] public float mouthEntryDepth = .3f;
    Vector3 mouthInHead;
    Vector3 MouthOrigin { get { return realMouth && Attached(body.head) ? body.head.TransformPoint(mouthInHead) : mouth.position; } }
    public Vector3 MouthBitePoint { get { return Attached(mouth) ? MouthOrigin - transform.forward * insertionDepth * EntryProgress() : Vector3.zero; } }
    public TitanGrabStage Stage { get; private set; }
    public bool IsBusy { get { return Stage != TitanGrabStage.Idle; } }
    public bool UsesLeftHand { get; private set; }
    public Vector3 LockedPoint { get; private set; }
    public Vector3 LastContactPoint { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float Elapsed { get { return elapsed; } }
    public int ContactCount { get; private set; }
    public int CaptureCount { get; private set; }
    public int BiteContactCount { get; private set; }
    public int BiteHitCount { get; private set; }
    public Vector3 LastBitePoint { get; private set; }
    public bool LastQueryOverflow { get; private set; }
    public string LastResult { get; private set; }

    ModularTitanBody body;
    TitanPatrolChaseAI brain;
    NavMeshAgent agent;
    TitanCombatTarget victim;
    Transform upper, lower, hand, grip, spine, chest, neck, jaw, mouth;
    Quaternion closedJawRotation, aerialCaughtUpper, aerialCaughtLower;
    Transform[] bones;
    Pose[] snapshots;
    Transform[] legBones = new Transform[6];
    Vector3[] plants = new Vector3[2];
    Quaternion[] flats = new Quaternion[2];
    readonly Vector3[] baseFeet = new Vector3[2];
    readonly Quaternion[] baseRotations = new Quaternion[2];
    float elapsed, windup, reach, holding, recovery, missRecovery, radius, captureTime;
    bool contacted, captured, eating, biteResolved, realMouth, escaped;
    TitanGrabEscape escape;
    float escapeRecoveryTime;
    float transfer, biteWindup, biteStrike, damage, mouthRadius, mouthCarryYaw, mouthCarryRoll, insertionDepth, carryScale = 1;
    float ContactTime { get { return windup + reach; } }
    float TransferStart { get { return ContactTime + holding; } }
    float BiteTime { get { return TransferStart + transfer + biteWindup + biteStrike; } }
    float RecoveryStart { get { return escaped ? escapeRecoveryTime : ContactTime + (captured ? holding + (eating ? transfer + biteWindup + biteStrike : 0) : 0); } }
    int beginFrame = -1;
    Vector3 contactGrip, selectionOrigin, previousCarryGrip, biteGripOffset;
    readonly RaycastHit[] rays = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];
    LineRenderer cue;
    Material cueMaterial;

    void Awake()
    {
        body = GetComponent<ModularTitanBody>(); brain = GetComponent<TitanPatrolChaseAI>(); agent = GetComponent<NavMeshAgent>();
        bones = body.hips != null ? body.hips.GetComponentsInChildren<Transform>() : new Transform[0];
        snapshots = new Pose[bones.Length];
        foreach (var bone in bones)
        {
            if (bone.name == "Spine") spine = bone;
            if (bone.name == "Chest") chest = bone;
            if (bone.name == "Neck") neck = bone;
            if (bone.name == "Jaw") jaw = bone;
        }
        if (jaw != null) closedJawRotation=jaw.localRotation;
        LastResult = "Ready";
    }
    void OnEnable() { body.Died += OnDied; body.BlindnessChanged += OnBlind; body.LimbSevered += OnSevered; }
    void OnDisable()
    {
        Cancel("Disabled");
        body.Died -= OnDied; body.BlindnessChanged -= OnBlind; body.LimbSevered -= OnSevered;
    }
    void OnDestroy() { if (cueMaterial != null) Destroy(cueMaterial); }
    void OnDied() { Cancel("Dead"); }
    void OnBlind(bool blind) { if (blind) Cancel("Blinded"); }
    void OnSevered(TitanHitRegion region)
    {
        if (region == TitanHitRegion.LeftLeg || region == TitanHitRegion.RightLeg ||
            region == (UsesLeftHand ? TitanHitRegion.LeftArm : TitanHitRegion.RightArm)) Cancel("Required limb lost");
    }
    void OnReleased() { if (IsBusy && captured && Stage != TitanGrabStage.Recovery) Cancel("Victim released"); }
    void Update()
    {
        // Director/input may start us earlier in THIS Update. Its delta belongs to
        // the preceding interval, not to the new wind-up (especially on long frames).
        if (automaticTick && Time.frameCount != beginFrame) TickAttack(Time.deltaTime);
    }
    void LateUpdate()
    {
        ApplyPose();
        if (IsBusy && captured && Stage != TitanGrabStage.Recovery && victim != null)
        {
            Physics.SyncTransforms();
            if (!ClearVolume(previousCarryGrip,grip.position,.45f,victim.transform)) Cancel("Carry obstructed");
            else previousCarryGrip=grip.position;
        }
        UpdateCue();
    }

    public bool TryBeginAerialGrab(TitanCombatTarget target, bool leftHand) { return TryBeginGrab(target,leftHand,true); }
    public bool TryBeginGrab(TitanCombatTarget target, bool leftHand, bool airborne = false)
    {
        if (!isActiveAndEnabled || IsBusy || CooldownRemaining > 0 || body.hips == null || body.head == null ||
            !body.CanWalk || body.animator == null || !body.animator.enabled || !(leftHand ? body.CanGrabLeft : body.CanGrabRight) ||
            target == null || target.IsDead || target.IsHeld || target.ReleasePending || target.IsGrabImmune || !target.isActiveAndEnabled || target.gameObject.scene != gameObject.scene) return false;
        var stomp = GetComponent<TitanStompAttack>();
        if (stomp != null && stomp.IsBusy) return false;
        if (brain != null && (!brain.isActiveAndEnabled || brain.target != target.transform || !brain.CanSeeTarget || brain.State != TitanAwarenessState.Chase)) return false;
        if (!airborne && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > 1) return false;
        Vector3 delta = target.transform.position - transform.position;
        if ((!airborne && (Vector3.ProjectOnPlane(delta, Vector3.up).magnitude > attackRange || Mathf.Abs(delta.y) > 2.2f)) ||
            (airborne && delta.y<=2.2f) || Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) > 65 ||
            !ClearRay(body.head.position, target.transform.position + Vector3.up, target.transform)) return false;
        upper = leftHand ? body.leftUpperArm : body.rightUpperArm;
        lower = leftHand ? body.leftLowerArm : body.rightLowerArm;
        hand = leftHand ? body.leftHand : body.rightHand;
        grip = leftHand ? body.leftGripSocket : body.rightGripSocket;
        mouth = body.mouthSocket;
        if (enableBite && (!Attached(mouth) || !Attached(jaw))) return false;
        if (upper == null || lower == null || hand == null || grip == null || !grip.IsChildOf(transform) || !CachePlants()) return false;
        Vector3 aimPoint=target.transform.position+Vector3.up;
        float handRadius=0; Vector3 palmPosition;
        if(airborne)
        {
            var motor=target.GetComponent<TitanAerialMotor>();
            var controller=target.GetComponent<CharacterController>();
            if(motor==null || !motor.CanSuspend || controller==null || !controller.enabled || Mathf.Abs(controller.center.x)+Mathf.Abs(controller.center.z)>.0001f ||
                !Finite(motor.Velocity) || !TitanAerialContact.HandSphere(hand,out palmPosition,out handRadius)) return false;
            float horizon=Mathf.Min(1.05f,SafeMinimum(aerialWindupDuration,0,0)+SafeMinimum(aerialReachDuration,.1f,.22f));
            Vector3 center=controller.bounds.center;
            float armReach=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,palmPosition)-.08f;
            // Prefer the reach-end intercept, but do not reject a currently reachable
            // flyer merely because its full-horizon prediction has already left range.
            // Selection is bounded; the chosen point still locks for this attempt.
            bool reachable=false;
            for(int i=6;i>=0;i--)
            {
                aimPoint=center+Vector3.ClampMagnitude(motor.Velocity*(horizon*i/6),24);
                if(Vector3.Dot(aimPoint-upper.position,transform.forward)>=1 && Vector3.Distance(upper.position,aimPoint)<=armReach)
                { reachable=true; break; }
            }
            if(!reachable || !ClearRay(body.head.position,aimPoint,target.transform)) return false;
            aerialStartPalm=palmPosition;
            aerialPlaneDirection=(hand.position-upper.position).normalized;
            aerialPlaneBend=Vector3.ProjectOnPlane(lower.position-upper.position,aerialPlaneDirection);
            if(aerialPlaneBend.sqrMagnitude<.000001f)
                aerialPlaneBend=Vector3.ProjectOnPlane(transform.right*(leftHand?-1:1)-transform.up*.65f,aerialPlaneDirection);
            aerialPlaneBend.Normalize();
        }
        if (brain != null && !brain.TryAcquireMovementLock(this)) return false;
        IsAerial=airborne; previousVictimPosition=target.transform.position; previousVictimUp=target.transform.up;
        previousVictimRotation=target.transform.rotation;
        LockedPoint = aimPoint; selectionOrigin = body.head.position;
        victim = target; UsesLeftHand = leftHand; elapsed = 0; contacted = captured = false; LastQueryOverflow = false;
        windup = airborne?SafeMinimum(aerialWindupDuration,0,0):Mathf.Max(.3f, windupDuration);
        reach = airborne?SafeMinimum(aerialReachDuration,.1f,.22f):Mathf.Max(.1f, reachDuration);
        holding = Mathf.Clamp(holdDuration, .3f, 3);
        recovery = Mathf.Max(.2f, recoveryDuration);
        missRecovery = SafeMinimum(aerialMissRecoveryDuration,.1f,.25f);
        radius = airborne?handRadius:Mathf.Max(.1f, contactRadius);
        eating = enableBite; biteResolved = escaped = false; escape=null; biteGripOffset=Vector3.zero;
        transfer = SafeMinimum(mouthTransferDuration,.3f,.7f); biteWindup = SafeMinimum(biteWindupDuration,.3f,.6f);
        biteStrike = SafeMinimum(biteDuration,.08f,.12f); mouthRadius = SafeMinimum(biteRadius,.1f,.55f);
        damage = SafeMinimum(biteDamage,0,100);
        realMouth = target.UsesMouthInsertion;
        carryScale = realMouth ? Mathf.Abs(transform.lossyScale.x) : 1;
        // MouthSocket belongs to the lower jaw. Chasing it DOWN while open puts
        // a giant palm into the chest. Feed at the head-relative closed aperture;
        // the lower teeth rise into this point at actual closure.
        if(mouth!=null)mouthInHead=body.head.InverseTransformPoint(mouth.position);
        insertionDepth = eating && realMouth ? Mathf.Clamp(SafeMinimum(mouthEntryDepth,0,.3f),0,.4f) * carryScale : 0;
        // Human size does NOT grow with the Titan. Beyond the established3x/5x
        // rigs, unbounded insertion would drag the same palm through the chest.
        // Keep the grip/contact radius; cap only feeding depth to half the human.
        var humanShape=target.GetComponent<CharacterController>();
        if(realMouth && humanShape!=null)insertionDepth=Mathf.Min(insertionDepth,humanShape.height*Mathf.Abs(target.transform.lossyScale.y)*.5f);
        Stage = windup>0?TitanGrabStage.Windup:TitanGrabStage.Reach;
        beginFrame = Time.frameCount; LastResult = airborne?"Direct reach / aim locked":"Wind-up / aim locked";
        body.SetWalking(false);
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
        UpdateCue(); return true;
    }

    public void TickAttack(float deltaTime)
    {
        if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        CooldownRemaining = Mathf.Max(0, CooldownRemaining - deltaTime);
        if (!IsBusy) return;
        bool needsVictim = Stage != TitanGrabStage.Recovery;
        if (!body.CanWalk || body.animator == null || !body.animator.enabled || !(UsesLeftHand ? body.CanGrabLeft : body.CanGrabRight) ||
            (needsVictim && (victim == null || !victim.isActiveAndEnabled || victim.IsDead ||
                (captured && !victim.IsHeldBy(this)) || (!contacted && victim.IsHeld))) ||
            (eating && (!Attached(mouth) || !mouth.gameObject.activeInHierarchy || !Attached(jaw))) ||
            (brain != null && (!brain.isActiveAndEnabled || (needsVictim && brain.target != victim.transform)))) { Cancel("Interrupted"); return; }
        float previousElapsed=elapsed;bool hadCapture=captured;
        elapsed += deltaTime;
        if(escaped)
        {
            Stage=TitanGrabStage.Recovery;
            if(elapsed>=RecoveryStart+recovery)Finish("Escaped / recovered");
            UpdateCue();return;
        }
        if(IsAerial && !contacted) AdvanceAerialContact(previousElapsed,elapsed);
        if(!IsBusy) return;
        if (!IsAerial && !contacted && elapsed >= ContactTime)
        {
            float now = elapsed; elapsed = ContactTime; Stage = TitanGrabStage.Reach;
            ResolveContact(); elapsed = now;
        }
        if (!IsBusy) return;
        if(escape!=null)
        {
            // This frame's pre-contact time cannot consume a newly displayed QTE.
            if(!hadCapture && captured)elapsed=captureTime;
            escape.Advance(elapsed-captureTime);
        }
        if(captured && victim!=null && victim.IsHeldBy(this))
        {
            victim.AdvanceHoldDamage(this,Mathf.Max(0,Mathf.Min(elapsed,eating?BiteTime:RecoveryStart)-captureTime));
            if(!IsBusy || victim==null || victim.IsDead)return;
        }
        if (captured && eating && !biteResolved && elapsed >= BiteTime)
        {
            float now = elapsed; elapsed = BiteTime; Stage = TitanGrabStage.Bite;
            ResolveBite(); elapsed = now;
        }
        if (!IsBusy) return;
        if (elapsed < windup) Stage = TitanGrabStage.Windup;
        else if (elapsed < ContactTime) Stage = TitanGrabStage.Reach;
        else if (captured && elapsed < TransferStart) Stage = TitanGrabStage.Holding;
        else if (captured && eating && elapsed < TransferStart + transfer) Stage = TitanGrabStage.ToMouth;
        else if (captured && eating && elapsed < BiteTime - biteStrike) Stage = TitanGrabStage.BiteWindup;
        else if (captured && eating && elapsed < BiteTime) Stage = TitanGrabStage.Bite;
        else
        {
            Stage = TitanGrabStage.Recovery; ReleaseVictim("Sequence complete");
            if (elapsed >= RecoveryStart + recovery) Finish("Recovered");
        }
        UpdateCue();
    }
    void AdvanceAerialContact(float fromTime, float toTime)
    {
        Vector3 current=victim.transform.position;
        float start=Mathf.Max(fromTime,windup), end=Mathf.Min(toTime,ContactTime);
        // Lab motors retain the old fixed-axis gate; the real ODM adapter opts in
        // to observed rotation CCD, without upright snapping or larger geometry.
        var motor=victim.GetComponent<TitanAerialMotor>();
        bool rotating=motor!=null && motor.SupportsRotatingCapsule;
        Quaternion currentRotation=victim.transform.rotation;
        bool stableAxis=rotating || Vector3.Angle(previousVictimUp,victim.transform.up)<.01f;
        var shape=victim.GetComponent<CharacterController>();
        if(end>start && stableAxis && shape!=null && shape.enabled &&
            (victimMask.value & (1<<shape.gameObject.layer))!=0)
        {
            for(int i=0;i<bones.Length;i++) if(Attached(bones[i])) snapshots[i]=new Pose(bones[i].localPosition,bones[i].localRotation);
            float saved=elapsed;
            try
            {
                int steps=Mathf.Clamp(Mathf.CeilToInt((end-start)*120),1,128);
                elapsed=start; body.animator.Update(0); ApplyPose();
                TitanAerialContact.HandPose handFrom;
                if(!TitanAerialContact.HandBox(hand,out handFrom)) { Cancel("Hand shape unavailable"); return; }
                if(!ClearVolume(handFrom.center,handFrom.center,handFrom.half.magnitude,victim.transform)) { Cancel("Aerial reach obstructed"); return; }
                for(int i=1;i<=steps && !captured;i++)
                {
                    float t0=Mathf.Lerp(start,end,(i-1f)/steps), t1=Mathf.Lerp(start,end,i/(float)steps);
                    elapsed=t1; body.animator.Update(0); ApplyPose();
                    TitanAerialContact.HandPose handTo; TitanAerialContact.HandBox(hand,out handTo);
                    if(!ClearVolume(handFrom.center,handTo.center,Mathf.Max(handFrom.half.magnitude,handTo.half.magnitude),victim.transform)) { Cancel("Aerial reach obstructed"); break; }
                    float a=(t0-fromTime)/(toTime-fromTime), b=(t1-fromTime)/(toTime-fromTime), fraction;
                    Vector3 targetFrom=Vector3.Lerp(previousVictimPosition,current,a), targetTo=Vector3.Lerp(previousVictimPosition,current,b);
                    Quaternion rotationFrom=rotating?Quaternion.Slerp(previousVictimRotation,currentRotation,a):currentRotation;
                    Quaternion rotationTo=rotating?Quaternion.Slerp(previousVictimRotation,currentRotation,b):currentRotation;
                    if(TitanAerialContact.SweepHand(shape,handFrom,handTo,targetFrom,targetTo,rotationFrom,rotationTo,out fraction))
                    {
                        elapsed=Mathf.Lerp(t0,t1,fraction); body.animator.Update(0); ApplyPose();
                        TitanAerialContact.HandPose actual; TitanAerialContact.HandBox(hand,out actual);
                        Vector3 rootAtContact=Vector3.Lerp(targetFrom,targetTo,fraction), actualContact;
                        Quaternion rotationAtContact=Quaternion.Slerp(rotationFrom,rotationTo,fraction);
                        if(TitanAerialContact.CapsuleGap(shape,actual,rootAtContact,rotationAtContact,out actualContact)<=.0001f)
                        {
                            contactGrip=grip.position; LastContactPoint=actualContact;
                            TryCapture(rootAtContact,rotationAtContact);
                            if(captured) { contacted=true; ContactCount++; LastResult="Aerial hand contact / captured"; }
                        }
                    }
                    handFrom=handTo;
                }
            }
            finally
            {
                elapsed=saved;
                if(body.CanWalk && body.animator!=null && body.animator.enabled)
                    for(int i=0;i<bones.Length;i++) if(Attached(bones[i]))
                    { bones[i].localPosition=snapshots[i].position; bones[i].localRotation=snapshots[i].rotation; }
                Physics.SyncTransforms();
            }
        }
        if(!IsBusy || victim==null) return;
        previousVictimPosition=current; previousVictimUp=victim.transform.up; previousVictimRotation=currentRotation;
        if(!contacted && toTime>=ContactTime) { contacted=true; recovery=missRecovery; ContactCount++; LastResult="Aerial miss / recovery"; }
    }
    void ResolveContact()
    {
        contacted = true; ContactCount++;
        if (!ClearRay(selectionOrigin, LockedPoint, victim.transform)) { LastResult = "Reach blocked"; return; }
        // Evaluate the exact contact pose even on a long frame. Restore the rendered
        // pose afterwards: do not accumulate an overlay or rewind Animator phase.
        for (int i = 0; i < bones.Length; i++)
            if (Attached(bones[i])) snapshots[i] = new Pose(bones[i].localPosition, bones[i].localRotation);
        try
        {
            body.animator.Update(0);
            float contactTime = elapsed;
            elapsed = windup; ApplyPose(); Vector3 from = grip.position;
            body.animator.Update(0); elapsed = contactTime; ApplyPose(); contactGrip = LastContactPoint = grip.position;
            Physics.SyncTransforms();
            Vector3 sweep = contactGrip - from;
            int swept = sweep.sqrMagnitude > .0001f ? gameObject.scene.GetPhysicsScene().SphereCast(from, radius, sweep.normalized, rays, sweep.magnitude, sceneryMask, QueryTriggerInteraction.Ignore) : 0;
            if (swept == rays.Length) { LastQueryOverflow = true; LastResult = "Sweep saturated / miss"; return; }
            for (int i = 0; i < swept; i++)
                if (rays[i].transform.gameObject.scene == gameObject.scene && !Attached(rays[i].transform) && !rays[i].transform.IsChildOf(victim.transform)) { LastResult = "Reach blocked"; return; }
            int solids = gameObject.scene.GetPhysicsScene().OverlapSphere(contactGrip, radius, overlaps, sceneryMask, QueryTriggerInteraction.Ignore);
            if (solids == overlaps.Length) { LastQueryOverflow = true; LastResult = "Contact scenery saturated / miss"; return; }
            for (int i = 0; i < solids; i++)
                if (overlaps[i].gameObject.scene == gameObject.scene && !Attached(overlaps[i].transform) && !overlaps[i].transform.IsChildOf(victim.transform)) { LastResult = "Contact blocked"; return; }
            int count = gameObject.scene.GetPhysicsScene().OverlapSphere(contactGrip, radius, overlaps, victimMask, QueryTriggerInteraction.Ignore);
            LastQueryOverflow = count == overlaps.Length;
            if (LastQueryOverflow) { LastResult = "Contact query saturated / miss"; return; }
            for (int i = 0; i < count; i++)
            {
                var col = overlaps[i];
                if (col.GetComponentInParent<TitanCombatTarget>() != victim || !ClearRay(contactGrip, col.bounds.center, victim.transform)) continue;
                TryCapture(); break;
            }
            // Unity CCT scene queries shrink its capsule. Test the configured shape
            // too, so a real controller is not harder to hit than the Box stand-in.
            var controller = victim.GetComponent<CharacterController>();
            if (!captured && controller != null && controller.enabled && (victimMask.value & (1 << controller.gameObject.layer)) != 0 &&
                (TitanCombatTarget.ClosestCombatPoint(controller, contactGrip) - contactGrip).sqrMagnitude <= radius * radius &&
                ClearRay(contactGrip, controller.bounds.center, victim.transform)) TryCapture();
            if (!captured) LastResult = "Missed";
        }
        finally
        {
            for (int i = 0; i < bones.Length; i++)
                if (Attached(bones[i])) { bones[i].localPosition = snapshots[i].position; bones[i].localRotation = snapshots[i].rotation; }
            Physics.SyncTransforms();
        }
    }
    void TryCapture(Vector3? contactPosition = null,Quaternion? contactRotation = null)
    {
        if (!victim.TryHold(this, grip, IsAerial,contactPosition,contactRotation)) return;
        captured = true; captureTime=elapsed;
        aerialCaughtUpper=upper.localRotation; aerialCaughtLower=lower.localRotation;
        biteGripOffset=victim.HeldCenterOffset(this);
        Vector3 horizontal=Vector3.ProjectOnPlane(biteGripOffset,Vector3.up);
        mouthCarryYaw=horizontal.sqrMagnitude>.0001f?Vector3.SignedAngle(horizontal,transform.right*(UsesLeftHand?1:-1),Vector3.up):0;
        mouthCarryRoll=realMouth?(UsesLeftHand?-90:90):0;
        if(realMouth && biteGripOffset.sqrMagnitude>.0001f)
        {
            // Large palms can catch a small human at the feet. Keeping that
            // vertical offset would force the palm down through the chest when
            // feeding. Roll around the SAME grip into a horizontal, head-first
            // presentation; never change the physical grip point or its radius.
            Vector3 turned=Quaternion.AngleAxis(mouthCarryYaw,Vector3.up)*biteGripOffset;
            // Keep the palm above/outside the lips rather than hanging
            // its lower edge into the upper chest while the torso enters.
            Vector3 feedDirection=transform.right*(UsesLeftHand?1:-1)-Vector3.up*.5f;
            mouthCarryRoll=Vector3.SignedAngle(turned,feedDirection,transform.forward);
        }
        previousCarryGrip=contactGrip; CaptureCount++; victim.HoldReleased += OnReleased; LastResult = "Captured";
        escape=victim.GetComponent<TitanGrabEscape>();
        if(escape!=null && escape.Begin(this))
        {
            float extra=Mathf.Max(0,captureTime+TitanGrabEscape.Window-TransferStart);
            holding+=extra;
            victim.ReserveEscapeLease(this,extra,BiteTime-captureTime+.1f);
            LastResult="Captured / break free: 3 keys in 3 seconds";
        }
        else escape=null;
    }
    internal bool BeginEscapeRecovery(TitanCombatTarget target)
    {
        if(!captured || !IsBusy || Stage==TitanGrabStage.Recovery || target!=victim || !target.IsHeldBy(this))return false;
        escaped=biteResolved=true;escapeRecoveryTime=elapsed;Stage=TitanGrabStage.Recovery;LastResult="Quick-time escape";
        return true;
    }
    public void Cancel(string reason = "Cancelled") { if (IsBusy) Finish(reason); }
    void Finish(string reason)
    {
        Stage = TitanGrabStage.Idle; LastResult = reason;
        CooldownRemaining = IsAerial?SafeMinimum(aerialCooldownDuration,.1f,.65f):Mathf.Max(.2f, cooldownDuration);
        ReleaseVictim(reason); victim = null;
        if (brain != null) brain.ReleaseMovementLock(this);
        if (cue != null) cue.enabled = false;
        if (eating && !body.IsDead && body.animator != null && body.animator.enabled && Attached(jaw)) jaw.localRotation=closedJawRotation;
        // No body pose reset: injury/death owns the current surviving bones.
    }
    void ReleaseVictim(string reason)
    {
        if (victim == null) return;
        victim.HoldReleased -= OnReleased; victim.ReleaseHold(this, reason);
    }

    public void ApplyPose()
    {
        if (!IsBusy || !body.CanWalk || !Attached(upper) || !Attached(grip)) return;
        float recover = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - RecoveryStart) / recovery));
        float poseTime=escaped?escapeRecoveryTime:elapsed;
        float carry = captured ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((poseTime - ContactTime) / .55f)) : 0;
        float toMouth = captured && eating ? Mathf.SmoothStep(0,1,Mathf.Clamp01((poseTime-TransferStart)/transfer)) : 0;
        float load = IsAerial?0:Mathf.SmoothStep(0, 1, poseTime / windup) * (1 - recover) * (1 - carry * .45f) * (1-toMouth);
        baseFeet[0] = legBones[2].position; baseFeet[1] = legBones[5].position;
        baseRotations[0] = legBones[2].rotation; baseRotations[1] = legBones[5].rotation;
        Vector3 toward = Vector3.ProjectOnPlane(LockedPoint - transform.position, Vector3.up).normalized;
        body.hips.position += (toward * 2.6f - Vector3.up * 3.8f) * load;
        Tilt(spine, 14 * load); Tilt(chest, 18 * load); Tilt(neck, 10 * load); Tilt(body.head, 15 * load);
        for (int i = 0; i < 2; i++)
        {
            Solve(legBones[i*3], legBones[i*3+1], legBones[i*3+2], Vector3.Lerp(plants[i], baseFeet[i], recover), transform.forward, 1);
            legBones[i*3+2].rotation = Quaternion.Slerp(flats[i], baseRotations[i], recover);
        }
        float side = UsesLeftHand ? -1 : 1;
        Vector3 windupPoint = LockedPoint + Vector3.up * 2 + transform.right * side * 1.1f - toward * .45f;
        float reachProgress=Mathf.SmoothStep(0,1,Mathf.Clamp01((poseTime-windup)/reach));
        // Air catches go directly from the selected physical palm to the fixed
        // intercept. Never lift to the ground grab's overhead warning/load point.
        Vector3 aim = Vector3.Lerp(IsAerial?aerialStartPalm:windupPoint, LockedPoint, reachProgress);
        if (captured) aim = Vector3.Lerp(contactGrip, CarryPoint(), carry);
        if (eating && Attached(mouth) && Attached(jaw))
        {
            float close = Mathf.SmoothStep(0,1,Mathf.Clamp01((poseTime-(BiteTime-biteStrike))/biteStrike));
            // Generic Animator constant curves may not rewrite an unchanged jaw
            // channel each evaluation. An additive tilt then accumulates and spins
            // MouthSocket (and its arm target) around the head every frame.
            jaw.localRotation=closedJawRotation;
            Tilt(jaw, 28 * toMouth * (1-close) * (1-recover));
            // Preserve the physical catch offset. Move the ARM to bring the held
            // torso to the mouth; never recenter/snap an aerial victim in the grip.
            Quaternion heldTurn=CarryTurn(toMouth);
            if(captured && victim!=null)
            {
                if(realMouth)victim.SetAerialCarryOrientation(this,heldTurn);
                else if(IsAerial)victim.SetAerialCarryYaw(this,mouthCarryYaw*toMouth);
            }
            // A foot/side catch can leave the person IN FRONT of the palm. Holding
            // that offset fixed in WORLD space would put the palm inside the chest
            // at the mouth. Turn around the same grip into a side-on carry instead.
            float entry = EntryProgress();
            Vector3 feed = MouthBitePoint + transform.forward * (IsAerial?.6f:.35f) * (1-entry);
            aim = MouthCarryAim(aim,feed-heldTurn*biteGripOffset,toMouth);
        }
        float armWeight=(1-recover)*(IsAerial?Mathf.Clamp01((poseTime-windup)/reach):Mathf.Clamp01(poseTime/.2f));
        if(IsAerial && captured)
        {
            // CCD can catch before full extension. Re-solving that partial pose
            // from idle with the same fractional weight would blend it TWICE and
            // retract/pop the arm on the catch frame. Start carry from the exact
            // caught joint pose, then smoothly take over; recovery returns to idle.
            upper.localRotation=Quaternion.Slerp(aerialCaughtUpper,upper.localRotation,recover);
            lower.localRotation=Quaternion.Slerp(aerialCaughtLower,lower.localRotation,recover);
            armWeight=(1-recover)*Mathf.SmoothStep(0,1,Mathf.Clamp01((poseTime-ContactTime)/.12f));
        }
        if(IsAerial && !captured)
        {
            // Aim the PHYSICAL palm, not the offset grip socket, at the intercept.
            // Iterate the small rotating socket offset; this does not change aim
            // or move the victim. After contact the same grip pose carries them.
            Quaternion baseUpper=upper.localRotation,baseLower=lower.localRotation;
            for(int i=0;i<3;i++)
            {
                Vector3 palm;float physicalRadius;
                if(!TitanAerialContact.HandSphere(hand,out palm,out physicalRadius)) break;
                Vector3 gripAim=aim+grip.position-palm;
                // Infer the hinge from the SAME animation base on each iteration,
                // never from a previously solved almost-straight elbow.
                upper.localRotation=baseUpper;lower.localRotation=baseLower;
                SolveArm(gripAim,armWeight);
            }
        }
        else SolveArm(aim,armWeight,IsAerial && captured?carry:0);
        Transform otherUpper = UsesLeftHand ? body.rightUpperArm : body.leftUpperArm;
        Transform otherLower = UsesLeftHand ? body.rightLowerArm : body.leftLowerArm;
        if (Attached(otherUpper) && otherLower != null)
            Aim(otherUpper, otherLower, otherUpper.position - Vector3.up * 2 - transform.right * side * 1.6f, load * .65f);
    }
    void SolveArm(Vector3 target, float weight, float outsideCarry = 0)
    {
        Vector3 firstVector=lower.position-upper.position, secondVector=grip.position-lower.position;
        float first=firstVector.magnitude, second=secondVector.magnitude;
        Vector3 direction=target-upper.position;
        if (first<.001f || second<.001f || direction.sqrMagnitude<.0001f || weight<=0) return;
        float distance=Mathf.Clamp(direction.magnitude,Mathf.Abs(first-second)+.01f,first+second-.01f);
        direction.Normalize(); target=upper.position+direction*distance;
        // Transport the selected arm's ACTUAL initial hinge plane to the reach
        // direction. A fast reach must not spend its first frames rolling into a
        // different pole (or cross a shortest-arc roll seam while partially blended).
        // Frontal selection keeps the goal away from the antipodal initial direction.
        // The approved ground solver retains its separate torso-facing pole.
        Vector3 bend=IsAerial?Quaternion.FromToRotation(aerialPlaneDirection,direction)*aerialPlaneBend:
            Vector3.Cross(direction,transform.forward)*(UsesLeftHand?1:-1);
        if (bend.sqrMagnitude<.001f) bend=Vector3.ProjectOnPlane(IsAerial?transform.forward:transform.right*(UsesLeftHand?-1:1)-transform.up,direction);
        bend.Normalize();
        if(outsideCarry>0)
        {
            // The transported catch hinge can point BACK through the chest when
            // the hand rises. Carry with the elbow outside and in front instead;
            // blend only after contact so the approved fast reach is unchanged.
            Vector3 outside=Vector3.ProjectOnPlane(transform.forward+transform.right*(UsesLeftHand?-.8f:.8f),direction);
            if(outside.sqrMagnitude>.001f) bend=Vector3.Slerp(bend,outside.normalized,outsideCarry).normalized;
        }
        float along=(first*first+distance*distance-second*second)/(2*distance);
        Vector3 middle=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
        Vector3 normal=Vector3.Cross(bend,direction).normalized;
        // The grip has a forward palm offset; it is not the anatomical elbow
        // hinge. Using it to infer roll can nearly cancel the small idle bend.
        Vector3 sourceNormal=Vector3.Cross(firstVector,hand.position-lower.position);
        if (sourceNormal.sqrMagnitude<.000001f) sourceNormal=Vector3.ProjectOnPlane(transform.forward,firstVector.normalized);
        if (sourceNormal.sqrMagnitude<.000001f) sourceNormal=Vector3.ProjectOnPlane(transform.up,firstVector.normalized);
        sourceNormal.Normalize();
        Quaternion before=upper.rotation;
        Quaternion solved=Quaternion.LookRotation(middle-upper.position,normal)*
            Quaternion.Inverse(Quaternion.LookRotation(firstVector,sourceNormal))*before;
        upper.rotation=Quaternion.Slerp(before,solved,weight);
        Vector3 rotatedNormal=(upper.rotation*Quaternion.Inverse(before))*sourceNormal;
        solved=Quaternion.LookRotation(target-lower.position,normal)*
            Quaternion.Inverse(Quaternion.LookRotation(grip.position-lower.position,rotatedNormal))*lower.rotation;
        lower.rotation=Quaternion.Slerp(lower.rotation,solved,weight);
    }
    Vector3 MouthCarryAim(Vector3 from,Vector3 to,float progress)
    {
        Vector3 point=Vector3.Lerp(from,to,progress);
        // Lift in FRONT of the torso before bringing the held person inward.
        // Endpoints, duration and victim-to-grip offset remain fixed; only the
        // arm follows this clearance arc. Ground carry retains its approved path.
        if(IsAerial) point+=(transform.forward*.9f+transform.up*.35f)*Mathf.Sin(Mathf.PI*progress);
        return point;
    }
    Quaternion CarryTurn(float progress)
    {
        Quaternion yaw=IsAerial||realMouth?Quaternion.AngleAxis(mouthCarryYaw*progress,Vector3.up):Quaternion.identity;
        return realMouth?Quaternion.AngleAxis(mouthCarryRoll*progress,transform.forward)*yaw:yaw;
    }
    float EntryProgress()
    {
        return insertionDepth > 0 && captured && eating && !escaped ? Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-TransferStart-transfer)/biteWindup)) : 0;
    }
    // Anatomical waypoint scales once for the opted-in Scout path. A world-height
    // 8.2m waypoint would pull a 5x Titan's hand down through its chest/hips.
    // Historical proxy/lab carry remains unchanged.
    Vector3 CarryPoint() { return transform.position + (Vector3.up * (IsAerial?8.2f:6.2f) + transform.forward * 2 + transform.right * (UsesLeftHand ? -1 : 1) * 1.2f) * carryScale; }
    static bool Finite(Vector3 value) { return !(float.IsNaN(value.x)||float.IsNaN(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.x)||float.IsInfinity(value.y)||float.IsInfinity(value.z)); }
    static float SafeMinimum(float value, float min, float fallback) { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(min,value); }

    void ResolveBite()
    {
        biteResolved = true; BiteContactCount++;
        var target = victim;
        if (target == null || !target.IsHoldWithinLease(this,BiteTime-captureTime))
        {
            if (target != null) target.ReleaseHold(this,"Hold lease expired before bite");
            if (IsBusy) Cancel("Hold lease expired before bite");
            return;
        }
        for (int i = 0; i < bones.Length; i++)
            if (Attached(bones[i])) snapshots[i] = new Pose(bones[i].localPosition,bones[i].localRotation);
        try
        {
            body.animator.Update(0); ApplyPose();
            if (target != null) target.TickHold(0);
            if (!IsBusy) return;
            Physics.SyncTransforms(); LastBitePoint = MouthBitePoint;
            Vector3 hit;
            bool contact = target != null && target.IsHeldBy(this) && !target.IsDead &&
                target.TryGetHeldContact(this,LastBitePoint,mouthRadius,out hit);
            // Reject a blocked lift/transfer or mouth volume, including long ticks.
            bool clear = contact && ClearVolume(contactGrip,CarryPoint(),.45f,target.transform) &&
                ClearVolume(CarryPoint(),grip.position,.45f,target.transform) &&
                ClearVolume(LastBitePoint,LastBitePoint,mouthRadius,target.transform);
            if(clear && IsAerial)
            {
                // Long explicit ticks must not skip scenery on the new forward
                // transfer arc. Keep the conservative existing checks as well.
                Vector3 from=CarryPoint();
                for(int i=1;i<=12 && clear;i++)
                {
                    float progress=i/12f;
                    Quaternion turn=CarryTurn(progress);
                    Vector3 goal=MouthOrigin+transform.forward*.6f-turn*biteGripOffset;
                    Vector3 to=MouthCarryAim(CarryPoint(),goal,progress);
                    clear=ClearVolume(from,to,.45f,target.transform);from=to;
                }
            }
            if(clear && insertionDepth>0)
            {
                Quaternion turn=CarryTurn(1);
                Vector3 start=MouthOrigin+transform.forward*(IsAerial?.6f:.35f)-turn*biteGripOffset;
                clear=ClearVolume(start,grip.position,.45f,target.transform);
            }
            Stage = TitanGrabStage.Recovery; // An intentional lethal release must still recover.
            if (clear && target.IsHeldBy(this) && body.CanWalk)
            {
                float before = target.Health;
                target.TakeBiteDamage(this,damage,LastBitePoint,transform.forward);
                if (target.Health < before) BiteHitCount++;
                if (IsBusy) LastResult = "Bite contact";
            }
            else LastResult = contact ? "Bite blocked / released" : "Mouth missed / released";
            if (IsBusy) ReleaseVictim("Bite complete");
        }
        finally
        {
            // Damage callbacks can sever a leg or kill the Titan. Never overwrite
            // the current injury/ragdoll pose after that ownership handoff.
            if (body.CanWalk && body.animator != null && body.animator.enabled)
                for (int i = 0; i < bones.Length; i++)
                    if (Attached(bones[i])) { bones[i].localPosition=snapshots[i].position; bones[i].localRotation=snapshots[i].rotation; }
            Physics.SyncTransforms();
        }
    }
    bool ClearVolume(Vector3 from, Vector3 to, float size, Transform allowed)
    {
        Vector3 delta = to-from;
        int count = delta.sqrMagnitude > .0001f ? gameObject.scene.GetPhysicsScene().SphereCast(from,size,delta.normalized,rays,delta.magnitude,sceneryMask,QueryTriggerInteraction.Ignore) : 0;
        if (count == rays.Length) { LastQueryOverflow=true; return false; }
        for (int i=0;i<count;i++) if (!Attached(rays[i].transform) && !rays[i].transform.IsChildOf(allowed)) return false;
        count = gameObject.scene.GetPhysicsScene().OverlapSphere(to,size,overlaps,sceneryMask,QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) { LastQueryOverflow=true; return false; }
        for (int i=0;i<count;i++) if (!Attached(overlaps[i].transform) && !overlaps[i].transform.IsChildOf(allowed)) return false;
        return true;
    }
    bool CachePlants()
    {
        for (int i = 0; i < 2; i++)
        {
            TitanDetachableLimb leg = null;
            foreach (var item in body.limbs)
                if (!item.severed && item.region == (i == 0 ? TitanHitRegion.LeftLeg : TitanHitRegion.RightLeg)) leg = item;
            if (leg == null || leg.bodies.Length < 3) return false;
            for (int j = 0; j < 3; j++) legBones[i*3+j] = leg.bodies[j].transform;
            var ankle = legBones[i*3+2]; BoxCollider shape = null;
            foreach (var box in ankle.GetComponentsInChildren<BoxCollider>()) if (!box.isTrigger && box.attachedRigidbody == leg.bodies[2]) { shape = box; break; }
            if (shape == null) return false;
            flats[i] = transform.rotation * Quaternion.Inverse(Quaternion.Inverse(ankle.rotation) * shape.transform.rotation);
            // Support probes belong to the rig's size, not the human's grab
            // radius. At10x the ankle itself can sit above a fixed10m ray's end.
            float probeScale=Mathf.Max(1,Mathf.Abs(transform.lossyScale.y));
            int count = gameObject.scene.GetPhysicsScene().Raycast(ankle.position + Vector3.up * (4*probeScale), Vector3.down, rays, 10*probeScale, sceneryMask, QueryTriggerInteraction.Ignore);
            if (count == rays.Length) return false;
            float ground = float.NegativeInfinity;
            for (int j = 0; j < count; j++)
                if (!Attached(rays[j].transform) && rays[j].rigidbody == null && rays[j].normal.y > .65f &&
                    rays[j].point.y <= transform.position.y + 1.2f && rays[j].transform.GetComponentInParent<TitanCombatTarget>() == null)
                    ground = Mathf.Max(ground, rays[j].point.y);
            if (float.IsNegativeInfinity(ground)) return false;
            Vector3 offset = Quaternion.Inverse(ankle.rotation) * (shape.transform.TransformPoint(shape.center) - ankle.position);
            plants[i] = new Vector3(ankle.position.x, ground + shape.size.y * Mathf.Abs(shape.transform.lossyScale.y) * .5f + .035f, ankle.position.z) - flats[i] * offset;
        }
        return true;
    }
    bool Attached(Transform bone) { return bone != null && bone.IsChildOf(transform); }
    void Tilt(Transform bone, float angle) { if (Attached(bone)) bone.rotation = Quaternion.AngleAxis(angle, transform.right) * bone.rotation; }
    static void Solve(Transform a, Transform b, Transform end, Vector3 target, Vector3 pole, float weight)
    {
        float first = Vector3.Distance(a.position, b.position), second = Vector3.Distance(b.position, end.position);
        Vector3 direction = target - a.position;
        float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(first-second)+.01f, first+second-.01f);
        direction.Normalize();
        Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
        if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
        float along = (first*first + distance*distance - second*second) / (2*distance);
        Vector3 middle = a.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
        Aim(a, b, middle, weight); Aim(b, end, target, weight);
    }
    static void Aim(Transform bone, Transform child, Vector3 target, float weight)
    {
        Vector3 from = child.position - bone.position, to = target - bone.position;
        if (from.sqrMagnitude > .0001f && to.sqrMagnitude > .0001f)
            bone.rotation = Quaternion.Slerp(bone.rotation, Quaternion.FromToRotation(from, to) * bone.rotation, weight);
    }
    bool ClearRay(Vector3 from, Vector3 to, Transform allowed)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .0001f) return true;
        int count = gameObject.scene.GetPhysicsScene().Raycast(from, delta.normalized, rays, delta.magnitude, sceneryMask, QueryTriggerInteraction.Ignore);
        if (count == rays.Length) return false;
        for (int i = 0; i < count; i++)
            if (rays[i].transform.gameObject.scene == gameObject.scene && !Attached(rays[i].transform) && !rays[i].transform.IsChildOf(allowed)) return false;
        return true;
    }
    void UpdateCue()
    {
        bool atMouth = Stage == TitanGrabStage.ToMouth || Stage == TitanGrabStage.BiteWindup || Stage == TitanGrabStage.Bite;
        bool visible = drawTelegraph && (atMouth || (!IsAerial && (Stage == TitanGrabStage.Windup || Stage == TitanGrabStage.Reach)));
        if (!visible) { if (cue != null) cue.enabled = false; return; }
        if (cue == null)
        {
            var go = new GameObject("Grab fixed aim telegraph"); go.transform.SetParent(transform, false);
            cue = go.AddComponent<LineRenderer>(); cue.loop = true; cue.useWorldSpace = true; cue.positionCount = 48; cue.widthMultiplier = .08f;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit"); if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) { cueMaterial = new Material(shader); cue.sharedMaterial = cueMaterial; }
        }
        cue.enabled = true;
        Color color = Stage == TitanGrabStage.Reach || Stage == TitanGrabStage.Bite ? Color.red : atMouth ? new Color(1,.5f,0) : Color.cyan;
        cue.startColor = cue.endColor = color;
        if (cueMaterial != null) { if (cueMaterial.HasProperty("_BaseColor")) cueMaterial.SetColor("_BaseColor", color); else cueMaterial.color = color; }
        for (int i = 0; i < cue.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / cue.positionCount;
            cue.SetPosition(i, atMouth && Attached(mouth) ? MouthBitePoint + (transform.right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle))*mouthRadius :
                LockedPoint + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
        }
    }
}
