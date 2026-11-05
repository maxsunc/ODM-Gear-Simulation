#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
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
    GameObject player,owner,jumpFloor;
    ModularTitanBody biteTitan;
    TitanGrabAttack biteGrab;
    Transform biteModel;
    MonoBehaviour corpsePresentation,biteFeedback;
    MonoBehaviour walk,gear,hp;
    TitanAerialMotor motor;
    TitanCombatTarget target;
    CharacterController cc;
    AnimatorController animation;
    static Type Legacy(string name)
    { foreach(var a in AppDomain.CurrentDomain.GetAssemblies()) if(a.GetName().Name=="Assembly-CSharp") return a.GetType(name,true);throw new Exception("Real default assembly not compiled"); }
    static void Set(object o,string name,object value) { o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(o,value); }
    static T Get<T>(object o,string name) { return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(o); }
    static object Call(object o,string name,params object[] args) { return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args); }
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
    ModularTitanBody HookTitan()
    {
        var body=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
        SceneManager.MoveGameObjectToScene(body.gameObject,scene);body.transform.position=Vector3.right*30;
        body.GetComponent<TitanMotionStyle>().automaticTick=false;Physics.SyncTransforms();return body;
    }
    [UnityTest] public IEnumerator HooksFollowExactAnimatedBoneLocalPointThroughTranslationRotationAndScale()
    {
        gear.enabled=false;var body=HookTitan();
        var shape=body.ragdollBones.First(e=>e.collider!=null && e.body.transform==body.leftLowerArm).collider;
        Vector3 point=shape.ClosestPoint(shape.bounds.center+Vector3.back*10),local=shape.transform.InverseTransformPoint(point);
        Assert.That(Call(gear,"TryAttachHook",false,shape,point),Is.True);
        body.transform.position+=new Vector3(3,4,5);body.transform.rotation=Quaternion.Euler(0,127,0);body.transform.localScale=Vector3.one*3;
        body.leftLowerArm.localRotation*=Quaternion.Euler(35,0,25);Physics.SyncTransforms();Call(gear,"RefreshHookAnchors");
        Vector3 expected=shape.transform.TransformPoint(local),actual=(Vector3)gear.GetType().GetProperty("LeftAnchor").GetValue(gear);
        Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.0001f));Assert.That(Vector3.Distance(actual,point),Is.GreaterThan(5));
        Assert.That(Vector3.Distance(Get<LineRenderer[]>(gear,"lines")[1].GetPosition(1),expected),Is.LessThan(.0001f));Assert.That(player.transform.parent,Is.Null);
        yield return null;
    }
    [UnityTest] public IEnumerator TwoHooksTrackIndependentlyAndSeverDeathOrDisabledSurfaceReleaseOnlyInvalidAnchors()
    {
        gear.enabled=false;var body=HookTitan();var shape=body.ragdollBones.First(e=>e.collider!=null && e.body.transform==body.leftLowerArm).collider;
        var rock=new GameObject("Moving scenery hook");SceneManager.MoveGameObjectToScene(rock,scene);rock.transform.position=Vector3.right*10;var solid=rock.AddComponent<BoxCollider>();Physics.SyncTransforms();
        Assert.That(Call(gear,"TryAttachHook",false,shape,shape.bounds.center),Is.True);Assert.That(Call(gear,"TryAttachHook",true,solid,solid.bounds.center),Is.True);
        rock.transform.position+=Vector3.forward*5;Call(gear,"RefreshHookAnchors");
        Assert.That(Vector3.Distance((Vector3)gear.GetType().GetProperty("RightAnchor").GetValue(gear),solid.transform.position),Is.LessThan(.0001f));
        body.ApplyDamage(TitanHitRegion.LeftArm,1000,shape.bounds.center,Vector3.zero);Call(gear,"RefreshHookAnchors");
        Assert.That(Get<bool>(gear,"leftGrappled"),Is.False);Assert.That(Get<bool>(gear,"rightGrappled"),Is.True);
        solid.enabled=false;Call(gear,"RefreshHookAnchors");Assert.That(Get<bool>(gear,"rightGrappled"),Is.False);
        var torso=body.ragdollBones.First(e=>e.collider!=null && e.body.transform==body.hips).collider;
        Assert.That(Call(gear,"TryAttachHook",true,torso,torso.bounds.center),Is.True);body.Die();Call(gear,"RefreshHookAnchors");Assert.That(Get<bool>(gear,"rightGrappled"),Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator CaptureImmediatelyCancelsCommittedSlashAndCannotReopenAfterAnticipation()
    {
        var attack=Add("Attack");var window=new GameObject("Attack window");window.transform.SetParent(player.transform,false);window.SetActive(false);
        var dealer=window.AddComponent(Legacy("DamageDealer"));Set(attack,"damageDealer",dealer);Set(attack,"strikeDelay",.075f);Set(attack,"activeDuration",.12f);
        Assert.That(Call(attack,"TryBeginCharge"),Is.True);Assert.That((bool)attack.GetType().GetProperty("IsAttacking").GetValue(attack),Is.True);
        Hold();Assert.That((bool)attack.GetType().GetProperty("IsAttacking").GetValue(attack),Is.False);Assert.That(window.activeSelf,Is.False);
        Assert.That(Call(attack,"TryBeginCharge"),Is.False);yield return new WaitForSeconds(.12f);
        Assert.That(window.activeSelf,Is.False,"Cancelled anticipation cannot resurrect a damage window");target.EscapeHold();
        Assert.That(Call(attack,"TryBeginCharge"),Is.True,"Only a fresh request after safe release can start another attack");Call(attack,"CancelAttack");
    }
    [UnityTest] public IEnumerator HeldAndReleasePendingRejectEvenForcedBaseDamageContacts()
    {
        var body=HookTitan();var limb=body.limbs.First(l=>l.region==TitanHitRegion.RightArm);var zone=limb.hitZones[0].GetComponent<Collider>();float before=limb.health;
        var box=new GameObject("Release-obstructing gear");box.transform.SetParent(player.transform,false);box.transform.localPosition=Vector3.right*2;box.AddComponent<BoxCollider>().size=Vector3.one*.5f;
        var window=new GameObject("Forced rogue damage window");window.transform.SetParent(player.transform,false);var dealer=window.AddComponent(Legacy("DamageDealer"));Set(dealer,"baseDamage",1000f);
        Hold();dealer.GetType().GetMethod("ProcessContact",new[]{typeof(Collider)}).Invoke(dealer,new object[]{zone});Assert.That(limb.health,Is.EqualTo(before));
        var wall=new GameObject("Release blocker");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=box.transform.position;wall.AddComponent<BoxCollider>();Physics.SyncTransforms();
        target.EscapeHold();Assert.That(target.ReleasePending,Is.True);dealer.GetType().GetMethod("ProcessContact",new[]{typeof(Collider)}).Invoke(dealer,new object[]{zone});Assert.That(limb.health,Is.EqualTo(before));
        Object.Destroy(wall);yield return null;target.TickHold(0);Assert.That(target.ReleasePending,Is.False);
    }
    MonoBehaviour DamageMemory(out MonoBehaviour attack)
    {
        walk.enabled=false;gear.enabled=false;attack=Add("Attack");Set(attack,"strikeDelay",.075f);Set(attack,"activeDuration",.12f);Set(attack,"recoveryDuration",.195f);
        var fx=Add("TitanSwordFeedback");fx.enabled=false;Set(fx,"attack",attack);fx.enabled=true;return fx;
    }
    [UnityTest] public IEnumerator NativeTitanCollisionPreservesApproachDamageBrieflyForThatTitanOnly()
    {
        var body=HookTitan();MonoBehaviour attack;var fx=DamageMemory(out attack);
        var physicalPart=new GameObject("Owned Titan physical contact surface");physicalPart.transform.SetParent(body.transform,false);physicalPart.transform.position=new Vector3(3,8,0);
        physicalPart.AddComponent<BoxCollider>().size=new Vector3(1,5,5);Physics.SyncTransforms();
        for(int i=0;i<3;i++){cc.Move(Vector3.right*.6f);Call(fx,"SampleDamageMotion",.02f);}
        Assert.That((float)fx.GetType().GetProperty("CurrentDamageSpeed").GetValue(fx),Is.EqualTo(30).Within(.01f));
        Assert.That(Call(attack,"TryBeginCharge"),Is.True);
        cc.Move(Vector3.right*.6f);Call(fx,"SampleDamageMotion",.02f);cc.Move(Vector3.right*.6f);Call(fx,"SampleDamageMotion",.02f);
        Assert.That(player.transform.position.x,Is.LessThan(2.1f),"Native CCT still collides; no pass-through/velocity injection");
        Assert.That((float)fx.GetType().GetProperty("CurrentDamageSpeed").GetValue(fx),Is.LessThan(1));
        Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.EqualTo(30).Within(.02f),"Native impact must preserve actual approach speed despite the following stopped sample");
        var other=HookTitan();Assert.That((float)Call(fx,"DamageSpeedFor",other),Is.LessThan(1),"No borrowing another target's collision receipt");
        yield return new WaitForSeconds(.21f);Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.LessThan(1),"Repeated pushing/late attacks cannot bank speed indefinitely");
    }
    [UnityTest] public IEnumerator WallTeleportCancellationAndCaptureInvalidateImpactReceipts()
    {
        var body=HookTitan();MonoBehaviour attack;var fx=DamageMemory(out attack);var solid=body.ragdollBones.First(e=>e.collider!=null).collider;
        Action prime=()=>
        {
            Call(fx,"ResetDamageHistory");for(int i=0;i<2;i++){player.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);}
            Call(fx,"RecordSolidContact",solid,Vector3.right,Vector3.left);Call(fx,"SampleDamageMotion",.02f);
            Call(attack,"CancelAttack"); // Cancellation intentionally clears the first receipt.
            for(int i=0;i<2;i++){player.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);}
            Call(fx,"RecordSolidContact",solid,Vector3.right,Vector3.left);Call(fx,"SampleDamageMotion",.02f);Assert.That(Call(attack,"TryBeginCharge"),Is.True);
            Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.EqualTo(30).Within(.02f));
        };
        prime();var wall=new GameObject("Ordinary blocking wall").AddComponent<BoxCollider>();Call(fx,"RecordSolidContact",wall,Vector3.right,Vector3.left);Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.Zero);
        prime();player.transform.position+=Vector3.right*100;Call(fx,"SampleDamageMotion",.02f);Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.Zero);
        prime();Call(attack,"CancelAttack");Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.Zero);
        prime();Hold();Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.Zero);target.EscapeHold();Assert.That((float)Call(fx,"DamageSpeedFor",body),Is.Zero);
        Object.Destroy(wall.gameObject);yield return null;
    }
    [UnityTest] public IEnumerator TangentialFootContactDoesNotEraseTrustedWalkingSpeed()
    {
        MonoBehaviour attack;var fx=DamageMemory(out attack);
        for(int i=0;i<2;i++){player.transform.position+=Vector3.right*.6f;Call(fx,"SampleDamageMotion",.02f);}
        var floor=new GameObject("Foot support not a blocking wall").AddComponent<BoxCollider>();
        Call(fx,"RecordSolidContact",floor,Vector3.down,Vector3.up);
        Assert.That((float)fx.GetType().GetProperty("CurrentDamageSpeed").GetValue(fx),Is.EqualTo(30).Within(.02f));Object.Destroy(floor.gameObject);yield return null;
    }
    void GravityOnly(float acceleration)
    {
        // Exercise the REAL CharacterController/motor, without input or automatic
        // Update/FixedUpdate racing deterministic ticks in this owned local scene.
        walk.enabled=false;gear.enabled=false;Set(walk,"enabledMovement",false);
        Call(walk,"ResetGravity",true);Set(walk,"gravityConstant",acceleration);
        player.transform.position=new Vector3(0,40,0);Physics.SyncTransforms();cc.Move(Vector3.zero);
    }
    void GravitySteps(int count) { for(int i=0;i<count;i++)Call(walk,"FixedUpdate"); }
    [UnityTest] public IEnumerator InspectorGravityActuallyControlsFallAndCanChangeInFlight()
    {
        GravityOnly(10);float dt=Time.fixedDeltaTime,start=player.transform.position.y;const int n=25;
        GravitySteps(n);
        Assert.That((float)walk.GetType().GetProperty("GravityVerticalSpeed").GetValue(walk),Is.EqualTo(-10*dt*n).Within(.0001f));
        Assert.That(start-player.transform.position.y,Is.EqualTo(10*dt*dt*n*(n+1)/2).Within(.002f));
        Set(walk,"gravityConstant",30f);float before=player.transform.position.y;GravitySteps(1);
        Assert.That(before-player.transform.position.y,Is.EqualTo((10*dt*n+30*dt)*dt).Within(.001f),"Inspector changes must affect the very next physics tick, without resetting momentum");
        yield return null;
    }
    [UnityTest] public IEnumerator ZeroGravityCoastsExistingFallAndNeverStartsFallingFromRest()
    {
        GravityOnly(0);float start=player.transform.position.y;GravitySteps(50);
        Assert.That(player.transform.position.y,Is.EqualTo(start).Within(.0001f));
        Set(walk,"gravityConstant",10f);GravitySteps(10);Set(walk,"gravityConstant",0f);
        start=player.transform.position.y;GravitySteps(10);
        Assert.That(start-player.transform.position.y,Is.EqualTo(10*Time.fixedDeltaTime*10*Time.fixedDeltaTime*10).Within(.002f),"Zero acceleration is not an implicit velocity reset");
        yield return null;
    }
    [UnityTest] public IEnumerator FallSpeedLimitIsIndependentOfGravityAndHandlesExtremeInspectorValues()
    {
        GravityOnly(25000);Set(walk,"maximumFallSpeed",12f);float start=player.transform.position.y;GravitySteps(50);
        Assert.That(start-player.transform.position.y,Is.EqualTo(12*Time.fixedDeltaTime*50).Within(.003f));
        Assert.That((float)walk.GetType().GetProperty("GravityVerticalSpeed").GetValue(walk),Is.EqualTo(-12).Within(.0001f));
        Set(walk,"maximumFallSpeed",6f);start=player.transform.position.y;GravitySteps(1);
        Assert.That(start-player.transform.position.y,Is.EqualTo(6*Time.fixedDeltaTime).Within(.001f));
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Call(walk,"ResetGravity",true);Set(walk,"gravityConstant",invalid);Set(walk,"maximumFallSpeed",invalid);GravitySteps(1);
            Assert.That((float)walk.GetType().GetProperty("GravityVerticalSpeed").GetValue(walk),Is.EqualTo(-9.8f*Time.fixedDeltaTime).Within(.0001f));
        }
        Call(walk,"ResetGravity",true);Set(walk,"gravityConstant",-10f);start=player.transform.position.y;GravitySteps(1);
        Assert.That(player.transform.position.y,Is.EqualTo(start).Within(.0001f),"Invalid negative gravity must not launch the player upward");
        yield return null;
    }
    [UnityTest] public IEnumerator LandingClearsAccumulatedFallSpeedAndWalkingOffALedgeStartsFresh()
    {
        GravityOnly(30);var floor=new GameObject("Owned gravity landing floor");SceneManager.MoveGameObjectToScene(floor,scene);
        floor.transform.position=new Vector3(0,-.5f,0);floor.AddComponent<BoxCollider>().size=new Vector3(30,1,30);
        player.transform.position=new Vector3(0,5,0);Physics.SyncTransforms();GravitySteps(100);
        Assert.That(cc.isGrounded,Is.True);Assert.That(player.transform.position.y,Is.EqualTo(1.8f).Within(.1f));
        Assert.That((float)walk.GetType().GetProperty("GravityVerticalSpeed").GetValue(walk),Is.Zero);
        cc.Move(Vector3.right*40);Assert.That(cc.isGrounded,Is.False);float start=player.transform.position.y;GravitySteps(1);
        Assert.That(start-player.transform.position.y,Is.EqualTo(30*Time.fixedDeltaTime*Time.fixedDeltaTime).Within(.002f),"No retained terminal-speed slam off the next ledge");
        yield return null;
    }
    [UnityTest] public IEnumerator UpwardReleaseUsesTheSameGravityAndPoweredResetCancelsOldVerticalMotion()
    {
        GravityOnly(20);Assert.That(Call(walk,"SuspendForTitan",owner),Is.True);Call(walk,"ResumeAfterTitan",owner,Vector3.up*4);
        // A nearby foot probe must not erase an upward throw as it clears support.
        Call(walk,"zeroGravity");float start=player.transform.position.y;GravitySteps(1);
        Assert.That(player.transform.position.y-start,Is.EqualTo(4*Time.fixedDeltaTime).Within(.001f));
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.EqualTo(4-20*Time.fixedDeltaTime).Within(.0001f));
        Set(walk,"gravityConstant",40f);GravitySteps(1);
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.EqualTo(4-60*Time.fixedDeltaTime).Within(.0001f));
        // Gas already calls ResetGravity(false); it must not fight stale release-up.
        Call(walk,"ResetGravity",false);start=player.transform.position.y;GravitySteps(20);
        Assert.That(player.transform.position.y,Is.EqualTo(start).Within(.0001f));
        Call(walk,"ResetGravity",true);GravitySteps(1);
        Assert.That(start-player.transform.position.y,Is.EqualTo(40*Time.fixedDeltaTime*Time.fixedDeltaTime).Within(.001f));
        yield return null;
    }
    [UnityTest] public IEnumerator HeldAndDisabledControllersDoNotAccumulateGravityAndReleaseStartsFromIncomingVelocity()
    {
        GravityOnly(30);GravitySteps(5);Assert.That(Call(walk,"SuspendForTitan",owner),Is.True);
        float start=player.transform.position.y;GravitySteps(100);Assert.That(player.transform.position.y,Is.EqualTo(start).Within(.0001f));
        Call(walk,"ResumeAfterTitan",owner,Vector3.down*2);GravitySteps(1);
        Assert.That(start-player.transform.position.y,Is.EqualTo((2+30*Time.fixedDeltaTime)*Time.fixedDeltaTime).Within(.001f));
        cc.enabled=false;float speed=(float)walk.GetType().GetProperty("GravityVerticalSpeed").GetValue(walk);GravitySteps(100);
        Assert.That((float)walk.GetType().GetProperty("GravityVerticalSpeed").GetValue(walk),Is.EqualTo(speed));
        yield return null;
    }
    [UnityTest] public IEnumerator CompletedBallisticJumpDoesNotLeaveAHiddenGravityMultiplier()
    {
        JumpOnly(10,.5f);Assert.That(Call(walk,"TryJump"),Is.True);GravitySteps(100);
        Assert.That(Get<bool>(walk,"isJumping"),Is.False);player.transform.position=Vector3.up*40;Physics.SyncTransforms();cc.Move(Vector3.zero);
        float start=player.transform.position.y;GravitySteps(1);
        Assert.That(start-player.transform.position.y,Is.EqualTo(10*Time.fixedDeltaTime*Time.fixedDeltaTime).Within(.001f));
        yield return null;
    }
    [UnityTest] public IEnumerator UpwardReleaseStopsAtRealCeilingThenFallsWithInspectorGravity()
    {
        GravityOnly(20);var ceiling=new GameObject("Owned release ceiling");SceneManager.MoveGameObjectToScene(ceiling,scene);
        ceiling.transform.position=player.transform.position+Vector3.up*2.1f;ceiling.AddComponent<BoxCollider>().size=new Vector3(10,.2f,10);Physics.SyncTransforms();
        Call(walk,"SuspendForTitan",owner);Call(walk,"ResumeAfterTitan",owner,Vector3.up*20);GravitySteps(1);
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.Zero,"Native CCT head contact must cancel upward momentum");
        float start=player.transform.position.y;GravitySteps(1);
        Assert.That(start-player.transform.position.y,Is.EqualTo(20*Time.fixedDeltaTime*Time.fixedDeltaTime).Within(.002f));
        yield return null;
    }
    void JumpOnly(float acceleration=20,float height=4)
    {
        GravityOnly(acceleration);
        if(jumpFloor==null)
        {
            jumpFloor=new GameObject("Owned jump floor");SceneManager.MoveGameObjectToScene(jumpFloor,scene);
            jumpFloor.transform.position=Vector3.down*.5f;jumpFloor.AddComponent<BoxCollider>().size=new Vector3(30,1,30);
        }
        Set(walk,"jumpHeight",height);Set(walk,"jumpDelay",0f);Set(walk,"jumpCooldown",0f);Set(walk,"jumpCooldownRemaining",0f);
        Set(walk,"targetLayer",(LayerMask)~0);Set(walk,"enabledMovement",true);Set(walk,"move",Vector3.zero);walk.enabled=true;
        // Settle from ABOVE the native skin envelope, rather than spawning the
        // capsule already .08m inside its support skin and comparing to that pose.
        player.transform.position=Vector3.up*(cc.height*.5f+cc.skinWidth+.1f);Physics.SyncTransforms();cc.Move(Vector3.down*.3f);
    }
    [UnityTest] public IEnumerator CustomJumpHeightUsesGravityToReachBallisticApexAndLand()
    {
        foreach(var choice in new[]{new Vector2(10,1.5f),new Vector2(20,4),new Vector2(30,7)})
        {
            JumpOnly(choice.x,choice.y);float start=player.transform.position.y,peak=start;
            Assert.That((float)walk.GetType().GetProperty("JumpLaunchSpeed").GetValue(walk),Is.EqualTo(Mathf.Sqrt(2*choice.x*choice.y)).Within(.0001f));
            Assert.That(Call(walk,"TryJump"),Is.True);int steps=0;
            do { GravitySteps(1);peak=Mathf.Max(peak,player.transform.position.y);Assert.That(++steps,Is.LessThan(400)); }
            while(Get<bool>(walk,"isJumping"));
            Assert.That(peak-start,Is.EqualTo(choice.y).Within(.035f),"Custom height is a measured height, not an arbitrary per-frame force");
            Assert.That(cc.isGrounded,Is.True);Assert.That(player.transform.position.y,Is.EqualTo(start).Within(.02f));
        }
        yield return null;
    }
    [UnityTest] public IEnumerator JumpDelayCooldownAndDuplicateRequestsAreEnforcedInPhysicsTime()
    {
        JumpOnly(20,.25f);Set(walk,"jumpDelay",.1f);Set(walk,"jumpCooldown",2f);float start=player.transform.position.y;
        Assert.That(Call(walk,"TryJump"),Is.True);Assert.That(Call(walk,"TryJump"),Is.False);
        GravitySteps(4);Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);Assert.That(player.transform.position.y,Is.EqualTo(start).Within(.002f));
        GravitySteps(1);Assert.That(Get<float>(walk,"jumpForce"),Is.GreaterThan(0));
        GravitySteps(40);Assert.That(cc.isGrounded,Is.True);Assert.That(Call(walk,"TryJump"),Is.False,"Cooldown can outlast a short jump");
        GravitySteps(100);Set(walk,"jumpDelay",0f);Assert.That(Call(walk,"TryJump"),Is.True);GravitySteps(1);
        Assert.That(Get<float>(walk,"jumpForce"),Is.GreaterThan(0),"Zero delay launches on the next fixed tick");
        yield return null;
    }
    [UnityTest] public IEnumerator JumpRejectsMidairTriggersSideWallsAndRepeatedAirborneRequests()
    {
        GravityOnly(20);walk.enabled=true;Set(walk,"enabledMovement",true);Set(walk,"targetLayer",(LayerMask)~0);
        Assert.That(Call(walk,"TryJump"),Is.False);
        var trigger=new GameObject("Not jump support");SceneManager.MoveGameObjectToScene(trigger,scene);
        trigger.transform.position=player.transform.position+Vector3.down*1.85f;var triggerBox=trigger.AddComponent<BoxCollider>();triggerBox.size=new Vector3(5,.1f,5);triggerBox.isTrigger=true;
        var wall=new GameObject("Vertical wall not ground");SceneManager.MoveGameObjectToScene(wall,scene);
        wall.transform.position=player.transform.position+Vector3.right*.65f;wall.AddComponent<BoxCollider>().size=new Vector3(.1f,10,10);Physics.SyncTransforms();
        Assert.That(Call(walk,"TryJump"),Is.False,"A trigger/nearby wall must not authorize an air jump");
        Object.DestroyImmediate(trigger);Object.DestroyImmediate(wall);JumpOnly();Assert.That(Call(walk,"TryJump"),Is.True);GravitySteps(5);
        Assert.That(Call(walk,"TryJump"),Is.False,"No stacked or double jumps even with zero cooldown");
        yield return null;
    }
    [UnityTest] public IEnumerator LeavingTheFloorDuringJumpDelayCancelsTakeoffWithoutDisablingGravity()
    {
        JumpOnly();Set(walk,"jumpDelay",.2f);Assert.That(Call(walk,"TryJump"),Is.True);GravitySteps(2);
        cc.Move(Vector3.right*40);float start=player.transform.position.y;GravitySteps(20);
        Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);Assert.That(Get<bool>(walk,"isJumping"),Is.False);
        Assert.That((bool)walk.GetType().GetProperty("JumpPending").GetValue(walk),Is.False);
        Assert.That(player.transform.position.y,Is.LessThan(start-.5f));Assert.That(Call(walk,"TryJump"),Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator JumpHeadContactStopsAscentAndReturnsToTheFloor()
    {
        JumpOnly();var ceiling=new GameObject("Owned jump ceiling");SceneManager.MoveGameObjectToScene(ceiling,scene);
        ceiling.transform.position=new Vector3(0,4.3f,0);ceiling.AddComponent<BoxCollider>().size=new Vector3(10,.2f,10);Physics.SyncTransforms();
        float start=player.transform.position.y,peak=start;Assert.That(Call(walk,"TryJump"),Is.True);
        for(int i=0;i<80;i++){GravitySteps(1);peak=Mathf.Max(peak,player.transform.position.y);}
        Assert.That(peak-start,Is.GreaterThan(.3f).And.LessThan(.7f));Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);
        Assert.That(cc.isGrounded,Is.True);Assert.That(Get<bool>(walk,"isJumping"),Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator GasDisableHoldAndDeathCancelJumpRequestsWithoutAStoredTakeoff()
    {
        JumpOnly();Set(walk,"jumpDelay",.1f);Assert.That(Call(walk,"TryJump"),Is.True);Call(walk,"ResetGravity",false);GravitySteps(10);
        Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);Assert.That(Get<bool>(walk,"isJumping"),Is.False);
        Call(walk,"ResetGravity",true);Assert.That(Call(walk,"TryJump"),Is.True);walk.enabled=false;walk.enabled=true;GravitySteps(10);
        Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);
        Assert.That(Call(walk,"TryJump"),Is.True);Call(walk,"SuspendForTitan",owner);GravitySteps(10);Call(walk,"ResumeAfterTitan",owner,Vector3.zero);GravitySteps(10);
        Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);
        Assert.That(Call(walk,"TryJump"),Is.True);Set(hp,"isDead",true);GravitySteps(10);Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);
        Assert.That(Call(walk,"TryJump"),Is.False);Set(hp,"isDead",false);Set(walk,"jumpDelay",0f);
        Assert.That(Call(walk,"TryJump"),Is.True);GravitySteps(1);Assert.That(Get<float>(walk,"jumpForce"),Is.GreaterThan(0));
        Call(walk,"ResetGravity",false);float start=player.transform.position.y;GravitySteps(20);
        Assert.That(player.transform.position.y,Is.EqualTo(start).Within(.0001f),"Powered ODM owns movement immediately, with no delayed jump coroutine");
        yield return null;
    }
    [UnityTest] public IEnumerator DisabledInvalidAndZeroGravityJumpSettingsFailWithoutAnUnboundedLaunch()
    {
        JumpOnly();foreach(float height in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        { Set(walk,"jumpHeight",height);Assert.That(Call(walk,"TryJump"),Is.False); }
        Set(walk,"jumpHeight",4f);Set(walk,"gravityConstant",0f);Assert.That(Call(walk,"TryJump"),Is.False,"A finite height needs positive gravity");
        Set(walk,"gravityConstant",20f);cc.enabled=false;Assert.That(Call(walk,"TryJump"),Is.False);cc.enabled=true;
        walk.enabled=false;Assert.That(Call(walk,"TryJump"),Is.False);walk.enabled=true;
        Set(walk,"jumpDelay",float.NaN);Set(walk,"jumpCooldown",float.PositiveInfinity);Assert.That(Call(walk,"TryJump"),Is.True);GravitySteps(5);
        Assert.That(Get<float>(walk,"jumpForce"),Is.GreaterThan(0));Assert.That(Get<float>(walk,"jumpCooldownRemaining"),Is.EqualTo(.15f));
        yield return null;
    }
    [UnityTest] public IEnumerator JumpApexIsStableAcrossPhysicsStepSizesAndHighGravityCannotSkipCeilings()
    {
        foreach(float dt in new[]{.01f,.02f,.05f})
        {
            JumpOnly();float start=player.transform.position.y,peak=start;Assert.That(Call(walk,"TryJump"),Is.True);Call(walk,"TickJumpRequest",0f);
            int steps=0;do { Call(walk,"TickVerticalMotion",dt);peak=Mathf.Max(peak,player.transform.position.y);Assert.That(++steps,Is.LessThan(400)); }
            while(Get<bool>(walk,"isJumping"));
            Assert.That(peak-start,Is.EqualTo(4).Within(.035f));Assert.That(cc.isGrounded,Is.True);
        }
        JumpOnly(25000);var ceiling=new GameObject("Real apex must be swept, not skipped");SceneManager.MoveGameObjectToScene(ceiling,scene);
        ceiling.transform.position=new Vector3(0,7.69f,0);ceiling.AddComponent<BoxCollider>().size=new Vector3(10,.2f,10);Physics.SyncTransforms();
        float before=player.transform.position.y;Assert.That(Call(walk,"TryJump"),Is.True);GravitySteps(1);
        Assert.That(Get<float>(walk,"jumpForce"),Is.Zero);Assert.That(player.transform.position.y-before,Is.GreaterThan(3.5f).And.LessThan(4f),"Native ceiling at 3.99m must be caught even though the full rise fits into one fixed step");
        yield return null;
    }
    void ScoutBiteSetup(float damage=100)
    {
        walk.enabled=false;gear.enabled=false;cc.enabled=false;player.transform.SetPositionAndRotation(new Vector3(.7f,1.8f,5.7f),Quaternion.identity);cc.enabled=true;
        var visual=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));
        SceneManager.MoveGameObjectToScene(visual,scene);visual.transform.SetParent(player.transform,false);visual.transform.localPosition=Vector3.down*1.8f;visual.transform.localScale=Vector3.one*2;
        biteModel=visual.transform;var animator=visual.GetComponent<Animator>();animator.Play("Held",0,0);animator.Update(0);animator.enabled=false;
        corpsePresentation=Add("ScoutPlayerRagdoll");Set(corpsePresentation,"visualRoot",biteModel);
        biteFeedback=Add("TitanSwordFeedback");biteFeedback.enabled=false;Set(biteFeedback,"effectMaterial",UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSwordImpact.mat"));biteFeedback.enabled=true;
        var floor=new GameObject("Owned real Scout bite floor");SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=Vector3.down*.25f;floor.AddComponent<BoxCollider>().size=new Vector3(80,.5f,80);
        biteTitan=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();SceneManager.MoveGameObjectToScene(biteTitan.gameObject,scene);
        biteTitan.GetComponent<TitanMotionStyle>().automaticTick=false;
        biteGrab=biteTitan.gameObject.AddComponent<TitanGrabAttack>();biteGrab.automaticTick=false;biteGrab.drawTelegraph=false;biteGrab.enableBite=true;biteGrab.biteDamage=damage;
        Physics.SyncTransforms();
    }
    void BiteFrame(float dt)
    {
        biteGrab.TickAttack(dt);biteTitan.animator.Update(0);biteGrab.ApplyPose();target.TickHold(dt);
    }
    void CaptureScout(bool left=true,bool aerial=false)
    {
        Assert.That(aerial?biteGrab.TryBeginAerialGrab(target,left):biteGrab.TryBeginGrab(target,left),Is.True);
        if(aerial){for(int i=0;i<40&&!target.IsHeld;i++)BiteFrame(1f/120);}else BiteFrame(.84f);
        Assert.That(target.IsHeldBy(biteGrab),Is.True,biteGrab.LastResult);
    }
    void FinishScoutBite()
    {
        for(int i=0;i<450 && biteGrab.BiteContactCount==0;i++)BiteFrame(1f/120);
        hp.CancelInvoke();
        Assert.That(biteGrab.BiteContactCount,Is.EqualTo(1),biteGrab.LastResult);
    }
    T Property<T>(object o,string name){return (T)o.GetType().GetProperty(name).GetValue(o);}
    IEnumerator RealScoutBite(bool left)
    {
        ScoutBiteSetup();CaptureScout(left);
        while(biteGrab.Stage!=TitanGrabStage.BiteWindup)BiteFrame(1f/120);
        Vector3 outside=target.transform.position;float outsideDepth=Vector3.Dot(outside-biteTitan.mouthSocket.position,biteTitan.transform.forward);
        // Wait through insertion, stopping before the closure contact event.
        for(int i=0;i<60 && biteGrab.Stage!=TitanGrabStage.Bite;i++)BiteFrame(1f/120);
        Assert.That(biteGrab.BiteContactCount,Is.Zero);Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);
        Assert.That(Vector3.Dot(target.transform.position-biteTitan.mouthSocket.position,biteTitan.transform.forward),Is.LessThan(outsideDepth-.3f),"Feed the torso inward before biting; don't just kill outside the lips");
        FinishScoutBite();Assert.That(biteGrab.BiteHitCount,Is.EqualTo(1),biteGrab.LastResult);Assert.That(Get<bool>(hp,"isDead"),Is.True);
        var corpse=Property<GameObject>(corpsePresentation,"Corpse");Assert.That(corpse,Is.Not.Null,Property<string>(corpsePresentation,"Error"));
        Assert.That(Property<Rigidbody[]>(corpsePresentation,"Bodies").Length,Is.EqualTo(13));Assert.That(corpse.GetComponentsInChildren<CharacterJoint>().Length,Is.EqualTo(12));
        Assert.That(Property<int>(corpsePresentation,"ActivationCount"),Is.EqualTo(1));Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.EqualTo(1));
        Assert.That(Vector3.Distance(corpse.transform.GetChild(0).position,Property<Vector3>(corpsePresentation,"CapturedPosition")),Is.LessThan(.0001f),"Corpse is at exact bite pose, not the safe controller-exit pose");
        Assert.That(corpse.transform.parent,Is.Null);Assert.That(player.transform.parent,Is.Null);Assert.That(walk.enabled||gear.enabled,Is.False);
        Assert.That(biteModel.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),Is.True);Assert.That(cc.enabled,Is.False);
        foreach(var skin in corpse.GetComponentsInChildren<SkinnedMeshRenderer>())Assert.That(skin.bones.All(b=>b.IsChildOf(corpse.transform)),Is.True,"Skin remains bound to copied physical bones");
        var alivePosition=corpse.transform.GetChild(0).position;player.transform.position+=Vector3.right*50;Call(corpsePresentation,"LateUpdate");
        Assert.That(Vector3.Distance(corpse.transform.GetChild(0).position,alivePosition),Is.LessThan(.0001f),"Pending release/motor root cannot drag corpse out of mouth");
        BiteFrame(5);Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.EqualTo(1));Assert.That(Property<int>(corpsePresentation,"ActivationCount"),Is.EqualTo(1));
        yield return null;
    }
    [UnityTest] public IEnumerator LeftHandFeedsRealScoutIntoMouthThenRagdollsAndBleedsOnce(){yield return RealScoutBite(true);}
    [UnityTest] public IEnumerator RightHandFeedsRealScoutIntoMouthThenRagdollsAndBleedsOnce(){yield return RealScoutBite(false);}
    [UnityTest] public IEnumerator NonlethalBiteBleedsWithoutDuplicatingHealthOrCreatingACorpse()
    {
        ScoutBiteSetup(40);CaptureScout();FinishScoutBite();
        Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(60));Assert.That(target.Health,Is.EqualTo(60));Assert.That(Get<bool>(hp,"isDead"),Is.False);
        Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.EqualTo(1));
        Assert.That(biteModel.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),Is.True);Assert.That(target.IsHeld,Is.False);yield return null;
    }
    [UnityTest] public IEnumerator EscapeBeforeChompNeverSpawnsBloodOrRagdoll()
    {
        ScoutBiteSetup();CaptureScout();BiteFrame(1.5f);target.EscapeHold();BiteFrame(5);
        Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(100));Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);
        Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator NewMouthObstructionCancelsRealBiteEffectsAndDamage()
    {
        ScoutBiteSetup();CaptureScout();while(biteGrab.Stage!=TitanGrabStage.BiteWindup)BiteFrame(1f/120);
        var wall=new GameObject("Solid at final feeding path");SceneManager.MoveGameObjectToScene(wall,scene);
        wall.transform.position=biteGrab.MouthBitePoint;wall.AddComponent<BoxCollider>().size=Vector3.one*2;Physics.SyncTransforms();FinishScoutBite();
        Assert.That(biteGrab.BiteHitCount,Is.Zero);Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(100));Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);
        Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);yield return null;
    }
    [UnityTest] public IEnumerator LongClothSurvivesRealBiteAsAnIndependentOwnedCorpseSnapshot()
    {
        ScoutBiteSetup();var cloth=Add("ScoutCapeCloth");Assert.That(Call(cloth,"Initialize",biteModel),Is.True);
        var live=Property<Mesh>(cloth,"FabricMesh");CaptureScout();FinishScoutBite();
        var corpse=Property<GameObject>(corpsePresentation,"Corpse");Assert.That(corpse,Is.Not.Null,Property<string>(corpsePresentation,"Error"));
        var snapshot=corpse.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Scout simulated fabric").sharedMesh;
        Assert.That(snapshot,Is.Not.SameAs(live));Assert.That(snapshot.vertexCount,Is.EqualTo(586));Assert.That(corpse.GetComponentsInChildren<CharacterJoint>().Length,Is.EqualTo(12));
        Assert.That(biteModel.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),Is.True);Object.Destroy(cloth);yield return null;
        Assert.That(snapshot!=null,Is.True);Assert.That(snapshot.vertices.Length,Is.EqualTo(586));Assert.That(corpse.transform.parent,Is.Null);
    }
    [UnityTest] public IEnumerator ActualScoutCorpsePhysicsHasCoincidentAnchorsAndNoLaunchExplosion()
    {
        ScoutBiteSetup();CaptureScout();FinishScoutBite();var corpse=Property<GameObject>(corpsePresentation,"Corpse");Assert.That(corpse,Is.Not.Null,Property<string>(corpsePresentation,"Error"));
        var bodies=Property<Rigidbody[]>(corpsePresentation,"Bodies");foreach(var joint in corpse.GetComponentsInChildren<CharacterJoint>())
            Assert.That(Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)),Is.LessThan(.002f));
        float max=0;for(int i=0;i<180;i++){scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in bodies)max=Mathf.Max(max,rb.velocity.magnitude);}
        Debug.Log("SCOUT_BITE_RAGDOLL max1_5sSpeed="+max);Assert.That(max,Is.LessThan(40),"No post-simulation velocity clipping or weakened death speed threshold");
        foreach(var rb in bodies){Assert.That(float.IsNaN(rb.position.x)||float.IsInfinity(rb.position.y),Is.False);Assert.That(rb.isKinematic,Is.False);}
        yield return null;
    }
    [UnityTest] public IEnumerator SceneScoutHealthAutomaticallyCreatesOnePassiveDeathAdapter()
    {
        ScoutBiteSetup();Object.DestroyImmediate(corpsePresentation);var driver=Add("ScoutPlayerAnimation");Set(driver,"model",biteModel);driver.enabled=false;
        var first=Call(hp,"EnsureScoutRagdoll");Assert.That(first,Is.Not.Null);Assert.That(Call(hp,"EnsureScoutRagdoll"),Is.SameAs(first));
        Assert.That(Property<GameObject>(first,"Corpse"),Is.Null);Assert.That(biteModel.GetComponentsInChildren<Rigidbody>().Length,Is.Zero,"No live Scout physics/motor changes before death");yield return null;
    }
    [UnityTest] public IEnumerator SolidSceneryAtCorpsePoseRefusesUnsafeDynamicHandoff()
    {
        ScoutBiteSetup();var wall=new GameObject("Invalid corpse birth solid");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=player.transform.position;wall.AddComponent<BoxCollider>().size=Vector3.one*8;Physics.SyncTransforms();
        Call(hp,"Die");hp.CancelInvoke();Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);
        StringAssert.Contains("intersects",Property<string>(corpsePresentation,"Error"));Assert.That(Property<Rigidbody[]>(corpsePresentation,"Bodies").Length,Is.Zero);yield return null;
    }
    IEnumerator ScaledRealScoutBite(float scale,bool left)
    {
        ScoutBiteSetup();biteTitan.transform.localScale=Vector3.one*scale;
        cc.enabled=false;player.transform.position=new Vector3(left?-.7f:.7f,8,2.6f)*scale;cc.enabled=true;Call(motor,"ResetMotionHistory");
        biteTitan.animator.Update(0);Physics.SyncTransforms();
        var upper=left?biteTitan.leftUpperArm:biteTitan.rightUpperArm;var lower=left?biteTitan.leftLowerArm:biteTitan.rightLowerArm;var hand=left?biteTitan.leftHand:biteTitan.rightHand;
        Assert.That(biteGrab.TryBeginAerialGrab(target,left),Is.True,"scale="+scale+" canWalk="+biteTitan.CanWalk+" canSuspend="+motor.CanSuspend+" plants="+Call(biteGrab,"CachePlants")+" head="+biteTitan.head.position+" target="+player.transform.position+" upper="+upper.position+" reach="+(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position))+" ray="+Call(biteGrab,"ClearRay",biteTitan.head.position,player.transform.position+Vector3.up,player.transform));
        var bones=new[]{left?biteTitan.leftUpperArm:biteTitan.rightUpperArm,left?biteTitan.leftLowerArm:biteTitan.rightLowerArm,left?biteTitan.leftHand:biteTitan.rightHand};
        var previous=bones.Select(b=>b.rotation).ToArray();float maximum=0,penetration=0,earlyPenetration=0,gripRadius=0;int steps=0;string worst="";bool gripRecorded=false;Vector3 localGrip=Vector3.zero;
        while(biteGrab.BiteContactCount==0 && biteGrab.IsBusy && steps++<500)
        {
            BiteFrame(1f/120);
            if(target.IsHeldBy(biteGrab))
            {
                var grip=left?biteTitan.leftGripSocket:biteTitan.rightGripSocket;
                if(!gripRecorded){gripRecorded=true;localGrip=player.transform.InverseTransformPoint(grip.position);gripRadius=Vector3.Distance(player.transform.position,grip.position);}
                Assert.That(Vector3.Distance(player.transform.TransformPoint(localGrip),grip.position),Is.LessThan(.003f));
                Assert.That(Vector3.Distance(player.transform.position,grip.position),Is.EqualTo(gripRadius).Within(.003f),"Feeding rotates around the same physical grip; never recenter/regrip");
            }
            for(int j=0;j<bones.Length;j++){maximum=Mathf.Max(maximum,Quaternion.Angle(previous[j],bones[j].rotation));previous[j]=bones[j].rotation;}
            var colliders=biteTitan.GetComponentsInChildren<Collider>();
            foreach(var arm in colliders)
            {
                if(!arm.enabled||arm.isTrigger||arm.attachedRigidbody==null||(arm.attachedRigidbody.transform!=bones[1]&&arm.attachedRigidbody.transform!=bones[2]))continue;
                foreach(var torso in colliders)
                {
                    if(!torso.enabled||torso.isTrigger||torso.attachedRigidbody==null||(torso.attachedRigidbody.name!="Chest"&&torso.attachedRigidbody.name!="Spine"))continue;
                    Vector3 normal;float depth;if(Physics.ComputePenetration(arm,arm.transform.position,arm.transform.rotation,torso,torso.transform.position,torso.transform.rotation,out normal,out depth))
                    {
                        // Match existing AerialCarryAndMouth clearance window:
                        // .9s onward, after the unchanged catch/takeover pose.
                        // Record early reach separately; don't claim it is clear.
                        if(biteGrab.Elapsed<.9f)earlyPenetration=Mathf.Max(earlyPenetration,depth);
                        else if(depth>penetration){penetration=depth;worst=" time="+biteGrab.Elapsed+" stage="+biteGrab.Stage+" part="+arm.name+" vs="+torso.name;}
                    }
                }
            }
        }
        hp.CancelInvoke();Debug.Log("SCOUT_REAL_MOUTH scale="+scale+" left="+left+" arm120Hz="+maximum+" carryMouthPenetration="+penetration+" unchangedEarlyReachPenetration="+earlyPenetration+worst);
        Assert.That(biteGrab.CaptureCount,Is.EqualTo(1));Assert.That(biteGrab.BiteHitCount,Is.EqualTo(1),biteGrab.LastResult);
        Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null,Property<string>(corpsePresentation,"Error"));
        Assert.That(maximum,Is.LessThan(8));Assert.That(penetration,Is.LessThan(.005f));yield return null;
    }
    [UnityTest] public IEnumerator ThreeTimesLeftScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(3,true);}
    [UnityTest] public IEnumerator ThreeTimesRightScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(3,false);}
    [UnityTest] public IEnumerator FiveTimesLeftScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(5,true);}
    [UnityTest] public IEnumerator FiveTimesRightScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(5,false);}
    [UnityTest] public IEnumerator ChompCanRenderRealScoutRagdollAndBloodInOwnedGraphicsPreview()
    {
        string folder=Environment.GetEnvironmentVariable("ODM_SCOUT_BITE_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"),"Never capture over a user's live Scene/Play");
        Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        var old=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var renderer=UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
        var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var rt=new RenderTexture(800,800,24);var texture=new Texture2D(800,800,TextureFormat.RGB24,false);
        var camera=new GameObject("Owned bite preview camera").AddComponent<Camera>();var light=new GameObject("Owned bite preview light").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(35,160,0);
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            ScoutBiteSetup();biteTitan.transform.localScale=Vector3.one*3;
            cc.enabled=false;player.transform.position=new Vector3(-2.1f,24,7.8f);cc.enabled=true;Call(motor,"ResetMotionHistory");Physics.SyncTransforms();
            camera.transform.position=new Vector3(7,33,18);camera.transform.LookAt(new Vector3(0,30.5f,2.7f));camera.fieldOfView=50;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.065f,.08f);camera.targetTexture=rt;
            // Batchmode doesn't resume WaitForEndOfFrame; warm the real camera
            // before capture, then read the previous completed frame on next Update.
            yield return null;yield return null;yield return null;CaptureScout(true,true);
            while(biteGrab.Stage!=TitanGrabStage.BiteWindup)BiteFrame(1f/120);
            yield return null;yield return null;SaveBiteFrame(rt,texture,System.IO.Path.Combine(folder,"ScoutBite_Approach.png"));
            while(biteGrab.Stage!=TitanGrabStage.Bite)BiteFrame(1f/120);
            yield return null;yield return null;SaveBiteFrame(rt,texture,System.IO.Path.Combine(folder,"ScoutBite_Inside.png"));
            FinishScoutBite();Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null,Property<string>(corpsePresentation,"Error"));
            yield return new WaitForSeconds(.12f);for(int i=0;i<12;i++)scene.GetPhysicsScene().Simulate(1f/120);
            yield return null;SaveBiteFrame(rt,texture,System.IO.Path.Combine(folder,"ScoutBite_Chomp.png"));
            Assert.That(texture.GetPixels32().Count(c=>c.r>65&&c.r>c.g*3&&c.r>c.b*3),Is.GreaterThan(15),"Actual red blood must render outside the mouth, not just a presentation counter");
            for(int i=0;i<36;i++)scene.GetPhysicsScene().Simulate(1f/120);
            yield return null;SaveBiteFrame(rt,texture,System.IO.Path.Combine(folder,"ScoutBite_Ragdoll.png"));
        }
        finally
        {
            camera.targetTexture=null;UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=old;QualitySettings.renderPipeline=oldQuality;
            Object.Destroy(pipeline);Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);Object.Destroy(rt);Object.Destroy(texture);
        }
    }
    static void SaveBiteFrame(RenderTexture rt,Texture2D texture,string path)
    {
        var old=RenderTexture.active;RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();RenderTexture.active=old;System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
    }
    [UnityTest] public IEnumerator ReleaseTransfersUpwardAndPlanarMotionWithoutResumingOldHooks()
    {
        Hold();Socket.position+=new Vector3(.4f,.2f,0);target.TickHold(.1f);target.EscapeHold();
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.GreaterThan(1));Assert.That(Get<float>(gear,"residualSpeed"),Is.GreaterThan(1));
        Assert.That(Get<bool>(gear,"leftGrappled")||Get<bool>(gear,"rightGrappled"),Is.False);yield return null;
    }
}
#endif
