using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TitanWorldNavigationPlayModeTests
{
    Scene scene;GameObject root;readonly List<NavMeshData> data=new List<NavMeshData>();
    readonly List<NavMeshDataInstance> instances=new List<NavMeshDataInstance>();readonly List<int> ids=new List<int>();
    readonly List<Object> owned=new List<Object>();
    [UnitySetUp]public IEnumerator Setup()
    {
        scene=SceneManager.CreateScene("Owned full-map navigation test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        root=new GameObject("Explicit environment");SceneManager.MoveGameObjectToScene(root,scene);yield return null;
    }
    [UnityTearDown]public IEnumerator Cleanup()
    {
        if(scene.IsValid() && scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        foreach(var i in instances)if(i.valid)i.Remove();instances.Clear();
        foreach(var d in data)if(d!=null)Object.Destroy(d);data.Clear();
        foreach(int id in ids)NavMesh.RemoveSettings(id);ids.Clear();
        foreach(var o in owned)if(o!=null)Object.Destroy(o);owned.Clear();yield return null;
    }
    GameObject Box(string name,Vector3 position,Vector3 size)
    {
        var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.position=position;go.AddComponent<BoxCollider>().size=size;return go;
    }
    Mesh Ramp(float length=600,float width=120)
    {
        var m=new Mesh {name="Actual readable test terrain mesh"};
        float x=length/2,z=width/2;
        m.vertices=new[]{new Vector3(-x,-x*.1f,-z),new Vector3(-x,-x*.1f,z),new Vector3(x,x*.1f,z),new Vector3(x,x*.1f,-z)};
        m.triangles=new[]{0,1,2,0,2,3};m.RecalculateNormals();m.RecalculateBounds();owned.Add(m);return m;
    }
    List<NavMeshBuildSource> Sources(out Bounds bounds)
    {
        var result=new List<NavMeshBuildSource>();string error;
        Assert.That(TitanNavigationGeometry.Collect(scene,new[]{root.transform},result,out bounds,out error),Is.True,error);return result;
    }
    NavMeshBuildSettings Settings(float radius=.5f,float height=2)
    {
        var s=NavMesh.CreateSettings();ids.Add(s.agentTypeID);s.agentRadius=radius;s.agentHeight=height;s.agentClimb=.6f;s.agentSlope=40;
        s.overrideVoxelSize=true;s.voxelSize=radius/4;return s;
    }
    NavMeshData Bake(NavMeshBuildSettings settings,List<NavMeshBuildSource> sources,Bounds bounds)
    {
        bounds.Expand(new Vector3(40,220,40));var d=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
        Assert.That(d,Is.Not.Null);data.Add(d);return d;
    }
    bool Route(int id,Vector3 from,Vector3 to,out NavMeshPath path)
    {
        var f=new NavMeshQueryFilter {agentTypeID=id,areaMask=NavMesh.AllAreas};NavMeshHit a,b;path=new NavMeshPath();
        if(!NavMesh.SamplePosition(from,out a,3,f)||!NavMesh.SamplePosition(to,out b,3,f))return false;
        return NavMesh.CalculatePath(a.position,b.position,f,path)&&path.status==NavMeshPathStatus.PathComplete;
    }
    [UnityTest]public IEnumerator CollectsWholeMapPhysicalMeshAndExcludesTriggersActorsAndMovingBodies()
    {
        Box("Far real floor",new Vector3(800,-1,0),new Vector3(80,2,80));
        var mesh=new GameObject("Mesh ground");mesh.transform.SetParent(root.transform,false);mesh.AddComponent<MeshCollider>().sharedMesh=Ramp();
        Box("No fake trigger floor",Vector3.one*2000,Vector3.one*100).GetComponent<Collider>().isTrigger=true;
        var moving=Box("Moving debris",Vector3.one*3000,Vector3.one*50);moving.AddComponent<Rigidbody>();
        var player=new GameObject("Controller exclusion");player.transform.SetParent(root.transform,false);player.transform.position=Vector3.one*4000;
        player.AddComponent<CharacterController>();Box("nested gear",Vector3.zero,Vector3.one).transform.SetParent(player.transform,false);
        Physics.SyncTransforms();Bounds bounds;var sources=Sources(out bounds);
        Assert.That(sources.Count,Is.EqualTo(2));Assert.That(sources.Exists(s=>s.shape==NavMeshBuildSourceShape.Mesh),Is.True);
        Assert.That(bounds.max.x,Is.GreaterThan(800));Assert.That(bounds.max.x,Is.LessThan(900));yield return null;
    }
    [UnityTest]public IEnumerator CrossSceneGeometryFailsClosed()
    {
        Box("Floor",Vector3.zero,new Vector3(40,1,40));var foreign=new GameObject("Other scene source");
        var list=new List<NavMeshBuildSource>();Bounds b;string error;
        Assert.That(TitanNavigationGeometry.Collect(scene,new[]{root.transform,foreign.transform},list,out b,out error),Is.False);
        Assert.That(list,Is.Empty);Object.Destroy(foreign);yield return null;
    }
    [UnityTest]public IEnumerator ActualTerrainColliderIsCollectedWithoutARendererSubstitute()
    {
        var terrain=new TerrainData {heightmapResolution=33,size=new Vector3(80,12,80)};owned.Add(terrain);
        var go=Terrain.CreateTerrainGameObject(terrain);SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(root.transform,false);Physics.SyncTransforms();
        Bounds b;var sources=Sources(out b);Assert.That(sources.Exists(s=>s.shape==NavMeshBuildSourceShape.Terrain&&s.sourceObject==terrain),Is.True);yield return null;
    }
    [UnityTest]public IEnumerator BakedMeshRampHasACompleteFiveHundredMetreRouteAroundPhysicalCover()
    {
        var mesh=new GameObject("Actual sloped terrain");mesh.transform.SetParent(root.transform,false);mesh.AddComponent<MeshCollider>().sharedMesh=Ramp();
        Box("Physical building",new Vector3(0,15,0),new Vector3(30,70,50));Physics.SyncTransforms();
        Bounds b;var sources=Sources(out b);var settings=Settings();instances.Add(NavMesh.AddNavMeshData(Bake(settings,sources,b)));
        NavMeshPath path;Assert.That(Route(settings.agentTypeID,new Vector3(-250,-25,0),new Vector3(250,25,0),out path),Is.True);
        Assert.That(path.corners.Length,Is.GreaterThan(2));float length=0;
        for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
        Assert.That(length,Is.GreaterThan(500));TestContext.WriteLine("TITAN_WORLD_RAMP_ROUTE length="+length+" corners="+path.corners.Length);yield return null;
    }
    [UnityTest]public IEnumerator ThreeAndFiveTimesClearanceCannotUseEachOthersNarrowRoute()
    {
        Box("Floor",new Vector3(0,-.5f,0),new Vector3(200,1,120));
        Box("Left wall",new Vector3(-54,50,0),new Vector3(92,100,6));Box("Right wall",new Vector3(54,50,0),new Vector3(92,100,6));Physics.SyncTransforms();
        Bounds b;var sources=Sources(out b);var small=Settings(5.4f,36);var large=Settings(9,60);
        instances.Add(NavMesh.AddNavMeshData(Bake(small,sources,b)));instances.Add(NavMesh.AddNavMeshData(Bake(large,sources,b)));
        NavMeshPath path;Assert.That(Route(small.agentTypeID,new Vector3(0,0,-35),new Vector3(0,0,35),out path),Is.True,"3x body fits 16m opening");
        Assert.That(Route(large.agentTypeID,new Vector3(0,0,-35),new Vector3(0,0,35),out path),Is.False,"5x body cannot borrow 3x nav data");yield return null;
    }
    [UnityTest]public IEnumerator ScaledAgentAvoidanceAppliesWorldClearanceExactlyOnce()
    {
        foreach(float scale in new[]{3f,5f})
        {
            var go=new GameObject("Scaled agent shape");SceneManager.MoveGameObjectToScene(go,scene);go.SetActive(false);go.transform.localScale=Vector3.one*scale;
            var agent=go.AddComponent<NavMeshAgent>();var profile=new TitanWorldNavigation.Profile {scale=scale,radius=1.8f*scale,height=12*scale,agentTypeID=0};
            profile.ConfigureAgent(agent,go.transform.lossyScale);
            Assert.That(agent.radius,Is.EqualTo(1.8f).Within(.0001f));Assert.That(agent.height,Is.EqualTo(12).Within(.0001f));
            Assert.That(agent.radius*scale,Is.EqualTo(profile.radius).Within(.0001f));Assert.That(agent.height*scale,Is.EqualTo(profile.height).Within(.0001f));
        }
        yield return null;
    }
    [UnityTest]public IEnumerator RegistrationLifecycleRemovesOnlyOwnedDataAndCanRegisterAgain()
    {
        Box("Floor",new Vector3(0,-.5f,0),new Vector3(60,1,60));Physics.SyncTransforms();Bounds b;var sources=Sources(out b);
        var external=Settings();instances.Add(NavMesh.AddNavMeshData(Bake(external,sources,b)));
        var own=Settings();var d=Bake(own,sources,b);var nav=root.AddComponent<TitanWorldNavigation>();
        nav.profiles=new[]{new TitanWorldNavigation.Profile {label="test",agentTypeID=own.agentTypeID,radius=own.agentRadius,height=own.agentHeight,data=d}};
        Assert.That(nav.Register(),Is.True,nav.Error);NavMeshPath path;
        Assert.That(Route(own.agentTypeID,new Vector3(-10,0,0),new Vector3(10,0,0),out path),Is.True);
        nav.enabled=false;Assert.That(nav.IsReady,Is.False);
        Assert.That(Route(own.agentTypeID,new Vector3(-10,0,0),new Vector3(10,0,0),out path),Is.False);
        Assert.That(Route(external.agentTypeID,new Vector3(-10,0,0),new Vector3(10,0,0),out path),Is.True);
        nav.enabled=true;Assert.That(nav.IsReady,Is.True);Assert.That(nav.Register(),Is.True);yield return null;
    }
}
