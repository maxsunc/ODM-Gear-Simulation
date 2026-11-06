using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Exclusive temporary native ragdoll. Real motors/input are suspended;
/// only the disabled controller/camera root follows the physical hips. Stand-up
/// requires support AND full world-up controller/gear clearance at those hips.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(85)]
public sealed class ScoutStompReaction : MonoBehaviour
{
    [Min(.2f)]public float minimumDownTime=2;
    [Range(0,12)]public float knockbackSpeed=9;
    [Range(0,6)]public float upwardSpeed=3;
    [Min(.1f)]public float maximumGetUpSpeed=3;
    public bool IsActive {get;private set;}
    public bool GetUpPending {get;private set;}
    public int ReactionCount {get;private set;}
    public Vector3 LastImpulse {get;private set;}
    public string Error {get;private set;}
    PlayerHealth health;ScoutPlayerRagdoll presentation;TitanODMMotor motor;CharacterController controller;Transform root;
    float age;Vector3 previousHips;
    Collider[] colliders;bool[] collisionStates;Renderer[] renderers;bool[] renderStates;Animator[] animators;bool[] animatorStates;
    Behaviour[] visualScripts;bool[] scriptStates;
    Rigidbody[] bodies;bool[] kinematic,gravity,contacts;
    TitanReleaseClearance clearance;readonly RaycastHit[] supportHits=new RaycastHit[64];
    void Awake(){root=transform;health=GetComponent<PlayerHealth>();motor=GetComponent<TitanODMMotor>();controller=GetComponent<CharacterController>();}
    public bool Begin(Vector3 stompPoint,Transform attacker)
    {
        if(health==null || health.isDead || Time.timeScale<=0 || !isActiveAndEnabled || motor==null || controller==null)return false;
        Vector3 away=Vector3.ProjectOnPlane(root.position-stompPoint,Vector3.up).normalized;
        if(away.sqrMagnitude<.5f)away=attacker!=null?Vector3.ProjectOnPlane(root.position-attacker.position,Vector3.up).normalized:Vector3.back;
        if(away.sqrMagnitude<.5f)away=-root.forward;
        LastImpulse=away*Safe(knockbackSpeed,9,0,12)+Vector3.up*Safe(upwardSpeed,3,0,6);
        if(IsActive)
        {
            foreach(var rb in presentation.Bodies)if(rb!=null)rb.AddForce(Vector3.ClampMagnitude(rb.velocity+LastImpulse,18)-rb.velocity,ForceMode.VelocityChange);
            age=0;ReactionCount++;return true;
        }
        var target=GetComponent<TitanCombatTarget>();if(target!=null&&(target.IsHeld||target.ReleasePending))return false;
        presentation=health.EnsureScoutRagdoll();if(presentation==null || presentation.Corpse!=null || !motor.CanSuspend)return false;
        if(!presentation.CaptureDeathPose())return false;
        Cache();var solids=new List<Collider>();foreach(var c in colliders)if(c.enabled&&!c.isTrigger){if(!TitanReleaseClearance.Supported(c)){Error="Unsupported solid gear shape";return false;}solids.Add(c);}
        clearance=new TitanReleaseClearance(root,solids.ToArray());Vector3 velocity=Vector3.ClampMagnitude(motor.Velocity,6)+LastImpulse;
        if(!motor.TrySuspend())return false;
        if(!presentation.ActivateTemporary(attacker,velocity)){Error=presentation.Error;RestoreFlags();motor.Resume(Vector3.zero);return false;}
        var proxy=presentation.Corpse.AddComponent<TitanCombatProxy>();proxy.target=target;
        foreach(var script in visualScripts)if(script!=null)script.enabled=false;
        previousHips=presentation.Hips.position;age=0;IsActive=true;GetUpPending=false;ReactionCount++;Error=null;return true;
    }
    void Cache()
    {
        colliders=GetComponentsInChildren<Collider>(true);collisionStates=Array.ConvertAll(colliders,c=>c.enabled);
        renderers=presentation.visualRoot.GetComponentsInChildren<Renderer>(true);renderStates=Array.ConvertAll(renderers,r=>r.enabled);
        animators=GetComponentsInChildren<Animator>(true);animatorStates=Array.ConvertAll(animators,a=>a.enabled);
        var list=new List<Behaviour>();var driver=GetComponent<ScoutPlayerAnimation>();if(driver!=null)list.Add(driver);var cape=GetComponent<ScoutCapeCloth>();if(cape!=null)list.Add(cape);
        visualScripts=list.ToArray();scriptStates=Array.ConvertAll(visualScripts,s=>s.enabled);
        bodies=GetComponentsInChildren<Rigidbody>(true);kinematic=Array.ConvertAll(bodies,b=>b.isKinematic);gravity=Array.ConvertAll(bodies,b=>b.useGravity);contacts=Array.ConvertAll(bodies,b=>b.detectCollisions);
    }
    void LateUpdate(){TickReaction(Time.deltaTime);}
    public void TickReaction(float dt)
    {
        if(!IsActive || root==null || health==null)return;
        if(health.isDead){IsActive=GetUpPending=false;return;}
        if(presentation==null)return;
        if(presentation.Hips!=null)
        {
            Vector3 now=presentation.Hips.position;root.position+=now-previousHips;previousHips=now;
            presentation.RestoreAttackerContactsWhenClear();
        }
        if(Time.timeScale<=0 || float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)return;
        age+=dt;if(age<Safe(minimumDownTime,2,.2f,10))return;
        GetUpPending=true;
        foreach(var rb in presentation.Bodies)if(rb!=null && rb.velocity.magnitude>Safe(maximumGetUpSpeed,3,.1f,8))return;
        Vector3 point;if(!TryStandingPoint(out point))return;
        presentation.EndTemporary();root.position=point;
        var driver=root.GetComponent<ScoutPlayerAnimation>();if(driver!=null)driver.ResetMotionHistory();
        RestoreFlags();IsActive=GetUpPending=false;
        motor.Resume(Vector3.zero);motor.ResetMotionHistory();var feedback=root.GetComponent<TitanSwordFeedback>();if(feedback!=null)feedback.ResetDamageHistory();
    }
    bool TryStandingPoint(out Vector3 point)
    {
        point=root.position;Vector3 hips=presentation.Hips!=null?presentation.Hips.position:root.position;
        var scene=root.gameObject.scene.GetPhysicsScene();int n=scene.Raycast(hips+Vector3.up,Vector3.down,supportHits,8,~0,QueryTriggerInteraction.Ignore);
        if(n>=supportHits.Length)return false;float highest=float.NegativeInfinity;
        for(int i=0;i<n;i++)
        {
            var h=supportHits[i];if(h.collider==null || h.rigidbody!=null || h.transform.IsChildOf(root) || (presentation.Corpse!=null&&h.transform.IsChildOf(presentation.Corpse.transform)) || h.normal.y<Mathf.Cos(controller.slopeLimit*Mathf.Deg2Rad))continue;
            if(h.point.y>highest){highest=h.point.y;point=h.point;}
        }
        if(float.IsNegativeInfinity(highest))return false;
        point+=Vector3.up*(controller.height*Mathf.Abs(root.lossyScale.y)*.5f+controller.skinWidth+.03f)-root.TransformVector(controller.center);
        return clearance.CanStandAt(point,presentation.Corpse!=null?presentation.Corpse.transform:null);
    }
    void RestoreFlags()
    {
        for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=collisionStates[i];
        for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].enabled=renderStates[i];
        for(int i=0;i<animators.Length;i++)if(animators[i]!=null)animators[i].enabled=animatorStates[i];
        for(int i=0;i<visualScripts.Length;i++)if(visualScripts[i]!=null)visualScripts[i].enabled=scriptStates[i];
        for(int i=0;i<bodies.Length;i++)if(bodies[i]!=null){bodies[i].isKinematic=kinematic[i];bodies[i].useGravity=gravity[i];bodies[i].detectCollisions=contacts[i];}
    }
    void OnDisable()
    {
        if(IsActive&&motor!=null){age=10;motor.ContinueRelease(()=>{TickReaction(0);return !IsActive;});}
    }
    static float Safe(float value,float fallback,float min,float max){return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max);}
}
