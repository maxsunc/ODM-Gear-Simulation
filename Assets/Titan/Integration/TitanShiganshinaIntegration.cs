using UnityEngine;
using UnityEngine.AI;

/// <summary>Explicit scene opt-in. Does not change the passive body/player prefabs
/// or spawn lab controllers. Existing gear, camera, keys and real HP remain in use.</summary>
[DefaultExecutionOrder(-500)]
public class TitanShiganshinaIntegration : MonoBehaviour
{
    public Movement player;
    public ModularTitanBody[] titans=new ModularTitanBody[0];
    public TitanNavigationSurface navigation;
    public string SetupError { get; private set; }
    void Start()
    {
        if(player==null || navigation==null || !navigation.Build())
        { SetupError=player==null?"Player not assigned":navigation==null?"Navigation not assigned":navigation.Error;Debug.LogError(SetupError,this);return; }
        foreach(var titan in titans)
        {
            if(titan==null) continue;
            var agent=titan.gameObject.AddComponent<NavMeshAgent>();agent.enabled=false;
            agent.agentTypeID=navigation.agentTypeID;agent.radius=navigation.agentRadius;agent.height=navigation.agentHeight;
            agent.baseOffset=0;agent.acceleration=26;agent.angularSpeed=330;
            agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;agent.enabled=true;
            var brain=titan.gameObject.AddComponent<TitanPatrolChaseAI>();brain.target=player.transform;
            brain.targetSightOffset=Vector3.zero; // This player's CC is centered on its ROOT, not feet.
            Vector3 origin=titan.transform.position;
            brain.patrolPoints=new[]{origin,origin+Vector3.right*16,origin+Vector3.forward*16};
            titan.gameObject.AddComponent<TitanStompAttack>();
            var grab=titan.gameObject.AddComponent<TitanGrabAttack>();grab.enableBite=true;
            titan.gameObject.AddComponent<TitanCombatDirector>();
        }
    }
    void OnGUI()
    {
        if(player==null) return;var health=player.GetComponent<PlayerHealth>();var target=player.GetComponent<TitanCombatTarget>();
        GUILayout.BeginArea(new Rect(12,12,390,100),GUI.skin.box);
        GUILayout.Label(SetupError!=null?SetupError:"ODM / MODULAR TITAN COMBAT");
        if(health!=null) GUILayout.Label("HP "+health.currentHP.ToString("F0")+" / "+health.maxHP.ToString("F0")+
            (health.isDead?" — respawning":target!=null && target.ReleasePending?" — clearing release":target!=null && target.IsHeld?" — HELD: swords still active":""));
        GUILayout.Label("Mouse / Q,E: hooks | Space: gas | Shift: boost | C: jump");
        GUILayout.Label("Hold/release F: swords | J: existing menu | T: music");
        GUILayout.EndArea();
    }
}
