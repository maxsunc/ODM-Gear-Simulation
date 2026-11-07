using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in visual owner. Existing motor retains all physics/input authority.
/// Animator evaluates first; bounded generic-rig arm IK finishes before blade sweeps.</summary>
[DefaultExecutionOrder(90)]
[RequireComponent(typeof(Movement),typeof(ODM_Movement),typeof(Attack))]
public sealed class ScoutPlayerAnimation : MonoBehaviour
{
    // Ground gait dimensions shared with the owned clip authoring recipe.
    public const float RunCycleDuration=.56f,RunStanceFraction=.30f,RunHalfStride=.43f;
    public static float RunPlaybackSpeed(float planarSpeed,float visualScale)
    {
        float strideSpeed=2*RunHalfStride/(RunCycleDuration*RunStanceFraction)*Mathf.Max(.01f,visualScale);
        return Mathf.Clamp(planarSpeed/strideSpeed,.1f,3);
    }
    // Slow body-only entry; the late arm solver still follows both anchors every frame.
    public const float DualHookReclineDuration=.75f,DefaultFallBlendDuration=.5f,HeldCycleDuration=1.8f;
    // One readable back-of-blade beat per slower held loop; no Attack event.
    public static float HeldBackStrike(float phase)
    {
        phase=Mathf.Repeat(phase,1);
        return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.44f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,.82f,phase)));
    }
    [Header("Animation blending")]
    [Tooltip("Seconds to blend from the current animation into Fall. Higher = slower/smoother. Does not change gravity, movement, or attack timing.")]
    [Min(.05f)] public float fallBlendDuration=DefaultFallBlendDuration;
    public static bool IsDualHookFlight(string state) { return state=="GasBoth"||state=="AttachedBoth"; }
    public static bool DeferDualPoseSwitch(string previous,string next,float dualEntryAge)
    {
        return IsDualHookFlight(previous)&&IsDualHookFlight(next)&&dualEntryAge<DualHookReclineDuration;
    }
    public static float PoseTransitionDuration(string previous,string next)
    {
        if(next=="Fall")return DefaultFallBlendDuration;
        if(!IsDualHookFlight(next))return next=="Land"?.045f:.075f;
        if(!IsDualHookFlight(previous))return DualHookReclineDuration;
        return .20f; // Small gas/coast adjustment after the entry has settled.
    }
    public float GetPoseTransitionDuration(string previous,string next)
    {
        return next=="Fall"?Mathf.Max(.05f,fallBlendDuration):PoseTransitionDuration(previous,next);
    }
    public Animator animator;
    public Transform flipPivot,model;
    public Transform leftUpper,leftLower,leftHand,rightUpper,rightLower,rightHand;
    public Renderer[] hiddenRenderers;
    public Animator[] stoppedAnimators;
    [HideInInspector] public Animator originalMovementAnimator,originalGearAnimator,originalAttackAnimator;
    [HideInInspector] public Transform[] originalBlades,originalShooters;
    [HideInInspector] public float originalStrikeDelay,originalActiveDuration=.6f,originalRecoveryDuration;
    [Min(.1f)] public float flipEnvelopeRadius=3.6f;
    public string State { get { return brain.State; } }
    public Vector3 MeasuredVelocity { get; private set; }
    public bool Grounded { get; private set; }
    public float LeftAimWeight { get; private set; }
    public float RightAimWeight { get; private set; }
    public bool IsStruggling { get; private set; }
    readonly ScoutAnimationBrain brain=new ScoutAnimationBrain();
    readonly Collider[] overlaps=new Collider[96];
    readonly RaycastHit[] casts=new RaycastHit[96];
    Movement walking;ODM_Movement gear;Attack attack;PlayerHealth health;TitanCombatTarget combatTarget;
    Vector3 lastPosition,previousFlipPosition,leftAimAxis,rightAimAxis;
    string playing;
    float dualEntryAge=DualHookReclineDuration;
    bool sampled,mirroredAttack,attackVariantChosen;
    Transform heldPivot,heldHips,heldHand,heldForearm,beatUpper,beatLower,beatHand;
    TitanGrabAttack visualHolder;
    BoxCollider heldPalm;
    ScoutCapeCloth heldCloth;
    Vector3 pivotRestPosition,bladeAxis,bladeFlat,heldUp;
    float bladeContactOffset,feedingBlend=1;
    bool mounted;
    System.Random attackRandom=new System.Random();
    public bool MirroredAttack { get { return mirroredAttack; } }
    public string AttackAnimationState(string state)
    {
        return mirroredAttack&&(state.StartsWith("Charge")||state.StartsWith("Attack"))?state+"_Right":state;
    }
    void OnChargeStarted()
    {
        // Separate RNG: cosmetic variation must not perturb Titan/physics randomness.
        mirroredAttack=attackRandom.Next(2)==1;attackVariantChosen=true;
    }
    void Awake()
    {
        walking=GetComponent<Movement>();gear=GetComponent<ODM_Movement>();attack=GetComponent<Attack>();health=GetComponent<PlayerHealth>();
        lastPosition=previousFlipPosition=transform.position;combatTarget=GetComponent<TitanCombatTarget>();
        if(Application.isPlaying)
        {
            var cloth=GetComponent<ScoutCapeCloth>();if(cloth==null)cloth=gameObject.AddComponent<ScoutCapeCloth>();
            if(model!=null && cloth.isActiveAndEnabled)cloth.Initialize(model);
        }
        if(animator!=null) { animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
        // Swords extend along each hand's anatomical rest axis (pistol-grip design).
        if(leftHand!=null)leftAimAxis=leftHand.InverseTransformDirection((leftHand.position-leftLower.position).normalized);
        if(rightHand!=null)rightAimAxis=rightHand.InverseTransformDirection((rightHand.position-rightLower.position).normalized);
    }
    void OnEnable()
    {
        if(attack==null)attack=GetComponent<Attack>();
        attack.ChargeStarted+=OnChargeStarted;attack.Released+=OnStrike;
    }
    void OnDisable()
    {
        if(attack!=null){attack.ChargeStarted-=OnChargeStarted;attack.Released-=OnStrike;}
        attackVariantChosen=false;IsStruggling=false;LeftAimWeight=RightAimWeight=0;
        ResetHeldPresentation();
        if(flipPivot!=null)flipPivot.localRotation=Quaternion.identity;
    }
    public void ResetMotionHistory()
    {
        lastPosition=previousFlipPosition=transform.position;sampled=false;MeasuredVelocity=Vector3.zero;
        brain.Step(new ScoutAnimationBrain.Frame{held=true},0);playing=null;dualEntryAge=DualHookReclineDuration;
        IsStruggling=false;LeftAimWeight=RightAimWeight=0;ResetHeldPresentation();
    }
    void FixedUpdate()
    {
        if(sampled)MeasuredVelocity=(transform.position-lastPosition)/Time.fixedDeltaTime;
        sampled=true;lastPosition=transform.position;
    }
    void OnStrike()
    {
        if(animator==null || PlayerCombatPermission.IsBlocked(this))return;
        bool held=(walking!=null&&walking.TitanSuspended)||(gear!=null&&gear.TitanSuspended);
        if(!attackVariantChosen)OnChargeStarted();
        string clip=AttackAnimationState(held?"AttackHeld":Grounded?"AttackGround":"AttackAir");
        animator.CrossFadeInFixedTime(clip,.025f,0,0);playing=clip;
    }
    void Update()
    {
        if(animator==null||flipPivot==null||walking==null||gear==null)return;
        // Read-only equivalent of the existing ground volume. Do NOT call CheckGround:
        // that legacy method also rotates the gameplay root as a side effect.
        int n=walking.groundCheck==null?0:gameObject.scene.GetPhysicsScene().OverlapBox(walking.groundCheck.position,new Vector3(.1f,.4f,.1f),overlaps,Quaternion.identity,walking.targetLayer,QueryTriggerInteraction.Ignore);
        Grounded=false;
        if(n<overlaps.Length)for(int i=0;i<n;i++)if(!overlaps[i].transform.IsChildOf(transform))Grounded=true;
        int hooks=(gear.LeftHooked?1:0)|(gear.RightHooked?2:0);
        bool held=walking.TitanSuspended||gear.TitanSuspended;
        if(combatTarget==null)combatTarget=GetComponent<TitanCombatTarget>();
        IsStruggling=held&&combatTarget!=null&&combatTarget.IsHeld&&!combatTarget.IsDead&&combatTarget.HoldOwner is TitanGrabAttack;
        bool clear=hooks==0&&!Grounded&&FlipClear();
        string state=brain.Step(new ScoutAnimationBrain.Frame {grounded=Grounded,gas=gear.IsGasing,hooks=hooks,speed=MeasuredVelocity.magnitude,vertical=MeasuredVelocity.y,
            charging=attack.IsCharging,attacking=attack.IsAttacking,dead=health!=null&&health.isDead,held=held,flipClear=clear},Time.deltaTime);
        if(!attack.IsCharging&&!attack.IsAttacking)attackVariantChosen=false;
        string visualState=AttackAnimationState(state);
        // Keep the native entry fade intact across gas toggles. Only the small
        // visual gas/coast adjustment waits; hooks, thrust and arm IK stay live.
        if(visualState!=playing&&!DeferDualPoseSwitch(playing,visualState,dualEntryAge))
        {
            float blendDuration=GetPoseTransitionDuration(playing,visualState);
            if(IsDualHookFlight(visualState)&&!IsDualHookFlight(playing))dualEntryAge=0;
            animator.CrossFadeInFixedTime(visualState,blendDuration,0,0);playing=visualState;
        }
        dualEntryAge=IsDualHookFlight(state)?Mathf.Min(DualHookReclineDuration,dualEntryAge+Time.deltaTime):DualHookReclineDuration;
        // Suspension alone (blocked safe exit/temporary reaction) is NOT a
        // squeezing hand. Freeze the held pose immediately after actual release.
        animator.speed=state=="Held"&&!IsStruggling?0:state=="Run"?RunPlaybackSpeed(Vector3.ProjectOnPlane(MeasuredVelocity,Vector3.up).magnitude,model!=null?model.lossyScale.z:2):1;
        if(state=="ReleaseFlip")
        {
            float f=Mathf.Clamp01(brain.ReleaseAge/.48f);float angle=-360*(f*f*(3-2*f));
            flipPivot.localRotation=Quaternion.AngleAxis(angle,Vector3.right);
        }
        else if(!mounted||state!="Held")flipPivot.localRotation=Quaternion.Slerp(flipPivot.localRotation,Quaternion.identity,1-Mathf.Exp(-30*Time.deltaTime));
        previousFlipPosition=transform.position;
    }
    bool FlipClear()
    {
        var physics=gameObject.scene.GetPhysicsScene();
        int n=physics.OverlapSphere(transform.position,flipEnvelopeRadius,overlaps,~0,QueryTriggerInteraction.Ignore);
        if(n==overlaps.Length)return false;
        for(int i=0;i<n;i++)if(overlaps[i]!=null&&!overlaps[i].transform.IsChildOf(transform))return false;
        Vector3 d=transform.position-previousFlipPosition;float distance=d.magnitude;
        if(distance>.0001f)
        {
            n=physics.SphereCast(previousFlipPosition,flipEnvelopeRadius,d/distance,casts,distance,~0,QueryTriggerInteraction.Ignore);
            if(n==casts.Length)return false;
            for(int i=0;i<n;i++)if(casts[i].collider!=null&&!casts[i].collider.transform.IsChildOf(transform))return false;
        }
        return true;
    }
    void LateUpdate()
    {
        if(animator==null||gear==null)return;
        bool restrained=walking.TitanSuspended||gear.TitanSuspended||(health!=null&&health.isDead);
        bool blocked=attack.IsCharging||attack.IsAttacking||restrained||State=="ReleaseFlip";
        float blend=1-Mathf.Exp(-25*Time.deltaTime);
        bool leftAim=gear.LeftHooked||Input.GetButton("Fire1")||Input.GetKey(KeyCode.Q);
        bool rightAim=gear.RightHooked||Input.GetButton("Fire2")||Input.GetKey(KeyCode.E);
        Vector3 shot=transform.position+transform.forward*Mathf.Max(1,gear.range);
        if((leftAim&&!gear.LeftHooked)||(rightAim&&!gear.RightHooked))
        {
            var camera=gear.camTransform;if(camera!=null)
            {
                RaycastHit hit;shot=gameObject.scene.GetPhysicsScene().Raycast(camera.position,camera.forward,out hit,gear.range,gear.targetLayer,QueryTriggerInteraction.Ignore)?hit.point:camera.position+camera.forward*gear.range;
            }
        }
        // No residual hook/pistol IK fighting the restrained elbows on capture.
        LeftAimWeight=restrained?0:Mathf.Lerp(LeftAimWeight,!blocked&&leftAim?1:0,blend);
        RightAimWeight=restrained?0:Mathf.Lerp(RightAimWeight,!blocked&&rightAim?1:0,blend);
        AimArm(leftUpper,leftLower,leftHand,gear.LeftHooked?gear.LeftAnchor:shot,leftAimAxis,LeftAimWeight,-1);
        AimArm(rightUpper,rightLower,rightHand,gear.RightHooked?gear.RightAnchor:shot,rightAimAxis,RightAimWeight,1);
        UpdateHeldPresentation();
    }
    void ResetHeldPresentation()
    {
        if(heldPivot!=null){heldPivot.localPosition=pivotRestPosition;heldPivot.localRotation=Quaternion.identity;}
        mounted=false;visualHolder=null;feedingBlend=1;
    }
    bool BindHeldPresentation(TitanGrabAttack holder)
    {
        if(model==null||flipPivot==null||flipPivot.parent==null||flipPivot==transform||!flipPivot.IsChildOf(transform)||!model.IsChildOf(flipPivot))return false;
        // A custom installation with physical children is not a passive visual.
        if(flipPivot.GetComponentsInChildren<Collider>(true).Length!=0||flipPivot.GetComponentsInChildren<Rigidbody>(true).Length!=0)return false;
        if(heldPivot!=flipPivot){heldPivot=flipPivot;pivotRestPosition=flipPivot.localPosition;}
        var body=holder.GetComponent<ModularTitanBody>();if(body==null)return false;
        heldHand=holder.UsesLeftHand?body.leftHand:body.rightHand;heldForearm=holder.UsesLeftHand?body.leftLowerArm:body.rightLowerArm;
        heldPalm=null;heldCloth=GetComponent<ScoutCapeCloth>();heldHips=beatUpper=beatLower=beatHand=null;MeshFilter blade=null;
        if(heldHand==null||heldForearm==null)return false;
        foreach(var box in heldHand.GetComponentsInChildren<BoxCollider>())if(!box.isTrigger&&box.attachedRigidbody!=null&&box.attachedRigidbody.transform==heldHand){heldPalm=box;break;}
        foreach(var bone in model.GetComponentsInChildren<Transform>())
        {
            if(bone.name=="Hips")heldHips=bone;else if(bone.name=="RightUpperArm")beatUpper=bone;else if(bone.name=="RightLowerArm")beatLower=bone;else if(bone.name=="RightHand")beatHand=bone;
            else if(bone.name=="ScoutBlade_R")blade=bone.GetComponent<MeshFilter>();
        }
        bladeAxis=bladeFlat=Vector3.zero;bladeContactOffset=0;
        if(blade!=null&&blade.sharedMesh!=null&&beatHand!=null)
        {
            Bounds bounds=blade.sharedMesh.bounds;Vector3 e=bounds.extents;
            Vector3 thin=e.x<e.y&&e.x<e.z?Vector3.right:e.y<e.z?Vector3.up:Vector3.forward;
            Vector3 axis=blade.transform.TransformDirection(bounds.center.normalized);
            bladeAxis=beatHand.InverseTransformDirection(axis).normalized;bladeFlat=beatHand.InverseTransformDirection(blade.transform.TransformDirection(thin)).normalized;
            bladeContactOffset=Vector3.Distance(blade.transform.position,blade.transform.TransformPoint(bounds.center))*.7f;
        }
        visualHolder=holder;feedingBlend=1;heldUp=Vector3.zero;return heldPalm!=null&&heldHips!=null;
    }
    void UpdateHeldPresentation()
    {
        // Render-only mounting. Never move/rotate the gameplay root or native
        // controller, change the CCD grip, reparent the player or drive Titan bones.
        var holder=combatTarget!=null&&combatTarget.IsHeld&&!combatTarget.IsDead?combatTarget.HoldOwner as TitanGrabAttack:null;
        if(holder==null||!IsStruggling)
        {
            if(combatTarget!=null&&combatTarget.ReleasePending)return; // original safe exit still owns controls
            if(heldPivot!=null)heldPivot.localPosition=Vector3.Lerp(heldPivot.localPosition,pivotRestPosition,1-Mathf.Exp(-24*Time.deltaTime));
            mounted=false;visualHolder=null;return;
        }
        if(holder!=visualHolder&&!BindHeldPresentation(holder))return;
        if(heldPivot==null||heldPalm==null||heldHips==null||heldHand==null||heldForearm==null)return;
        bool feeding=holder.Stage==TitanGrabStage.ToMouth||holder.Stage==TitanGrabStage.BiteWindup||holder.Stage==TitanGrabStage.Bite||holder.Stage==TitanGrabStage.Recovery;
        feedingBlend=feeding?Mathf.MoveTowards(feedingBlend,0,Time.deltaTime/.18f):1;
        Vector3 arm=(heldHand.position-heldForearm.position).normalized;if(arm.sqrMagnitude<.5f)return;
        Vector3 up=Vector3.ProjectOnPlane(Vector3.up,arm);if(up.sqrMagnitude<.01f)up=Vector3.ProjectOnPlane(holder.transform.forward,arm);up.Normalize();
        // Parallel-transport the last perpendicular axis through a near-vertical
        // wrist, rather than flipping the Scout180 degrees at the up singularity.
        Vector3 previous=Vector3.ProjectOnPlane(heldUp,arm);
        if(previous.sqrMagnitude>.01f)
        {
            previous.Normalize();if(Vector3.Dot(previous,up)<0)up=-up;
            up=Vector3.Slerp(previous,up,1-Mathf.Exp(-12*Time.deltaTime));
        }
        heldUp=up;
        Quaternion rotation=Quaternion.LookRotation(-arm,up);
        Vector3 half=Vector3.Scale(heldPalm.size,heldPalm.transform.lossyScale)*.5f;
        Vector3 normal=heldPalm.transform.InverseTransformDirection(up);float surface=float.PositiveInfinity;
        for(int axis=0;axis<3;axis++)if(Mathf.Abs(normal[axis])>.0001f)surface=Mathf.Min(surface,Mathf.Abs(half[axis]/normal[axis]));
        float humanHeight=1.8f*Mathf.Abs(model.lossyScale.y);
        Vector3 center=heldPalm.transform.TransformPoint(heldPalm.center);
        // Nest the waist slightly inside the palm shell, at its heel rather
        // than floating beyond the fingertips. The head/torso remain exposed.
        Vector3 waist=center+up*Mathf.Max(0,surface*.96f-humanHeight*.025f)+(heldHand.position-center)*.25f;
        Vector3 hipLocal=heldPivot.InverseTransformPoint(heldHips.position),scaledHip=Vector3.Scale(hipLocal,heldPivot.lossyScale);
        Vector3 basePosition=heldPivot.parent.TransformPoint(pivotRestPosition);
        Vector3 desired=waist-rotation*scaledHip;desired=basePosition+Vector3.ClampMagnitude(desired-basePosition,humanHeight);
        heldPivot.position=Vector3.Lerp(basePosition,desired,feedingBlend);
        heldPivot.rotation=Quaternion.Slerp(heldPivot.parent.rotation,rotation,feedingBlend);
        if(!mounted&&feedingBlend>0&&heldCloth!=null)heldCloth.ResetSimulation();mounted=feedingBlend>0;
        if(!feeding&&beatUpper!=null&&beatLower!=null&&beatHand!=null&&bladeAxis.sqrMagnitude>.5f)
        {
            float phase=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            float beat=HeldBackStrike(phase);
            // Back/flat lands across the exposed heel/wrist surface, not a
            // buried forearm capsule hidden behind the palm. No damage query.
            Vector3 point=center+up*(surface*1.02f+humanHeight*.025f)-arm*(humanHeight*.20f);
            Vector3 across=(-heldPivot.right*.6f-arm*.8f).normalized;
            Quaternion handRotation=Quaternion.LookRotation(across,up)*Quaternion.Inverse(Quaternion.LookRotation(bladeAxis,bladeFlat));
            FitHeldBeat(point-across*bladeContactOffset,handRotation,beat);
        }
    }
    void FitHeldBeat(Vector3 target,Quaternion rotation,float weight)
    {
        if(weight<=.001f)return;
        Quaternion uq=beatUpper.rotation,lq=beatLower.rotation,hq=beatHand.rotation;
        float a=Vector3.Distance(beatUpper.position,beatLower.position),b=Vector3.Distance(beatLower.position,beatHand.position);
        Vector3 delta=target-beatUpper.position;if(delta.sqrMagnitude<.0001f)return;
        Vector3 direction=delta.normalized;float reach=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,(a+b)*.98f);
        Vector3 pole=Vector3.ProjectOnPlane(model.right*.75f-model.forward*.25f,direction).normalized;if(pole.sqrMagnitude<.5f)return;
        float along=(a*a-b*b+reach*reach)/(2*reach);Vector3 elbow=beatUpper.position+direction*along+pole*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        beatUpper.rotation=Quaternion.FromToRotation(beatLower.position-beatUpper.position,elbow-beatUpper.position)*beatUpper.rotation;
        beatLower.rotation=Quaternion.FromToRotation(beatHand.position-beatLower.position,beatUpper.position+direction*reach-beatLower.position)*beatLower.rotation;
        Quaternion solvedU=beatUpper.rotation,solvedL=beatLower.rotation;
        beatUpper.rotation=Quaternion.Slerp(uq,solvedU,weight);beatLower.rotation=Quaternion.Slerp(lq,solvedL,weight);beatHand.rotation=Quaternion.Slerp(hq,rotation,weight);
    }
    void AimArm(Transform upper,Transform lower,Transform hand,Vector3 anchor,Vector3 handAxis,float weight,float side)
    {
        if(upper==null||lower==null||hand==null||weight<.001f)return;
        Quaternion uq=upper.rotation,lq=lower.rotation,hq=hand.rotation;
        Vector3 delta=anchor-upper.position;if(delta.sqrMagnitude<.001f)return;
        Vector3 local=transform.InverseTransformDirection(delta.normalized);
        float yaw=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-110,110);
        float pitch=Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y,-1,1))*Mathf.Rad2Deg,-75,80);
        Vector3 direction=transform.TransformDirection(Quaternion.Euler(-pitch,yaw,0)*Vector3.forward);
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
        float reach=Mathf.Min(delta.magnitude,(a+b)*.94f);reach=Mathf.Max(Mathf.Abs(a-b)+.001f,reach);
        Vector3 target=upper.position+direction*reach;
        Vector3 pole=transform.forward*.45f+transform.right*side*.35f-transform.up*.65f;
        pole=Vector3.ProjectOnPlane(pole,direction).normalized;if(pole.sqrMagnitude<.01f)return;
        float along=(a*a-b*b+reach*reach)/(2*reach);float bend=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        Vector3 elbow=upper.position+direction*along+pole*bend;
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
        hand.rotation=Quaternion.FromToRotation(hand.TransformDirection(handAxis),direction)*hand.rotation;
        Quaternion solvedU=upper.rotation,solvedL=lower.rotation,solvedH=hand.rotation;
        upper.rotation=Quaternion.Slerp(uq,solvedU,weight);lower.rotation=Quaternion.Slerp(lq,solvedL,weight);hand.rotation=Quaternion.Slerp(hq,solvedH,weight);
    }
}
