using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Opt-in FreeRoam navigation experiment. Preserves scene actor transforms,
/// uses per-size clearance and existing perception/motion; combat is an explicit test toggle.</summary>
[DefaultExecutionOrder(-500)]
public class TitanFreeRoamIntegration : MonoBehaviour
{
    public Movement player;
    public ModularTitanBody[] titans=new ModularTitanBody[0];
    public TitanWorldNavigation navigation;
    [Header("Perception (world units, not multiplied by scale)")]
    [Min(1)] public float sightRange=138;
    [Range(1,360)] public float fieldOfView=180;
    [Header("Navigation experiment")]
    [Min(1)] public float patrolRadius=200;
    [Min(.1f)] public float spawnProjectionDistance=3;
    public bool enableCombat;
    public string SetupError { get; private set; }
    readonly List<NavMeshAgent> agents=new List<NavMeshAgent>();
    void Start()
    {
        if(player==null || navigation==null || !navigation.IsReady)
        { SetupError=player==null?"Assign real player":navigation==null?"Assign baked navigation":navigation.Error??"Enable baked navigation registration";return; }
        foreach(var body in titans)
        {
            if(body==null) continue;
            Vector3 scale=body.transform.lossyScale;var profile=navigation.ForScale(scale.x);
            if(profile==null || scale.x<=0 || Mathf.Abs(scale.x-scale.y)>.001f || Mathf.Abs(scale.x-scale.z)>.001f)
            { SetupError="Titan scale changed/nonuniform: rebake navigation for "+body.name;continue; }
            if(body.GetComponent<NavMeshAgent>()!=null || body.GetComponent<TitanPatrolChaseAI>()!=null)
            { SetupError="Remove duplicate AI from "+body.name;continue; }
            var filter=new NavMeshQueryFilter {agentTypeID=profile.agentTypeID,areaMask=NavMesh.AllAreas};NavMeshHit hit;
            if(!NavMesh.SamplePosition(body.transform.position,out hit,spawnProjectionDistance,filter))
            { SetupError=body.name+" is off its size's NavMesh; place it on clear ground and rebake";continue; }
            // Add disabled, before first animation/attack frame; avoids briefly binding
            // a newly enabled agent to the unrelated default Humanoid type.
            body.gameObject.SetActive(false);
            var agent=body.gameObject.AddComponent<NavMeshAgent>();agent.enabled=false;
            profile.ConfigureAgent(agent,scale);
            agent.acceleration=26;agent.angularSpeed=330;agent.baseOffset=0;
            agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            body.gameObject.SetActive(true);agent.enabled=true;
            if(!agent.Warp(hit.position)) { agent.enabled=false;SetupError="Agent placement failed for "+body.name;continue; }
            agents.Add(agent);
            var brain=body.gameObject.AddComponent<TitanPatrolChaseAI>();brain.target=player.transform;
            brain.targetSightOffset=Vector3.zero;brain.sightRange=sightRange;brain.fieldOfView=fieldOfView;
            Vector3 origin=hit.position;
            brain.patrolPoints=new[]{origin,origin+Vector3.right*patrolRadius,origin+Vector3.forward*patrolRadius,
                origin-Vector3.right*patrolRadius,origin-Vector3.forward*patrolRadius};
            if(enableCombat)
            {
                body.gameObject.AddComponent<TitanStompAttack>();
                var grab=body.gameObject.AddComponent<TitanGrabAttack>();grab.enableBite=true;
                body.gameObject.AddComponent<TitanCombatDirector>();
            }
        }
    }
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12,12,500,145),GUI.skin.box);
        GUILayout.Label("FREEROAM FULL-MAP NAV TEST / combat "+(enableCombat?"ON":"OFF"));
        if(SetupError!=null) GUILayout.Label(SetupError);
        else GUILayout.Label("Baked sources: "+(navigation!=null?navigation.sourceCount:0)+" | Sight "+sightRange+"m / "+fieldOfView+"deg");
        foreach(var agent in agents)
        {
            if(agent==null) continue;var brain=agent.GetComponent<TitanPatrolChaseAI>();
            GUILayout.Label(agent.name+": "+brain.State+" / "+brain.NavigationStatus+
                (agent.enabled && agent.isOnNavMesh && agent.hasPath?" / "+agent.remainingDistance.ToString("F0")+"m":""));
        }
        GUILayout.Label("Existing controls unchanged. Select setup in Edit Mode to tune sight/combat.");
        GUILayout.EndArea();
    }
    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;
        Gizmos.color=Color.cyan;
        foreach(var a in agents) if(a!=null && a.enabled && a.isOnNavMesh && a.hasPath)
        { var corners=a.path.corners;for(int i=1;i<corners.Length;i++)Gizmos.DrawLine(corners[i-1]+Vector3.up,corners[i]+Vector3.up); }
    }
}
