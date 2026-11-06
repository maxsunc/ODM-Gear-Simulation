#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class TitanStompDebrisPlayModeTests
{
    Scene scene,previous;ModularTitanBody body;TitanStompAttack stomp;TitanCombatTarget victim;Component fx;
    static Type Real(string name){return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);}
    static T Prop<T>(object o,string name){return (T)o.GetType().GetProperty(name).GetValue(o);}
    [UnitySetUp]public IEnumerator Setup()
    {
        previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Owned stomp debris",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Debris floor";floor.transform.position=Vector3.down*.25f;floor.transform.localScale=new Vector3(200,.5f,200);
        body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();body.GetComponent<TitanMotionStyle>().automaticTick=false;
        stomp=body.gameObject.AddComponent<TitanStompAttack>();stomp.automaticTick=false;
        fx=body.gameObject.AddComponent(Real("TitanStompDebris"));fx.GetType().GetMethod("ResetVariation").Invoke(fx,new object[]{412});
        var go=new GameObject("Debris victim");go.transform.position=new Vector3(.7f,0,5.7f);go.AddComponent<BoxCollider>().center=Vector3.up;victim=go.AddComponent<TitanCombatTarget>();victim.maxHealth=10000;victim.ResetHealth();
        SceneManager.SetActiveScene(previous);yield return null;Physics.SyncTransforms();
    }
    [UnityTearDown]public IEnumerator Cleanup(){SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);}
    GameObject Pool(){return scene.GetRootGameObjects().Single(g=>g.name=="[Titan FX] Stomp debris pool");}
    void Impact(){Assert.That(stomp.TryBeginStomp(victim,true),Is.True);stomp.TickAttack(.81f);}
    [Test]public void DebrisAppearsOnceAtActualImpactNeverAtWindupOrCancellationAndLeavesGameplayRandomAlone()
    {
        var state=UnityEngine.Random.state;Assert.That(stomp.TryBeginStomp(victim,true),Is.True);stomp.TickAttack(.6f);Assert.That(Prop<int>(fx,"BurstCount"),Is.Zero);
        stomp.TickAttack(.21f);Assert.That(Prop<int>(fx,"BurstCount"),Is.EqualTo(1));Assert.That(Prop<Vector3>(fx,"LastPoint"),Is.EqualTo(stomp.LockedPoint));Assert.That(Prop<int>(fx,"LastParticleCount"),Is.InRange(56,102));
        Assert.That(Pool().GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),Is.GreaterThan(20));stomp.TickAttack(5);stomp.TickAttack(3);
        Assert.That(stomp.TryBeginStomp(victim,false),Is.True);stomp.TickAttack(.3f);body.StunEyes();stomp.TickAttack(10);
        Assert.That(Prop<int>(fx,"BurstCount"),Is.EqualTo(1));Assert.That(UnityEngine.Random.state,Is.EqualTo(state));
    }
    [Test]public void SharedDebrisPoolStaysBoundedAndHasNoGameplayPhysicsOrAttackRingObjects()
    {
        for(int i=0;i<12;i++){Impact();stomp.TickAttack(2);stomp.TickAttack(3);}
        var pool=Pool();Assert.That(pool.GetComponentsInChildren<ParticleSystem>().Length,Is.EqualTo(8));Assert.That(pool.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),Is.LessThanOrEqualTo(448));
        Assert.That(pool.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(pool.GetComponentsInChildren<Rigidbody>(),Is.Empty);Assert.That(pool.GetComponentsInChildren<LineRenderer>(),Is.Empty);Assert.That(pool.GetComponentsInChildren<Light>(),Is.Empty);
        Assert.That(pool.transform.parent,Is.Null);Assert.That(pool.scene,Is.EqualTo(scene));
        foreach(var p in pool.GetComponentsInChildren<ParticleSystem>()){Assert.That(p.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));Assert.That(p.main.useUnscaledTime,Is.False);Assert.That(p.collision.enabled,Is.False);}
        Assert.That(pool.GetComponentsInChildren<ParticleSystemRenderer>().Count(r=>r.renderMode==ParticleSystemRenderMode.Mesh),Is.EqualTo(4));
    }
    [UnityTest]public IEnumerator DebrisFinishesIndependentlyIfAttackerIsDestroyedAndSceneCleanupOwnsThePool()
    {
        Impact();var pool=Pool();Object.Destroy(body.gameObject);yield return null;Assert.That(pool,Is.Not.Null);
        foreach(var p in pool.GetComponentsInChildren<ParticleSystem>())p.Simulate(3,false,false);Assert.That(pool.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount),Is.Zero);
    }
    [UnityTest]public IEnumerator DebrisRendersRealDustAndFacetedFragmentsInOwnedNativeGraphics()
    {
        string folder=Environment.GetEnvironmentVariable("ODM_STOMP_DEBRIS_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"));Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        var oldPipeline=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;float oldTime=Time.timeScale;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var renderer=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var camera=new GameObject("Owned debris camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.gameObject.AddComponent<AudioListener>();
        var light=new GameObject("Owned debris sun").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(45,-35,0);
        var ground=new Material(Resources.Load<Material>("TitanStompChunks"));ground.SetColor("_Color",new Color(.26f,.23f,.19f));scene.GetRootGameObjects().Single(g=>g.name=="Debris floor").GetComponent<Renderer>().sharedMaterial=ground;
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;body.transform.localScale=Vector3.one*3;victim.transform.position=new Vector3(2.1f,0,17.1f);Physics.SyncTransforms();
            Assert.That(stomp.TryBeginStomp(victim,true),Is.True);for(int i=0;i<97;i++){body.animator.Update(0);stomp.TickAttack(1f/120);stomp.ApplyPose();}Time.timeScale=0;
            var point=stomp.LockedPoint;camera.transform.position=point+new Vector3(12,9,14);camera.transform.LookAt(point+Vector3.up*1.2f);camera.fieldOfView=55;
            var renderers=Pool().GetComponentsInChildren<ParticleSystemRenderer>();foreach(var r in renderers){Assert.That(r.sharedMaterial.shader.isSupported,Is.True);r.enabled=false;}
            yield return null;yield return null;yield return Capture(folder+"/Stomp_NoDebris.png");foreach(var r in renderers)r.enabled=true;
            foreach(var p in Pool().GetComponentsInChildren<ParticleSystem>())p.Simulate(.12f,false,false);
            yield return null;yield return null;yield return Capture(folder+"/Stomp_DebrisBurst.png");
            var before=new Texture2D(2,2);var after=new Texture2D(2,2);before.LoadImage(System.IO.File.ReadAllBytes(folder+"/Stomp_NoDebris.png"));after.LoadImage(System.IO.File.ReadAllBytes(folder+"/Stomp_DebrisBurst.png"));
            var a=before.GetPixels32();var b=after.GetPixels32();Assert.That(a.Count(c=>c.r>220&&c.b>220&&c.g<30),Is.LessThan(100),"No missing-shader magenta in actual native capture");Assert.That(a.Count(c=>c.r<30&&c.b>220&&c.g>220),Is.LessThan(100),"No asynchronously-compiling cyan placeholder in actual native capture");int changed=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>25)changed++;
            Object.Destroy(before);Object.Destroy(after);Assert.That(changed,Is.GreaterThan(250),"Actual particle render must visibly change the SAME impact pose, not just a burst counter");
            foreach(var p in Pool().GetComponentsInChildren<ParticleSystem>())p.Simulate(.35f,false,false);yield return null;yield return Capture(folder+"/Stomp_DebrisSpread.png");
            Debug.Log("STOMP_DEBRIS_NATIVE changedPixels="+changed+" device="+SystemInfo.graphicsDeviceName+" "+SystemInfo.graphicsDeviceType);
        }
        finally{Time.timeScale=oldTime;UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=oldPipeline;QualitySettings.renderPipeline=oldQuality;Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);Object.Destroy(pipeline);Object.Destroy(ground);}
    }
    static IEnumerator Capture(string path)
    {
        if(System.IO.File.Exists(path))System.IO.File.Delete(path);ScreenCapture.CaptureScreenshot(path);for(int i=0;i<120&&!System.IO.File.Exists(path);i++)yield return null;Assert.That(System.IO.File.Exists(path),Is.True);
    }
}
#endif
