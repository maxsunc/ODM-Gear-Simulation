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

public class TitanSwordImpactPlayModeTests
{
    Scene scene,previous;
    ModularTitanBody body;
    static Type Real(string n) { return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).First(t=>t!=null); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n).SetValue(o,v); }
    static object Call(object o,string n,params object[] args) { return o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args); }
    static T Prop<T>(object o,string n) { return (T)o.GetType().GetProperty(n).GetValue(o,null); }
    [UnitySetUp] public IEnumerator Setup()
    {
        previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Owned sword impact runtime",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
        body=Object.Instantiate(prefab).GetComponent<ModularTitanBody>();body.useRegionalHealth=true;body.GetHealth(TitanHitRegion.Nape);
        body.transform.position=Vector3.up*100;body.animator.Update(0);yield return null;
    }
    [UnityTearDown] public IEnumerator Teardown()
    { SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene); }

    [Test] public void RegionalHealthAccumulatesAndNapeOnlyKillsAtZero()
    {
        Assert.That(body.GetHealth(TitanHitRegion.LeftArm),Is.EqualTo(1000));Assert.That(body.GetHealth(TitanHitRegion.RightLeg),Is.EqualTo(2500));
        Assert.That(body.GetHealth(TitanHitRegion.Eyes),Is.EqualTo(500));Assert.That(body.GetHealth(TitanHitRegion.Nape),Is.EqualTo(500));
        var a=body.ReceiveDamage(TitanHitRegion.Nape,250,body.head.position,Vector3.zero);
        Assert.That(a.Outcome,Is.EqualTo(TitanDamageOutcome.Hit));Assert.That(a.RemainingHealth,Is.EqualTo(250));Assert.That(body.IsDead,Is.False);
        var b=body.ReceiveDamage(TitanHitRegion.Nape,775,body.head.position,Vector3.zero);
        Assert.That(b.Outcome,Is.EqualTo(TitanDamageOutcome.Killed));Assert.That(b.Damage,Is.EqualTo(775));Assert.That(b.AppliedDamage,Is.EqualTo(250));Assert.That(body.IsDead);
        Assert.That(body.ReceiveDamage(TitanHitRegion.Nape,1000,Vector3.zero,Vector3.zero).Accepted,Is.False);
    }
    [TestCase(TitanHitRegion.LeftArm,1000)][TestCase(TitanHitRegion.RightArm,1000)]
    [TestCase(TitanHitRegion.LeftLeg,2500)][TestCase(TitanHitRegion.RightLeg,2500)]
    public void EveryLimbHasIndependentHealthAndOnlyZeroSevers(TitanHitRegion region,float hp)
    {
        body.ReceiveDamage(region,hp-1,Vector3.zero,Vector3.zero);Assert.That(body.IsSevered(region),Is.False);Assert.That(body.GetHealth(region),Is.EqualTo(1));
        Assert.That(body.GetHealth(TitanHitRegion.Nape),Is.EqualTo(500));
        var result=body.ReceiveDamage(region,1,Vector3.zero,Vector3.zero);Assert.That(result.Outcome,Is.EqualTo(TitanDamageOutcome.Severed));Assert.That(body.IsSevered(region));
        Assert.That(body.ReceiveDamage(region,1000,Vector3.zero,Vector3.zero).Accepted,Is.False);
    }
    [Test] public void FaceNeedsFiveHundredDamageThenRecoversHealthWithoutBlindHitSpam()
    {
        Assert.That(body.ReceiveDamage(TitanHitRegion.Eyes,250,Vector3.zero,Vector3.zero).Outcome,Is.EqualTo(TitanDamageOutcome.Hit));Assert.That(body.IsBlind,Is.False);
        Assert.That(body.ReceiveDamage(TitanHitRegion.Eyes,250,Vector3.zero,Vector3.zero).Outcome,Is.EqualTo(TitanDamageOutcome.Blinded));Assert.That(body.IsBlind);
        body.TickStatus(1);float remaining=body.BlindTimeRemaining;
        Assert.That(body.ReceiveDamage(TitanHitRegion.Eyes,999,Vector3.zero,Vector3.zero).Accepted,Is.False);Assert.That(body.BlindTimeRemaining,Is.EqualTo(remaining));
        body.TickStatus(body.eyeStunDuration);Assert.That(body.IsBlind,Is.False);Assert.That(body.GetHealth(TitanHitRegion.Eyes),Is.EqualTo(500));
    }
    [Test] public void SpeedDamageIsCappedUsefulWhenStationaryAndFastEnoughCanSeverALeg()
    {
        var go=new GameObject("Owned dealer");var d=go.AddComponent(Real("DamageDealer"));Set(d,"baseDamage",250f);
        Assert.That((float)Call(d,"CalculateDamage",0f),Is.EqualTo(250));Assert.That((float)Call(d,"CalculateDamage",15f),Is.EqualTo(250));
        Assert.That((float)Call(d,"CalculateDamage",30f),Is.EqualTo(775));Assert.That((float)Call(d,"CalculateDamage",80f),Is.EqualTo(2525));
        Assert.That((float)Call(d,"CalculateDamage",10000f),Is.EqualTo(2525));Assert.That((float)Call(d,"CalculateDamage",float.NaN),Is.EqualTo(250));
        Assert.That(body.ReceiveDamage(TitanHitRegion.RightLeg,(float)Call(d,"CalculateDamage",80f),Vector3.zero,Vector3.zero).Outcome,Is.EqualTo(TitanDamageOutcome.Severed));
        Object.DestroyImmediate(go);
    }
    [Test] public void DamageMotionIgnoresTeleportAndNeedsTwoContinuousSamples()
    {
        var go=new GameObject("Owned player damage sampling");var playerHealth=go.AddComponent(Real("PlayerHealth"));var fx=(MonoBehaviour)go.AddComponent(Real("TitanSwordFeedback"));
        go.transform.position=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);Assert.That(Prop<float>(fx,"CurrentDamageSpeed"),Is.Zero);
        go.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);Assert.That(Prop<float>(fx,"CurrentDamageSpeed"),Is.EqualTo(30).Within(.01f));
        go.transform.position+=Vector3.right*100;Call(fx,"SampleDamageMotion",.02f);Assert.That(Prop<float>(fx,"CurrentDamageSpeed"),Is.Zero);
        go.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);Assert.That(Prop<float>(fx,"CurrentDamageSpeed"),Is.Zero);
        go.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);Assert.That(Prop<float>(fx,"CurrentDamageSpeed"),Is.EqualTo(30).Within(.02f));
        Set(playerHealth,"isGrabbed",true);go.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);Assert.That(Prop<float>(fx,"CurrentDamageSpeed"),Is.Zero,"Carried movement must never become sword damage");
        Set(playerHealth,"isGrabbed",false);go.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);Assert.That(Prop<float>(fx,"CurrentDamageSpeed"),Is.Zero,"Release needs fresh continuous motion, not a held-position jump");
        Object.DestroyImmediate(go);
    }
    [Test] public void ConfirmedContactsDeduplicateAllRegionCollidersAndColorOnlyActualOutcomes()
    {
        var root=new GameObject("Owned sword player");var window=new GameObject("Cutting window");window.transform.SetParent(root.transform);
        var dealer=window.AddComponent(Real("DamageDealer"));Set(dealer,"speedScaledDamage",true);Set(dealer,"baseDamage",250f);
        var fx=(MonoBehaviour)root.AddComponent(Real("TitanSwordFeedback"));fx.enabled=false;Set(fx,"dealer",dealer);fx.enabled=true;
        var zones=body.hitZones.Where(z=>z.region==TitanHitRegion.LeftArm).ToArray();Assert.That(zones.Length,Is.GreaterThan(1));
        var contact=dealer.GetType().GetMethod("ProcessContact",new[]{typeof(Collider)});
        foreach(var z in zones)contact.Invoke(dealer,new object[]{z.GetComponent<Collider>()});
        Assert.That(body.GetHealth(TitanHitRegion.LeftArm),Is.EqualTo(750));Assert.That(Prop<int>(fx,"ConfirmedHitCount"),Is.EqualTo(1));
        Assert.That(Prop<TitanDamageResult>(fx,"LastHit").Outcome,Is.EqualTo(TitanDamageOutcome.Hit));
        var color=fx.GetType().GetMethod("OutcomeColor");
        Assert.That((Color)color.Invoke(null,new object[]{TitanDamageOutcome.Hit}),Is.EqualTo(Color.white));
        foreach(var outcome in new[]{TitanDamageOutcome.Severed,TitanDamageOutcome.Blinded}){var c=(Color)color.Invoke(null,new object[]{outcome});Assert.That(c.r>.9f&&c.g>.7f&&c.b<.1f);}
        var red=(Color)color.Invoke(null,new object[]{TitanDamageOutcome.Killed});Assert.That(red.r>.9f&&red.g<.2f);
        window.SetActive(false);window.SetActive(true);contact.Invoke(dealer,new object[]{zones[0].GetComponent<Collider>()});Assert.That(body.GetHealth(TitanHitRegion.LeftArm),Is.EqualTo(500));
        Object.DestroyImmediate(root);
    }
    [UnityTest] public IEnumerator BloodAndTipTrailsAreBoundedCutOnlyAndCleanUpOnDisable()
    {
        var root=new GameObject("Owned pooled presentation");var window=new GameObject("Owned damage window");window.transform.SetParent(root.transform);window.SetActive(false);
        var dealer=window.AddComponent(Real("DamageDealer"));var sweep=window.AddComponent(Real("TitanODMSwordSweep"));
        var blades=new Transform[2];for(int i=0;i<2;i++){var blade=GameObject.CreatePrimitive(PrimitiveType.Cube);blade.transform.SetParent(root.transform);blades[i]=blade.transform;}
        Set(sweep,"bladeMeshes",blades);var fx=(MonoBehaviour)root.AddComponent(Real("TitanSwordFeedback"));fx.enabled=false;
        var asset=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSwordImpact.mat");Assert.That(asset,Is.Not.Null);Assert.That(asset.shader.name,Is.EqualTo("ODM/Titan/Impact"));
        foreach(string clip in new[]{"SwordWhoosh","FleshHit","LimbSever","NapeKill"})Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TitanCombat/"+clip+".wav"),Is.Not.Null);
        var material=new Material(asset);Set(fx,"dealer",dealer);Set(fx,"sweep",sweep);Set(fx,"effectMaterial",material);fx.enabled=true;
        Call(fx,"LateUpdate");Assert.That(root.GetComponentsInChildren<TrailRenderer>().Length,Is.EqualTo(2));Assert.That(root.GetComponentsInChildren<TrailRenderer>().All(t=>!t.emitting));
        window.SetActive(true);Call(fx,"LateUpdate");Assert.That(root.GetComponentsInChildren<TrailRenderer>().All(t=>t.emitting));
        window.SetActive(false);Call(fx,"LateUpdate");Assert.That(root.GetComponentsInChildren<TrailRenderer>().All(t=>!t.emitting));
        var ps=Resources.FindObjectsOfTypeAll<ParticleSystem>().Where(p=>p.gameObject.scene==scene&&p.name.StartsWith("[Sword FX]")).ToArray();Assert.That(ps.Length,Is.EqualTo(8));Assert.That(ps.All(p=>p.main.maxParticles==96));
        fx.GetType().GetMethod("OnHit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fx,new object[]{new TitanDamageResult{Body=body,Outcome=TitanDamageOutcome.Severed,Region=TitanHitRegion.LeftArm,Damage=1000,Point=body.leftHand.position,Direction=Vector3.right}});
        Assert.That(ps.Sum(p=>p.particleCount),Is.GreaterThan(0));Assert.That(ps.Any(p=>p.emission.enabled),"Detached branch receives a short moving spray, not only a stationary puff");
        fx.enabled=false;Object.Destroy(root);Object.Destroy(material);yield return null;
        Assert.That(Resources.FindObjectsOfTypeAll<ParticleSystem>().Any(p=>p.gameObject.scene==scene&&p.name.StartsWith("[Sword FX]")),Is.False);
    }
    [Test] public void RenderOnlyShakeRestoresProjectionWithoutMovingCameraOrAimingRoot()
    {
        var root=new GameObject("Owned camera feedback");var camera=root.AddComponent<Camera>();camera.enabled=false;
        var fx=(MonoBehaviour)root.AddComponent(Real("TitanSwordFeedback"));Set(fx,"playerCamera",camera);
        var onHit=fx.GetType().GetMethod("OnHit",BindingFlags.NonPublic|BindingFlags.Instance);
        onHit.Invoke(fx,new object[]{new TitanDamageResult{Outcome=TitanDamageOutcome.Killed,Body=body,Damage=775,Point=body.head.position,Direction=Vector3.forward}});
        var matrix=camera.projectionMatrix;var position=root.transform.position;var rotation=root.transform.rotation;
        Call(fx,"BeginShake",camera);Assert.That(camera.projectionMatrix,Is.Not.EqualTo(matrix));Assert.That(root.transform.position,Is.EqualTo(position));Assert.That(root.transform.rotation,Is.EqualTo(rotation));
        Call(fx,"EndShake",camera);Assert.That(camera.projectionMatrix,Is.EqualTo(matrix));
        camera.fieldOfView+=10;Assert.That(camera.projectionMatrix.m11,Is.Not.EqualTo(matrix.m11),"Shake must not freeze automatic FOV projection");matrix=camera.projectionMatrix;
        Call(fx,"BeginShake",camera);fx.enabled=false;Assert.That(camera.projectionMatrix,Is.EqualTo(matrix));Object.DestroyImmediate(root);
    }
    [UnityTest] public IEnumerator CutImpulseLaunchesWholeBranchesWithoutJointEnergyAtOneThreeAndFiveScale()
    {
        Object.Destroy(body.gameObject);yield return null;
        foreach(float scale in new[]{1f,3f,5f})foreach(var region in new[]{TitanHitRegion.LeftArm,TitanHitRegion.RightArm,TitanHitRegion.LeftLeg,TitanHitRegion.RightLeg})
        {
            body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
            body.transform.localScale=Vector3.one*scale;body.transform.position=Vector3.up*100;body.useRegionalHealth=true;body.GetHealth(region);body.animator.Update(.37f);
            Physics.SyncTransforms(); // Sample current physics/visual pose, not stale pre-teleport Rigidbody positions.
            var limb=body.limbs.First(l=>l.region==region);var pose=limb.bodies.Select(b=>b.position).ToArray();
            body.ReceiveDamage(region,body.GetMaximumHealth(region),limb.rootBone.position,Vector3.right*150);
            for(int i=0;i<limb.bodies.Length;i++)Assert.That(Vector3.Distance(pose[i],limb.bodies[i].position),Is.LessThan(.001f),"Impulse must not snap the sever pose");
            yield return null;Physics.SyncTransforms();var physics=scene.GetPhysicsScene();float maximum=0;
            physics.Simulate(1/120f);float mass=limb.bodies.Sum(b=>b.mass);Vector3 v=limb.bodies.Aggregate(Vector3.zero,(sum,b)=>sum+b.velocity*b.mass)/mass;
            Assert.That(v.x,Is.GreaterThan(5f),"The whole branch launches along the slash, not just the root bone");
            for(int i=0;i<119;i++){physics.Simulate(1/120f);maximum=Mathf.Max(maximum,limb.bodies.Max(b=>b.velocity.magnitude));}
            Assert.That(maximum,Is.LessThan(40f),"Retain strict ragdoll safety without velocity caps");
            Assert.That(limb.bodies.All(b=>b.maxAngularVelocity==4&&b.solverIterations==12&&b.solverVelocityIterations==4));
            Debug.Log("TITAN_SEVER_IMPACT "+region+" scale="+scale+" launch="+v+" max="+maximum);
            Object.Destroy(limb.rootBone.parent.gameObject);Object.Destroy(body.gameObject);yield return null;
        }
    }
}
#endif
