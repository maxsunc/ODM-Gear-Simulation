#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class ScoutCapeClothPlayModeTests
{
    Scene scene,previous;
    GameObject scout;
    MonoBehaviour cloth;
    static Type Real(string n){return AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Assembly-CSharp").GetType(n,true);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);}
    static T Prop<T>(object o,string n){return (T)o.GetType().GetProperty(n).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n).SetValue(o,v);}
    Vector3 Point(int i){return (Vector3)Call(cloth,"GetParticle",i);}
    [UnitySetUp] public IEnumerator Setup()
    {
        previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Owned Scout fabric",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        scout=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));scout.GetComponent<Animator>().enabled=false;scout.transform.localScale=Vector3.one*2;
        cloth=(MonoBehaviour)scout.AddComponent(Real("ScoutCapeCloth"));Assert.That(Call(cloth,"Initialize",scout.transform),Is.True);yield return null;
    }
    [UnityTearDown] public IEnumerator Teardown(){SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);}
    void Steps(int n,float dt=1f/60){for(int i=0;i<n;i++)Call(cloth,"Simulate",dt);}
    void PinsAreExact()
    {for(int i=0;i<13;i++)Assert.That(Vector3.Distance(Point(i),(Vector3)Call(cloth,"GetPinTarget",i)),Is.LessThan(.0005f));}
    [Test] public void LongerFabricUsesApprovedAtlasAndDoesNotAlterSkeletonPhysicsOrGameplayRoot()
    {
        Assert.That(Prop<float>(cloth,"SourceLength"),Is.EqualTo(.98f));Assert.That(.98f/.62f,Is.GreaterThan(1.5f));
        var mesh=Prop<Mesh>(cloth,"FabricMesh");Assert.That(mesh.vertexCount,Is.EqualTo(586));Assert.That(mesh.triangles.Length/3,Is.EqualTo(1000));
        var old=scout.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="ScoutCape");Assert.That(old.enabled,Is.False);
        Assert.That(Prop<Renderer>(cloth,"FabricRenderer").sharedMaterial,Is.SameAs(old.sharedMaterial));
        Assert.That(scout.GetComponentsInChildren<Rigidbody>().Length,Is.Zero);Assert.That(scout.GetComponentsInChildren<Collider>().Length,Is.Zero);
        Assert.That(scout.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="ScoutBody").bones.Length,Is.EqualTo(43));
        var position=scout.transform.position;var rotation=scout.transform.rotation;Steps(240);PinsAreExact();
        Assert.That(scout.transform.position,Is.EqualTo(position));Assert.That(scout.transform.rotation,Is.EqualTo(rotation));
        cloth.enabled=false;Assert.That(old.enabled,Is.True);Assert.That(Prop<Renderer>(cloth,"FabricRenderer").enabled,Is.False);
    }
    [Test] public void ActualFreeVerticesRespondToWindAndRemainConstrainedRatherThanRotatingTwoBones()
    {
        Set(cloth,"ambientWind",Vector3.zero);Set(cloth,"gustStrength",0f);Steps(180);
        var still=Enumerable.Range(0,273).Select(Point).ToArray();var bones=scout.GetComponentsInChildren<Transform>().Where(t=>t.name=="CapeUpper"||t.name=="CapeLower").ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
        Set(cloth,"ambientWind",new Vector3(8,1,-5));Set(cloth,"gustStrength",3f);Steps(180);
        float motion=Enumerable.Range(13,260).Max(i=>Vector3.Distance(still[i],Point(i)));Assert.That(motion,Is.GreaterThan(.12f));PinsAreExact();
        for(int i=0;i<bones.Length;i++)Assert.That(bones[i].localRotation,Is.EqualTo(rotations[i]),"Fabric dynamics must not become a second animation writer");
        Assert.That((float)Call(cloth,"MaximumStructuralStretch"),Is.LessThan(1.2f));
        var mesh=Prop<Mesh>(cloth,"FabricMesh");Assert.That(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)),Is.True);
    }
    [Test] public void EightyMetrePerSecondFlightTurnsAndHitchesKeepPinsFiniteAndCannotMovePlayer()
    {
        for(int i=0;i<240;i++)
        {
            scout.transform.position+=new Vector3(0,i<120?0:.15f,80f/60);
            scout.transform.rotation=Quaternion.Euler(i<120?0:-55,i*.75f,Mathf.Sin(i*.04f)*30);
            var position=scout.transform.position;var rotation=scout.transform.rotation;Call(cloth,"Simulate",1f/60);PinsAreExact();
            Assert.That(scout.transform.position,Is.EqualTo(position));Assert.That(scout.transform.rotation,Is.EqualTo(rotation));
            Assert.That((float)Call(cloth,"MaximumStructuralStretch"),Is.LessThan(1.2f),"Flight stress frame "+i);
        }
        int reset=Prop<int>(cloth,"ResetCount");scout.transform.position+=Vector3.right*100;Call(cloth,"Simulate",1f/60);Assert.That(Prop<int>(cloth,"ResetCount"),Is.GreaterThan(reset));PinsAreExact();
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-1f,.5f}){Call(cloth,"Simulate",bad);PinsAreExact();}
        Assert.That(Enumerable.Range(0,273).All(i=>Vector3.Distance(Point(i),Point(i%13))<2.2f),Is.True,"No unbounded cloth explosion after a reset/hitch");
    }
    [Test] public void BodyAndGearProxiesKeepFabricOutOfTorsoWithoutNativeColliders()
    {
        Set(cloth,"ambientWind",new Vector3(0,0,15));Set(cloth,"gustStrength",2f);Steps(300);PinsAreExact();
        var named=scout.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);Vector3 a=named["Hips"].position,b=named["Chest"].position,axis=b-a;
        for(int i=13;i<273;i++)
        {
            Vector3 p=Point(i),nearest=a+axis*Mathf.Clamp01(Vector3.Dot(p-a,axis)/axis.sqrMagnitude);
            Vector3 d=p-nearest;float normalized=Mathf.Sqrt(d.x*d.x/(.19f*2*.19f*2)+(d.y*d.y+d.z*d.z)/(.14f*2*.14f*2));
            Assert.That(normalized,Is.GreaterThanOrEqualTo(.999f),"Cloth stays outside the jacket's broad/shallow anatomical cross-section, not a falsely inflated circular torso");
        }
        Assert.That(scout.GetComponentsInChildren<Collider>().Length,Is.Zero);
    }
    [UnityTest] public IEnumerator NativeGraphicsShowLongerSimulatedCapeWithOriginalScoutArt()
    {
        string folder=Environment.GetEnvironmentVariable("ODM_SCOUT_COMBAT_UX_PREVIEW");
        if(string.IsNullOrEmpty(folder))Assert.Ignore("Native capture is explicitly opt-in in the owned worker");
        Assert.That(System.IO.File.Exists(".titan-validation-owned"),Is.True);Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
        System.IO.Directory.CreateDirectory(folder);Set(cloth,"automaticSimulation",false);
        var old=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var rendererData=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
        var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{rendererData});
        var rt=new RenderTexture(1000,1000,24);var texture=new Texture2D(1000,1000,TextureFormat.RGB24,false);
        var camera=new GameObject("Owned cloth camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.065f,.08f);camera.fieldOfView=45;camera.targetTexture=rt;
        var light=new GameObject("Owned cloth light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.65f;light.transform.rotation=Quaternion.Euler(30,140,0);
        var fill=new GameObject("Owned cloth fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.transform.rotation=Quaternion.Euler(15,-40,0);
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/Scout/Animations/Idle.anim").SampleAnimation(scout,0);
            Call(cloth,"ResetSimulation");Steps(120);camera.transform.position=new Vector3(-3.1f,2.5f,-5.8f);camera.transform.LookAt(new Vector3(0,1.9f,0));
            for(int i=0;i<5;i++)yield return null;
            Save(rt,texture,System.IO.Path.Combine(folder,"LongCape_Idle.png"));
            Assert.That(texture.GetPixels32().Count(c=>c.g>35 && c.g>c.r*1.12f && c.g>c.b*1.12f),Is.GreaterThan(200),"Actual green cape pixels, not an empty render");
            Assert.That(texture.GetPixels32().Count(c=>c.r>180 && c.b>180 && c.g<60),Is.LessThan(50),"No missing-shader magenta");
            Set(cloth,"ambientWind",new Vector3(8,.5f,-5));Set(cloth,"gustStrength",3f);
            for(int i=0;i<24;i++)
            {
                Steps(3);yield return null;Save(rt,texture,System.IO.Path.Combine(folder,"CapeWind_"+i.ToString("D2")+".png"));
            }
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/Scout/Animations/GasBoth.anim").SampleAnimation(scout,.3f);
            Call(cloth,"ResetSimulation");Set(cloth,"ambientWind",new Vector3(2,.2f,-2));
            Vector3 offset=new Vector3(-4.8f,3.4f,-7.5f);camera.fieldOfView=50;
            for(int i=0;i<120;i++)
            {
                scout.transform.position+=Vector3.forward*(80f/60);Call(cloth,"Simulate",1f/60);
            }
            camera.transform.position=scout.transform.position+offset;camera.transform.LookAt(scout.transform.position+new Vector3(0,1.9f,-.6f));
            yield return null;yield return null;Save(rt,texture,System.IO.Path.Combine(folder,"LongCape_Flight80.png"));
            Debug.Log("SCOUT_CLOTH_NATIVE "+SystemInfo.graphicsDeviceName+" vertices="+Prop<Mesh>(cloth,"FabricMesh").vertexCount+" maximumStretch="+Call(cloth,"MaximumStructuralStretch"));
        }
        finally
        {
            camera.targetTexture=null;UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=old;QualitySettings.renderPipeline=oldQuality;
            Object.Destroy(pipeline);Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);Object.Destroy(fill.gameObject);Object.Destroy(rt);Object.Destroy(texture);
        }
    }
    static void Save(RenderTexture rt,Texture2D texture,string path)
    {
        var old=RenderTexture.active;try{RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());}finally{RenderTexture.active=old;}
    }
    [UnityTest] public IEnumerator CorpseCapeSnapshotOwnsItsMeshAndSurvivesLiveClothDestruction()
    {
        Steps(60);var root=new GameObject("Independent cloth snapshot corpse");var copy=Object.Instantiate(scout,root.transform);var copiedCloth=copy.GetComponent(Real("ScoutCapeCloth"));if(copiedCloth!=null)Object.DestroyImmediate(copiedCloth);
        // Clone the passive visual only, as the real ragdoll does. Our fixture's
        // cloth lives on that same visual root, unlike the actual player's root.
        var method=Real("ScoutCapeCloth").GetMethod("PreserveCorpseMesh");method.Invoke(null,new object[]{copy,root});
        var mesh=copy.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Scout simulated fabric").sharedMesh;
        Assert.That(mesh,Is.Not.SameAs(Prop<Mesh>(cloth,"FabricMesh")));var before=mesh.vertices;
        Object.Destroy(scout);yield return null;Assert.That(mesh,Is.Not.Null);Assert.That(mesh.vertices,Is.EqualTo(before));
        Object.Destroy(root);yield return null;Assert.That(mesh==null,Is.True,"Independent snapshot mesh must be reclaimed with its corpse");
    }
}
#endif
