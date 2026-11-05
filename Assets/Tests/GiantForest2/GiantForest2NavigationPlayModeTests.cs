#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

/// <summary>Navigation-only tests of the CURRENT saved user map, not a map rebuild.</summary>
public class GiantForest2NavigationPlayModeTests
{
    Scene scene;TitanWorldNavigation nav;Component manager,spawner;Terrain terrain;
    static Type Real(string n){return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).First(t=>t!=null);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n).Invoke(o,args);}
    static T Field<T>(object o,string n){return (T)o.GetType().GetField(n).GetValue(o);}
    static T Prop<T>(object o,string n){return (T)o.GetType().GetProperty(n).GetValue(o);}
    T[] All<T>()where T:Component{return scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();}
    Vector3 Point(float x,float z){return new Vector3(x,terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y,z);}
    [UnitySetUp]public IEnumerator Load()
    {
        if(!Application.isBatchMode || !File.Exists(".giant-forest-owned"))Assert.Ignore("Only explicitly owned navigation worker may load this scene");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/GiantForest2.unity",LoadSceneMode.Single);scene=SceneManager.GetActiveScene();
        nav=All<TitanWorldNavigation>().Single();manager=All<Component>().First(c=>c!=null&&c.GetType()==Real("TitanFreeRoamIntegration"));
        spawner=All<Component>().First(c=>c!=null&&c.GetType()==Real("TitanRandomSpawner"));spawner.GetType().GetField("automaticSpawning").SetValue(spawner,false);
        terrain=All<Terrain>().Single();for(int i=0;i<5;i++)yield return null;
    }
    [UnityTearDown]public IEnumerator Cleanup()
    {if(scene.IsValid()&&scene.isLoaded){var empty=SceneManager.CreateScene("Owned navigation cleanup");SceneManager.SetActiveScene(empty);yield return SceneManager.UnloadSceneAsync(scene);}}
    [UnityTest]public IEnumerator AllFiveExactClearanceProfilesHaveCompleteForestRoutesAndWallsStaySolid()
    {
        Assert.That(nav.IsReady,Is.True,nav.Error);Assert.That(nav.profiles.Select(p=>p.scale),Is.EquivalentTo(new[]{3f,5f,6f,7f,10f}));Assert.That(nav.sourceCount,Is.EqualTo(2549));
        foreach(var p in nav.profiles)
        {
            Assert.That(p.radius,Is.EqualTo(1.8f*p.scale).Within(.001f));Assert.That(p.height,Is.EqualTo(12*p.scale));
            StringAssert.StartsWith("Assets/Navigation/Titans/GiantForest2/",AssetDatabase.GetAssetPath(p.data));
            var filter=new NavMeshQueryFilter{agentTypeID=p.agentTypeID,areaMask=NavMesh.AllAreas};NavMeshHit start;
            Assert.That(NavMesh.SamplePosition(Point(0,-550),out start,6,filter),Is.True,p.label);
            foreach(var target in new[]{new Vector2(0,550),new Vector2(-510,440),new Vector2(510,440),new Vector2(-510,-440),new Vector2(510,-440)})
            {
                NavMeshHit end;Assert.That(NavMesh.SamplePosition(Point(target.x,target.y),out end,45,filter),Is.True,p.label+target);
                var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(start.position,end.position,filter,path),Is.True);Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),p.label+target);
            }
            NavMeshHit inside,outside,wall;Assert.That(NavMesh.SamplePosition(Point(700,0),out inside,35,filter),Is.True);Assert.That(NavMesh.SamplePosition(Point(825,0),out outside,35,filter),Is.True);
            Assert.That(NavMesh.Raycast(inside.position,outside.position,out wall,filter),Is.True,"No shortcut through actual user wall: "+p.label);
        }
        yield return null;
    }
    [UnityTest]public IEnumerator SevenAndTenSpawnWithRealFullRigClearanceAndMatchingNativeAgentsWithoutRuntimeBake()
    {
        int triangles=NavMesh.CalculateTriangulation().indices.Length;var prefab=Field<GameObject>(spawner,"titanPrefab");
        foreach(float size in new[]{7f,10f})
        {
            ModularTitanBody body=null;string error="";
            for(int z=-600;z<=600&&body==null;z+=100)for(int x=-600;x<=600&&body==null;x+=100)
            {
                object[] args={prefab,Point(x,z),Quaternion.identity,size,null,null};if((bool)Call(manager,"TrySpawn",args))body=(ModularTitanBody)args[4];error=(string)args[5];
            }
            Assert.That(body,Is.Not.Null,"No full-rig-clear "+size+"x birth: "+error);
            var agent=body.GetComponent<NavMeshAgent>();Assert.That(agent.isOnNavMesh,Is.True);Assert.That(agent.agentTypeID,Is.EqualTo(nav.ForScale(size).agentTypeID));
            Assert.That(agent.radius,Is.EqualTo(1.8f).Within(.001f));Assert.That(agent.height,Is.EqualTo(12).Within(.001f));Assert.That(body.transform.lossyScale.x,Is.EqualTo(size));
            Assert.That(Prop<bool>(body.GetComponent(Real("TitanRuntimeActor")),"IsReady"),Is.True);
            Assert.That((bool)Call(manager,"Despawn",body),Is.True);yield return null;
        }
        Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));
    }
    [UnityTest]public IEnumerator CurrentUserSizeChoicesAreAllSupportedAndTheActualRandomSpawnerArrives()
    {
        object[] config={null};Assert.That((bool)Call(spawner,"ValidateConfiguration",config),Is.True,(string)config[0]);Assert.That(Prop<string>(spawner,"SizeWarning"),Is.Null);
        spawner.GetType().GetField("randomSeed").SetValue(spawner,241);Call(spawner,"RestartSchedule");int triangles=NavMesh.CalculateTriangulation().indices.Length;
        bool queued=false;string error="";
        for(int i=0;i<30&&!queued;i++){object[] args={null};queued=(bool)Call(spawner,"TryQueueSpawn",args);error=(string)args[0];}
        Assert.That(queued,Is.True,error);var body=Prop<ModularTitanBody>(spawner,"PendingBody");Assert.That(body.gameObject.activeInHierarchy,Is.False);
        yield return new WaitForSeconds(Field<float>(spawner,"warningDuration")+.15f);
        Assert.That(Prop<int>(spawner,"SpawnedCount"),Is.EqualTo(1),Prop<string>(spawner,"LastError"));Assert.That(body.GetComponent<NavMeshAgent>().isOnNavMesh,Is.True);
        Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));
    }
}
#endif
