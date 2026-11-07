using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Small explicit-source runtime bake for the isolated AI lab (built-in Unity 2021 AI API).
/// Does not change global agent settings or remove another scene's navigation data.
/// Production levels should supply their own authored/baked NavMesh instead.
/// </summary>
public class TitanNavigationSurface : MonoBehaviour
{
    public BoxCollider[] geometry = new BoxCollider[0];
    public int agentTypeID;
    public float agentRadius = 1.8f;
    public float agentHeight = 12f;
    [Min(0)] public float localBakeSize; // 0 retains the lab's full explicit-source bounds.
    public bool IsReady { get { return instance.valid; } }
    public string Error { get; private set; }
    NavMeshData data;
    NavMeshDataInstance instance;

    void Start() { if (!IsReady) Build(); }

    public bool Build()
    {
        Release();
        var sources = new List<NavMeshBuildSource>();
        var bounds = new Bounds();
        bool first = true;
        foreach (var collider in geometry)
        {
            if (collider == null || !collider.enabled || collider.isTrigger) continue;
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = collider.transform.localToWorldMatrix * Matrix4x4.Translate(collider.center),
                size = collider.size,
                area = 0,
                sourceObject = collider
            });
            if (first) { bounds = collider.bounds; first = false; }
            else bounds.Encapsulate(collider.bounds);
        }
        if (sources.Count == 0) { Error = "No explicit navigation geometry assigned"; return false; }
        bounds.Expand(new Vector3(agentRadius * 4, agentHeight * 2, agentRadius * 4));
        if(localBakeSize>0) bounds=new Bounds(transform.position,new Vector3(localBakeSize,128,localBakeSize));
        var settings = NavMesh.GetSettingsByID(agentTypeID);
        if (settings.agentTypeID < 0) { Error = "Unknown NavMesh agent type"; return false; }
        settings.agentRadius = agentRadius;
        settings.agentHeight = agentHeight;
        settings.agentClimb = .6f;
        settings.agentSlope = 40f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = agentRadius / 4f;
        data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null) { Error = "Runtime NavMesh bake failed"; return false; }
        data.name = "Titan AI Lab - owned runtime navigation";
        instance = NavMesh.AddNavMeshData(data);
        instance.owner = this;
        Error = instance.valid ? null : "NavMesh registration failed";
        return instance.valid;
    }

    public void Release()
    {
        if (instance.valid) instance.Remove();
        if (data != null)
        {
            if (Application.isPlaying) Destroy(data); else DestroyImmediate(data);
            data = null;
        }
    }
    void OnDestroy() { Release(); }
}
