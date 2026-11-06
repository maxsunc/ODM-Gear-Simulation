using UnityEngine;
using UnityEngine.AI;

/// <summary>Runtime-only ownership and lifecycle for ONE initialized enemy.
/// Never repairs severed rigs, resurrects corpses, or resets health on re-registration.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody))]
public sealed class TitanRuntimeActor : MonoBehaviour
{
    public TitanFreeRoamIntegration Owner { get; private set; }
    public bool IsReady { get; private set; }
    public string Error { get; private set; }
    public bool IsInitializing { get; private set; }
    public NavMeshAgent Agent { get; private set; }
    public TitanPatrolChaseAI Brain { get; private set; }
    ModularTitanBody body;
    TitanStompAttack stomp;
    TitanGrabAttack grab;
    TitanCombatDirector director;
    Vector3 registeredScale;

    internal bool Initialize(TitanFreeRoamIntegration owner, Vector3 position,
        TitanWorldNavigation.Profile profile, out string error)
    {
        Owner=owner;body=GetComponent<ModularTitanBody>();registeredScale=transform.lossyScale;
        bool active=gameObject.activeSelf;IsInitializing=true;
        try
        {
            // Add the agent while INACTIVE so it never briefly binds to Humanoid.
            gameObject.SetActive(false);
            if(owner.useRegionalHealth)body.useRegionalHealth=true;
            if(GetComponent<TitanAudioEmitter>()==null)gameObject.AddComponent<TitanAudioEmitter>();
            Agent=gameObject.AddComponent<NavMeshAgent>();Agent.enabled=false;
            profile.ConfigureAgent(Agent,registeredScale);Agent.acceleration=26;Agent.angularSpeed=330;Agent.baseOffset=0;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            Brain=gameObject.AddComponent<TitanPatrolChaseAI>();Brain.enabled=false;
            Brain.target=owner.player.transform;Brain.targetSightOffset=Vector3.zero;
            Brain.sightRange=owner.sightRange;Brain.fieldOfView=owner.fieldOfView;
            Brain.projectionDistance=owner.spawnProjectionDistance;
            float r=owner.patrolRadius;
            Brain.patrolPoints=new[]{position,position+Vector3.right*r,position+Vector3.forward*r,
                position-Vector3.right*r,position-Vector3.forward*r};
            stomp=gameObject.AddComponent<TitanStompAttack>();stomp.enabled=false;stomp.drawTelegraph=false;
            stomp.damage=owner.stompDamage;stomp.cooldownDuration=owner.stompCooldown;
            gameObject.AddComponent<TitanStompDebris>();
            Brain.chaseStoppingDistance*=registeredScale.y;
            grab=gameObject.AddComponent<TitanGrabAttack>();grab.enabled=false;grab.enableBite=true;grab.drawTelegraph=false;grab.holdDamagePerSecond=owner.grabDamagePerSecond;
            director=gameObject.AddComponent<TitanCombatDirector>();director.enabled=false;director.groundStompChance=owner.groundStompChance;
            if(owner.configureTitanLayer)foreach(var t in GetComponentsInChildren<Transform>(true))t.gameObject.layer=owner.titanLayer;
            transform.position=position;
            body.Died+=OnDied;
            gameObject.SetActive(active);
        }
        finally { IsInitializing=false; }
        if(!active) { error=null;return true; }
        return TryResume(out error);
    }
    void OnEnable() { if(Owner!=null && !IsInitializing) { string error;TryResume(out error); } }
    void OnDisable() { if(!IsInitializing)Suspend(); }
    void OnDestroy() { if(body!=null)body.Died-=OnDied; }
    void OnDied() { Suspend(); }

    public bool TryResume(out string error)
    {
        error=null;
        if(Owner==null || !Owner.isActiveAndEnabled || Owner.player==null || body==null || body.IsDead)
            return Fail("Owner/target unavailable or Titan is dead",out error);
        if(!TitanSpawnPlacement.Finite(transform.lossyScale) || (registeredScale-transform.lossyScale).sqrMagnitude>.000001f)
        {Suspend();return Fail("Initialized Titan scale changed; despawn and spawn a fresh supported-size actor",out error);}
        if(IsReady)return true; // Do not reset an attack/health/pose on duplicate registration.
        if(!enabled || !gameObject.activeInHierarchy)return Fail("Titan is inactive; waiting for activation",out error);
        body.Initialize();body.GetHealth(TitanHitRegion.Nape);
        Vector3 point;TitanWorldNavigation.Profile profile;
        Physics.SyncTransforms();
        if(!TitanSpawnPlacement.TryFind(Owner.navigation,gameObject.scene,transform.position,transform.rotation,transform.lossyScale,
            Owner.spawnProjectionDistance,transform,out point,out profile,out error) || !TitanSpawnPlacement.BodyIsClear(body,point,out error))
        {Error=error;return false;}
        Agent.enabled=true;
        if(!Agent.isOnNavMesh || !Agent.Warp(point))
        {Agent.enabled=false;return Fail("Agent could not bind to its existing navigation profile",out error);}
        Brain.enabled=true;IsReady=true;Error=null;ApplyCombatPolicy();return true;
    }
    bool Fail(string reason,out string error) { Error=error=reason;return false; }
    public void ApplyCombatPolicy()
    {
        if(!IsReady)return;
        bool combat=Owner!=null && Owner.enableCombat;
        if(!combat)
        {
            director.CancelAttacks("Combat disabled");director.SetAutomaticAttacks(false);
            director.enabled=false;grab.enabled=false;stomp.enabled=false;
        }
        else
        {
            stomp.automaticAttack=false;stomp.enabled=true;grab.enabled=true;
            director.enabled=true;director.SetAutomaticAttacks(true);
        }
    }
    public void Suspend()
    {
        IsReady=false;
        if(director!=null) { director.CancelAttacks("Runtime Titan suspended");director.enabled=false; }
        if(grab!=null)grab.enabled=false;if(stomp!=null)stomp.enabled=false;
        if(Brain!=null)Brain.enabled=false;
        if(Agent!=null) { if(Agent.enabled && Agent.isOnNavMesh) {Agent.ResetPath();Agent.isStopped=true;} Agent.enabled=false; }
        if(body!=null)body.SetWalking(false);
    }
}
