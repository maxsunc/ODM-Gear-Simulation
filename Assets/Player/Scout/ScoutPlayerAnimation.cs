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
    public const float DualHookReclineDuration=.75f,DefaultFallBlendDuration=.5f;
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
    readonly ScoutAnimationBrain brain=new ScoutAnimationBrain();
    readonly Collider[] overlaps=new Collider[96];
    readonly RaycastHit[] casts=new RaycastHit[96];
    Movement walking;ODM_Movement gear;Attack attack;PlayerHealth health;
    Vector3 lastPosition,previousFlipPosition,leftAimAxis,rightAimAxis;
    string playing;
    float dualEntryAge=DualHookReclineDuration;
    bool sampled;
    void Awake()
    {
        walking=GetComponent<Movement>();gear=GetComponent<ODM_Movement>();attack=GetComponent<Attack>();health=GetComponent<PlayerHealth>();
        lastPosition=previousFlipPosition=transform.position;
        if(animator!=null) { animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
        // Swords extend along each hand's anatomical rest axis (pistol-grip design).
        if(leftHand!=null)leftAimAxis=leftHand.InverseTransformDirection((leftHand.position-leftLower.position).normalized);
        if(rightHand!=null)rightAimAxis=rightHand.InverseTransformDirection((rightHand.position-rightLower.position).normalized);
    }
    void OnEnable()
    {
        if(attack==null)attack=GetComponent<Attack>();
        attack.Released+=OnStrike;
    }
    void OnDisable()
    {
        if(attack!=null)attack.Released-=OnStrike;
        if(flipPivot!=null)flipPivot.localRotation=Quaternion.identity;
    }
    void FixedUpdate()
    {
        if(sampled)MeasuredVelocity=(transform.position-lastPosition)/Time.fixedDeltaTime;
        sampled=true;lastPosition=transform.position;
    }
    void OnStrike()
    {
        if(animator==null)return;
        bool held=(walking!=null&&walking.TitanSuspended)||(gear!=null&&gear.TitanSuspended);
        string clip=held?"AttackHeld":Grounded?"AttackGround":"AttackAir";
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
        bool clear=hooks==0&&!Grounded&&FlipClear();
        string state=brain.Step(new ScoutAnimationBrain.Frame {grounded=Grounded,gas=gear.IsGasing,hooks=hooks,speed=MeasuredVelocity.magnitude,vertical=MeasuredVelocity.y,
            charging=attack.IsCharging,attacking=attack.IsAttacking,dead=health!=null&&health.isDead,held=held,flipClear=clear},Time.deltaTime);
        // Keep the native entry fade intact across gas toggles. Only the small
        // visual gas/coast adjustment waits; hooks, thrust and arm IK stay live.
        if(state!=playing&&!DeferDualPoseSwitch(playing,state,dualEntryAge))
        {
            float blendDuration=GetPoseTransitionDuration(playing,state);
            if(IsDualHookFlight(state)&&!IsDualHookFlight(playing))dualEntryAge=0;
            animator.CrossFadeInFixedTime(state,blendDuration,0,0);playing=state;
        }
        dualEntryAge=IsDualHookFlight(state)?Mathf.Min(DualHookReclineDuration,dualEntryAge+Time.deltaTime):DualHookReclineDuration;
        animator.speed=state=="Run"?RunPlaybackSpeed(Vector3.ProjectOnPlane(MeasuredVelocity,Vector3.up).magnitude,model!=null?model.lossyScale.z:2):1;
        if(state=="ReleaseFlip")
        {
            float f=Mathf.Clamp01(brain.ReleaseAge/.48f);float angle=-360*(f*f*(3-2*f));
            flipPivot.localRotation=Quaternion.AngleAxis(angle,Vector3.right);
        }
        else flipPivot.localRotation=Quaternion.Slerp(flipPivot.localRotation,Quaternion.identity,1-Mathf.Exp(-30*Time.deltaTime));
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
        bool blocked=attack.IsCharging||attack.IsAttacking||walking.TitanSuspended||gear.TitanSuspended||(health!=null&&health.isDead)||State=="ReleaseFlip";
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
        LeftAimWeight=Mathf.Lerp(LeftAimWeight,!blocked&&leftAim?1:0,blend);
        RightAimWeight=Mathf.Lerp(RightAimWeight,!blocked&&rightAim?1:0,blend);
        AimArm(leftUpper,leftLower,leftHand,gear.LeftHooked?gear.LeftAnchor:shot,leftAimAxis,LeftAimWeight,-1);
        AimArm(rightUpper,rightLower,rightHand,gear.RightHooked?gear.RightAnchor:shot,rightAimAxis,RightAimWeight,1);
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
