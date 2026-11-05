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

public class TitanAudioPlayModeTests
{
    Scene scene,previous;ModularTitanBody body;TitanMotionStyle motion;Component emitter;
    static Type Real(string n){return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).First(t=>t!=null);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n).Invoke(o,args);}
    static T Prop<T>(object o,string n){return (T)o.GetType().GetProperty(n).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n).SetValue(o,v);}
    [UnitySetUp]public IEnumerator Setup()
    {
        previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Owned Titan sound test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        var floor=new GameObject("Real support");floor.transform.position=Vector3.down*.5f;floor.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
        body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
        motion=body.GetComponent<TitanMotionStyle>();motion.automaticTick=false;
        emitter=body.gameObject.AddComponent(Real("TitanAudioEmitter"));Set(emitter,"automaticSampling",false);Call(emitter,"ResetVariation",431);
        SceneManager.SetActiveScene(previous);Physics.SyncTransforms();yield return null;
    }
    [UnityTearDown]public IEnumerator Cleanup(){SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);}
    void Pose(float gait,float phase)
    {
        body.SetWalking(true);motion.TickMotion(.02f,4.6f,false,0);body.animator.SetFloat("Gait",gait);
        body.animator.Play("Locomotion",0,phase);body.animator.Update(0);Call(emitter,"SampleFeet",1f/120);
    }
    [Test]public void PaletteReferencesExactSuppliedFilesAndAllNewActorSourcesUseSfxWithoutPhysics()
    {
        var p=Resources.Load<ScriptableObject>("TitanAudioPalette");Assert.That(p,Is.Not.Null);
        var screams=(AudioClip[])p.GetType().GetField("blindScreams").GetValue(p);var steps=(AudioClip[])p.GetType().GetField("footsteps").GetValue(p);
        Assert.That(screams.Select(AssetDatabase.GetAssetPath),Is.EquivalentTo(new[]{"Assets/Audio/titanScream.wav","Assets/Audio/titanScream2.mp3"}));
        Assert.That(steps.Select(AssetDatabase.GetAssetPath),Is.EquivalentTo(new[]{"Assets/Audio/titanFootstep1.mp3","Assets/Audio/titanFootstep2.mp3","Assets/Audio/titanFootstep3.mp3"}));
        Assert.That(AssetDatabase.GetAssetPath((Object)p.GetType().GetField("spawnLightning").GetValue(p)),Is.EqualTo("Assets/Audio/titanLightning.mp3"));
        Assert.That(AssetDatabase.GetAssetPath((Object)p.GetType().GetField("eaten").GetValue(p)),Is.EqualTo("Assets/Audio/bloodSplat.mp3"));
        var sources=body.GetComponentsInChildren<AudioSource>();Assert.That(sources.Length,Is.EqualTo(3));
        foreach(var s in sources){Assert.That(s.outputAudioMixerGroup.name,Is.EqualTo("SFX"));Assert.That(s.spatialBlend,Is.EqualTo(1));Assert.That(s.playOnAwake,Is.False);Assert.That(s.GetComponent<Collider>(),Is.Null);Assert.That(s.GetComponent<Rigidbody>(),Is.Null);}
    }
    [Test]public void BlindScreamsOnlyOnNewBlindnessRandomizeBothClipsAndLeaveGameplayRandomUntouched()
    {
        body.useRegionalHealth=true;Call(emitter,"ResetVariation",431);var global=UnityEngine.Random.state;
        body.ReceiveDamage(TitanHitRegion.Eyes,100,body.head.position,Vector3.zero);Assert.That(Prop<int>(emitter,"ScreamCount"),Is.Zero);
        body.ReceiveDamage(TitanHitRegion.Eyes,500,body.head.position,Vector3.zero);Assert.That(Prop<int>(emitter,"ScreamCount"),Is.EqualTo(1));
        body.ReceiveDamage(TitanHitRegion.Eyes,1000,body.head.position,Vector3.zero);body.StunEyes();Assert.That(Prop<int>(emitter,"ScreamCount"),Is.EqualTo(1));
        var seen=new System.Collections.Generic.HashSet<AudioClip>{Prop<AudioClip>(emitter,"LastScream")};
        for(int i=0;i<30;i++){body.TickStatus(body.eyeStunDuration+.01f);body.StunEyes();seen.Add(Prop<AudioClip>(emitter,"LastScream"));}
        Assert.That(seen.Count,Is.EqualTo(2));Assert.That(Prop<int>(emitter,"ScreamCount"),Is.EqualTo(31));Assert.That(UnityEngine.Random.state,Is.EqualTo(global));
        ((Behaviour)emitter).enabled=false;body.TickStatus(body.eyeStunDuration+.01f);body.StunEyes();Assert.That(Prop<int>(emitter,"ScreamCount"),Is.EqualTo(31));
    }
    [TestCase(0f,.13f,1f)] [TestCase(1f,.71f,1f)] [TestCase(0f,.89f,3f)] [TestCase(1f,.31f,5f)]
    public void RealWalkAndRunSolesPlayOncePerLandingAndFollowCycleOffset(float gait,float offset,float size)
    {
        body.transform.localScale=Vector3.one*size;
        body.animator.SetFloat("CycleOffset",offset);var global=UnityEngine.Random.state;var seen=new System.Collections.Generic.HashSet<AudioClip>();
        int previousCount=0;
        for(int cycle=0;cycle<5;cycle++)
        {
            for(int i=0;i<120;i++){Pose(gait,cycle+i/120f);var clip=Prop<AudioClip>(emitter,"LastFootstep");if(clip!=null)seen.Add(clip);}
            int count=Prop<int>(emitter,"FootstepCount");
            if(cycle>0)Assert.That(count-previousCount,Is.EqualTo(2),"Exactly two planted soles per full gait cycle, not a timer or per-frame playback");previousCount=count;
        }
        Assert.That(seen.Count,Is.GreaterThanOrEqualTo(2));Assert.That(UnityEngine.Random.state,Is.EqualTo(global));
        int before=Prop<int>(emitter,"FootstepCount");for(int i=0;i<20;i++)Call(emitter,"SampleFeet",1f/120);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.EqualTo(before));
    }
    [Test]public void IdleBlindLegLossHitchesAndMissingGroundCannotManufactureFootsteps()
    {
        for(int i=0;i<120;i++)Call(emitter,"SampleFeet",1f/120);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.Zero);
        body.transform.position=Vector3.up*100;for(int i=0;i<240;i++)Pose(1,i/120f);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.Zero);
        body.transform.position=Vector3.zero;Call(emitter,"SampleFeet",.3f);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.Zero);
        body.StunEyes();for(int i=0;i<240;i++)Pose(0,i/120f);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.Zero);
        body.TickStatus(body.eyeStunDuration+.01f);body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);
        for(int i=0;i<240;i++)Pose(0,i/120f);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.Zero);
    }
    [UnityTest]public IEnumerator EatenCueRequiresRealLethalBiteDamageDeduplicatesAndDoesNotFollowReleasedPlayer()
    {
        var go=new GameObject("Real player health");SceneManager.MoveGameObjectToScene(go,scene);
        var health=(MonoBehaviour)go.AddComponent(Real("PlayerHealth"));var source=(TitanHealthSource)go.AddComponent(Real("TitanODMHealthSource"));yield return null;
        var impact=new TitanBiteImpact{Attacker=body.transform,Point=new Vector3(2,8,3),Damage=100,Lethal=true};
        source.CompleteBite(impact,false);Assert.That(Prop<int>(source,"EatenSoundCount"),Is.Zero);
        impact.Lethal=false;source.PrepareBite(impact);source.ApplyDamage(1);source.CompleteBite(impact,true);Assert.That(Prop<int>(source,"EatenSoundCount"),Is.Zero);
        impact.Lethal=true;source.PrepareBite(impact);source.ApplyDamage(100);health.CancelInvoke();source.CompleteBite(impact,true);source.CompleteBite(impact,true);
        Assert.That(Prop<int>(source,"EatenSoundCount"),Is.EqualTo(1));
        var cue=scene.GetRootGameObjects().Single(o=>o.name=="[Titan SFX] Eaten").GetComponent<AudioSource>();
        Assert.That(AssetDatabase.GetAssetPath(cue.clip),Is.EqualTo("Assets/Audio/bloodSplat.mp3"));Assert.That(cue.outputAudioMixerGroup.name,Is.EqualTo("SFX"));Assert.That(cue.spatialBlend,Is.Zero);
        go.transform.position+=Vector3.right*50;Assert.That(cue.transform.position,Is.EqualTo(impact.Point));Assert.That(cue.transform.parent,Is.Null);
    }
}
#endif
