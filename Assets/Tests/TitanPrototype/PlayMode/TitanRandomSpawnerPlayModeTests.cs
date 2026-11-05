#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class TitanRandomSpawnerPlayModeTests
{
    Scene scene,previous;GameObject player;MonoBehaviour manager,spawner;TitanWorldNavigation nav;
    readonly List<int> ids=new List<int>();readonly List<NavMeshData> data=new List<NavMeshData>();
    static Type Real(string n){return AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Assembly-CSharp").GetType(n,true);}
    static void Set(object o,string n,object v){o.GetType().GetField(n).SetValue(o,v);}
    static T Prop<T>(object o,string n){return (T)o.GetType().GetProperty(n).GetValue(o);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n).Invoke(o,a);}
    GameObject Prefab{get{return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");}}
    void Choices(float[] scales,float[] weights=null)
    {
        var t=Real("TitanRandomSpawner+SizeChoice");var array=Array.CreateInstance(t,scales.Length);
        for(int i=0;i<scales.Length;i++){var o=Activator.CreateInstance(t);Set(o,"scale",scales[i]);Set(o,"weight",weights==null?1f:weights[i]);array.SetValue(o,i);}Set(spawner,"sizes",array);
    }
    bool Queue(out string error){object[] a={null};bool ok=(bool)Call(spawner,"TryQueueSpawn",a);error=(string)a[0];return ok;}
    ModularTitanBody Pending{get{return Prop<ModularTitanBody>(spawner,"PendingBody");}}
    [UnitySetUp]public IEnumerator Setup()
    {
        previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Owned random Titan spawn fixture",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        var floor=new GameObject("Actual floor");floor.transform.position=Vector3.down*.5f;floor.AddComponent<BoxCollider>().size=new Vector3(240,1,240);
        var sources=new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(floor.transform.position,Quaternion.identity,Vector3.one),size=new Vector3(240,1,240)}};
        var profiles=new List<TitanWorldNavigation.Profile>();
        foreach(float scale in new[]{1f,3f,5f})
        {
            var setting=NavMesh.CreateSettings();ids.Add(setting.agentTypeID);var asset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];var so=new SerializedObject(asset);var entries=so.FindProperty("m_Settings");
            for(int i=0;i<entries.arraySize;i++){var e=entries.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("agentTypeID").intValue!=setting.agentTypeID)continue;e.FindPropertyRelative("agentRadius").floatValue=1.8f*scale;e.FindPropertyRelative("agentHeight").floatValue=12*scale;}
            so.ApplyModifiedPropertiesWithoutUndo();setting=NavMesh.GetSettingsByID(setting.agentTypeID);setting.overrideVoxelSize=true;setting.voxelSize=setting.agentRadius/4;
            var d=NavMeshBuilder.BuildNavMeshData(setting,sources,new Bounds(Vector3.up*30,new Vector3(260,160,260)),Vector3.zero,Quaternion.identity);data.Add(d);
            profiles.Add(new TitanWorldNavigation.Profile{scale=scale,agentTypeID=setting.agentTypeID,radius=setting.agentRadius,height=setting.agentHeight,data=d});
        }
        var host=new GameObject("Scene manager");nav=host.AddComponent<TitanWorldNavigation>();nav.profiles=profiles.ToArray();nav.bakedBounds=new Bounds(Vector3.up*30,new Vector3(260,160,260));Assert.That(nav.Register(),Is.True,nav.Error);
        player=new GameObject("Player");player.transform.position=new Vector3(0,1,100);var box=player.AddComponent<BoxCollider>();box.center=Vector3.zero;box.size=new Vector3(.8f,2,.8f);player.AddComponent<TitanCombatTarget>();
        var movement=(MonoBehaviour)player.AddComponent(Real("Movement"));movement.enabled=false;
        manager=(MonoBehaviour)host.AddComponent(Real("TitanFreeRoamIntegration"));Set(manager,"player",movement);Set(manager,"navigation",nav);Set(manager,"enableCombat",true);Set(manager,"showDiagnostics",false);
        var go=new GameObject("Spawner under test");spawner=(MonoBehaviour)go.AddComponent(Real("TitanRandomSpawner"));Set(spawner,"sceneManager",manager);Set(spawner,"titanPrefab",Prefab);
        Set(spawner,"automaticSpawning",false);Set(spawner,"initialSpawnCount",0);Set(spawner,"initialDelay",10000f);Set(spawner,"effectsEnabled",false);Set(spawner,"warningDuration",.04f);
        Set(spawner,"minimumPlayerDistance",20f);Set(spawner,"minimumTitanSeparation",10f);Set(spawner,"randomSeed",123);
        Choices(new[]{1f});Call(spawner,"RestartSchedule");Physics.SyncTransforms();yield return null;
    }
    [UnityTearDown]public IEnumerator Cleanup()
    {
        SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);foreach(int id in ids)NavMesh.RemoveSettings(id);ids.Clear();foreach(var d in data)if(d!=null)Object.Destroy(d);data.Clear();yield return null;
    }
    void Effects()
    {
        Set(spawner,"effectsEnabled",true);Set(spawner,"lightningMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSpawnLightning.mat"));Set(spawner,"particleMaterial",AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSwordImpact.mat"));
    }
    Component[] Fx(){return spawner.GetComponentsInChildren(Real("TitanSpawnStrikeEffect"),true);}
    [UnityTest]public IEnumerator WeightedCustomSizesAreRepeatableAndDoNotDisturbGameplayRandom()
    {
        Choices(new[]{3f,5f},new[]{1f,3f});UnityEngine.Random.InitState(98765);var before=UnityEngine.Random.state;
        var sequence=new List<float>();int small=0;
        for(int i=0;i<1000;i++){object[] args={0f,null};Assert.That((bool)Call(spawner,"TryChooseScale",args));float s=(float)args[0];sequence.Add(s);if(s==3)small++;else Assert.That(s,Is.EqualTo(5));}
        Assert.That(small,Is.InRange(180,320));Assert.That(UnityEngine.Random.state,Is.EqualTo(before));Call(spawner,"RestartSchedule");
        for(int i=0;i<30;i++){object[] args={0f,null};Call(spawner,"TryChooseScale",args);Assert.That(args[0],Is.EqualTo(sequence[i]));}yield return null;
    }
    [UnityTest]public IEnumerator RandomSpawnIsInactiveThroughWarningThenFightsOnSharedNavigationAndRespectsCap()
    {
        Set(spawner,"maximumAlive",1);int triangles=NavMesh.CalculateTriangulation().indices.Length;string error;
        Assert.That(Queue(out error),Is.True,error);var body=Pending;Assert.That(body.gameObject.activeSelf,Is.False);Assert.That(Prop<int>(manager,"PreparedCount"),Is.EqualTo(1));Assert.That(Prop<int>(manager,"LivingCount"),Is.Zero);
        yield return new WaitForSeconds(.08f);Assert.That(body.gameObject.activeInHierarchy);Assert.That(body.GetComponent<NavMeshAgent>().isOnNavMesh);Assert.That(body.GetComponent<TitanCombatDirector>().isActiveAndEnabled);
        Assert.That(Prop<int>(spawner,"SpawnedCount"),Is.EqualTo(1));Assert.That(Prop<int>(manager,"PreparedCount"),Is.Zero);Assert.That(Queue(out error),Is.False);StringAssert.Contains("cap",error);
        Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));Assert.That(Vector3.ProjectOnPlane(body.transform.position-player.transform.position,Vector3.up).magnitude,Is.GreaterThanOrEqualTo(20));
    }
    [UnityTest]public IEnumerator PlayerEnteringWarningZoneCancelsBeforeTitanOrImpactSound()
    {
        Effects();Set(spawner,"warningDuration",1f);string error;Assert.That(Queue(out error),Is.True,error);var body=Pending;player.transform.position=body.transform.position+Vector3.up;
        yield return null;yield return null;Assert.That(body==null);Assert.That(Prop<int>(spawner,"SpawnedCount"),Is.Zero);Assert.That(Prop<int>(manager,"PreparedCount"),Is.Zero);
        foreach(var fx in Fx()){Assert.That(Prop<int>(fx,"ImpactCount"),Is.Zero);Assert.That(Prop<int>(fx,"SoundCount"),Is.Zero);Assert.That(Prop<bool>(fx,"IsPlaying"),Is.False);}
    }
    [UnityTest]public IEnumerator NewSolidDuringWarningIsRecheckedAndNoFakeStrikeIsConfirmed()
    {
        Effects();string error;Assert.That(Queue(out error),Is.True,error);var body=Pending;
        var wall=new GameObject("New blocking solid");wall.transform.position=body.transform.position+Vector3.up*6;wall.AddComponent<BoxCollider>().size=new Vector3(3,12,3);Physics.SyncTransforms();
        yield return new WaitForSeconds(.09f);Assert.That(Prop<int>(spawner,"SpawnedCount"),Is.Zero);Assert.That(Pending,Is.Null);Assert.That(Prop<int>(manager,"PreparedCount"),Is.Zero);
        foreach(var fx in Fx())Assert.That(Prop<int>(fx,"ImpactCount"),Is.Zero);
    }
    [UnityTest]public IEnumerator ConfirmedStrikeUsesBoundedCosmeticsAndOptionalAssignedSoundWithoutPhysicsOrCameraAuthority()
    {
        Effects();Set(spawner,"maximumAlive",4);Set(spawner,"effectPoolSize",2);var clip=AudioClip.Create("Owned silent sound assignment probe",22050,1,44100,false);Set(spawner,"spawnSound",clip);
        Vector3 gravity=Physics.gravity,root=player.transform.position;float timeScale=Time.timeScale;string error;
        for(int i=0;i<3;i++){Assert.That(Queue(out error),Is.True,error);yield return new WaitForSeconds(.09f);}
        Assert.That(Fx().Length,Is.EqualTo(2));Assert.That(Fx().Sum(f=>Prop<int>(f,"ImpactCount")),Is.EqualTo(3));Assert.That(Fx().Sum(f=>Prop<int>(f,"SoundCount")),Is.EqualTo(3));
        foreach(var fx in Fx()){Assert.That(fx.GetComponentsInChildren<Collider>().Length,Is.Zero);Assert.That(fx.GetComponentsInChildren<Rigidbody>().Length,Is.Zero);Assert.That(fx.GetComponentsInChildren<ParticleSystem>().All(p=>p.main.maxParticles<=128));}
        Assert.That(player.transform.position,Is.EqualTo(root));Assert.That(Physics.gravity,Is.EqualTo(gravity));Assert.That(Time.timeScale,Is.EqualTo(timeScale));
        spawner.enabled=false;yield return null;Assert.That(Fx().Length,Is.Zero);Object.Destroy(clip);
    }
    [UnityTest]public IEnumerator PlacedTitansConsumeCapAndAreNeverCleanedUpByThisSpawner()
    {
        object[] args={Prefab,Vector3.zero,Quaternion.identity,1f,null,null};Assert.That((bool)Call(manager,"TrySpawn",args),Is.True,(string)args[5]);var placed=(ModularTitanBody)args[4];
        Set(spawner,"maximumAlive",1);string error;Assert.That(Queue(out error),Is.False);StringAssert.Contains("cap",error);
        placed.Die();Assert.That(Queue(out error),Is.True,error);Call(spawner,"DespawnOwned");yield return null;Assert.That(placed!=null);Assert.That(placed.IsDead);
    }
    [UnityTest]public IEnumerator DeadOwnedPopulationIsReplacedAndOldOwnedCorpseIsRemoved()
    {
        string error;Set(spawner,"maximumAlive",1);Set(spawner,"minimumSpawnInterval",.1f);Set(spawner,"maximumSpawnInterval",.1f);Set(spawner,"corpseLifetime",.05f);
        Assert.That(Queue(out error),Is.True,error);var body=Pending;yield return new WaitForSeconds(.08f);body.Die();Set(spawner,"automaticSpawning",true);
        yield return new WaitForSeconds(.5f);Assert.That(body==null);Assert.That(Prop<int>(spawner,"SpawnedCount"),Is.GreaterThanOrEqualTo(2));Assert.That(Prop<int>(spawner,"OwnedAliveCount"),Is.EqualTo(1));
    }
    [UnityTest]public IEnumerator DisabledSpawnerCancelsOnlyItsWarningAndDoesNotLeaveInactiveReservations()
    {
        Effects();Set(spawner,"warningDuration",1f);string error;Assert.That(Queue(out error),Is.True,error);var body=Pending;
        spawner.enabled=false;yield return null;Assert.That(body==null);Assert.That(Prop<int>(manager,"PreparedCount"),Is.Zero);Assert.That(Fx().Length,Is.Zero);
        spawner.enabled=true;Assert.That(Queue(out error),Is.True,error);Call(spawner,"CancelPending");
    }
    [UnityTest]public IEnumerator UnsupportedWeightedSizesFailClearlyButAreIncludedInNextEditorBakeRequest()
    {
        Choices(new[]{2f});object[] args={null};Assert.That((bool)Call(spawner,"ValidateConfiguration",args),Is.False);StringAssert.Contains("profile",(string)args[0]);
        object[] bake={null,null};Assert.That((bool)Call(manager,"TryGetBakeScales",bake),Is.True);CollectionAssert.Contains((float[])bake[0],2f);Assert.That(nav.ForScale(2),Is.Null);
        Choices(new[]{3f},new[]{0f});args=new object[]{null};Assert.That((bool)Call(spawner,"ValidateConfiguration",args),Is.False);yield return null;
    }
    [UnityTest]public IEnumerator ImpossibleBoundsUseBoundedAttemptsWithoutClonesEffectsOrHiddenBake()
    {
        Set(spawner,"candidateAttempts",8);Set(spawner,"useCustomBounds",true);Set(spawner,"spawnBounds",new Bounds(Vector3.one*10000,Vector3.one));int triangles=NavMesh.CalculateTriangulation().indices.Length;
        string error;Assert.That(Queue(out error),Is.False);StringAssert.Contains("8 attempts",error);Assert.That(Prop<int>(manager,"RegisteredCount"),Is.Zero);Assert.That(Fx().Length,Is.Zero);Assert.That(NavMesh.CalculateTriangulation().indices.Length,Is.EqualTo(triangles));yield return null;
    }
    [UnityTest]public IEnumerator PreparedInactiveRigReservesItsActualArmBoundsAgainstOtherWarnings()
    {
        string error;Set(spawner,"warningDuration",1f);Assert.That(Queue(out error),Is.True,error);var body=Pending;
        object[] args={Prefab,body.transform.position,body.transform.rotation,1f,null,null};Assert.That((bool)Call(manager,"TryPrepareSpawn",args),Is.False);StringAssert.Contains("reserved",(string)args[5]);
        Assert.That(Prop<int>(manager,"PreparedCount"),Is.EqualTo(1));Assert.That(Prop<int>(manager,"RegisteredCount"),Is.EqualTo(1));Call(spawner,"CancelPending");yield return null;
    }
    [UnityTest]public IEnumerator StrikeMaterialsCompileAndCanRenderInOwnedGraphicsPreview()
    {
        var lightning=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSpawnLightning.mat");var particles=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSwordImpact.mat");
        Assert.That(lightning,Is.Not.Null);Assert.That(lightning.shader.name,Is.EqualTo("ODM/Titan/SpawnLightning"));Assert.That(ShaderUtil.ShaderHasError(lightning.shader),Is.False);
        string folder=Environment.GetEnvironmentVariable("ODM_TITAN_STRIKE_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"),"Graphics capture must run only in the owned disposable project");
        Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        var old=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var renderer=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");Assert.That(renderer,Is.Not.Null);
        var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var rt=new RenderTexture(640,640,24);var texture=new Texture2D(640,640,TextureFormat.RGB24,false);var camera=new GameObject("Owned strike preview camera").AddComponent<Camera>();
        var light=new GameObject("Owned preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(40,-30,0);
        var go=new GameObject("Owned strike preview");var fx=go.AddComponent(Real("TitanSpawnStrikeEffect"));Call(fx,"Configure",lightning,particles);
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            camera.transform.position=new Vector3(24,15,-32);camera.transform.LookAt(Vector3.up*9);camera.fieldOfView=48;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.075f);camera.targetTexture=rt;camera.farClipPlane=150;
            object[] spawn={Prefab,Vector3.zero,Quaternion.identity,1f,null,null};Assert.That((bool)Call(manager,"TryPrepareSpawn",spawn),Is.True,(string)spawn[5]);
            Call(fx,"Warn",Vector3.zero,1f,123);yield return null;yield return null;yield return null;
            SavePreview(rt,texture,System.IO.Path.Combine(folder,"TitanSpawn_Warning.png"));
            object[] activate={spawn[4],null};Assert.That((bool)Call(manager,"TryActivatePreparedSpawn",activate),Is.True,(string)activate[1]);
            Call(fx,"Strike",null,.7f,600f,true);yield return null;
            SavePreview(rt,texture,System.IO.Path.Combine(folder,"TitanSpawn_Strike.png"));
            Assert.That(texture.GetPixels32().Skip(640*416).Count(c=>c.r>180&&c.g>120),Is.GreaterThan(20),"Sky above the Titan must contain visible strike lightning, not merely a lit body");
            yield return new WaitForSeconds(.35f);SavePreview(rt,texture,System.IO.Path.Combine(folder,"TitanSpawn_Steam.png"));
        }
        finally
        {
            camera.targetTexture=null;UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=old;QualitySettings.renderPipeline=oldQuality;
            Object.Destroy(pipeline);Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);Object.Destroy(go);Object.Destroy(rt);Object.Destroy(texture);
        }
    }
    static void SavePreview(RenderTexture rt,Texture2D texture,string path)
    {
        var old=RenderTexture.active;RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();RenderTexture.active=old;System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
    }
}
#endif
