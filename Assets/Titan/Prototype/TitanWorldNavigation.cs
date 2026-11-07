using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Registers only this scene's Editor-baked data; no whole-map runtime bake,
/// global setting mutation, RemoveAllNavMeshData or CPU-readable mesh requirement.</summary>
[DefaultExecutionOrder(-600)]
public class TitanWorldNavigation : MonoBehaviour
{
    [Serializable] public class Profile
    {
        public string label;
        public float scale=1, radius=1.8f, height=12;
        public int agentTypeID;
        public NavMeshData data;
        public void ConfigureAgent(NavMeshAgent agent, Vector3 worldScale)
        {
            if(worldScale.x<=0 || worldScale.y<=0 || worldScale.z<=0)throw new ArgumentOutOfRangeException("worldScale");
            agent.agentTypeID=agentTypeID;
            // Data clearance is world-sized; native component avoidance follows
            // transform scale. Assign local dimensions, not scale-squared ones.
            agent.radius=radius/worldScale.x;agent.height=height/worldScale.y;
        }
    }
    public Transform[] geometryRoots=new Transform[0];
    public Profile[] profiles=new Profile[0];
    public Bounds bakedBounds;
    public int sourceCount;
    public string bakedAt;
    public bool IsReady { get; private set; }
    public string Error { get; private set; }
    readonly List<NavMeshDataInstance> instances=new List<NavMeshDataInstance>();
    void OnEnable() { if(Application.isPlaying) Register(); }
    void OnDisable() { Release(); }
    void OnDestroy() { Release(); }
    public bool Register()
    {
        Release();
        if(profiles==null || profiles.Length==0) { Error="Bake full-map navigation first";return false; }
        foreach(var profile in profiles)
        {
            var settings=profile==null?new NavMeshBuildSettings():NavMesh.GetSettingsByID(profile.agentTypeID);
            if(profile==null || profile.data==null ||
                settings.agentTypeID==-1 || Mathf.Abs(settings.agentRadius-profile.radius)>.01f ||
                Mathf.Abs(settings.agentHeight-profile.height)>.01f)
            { Release();Error="Missing/mismatched Titan navigation profile; rebake in Editor";return false; }
            var instance=NavMesh.AddNavMeshData(profile.data); instance.owner=this;
            if(!instance.valid) { Release();Error="Could not register Titan navigation data";return false; }
            instances.Add(instance);
        }
        Error=null;IsReady=true;return true;
    }
    public Profile ForScale(float scale)
    {
        foreach(var p in profiles) if(p!=null && Mathf.Abs(p.scale-scale)<.001f) return p;
        return null;
    }
    public void Release()
    {
        foreach(var instance in instances) if(instance.valid) instance.Remove();
        instances.Clear();IsReady=false;
    }
    void OnDrawGizmosSelected() { Gizmos.color=new Color(.1f,.8f,1,.6f);Gizmos.DrawWireCube(bakedBounds.center,bakedBounds.size); }
}
