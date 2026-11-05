using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Area-weighted proposals from loaded walkable triangles, cached once.
/// Combined triangulation is only a proposal source; the caller MUST still query
/// its scene's matching agent profile and actual physics before reserving a spawn.</summary>
public sealed class TitanSpawnSurfaceSampler
{
    struct Triangle { public Vector3 a,b,c;public double end; }
    readonly List<Triangle> triangles=new List<Triangle>();
    double area;
    public int TriangleCount {get{return triangles.Count;}}
    public bool Build(TitanWorldNavigation nav,out string error)
    {
        triangles.Clear();area=0;error=null;
        if(nav==null || !nav.IsReady) {error="Navigation must be registered before caching spawn surfaces";return false;}
        Bounds bounds=nav.bakedBounds;
        if(bounds.size.sqrMagnitude<.001f)
        {
            bool first=true;
            foreach(var p in nav.profiles)if(p!=null&&p.data!=null)
            {
                var b=p.data.sourceBounds;b.center+=p.data.position;
                if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
            }
        }
        var mesh=NavMesh.CalculateTriangulation();
        for(int i=0;i+2<mesh.indices.Length;i+=3)
        {
            Vector3 a=mesh.vertices[mesh.indices[i]],b=mesh.vertices[mesh.indices[i+1]],c=mesh.vertices[mesh.indices[i+2]];
            float size=Vector3.Cross(b-a,c-a).magnitude*.5f;
            if(!TitanSpawnPlacement.Finite(size)||size<.001f||!bounds.Contains((a+b+c)/3))continue;
            area+=size;triangles.Add(new Triangle{a=a,b=b,c=c,end=area});
        }
        if(triangles.Count==0){error="No loaded walkable triangles in the scene navigation bounds";return false;}
        return true;
    }
    public bool TrySample(System.Random random,out Vector3 point)
    {
        point=Vector3.zero;if(random==null||triangles.Count==0)return false;
        double value=random.NextDouble()*area;int lo=0,hi=triangles.Count-1;
        while(lo<hi){int mid=(lo+hi)/2;if(value<triangles[mid].end)hi=mid;else lo=mid+1;}
        var t=triangles[lo];float u=Mathf.Sqrt((float)random.NextDouble()),v=(float)random.NextDouble();
        point=t.a*(1-u)+t.b*(u*(1-v))+t.c*(u*v);return true;
    }
}
