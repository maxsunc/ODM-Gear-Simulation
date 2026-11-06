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

/// <summary>Navigation/runtime tests of the CURRENT saved user map, not a map rebuild.</summary>
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
            Assert.That(body.GetComponent<TitanPatrolChaseAI>().chaseStoppingDistance,Is.EqualTo(6*size));
            Assert.That(body.GetComponent<TitanStompAttack>().damage,Is.EqualTo(60));Assert.That(body.GetComponent<TitanStompAttack>().cooldownDuration,Is.EqualTo(1.6f));
            Assert.That(body.GetComponent<TitanGrabAttack>().drawTelegraph || body.GetComponent<TitanStompAttack>().drawTelegraph,Is.False);
            Assert.That((bool)Call(manager,"Despawn",body),Is.True);yield return null;
        }
        Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));
    }
    [UnityTest]public IEnumerator AllSavedSizesProduceSynchronizedWalkRunImpactsAtTheirActualBakedTerrainHeight()
    {
        var prefab=Field<GameObject>(spawner,"titanPrefab");var report=new System.Text.StringBuilder();
        foreach(float size in new[]{3f,5f,6f,7f,10f})
        {
            ModularTitanBody body=null;string error="";
            for(int z=-600;z<=600&&body==null;z+=100)for(int x=-600;x<=600&&body==null;x+=100)
            {
                object[] args={prefab,Point(x,z),Quaternion.identity,size,null,null};if((bool)Call(manager,"TrySpawn",args))body=(ModularTitanBody)args[4];error=(string)args[5];
            }
            Assert.That(body,Is.Not.Null,error);body.GetComponent<TitanPatrolChaseAI>().enabled=false;
            var motion=body.GetComponent<TitanMotionStyle>();motion.automaticTick=false;
            var emitter=(Behaviour)body.GetComponent(Real("TitanAudioEmitter"));emitter.GetType().GetField("automaticSampling").SetValue(emitter,false);
            foreach(float gait in new[]{0f,1f})
            {
                emitter.enabled=false;emitter.enabled=true;int before=Prop<int>(emitter,"FootstepCount");
                for(int i=0;i<720;i++)
                {
                    body.SetWalking(true);motion.TickMotion(1f/120,gait==0?4.6f:16f,gait==1,0);body.animator.SetFloat("Gait",gait);body.animator.SetFloat("CycleOffset",.23f);
                    body.animator.Play("Locomotion",0,i/240f);body.animator.Update(0);Call(emitter,"SampleFeet",1f/120);
                }
                int strikes=Prop<int>(emitter,"FootstepCount")-before;report.AppendLine("size="+size+" gait="+gait+" navRootAboveTerrain="+(body.transform.position.y-Point(body.transform.position.x,body.transform.position.z).y)+" strikes="+strikes);
                Assert.That(strikes,Is.InRange(5,6),report.ToString());
            }
            Assert.That((bool)Call(manager,"Despawn",body),Is.True);yield return null;
        }
        Debug.Log("NATIVE_FOREST_FOOTSTEPS\n"+report);File.WriteAllText("Library/GiantForest2/footstep-terrain-report.txt",report.ToString());
    }
    [UnityTest]public IEnumerator GameplayDirectorSelectsSizedGroundStompsAndDamagesTheRealHealthBar()
    {
        var player=Field<Component>(manager,"player");var health=player.GetComponent(Real("PlayerHealth"));
        Assert.That(Field<bool>(health,"showHealthBar"),Is.True);Assert.That(manager.GetType().GetMethod("OnGUI",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic),Is.Null);
        ((Behaviour)player).enabled=false;((Behaviour)player.GetComponent(Real("ODM_Movement"))).enabled=false;
        var cc=player.GetComponent<CharacterController>();var victim=player.GetComponent<TitanCombatTarget>();var prefab=Field<GameObject>(spawner,"titanPrefab");
        foreach(float size in new[]{3f,5f,6f,7f,10f})
        {
            ModularTitanBody body=null;string error="";
            for(int z=-600;z<=600&&body==null;z+=100)for(int x=-600;x<=600&&body==null;x+=100)
            {object[] args={prefab,Point(x,z),Quaternion.identity,size,null,null};if((bool)Call(manager,"TrySpawn",args))body=(ModularTitanBody)args[4];error=(string)args[5];}
            Assert.That(body,Is.Not.Null,error);var brain=body.GetComponent<TitanPatrolChaseAI>();brain.automaticTick=false;
            var director=body.GetComponent<TitanCombatDirector>();director.automaticTick=false;director.ResetDecisions(2);var stomp=body.GetComponent<TitanStompAttack>();stomp.automaticTick=false;
            health.GetType().GetField("currentHP").SetValue(health,100f);
            foreach(float distance in new[]{5.7f,5.2f,4.7f})
            {
                if(stomp.IsBusy)break;var p=body.transform.position+Vector3.forward*(size*distance);p.y=Point(p.x,p.z).y+cc.height*.5f;
                cc.enabled=false;player.transform.position=p;cc.enabled=true;Physics.SyncTransforms();brain.TickAI(.2f);director.TickSelection(.3f);
            }
            Assert.That(stomp.IsBusy,Is.True,"size="+size+" "+director.LastChoice+" / "+brain.NavigationStatus);
            Assert.That(brain.IsMovementLocked,Is.True);var audio=body.GetComponent(Real("TitanAudioEmitter"));int sounds=Prop<int>(audio,"FootstepCount");
            for(int i=0;i<97;i++){body.animator.Update(0);stomp.TickAttack(1f/120);stomp.ApplyPose();}
            Assert.That(victim.Health,Is.EqualTo(40),"Actual60HP stomp at"+size+"x");Assert.That(Field<float>(health,"currentHP"),Is.EqualTo(40));
            Assert.That(Prop<int>(audio,"FootstepCount"),Is.EqualTo(sounds+1));Assert.That(body.GetComponentsInChildren<LineRenderer>(),Is.Empty);
            Assert.That(health.GetType().GetMethod("TickRegeneration"),Is.Null);Assert.That(victim.Health,Is.EqualTo(40));
            var debris=body.GetComponent(Real("TitanStompDebris"));Assert.That(debris,Is.Not.Null);Assert.That(Prop<int>(debris,"BurstCount"),Is.EqualTo(1));Assert.That(Vector3.Distance(Prop<Vector3>(debris,"LastPoint"),stomp.LockedPoint),Is.LessThan(.001f));
            Assert.That(body.GetComponent<TitanGrabAttack>().holdDamagePerSecond,Is.EqualTo(10));
            Debug.Log("NATIVE_SIZED_STOMP size="+size+" stop="+brain.chaseStoppingDistance+" damage="+stomp.damage+" persistentHp="+victim.Health);
            Assert.That((bool)Call(manager,"Despawn",body),Is.True);yield return null;
        }
    }
    [UnityTest]public IEnumerator ActualForestScoutStrugglesOnlyDuringConfirmedGrabWithOriginalDamageQteAndWorldIdentity()
    {
        var player=Field<Component>(manager,"player");var driver=player.GetComponent(Real("ScoutPlayerAnimation"));Assert.That(driver,Is.Not.Null);Assert.That(((Behaviour)driver).enabled,Is.True);var target=player.GetComponent<TitanCombatTarget>();var cc=player.GetComponent<CharacterController>();var prefab=Field<GameObject>(spawner,"titanPrefab");ModularTitanBody body=null;
        for(int z=-600;z<=600&&body==null;z+=100)for(int x=-600;x<=600&&body==null;x+=100){object[] args={prefab,Point(x,z),Quaternion.identity,3f,null,null};if((bool)Call(manager,"TrySpawn",args))body=(ModularTitanBody)args[4];}Assert.That(body,Is.Not.Null);
        body.GetComponent<TitanPatrolChaseAI>().automaticTick=false;body.GetComponent<TitanCombatDirector>().automaticTick=false;body.GetComponent<TitanStompAttack>().automaticTick=false;body.GetComponent<TitanMotionStyle>().automaticTick=false;var grab=body.GetComponent<TitanGrabAttack>();grab.automaticTick=false;Assert.That(grab.holdDamagePerSecond,Is.EqualTo(10));
        int id=player.GetInstanceID(),handle=scene.handle,triangles=NavMesh.CalculateTriangulation().indices.Length;cc.enabled=false;player.transform.position=body.transform.TransformPoint(new Vector3(-.7f,8,2.6f));cc.enabled=true;body.animator.Update(0);Physics.SyncTransforms();body.GetComponent<TitanPatrolChaseAI>().TickAI(.2f);Assert.That(grab.TryBeginAerialGrab(target,true),Is.True,"Native chase/contact prerequisites: "+body.GetComponent<TitanPatrolChaseAI>().State+" / "+body.GetComponent<TitanPatrolChaseAI>().NavigationStatus);
        for(int i=0;i<40&&!target.IsHeld;i++){grab.TickAttack(1f/120);body.animator.Update(0);grab.ApplyPose();target.TickHold(1f/120);}Assert.That(target.IsHeldBy(grab),Is.True);float hp=target.Health;Vector3 localGrip=player.transform.InverseTransformPoint(body.leftGripSocket.position);var model=Field<Transform>(driver,"model");var animator=Field<Animator>(driver,"animator");var head=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");yield return null;float at=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;Quaternion q=head.localRotation;
        yield return new WaitForSeconds(.3f);Assert.That(Prop<bool>(driver,"IsStruggling"),Is.True);Assert.That(Prop<string>(driver,"State"),Is.EqualTo("Held"));Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-at,Is.GreaterThan(.15f));Assert.That(Quaternion.Angle(q,head.localRotation),Is.GreaterThan(.5f));Assert.That(target.Health,Is.LessThan(hp).And.GreaterThan(hp-5));Assert.That(Vector3.Distance(player.transform.TransformPoint(localGrip),body.leftGripSocket.position),Is.LessThan(.003f));Assert.That(player.transform.IsChildOf(body.transform),Is.False);
        var escape=player.GetComponent<TitanGrabEscape>();Assert.That(escape.State,Is.EqualTo(TitanGrabEscapeState.Running));Assert.That(TitanGrabEscape.Window,Is.EqualTo(3));grab.Cancel("Owned forest held animation complete");yield return null;Assert.That(Prop<bool>(driver,"IsStruggling"),Is.False);Assert.That(player.GetInstanceID(),Is.EqualTo(id));Assert.That(scene.handle,Is.EqualTo(handle));Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));
        Debug.Log("NATIVE_FOREST_HELD_STRUGGLE samePlayer="+id+" hp="+target.Health+" loopDuration=1.2 damagePerSecond="+grab.holdDamagePerSecond+" navigationTriangles="+triangles);
    }
    [UnityTest]public IEnumerator RealForestCorpsesSteamFadeAndDisappearWithoutResettingPlayerWorldOrInjuredSurvivor()
    {
        Call(manager,"SetCombatEnabled",false);var player=Field<Component>(manager,"player");var health=player.GetComponent(Real("PlayerHealth"));var prefab=Field<GameObject>(spawner,"titanPrefab");int triangles=NavMesh.CalculateTriangulation().indices.Length,playerId=player.GetInstanceID(),handle=scene.handle;float hp=Field<float>(health,"currentHP");
        ModularTitanBody injured=null,dead=null;
        for(int z=-600;z<=600&&(injured==null||dead==null);z+=100)for(int x=-600;x<=600&&(injured==null||dead==null);x+=100)
        {object[] args={prefab,Point(x,z),Quaternion.identity,3f,null,null};if((bool)Call(manager,"TrySpawn",args)){if(injured==null)injured=(ModularTitanBody)args[4];else dead=(ModularTitanBody)args[4];}}
        Assert.That(injured!=null&&dead!=null,Is.True);injured.Sever(TitanHitRegion.RightLeg,Vector3.zero);injured.ReceiveDamage(TitanHitRegion.LeftArm,50,injured.leftHand.position,Vector3.zero);dead.Die();var fx=dead.GetComponent<TitanCorpseDecomposition>();var steam=fx.Steam;Assert.That(fx,Is.Not.Null);float start=Time.time;
        while(Time.time-start<4)yield return null;Assert.That(fx.Shade,Is.LessThan(.9f));Assert.That(fx.Opacity,Is.EqualTo(1));Assert.That(steam.particleCount,Is.GreaterThan(0));
        while(Time.time-start<10)yield return null;Assert.That(fx.Opacity,Is.InRange(.1f,.95f));Assert.That(dead.IsDead,Is.True);
        while(dead!=null&&Time.time-start<20)yield return null;Assert.That(dead==null,Is.True);Assert.That(steam==null,Is.True);yield return new WaitForSeconds(1.1f);
        Assert.That(injured!=null&&!injured.IsDead,Is.True);Assert.That(injured.IsSevered(TitanHitRegion.RightLeg),Is.True);Assert.That(injured.GetHealth(TitanHitRegion.LeftArm),Is.EqualTo(950));Assert.That(player.GetInstanceID(),Is.EqualTo(playerId));Assert.That(Field<float>(health,"currentHP"),Is.EqualTo(hp));Assert.That(scene.handle,Is.EqualTo(handle));Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));
        Debug.Log("NATIVE_FOREST_DECOMPOSITION lifetime="+fx+" elapsed="+(Time.time-start)+" survivorArmHp="+injured.GetHealth(TitanHitRegion.LeftArm)+" playerHp="+hp+" navigationTriangles="+triangles);
    }
    [UnityTest]public IEnumerator PlayerDeathRespawnsOnlyPlayerAndPreservesActualForestActorsNavigationAndMusic()
    {
        var player=Field<Component>(manager,"player");var health=(MonoBehaviour)player.GetComponent(Real("PlayerHealth"));var target=player.GetComponent<TitanCombatTarget>();var prefab=Field<GameObject>(spawner,"titanPrefab");
        int rootId=player.GetInstanceID(),managerId=manager.GetInstanceID(),spawnerId=spawner.GetInstanceID(),sceneHandle=scene.handle,triangles=NavMesh.CalculateTriangulation().indices.Length;var music=All<AudioSource>().Where(a=>a.outputAudioMixerGroup!=null&&a.outputAudioMixerGroup.name=="Music").Select(a=>a.GetInstanceID()).ToArray();
        ModularTitanBody injured=null,dead=null;
        for(int z=-600;z<=600&&(injured==null||dead==null);z+=100)for(int x=-600;x<=600&&(injured==null||dead==null);x+=100)
        {object[] args={prefab,Point(x,z),Quaternion.identity,3f,null,null};if((bool)Call(manager,"TrySpawn",args)){if(injured==null)injured=(ModularTitanBody)args[4];else dead=(ModularTitanBody)args[4];}}
        Assert.That(injured!=null&&dead!=null,Is.True);injured.ReceiveDamage(TitanHitRegion.LeftArm,50,injured.leftHand.position,Vector3.zero);injured.Sever(TitanHitRegion.RightLeg,Vector3.zero);dead.Die();int injuredId=injured.GetInstanceID(),deadId=dead.GetInstanceID();float armHp=injured.GetHealth(TitanHitRegion.LeftArm);
        int loads=0;UnityEngine.Events.UnityAction<Scene,LoadSceneMode> loaded=(s,m)=>loads++;SceneManager.sceneLoaded+=loaded;
        try
        {
            target.TakeDamage(100,player.transform.position);Assert.That(Field<bool>(health,"isDead"),Is.True);float at=Time.time;
            while(Field<bool>(health,"isDead")&&Time.time-at<5)yield return null;
            Assert.That(Field<bool>(health,"isDead"),Is.False,Prop<string>(player.GetComponent(Real("PlayerRespawn")),"Error"));Assert.That(target.Health,Is.EqualTo(100));Assert.That(Time.time-at,Is.GreaterThanOrEqualTo(2.99f));Assert.That(loads,Is.Zero);
            Assert.That(player.GetInstanceID(),Is.EqualTo(rootId));Assert.That(manager.GetInstanceID(),Is.EqualTo(managerId));Assert.That(spawner.GetInstanceID(),Is.EqualTo(spawnerId));Assert.That(scene.handle,Is.EqualTo(sceneHandle));
            Assert.That(injured.GetInstanceID(),Is.EqualTo(injuredId));Assert.That(injured.IsSevered(TitanHitRegion.RightLeg),Is.True);Assert.That(injured.GetHealth(TitanHitRegion.LeftArm),Is.EqualTo(armHp));Assert.That(dead.GetInstanceID(),Is.EqualTo(deadId));Assert.That(dead.IsDead,Is.True);
            Assert.That(injured.GetComponent<TitanPatrolChaseAI>().target,Is.EqualTo(player.transform));Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));Assert.That(All<AudioSource>().Where(a=>a.outputAudioMixerGroup!=null&&a.outputAudioMixerGroup.name=="Music").Select(a=>a.GetInstanceID()),Is.EquivalentTo(music));
            Assert.That(player.GetComponent<CharacterController>().enabled,Is.True);Assert.That(((Behaviour)player).enabled,Is.True);Assert.That(((Behaviour)player.GetComponent(Real("ODM_Movement"))).enabled,Is.True);
            Debug.Log("NATIVE_FOREST_PLAYER_ONLY_RESPAWN seconds="+(Time.time-at)+" sameScene="+sceneHandle+" samePlayer="+rootId+" injuredHp="+armHp+" deadTitanStillDead="+dead.IsDead);
        }
        finally{SceneManager.sceneLoaded-=loaded;health.CancelInvoke();}
    }
    [UnityTest]public IEnumerator CurrentUserSizeChoicesAreAllSupportedAndTheActualRandomSpawnerArrives()
    {
        var player=Field<Component>(manager,"player");Assert.That(player.GetComponents<TitanGrabEscape>().Length,Is.EqualTo(1),"Current saved map auto-installs one player QTE without changing its scene asset");
        Assert.That(player.GetComponent<TitanGrabEscape>().automaticInput,Is.True);
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
