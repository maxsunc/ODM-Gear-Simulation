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
    void Pose(float gait,float phase,float? offset=null,float dt=1f/120)
    {
        body.SetWalking(true);motion.TickMotion(dt,4.6f,false,0);body.animator.SetFloat("Gait",gait);
        if(offset.HasValue)body.animator.SetFloat("CycleOffset",offset.Value);
        body.animator.Play("Locomotion",0,phase);body.animator.Update(0);Call(emitter,"SampleFeet",dt);
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
            for(int i=0;i<120;i++){Pose(gait,cycle+i/120f,offset);var clip=Prop<AudioClip>(emitter,"LastFootstep");if(clip!=null)seen.Add(clip);}
            int count=Prop<int>(emitter,"FootstepCount");
            if(cycle>0)Assert.That(count-previousCount,Is.EqualTo(2),"Exactly two planted soles per full gait cycle, not a timer or per-frame playback");previousCount=count;
        }
        Assert.That(seen.Count,Is.GreaterThanOrEqualTo(2));Assert.That(UnityEngine.Random.state,Is.EqualTo(global));
        int before=Prop<int>(emitter,"FootstepCount");for(int i=0;i<20;i++)Call(emitter,"SampleFeet",1f/120);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.EqualTo(before));
    }
    static BoxCollider Sole(ModularTitanBody b,int foot)
    {
        var leg=b.limbs.First(l=>l.region==(foot==0?TitanHitRegion.LeftLeg:TitanHitRegion.RightLeg));
        return leg.bodies[2].GetComponentsInChildren<BoxCollider>().First(s=>!s.isTrigger && s.attachedRigidbody==leg.bodies[2]);
    }
    static float SoleBottom(BoxCollider s)
    {
        float y=float.PositiveInfinity;
        for(int x=-1;x<=1;x+=2)for(int a=-1;a<=1;a+=2)for(int z=-1;z<=1;z+=2)
            y=Mathf.Min(y,s.transform.TransformPoint(s.center+Vector3.Scale(s.size*.5f,new Vector3(x,a,z))).y);
        return y;
    }
    [TestCase(0f,0f,1f,0f)] [TestCase(1f,0f,1f,0f)] [TestCase(.5f,.23f,3f,0f)]
    [TestCase(0f,.71f,5f,.25f)] [TestCase(1f,.13f,10f,.25f)]
    [TestCase(.1f,.17f,7f,.4f)] [TestCase(.25f,.37f,3f,.4f)] [TestCase(.75f,.51f,5f,.4f)] [TestCase(.9f,.93f,10f,.4f)]
    public void FootstepLandingPhaseTracksVisiblePlantAtRealGaitOffsetsAndNavHeights(float gait,float offset,float size,float rootHeight)
    {
        body.transform.localScale=Vector3.one*size;body.transform.position=Vector3.up*rootHeight*size;body.animator.SetFloat("CycleOffset",offset);
        var soles=new[]{Sole(body,0),Sole(body,1)};var rows=new System.Text.StringBuilder("phase,left,right,eventFoot\n");int count=0;
        for(int i=0;i<720;i++)
        {
            float phase=i/240f;Pose(gait,phase,offset);int now=Prop<int>(emitter,"FootstepCount");
            if(now>count)Assert.That(SoleBottom(soles[Prop<int>(emitter,"LastFoot")])/size-rootHeight,Is.LessThanOrEqualTo(.0401f),"Sound must coincide with the visible sole plant, not early descent");
            rows.AppendLine(phase+","+(SoleBottom(soles[0])/size)+","+(SoleBottom(soles[1])/size)+","+(now>count?Prop<int>(emitter,"LastFoot"):-1));count=now;
        }
        System.IO.File.WriteAllText("Library/FootstepTiming-"+gait+"-"+offset+"-"+size+"-"+rootHeight+".csv",rows.ToString());
        Assert.That(count,Is.InRange(5,6),"Exactly two observed strikes per full cycle after initial sampling, even above voxelized terrain");
    }
    [TestCase(30,0f,.35f,1f)] [TestCase(30,1f,1.65f,10f)] [TestCase(30,.5f,1.65f,7f)]
    [TestCase(60,0f,1f,3f)] [TestCase(60,1f,1.65f,5f)] [TestCase(120,1f,1.65f,10f)] [TestCase(120,.75f,.6f,7f)]
    public void RealAnimatorLandingsStayInSyncAtDifferentFrameRatesAndCadences(int fps,float gait,float rate,float size)
    {
        body.transform.localScale=Vector3.one*size;body.transform.position=Vector3.up*(size*.25f);body.SetWalking(true);motion.TickMotion(.1f,16,true,0);
        body.animator.SetFloat("Gait",gait);body.animator.SetFloat("StrideRate",rate);body.animator.SetFloat("CycleOffset",.17f);
        body.animator.Play("Locomotion",0,0);body.animator.Update(0);
        var soles=new[]{Sole(body,0),Sole(body,1)};float dt=1f/fps;var last=new float[2];var older=new float[2];int previousCount=0,boundaryCount=0,previousCycle=0;
        for(int frame=0;frame<10000;frame++)
        {
            body.animator.Update(dt);Call(emitter,"SampleFeet",dt);int count=Prop<int>(emitter,"FootstepCount");
            var heights=new[]{SoleBottom(soles[0])/size-.25f,SoleBottom(soles[1])/size-.25f};
            if(count>previousCount)
            {
                int foot=Prop<int>(emitter,"LastFoot");
                Assert.That(heights[foot]<=.0401f || (frame>1 && last[foot]<=.0601f && last[foot]<older[foot] && heights[foot]>last[foot]),Is.True,"Dispatch must be at the visible plant or the first observed frame after its minimum, not early descent");
            }
            older=last;last=heights;previousCount=count;
            int cycle=Mathf.FloorToInt(body.animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            if(cycle!=previousCycle)
            {
                if(previousCycle>=1)Assert.That(count-boundaryCount,Is.EqualTo(2),"No missing/double impacts at real Animator speed and display frame rate");
                boundaryCount=count;previousCycle=cycle;if(cycle>=6)break;
            }
        }
        Assert.That(previousCycle,Is.GreaterThanOrEqualTo(6));
    }
    [Test]public void LostSupportReturningUnderAnAlreadyPlantedFootCannotInventAnotherStrike()
    {
        for(int i=0;i<240;i++)Pose(0,i/240f,0);int count=Prop<int>(emitter,"FootstepCount");
        var support=scene.GetRootGameObjects().Single(g=>g.name=="Real support").GetComponent<BoxCollider>();
        support.enabled=false;Physics.SyncTransforms();Pose(0,1,0);support.enabled=true;Physics.SyncTransforms();
        for(int i=0;i<20;i++)Pose(0,1,0);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.EqualTo(count));
    }
    [Test]public void NearbyButAirborneRootCannotEmitAndAudioObserverCannotAlterAnimationOrGameplayRandom()
    {
        body.transform.position=Vector3.up;var global=UnityEngine.Random.state;
        for(int i=0;i<360;i++)Pose(1,i/120f,.37f);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.Zero);
        var rotations=body.hips.GetComponentsInChildren<Transform>().Select(t=>t.rotation).ToArray();var position=body.transform.position;var state=body.animator.GetCurrentAnimatorStateInfo(0);float gait=body.animator.GetFloat("Gait"),rate=body.animator.GetFloat("StrideRate");
        Call(emitter,"SampleFeet",1f/120);Assert.That(body.hips.GetComponentsInChildren<Transform>().Select(t=>t.rotation),Is.EqualTo(rotations));Assert.That(body.transform.position,Is.EqualTo(position));
        Assert.That(body.animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(state.normalizedTime));Assert.That(body.animator.GetFloat("Gait"),Is.EqualTo(gait));Assert.That(body.animator.GetFloat("StrideRate"),Is.EqualTo(rate));Assert.That(UnityEngine.Random.state,Is.EqualTo(global));
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
    [Test]public void StompImpactSoundFiresOnceAtContactNotDuringWindupOrAfterCancellation()
    {
        var stomp=body.gameObject.AddComponent<TitanStompAttack>();stomp.automaticTick=false;
        ((Behaviour)emitter).enabled=false;((Behaviour)emitter).enabled=true;
        var go=new GameObject("Stomp sound target");SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(.7f,0,5.7f);
        go.AddComponent<BoxCollider>().center=Vector3.up;var target=go.AddComponent<TitanCombatTarget>();Physics.SyncTransforms();
        Assert.That(stomp.TryBeginStomp(target,true),Is.True);stomp.TickAttack(.6f);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.Zero);
        stomp.TickAttack(.21f);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.EqualTo(1));Assert.That(Prop<int>(emitter,"LastFoot"),Is.Zero);
        stomp.TickAttack(5);stomp.TickAttack(3);Assert.That(Prop<int>(emitter,"FootstepCount"),Is.EqualTo(1));
        Assert.That(stomp.TryBeginStomp(target,false),Is.True);stomp.TickAttack(.3f);body.StunEyes();stomp.TickAttack(10);
        Assert.That(Prop<int>(emitter,"FootstepCount"),Is.EqualTo(1));Assert.That(stomp.GetComponentsInChildren<LineRenderer>(),Is.Empty);
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
