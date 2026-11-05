#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

/// <summary>ONLY the owned full-project worker. Never load a scene in a user's Play.</summary>
public class GiantForest2PlayModeTests
{
    Scene scene;TitanWorldNavigation nav;MonoBehaviour manager,player,spawner;GameObject environment;
    static Type Real(string name){return AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Assembly-CSharp").GetType(name,true);}
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name).GetValue(o);}
    static T Prop<T>(object o,string name){return (T)o.GetType().GetProperty(name).GetValue(o);}
    static void Set(object o,string name,object value){o.GetType().GetField(name).SetValue(o,value);}
    T[] All<T>()where T:Component{return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();}
    MonoBehaviour FindReal(string name){return All<MonoBehaviour>().First(b=>b!=null&&b.GetType()==Real(name));}
    [UnitySetUp]public IEnumerator Load()
    {
        if(!Application.isBatchMode||!File.Exists(".giant-forest-owned"))Assert.Ignore("Only an explicitly owned GiantForest2 batch worker may load this scene");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/GiantForest2.unity",LoadSceneMode.Single);scene=SceneManager.GetActiveScene();
        nav=All<TitanWorldNavigation>().Single();manager=FindReal("TitanFreeRoamIntegration");player=FindReal("Movement");spawner=FindReal("TitanRandomSpawner");Set(spawner,"automaticSpawning",false);
        environment=scene.GetRootGameObjects().Single(r=>r.name=="Giant Forest 2.0 [generated environment]");
        for(int i=0;i<5;i++)yield return null;
    }
    [UnityTearDown]public IEnumerator Unload()
    {
        if(!scene.IsValid()||!scene.isLoaded)yield break;
        var empty=SceneManager.CreateScene("Owned empty forest-test cleanup");SceneManager.SetActiveScene(empty);yield return SceneManager.UnloadSceneAsync(scene);yield return null;
    }
    [UnityTest]public IEnumerator ApprovedTreesVaryAndAreTwoToThreeTimesTheLargestSavedTitan()
    {
        var actors=All<ModularTitanBody>();float tallest=actors.Max(b=>12*b.transform.lossyScale.x);Assert.That(tallest,Is.EqualTo(72));
        var groups=environment.GetComponentsInChildren<LODGroup>();Assert.That(groups.Length,Is.EqualTo(208));
        var h=groups.Select(g=>g.GetLODs()[0].renderers.Single().bounds.size.y).ToArray();
        Assert.That(h.Min(),Is.GreaterThanOrEqualTo(tallest*2));Assert.That(h.Max(),Is.LessThanOrEqualTo(tallest*3+.1f));Assert.That(h.Max()-h.Min(),Is.GreaterThan(55));
        foreach(var g in groups)
        {
            var lod=g.GetLODs();Assert.That(lod.Length,Is.EqualTo(3));int last=int.MaxValue;
            foreach(var level in lod){var mesh=level.renderers.Single().GetComponent<MeshFilter>().sharedMesh;int count=Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(int)mesh.GetIndexCount(s));Assert.That(count,Is.LessThan(last));last=count;}
            Assert.That(g.GetComponentsInChildren<MeshCollider>().Length,Is.EqualTo(1));
        }
        TestContext.WriteLine("Actual LOD0 tree heights: "+h.Min()+" to "+h.Max()+"m; Titan reference: "+tallest+"m");yield return null;
    }
    [UnityTest]public IEnumerator PhysicalHeightfieldWallsAndEveryTrunkCanBeHookedWithoutLeafSolidBoxes()
    {
        Assert.That(scene.GetRootGameObjects().Any(r=>r.name=="Map Control"||r.name=="Cube"),Is.False);
        var terrain=environment.GetComponentInChildren<Terrain>();Assert.That(terrain.terrainData.size.x,Is.EqualTo(2048));Assert.That(terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(terrain.terrainData));
        Assert.That(Field<LayerMask>(player,"targetLayer").value&(1<<9),Is.Not.Zero);
        Assert.That(Field<LayerMask>(FindReal("ODM_Movement"),"targetLayer").value&(1<<9),Is.Not.Zero);
        foreach(var group in environment.GetComponentsInChildren<LODGroup>())
        {
            var wood=group.GetComponentInChildren<MeshCollider>();Assert.That(wood.convex,Is.False);Assert.That(wood.gameObject.layer,Is.EqualTo(9));Assert.That(wood.sharedMesh.isReadable,Is.True);
            RaycastHit hit;var ray=new Ray(group.transform.position+Vector3.up*10+Vector3.right*100,Vector3.left);
            Assert.That(wood.Raycast(ray,out hit,200),Is.True,"Actual bole ray missed "+group.name);Assert.That(hit.collider,Is.SameAs(wood));
            Assert.That(group.GetComponentsInChildren<BoxCollider>().Length,Is.Zero,"No giant canopy-bounds surrogate collider");
        }
        var wall=environment.transform.Find("Perimeter walls - 1536m enclosure");Assert.That(wall,Is.Not.Null);Assert.That(wall.GetComponentsInChildren<BoxCollider>().Length,Is.GreaterThan(2000));
        yield return null;
    }
    static Bounds VisualBounds(Transform root)
    {var r=root.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var item in r.Skip(1))b.Encapsulate(item.bounds);return b;}
    [UnityTest]public IEnumerator WallsAreDoubleHeightAndRocksIncludeSmallMediumAndLargeHookableSizes()
    {
        var walls=environment.transform.Find("Perimeter walls - 1536m enclosure");Assert.That(walls.childCount,Is.EqualTo(259));
        foreach(Transform wall in walls)
        {
            var b=VisualBounds(wall);bool pier=wall.name=="Corner anchor pier";
            Assert.That(b.size.y,Is.EqualTo(pier?120:121.1f).Within(.03f),wall.name);Assert.That(b.min.y,Is.EqualTo(12).Within(.03f),"No floating foundations");
            Assert.That(wall.localScale.x,Is.EqualTo(pier?11:1));Assert.That(wall.localScale.z,Is.EqualTo(pier?11:1));
            foreach(var c in wall.GetComponentsInChildren<Collider>())Assert.That(c.gameObject.layer,Is.EqualTo(9));
        }
        var rocks=environment.transform.Find("Fallen wood and rock clusters").Cast<Transform>().Where(t=>t.name.StartsWith("GF2_Boulder_")).ToArray();Assert.That(rocks.Length,Is.EqualTo(40));
        var heights=rocks.Select(r=>VisualBounds(r).size.y).ToArray();Assert.That(heights.Min(),Is.GreaterThanOrEqualTo(3.99f));Assert.That(heights.Max(),Is.LessThanOrEqualTo(32.01f));
        Assert.That(heights.Count(h=>h<8.01f),Is.EqualTo(8));Assert.That(heights.Count(h=>h>=9.99f&&h<17.01f),Is.EqualTo(16));Assert.That(heights.Count(h=>h>=21.99f),Is.EqualTo(16));Assert.That(heights.Max()/heights.Min(),Is.GreaterThan(5));
        foreach(var rock in rocks)
        {
            var b=VisualBounds(rock);var collider=rock.GetComponentInChildren<MeshCollider>();RaycastHit hit;Assert.That(collider.gameObject.layer,Is.EqualTo(9));Assert.That(collider.bounds.size.y,Is.EqualTo(b.size.y).Within(.05f));
            Assert.That(collider.Raycast(new Ray(b.center+Vector3.up*(b.extents.y+3),Vector3.down),out hit,b.size.y+6),Is.True,"Actual scaled rock must be solid and hookable");
            Assert.That(rock.lossyScale.x,Is.EqualTo(rock.lossyScale.y).Within(.0001f));Assert.That(rock.lossyScale.z,Is.EqualTo(rock.lossyScale.y).Within(.0001f));
        }
        TestContext.WriteLine("ROCK_HEIGHTS "+heights.Min()+".."+heights.Max()+"m; tiers8/16/16. WALL_HEIGHT121.1m, base12m");yield return null;
    }
    [UnityTest]public IEnumerator MapSpecificThreeFiveSixNavigationCrossesTheForestButNotSolidWalls()
    {
        Assert.That(nav.IsReady,Is.True,nav.Error);Assert.That(nav.profiles.Select(p=>p.scale),Is.EquivalentTo(new[]{3f,5f,6f}));Assert.That(nav.geometryRoots,Is.EqualTo(new[]{environment.transform}));Assert.That(nav.sourceCount,Is.EqualTo(2549));
        var terrain=environment.GetComponentInChildren<Terrain>();
        Func<float,float,Vector3> point=(x,z)=>new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y,z);
        foreach(var p in nav.profiles)
        {
            StringAssert.StartsWith("Assets/Navigation/Titans/GiantForest2/",AssetDatabase.GetAssetPath(p.data));Assert.That(p.radius,Is.EqualTo(1.8f*p.scale).Within(.001f));Assert.That(p.height,Is.EqualTo(12*p.scale));
            var filter=new NavMeshQueryFilter{agentTypeID=p.agentTypeID,areaMask=NavMesh.AllAreas};NavMeshHit start;Assert.That(NavMesh.SamplePosition(point(0,-550),out start,6,filter),Is.True);
            foreach(var target in new[]{new Vector2(0,550),new Vector2(-510,440),new Vector2(510,440),new Vector2(-510,-440),new Vector2(510,-440)})
            {
                NavMeshHit end;Assert.That(NavMesh.SamplePosition(point(target.x,target.y),out end,45,filter),Is.True);var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(start.position,end.position,filter,path),Is.True);Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
            }
            NavMeshHit inside,outside,wall;Assert.That(NavMesh.SamplePosition(point(700,0),out inside,35,filter),Is.True);Assert.That(NavMesh.SamplePosition(point(825,0),out outside,35,filter),Is.True);Assert.That(NavMesh.Raycast(inside.position,outside.position,out wall,filter),Is.True,"No path through actual wall");
        }
        yield return null;
    }
    [UnityTest]public IEnumerator SavedPlayerAndAllPlacedSizesInitializeWithUnchangedGravityAndOneAgentEach()
    {
        Assert.That(Prop<int>(manager,"RegisteredCount"),Is.EqualTo(6));Assert.That(Field<bool>(manager,"enableCombat"),Is.True);
        Assert.That(Field<float>(player,"gravityConstant"),Is.EqualTo(16));Assert.That(Field<float>(player,"jumpHeight"),Is.EqualTo(4));Assert.That(Field<float>(player,"jumpDelay"),Is.EqualTo(.1f));Assert.That(Field<float>(player,"jumpCooldown"),Is.EqualTo(.15f));
        var scales=All<ModularTitanBody>().Select(b=>b.transform.lossyScale.x).OrderBy(v=>v).ToArray();Assert.That(scales,Is.EqualTo(new[]{3f,5f,6f,6f,6f,6f}));
        foreach(var body in All<ModularTitanBody>())
        {
            var actor=body.GetComponent(Real("TitanRuntimeActor"));Assert.That(actor,Is.Not.Null);Assert.That(Prop<bool>(actor,"IsReady"),Is.True,Prop<string>(actor,"Error"));
            var agents=body.GetComponents<NavMeshAgent>();Assert.That(agents.Length,Is.EqualTo(1));Assert.That(agents[0].isOnNavMesh,Is.True);Assert.That(agents[0].height,Is.EqualTo(12).Within(.001f));Assert.That(agents[0].radius,Is.EqualTo(1.8f).Within(.001f));Assert.That(agents[0].agentTypeID,Is.EqualTo(nav.ForScale(body.transform.lossyScale.x).agentTypeID));
        }
        // ODM's subsequent zero-displacement CCT moves can overwrite isGrounded.
        // Check actual native support (the real jump gate) AND a tight floor pose,
        // not that transient flag or the generous animation ground probe.
        yield return new WaitForSeconds(.5f);Assert.That((bool)Call(player,"HasJumpSupport"),Is.True);Assert.That(player.transform.position.y,Is.InRange(11.7f,12.1f));
    }
    [UnityTest]public IEnumerator FreshActorsAtEverySupportedSizeAndRandomWarningSpawnUseExistingData()
    {
        int triangles=NavMesh.CalculateTriangulation().indices.Length;var prefab=Field<GameObject>(spawner,"titanPrefab");
        foreach(float scale in new[]{3f,5f,6f})
        {
            ModularTitanBody spawned=null;string error="";
            for(int z=-380;z<=380&&spawned==null;z+=95)for(int x=-480;x<=480&&spawned==null;x+=120)
            {
                var terrain=environment.GetComponentInChildren<Terrain>();Vector3 point=new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y,z);
                object[] args={prefab,point,Quaternion.identity,scale,null,null};bool ok=(bool)Call(manager,"TrySpawn",args);error=(string)args[5];if(ok)spawned=(ModularTitanBody)args[4];
            }
            Assert.That(spawned,Is.Not.Null,error);Assert.That(spawned.GetComponent<NavMeshAgent>().isOnNavMesh,Is.True);Assert.That(Prop<bool>(spawned.GetComponent(Real("TitanRuntimeActor")),"IsReady"),Is.True);Assert.That((bool)Call(manager,"Despawn",spawned),Is.True);yield return null;
        }
        Set(spawner,"randomSeed",241);Call(spawner,"RestartSchedule");bool queued=false;string reason="";
        for(int i=0;i<12&&!queued;i++){object[] args={null};queued=(bool)Call(spawner,"TryQueueSpawn",args);reason=(string)args[0];}
        Assert.That(queued,Is.True,reason);Assert.That(Prop<bool>(spawner,"HasPendingSpawn"),Is.True);
        yield return new WaitForSeconds(Field<float>(spawner,"warningDuration")+.15f);Assert.That(Prop<int>(spawner,"SpawnedCount"),Is.EqualTo(1),Prop<string>(spawner,"LastError"));Assert.That(Prop<int>(spawner,"OwnedAliveCount"),Is.EqualTo(1));Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles),"No runtime navigation rebake");
    }
    [UnityTest]public IEnumerator NativeGraphicsShowTheActualPlayableScene()
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Use owned graphics action");
        // Real native frames: imported materials, scene lighting, active animation,
        // independent body initialization. No input, staging actors or concept art.
        for(int i=0;i<30;i++)yield return null;
        Directory.CreateDirectory("Library/GiantForest2");var cam=Camera.main;Assert.That(cam,Is.Not.Null);
        Shot(cam,"Gameplay");var review=new GameObject("Owned unsaved forest review camera");var c=review.AddComponent<Camera>();c.nearClipPlane=.3f;c.farClipPlane=4000;c.fieldOfView=65;
        bool fog=RenderSettings.fog;
        try
        {
            c.transform.SetPositionAndRotation(new Vector3(0,23,-600),Quaternion.LookRotation(new Vector3(30,95,-250)-new Vector3(0,23,-600)));Shot(c,"RuntimeEntry");
            c.transform.SetPositionAndRotation(new Vector3(0,68,-550),Quaternion.LookRotation(new Vector3(0,72,-768)-new Vector3(0,68,-550)));Shot(c,"RuntimeGate");
            var rock=environment.transform.Find("Fallen wood and rock clusters").Cast<Transform>().Where(t=>t.name.StartsWith("GF2_Boulder_")).OrderByDescending(t=>VisualBounds(t).size.y).First();
            var bounds=VisualBounds(rock);var view=bounds.center+new Vector3(65,18,-80);c.transform.SetPositionAndRotation(view,Quaternion.LookRotation(bounds.center-view));Shot(c,"RuntimeRocks");
            RenderSettings.fog=false;c.transform.SetPositionAndRotation(new Vector3(750,880,-1120),Quaternion.LookRotation(new Vector3(0,50,0)-new Vector3(750,880,-1120)));Shot(c,"RuntimeOverview");
        }
        finally{RenderSettings.fog=fog;Object.Destroy(review);}
        for(int i=0;i<10;i++)yield return null;
        Shot(cam,"Gameplay");
        Assert.That(Prop<int>(manager,"RegisteredCount"),Is.EqualTo(6));TestContext.WriteLine("Native graphics device: "+SystemInfo.graphicsDeviceName);yield return null;
    }
    static void Shot(Camera cam,string name)
    {
        var rt=RenderTexture.GetTemporary(1600,1000,24);var target=cam.targetTexture;var active=RenderTexture.active;
        try
        {
            cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            var px=image.GetPixels32();int pink=px.Count(p=>p.r>180&&p.b>180&&p.g<60);Assert.That(pink,Is.LessThan(500),"Missing-material magenta");File.WriteAllBytes("Library/GiantForest2/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
        }
        finally{RenderTexture.active=active;cam.targetTexture=target;RenderTexture.ReleaseTemporary(rt);}
    }
}
#endif
