#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class TitanODMIntegrationPlayModeTests
{
    Scene scene,previous;
    GameObject player,owner;
    MonoBehaviour walk,gear,hp;
    TitanAerialMotor motor;
    TitanCombatTarget target;
    CharacterController cc;
    AnimatorController animation;
    static Type Legacy(string name)
    { foreach(var a in AppDomain.CurrentDomain.GetAssemblies()) if(a.GetName().Name=="Assembly-CSharp") return a.GetType(name,true);throw new Exception("Real default assembly not compiled"); }
    static void Set(object o,string name,object value) { o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(o,value); }
    static T Get<T>(object o,string name) { return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(o); }
    static object Call(object o,string name,params object[] args) { return o.GetType().GetMethod(name).Invoke(o,args); }
    MonoBehaviour Add(string name) { return (MonoBehaviour)player.AddComponent(Legacy(name)); }
    [UnitySetUp] public IEnumerator SetUp()
    {
        previous=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Real_ODM_adapter_test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));SceneManager.SetActiveScene(scene);
        player=new GameObject("Real legacy ODM components");player.transform.position=Vector3.up*8;
        cc=player.AddComponent<CharacterController>();cc.height=3.6f;cc.radius=.5f;cc.center=Vector3.zero;
        walk=Add("Movement");gear=Add("ODM_Movement");hp=Add("PlayerHealth");
        animation=new AnimatorController();animation.AddLayer("Base");
        foreach(string s in new[]{"isRunning","isGasing","isFalling","StartGas","holdingAttack","canGrapple"}) animation.AddParameter(s,AnimatorControllerParameterType.Bool);
        var mesh=new GameObject("Character");mesh.transform.SetParent(player.transform,false);var anim=mesh.AddComponent<Animator>();anim.runtimeAnimatorController=animation;
        var indicator=new GameObject("GrappleIndict").AddComponent<Animator>();indicator.runtimeAnimatorController=animation;
        var ground=new GameObject("Groundcheck").transform;ground.SetParent(player.transform,false);ground.localPosition=Vector3.down*1.8f;
        var particles=new GameObject("Gas").AddComponent<ParticleSystem>();particles.transform.SetParent(player.transform,false);particles.Stop();
        var trail=new GameObject("Gas trail").AddComponent<TrailRenderer>();trail.transform.SetParent(player.transform,false);trail.emitting=false;
        var sounds=new[]{player.AddComponent<AudioSource>(),player.AddComponent<AudioSource>(),player.AddComponent<AudioSource>(),player.AddComponent<AudioSource>()};
        object audio=Activator.CreateInstance(Legacy("Audio"));Set(audio,"container",sounds);
        var lineA=new GameObject("Right line").AddComponent<LineRenderer>();var lineB=new GameObject("Left line").AddComponent<LineRenderer>();lineA.positionCount=lineB.positionCount=2;
        Set(walk,"cam",mesh.transform);Set(walk,"controller",cc);Set(walk,"groundCheck",ground);Set(walk,"jumpSfx",sounds[0]);Set(walk,"moveSpeed",15f);Set(walk,"enabledMovement",true);
        Set(gear,"controller",cc);Set(gear,"acceleration",50f);Set(gear,"maxSpeed",80f);Set(gear,"resistanceInertia",.8f);Set(gear,"strafeSpeed",50f);
        Set(gear,"raycastShooters",new[]{mesh.transform,mesh.transform});Set(gear,"camTransform",mesh.transform);Set(gear,"audio",audio);
        Set(gear,"lines",new[]{lineA,lineB});Set(gear,"gasTrail",trail);Set(gear,"gasParticle",particles);Set(gear,"camAnim",anim);Set(gear,"characterMesh",mesh.transform);
        Add("TitanODMHealthSource");target=player.AddComponent<TitanCombatTarget>();motor=(TitanAerialMotor)Add("TitanODMMotor");
        owner=new GameObject("Owner");owner.AddComponent<TitanCombatTarget>();var socket=new GameObject("Grip").transform;socket.SetParent(owner.transform);socket.position=player.transform.position-Vector3.right;
        SceneManager.SetActiveScene(previous);yield return null;
    }
    [UnityTearDown] public IEnumerator TearDown()
    { SceneManager.SetActiveScene(previous);yield return SceneManager.UnloadSceneAsync(scene);Object.DestroyImmediate(animation); }
    Transform Socket { get { return owner.transform.GetChild(0); } }
    void Hold(bool air=true)
    { Assert.That(motor.CanSuspend,Is.True);Assert.That(target.TryHold(owner.GetComponent<TitanCombatTarget>(),Socket,air,air?(Vector3?)player.transform.position:null),Is.True); }
    [UnityTest] public IEnumerator RealHooksBoostAndCoroutinesSuspendWithoutChangingFreeFlightTuning()
    {
        Set(gear,"leftGrappled",true);Set(gear,"rightGrappled",true);Set(gear,"currentSpeed",65f);Set(gear,"residualSpeed",25f);Set(gear,"gasing",true);
        Set(walk,"isJumping",true);Set(walk,"jumpForce",55f);Hold();
        Assert.That(Get<bool>(walk,"titanSuspended"),Is.True);Assert.That(Get<bool>(gear,"titanSuspended"),Is.True);
        Assert.That(Get<bool>(gear,"leftGrappled")||Get<bool>(gear,"rightGrappled"),Is.False);
        Assert.That(Get<float>(gear,"currentSpeed")+Get<float>(gear,"residualSpeed"),Is.Zero);Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);
        Vector3 position=player.transform.position;yield return new WaitForFixedUpdate();Assert.That(Vector3.Distance(position,player.transform.position),Is.LessThan(.001f));
        Assert.That(Get<float>(gear,"acceleration"),Is.EqualTo(50));Assert.That(Get<float>(gear,"maxSpeed"),Is.EqualTo(80));Assert.That(Get<float>(walk,"moveSpeed"),Is.EqualTo(15));
        Assert.That(player.transform.parent,Is.Null);target.EscapeHold();Assert.That(cc.enabled,Is.True);
    }
    [UnityTest] public IEnumerator RealGroundHoldAlsoSuspendsAndReleasesAtCurrentPoseNotCapturePose()
    {
        Vector3 start=player.transform.position;Hold(false);Socket.position+=Vector3.right*3;target.TickHold(.1f);Vector3 current=player.transform.position;
        target.EscapeHold();Assert.That(target.ReleasePending,Is.False);Assert.That(Vector3.Distance(current,player.transform.position),Is.LessThan(.001f));
        Assert.That(Vector3.Distance(start,player.transform.position),Is.GreaterThan(1));Assert.That(Get<bool>(walk,"titanSuspended"),Is.False);yield return null;
    }
    [UnityTest] public IEnumerator FullGearBoxClearanceCanKeepReleasePendingAfterCapsuleIsClear()
    {
        var box=new GameObject("Solid gear outside capsule");box.transform.SetParent(player.transform,false);box.transform.localPosition=Vector3.right*2;box.AddComponent<BoxCollider>().size=Vector3.one*.5f;
        Hold();var obstruction=new GameObject("Wall at gear only");SceneManager.MoveGameObjectToScene(obstruction,scene);obstruction.transform.position=box.transform.position;obstruction.AddComponent<BoxCollider>();Physics.SyncTransforms();
        Vector3 contact;Assert.That(target.TryGetHeldContact(owner.GetComponent<TitanCombatTarget>(),box.transform.position,.1f,out contact),Is.False,"Biting a gear box alone is not human contact");
        target.EscapeHold();Assert.That(target.IsHeld,Is.False);Assert.That(target.ReleasePending,Is.True);Assert.That(cc.enabled,Is.False);
        Assert.That(Get<bool>(walk,"titanSuspended"),Is.True);Object.Destroy(obstruction);yield return null;target.TickHold(0);
        Assert.That(target.ReleasePending,Is.False);Assert.That(cc.enabled,Is.True);Assert.That(Get<bool>(gear,"titanSuspended"),Is.False);
    }
    [UnityTest] public IEnumerator TiltedAvatarStillWaitsForNativeUprightControllerHeadroom()
    {
        player.transform.rotation=Quaternion.Euler(0,0,45);Physics.SyncTransforms();Hold();
        var ceiling=new GameObject("Native capsule headroom obstruction");SceneManager.MoveGameObjectToScene(ceiling,scene);
        ceiling.transform.position=player.transform.position+Vector3.up*1.7f;ceiling.AddComponent<BoxCollider>().size=new Vector3(10,.1f,10);Physics.SyncTransforms();
        target.EscapeHold();Assert.That(target.ReleasePending,Is.True,"Visual tilt must not shrink the actual upright CCT clearance");
        Assert.That(cc.enabled,Is.False);Object.Destroy(ceiling);yield return null;target.TickHold(0);
        Assert.That(target.ReleasePending,Is.False);Assert.That(cc.enabled,Is.True);
    }
    [UnityTest] public IEnumerator RealHealthIsSingleSourceAndExternalDeathReleasesBeforeRagdoll()
    {
        target.TakeDamage(40,Vector3.zero);Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(60));Assert.That(target.Health,Is.EqualTo(60));Hold();
        Call(hp,"TakeDamage",60f);Assert.That(target.IsHeld,Is.False);Assert.That(Get<bool>(hp,"isDead"),Is.True);
        Assert.That(walk.enabled,Is.False);Assert.That(gear.enabled,Is.False);Assert.That(player.transform.parent,Is.Null);yield return null;
    }
    [UnityTest] public IEnumerator RealSwordWindowSweepsCrossedZonesAndDeduplicatesTheWholeLimb()
    {
        var body=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
        SceneManager.MoveGameObjectToScene(body.gameObject,scene);body.GetComponent<TitanMotionStyle>().automaticTick=false;
        var limb=Array.Find(body.limbs,l=>l.region==TitanHitRegion.LeftArm);var zone=limb.hitZones[0].GetComponent<Collider>();
        var blade=new GameObject("Physical test blade");SceneManager.MoveGameObjectToScene(blade,scene);
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.01f,-1,0),new Vector3(.01f,-1,0),new Vector3(-.01f,1,0),new Vector3(.01f,1,0)};mesh.RecalculateBounds();blade.AddComponent<MeshFilter>().sharedMesh=mesh;
        Vector3 center=zone.bounds.center;blade.transform.position=center-Vector3.right*2;
        var window=new GameObject("Actual Attack damage window");SceneManager.MoveGameObjectToScene(window,scene);window.transform.SetParent(player.transform);window.SetActive(false);
        window.AddComponent(Legacy("DamageDealer"));var sweep=window.AddComponent(Legacy("TitanODMSwordSweep"));Set(sweep,"bladeMeshes",new[]{blade.transform});window.SetActive(true);
        blade.transform.position=center+Vector3.right*2;sweep.SendMessage("LateUpdate");
        Assert.That(limb.health,Is.EqualTo(body.limbHealth-10).Within(.001f),"Physical crossing hits once despite many substeps and arm zones");
        blade.transform.position=center-Vector3.right*2;sweep.SendMessage("LateUpdate");Assert.That(limb.health,Is.EqualTo(body.limbHealth-10).Within(.001f));
        window.SetActive(false);yield return null;Assert.That(limb.health,Is.EqualTo(body.limbHealth-10).Within(.001f));Object.Destroy(mesh);
    }
    [UnityTest] public IEnumerator ReleaseTransfersUpwardAndPlanarMotionWithoutResumingOldHooks()
    {
        Hold();Socket.position+=new Vector3(.4f,.2f,0);target.TickHold(.1f);target.EscapeHold();
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.GreaterThan(1));Assert.That(Get<float>(gear,"residualSpeed"),Is.GreaterThan(1));
        Assert.That(Get<bool>(gear,"leftGrappled")||Get<bool>(gear,"rightGrappled"),Is.False);yield return null;
    }
}
#endif
