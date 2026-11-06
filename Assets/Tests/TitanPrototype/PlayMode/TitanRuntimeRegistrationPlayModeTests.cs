#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class TitanRuntimeRegistrationPlayModeTests
{
    Scene scene,previous,foreign;
    MonoBehaviour manager;
    TitanWorldNavigation nav;
    GameObject player;
    TitanCombatTarget human;
    readonly List<int> ids=new List<int>();
    readonly List<NavMeshData> data=new List<NavMeshData>();
    static Type Real(string n) { return AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Assembly-CSharp").GetType(n,true); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n).SetValue(o,v); }
    static T Prop<T>(object o,string n) { return (T)o.GetType().GetProperty(n).GetValue(o); }
    static object Call(object o,string n,params object[] args) { return o.GetType().GetMethod(n).Invoke(o,args); }
    GameObject Prefab {get{return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");}}
    [UnitySetUp] public IEnumerator Setup()
    {
        previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Owned runtime Titan registration",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        var floor=new GameObject("Actual spawn floor");floor.transform.position=Vector3.down*.5f;floor.AddComponent<BoxCollider>().size=new Vector3(240,1,240);
        var sources=new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(floor.transform.position,Quaternion.identity,Vector3.one),size=new Vector3(240,1,240),area=0}};
        var profiles=new List<TitanWorldNavigation.Profile>();
        foreach(float scale in new[]{1f,3f,5f})
        {
            var settings=NavMesh.CreateSettings();ids.Add(settings.agentTypeID);
            // Only NEW owned agent types in this disposable fixture; no changes to
            // Humanoid or project-owned Titan profiles and no settings-file save.
            var asset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];var so=new SerializedObject(asset);var entries=so.FindProperty("m_Settings");
            for(int i=0;i<entries.arraySize;i++){var e=entries.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("agentTypeID").intValue!=settings.agentTypeID)continue;e.FindPropertyRelative("agentRadius").floatValue=1.8f*scale;e.FindPropertyRelative("agentHeight").floatValue=12*scale;e.FindPropertyRelative("agentClimb").floatValue=.6f*scale;}
            so.ApplyModifiedPropertiesWithoutUndo();settings=NavMesh.GetSettingsByID(settings.agentTypeID);settings.overrideVoxelSize=true;settings.voxelSize=settings.agentRadius/4;
            var d=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.up*30,new Vector3(260,160,260)),Vector3.zero,Quaternion.identity);Assert.That(d,Is.Not.Null);data.Add(d);
            profiles.Add(new TitanWorldNavigation.Profile{scale=scale,agentTypeID=settings.agentTypeID,radius=settings.agentRadius,height=settings.agentHeight,data=d});
        }
        var host=new GameObject("Opted-in runtime scene manager");nav=host.AddComponent<TitanWorldNavigation>();nav.profiles=profiles.ToArray();Assert.That(nav.Register(),Is.True,nav.Error);
        player=new GameObject("Scene player target");player.transform.position=new Vector3(0,1,100);var box=player.AddComponent<BoxCollider>();box.center=Vector3.zero;box.size=new Vector3(.8f,2,.8f);
        var movement=(MonoBehaviour)player.AddComponent(Real("Movement"));movement.enabled=false;human=player.AddComponent<TitanCombatTarget>();
        manager=(MonoBehaviour)host.AddComponent(Real("TitanFreeRoamIntegration"));Set(manager,"player",movement);Set(manager,"navigation",nav);Set(manager,"enableCombat",true);Set(manager,"showDiagnostics",false);
        Physics.SyncTransforms();yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        SceneManager.SetActiveScene(previous);if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        if(foreign.IsValid()&&foreign.isLoaded)yield return SceneManager.UnloadSceneAsync(foreign);
        foreach(int id in ids)NavMesh.RemoveSettings(id);ids.Clear();foreach(var d in data)Object.Destroy(d);data.Clear();yield return null;
    }
    ModularTitanBody Clone(float scale,Vector3 point,bool active=true)
    {
        var holder=new GameObject("Inactive fixture holder");holder.SetActive(false);
        var go=Object.Instantiate(Prefab,holder.transform);go.SetActive(false);go.transform.localScale=Vector3.one*scale;go.transform.position=point;go.transform.SetParent(null,true);Object.Destroy(holder);
        if(active)go.SetActive(true);return go.GetComponent<ModularTitanBody>();
    }
    object Actor(ModularTitanBody b){return b.GetComponent(Real("TitanRuntimeActor"));}
    bool Register(ModularTitanBody b,out string error){object[] args={b,null};bool ok=(bool)Call(manager,"TryRegister",args);error=(string)args[1];return ok;}
    bool Spawn(float scale,Vector3 point,out ModularTitanBody b,out string error)
    {object[] args={Prefab,point,Quaternion.identity,scale,null,null};bool ok=(bool)Call(manager,"TrySpawn",args);b=(ModularTitanBody)args[4];error=(string)args[5];return ok;}
    void Ready(ModularTitanBody b)
    {
        var a=Actor(b);Assert.That(a,Is.Not.Null);Assert.That(Prop<bool>(a,"IsReady"),Is.True,Prop<string>(a,"Error"));
        Assert.That(b.GetComponents<NavMeshAgent>().Length,Is.EqualTo(1));Assert.That(b.GetComponents<TitanPatrolChaseAI>().Length,Is.EqualTo(1));
        Assert.That(b.GetComponents<TitanCombatDirector>().Length,Is.EqualTo(1));Assert.That(b.GetComponent<TitanCombatDirector>().isActiveAndEnabled);
        Assert.That(b.GetComponent<NavMeshAgent>().isOnNavMesh);Assert.That(b.GetComponent<TitanPatrolChaseAI>().target,Is.EqualTo(player.transform));
        Assert.That(b.useRegionalHealth);Assert.That(b.GetHealth(TitanHitRegion.Nape),Is.EqualTo(500));
    }
    [UnityTest]public IEnumerator LatePlainClonesRegisterWithoutListOrRebakeAndUseCorrectScaleOnce()
    {
        int triangles=NavMesh.CalculateTriangulation().indices.Length;
        var small=Clone(3,new Vector3(-40,0,0));yield return null;yield return null;Ready(small);
        var large=Clone(5,new Vector3(40,0,0));yield return null;yield return null;Ready(large);
        Assert.That(Prop<int>(manager,"RegisteredCount"),Is.EqualTo(2));
        foreach(var b in new[]{small,large}){var a=b.GetComponent<NavMeshAgent>();Assert.That(a.radius,Is.EqualTo(1.8f).Within(.001f));Assert.That(a.height,Is.EqualTo(12).Within(.001f));Assert.That(a.agentTypeID,Is.EqualTo(nav.ForScale(b.transform.lossyScale.x).agentTypeID));}
        Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));Assert.That(Prefab.GetComponent<NavMeshAgent>(),Is.Null);Assert.That(Prefab.GetComponent(Real("TitanRuntimeActor")),Is.Null);
    }
    [UnityTest]public IEnumerator SpawnApiUsesExistingDataRejectsCrowdsAndDespawnsOnlyOwnedActors()
    {
        string error;ModularTitanBody a,b;int triangles=NavMesh.CalculateTriangulation().indices.Length;
        Assert.That(Spawn(3,Vector3.zero,out a,out error),Is.True,error);Ready(a);
        Assert.That(Spawn(5,new Vector3(65,0,0),out b,out error),Is.True,error);Ready(b);
        Assert.That(Spawn(3,Vector3.right,out var rejected,out error),Is.False);Assert.That(rejected,Is.Null);StringAssert.Contains("blocked",error.ToLowerInvariant());
        Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));Assert.That((bool)Call(manager,"Despawn",a));Assert.That(Prop<int>(manager,"RegisteredCount"),Is.EqualTo(1));
        Assert.That((bool)Call(manager,"Despawn",a),Is.False);yield return null;Assert.That(a==null);Ready(b);
    }
    [UnityTest]public IEnumerator DuplicateRegistrationAndCombatToggleDoNotResetHealthOrDuplicateComponents()
    {
        string error;ModularTitanBody b;Assert.That(Spawn(3,Vector3.zero,out b,out error),Is.True,error);
        b.ApplyDamage(TitanHitRegion.Nape,125,b.head.position,Vector3.zero);
        for(int i=0;i<4;i++)Assert.That(Register(b,out error),Is.True,error);
        Assert.That(b.GetHealth(TitanHitRegion.Nape),Is.EqualTo(375));Assert.That(b.GetComponents<TitanGrabAttack>().Length,Is.EqualTo(1));
        Call(manager,"SetCombatEnabled",false);Assert.That(b.GetComponent<TitanCombatDirector>().enabled,Is.False);Assert.That(b.GetComponent<TitanStompAttack>().enabled,Is.False);
        Call(manager,"SetCombatEnabled",true);Assert.That(b.GetComponent<TitanCombatDirector>().enabled);Assert.That(b.GetComponent<TitanGrabAttack>().enableBite);yield return null;
    }
    [UnityTest]public IEnumerator DisableEnableAndManagerSuspendResumePreserveDamageAndOwnership()
    {
        string error;ModularTitanBody b;Assert.That(Spawn(3,Vector3.zero,out b,out error),Is.True,error);b.ApplyDamage(TitanHitRegion.Nape,125,b.head.position,Vector3.zero);
        b.gameObject.SetActive(false);Assert.That(Prop<int>(manager,"ActiveCount"),Is.Zero);b.gameObject.SetActive(true);yield return null;
        Assert.That(Prop<bool>(Actor(b),"IsReady"));Assert.That(b.GetHealth(TitanHitRegion.Nape),Is.EqualTo(375));
        manager.enabled=false;Assert.That(b.GetComponent<NavMeshAgent>().enabled,Is.False);manager.enabled=true;yield return null;yield return null;
        Assert.That(Prop<bool>(Actor(b),"IsReady"),Is.True,Prop<string>(Actor(b),"Error"));Assert.That(b.GetHealth(TitanHitRegion.Nape),Is.EqualTo(375));Assert.That(b.GetComponents<TitanCombatDirector>().Length,Is.EqualTo(1));
    }
    [UnityTest]public IEnumerator InvalidUnsupportedOffMeshAndBlockedSpawnsFailWithoutComponentsOrDistantSnapping()
    {
        string error;ModularTitanBody b;Assert.That(Spawn(2,Vector3.zero,out b,out error),Is.False);StringAssert.Contains("profile",error);
        Assert.That(Spawn(3,new Vector3(10000,0,0),out b,out error),Is.False);StringAssert.Contains("No ground",error);
        var wall=new GameObject("Real spawn obstruction");wall.AddComponent<BoxCollider>().size=new Vector3(10,40,10);wall.transform.position=Vector3.up*20;Physics.SyncTransforms();
        Assert.That(Spawn(3,Vector3.zero,out b,out error),Is.False);StringAssert.Contains("blocked",error.ToLowerInvariant());
        var invalid=Clone(3,new Vector3(50,0,0),false);invalid.transform.localScale=new Vector3(3,3,5);
        Assert.That(Register(invalid,out error),Is.False);StringAssert.Contains("uniform",error);Assert.That(Actor(invalid),Is.Null);Assert.That(invalid.GetComponent<NavMeshAgent>(),Is.Null);
        Assert.That(Prop<int>(manager,"RegisteredCount"),Is.Zero);yield return null;
    }
    [UnityTest]public IEnumerator WideActualArmOutsideNavCapsuleCannotSpawnThroughScenery()
    {
        var wall=new GameObject("Arm-width obstruction outside root capsule");wall.transform.position=new Vector3(14,27,0);wall.AddComponent<BoxCollider>().size=Vector3.one*2;Physics.SyncTransforms();
        string error;ModularTitanBody b;Assert.That(Spawn(3,Vector3.zero,out b,out error),Is.False);StringAssert.Contains("Arm-width obstruction",error);Assert.That(Prop<int>(manager,"RegisteredCount"),Is.Zero);yield return null;
    }
    [UnityTest]public IEnumerator InactivePlacedActorWaitsForEnableAndForeignSceneIsNotAdopted()
    {
        var b=Clone(3,Vector3.zero,false);Set(manager,"titans",new[]{b});yield return null;Assert.That(Actor(b),Is.Null);Assert.That(b.gameObject.activeSelf,Is.False);
        b.gameObject.SetActive(true);yield return null;yield return null;Ready(b);
        foreign=SceneManager.CreateScene("Other opt-in boundary",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var other=Clone(3,new Vector3(60,0,0),false);SceneManager.MoveGameObjectToScene(other.gameObject,foreign);other.gameObject.SetActive(true);yield return null;
        Assert.That(Actor(other),Is.Null);string error;Assert.That(Register(other,out error),Is.False);StringAssert.Contains("scene",error);Assert.That(Prop<int>(manager,"RegisteredCount"),Is.EqualTo(1));
    }
    [UnityTest]public IEnumerator PlacedActorsDiscoverOnManagerEnableAndAnotherOwnerCannotStealThem()
    {
        manager.enabled=false;var b=Clone(3,Vector3.zero);manager.enabled=true;yield return null;yield return null;Ready(b);
        var host=new GameObject("Other manager");var other=(MonoBehaviour)host.AddComponent(Real("TitanFreeRoamIntegration"));
        Set(other,"autoRegisterSceneTitans",false);Set(other,"showDiagnostics",false);Set(other,"player",manager.GetType().GetField("player").GetValue(manager));Set(other,"navigation",nav);
        object[] args={b,null};Assert.That((bool)Call(other,"TryRegister",args),Is.False);StringAssert.Contains("owner",(string)args[1]);Assert.That(Prop<object>(Actor(b),"Owner"),Is.SameAs(manager));Ready(b);
    }
    [UnityTest]public IEnumerator ExplicitInactiveListStillRegistersOnEnableWithAutoDiscoveryOff()
    {
        Set(manager,"autoRegisterSceneTitans",false);var b=Clone(3,Vector3.zero,false);Set(manager,"titans",new[]{b});yield return null;
        Assert.That(Actor(b),Is.Null);b.gameObject.SetActive(true);yield return null;yield return null;Ready(b);
        var ignored=Clone(3,new Vector3(65,0,0));yield return null;Assert.That(Actor(ignored),Is.Null);
    }
    [UnityTest]public IEnumerator NavigationDisableSuspendsAndReregisteringDataResumesWithoutRebaking()
    {
        string error;ModularTitanBody b;Assert.That(Spawn(3,Vector3.zero,out b,out error),Is.True,error);int triangles=NavMesh.CalculateTriangulation().indices.Length;
        nav.enabled=false;yield return null;Assert.That(Prop<bool>(Actor(b),"IsReady"),Is.False);Assert.That(b.GetComponent<NavMeshAgent>().enabled,Is.False);
        nav.enabled=true;yield return null;yield return null;Ready(b);Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));
    }
    [UnityTest]public IEnumerator ChangingAnInitializedScaleIsRejectedWithoutRebuildingOrResettingTheRig()
    {
        string error;ModularTitanBody b;Assert.That(Spawn(3,Vector3.zero,out b,out error),Is.True,error);b.ApplyDamage(TitanHitRegion.Nape,125,b.head.position,Vector3.zero);
        b.transform.localScale=Vector3.one*5;Assert.That(Register(b,out error),Is.False);StringAssert.Contains("scale changed",error);
        Assert.That(b.GetComponent<NavMeshAgent>().enabled,Is.False);Assert.That(b.GetHealth(TitanHitRegion.Nape),Is.EqualTo(375));Assert.That(b.GetComponents<TitanCombatDirector>().Length,Is.EqualTo(1));yield return null;
    }
    [UnityTest]public IEnumerator BakeSizeRequestKeepsExistingProfilesAndFutureSizesWithoutPlaceholderActors()
    {
        Set(manager,"additionalBakeScales",new[]{2f,2.00001f});Clone(7,new Vector3(70,0,0),false);
        object[] args={null,null};Assert.That((bool)Call(manager,"TryGetBakeScales",args),Is.True,(string)args[1]);
        CollectionAssert.AreEqual(new[]{1f,2f,3f,5f,7f},(float[])args[0]);Assert.That(nav.ForScale(2),Is.Null,"Declaring a size does NOT secretly bake it at runtime");
        Set(manager,"additionalBakeScales",new[]{float.NaN});args=new object[]{null,null};Assert.That((bool)Call(manager,"TryGetBakeScales",args),Is.False);StringAssert.Contains("finite",(string)args[1]);yield return null;
    }
    [UnityTest]public IEnumerator SpawnedDirectorActuallySelectsAndLandsAGroundAttack()
    {
        string error;ModularTitanBody b;Assert.That(Spawn(1,Vector3.zero,out b,out error),Is.True,error);var brain=b.GetComponent<TitanPatrolChaseAI>();brain.automaticTick=false;
        var director=b.GetComponent<TitanCombatDirector>();director.automaticTick=false;director.ResetDecisions(2);
        player.transform.position=new Vector3(.7f,1,5.7f);b.animator.Update(0);Physics.SyncTransforms();brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget);Assert.That(brain.State,Is.EqualTo(TitanAwarenessState.Chase));director.TickSelection(.2f);
        var stomp=b.GetComponent<TitanStompAttack>();Assert.That(stomp.IsBusy,Is.True,director.LastChoice);stomp.TickAttack(.81f);
        Assert.That(stomp.damage,Is.EqualTo(60),"Gameplay stomp is intentionally stronger than the historical40HP lab attack");Assert.That(stomp.cooldownDuration,Is.EqualTo(1.6f));
        Assert.That(human.HitCount,Is.EqualTo(1));Assert.That(human.Health,Is.EqualTo(40));yield return null;
    }
    [UnityTest]public IEnumerator DespawningHoldingActorReleasesLeaseAndDeadActorsAreNotResurrected()
    {
        string error;ModularTitanBody b;Assert.That(Spawn(1,Vector3.zero,out b,out error),Is.True,error);var brain=b.GetComponent<TitanPatrolChaseAI>();brain.automaticTick=false;
        b.GetComponent<TitanCombatDirector>().automaticTick=false;player.transform.position=new Vector3(.7f,1,5.7f);b.animator.Update(0);Physics.SyncTransforms();brain.TickAI(.2f);
        var grab=b.GetComponent<TitanGrabAttack>();grab.automaticTick=false;Assert.That(grab.TryBeginGrab(human,true));grab.TickAttack(.84f);Assert.That(human.IsHeld,Is.True,grab.LastResult);
        Assert.That((bool)Call(manager,"Despawn",b));Assert.That(human.IsHeld,Is.False);Assert.That(player.transform.parent,Is.Null);yield return null;
        player.transform.position=new Vector3(0,1,100);Assert.That(Spawn(1,Vector3.zero,out b,out error),Is.True,error);b.Die();Assert.That(Register(b,out error),Is.False);
        Assert.That(b.IsDead);Assert.That(Prop<bool>(Actor(b),"IsReady"),Is.False);Assert.That(b.animator.enabled,Is.False);
    }
}
#endif
