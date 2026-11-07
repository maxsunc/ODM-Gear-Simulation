#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class TitanCorpseDecompositionPlayModeTests
{
    Scene scene,previous;ModularTitanBody body;float oldTime;
    [UnitySetUp]public IEnumerator Setup()
    {
        oldTime=Time.timeScale;previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Owned decomposition",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Decomposition floor";floor.transform.position=Vector3.down*.25f;floor.transform.localScale=new Vector3(500,.5f,500);
        body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();body.GetComponent<TitanMotionStyle>().automaticTick=false;
        SceneManager.SetActiveScene(previous);yield return null;Physics.SyncTransforms();
    }
    [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=oldTime;SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);}
    TitanCorpseDecomposition Kill(){body.Die();var fx=body.GetComponent<TitanCorpseDecomposition>();Assert.That(fx,Is.Not.Null);fx.enabled=false;return fx;}
    Renderer Skin {get{return body.GetComponentsInChildren<SkinnedMeshRenderer>().First();}}
    [Test]public void OnlyRealDeathOrSeverStartsDecompositionAndDuplicateDeathIsIdempotent()
    {
        var shared=Skin.sharedMaterial;Assert.That(TitanCorpseDecomposition.BeginDeath(body),Is.Null);body.StunEyes();Assert.That(body.GetComponent<TitanCorpseDecomposition>(),Is.Null);
        body.Sever(TitanHitRegion.LeftArm,Vector3.zero);Assert.That(body.IsDead,Is.False);Assert.That(body.GetComponent<TitanCorpseDecomposition>(),Is.Null);
        var limb=body.limbs.First(l=>l.region==TitanHitRegion.LeftArm);Assert.That(limb.rootBone.parent.GetComponent<TitanCorpseDecomposition>().IsBranch,Is.True);
        var fx=Kill();body.Die();Assert.That(body.GetComponents<TitanCorpseDecomposition>().Length,Is.EqualTo(1));Assert.That(TitanCorpseDecomposition.BeginDeath(body),Is.SameAs(fx));Assert.That(shared.GetColor("_BaseColor"),Is.EqualTo(Color.white));
    }
    [TestCase(1f)][TestCase(3f)][TestCase(5f)][TestCase(10f)]public void DarkeningAndRealTransparencyAreMonotonicAndKeepOriginalLitTexturesAndPhysics(float scale)
    {
        body.transform.localScale=Vector3.one*scale;Physics.SyncTransforms();var renderer=Skin;var source=renderer.sharedMaterial;var sourceColor=source.GetColor("_BaseColor");var sourceQueue=source.renderQueue;var fx=Kill();
        var clone=renderer.sharedMaterial;Assert.That(clone,Is.Not.SameAs(source));Assert.That(clone.shader,Is.SameAs(source.shader));foreach(string texture in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"})Assert.That(clone.GetTexture(texture),Is.SameAs(source.GetTexture(texture)));
        var bones=body.ragdollBones.Where(b=>b.body!=null).Select(b=>b.body).ToArray();var positions=bones.Select(b=>b.position).ToArray();var velocities=bones.Select(b=>b.velocity).ToArray();var rotations=bones.Select(b=>b.rotation).ToArray();var root=body.transform.position;
        float shade=1,opacity=1;for(int i=0;i<17;i++){fx.TickDecomposition(1);Assert.That(fx.Shade,Is.LessThanOrEqualTo(shade));Assert.That(fx.Opacity,Is.LessThanOrEqualTo(opacity));shade=fx.Shade;opacity=fx.Opacity;}
        Assert.That(shade,Is.EqualTo(.55f).Within(.001f));Assert.That(opacity,Is.LessThan(.02f));Assert.That(clone.GetColor("_BaseColor").a,Is.EqualTo(opacity*sourceColor.a).Within(.001f));Assert.That(clone.GetFloat("_Surface"),Is.EqualTo(1));Assert.That(clone.GetFloat("_ZWrite"),Is.Zero);Assert.That(clone.GetFloat("_SrcBlend"),Is.EqualTo((float)BlendMode.SrcAlpha));Assert.That(clone.GetFloat("_DstBlend"),Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));Assert.That(clone.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),Is.True);Assert.That(clone.GetShaderPassEnabled("ShadowCaster"),Is.False);
        Assert.That(source.GetColor("_BaseColor"),Is.EqualTo(sourceColor));Assert.That(source.renderQueue,Is.EqualTo(sourceQueue));Assert.That(source.GetFloat("_Surface"),Is.Zero);Assert.That(body.transform.position,Is.EqualTo(root));
        for(int i=0;i<bones.Length;i++){Assert.That(bones[i].position,Is.EqualTo(positions[i]));Assert.That(bones[i].rotation,Is.EqualTo(rotations[i]));Assert.That(bones[i].velocity,Is.EqualTo(velocities[i]));Assert.That(bones[i].isKinematic,Is.False);Assert.That(bones[i].detectCollisions,Is.True);Assert.That(bones[i].solverIterations,Is.EqualTo(12));Assert.That(bones[i].solverVelocityIterations,Is.EqualTo(4));Assert.That(bones[i].maxAngularVelocity,Is.EqualTo(4));}
    }
    [Test]public void SteamIsBoundedWorldSpacePrivateRandomAndAddsNoPhysicsOrLights()
    {
        var state=UnityEngine.Random.state;var gravity=Physics.gravity;var fx=Kill();for(int i=0;i<200;i++)fx.TickDecomposition(.05f);
        Assert.That(fx.EmittedCount,Is.GreaterThan(100));Assert.That(fx.Steam.particleCount,Is.LessThanOrEqualTo(128));Assert.That(fx.Steam.main.maxParticles,Is.EqualTo(128));Assert.That(fx.Steam.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));Assert.That(fx.Steam.main.useUnscaledTime,Is.False);Assert.That(fx.Steam.collision.enabled,Is.False);
        var go=fx.Steam.gameObject;Assert.That(go.GetComponentsInChildren<Rigidbody>(),Is.Empty);Assert.That(go.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(go.GetComponentsInChildren<Light>(),Is.Empty);Assert.That(go.GetComponentsInChildren<AudioSource>(),Is.Empty);Assert.That(go.layer,Is.EqualTo(2));Assert.That(go.scene,Is.EqualTo(scene));Assert.That(UnityEngine.Random.state,Is.EqualTo(state));Assert.That(Physics.gravity,Is.EqualTo(gravity));Assert.That(fx.OwnedMaterialCount,Is.InRange(1,15));
    }
    [Test]public void InvalidDeltaAndZeroDeltaCannotAgeOrRepeatEffects()
    {
        var fx=Kill();foreach(float dt in new[]{0f,-1,float.NaN,float.PositiveInfinity,float.NegativeInfinity})fx.TickDecomposition(dt);Assert.That(fx.Age,Is.Zero);Assert.That(fx.EmittedCount,Is.Zero);Assert.That(fx.Opacity,Is.EqualTo(1));
    }
    [UnityTest]public IEnumerator ScaledPauseDoesNotAgeDecomposition()
    {
        var fx=Kill();fx.enabled=true;Time.timeScale=0;float age=fx.Age;yield return new WaitForSecondsRealtime(.12f);Assert.That(fx.Age,Is.EqualTo(age));Time.timeScale=1;
    }
    [Test]public void CurrentPhysicalBonesRatherThanStaleRootEmitSteamAfterRagdollMoves()
    {
        var fx=Kill();fx.TickDecomposition(1);foreach(var bone in body.ragdollBones)if(bone.body!=null)bone.body.position+=Vector3.right*300;scene.GetPhysicsScene().Simulate(1f/120);Assert.That(body.hips.position.x,Is.GreaterThan(250));int before=fx.EmittedCount;
        for(int i=0;i<6;i++)fx.TickDecomposition(.1f);Assert.That(fx.EmittedCount,Is.GreaterThan(before));var particles=new ParticleSystem.Particle[128];int n=fx.Steam.GetParticles(particles);Assert.That(particles.Take(n).Count(p=>p.position.x>250),Is.GreaterThan(5));Assert.That(body.transform.position.x,Is.LessThan(1),"Root deliberately stays behind; bone surface emissions must not");
    }
    [UnityTest]public IEnumerator DeathAndAllPreviouslySeveredBranchesDisappearWithoutLeakingMaterialsOrSteam()
    {
        foreach(var r in new[]{TitanHitRegion.LeftArm,TitanHitRegion.RightArm,TitanHitRegion.LeftLeg,TitanHitRegion.RightLeg})body.Sever(r,Vector3.zero);
        var roots=body.limbs.Select(l=>l.rootBone.parent.gameObject).ToArray();var branches=roots.Select(g=>g.GetComponent<TitanCorpseDecomposition>()).ToArray();foreach(var branch in branches){branch.enabled=false;Assert.That(branch.IsBranch,Is.True);Assert.That(branch.Steam.main.maxParticles,Is.EqualTo(32));}
        var fx=Kill();var materials=body.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Concat(roots.SelectMany(g=>g.GetComponentsInChildren<SkinnedMeshRenderer>()).SelectMany(r=>r.sharedMaterials)).Where(m=>m!=null&&m.hideFlags==HideFlags.DontSave).Distinct().ToArray();
        foreach(var branch in branches)branch.TickDecomposition(18.1f);fx.TickDecomposition(18.1f);yield return null;yield return null;
        Assert.That(body==null,Is.True);Assert.That(roots.All(g=>g==null),Is.True);Assert.That(materials.All(m=>m==null),Is.True);Assert.That(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ParticleSystem>()),Is.Empty);
    }
    [UnityTest]public IEnumerator SurvivorKeepsInjuriesAndCanDieNormallyAfterLooseLimbHasFullyDecomposed()
    {
        body.Sever(TitanHitRegion.RightLeg,Vector3.zero);var limb=body.limbs.First(l=>l.region==TitanHitRegion.RightLeg);var root=limb.rootBone.parent.gameObject;var branch=root.GetComponent<TitanCorpseDecomposition>();branch.TickDecomposition(18.1f);yield return null;yield return null;
        Assert.That(root==null,Is.True);Assert.That(body.IsDead,Is.False);Assert.That(body.IsSevered(TitanHitRegion.RightLeg),Is.True);Assert.That(body.GetHealth(TitanHitRegion.Nape),Is.GreaterThan(0));Assert.That(Kill(),Is.Not.Null);Assert.That(body.IsDead,Is.True);
    }
    [TestCase(0f,18f)][TestCase(30f,18f)][TestCase(4f,4f)]public void DetachedLifetimeCanShortenButNeverDisableOrExtendBoundedBranchDecay(float configured,float expected)
    {
        body.detachedLifetime=configured;body.Sever(TitanHitRegion.LeftArm,Vector3.zero);var limb=body.limbs.First(l=>l.region==TitanHitRegion.LeftArm);Assert.That(limb.rootBone.parent.GetComponent<TitanCorpseDecomposition>().Lifetime,Is.EqualTo(expected));
    }
    [UnityTest]public IEnumerator RemovingObserverRestoresOriginalMaterialReferencesAndDisposesOnlyItsOwnResources()
    {
        var renderer=Skin;var original=renderer.sharedMaterial;var fx=Kill();fx.TickDecomposition(12);var clone=renderer.sharedMaterial;var steam=fx.Steam;Object.Destroy(fx);yield return null;yield return null;Assert.That(renderer.sharedMaterial,Is.SameAs(original));Assert.That(original!=null,Is.True);Assert.That(clone==null,Is.True);Assert.That(steam==null,Is.True);Assert.That(body!=null,Is.True);
    }
    [UnityTest]public IEnumerator CorpseDecompositionRendersDarkSteamAndTrueFadeInOwnedNativeGraphics()
    {
        string folder=Environment.GetEnvironmentVariable("ODM_TITAN_DECAY_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"));Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        var oldPipeline=GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);var renderer=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");var pipeline=(RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var camera=new GameObject("Owned corpse camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.gameObject.AddComponent<AudioListener>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.17f,.20f);camera.fieldOfView=48;
        var sun=new GameObject("Owned corpse sun").AddComponent<Light>();SceneManager.MoveGameObjectToScene(sun.gameObject,scene);sun.type=LightType.Directional;sun.intensity=1.6f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
        try
        {
            GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;scene.GetRootGameObjects().Single(g=>g.name=="Decomposition floor").GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Titans/Prototype/Materials/AI_Lab_Ground.mat");body.transform.localScale=Vector3.one*3;body.animator.Update(0);Physics.SyncTransforms();var fx=Kill();Time.timeScale=0;
            float peak=0;for(int i=0;i<480;i++){scene.GetPhysicsScene().Simulate(1f/120);foreach(var bone in body.ragdollBones)if(bone.body!=null){peak=Mathf.Max(peak,bone.body.velocity.magnitude);Assert.That(bone.body.velocity.magnitude,Is.LessThan(40));}}
            var bounds=new Bounds(body.hips.position,Vector3.zero);foreach(var bone in body.ragdollBones)if(bone.collider!=null)bounds.Encapsulate(bone.collider.bounds);
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(9,bounds.size.magnitude*.58f);camera.transform.position=bounds.center+new Vector3(-30,35,24);camera.transform.LookAt(bounds.center);var steamRenderer=fx.Steam.GetComponent<ParticleSystemRenderer>();steamRenderer.enabled=false;
            yield return null;yield return null;yield return Capture(folder+"/Corpse_Fresh.png");
            fx.TickDecomposition(5);yield return null;yield return Capture(folder+"/Corpse_Dark.png");
            // Emit/simulate observed surface puffs across.8s, same stationary native pose.
            steamRenderer.enabled=true;for(int i=0;i<16;i++){fx.TickDecomposition(.05f);fx.Steam.Simulate(.05f,false,false);}
            yield return null;yield return Capture(folder+"/Corpse_Steam.png");Assert.That(fx.Steam.particleCount,Is.GreaterThan(5));Assert.That(steamRenderer.sharedMaterial.shader.isSupported,Is.True);
            fx.TickDecomposition(6.2f);steamRenderer.enabled=false;yield return null;yield return null;yield return Capture(folder+"/Corpse_Fading.png");Assert.That(fx.Opacity,Is.InRange(.2f,.6f));
            fx.TickDecomposition(6.1f);yield return null;yield return null;yield return Capture(folder+"/Corpse_Gone.png");Assert.That(body==null,Is.True);
            var fresh=Pixels(folder+"/Corpse_Fresh.png");var dark=Pixels(folder+"/Corpse_Dark.png");var steam=Pixels(folder+"/Corpse_Steam.png");var fading=Pixels(folder+"/Corpse_Fading.png");var gone=Pixels(folder+"/Corpse_Gone.png");int darker=0,smoke=0,translucent=0,bodyPixels=0;
            for(int i=0;i<fresh.Length;i++)
            {
                Assert.That(fresh[i].r>230&&fresh[i].b>230&&fresh[i].g<25,Is.False,"No missing-shader magenta");
                if(Difference(dark[i],gone[i])<35)continue;bodyPixels++;if(Luma(dark[i])+10<Luma(fresh[i]))darker++;if(Difference(steam[i],dark[i])>25)smoke++;
                if(Difference(fading[i],gone[i])<Difference(dark[i],gone[i])*.85f&&Difference(fading[i],gone[i])>4)translucent++;
            }
            Assert.That(bodyPixels,Is.GreaterThan(1000));Assert.That(darker,Is.GreaterThan(500));Assert.That(smoke,Is.GreaterThan(100));Assert.That(translucent,Is.GreaterThan(500),"Actual background contribution through same original skin, not just material alpha counter");
            Debug.Log("TITAN_DECOMPOSITION_NATIVE bodyPixels="+bodyPixels+" darker="+darker+" steam="+smoke+" translucent="+translucent+" corpsePeak="+peak+" device="+SystemInfo.graphicsDeviceName);
        }
        finally{GraphicsSettings.renderPipelineAsset=oldPipeline;QualitySettings.renderPipeline=oldQuality;Object.Destroy(camera.gameObject);Object.Destroy(sun.gameObject);Object.Destroy(pipeline);}
    }
    static int Luma(Color32 c){return (c.r+c.g+c.b)/3;}
    static int Difference(Color32 a,Color32 b){return Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);}
    static Color32[] Pixels(string path){var image=new Texture2D(2,2);image.LoadImage(System.IO.File.ReadAllBytes(path));var result=image.GetPixels32();Object.Destroy(image);return result;}
    static IEnumerator Capture(string path){System.IO.File.Delete(path);ScreenCapture.CaptureScreenshot(path);for(int i=0;i<120&&!System.IO.File.Exists(path);i++)yield return null;Assert.That(System.IO.File.Exists(path),Is.True);}
}
#endif
