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
    int EatenSoundCount {get{return Property<int>(player.GetComponent(Legacy("TitanODMHealthSource")),"EatenSoundCount");}}
    IEnumerator RealScoutBite(bool left)
    {
        ScoutBiteSetup();CaptureScout(left);
        while(biteGrab.Stage!=TitanGrabStage.BiteWindup)BiteFrame(1f/120);
        Vector3 outside=target.transform.position;float outsideDepth=Vector3.Dot(outside-biteTitan.mouthSocket.position,biteTitan.transform.forward);
        // Wait through insertion, stopping before the closure contact event.
        for(int i=0;i<60 && biteGrab.Stage!=TitanGrabStage.Bite;i++)BiteFrame(1f/120);
        Assert.That(biteGrab.BiteContactCount,Is.Zero);Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);Assert.That(EatenSoundCount,Is.Zero);
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
        BiteFrame(5);Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.EqualTo(1));Assert.That(Property<int>(corpsePresentation,"ActivationCount"),Is.EqualTo(1));Assert.That(EatenSoundCount,Is.EqualTo(1));
        yield return null;
    }
    [UnityTest] public IEnumerator LeftHandFeedsRealScoutIntoMouthThenRagdollsAndBleedsOnce(){yield return RealScoutBite(true);}
    [UnityTest] public IEnumerator RightHandFeedsRealScoutIntoMouthThenRagdollsAndBleedsOnce(){yield return RealScoutBite(false);}
    [UnityTest] public IEnumerator NonlethalBiteBleedsWithoutDuplicatingHealthOrCreatingACorpse()
    {
        ScoutBiteSetup(40);CaptureScout();FinishScoutBite();
        Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(60));Assert.That(target.Health,Is.EqualTo(60));Assert.That(Get<bool>(hp,"isDead"),Is.False);
        Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.EqualTo(1));
        Assert.That(biteModel.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),Is.True);Assert.That(target.IsHeld,Is.False);Assert.That(EatenSoundCount,Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator EscapeBeforeChompNeverSpawnsBloodOrRagdoll()
    {
        ScoutBiteSetup();CaptureScout();BiteFrame(1.5f);target.EscapeHold();BiteFrame(5);
        Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(100));Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);
        Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);Assert.That(EatenSoundCount,Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator NewMouthObstructionCancelsRealBiteEffectsAndDamage()
    {
        ScoutBiteSetup();CaptureScout();while(biteGrab.Stage!=TitanGrabStage.BiteWindup)BiteFrame(1f/120);
        var wall=new GameObject("Solid at final feeding path");SceneManager.MoveGameObjectToScene(wall,scene);
        wall.transform.position=biteGrab.MouthBitePoint;wall.AddComponent<BoxCollider>().size=Vector3.one*2;Physics.SyncTransforms();FinishScoutBite();
        Assert.That(biteGrab.BiteHitCount,Is.Zero);Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(100));Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);
        Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);Assert.That(EatenSoundCount,Is.Zero);yield return null;
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
    IEnumerator ScaledRealScoutBite(float scale,bool left,bool escapeWindow=false,bool escapeSuccess=false,float holdDrain=0)
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=holdDrain;var escape=escapeWindow?AddEscape():null;biteTitan.transform.localScale=Vector3.one*scale;
        cc.enabled=false;player.transform.position=new Vector3(left?-.7f:.7f,8,2.6f)*scale;cc.enabled=true;Call(motor,"ResetMotionHistory");
        biteTitan.animator.Update(0);Physics.SyncTransforms();
        var upper=left?biteTitan.leftUpperArm:biteTitan.rightUpperArm;var lower=left?biteTitan.leftLowerArm:biteTitan.rightLowerArm;var hand=left?biteTitan.leftHand:biteTitan.rightHand;
        Assert.That(biteGrab.TryBeginAerialGrab(target,left),Is.True,"scale="+scale+" canWalk="+biteTitan.CanWalk+" canSuspend="+motor.CanSuspend+" plants="+Call(biteGrab,"CachePlants")+" head="+biteTitan.head.position+" target="+player.transform.position+" upper="+upper.position+" reach="+(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position))+" ray="+Call(biteGrab,"ClearRay",biteTitan.head.position,player.transform.position+Vector3.up,player.transform));
        var bones=new[]{left?biteTitan.leftUpperArm:biteTitan.rightUpperArm,left?biteTitan.leftLowerArm:biteTitan.rightLowerArm,left?biteTitan.leftHand:biteTitan.rightHand};
        var previous=bones.Select(b=>b.rotation).ToArray();float maximum=0,penetration=0,earlyPenetration=0,gripRadius=0;int steps=0;string worst="";bool gripRecorded=false;Vector3 localGrip=Vector3.zero;float expectedHealth=100;
        while(biteGrab.BiteContactCount==0 && biteGrab.IsBusy && steps++<(escapeWindow?700:500))
        {
            BiteFrame(1f/120);
            if(escapeSuccess && target.IsHeldBy(biteGrab) && biteGrab.Elapsed>=1.5f){expectedHealth=100-holdDrain*(TitanGrabEscape.Window-escape.Remaining);WinEscape(escape);}
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
        hp.CancelInvoke();Debug.Log("SCOUT_REAL_MOUTH scale="+scale+" left="+left+" qte="+escapeWindow+" escape="+escapeSuccess+" arm120Hz="+maximum+" carryMouthPenetration="+penetration+" unchangedEarlyReachPenetration="+earlyPenetration+worst);
        Assert.That(biteGrab.CaptureCount,Is.EqualTo(1));
        if(escapeSuccess)
        {
            Assert.That(escape.State,Is.EqualTo(TitanGrabEscapeState.Succeeded));Assert.That(biteGrab.BiteHitCount,Is.Zero);Assert.That(target.Health,Is.EqualTo(expectedHealth).Within(.002f));
            Assert.That(target.ReleasePending,Is.False,"Arm recovery must eventually clear the actual supported-size escape");Assert.That(cc.enabled,Is.True);
            Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);Assert.That(EatenSoundCount,Is.Zero);
        }
        else
        {
            Assert.That(biteGrab.BiteHitCount,Is.EqualTo(1),biteGrab.LastResult);
            Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null,Property<string>(corpsePresentation,"Error"));
        }
        Assert.That(maximum,Is.LessThan(8));Assert.That(penetration,Is.LessThan(.005f));yield return null;
    }
    [UnityTest] public IEnumerator ThreeTimesLeftScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(3,true);}
    [UnityTest] public IEnumerator ThreeTimesRightScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(3,false);}
    [UnityTest] public IEnumerator FiveTimesLeftScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(5,true);}
    [UnityTest] public IEnumerator FiveTimesRightScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(5,false);}
    [UnityTest] public IEnumerator SevenTimesLeftScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(7,true);}
    [UnityTest] public IEnumerator SevenTimesRightScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(7,false);}
    [UnityTest] public IEnumerator TenTimesLeftScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(10,true);}
    [UnityTest] public IEnumerator TenTimesRightScoutFeedingPreservesStrictArmContinuityAndClearance(){yield return ScaledRealScoutBite(10,false);}
    [UnityTest] public IEnumerator PlayerHealthDamagePersistsAndRegenerationApiIsRemoved()
    {
        target.TakeDamage(60,player.transform.position);yield return null;Assert.That(target.Health,Is.EqualTo(40));
        Assert.That(hp.GetType().GetMethod("TickRegeneration"),Is.Null);Assert.That(hp.GetType().GetField("regenerationPerSecond"),Is.Null);
        target.TakeDamage(10,player.transform.position);yield return null;Assert.That(target.Health,Is.EqualTo(30));
    }
    [UnityTest] public IEnumerator PlayerHealthNeverHealsOnHoldReleasePauseOrDeathAndRejectsInvalidDamage()
    {
        target.TakeDamage(60,player.transform.position);Hold();yield return null;Assert.That(target.Health,Is.EqualTo(40));
        target.EscapeHold();yield return null;Assert.That(target.Health,Is.EqualTo(40));
        float old=Time.timeScale;try{Time.timeScale=0;yield return null;Assert.That(target.Health,Is.EqualTo(40));}finally{Time.timeScale=old;}
        foreach(float damage in new[]{-20f,float.NaN,float.PositiveInfinity,0f})Call(hp,"TakeDamage",damage);Assert.That(target.Health,Is.EqualTo(40));
        target.TakeDamage(100,player.transform.position);hp.CancelInvoke();yield return null;Assert.That(target.Health,Is.Zero);Assert.That(Get<bool>(hp,"isDead"),Is.True);
    }
    [UnityTest] public IEnumerator PlayerHealthDamagePersistsThroughBlockedRelease()
    {
        target.TakeDamage(60,player.transform.position);Hold();var wall=new GameObject("Blocked health release");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=player.transform.position;wall.AddComponent<BoxCollider>().size=Vector3.one*10;Physics.SyncTransforms();
        target.EscapeHold();Assert.That(target.ReleasePending,Is.True);yield return null;Assert.That(target.Health,Is.EqualTo(40));
        Object.DestroyImmediate(wall);Physics.SyncTransforms();target.TickHold(0);Assert.That(target.ReleasePending,Is.False);yield return null;Assert.That(target.Health,Is.EqualTo(40));
    }
    [TestCase(1f)] [TestCase(3f)] [TestCase(5f)] [TestCase(6f)] [TestCase(7f)] [TestCase(10f)]
    public void SizedStompDamagesActualControllerAndRealPlayerHealthInsteadOfASecondPool(float size)
    {
        ScoutBiteSetup();biteTitan.transform.localScale=Vector3.one*size;
        scene.GetRootGameObjects().Single(g=>g.name=="Owned real Scout bite floor").GetComponent<BoxCollider>().size=new Vector3(400,.5f,400);
        cc.enabled=false;player.transform.position=new Vector3(.7f*size,1.8f,5.7f*size);cc.enabled=true;
        var stomp=biteTitan.gameObject.AddComponent<TitanStompAttack>();stomp.automaticTick=false;stomp.damage=60;stomp.cooldownDuration=1.6f;Physics.SyncTransforms();
        Assert.That(stomp.TryBeginStomp(target,true),Is.True);stomp.TickAttack(.8f);
        Assert.That(Get<float>(hp,"currentHP"),Is.EqualTo(40));Assert.That(target.Health,Is.EqualTo(40));Assert.That(target.HitCount,Is.EqualTo(1));Assert.That(Get<bool>(hp,"showHealthBar"),Is.True);
        Assert.That(stomp.drawTelegraph || biteGrab.drawTelegraph,Is.False);Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null);Assert.That(Property<bool>(corpsePresentation,"Temporary"),Is.True);Assert.That(EatenSoundCount,Is.Zero);
        stomp.TickAttack(2);stomp.TickAttack(2);Assert.That(stomp.TryBeginStomp(target,false),Is.True);stomp.TickAttack(.8f);hp.CancelInvoke();
        Assert.That(target.Health,Is.Zero);Assert.That(Get<bool>(hp,"isDead"),Is.True,"Two gameplay stomps must kill a100HP player, even during knockdown");
        Assert.That(Property<bool>(corpsePresentation,"Temporary"),Is.False);Assert.That(Property<int>(corpsePresentation,"ActivationCount"),Is.EqualTo(1),"Death promotes the SAME moving ragdoll instead of spawning a second body");
        Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null,Property<string>(corpsePresentation,"Error"));Assert.That(EatenSoundCount,Is.Zero,"Stomp death is not a bite/eaten cue");
    }
    Component Reaction {get{return player.GetComponent(Legacy("ScoutStompReaction"));}}
    void CheckUniversalDeath()
    {
        var fx=player.GetComponent(Legacy("PlayerDeathFeedback"));Assert.That(fx,Is.Not.Null);
        Assert.That(Property<int>(fx,"DeathCount"),Is.EqualTo(1));Assert.That(Property<int>(fx,"BloodSoundCount"),Is.EqualTo(1));Assert.That(Property<int>(fx,"BoneSoundCount"),Is.EqualTo(1));
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(Property<AudioSource>(fx,"BloodSource").clip),Is.EqualTo("Assets/Audio/bloodSplat.mp3"));
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(Property<AudioSource>(fx,"BoneSource").clip),Is.EqualTo("Assets/Audio/boneCrush.wav"));
        Assert.That(Property<AudioSource>(fx,"BloodSource").outputAudioMixerGroup.name,Is.EqualTo("SFX"));Assert.That(Property<AudioSource>(fx,"BoneSource").outputAudioMixerGroup.name,Is.EqualTo("SFX"));
        Assert.That(Property<ParticleSystem>(fx,"Blood").particleCount,Is.GreaterThanOrEqualTo(90));
    }
    [UnityTest]public IEnumerator UniversalDeathGenericDamageAndDuplicateDieUseOneBloodBoneAndCorpse()
    {
        ScoutBiteSetup();var random=UnityEngine.Random.state;target.TakeDamage(100,player.transform.position);hp.CancelInvoke();Call(hp,"Die");CheckUniversalDeath();
        Assert.That(Property<int>(corpsePresentation,"ActivationCount"),Is.EqualTo(1));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));yield return null;
    }
    [TestCase(1f)][TestCase(3f)][TestCase(5f)][TestCase(10f)]
    public void UniversalDeathFirstLethalStompKeepsOriginalCorpseSpeedBound(float size)
    {
        ScoutBiteSetup();biteTitan.transform.localScale=Vector3.one*size;scene.GetRootGameObjects().Single(g=>g.name=="Owned real Scout bite floor").GetComponent<BoxCollider>().size=new Vector3(400,.5f,400);
        cc.enabled=false;player.transform.position=new Vector3(.7f*size,1.8f,5.7f*size);cc.enabled=true;var stomp=biteTitan.gameObject.AddComponent<TitanStompAttack>();stomp.automaticTick=false;stomp.damage=100;Physics.SyncTransforms();
        Assert.That(stomp.TryBeginStomp(target,true),Is.True);stomp.TickAttack(.8f);hp.CancelInvoke();CheckUniversalDeath();float peak=0;
        for(int i=0;i<180;i++){scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in Property<Rigidbody[]>(corpsePresentation,"Bodies"))peak=Mathf.Max(peak,rb.velocity.magnitude);}
        Assert.That(peak,Is.LessThan(40));Assert.That(Property<bool>(corpsePresentation,"Temporary"),Is.False);Debug.Log("PLAYER_LETHAL_STOMP scale="+size+" peak="+peak);
    }
    [UnityTest]public IEnumerator UniversalDeathBiteHasBothLayersWithoutADuplicateOldEatenSource()
    {
        ScoutBiteSetup();CaptureScout();FinishScoutBite();CheckUniversalDeath();Assert.That(EatenSoundCount,Is.EqualTo(1));
        Assert.That(scene.GetRootGameObjects().Any(g=>g.name=="[Titan SFX] Eaten"),Is.False);yield return null;
    }
    [UnityTest]public IEnumerator UniversalDeathSqueezeHasBothLayersAndNoFakeBiteContact()
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;AddEscape();CaptureScout();Set(hp,"currentHP",1f);BiteFrame(.2f);hp.CancelInvoke();CheckUniversalDeath();Assert.That(biteGrab.BiteHitCount,Is.Zero);yield return null;
    }
    [UnityTest]public IEnumerator UniversalDeathDirectHealthCallWhileHeldCapturesBeforeReleaseAndCannotResurrect()
    {
        ScoutBiteSetup();CaptureScout();BiteFrame(1);Call(hp,"Die");hp.CancelInvoke();CheckUniversalDeath();
        Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null,Property<string>(corpsePresentation,"Error"));Assert.That(target.IsHeld,Is.False);Call(hp,"OnReleased");Assert.That(walk.enabled||gear.enabled,Is.False);yield return null;
    }
    [UnityTest]public IEnumerator UniversalDeathVoidUsesHealthDeathInsteadOfBypassingItWithImmediateReload()
    {
        ScoutBiteSetup();var elements=Add("GameElements");Set(elements,"player",player.transform);player.transform.position=Vector3.down*26;Call(elements,"Update");hp.CancelInvoke();CheckUniversalDeath();Assert.That(Get<bool>(hp,"isDead"),Is.True);yield return null;
    }
    [UnityTest]public IEnumerator StompReactionNativeKnockbackFallsThenGetsUpWithOriginalControlFlags()
    {
        ScoutBiteSetup();Vector3 start=player.transform.position;target.TakeStompDamage(60,start-Vector3.forward*2,biteTitan.transform);
        Assert.That(target.Health,Is.EqualTo(40));Assert.That(Property<bool>(Reaction,"IsActive"),Is.True);Assert.That(motor.CanSuspend,Is.False);Assert.That(cc.enabled,Is.False);
        Assert.That(Property<bool>(corpsePresentation,"Temporary"),Is.True);Assert.That(Property<bool>(walk,"TitanSuspended"),Is.True);Assert.That(Property<bool>(gear,"TitanSuspended"),Is.True);
        yield return null;
    }
    [UnityTest]public IEnumerator StompReactionNativeRecoveryAndSpeedBound()
    {
        ScoutBiteSetup();Vector3 start=player.transform.position;target.TakeStompDamage(60,start-Vector3.forward*2,biteTitan.transform);float peak=0;
        for(int i=0;i<960&&Property<bool>(Reaction,"IsActive");i++)
        {
            scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in Property<Rigidbody[]>(corpsePresentation,"Bodies"))peak=Mathf.Max(peak,rb.velocity.magnitude);Call(Reaction,"TickReaction",1f/120);
        }
        Debug.Log("SCOUT_STOMP_REACTION peak="+peak+" travel="+(player.transform.position-start));Assert.That(peak,Is.LessThan(40));Assert.That(Property<bool>(Reaction,"IsActive"),Is.False,Property<string>(Reaction,"Error"));
        Assert.That(Vector3.Dot(player.transform.position-start,Vector3.forward),Is.GreaterThan(2));Assert.That(cc.enabled,Is.True);Assert.That(Property<bool>(walk,"TitanSuspended"),Is.False);Assert.That(Property<bool>(gear,"TitanSuspended"),Is.False);
        Assert.That(walk.enabled||gear.enabled,Is.False,"Restore original disabled fixture flags, not unconditionally enable gameplay");Assert.That(target.Health,Is.EqualTo(40));Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);yield return null;
    }
    void SettleReaction()
    {
        for(int i=0;i<360;i++){scene.GetPhysicsScene().Simulate(1f/120);Call(Reaction,"TickReaction",0f);}
        foreach(var rb in Property<Rigidbody[]>(corpsePresentation,"Bodies")){rb.velocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.isKinematic=true;}
    }
    [UnityTest]public IEnumerator StompReactionWaitsForWorldUpHeadroomAndCannotStandThroughACeiling()
    {
        ScoutBiteSetup();target.TakeStompDamage(60,player.transform.position-Vector3.forward*2,biteTitan.transform);SettleReaction();
        var ceiling=new GameObject("Blocked get-up headroom");SceneManager.MoveGameObjectToScene(ceiling,scene);ceiling.transform.position=Property<Transform>(corpsePresentation,"Hips").position+Vector3.up*2;
        ceiling.AddComponent<BoxCollider>().size=new Vector3(12,.2f,12);Physics.SyncTransforms();Vector3 before=player.transform.position;Call(Reaction,"TickReaction",3f);
        Assert.That(Property<bool>(Reaction,"IsActive"),Is.True);Assert.That(Property<bool>(Reaction,"GetUpPending"),Is.True);Assert.That(cc.enabled,Is.False);Assert.That(player.transform.position,Is.EqualTo(before));
        Object.DestroyImmediate(ceiling);Physics.SyncTransforms();Call(Reaction,"TickReaction",0f);Assert.That(Property<bool>(Reaction,"IsActive"),Is.False);Assert.That(cc.enabled,Is.True);yield return null;
    }
    [UnityTest]public IEnumerator StompReactionWholeGearClearanceAlsoBlocksStandUp()
    {
        ScoutBiteSetup();var box=new GameObject("Solid gear outside capsule");box.transform.SetParent(player.transform,false);box.transform.localPosition=Vector3.right*2;box.AddComponent<BoxCollider>().size=Vector3.one*.5f;
        target.TakeStompDamage(60,player.transform.position-Vector3.forward*2,biteTitan.transform);SettleReaction();
        var wall=new GameObject("Blocks only standing gear");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=new Vector3(player.transform.position.x+2,1.91f,player.transform.position.z);wall.AddComponent<BoxCollider>().size=Vector3.one*.5f;Physics.SyncTransforms();
        Call(Reaction,"TickReaction",3f);Assert.That(Property<bool>(Reaction,"GetUpPending"),Is.True);Assert.That(cc.enabled,Is.False);
        Object.DestroyImmediate(wall);Physics.SyncTransforms();Call(Reaction,"TickReaction",0f);Assert.That(Property<bool>(Reaction,"IsActive"),Is.False);Assert.That(box.GetComponent<BoxCollider>().enabled,Is.True);yield return null;
    }
    [UnityTest]public IEnumerator StompReactionCannotThrowThePhysicalPlayerThroughASolidWall()
    {
        ScoutBiteSetup();var wall=new GameObject("Native knockback wall");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=player.transform.position+Vector3.forward*4;wall.transform.position=new Vector3(wall.transform.position.x,4,wall.transform.position.z);wall.AddComponent<BoxCollider>().size=new Vector3(40,10,1);Physics.SyncTransforms();
        target.TakeStompDamage(60,player.transform.position-Vector3.forward*2,biteTitan.transform);float maxZ=float.NegativeInfinity,peak=0;
        for(int i=0;i<480&&Property<bool>(Reaction,"IsActive");i++)
        {
            scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in Property<Rigidbody[]>(corpsePresentation,"Bodies")){peak=Mathf.Max(peak,rb.velocity.magnitude);maxZ=Mathf.Max(maxZ,rb.position.z);}Call(Reaction,"TickReaction",1f/120);
        }
        Assert.That(peak,Is.LessThan(40));Assert.That(maxZ,Is.LessThan(wall.GetComponent<BoxCollider>().bounds.min.z+.05f));Assert.That(target.Health,Is.EqualTo(40));yield return null;
    }
    [UnityTest]public IEnumerator StompReactionDisabledComponentHandsSafeRecoveryToExistingMotorPoll()
    {
        ScoutBiteSetup();target.TakeStompDamage(60,player.transform.position-Vector3.forward*2,biteTitan.transform);SettleReaction();((Behaviour)Reaction).enabled=false;yield return null;yield return null;
        Assert.That(Property<bool>(Reaction,"IsActive"),Is.False);Assert.That(cc.enabled,Is.True);Assert.That(Property<bool>(walk,"TitanSuspended"),Is.False);Assert.That(walk.enabled||gear.enabled,Is.False);
    }
    [UnityTest]public IEnumerator StompReactionDestroyingTheReactionComponentStillFinishesSafeRecovery()
    {
        ScoutBiteSetup();target.TakeStompDamage(60,player.transform.position-Vector3.forward*2,biteTitan.transform);SettleReaction();Object.Destroy(Reaction);yield return null;yield return null;
        Assert.That(cc.enabled,Is.True);Assert.That(Property<bool>(walk,"TitanSuspended"),Is.False);Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);
    }
    [UnityTest]public IEnumerator StompReactionDeathPromotesSamePhysicalBodyAndNeverGetsBackUp()
    {
        ScoutBiteSetup();target.TakeStompDamage(60,player.transform.position-Vector3.forward*2,biteTitan.transform);var same=Property<GameObject>(corpsePresentation,"Corpse");
        target.TakeStompDamage(60,player.transform.position-Vector3.forward*2,biteTitan.transform);hp.CancelInvoke();CheckUniversalDeath();Call(Reaction,"TickReaction",100f);
        Assert.That(target.Health,Is.Zero);Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.SameAs(same));Assert.That(Property<int>(corpsePresentation,"ActivationCount"),Is.EqualTo(1));Assert.That(Property<bool>(corpsePresentation,"Temporary"),Is.False);
        Assert.That(cc.enabled,Is.False);Assert.That(walk.enabled||gear.enabled,Is.False);yield return null;
    }
    [UnityTest]public IEnumerator StompReactionRefusedUnsafeBirthCannotPoisonALaterDeathSnapshot()
    {
        ScoutBiteSetup();var obstruction=new GameObject("Unsafe live knockdown birth");SceneManager.MoveGameObjectToScene(obstruction,scene);obstruction.transform.position=player.transform.position;obstruction.AddComponent<BoxCollider>().size=Vector3.one*8;Physics.SyncTransforms();
        target.TakeStompDamage(10,player.transform.position-Vector3.forward*2,biteTitan.transform);Assert.That(target.Health,Is.EqualTo(90));Assert.That(Property<bool>(Reaction,"IsActive"),Is.False);Assert.That(cc.enabled,Is.True);Assert.That(motor.CanSuspend,Is.True);
        Object.DestroyImmediate(obstruction);cc.enabled=false;player.transform.position+=Vector3.forward*15;cc.enabled=true;Physics.SyncTransforms();Vector3 expected=biteModel.position;
        target.TakeDamage(100,player.transform.position);hp.CancelInvoke();Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null,Property<string>(corpsePresentation,"Error"));Assert.That(Vector3.Distance(Property<Vector3>(corpsePresentation,"CapturedPosition"),expected),Is.LessThan(.001f));yield return null;
    }
    [UnityTest]public IEnumerator StompReactionRepeatedDamageUsesSamePoolAndPhysicalShapesResolveToRealHealth()
    {
        ScoutBiteSetup();target.TakeStompDamage(10,player.transform.position-Vector3.forward*2,biteTitan.transform);target.TakeStompDamage(10,player.transform.position-Vector3.forward*2,biteTitan.transform);
        Assert.That(target.Health,Is.EqualTo(80));Assert.That(Property<int>(corpsePresentation,"ActivationCount"),Is.EqualTo(1));Assert.That(Property<int>(Reaction,"ReactionCount"),Is.EqualTo(2));
        var shape=Property<GameObject>(corpsePresentation,"Corpse").GetComponentInChildren<Collider>();Assert.That(TitanCombatProxy.Resolve(shape),Is.SameAs(target));
        float scale=Time.timeScale;try{Time.timeScale=0;Call(Reaction,"TickReaction",100f);Assert.That(Property<bool>(Reaction,"GetUpPending"),Is.False);}finally{Time.timeScale=scale;}yield return null;
    }
    [UnityTest]public IEnumerator PlayerStompAndUniversalDeathRenderInOwnedNativeGraphics()
    {
        string folder=Environment.GetEnvironmentVariable("ODM_PLAYER_IMPACT_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"));Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        var oldPipeline=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;float oldTime=Time.timeScale;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var renderer=UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var camera=new GameObject("Owned player impact camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.gameObject.AddComponent<AudioListener>();camera.fieldOfView=50;
        var light=new GameObject("Owned impact sun").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(45,-35,0);
        var material=new Material(Resources.Load<Material>("TitanStompChunks"));material.SetColor("_Color",new Color(.38f,.36f,.32f));
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;ScoutBiteSetup();var animator=biteModel.GetComponent<Animator>();animator.Play("Idle",0,0);animator.Update(0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);Object.DestroyImmediate(floor.GetComponent<Collider>());floor.transform.position=Vector3.down*.25f;floor.transform.localScale=new Vector3(80,.5f,80);floor.GetComponent<Renderer>().sharedMaterial=material;
            camera.transform.position=player.transform.position+new Vector3(-7,5,-12);camera.transform.LookAt(player.transform.position);
            var stomp=biteTitan.gameObject.AddComponent<TitanStompAttack>();stomp.automaticTick=false;stomp.damage=60;Physics.SyncTransforms();Assert.That(stomp.TryBeginStomp(target,true),Is.True);
            for(int i=0;i<97;i++){biteTitan.animator.Update(0);stomp.TickAttack(1f/120);stomp.ApplyPose();}Assert.That(Property<bool>(Reaction,"IsActive"),Is.True);Time.timeScale=0;
            float peak=0;for(int i=0;i<108;i++){scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in Property<Rigidbody[]>(corpsePresentation,"Bodies"))peak=Mathf.Max(peak,rb.velocity.magnitude);Call(Reaction,"TickReaction",0f);}
            camera.transform.position=player.transform.position+new Vector3(-7,5,-12);camera.transform.LookAt(Property<Transform>(corpsePresentation,"Hips").position);yield return null;yield return SaveHealthScreen(System.IO.Path.Combine(folder,"Stomp_Knockdown.png"));
            Time.timeScale=1;for(int i=0;i<960&&Property<bool>(Reaction,"IsActive");i++){scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in Property<Rigidbody[]>(corpsePresentation,"Bodies"))peak=Mathf.Max(peak,rb.velocity.magnitude);Call(Reaction,"TickReaction",1f/120);}
            Time.timeScale=0;Assert.That(Property<bool>(Reaction,"IsActive"),Is.False);Assert.That(target.Health,Is.EqualTo(40));Assert.That(cc.enabled,Is.True);Assert.That(peak,Is.LessThan(40));
            camera.transform.position=player.transform.position+new Vector3(-7,5,-12);camera.transform.LookAt(player.transform.position);yield return null;yield return SaveHealthScreen(System.IO.Path.Combine(folder,"Stomp_Recovered.png"));
            target.TakeDamage(100,player.transform.position);hp.CancelInvoke();CheckUniversalDeath();var fx=player.GetComponent(Legacy("PlayerDeathFeedback"));var blood=Property<ParticleSystem>(fx,"Blood");
            for(int i=0;i<14;i++){scene.GetPhysicsScene().Simulate(1f/120);fx.SendMessage("LateUpdate");blood.Simulate(1f/120,false,false);}
            camera.transform.LookAt(Property<Transform>(corpsePresentation,"Hips").position+Vector3.up*.3f);
            yield return null;yield return SaveHealthScreen(System.IO.Path.Combine(folder,"Death_Blood.png"));
            var image=new Texture2D(2,2);Assert.That(image.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,"Death_Blood.png"))),Is.True);var pixels=image.GetPixels32();int red=0;for(int i=0;i<pixels.Length-image.width*80;i++)if(pixels[i].r>35&&pixels[i].r>pixels[i].g*1.5f&&pixels[i].r>pixels[i].b*1.4f)red++;
            Object.Destroy(image);Assert.That(red,Is.GreaterThan(60),"Actual blood spray must render, not just increment an effect counter");
            for(int i=0;i<120;i++){scene.GetPhysicsScene().Simulate(1f/120);fx.SendMessage("LateUpdate");blood.Simulate(1f/120,false,false);}
            camera.transform.LookAt(Property<Transform>(corpsePresentation,"Hips").position+Vector3.up*.3f);yield return null;yield return SaveHealthScreen(System.IO.Path.Combine(folder,"Death_Fallen.png"));Debug.Log("PLAYER_IMPACT_NATIVE bloodPixels="+red+" peak="+peak+" device="+SystemInfo.graphicsDeviceName);
        }
        finally{Time.timeScale=oldTime;UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=oldPipeline;QualitySettings.renderPipeline=oldQuality;Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);Object.Destroy(material);Object.Destroy(pipeline);}
    }
    [UnityTest] public IEnumerator GrabDrainThreeTimesLeftPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(3,true,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainThreeTimesRightPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(3,false,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainFiveTimesLeftPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(5,true,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainFiveTimesRightPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(5,false,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainSevenTimesLeftPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(7,true,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainSevenTimesRightPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(7,false,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainTenTimesLeftPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(10,true,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainTenTimesRightPreservesArmGripAndClearance(){yield return ScaledRealScoutBite(10,false,true,true,10);}
    [UnityTest] public IEnumerator GrabDrainStartsOnlyOnCaptureAndSharesOneClockWithoutDoubleChargingOrPauseDebt()
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;AddEscape();Assert.That(target.Health,Is.EqualTo(100));CaptureScout();
        Assert.That(target.Health,Is.EqualTo(100),"No charging pre-capture time from the catch frame");
        biteGrab.TickAttack(1);target.TickHold(1);target.AdvanceHoldDamage(biteGrab,1);Assert.That(target.Health,Is.EqualTo(90).Within(.001f));
        biteGrab.TickAttack(.5f);target.TickHold(.5f);Assert.That(target.Health,Is.EqualTo(85).Within(.001f));
        float old=Time.timeScale;try{Time.timeScale=0;target.TickHold(1);Assert.That(target.Health,Is.EqualTo(85).Within(.001f));}finally{Time.timeScale=old;}
        biteGrab.TickAttack(.5f);target.TickHold(.5f);Assert.That(target.Health,Is.EqualTo(80).Within(.001f));yield return null;
    }
    [UnityTest] public IEnumerator GrabDrainStopsOnSuccessfulEscapeEvenWhenPhysicalReleaseIsBlocked()
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;var q=AddEscape();CaptureScout();BiteFrame(1);Assert.That(target.Health,Is.EqualTo(90).Within(.001f));
        var wall=new GameObject("Drain blocked exit");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=player.transform.position;wall.AddComponent<BoxCollider>().size=Vector3.one*10;Physics.SyncTransforms();
        WinEscape(q);Assert.That(target.ReleasePending,Is.True);BiteFrame(10);Assert.That(target.Health,Is.EqualTo(90).Within(.001f));Assert.That(biteGrab.BiteHitCount,Is.Zero);Assert.That(EatenSoundCount,Is.Zero);
        Object.DestroyImmediate(wall);Physics.SyncTransforms();FinishEscapeRelease();target.TickHold(20);Assert.That(target.Health,Is.EqualTo(90).Within(.001f));yield return null;
    }
    [UnityTest] public IEnumerator GrabDrainAccumulatesAcrossEscapesAndNeverRefillsHealth()
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;var q=AddEscape();
        for(int attempt=0;attempt<2;attempt++)
        {
            if(attempt>0){biteGrab.TickAttack(4);target.TickHold(4);cc.enabled=false;player.transform.SetPositionAndRotation(new Vector3(.7f,1.8f,5.7f),Quaternion.identity);cc.enabled=true;biteTitan.animator.Play("Idle",0,0);biteTitan.animator.Update(0);Physics.SyncTransforms();}
            CaptureScout();BiteFrame(1);WinEscape(q);FinishEscapeRelease();Assert.That(target.Health,Is.EqualTo(90-10*attempt).Within(.001f));
        }
        yield return null;
    }
    [UnityTest] public IEnumerator GrabDrainCancellationCannotLeaveDamageOrBloodCallbacksBehind()
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;AddEscape();CaptureScout();BiteFrame(.5f);float hpBefore=target.Health;
        biteTitan.StunEyes();Assert.That(target.IsHeld,Is.False);target.AdvanceHoldDamage(biteGrab,100);BiteFrame(100);Assert.That(target.Health,Is.EqualTo(hpBefore));
        Assert.That(EatenSoundCount,Is.Zero);Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator GrabDrainLethalSqueezeCapturesIndependentCorpseBeforeReleaseWithoutBiteEffects()
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;AddEscape();Set(hp,"currentHP",1f);CaptureScout();BiteFrame(.12f);hp.CancelInvoke();
        Assert.That(target.IsDead,Is.True);Assert.That(target.IsHeld,Is.False);Assert.That(biteGrab.BiteHitCount,Is.Zero);Assert.That(EatenSoundCount,Is.Zero);Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);
        var corpse=Property<GameObject>(corpsePresentation,"Corpse");Assert.That(corpse,Is.Not.Null,Property<string>(corpsePresentation,"Error"));Assert.That(corpse.transform.IsChildOf(biteTitan.transform),Is.False);
        var bodies=Property<Rigidbody[]>(corpsePresentation,"Bodies");float peak=0;
        for(int i=0;i<180;i++){scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in bodies)peak=Mathf.Max(peak,rb.velocity.magnitude);}
        Assert.That(peak,Is.LessThan(40));Debug.Log("GRAB_DRAIN_CORPSE peak="+peak);yield return null;
    }
    [UnityTest] public IEnumerator GrabDrainThreeTimesLeftSqueezeCorpseKeepsOriginalSpeedBound(){yield return ScaledSqueezeCorpse(3,true);}
    [UnityTest] public IEnumerator GrabDrainThreeTimesRightSqueezeCorpseKeepsOriginalSpeedBound(){yield return ScaledSqueezeCorpse(3,false);}
    [UnityTest] public IEnumerator GrabDrainFiveTimesLeftSqueezeCorpseKeepsOriginalSpeedBound(){yield return ScaledSqueezeCorpse(5,true);}
    [UnityTest] public IEnumerator GrabDrainFiveTimesRightSqueezeCorpseKeepsOriginalSpeedBound(){yield return ScaledSqueezeCorpse(5,false);}
    [UnityTest] public IEnumerator GrabDrainTenTimesLeftSqueezeCorpseKeepsOriginalSpeedBound(){yield return ScaledSqueezeCorpse(10,true);}
    [UnityTest] public IEnumerator GrabDrainTenTimesRightSqueezeCorpseKeepsOriginalSpeedBound(){yield return ScaledSqueezeCorpse(10,false);}
    IEnumerator ScaledSqueezeCorpse(float scale,bool left)
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;AddEscape();biteTitan.transform.localScale=Vector3.one*scale;
        cc.enabled=false;player.transform.position=new Vector3(left?-.7f:.7f,8,2.6f)*scale;cc.enabled=true;Call(motor,"ResetMotionHistory");biteTitan.animator.Update(0);Physics.SyncTransforms();
        Assert.That(biteGrab.TryBeginAerialGrab(target,left),Is.True);for(int i=0;i<50&&!target.IsHeld;i++)BiteFrame(1f/120);Assert.That(target.IsHeld,Is.True);
        Set(hp,"currentHP",1f);BiteFrame(.12f);hp.CancelInvoke();Assert.That(target.IsDead,Is.True);Assert.That(target.IsHeld,Is.False);Assert.That(biteGrab.BiteHitCount,Is.Zero);Assert.That(EatenSoundCount,Is.Zero);Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);
        var corpse=Property<GameObject>(corpsePresentation,"Corpse");Assert.That(corpse,Is.Not.Null,Property<string>(corpsePresentation,"Error"));Assert.That(corpse.transform.parent,Is.Null);
        var bodies=Property<Rigidbody[]>(corpsePresentation,"Bodies");float peak=0;
        for(int i=0;i<180;i++){scene.GetPhysicsScene().Simulate(1f/120);foreach(var rb in bodies)peak=Mathf.Max(peak,rb.velocity.magnitude);}
        Assert.That(peak,Is.LessThan(40));Debug.Log("GRAB_DRAIN_SCALED_CORPSE scale="+scale+" left="+left+" peak="+peak);yield return null;
    }
    [UnityTest] public IEnumerator GrabDrainTimeoutRetainsActualBiteAndCannotChargeTimeAfterItsEarlierContact()
    {
        ScoutBiteSetup();biteGrab.holdDamagePerSecond=10;AddEscape();CaptureScout();BiteFrame(1);Assert.That(target.Health,Is.EqualTo(90).Within(.001f));
        FinishScoutBite();Assert.That(Get<bool>(hp,"isDead"),Is.True);Assert.That(biteGrab.BiteHitCount,Is.EqualTo(1));Assert.That(EatenSoundCount,Is.EqualTo(1));yield return null;
    }
    TitanGrabEscape AddEscape()
    {var q=player.AddComponent<TitanGrabEscape>();q.automaticInput=false;q.ResetVariation(241);return q;}
    void EscapePress(TitanGrabEscape q,KeyCode key)
    {q.SampleButtons(0);q.SampleButtons(TitanGrabEscape.Button(key));}
    void WinEscape(TitanGrabEscape q)
    {for(int i=0;i<3;i++)EscapePress(q,q.CurrentKey);Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Succeeded));}
    void FinishEscapeRelease()
    {
        biteGrab.TickAttack(1);biteTitan.animator.Update(0);biteGrab.ApplyPose();target.TickHold(0);
        Assert.That(target.ReleasePending,Is.False);Assert.That(cc.enabled,Is.True);
    }
    [UnityTest] public IEnumerator EscapeQteStartsOnlyAtActualCaptureAndOffersThreeSecondsEvenAfterALongCatchFrame()
    {
        ScoutBiteSetup();var q=AddEscape();Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Idle));
        Assert.That(biteGrab.TryBeginGrab(target,true),Is.True);Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Idle));
        BiteFrame(10);Assert.That(target.IsHeldBy(biteGrab),Is.True);Assert.That(q.Remaining,Is.EqualTo(3));Assert.That(biteGrab.BiteContactCount,Is.Zero);
        // Attack logical time is bounded independently of the captured frame's old dt.
        q.Advance(2.999f);Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Running));q.Advance(3);Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Failed));
        yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteWrongKeysResetProgressNotClockAndHeldKeysOrMashingCannotWin()
    {
        ScoutBiteSetup();var q=AddEscape();q.SampleButtons(63);CaptureScout();
        q.SampleButtons(63);Assert.That(q.Progress,Is.Zero,"Pre-capture held buttons are not presses");
        var first=q.CurrentKey;EscapePress(q,first);Assert.That(q.Progress,Is.EqualTo(1));Assert.That(q.CurrentKey,Is.Not.EqualTo(first));
        q.SampleButtons(TitanGrabEscape.Button(first));Assert.That(q.Progress,Is.EqualTo(1),"Holding does not advance");
        q.Advance(2);EscapePress(q,q.CurrentKey==KeyCode.W?KeyCode.A:KeyCode.W);Assert.That(q.Progress,Is.Zero);Assert.That(q.Remaining,Is.EqualTo(1));
        EscapePress(q,q.CurrentKey);q.SampleButtons(0);q.SampleButtons(63);Assert.That(q.Progress,Is.Zero);Assert.That(q.Remaining,Is.EqualTo(1));
        Assert.That(q.WrongPresses,Is.EqualTo(2));Assert.That(q.CurrentKey,Is.EqualTo(first));WinEscape(q);Assert.That(target.Health,Is.EqualTo(100));yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteSuccessCancelsBiteRecoversArmAndLaunchesOnlyAfterNativeClearance()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();BiteFrame(.6f);WinEscape(q);
        Assert.That(target.IsHeld,Is.False);Assert.That(biteGrab.Stage,Is.EqualTo(TitanGrabStage.Recovery));FinishEscapeRelease();
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.EqualTo(6));Assert.That(Get<float>(gear,"residualSpeed"),Is.EqualTo(6).Within(.001f));
        Assert.That(player.transform.parent,Is.Null);Assert.That(Get<bool>(gear,"leftGrappled")||Get<bool>(gear,"rightGrappled"),Is.False);
        BiteFrame(10);Assert.That(target.Health,Is.EqualTo(100));Assert.That(biteGrab.BiteContactCount,Is.Zero);Assert.That(EatenSoundCount,Is.Zero);
        Assert.That(Property<int>(biteFeedback,"BiteBloodCount"),Is.Zero);Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Null);yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteBlockedExitCancelsBiteImmediatelyAndKeepsSuspensionUntilSceneryClears()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();
        var wall=new GameObject("Escape obstruction");SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=player.transform.position;wall.AddComponent<BoxCollider>().size=Vector3.one*10;Physics.SyncTransforms();
        WinEscape(q);Assert.That(target.ReleasePending,Is.True);Assert.That(target.IsHeld,Is.False);Assert.That(cc.enabled,Is.False);Assert.That(Get<bool>(walk,"titanSuspended"),Is.True);
        BiteFrame(5);Assert.That(target.Health,Is.EqualTo(100));Assert.That(biteGrab.BiteContactCount,Is.Zero);Assert.That(target.GrabGraceRemaining,Is.EqualTo(1));
        Object.DestroyImmediate(wall);Physics.SyncTransforms();FinishEscapeRelease();Assert.That(Get<float>(walk,"titanReleaseUp"),Is.EqualTo(6));yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteGraceBlocksAllHandsForOneSecondButNeverBlocksDamage()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();WinEscape(q);FinishEscapeRelease();
        Assert.That(target.GrabGraceRemaining,Is.EqualTo(1));Assert.That(target.TryHold(owner.GetComponent<TitanCombatTarget>(),Socket,true),Is.False);
        target.TakeDamage(10,player.transform.position);Assert.That(target.Health,Is.EqualTo(90));target.TickHold(.99f);Assert.That(target.IsGrabImmune,Is.True);
        target.TickHold(.02f);Assert.That(target.IsGrabImmune,Is.False);Assert.That(target.TryHold(owner.GetComponent<TitanCombatTarget>(),Socket,true),Is.True);target.EscapeHold();yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteFinalButtonsStayQuarantinedUntilReleasedAndDoNotDisableNormalControls()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();var first=q.CurrentKey;EscapePress(q,first);EscapePress(q,q.CurrentKey);var final=q.CurrentKey;EscapePress(q,final);FinishEscapeRelease();
        Assert.That(q.BlocksKey(final),Is.True);Assert.That(q.FilterAxis(1,final==KeyCode.A?KeyCode.D:KeyCode.A,final),Is.Zero);
        q.SampleButtons(0);Assert.That(q.BlocksKey(final),Is.False);Assert.That(q.FilterAxis(1,KeyCode.A,KeyCode.D),Is.EqualTo(1));yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteDeadlineRejectsLateThirdPressAndFailureStillUsesConfirmedBiteDeath()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();EscapePress(q,q.CurrentKey);EscapePress(q,q.CurrentKey);BiteFrame(3);EscapePress(q,KeyCode.W);
        Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Failed));FinishScoutBite();Assert.That(biteGrab.BiteHitCount,Is.EqualTo(1));Assert.That(target.IsDead,Is.True);
        Assert.That(EatenSoundCount,Is.EqualTo(1));Assert.That(Property<GameObject>(corpsePresentation,"Corpse"),Is.Not.Null);yield return null;
    }
    [UnityTest] public IEnumerator EscapeQtePausesWithGameAndDisableOrInjuryCannotLeaveAStaleChallenge()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();float scale=Time.timeScale;
        try{Time.timeScale=0;EscapePress(q,q.CurrentKey);Assert.That(q.Progress,Is.Zero);Assert.That(q.Remaining,Is.EqualTo(3));}finally{Time.timeScale=scale;}
        biteTitan.ApplyDamage(TitanHitRegion.LeftArm,1000,biteTitan.leftHand.position,Vector3.zero);
        Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Cancelled));Assert.That(target.IsHeld,Is.False);BiteFrame(10);Assert.That(target.Health,Is.EqualTo(100));yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteComponentDisableReleasesSafelyAndCannotBiteLater()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();q.enabled=false;Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Cancelled));Assert.That(target.IsHeld,Is.False);
        BiteFrame(10);Assert.That(target.Health,Is.EqualTo(100));Assert.That(biteGrab.BiteContactCount,Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteUsesAllSixKeysWithoutMutatingUnityGameplayRandom()
    {
        ScoutBiteSetup();var q=AddEscape();var state=UnityEngine.Random.state;var seen=new System.Collections.Generic.HashSet<KeyCode>();
        for(int n=0;n<24;n++)
        {
            player.transform.position=new Vector3(.7f,1.8f,5.7f);biteGrab.TickAttack(10);target.TickHold(10);CaptureScout();
            for(int i=0;i<3;i++){seen.Add(q.CurrentKey);EscapePress(q,q.CurrentKey);}FinishEscapeRelease();
        }
        Assert.That(seen,Is.EquivalentTo(new[]{KeyCode.W,KeyCode.A,KeyCode.S,KeyCode.D,KeyCode.Q,KeyCode.E}));Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(JsonUtility.ToJson(state)));yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteSceneInstallationIsIdempotentAndLeavesPassiveTitanPrefabAlone()
    {
        TitanGrabEscape.Install(player);TitanGrabEscape.Install(player);Assert.That(player.GetComponents<TitanGrabEscape>().Length,Is.EqualTo(1));
        var q=player.GetComponent<TitanGrabEscape>();Assert.That(q.automaticInput && q.showHud,Is.True);Assert.That(q.State,Is.EqualTo(TitanGrabEscapeState.Idle));
        var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");Assert.That(prefab.GetComponentsInChildren<TitanGrabEscape>(true),Is.Empty);yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteCanCompleteJustBeforeDeadlineEvenWithShortConfiguredHoldLease()
    {
        ScoutBiteSetup();var q=AddEscape();target.maximumHoldDuration=.5f;CaptureScout();BiteFrame(2.999f);
        Assert.That(target.IsHeldBy(biteGrab),Is.True);Assert.That(q.Remaining,Is.GreaterThan(0).And.LessThan(.002f));WinEscape(q);FinishEscapeRelease();
        BiteFrame(10);Assert.That(target.Health,Is.EqualTo(100));Assert.That(biteGrab.BiteContactCount,Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteJumpUsesNativeCeilingCollisionRatherThanMovingThroughScenery()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();BiteFrame(.6f);WinEscape(q);FinishEscapeRelease();
        Vector3 before=player.transform.position;var ceiling=new GameObject("Escape jump ceiling");SceneManager.MoveGameObjectToScene(ceiling,scene);
        ceiling.transform.position=before+Vector3.up*(cc.height*.5f+.15f);ceiling.AddComponent<BoxCollider>().size=new Vector3(20,.1f,20);Physics.SyncTransforms();
        Call(walk,"TickVerticalMotion",.2f);Assert.That(player.transform.position.y-before.y,Is.LessThan(.2f));Assert.That(Get<float>(walk,"titanReleaseUp"),Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteDeathWhileReleaseIsPendingNeverLaunchesOrResurrectsPlayer()
    {
        ScoutBiteSetup();var q=AddEscape();CaptureScout();var wall=new GameObject("Blocked escape before external death");SceneManager.MoveGameObjectToScene(wall,scene);
        wall.transform.position=player.transform.position;wall.AddComponent<BoxCollider>().size=Vector3.one*10;Physics.SyncTransforms();WinEscape(q);Assert.That(target.ReleasePending,Is.True);
        target.TakeDamage(100,player.transform.position);hp.CancelInvoke();Assert.That(target.IsDead,Is.True);Object.DestroyImmediate(wall);Physics.SyncTransforms();
        biteGrab.TickAttack(1);biteTitan.animator.Update(0);biteGrab.ApplyPose();target.TickHold(0);
        Assert.That(target.ReleasePending,Is.False);Assert.That(cc.enabled,Is.False,"Do not overwrite corpse-owned dead physics on delayed completion");
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.Zero);Assert.That(walk.enabled||gear.enabled,Is.False);Assert.That(target.Health,Is.Zero);Assert.That(EatenSoundCount,Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator EscapeQteThreeTimesLeftTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(3,true,true);}
    [UnityTest] public IEnumerator EscapeQteThreeTimesRightTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(3,false,true);}
    [UnityTest] public IEnumerator EscapeQteFiveTimesLeftTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(5,true,true);}
    [UnityTest] public IEnumerator EscapeQteFiveTimesRightTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(5,false,true);}
    [UnityTest] public IEnumerator EscapeQteThreeTimesLeftSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(3,true,true,true);}
    [UnityTest] public IEnumerator EscapeQteThreeTimesRightSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(3,false,true,true);}
    [UnityTest] public IEnumerator EscapeQteFiveTimesLeftSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(5,true,true,true);}
    [UnityTest] public IEnumerator EscapeQteFiveTimesRightSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(5,false,true,true);}
    [UnityTest] public IEnumerator EscapeQteSevenTimesLeftTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(7,true,true);}
    [UnityTest] public IEnumerator EscapeQteSevenTimesRightTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(7,false,true);}
    [UnityTest] public IEnumerator EscapeQteTenTimesLeftTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(10,true,true);}
    [UnityTest] public IEnumerator EscapeQteTenTimesRightTimeoutKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(10,false,true);}
    [UnityTest] public IEnumerator EscapeQteSevenTimesLeftSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(7,true,true,true);}
    [UnityTest] public IEnumerator EscapeQteSevenTimesRightSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(7,false,true,true);}
    [UnityTest] public IEnumerator EscapeQteTenTimesLeftSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(10,true,true,true);}
    [UnityTest] public IEnumerator EscapeQteTenTimesRightSuccessKeepsStrictArmAndMouthBounds(){yield return ScaledRealScoutBite(10,false,true,true);}
    [UnityTest] public IEnumerator EscapeQteHudRendersRealPromptsAndOutcomesInOwnedNativeGraphics()
    {
        string folder=Environment.GetEnvironmentVariable("ODM_GRAB_ESCAPE_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"));Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        float oldTime=Time.timeScale;var oldPipeline=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var renderer=UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
        var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var camera=new GameObject("Owned QTE review camera").AddComponent<Camera>();camera.gameObject.AddComponent<AudioListener>();var light=new GameObject("Owned QTE review sun").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,160,0);
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            ScoutBiteSetup();var q=AddEscape();CaptureScout();BiteFrame(.6f);Time.timeScale=0;
            camera.transform.position=player.transform.position+new Vector3(6,4,10);camera.transform.LookAt(player.transform.position);camera.fieldOfView=55;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.065f,.08f);
            yield return null;yield return null;yield return null;
            yield return SaveEscapeScreen(System.IO.Path.Combine(folder,"GrabEscape_Prompt.png"));
            Time.timeScale=1;EscapePress(q,q.CurrentKey);q.Advance(1);Time.timeScale=0;
            yield return null;yield return null;yield return SaveEscapeScreen(System.IO.Path.Combine(folder,"GrabEscape_Progress.png"));
            Time.timeScale=1;q.SampleButtons(0);q.SampleButtons(63);Time.timeScale=0;
            yield return null;yield return null;yield return SaveEscapeScreen(System.IO.Path.Combine(folder,"GrabEscape_Wrong.png"));
            Time.timeScale=1;WinEscape(q);Time.timeScale=0;
            yield return null;yield return null;yield return SaveEscapeScreen(System.IO.Path.Combine(folder,"GrabEscape_Success.png"));
            Debug.Log("GRAB_ESCAPE_NATIVE "+SystemInfo.graphicsDeviceName+" "+SystemInfo.graphicsDeviceType+" "+Screen.width+"x"+Screen.height);
        }
        finally
        {
            Time.timeScale=oldTime;UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=oldPipeline;QualitySettings.renderPipeline=oldQuality;
            Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);Object.Destroy(pipeline);
        }
    }
    static IEnumerator SaveEscapeScreen(string path)
    {
        // Native screenshot request schedules its own completed-frame readback;
        // never wait for a batchmode WaitForEndOfFrame that cannot resume.
        if(System.IO.File.Exists(path))System.IO.File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        for(int i=0;i<120 && !System.IO.File.Exists(path);i++)yield return null;
        Assert.That(System.IO.File.Exists(path),Is.True,"No actual completed-frame HUD screenshot");
        var image=new Texture2D(2,2);image.LoadImage(System.IO.File.ReadAllBytes(path));
        Assert.That(image.GetPixels32().Count(c=>(c.r>170 && c.g>150 && c.b<120)||(c.g>170 && c.r<160 && c.b<190)||(c.r>170 && c.g<140 && c.b<140)),Is.GreaterThan(20),"Actual gold prompt/time, red reset or green progress pixels must render");
        Object.Destroy(image);
    }
    [UnityTest] public IEnumerator PlayerHealthBarRendersPersistentDamageInOwnedNativeGraphics()
    {
        string folder=Environment.GetEnvironmentVariable("ODM_PLAYER_HEALTH_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"));Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        float oldTime=Time.timeScale;var oldPipeline=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var renderer=UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
        var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var camera=new GameObject("Owned health review camera").AddComponent<Camera>();camera.gameObject.AddComponent<AudioListener>();var light=new GameObject("Owned health review sun").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,160,0);
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;ScoutBiteSetup();Time.timeScale=0;
            camera.transform.position=player.transform.position+new Vector3(6,4,10);camera.transform.LookAt(player.transform.position);camera.fieldOfView=55;camera.backgroundColor=new Color(.04f,.065f,.08f);
            yield return null;yield return null;yield return SaveHealthScreen(System.IO.Path.Combine(folder,"Health_Full.png"));
            target.TakeDamage(60,player.transform.position);yield return null;yield return SaveHealthScreen(System.IO.Path.Combine(folder,"Health_StompDamage.png"));
            Time.timeScale=1;yield return new WaitForSeconds(8f);Time.timeScale=0;Assert.That(target.Health,Is.EqualTo(40));
            yield return null;yield return SaveHealthScreen(System.IO.Path.Combine(folder,"Health_Persistent.png"));
            Debug.Log("PLAYER_HEALTH_NATIVE "+SystemInfo.graphicsDeviceName+" "+SystemInfo.graphicsDeviceType+" "+Screen.width+"x"+Screen.height);
        }
        finally{Time.timeScale=oldTime;UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=oldPipeline;QualitySettings.renderPipeline=oldQuality;Object.Destroy(camera.gameObject);Object.Destroy(light.gameObject);Object.Destroy(pipeline);}
    }
    static IEnumerator SaveHealthScreen(string path)
    {
        if(System.IO.File.Exists(path))System.IO.File.Delete(path);ScreenCapture.CaptureScreenshot(path);
        for(int i=0;i<120&&!System.IO.File.Exists(path);i++)yield return null;Assert.That(System.IO.File.Exists(path),Is.True);
        var image=new Texture2D(2,2);image.LoadImage(System.IO.File.ReadAllBytes(path));int colored=0;
        for(int y=image.height-70;y<image.height-10;y++)for(int x=10;x<Mathf.Min(image.width,400);x++)
        {var c=image.GetPixel(x,y);if(Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b)>.2f && Mathf.Max(c.r,c.g,c.b)>.4f)colored++;}
        Object.Destroy(image);Assert.That(colored,Is.GreaterThan(100),"Actual top-left health fill must render, not just a health counter");
    }
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
