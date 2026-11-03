using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Explicit scene-local physical geometry, not visual bounds/flat substitutes.
/// Uses Unity's primitive/mesh/terrain conversion. Actor/trigger/moving sources excluded.</summary>
public static class TitanNavigationGeometry
{
    public static bool Collect(Scene scene, Transform[] roots, List<NavMeshBuildSource> output,
        out Bounds bounds, out string error)
    {
        output.Clear(); bounds=new Bounds(); error=null;
        var seen=new HashSet<Component>(); bool first=true;
        if(!scene.IsValid() || roots==null) { error="Explicit scene geometry roots required"; return false; }
        foreach(var root in roots)
        {
            if(root==null) continue;
            if(root.gameObject.scene!=scene) { error="Navigation source belongs to a different scene";output.Clear();return false; }
            var collected=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(root,~0,NavMeshCollectGeometry.PhysicsColliders,0,
                new List<NavMeshBuildMarkup>(),collected);
            foreach(var source in collected)
            {
                var c=source.component as Collider;
                var terrain=source.component as Terrain;
                if(c==null && terrain!=null)c=terrain.GetComponent<TerrainCollider>();
                if(!Eligible(c) || !seen.Add(c)) continue;
                if(source.shape==NavMeshBuildSourceShape.Mesh && source.sourceObject==null) continue;
                output.Add(source);
                if(first) { bounds=c.bounds;first=false; } else bounds.Encapsulate(c.bounds);
            }
            // Unity2021's hierarchy collector can omit Terrain in owned local-physics
            // scenes. Include the actual heightmap collider explicitly, never a flat proxy.
            foreach(var terrain in root.GetComponentsInChildren<TerrainCollider>())
            {
                if(!Eligible(terrain) || terrain.terrainData==null || !seen.Add(terrain))continue;
                output.Add(new NavMeshBuildSource {shape=NavMeshBuildSourceShape.Terrain,sourceObject=terrain.terrainData,
                    transform=terrain.transform.localToWorldMatrix,size=terrain.terrainData.size,component=terrain});
                if(first){bounds=terrain.bounds;first=false;}else bounds.Encapsulate(terrain.bounds);
            }
        }
        if(first) { error="No enabled static physical navigation geometry under the assigned roots";return false; }
        return true;
    }
    static bool Eligible(Collider c)
    {
        return c!=null && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
            c.attachedRigidbody==null && c.GetComponentInParent<ModularTitanBody>()==null &&
            c.GetComponentInParent<CharacterController>()==null;
    }
}
