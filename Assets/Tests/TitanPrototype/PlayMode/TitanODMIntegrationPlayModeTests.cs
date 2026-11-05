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
    GameObject player,owner,jumpFloor;
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
    [UnityTest] public IEnumerator ReleaseTransfersUpwardAndPlanarMotionWithoutResumingOldHooks()
    {
        Hold();Socket.position+=new Vector3(.4f,.2f,0);target.TickHold(.1f);target.EscapeHold();
        Assert.That(Get<float>(walk,"titanReleaseUp"),Is.GreaterThan(1));Assert.That(Get<float>(gear,"residualSpeed"),Is.GreaterThan(1));
        Assert.That(Get<bool>(gear,"leftGrappled")||Get<bool>(gear,"rightGrappled"),Is.False);yield return null;
    }
}
#endif
