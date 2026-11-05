using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Opt-in scene runtime registry. Placed and spawned bodies share ONE
/// initialization path and already-baked size profiles. No per-spawn navigation bake.</summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class TitanFreeRoamIntegration : MonoBehaviour
{
    public Movement player;
    [Tooltip("Optional explicit placed actors. Inactive actors stay inactive until enabled.")]
    public ModularTitanBody[] titans=new ModularTitanBody[0];
    public TitanWorldNavigation navigation;
    [Header("Perception (world units, not multiplied by scale)")]
    [Min(1)] public float sightRange=138;
    [Range(1,360)] public float fieldOfView=180;
    [Header("Navigation experiment")]
    [Min(1)] public float patrolRadius=200;
    [Min(.1f)] public float spawnProjectionDistance=3;
    public bool enableCombat;
    [Header("Runtime actors")]
    [Tooltip("Discover active scene Titans once, then register new/enabled bodies through lifecycle events; no recurring world scan.")]
    public bool autoRegisterSceneTitans=true;
    [Tooltip("Extra scales to bake ONCE for future spawns, even with no placed Titan of that size. Existing baked sizes are retained on rebake.")]
    public float[] additionalBakeScales=new float[0];
    public bool useRegionalHealth=true;
    public bool configureTitanLayer=true;
    [Range(0,31)] public int titanLayer=9;
    public bool showDiagnostics=true;
    public string SetupError { get; private set; }
    public int RegisteredCount { get { return actors.Count; } }
    public int ActiveCount { get { int n=0;foreach(var a in actors.Values)if(a!=null&&a.IsReady)n++;return n; } }
    public int FailureCount { get { return errors.Count; } }
    public int LivingCount { get { int n=0;foreach(var p in actors)if(p.Key!=null && !p.Key.IsDead && p.Key.gameObject.activeInHierarchy)n++;return n; } }
    public int PreparedCount { get { return prepared.Count; } }
    readonly HashSet<ModularTitanBody> prepared=new HashSet<ModularTitanBody>();
    readonly Dictionary<ModularTitanBody,TitanRuntimeActor> actors=new Dictionary<ModularTitanBody,TitanRuntimeActor>();
    readonly Dictionary<ModularTitanBody,string> errors=new Dictionary<ModularTitanBody,string>();
    readonly HashSet<ModularTitanBody> pending=new HashSet<ModularTitanBody>();
    readonly List<ModularTitanBody> work=new List<ModularTitanBody>();
    bool started,appliedCombat;
    GameObject staging;
    float cleanupAge;

    void OnEnable()
    {
        if(!Application.isPlaying)return;
        ModularTitanBody.BecameActive+=OnBodyActive;ModularTitanBody.BecameInactive+=OnBodyInactive;
        started=false;
    }
    void OnDisable()
    {
        ModularTitanBody.BecameActive-=OnBodyActive;ModularTitanBody.BecameInactive-=OnBodyInactive;
        foreach(var a in actors.Values)if(a!=null)a.Suspend();pending.Clear();
    }
    void OnDestroy() { if(staging!=null)Destroy(staging); }
    void OnBodyActive(ModularTitanBody body)
    {
        // Do not inspect an incompletely cloned root here. Scene/scale/parent can
        // still change immediately after Instantiate; validate in next Update.
        if(autoRegisterSceneTitans || actors.ContainsKey(body)) {pending.Add(body);return;}
        if(titans!=null)foreach(var explicitBody in titans)if(explicitBody==body) {pending.Add(body);break;}
    }
    void OnBodyInactive(ModularTitanBody body)
    {
        pending.Remove(body);TitanRuntimeActor a;
        if(actors.TryGetValue(body,out a) && a!=null && !a.IsInitializing)a.Suspend();
    }
    bool Ready(out string error)
    {
        error=null;
        if(!isActiveAndEnabled)error="Titan scene manager is disabled";
        else if(player==null || player.gameObject.scene!=gameObject.scene)error="Assign this scene's real player";
        else if(enableCombat && player.GetComponent<TitanCombatTarget>()==null)error="Install the player's TitanCombatTarget bridge before enabling combat";
        else if(navigation==null || !navigation.isActiveAndEnabled || !navigation.IsReady || navigation.gameObject.scene!=gameObject.scene)
            error=navigation!=null?navigation.Error??"Enable this scene's baked navigation":"Assign baked navigation";
        return error==null;
    }
    void Start() { Begin(); }
    void Begin()
    {
        string error;if(!Ready(out error)) {SetupError=error;return;}
        started=true;appliedCombat=enableCombat;SetupError=null;
        if(titans!=null)foreach(var b in titans)if(b!=null&&b.gameObject.activeInHierarchy)pending.Add(b);
        foreach(var pair in actors)if(pair.Key!=null&&pair.Key.gameObject.activeInHierarchy)pending.Add(pair.Key);
        if(autoRegisterSceneTitans)foreach(var root in gameObject.scene.GetRootGameObjects())
            foreach(var b in root.GetComponentsInChildren<ModularTitanBody>(false))pending.Add(b);
        ProcessPending();
    }
    void Update()
    {
        string error;
        if(!Ready(out error))
        {
            if(started)foreach(var a in actors.Values)if(a!=null)a.Suspend();
            started=false;SetupError=error;return;
        }
        if(!started) {Begin();return;}
        if(appliedCombat!=enableCombat) {appliedCombat=enableCombat;foreach(var a in actors.Values)if(a!=null)a.ApplyCombatPolicy();}
        ProcessPending();
        cleanupAge+=Time.deltaTime;
        if(cleanupAge>=1) {cleanupAge=0;RemoveDestroyed();}
    }
    void ProcessPending()
    {
        work.Clear();work.AddRange(pending);pending.Clear();
        foreach(var body in work)
        {
            if(body==null || body.IsDead || body.gameObject.scene!=gameObject.scene || !body.gameObject.activeInHierarchy)continue;
            string error;TryRegister(body,out error);
        }
        work.Clear();
    }
    void RemoveDestroyed()
    {
        work.Clear();foreach(var p in actors)if(p.Key==null||p.Value==null)work.Add(p.Key);
        foreach(var b in work) {actors.Remove(b);prepared.Remove(b);}
        work.Clear();foreach(var p in errors)if(p.Key==null)work.Add(p.Key);
        foreach(var b in work)errors.Remove(b);work.Clear();
    }
    bool Failure(ModularTitanBody body,string reason,out string error)
    {
        error=reason;if(body!=null && body.gameObject.scene==gameObject.scene)errors[body]=reason;return false;
    }
    public string GetError(ModularTitanBody body) { string error;return body!=null&&errors.TryGetValue(body,out error)?error:null; }
    public bool HasLivingActorWithin(Vector3 point,float distance,ModularTitanBody ignore=null)
    {
        foreach(var body in actors.Keys)if(body!=null && body!=ignore && !body.IsDead && (body.gameObject.activeInHierarchy||prepared.Contains(body)) &&
            Vector3.ProjectOnPlane(body.transform.position-point,Vector3.up).sqrMagnitude<distance*distance)return true;
        return false;
    }

    public bool TryGetBakeScales(out float[] result,out string error)
    {
        result=null;error=null;var sizes=new SortedSet<float>();var bodies=new HashSet<ModularTitanBody>();
        if(titans!=null)foreach(var b in titans)if(b!=null)bodies.Add(b);
        if(autoRegisterSceneTitans)foreach(var root in gameObject.scene.GetRootGameObjects())
            foreach(var b in root.GetComponentsInChildren<ModularTitanBody>(true))bodies.Add(b);
        foreach(var b in bodies)
        {
            var s=b.transform.lossyScale;
            if(!TitanSpawnPlacement.Finite(s)||s.x<=0||Mathf.Abs(s.x-s.y)>.001f||Mathf.Abs(s.x-s.z)>.001f)
            {error="Uniform positive finite Titan scale required: "+b.name;return false;}
            sizes.Add(Mathf.Round(s.x*1000)/1000);
        }
        if(navigation!=null && navigation.profiles!=null)foreach(var p in navigation.profiles)if(p!=null)sizes.Add(Mathf.Round(p.scale*1000)/1000);
        if(additionalBakeScales!=null)foreach(float s in additionalBakeScales)sizes.Add(Mathf.Round(s*1000)/1000);
        foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var spawner in root.GetComponentsInChildren<TitanRandomSpawner>(true))
            if(spawner.sceneManager==this && spawner.sizes!=null)foreach(var option in spawner.sizes)
                if(option!=null && option.weight>0)sizes.Add(Mathf.Round(option.scale*1000)/1000);
        foreach(float s in sizes)if(!TitanSpawnPlacement.Finite(s)||s<=0) {error="Bake scales must be finite and at least .001";return false;}
        if(sizes.Count==0) {error="Assign additional bake scales or place a Titan before the first bake";return false;}
        result=new float[sizes.Count];sizes.CopyTo(result);return true;
    }

    public bool TryRegister(ModularTitanBody body,out string error)
    {
        if(!Ready(out error)) {SetupError=error;return false;}
        if(body==null || body.gameObject.scene!=gameObject.scene)return Failure(body,"Titan must belong to this manager's scene",out error);
        var actor=body.GetComponent<TitanRuntimeActor>();
        if(actor!=null)
        {
            if(actor.Owner!=this)return Failure(body,"Titan already has another runtime owner",out error);
            if(actor.IsInitializing) {error=null;return true;}
            bool success=actor.TryResume(out error);
            if(success) {actors[body]=actor;errors.Remove(body);} else errors[body]=error;
            return success;
        }
        if(body.IsDead || body.hips==null || body.head==null || body.animator==null || body.ragdollBones.Length==0)
            return Failure(body,"Spawn a fresh valid modular Titan rig, not a corpse or empty body",out error);
        foreach(var limb in body.limbs)if(limb.severed)return Failure(body,"Cannot adopt a previously severed unregistered rig",out error);
        if(body.GetComponent<NavMeshAgent>()!=null || body.GetComponent<TitanPatrolChaseAI>()!=null ||
            body.GetComponent<TitanCombatDirector>()!=null || body.GetComponent<TitanGrabAttack>()!=null || body.GetComponent<TitanStompAttack>()!=null)
            return Failure(body,"Titan has existing external AI/attacks; refusing to duplicate or take them over",out error);
        Vector3 point;TitanWorldNavigation.Profile profile;Physics.SyncTransforms();
        if(!TitanSpawnPlacement.TryFind(navigation,gameObject.scene,body.transform.position,body.transform.rotation,body.transform.lossyScale,
            spawnProjectionDistance,body.transform,out point,out profile,out error) || !TitanSpawnPlacement.BodyIsClear(body,point,out error))
            return Failure(body,error,out error);
        actor=body.gameObject.AddComponent<TitanRuntimeActor>();
        if(!actor.Initialize(this,point,profile,out error)) {actors[body]=actor;return Failure(body,error,out error);}
        actors[body]=actor;errors.Remove(body);return true;
    }
    public void SetCombatEnabled(bool enabled)
    {
        enableCombat=appliedCombat=enabled;foreach(var a in actors.Values)if(a!=null)a.ApplyCombatPolicy();
    }
    public bool TrySpawn(GameObject prefab,Vector3 position,Quaternion rotation,float scale,
        out ModularTitanBody spawned,out string error)
    {
        if(!TryPrepareSpawn(prefab,position,rotation,scale,out spawned,out error))return false;
        if(TryActivatePreparedSpawn(spawned,out error))return true;
        Despawn(spawned);spawned=null;return false;
    }
    // Prepared actors are owned, configured and fully clearance-checked but remain
    // inactive through the visual warning. Always activate or Despawn the result.
    public bool TryPrepareSpawn(GameObject prefab,Vector3 position,Quaternion rotation,float scale,
        out ModularTitanBody spawned,out string error)
    {
        spawned=null;
        if(!Ready(out error))return false;
        var source=prefab!=null?prefab.GetComponent<ModularTitanBody>():null;
        if(source==null || source.hips==null || source.head==null || source.animator==null || source.IsDead)
        {error="Assign a fresh modular Titan prefab with the body on its root";return false;}
        Vector3 point;TitanWorldNavigation.Profile profile;Physics.SyncTransforms();
        if(!TitanSpawnPlacement.TryFind(navigation,gameObject.scene,position,rotation,Vector3.one*scale,spawnProjectionDistance,null,out point,out profile,out error))return false;
        if(staging==null)
        {
            staging=new GameObject("[Titan runtime] Inactive spawn staging");staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging,gameObject.scene);
        }
        // Clone inactive in the correct scene. No default-size agent, attack,
        // collider, or lifecycle notification can run before configuration.
        var instance=Instantiate(prefab,point,rotation,staging.transform);instance.SetActive(false);
        instance.transform.localScale=Vector3.one*scale;instance.transform.SetParent(null,true);
        var body=instance.GetComponent<ModularTitanBody>();
        if(!TryRegister(body,out error))
        {actors.Remove(body);errors.Remove(body);Destroy(instance);return false;}
        // Inactive reservations have no physics actors. Conservatively reserve
        // the full posed rig bounds so another warning cannot overlap its arms.
        Bounds bounds=TitanSpawnPlacement.RigBounds(body);
        foreach(var other in prepared)if(other!=null && bounds.Intersects(TitanSpawnPlacement.RigBounds(other)))
        {actors.Remove(body);errors.Remove(body);Destroy(instance);error="Spawn overlaps another reserved Titan";return false;}
        prepared.Add(body);pending.Remove(body);spawned=body;return true;
    }
    public bool TryActivatePreparedSpawn(ModularTitanBody body,out string error)
    {
        if(!Ready(out error))return false;
        if(body==null || !prepared.Contains(body)) {error="Actor is not an owned prepared spawn";return false;}
        var actor=body.GetComponent<TitanRuntimeActor>();
        if(actor==null || actor.Owner!=this || body.gameObject.activeInHierarchy) {error="Prepared spawn ownership/activation changed";return false;}
        Vector3 point;TitanWorldNavigation.Profile profile;Physics.SyncTransforms();
        if(!TitanSpawnPlacement.TryFind(navigation,gameObject.scene,body.transform.position,body.transform.rotation,body.transform.lossyScale,
            spawnProjectionDistance,body.transform,out point,out profile,out error) || !TitanSpawnPlacement.BodyIsClear(body,point,out error))return false;
        body.transform.position=point;body.gameObject.SetActive(true);
        if(!actor.IsReady) {body.gameObject.SetActive(false);error=actor.Error;return false;}
        prepared.Remove(body);pending.Remove(body);errors.Remove(body);return true;
    }
    public bool Despawn(ModularTitanBody body)
    {
        TitanRuntimeActor actor;
        if(body==null || !actors.TryGetValue(body,out actor) || actor==null || actor.Owner!=this)return false;
        actor.Suspend();pending.Remove(body);prepared.Remove(body);actors.Remove(body);errors.Remove(body);
        body.gameObject.SetActive(false);Destroy(body.gameObject);return true;
    }
    void OnGUI()
    {
        if(!showDiagnostics)return;
        GUILayout.BeginArea(new Rect(12,12,560,220),GUI.skin.box);
        GUILayout.Label("FREEROAM TITAN RUNTIME / combat "+(enableCombat?"ON":"OFF")+" | active "+ActiveCount+" / registered "+RegisteredCount);
        if(SetupError!=null)GUILayout.Label(SetupError);
        GUILayout.Label("Existing baked profiles shared by all actors; no per-spawn bake. Failures: "+FailureCount);
        int rows=0;foreach(var p in errors)if(p.Key!=null && rows++<3)GUILayout.Label(p.Key.name+": "+p.Value);
        foreach(var a in actors.Values)if(a!=null && a.IsReady && rows++<6)GUILayout.Label(a.name+": "+a.Brain.State+" / "+a.Brain.NavigationStatus);
        GUILayout.EndArea();
    }
    void OnDrawGizmos()
    {
        if(!Application.isPlaying)return;Gizmos.color=Color.cyan;
        foreach(var a in actors.Values)if(a!=null && a.IsReady && a.Agent.enabled && a.Agent.isOnNavMesh && a.Agent.hasPath)
        {var corners=a.Agent.path.corners;for(int i=1;i<corners.Length;i++)Gizmos.DrawLine(corners[i-1]+Vector3.up,corners[i]+Vector3.up);}
    }
}
