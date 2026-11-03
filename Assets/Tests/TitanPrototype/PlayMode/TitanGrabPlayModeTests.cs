#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class TitanGrabPlayModeTests
{
    Scene scene, previous;
    ModularTitanBody body;
    TitanGrabAttack grab;
    TitanCombatTarget human;
    Vector3 origin;
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previous = SceneManager.GetActiveScene();
        scene = SceneManager.CreateScene("Titan_Grab_Test", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SceneManager.SetActiveScene(scene);
        var floor = new GameObject("Grab floor"); floor.transform.position = Vector3.down * .25f;
        floor.AddComponent<BoxCollider>().size = new Vector3(80,.5f,80);
        SpawnBody();
        var go = new GameObject("Grab human"); go.transform.position = origin = new Vector3(.7f,0,5.7f);
        var box = go.AddComponent<BoxCollider>(); box.center = Vector3.up; box.size = new Vector3(.8f,2,.8f);
        human = go.AddComponent<TitanCombatTarget>();
        SceneManager.SetActiveScene(previous);
        yield return null;
        Physics.SyncTransforms();
    }
    void SpawnBody()
    {
        body = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
        if(body.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(body.gameObject, scene);
        body.GetComponent<TitanMotionStyle>().automaticTick = false;
        grab = body.gameObject.AddComponent<TitanGrabAttack>(); grab.automaticTick = false; grab.drawTelegraph = false;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(previous);
        if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }
    void Begin(bool left = true)
    {
        body.animator.Update(0); Physics.SyncTransforms();
        Assert.That(grab.TryBeginGrab(human, left), Is.True);
    }
    void Capture()
    {
        grab.TickAttack(grab.windupDuration + grab.reachDuration + .01f);
        Assert.That(human.IsHeldBy(grab), Is.True, "Actual grip query must reach the victim: " + grab.LastResult+
            " left="+grab.UsesLeftHand+" contact="+grab.LastContactPoint+" victim="+human.GetComponent<Collider>().bounds);
    }
    [UnityTest]
    public IEnumerator FixedTelegraphHasOneContactThenBoundedHoldAndRecoveryWithoutDamage()
    {
        Begin(); grab.TickAttack(.6f);
        Assert.That(grab.Stage, Is.EqualTo(TitanGrabStage.Windup)); Assert.That(human.IsHeld, Is.False);
        grab.TickAttack(.24f); Assert.That(human.IsHeldBy(grab), Is.True, grab.LastResult);
        Assert.That(grab.ContactCount, Is.EqualTo(1)); Assert.That(grab.CaptureCount, Is.EqualTo(1));
        body.animator.Update(0); grab.ApplyPose(); human.TickHold(0);
        Assert.That(human.transform.position.y, Is.GreaterThan(.3f));
        grab.TickAttack(1.5f); Assert.That(human.IsHeld, Is.False);
        Assert.That(grab.Stage, Is.EqualTo(TitanGrabStage.Recovery));
        grab.TickAttack(.5f); Assert.That(grab.Stage, Is.EqualTo(TitanGrabStage.Idle));
        Assert.That(human.Health, Is.EqualTo(100)); Assert.That(human.HitCount, Is.EqualTo(0));
        Assert.That(Vector3.Distance(origin,human.transform.position), Is.LessThan(.001f));
        Assert.That(human.transform.parent, Is.Null, "Never parent a held victim into the rig");
        Assert.That(grab.TryBeginGrab(human,false), Is.False, "Cooldown is mandatory");
        yield return null;
    }
    [UnityTest]
    public IEnumerator DodgingDoesNotMoveLockedAimOrCaptureRemotely()
    {
        Begin(); var locked = grab.LockedPoint;
        grab.TickAttack(.3f); human.transform.position = new Vector3(12,0,8); Physics.SyncTransforms();
        grab.TickAttack(.6f);
        Assert.That(grab.LockedPoint, Is.EqualTo(locked)); Assert.That(human.IsHeld, Is.False);
        Assert.That(grab.ContactCount, Is.EqualTo(1)); Assert.That(grab.CaptureCount, Is.EqualTo(0));
        grab.TickAttack(2); Assert.That(grab.IsBusy, Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator LongFrameCannotSkipContactOrLeaveAStaleHold()
    {
        Begin(false); grab.TickAttack(5);
        Assert.That(grab.ContactCount, Is.EqualTo(1)); Assert.That(grab.CaptureCount, Is.EqualTo(1),grab.LastResult);
        Assert.That(grab.IsBusy, Is.False); Assert.That(human.IsHeld, Is.False);
        Assert.That(human.GetComponent<Collider>().enabled, Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator OwnershipRestoresColliderAndRigidbodyStatesAndRejectsForeignRelease()
    {
        var child = new GameObject("Disabled shape"); child.transform.SetParent(human.transform,false);
        var disabled = child.AddComponent<SphereCollider>(); disabled.enabled = false;
        var rb = human.gameObject.AddComponent<Rigidbody>(); rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var foreign = body.gameObject.AddComponent<TitanStompAttack>(); foreign.automaticTick = false;
        Assert.That(human.TryHold(grab,body.leftGripSocket), Is.True);
        Assert.That(human.TryHold(foreign,body.rightGripSocket), Is.False);
        Assert.That(human.ReleaseHold(foreign), Is.False);
        Assert.That(rb.isKinematic, Is.True); Assert.That(human.GetComponent<BoxCollider>().enabled, Is.False);
        Assert.That(human.ReleaseHold(grab), Is.True);
        Assert.That(rb.isKinematic, Is.False); Assert.That(rb.useGravity, Is.True);
        Assert.That(rb.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
        Assert.That(rb.detectCollisions, Is.True); Assert.That(disabled.enabled, Is.False);
        Assert.That(human.GetComponent<BoxCollider>().enabled, Is.True);
        Assert.That(human.ReleaseHold(grab), Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator EscapeDisableDeathAndLeaseExpiryAlwaysRelease()
    {
        Begin(); Capture(); human.EscapeHold();
        Assert.That(grab.IsBusy, Is.False); Assert.That(human.IsHeld, Is.False);
        grab.TickAttack(5); Assert.That(human.Health, Is.EqualTo(100));
        Assert.That(human.TryHold(grab,body.leftGripSocket), Is.True);
        human.TickHold(5); Assert.That(human.IsHeld, Is.False, "Independent lease bounds a stuck attacker");
        Assert.That(human.TryHold(grab,body.leftGripSocket), Is.True);
        grab.enabled = false; human.TickHold(0); Assert.That(human.IsHeld, Is.False);
        grab.enabled = true;
        Assert.That(human.TryHold(grab,body.leftGripSocket), Is.True);
        human.TakeDamage(100,Vector3.zero); Assert.That(human.IsHeld, Is.False);
        human.ResetHealth(); Assert.That(human.Health, Is.EqualTo(100));
        Assert.That(human.TryHold(grab,body.leftGripSocket), Is.True);
        human.enabled = false; Assert.That(human.IsHeld, Is.False);
        Assert.That(human.GetComponent<BoxCollider>().enabled, Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator DestroyedOwnerAndDetachedSocketCannotStrandAVictim()
    {
        Assert.That(human.TryHold(grab,body.leftGripSocket),Is.True);
        var parent = body.leftGripSocket.parent;
        var detached = new GameObject("Detached grip test"); SceneManager.MoveGameObjectToScene(detached,scene);
        body.leftGripSocket.SetParent(detached.transform,true);
        human.TickHold(0); Assert.That(human.IsHeld,Is.False);
        body.leftGripSocket.SetParent(parent,true);
        Assert.That(human.TryHold(grab,body.leftGripSocket),Is.True);
        Object.Destroy(grab); yield return null;
        human.TickHold(0); Assert.That(human.IsHeld,Is.False);
        Assert.That(human.GetComponent<BoxCollider>().enabled,Is.True);
        Assert.That(Vector3.Distance(origin,human.transform.position),Is.LessThan(.001f));
    }
    [UnityTest]
    public IEnumerator ProxyControllerWarpAndResetRestoreOwnershipAndOriginalEnableState()
    {
        var controller = human.gameObject.AddComponent<CharacterController>(); controller.center=Vector3.up; controller.height=2;
        var proxy = human.gameObject.AddComponent<TitanSandboxTarget>();
        Physics.SyncTransforms(); Begin(); Capture(); Assert.That(controller.enabled,Is.False);
        proxy.Warp(new Vector3(8,0,4));
        Assert.That(human.IsHeld || grab.IsBusy,Is.False); Assert.That(controller.enabled,Is.True);
        Assert.That(human.transform.position,Is.EqualTo(new Vector3(8,0,4)));
        controller.enabled=false;
        Assert.That(human.TryHold(grab,body.leftGripSocket),Is.True);
        human.ResetHealth(); Assert.That(human.IsHeld,Is.False); Assert.That(controller.enabled,Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator RelevantArmLossReleasesButOtherArmLossDoesNotStealTheGrab()
    {
        Begin(); Capture();
        body.Sever(TitanHitRegion.RightArm,Vector3.zero);
        Assert.That(human.IsHeldBy(grab), Is.True); Assert.That(grab.IsBusy, Is.True);
        var limb = body.limbs.First(l=>l.region==TitanHitRegion.LeftArm);
        body.Sever(TitanHitRegion.LeftArm,Vector3.zero);
        var rotations = limb.bodies.Select(b=>b.transform.rotation).ToArray();
        Assert.That(human.IsHeld, Is.False); Assert.That(grab.IsBusy, Is.False);
        grab.ApplyPose();
        for(int i=0;i<rotations.Length;i++) Assert.That(Quaternion.Angle(rotations[i],limb.bodies[i].transform.rotation),Is.LessThan(.1f));
        Assert.That(human.transform.parent, Is.Null); Assert.That(human.gameObject.activeInHierarchy);
        yield return null;
    }
    [UnityTest]
    public IEnumerator BlindnessAndEitherLegLossCancelAndReleaseImmediately()
    {
        Begin(); Capture(); body.StunEyes();
        Assert.That(human.IsHeld, Is.False); Assert.That(grab.IsBusy, Is.False);
        body.TickStatus(6); grab.TickAttack(5); Begin(false); Capture();
        body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);
        Assert.That(human.IsHeld, Is.False); Assert.That(grab.IsBusy, Is.False);
        Assert.That(grab.TryBeginGrab(human,true), Is.False,"Standing grab defers to injury ownership");
        yield return null;
    }
    [UnityTest]
    public IEnumerator CoverBlocksSelectionAndNewCoverBlocksActualHandContact()
    {
        var wall = new GameObject("Grab cover"); SceneManager.MoveGameObjectToScene(wall,scene);
        wall.transform.position = new Vector3(.7f,4,4.95f);
        var col = wall.AddComponent<BoxCollider>(); col.size = new Vector3(6,8,.15f);
        Physics.SyncTransforms(); Assert.That(grab.TryBeginGrab(human,true), Is.False);
        col.enabled=false; Begin(); col.enabled=true; Physics.SyncTransforms();
        grab.TickAttack(1); Assert.That(human.IsHeld, Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator AutomaticUpdatesDoNotConsumeTimeFromBeforeTheAttackWasSelected()
    {
        grab.automaticTick=true; Begin();
        grab.SendMessage("Update");
        Assert.That(grab.Elapsed,Is.EqualTo(0),"Director/input starts must receive a full wind-up");
        Assert.That(grab.ContactCount,Is.EqualTo(0));
        grab.Cancel(); grab.automaticTick=false;
        var stomp=body.gameObject.AddComponent<TitanStompAttack>(); stomp.drawTelegraph=false;
        Assert.That(stomp.TryBeginStomp(human,true),Is.True);
        stomp.SendMessage("Update");
        Assert.That(stomp.Elapsed,Is.EqualTo(0)); Assert.That(stomp.ImpactCount,Is.EqualTo(0));
        yield return null;
        Assert.That(stomp.Elapsed,Is.GreaterThan(0),"Subsequent frames must advance normally");
    }
    [UnityTest]
    public IEnumerator GrabAndStompCannotOverlapEvenWithoutAnAIBrain()
    {
        var stomp = body.gameObject.AddComponent<TitanStompAttack>(); stomp.automaticTick = false; stomp.drawTelegraph=false;
        Begin(); Assert.That(stomp.TryBeginStomp(human,true),Is.False);
        grab.Cancel(); Assert.That(stomp.TryBeginStomp(human,true),Is.True);
        grab.TickAttack(5); Assert.That(grab.TryBeginGrab(human,false),Is.False);
        stomp.Cancel(); grab.TickAttack(float.NaN); grab.TickAttack(-1);
        Assert.That(grab.IsBusy, Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator MirroredGrabPlantsBothFeetThroughCrouchCarryAndRecovery()
    {
        foreach(bool left in new[]{true,false})
        {
            grab.TickAttack(5); Begin(left);
            for(int frame=0;frame<58;frame++)
            {
                grab.TickAttack(.05f); body.animator.Update(0); grab.ApplyPose();
                foreach(var bone in body.ragdollBones.Where(b=>b.body.name.EndsWith("Foot")))
                {
                    var box = bone.body.GetComponentsInChildren<BoxCollider>().First(c=>!c.isTrigger&&c.attachedRigidbody==bone.body);
                    float min=float.PositiveInfinity;
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                        min=Mathf.Min(min,box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3(x,y,z))).y);
                    Assert.That(min,Is.GreaterThan(-.12f),"Planted feet cannot sink during grab/recovery");
                    Assert.That(min,Is.LessThan(.55f));
                }
            }
            Assert.That(human.IsHeld,Is.False); Assert.That(grab.IsBusy,Is.False);
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator RoundedControllerOnlyVictimIsReachableWithEitherHandAtNavMeshHeight()
    {
        Object.Destroy(human.GetComponent<BoxCollider>());
        var cc=human.gameObject.AddComponent<CharacterController>(); cc.height=1.8f; cc.center=Vector3.up*.9f; cc.radius=.32f;
        body.transform.position+=Vector3.up*.18f; human.transform.position+=Vector3.up*.18f;
        yield return null;
        foreach(bool left in new[]{true,false})
        {
            // Match the proxy's normal controller update after teleports/re-enabling.
            cc.Move(Vector3.zero);
            grab.TickAttack(5); Begin(left); Capture();
            human.EscapeHold(); Assert.That(cc.enabled,Is.True);
            yield return null;
        }
    }
    [UnityTest]
    public IEnumerator MouthTransferKeepsElbowAndWristRotationsContinuousAcrossAnimationPhases()
    {
        float worst=0; string detail="";
        var jaw=body.mouthSocket.parent; Quaternion closed=jaw.localRotation;
        foreach(bool left in new[]{true,false}) for(int phase=0;phase<4;phase++)
        {
            grab.Cancel(); grab.TickAttack(10); human.ResetHealth();
            body.animator.SetFloat("CycleOffset",phase*.25f);
            grab.enableBite=true; int captures=grab.CaptureCount; Begin(left);
            var arm=left?new[]{body.leftUpperArm,body.leftLowerArm,body.leftHand}:new[]{body.rightUpperArm,body.rightLowerArm,body.rightHand};
            var previous=arm.Select(b=>b.rotation).ToArray();
            const float dt=1f/120;
            while(grab.IsBusy && grab.Elapsed<BiteAt()-.03f)
            {
                grab.TickAttack(dt); body.animator.Update(dt);
                float sourceBend=Vector3.Angle(arm[1].position-arm[0].position,arm[2].position-arm[1].position);
                grab.ApplyPose(); human.TickHold(0);
                Assert.That(Quaternion.Angle(closed,jaw.localRotation),Is.LessThan(28.1f),"Mouth opening must not accumulate across constant Animator curves");
                for(int i=0;i<arm.Length;i++)
                {
                    float step=Quaternion.Angle(previous[i],arm[i].rotation);
                    if(grab.Elapsed>1.5f && step>worst) { worst=step; detail=" hand="+left+" phase="+phase+" time="+grab.Elapsed+" bone="+arm[i].name+" sourceBend="+sourceBend+" shoulder="+arm[0].position.ToString("F3")+" elbow="+arm[1].position.ToString("F3")+" palm="+(left?body.leftGripSocket:body.rightGripSocket).position.ToString("F3"); }
                    previous[i]=arm[i].rotation;
                }
            }
            Assert.That(grab.CaptureCount,Is.EqualTo(captures+1));
            Assert.That(grab.IsBusy,Is.True,"Continuity coverage must reach mouth windup, not an early cancellation");
        }
        Debug.Log("TITAN_ARM_CONTINUITY max120HzStep="+worst+detail);
        Assert.That(worst,Is.LessThan(8),"No elbow/wrist flip while lifting to mouth"+detail);
        yield return null;
    }

    float BiteAt() { return grab.windupDuration+grab.reachDuration+grab.holdDuration+grab.mouthTransferDuration+grab.biteWindupDuration+grab.biteDuration; }
    void AdvanceTo(float time) { Assert.That(time,Is.GreaterThanOrEqualTo(grab.Elapsed)); grab.TickAttack(time-grab.Elapsed); }

    [UnityTest]
    public IEnumerator HandToMouthWarnsThenBitesOnceAndCompletesRecoveryWithEitherHand()
    {
        grab.enableBite=true;
        foreach(bool left in new[]{true,false})
        {
            grab.TickAttack(10); human.ResetHealth(); Begin(left); Capture();
            AdvanceTo(BiteAt()-grab.biteDuration-.05f);
            Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.BiteWindup));
            Assert.That(human.Health,Is.EqualTo(100));
            body.animator.Update(0); grab.ApplyPose(); human.TickHold(0);
            Transform grip=left?body.leftGripSocket:body.rightGripSocket;
            Assert.That(Vector3.Distance(grip.position,body.mouthSocket.position),Is.LessThan(.65f),"Arm must actually reach mouth");
            Assert.That(human.transform.position.y,Is.GreaterThan(7));
            AdvanceTo(BiteAt()-.02f);
            Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Bite)); Assert.That(human.Health,Is.EqualTo(100));
            int before=grab.BiteHitCount; AdvanceTo(BiteAt()+.01f);
            Assert.That(grab.BiteHitCount,Is.EqualTo(before+1),grab.LastResult+" mouth="+grab.LastBitePoint);
            Assert.That(human.Health,Is.EqualTo(0)); Assert.That(human.HitCount,Is.EqualTo(1));
            Assert.That(human.IsHeld,Is.False); Assert.That(human.GetComponent<Collider>().enabled,Is.True);
            Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Recovery),"Lethal release must not snap idle");
            Assert.That(human.transform.parent,Is.Null);
            grab.TickAttack(10); Assert.That(grab.IsBusy,Is.False); Assert.That(human.HitCount,Is.EqualTo(1));
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator RoundedControllerCanBeBittenWhileItsNativeColliderIsDisabled()
    {
        Object.Destroy(human.GetComponent<BoxCollider>());
        var cc=human.gameObject.AddComponent<CharacterController>(); cc.height=1.8f; cc.center=Vector3.up*.9f; cc.radius=.32f;
        body.transform.position+=Vector3.up*.18f; human.transform.position+=Vector3.up*.18f;
        yield return null; grab.enableBite=true;
        foreach(bool left in new[]{true,false})
        {
            grab.TickAttack(10); human.ResetHealth(); cc.Move(Vector3.zero); Begin(left); Capture();
            Assert.That(cc.enabled,Is.False); AdvanceTo(BiteAt()+.01f);
            Assert.That(human.IsDead,Is.True,grab.LastResult); Assert.That(cc.enabled,Is.True);
            Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Recovery));
            grab.TickAttack(grab.recoveryDuration+.1f); // Finish before ticking the next cooldown.
        }
    }
    [UnityTest]
    public IEnumerator AutomaticRenderedSequenceFollowsTheHandThroughMouthWindupAndRecovery()
    {
        grab.enableBite=true; grab.automaticTick=true; Begin();
        bool lifted=false, transfer=false, warning=false; float time=0;
        while(grab.IsBusy && time<7)
        {
            yield return null; time+=Time.deltaTime;
            lifted|=grab.Stage==TitanGrabStage.Holding;
            transfer|=grab.Stage==TitanGrabStage.ToMouth;
            warning|=grab.Stage==TitanGrabStage.BiteWindup;
            if(grab.Elapsed<BiteAt()) Assert.That(human.Health,Is.EqualTo(100),"No damage before the full contact window");
        }
        Assert.That(lifted&&transfer&&warning,Is.True,"Automatic playback must expose the counterplay stages");
        Assert.That(grab.IsBusy,Is.False,grab.LastResult); Assert.That(human.IsDead,Is.True,grab.LastResult);
        Assert.That(grab.BiteHitCount,Is.EqualTo(1)); Assert.That(human.IsHeld,Is.False);
        Assert.That(human.GetComponent<Collider>().enabled,Is.True);
    }
    [UnityTest]
    public IEnumerator LongFrameBiteHitsExactlyOnceButCannotBypassAShorterVictimLease()
    {
        grab.enableBite=true; Begin(); grab.TickAttack(10);
        Assert.That(grab.CaptureCount,Is.EqualTo(1)); Assert.That(grab.BiteHitCount,Is.EqualTo(1),grab.LastResult);
        Assert.That(human.IsHeld||grab.IsBusy,Is.False); Assert.That(human.HitCount,Is.EqualTo(1));
        human.ResetHealth(); human.maximumHoldDuration=.5f; grab.TickAttack(10); Begin(false);
        grab.TickAttack(10);
        Assert.That(human.Health,Is.EqualTo(100),"Lease expiry must win even before target LateUpdate runs");
        Assert.That(human.IsHeld||grab.IsBusy,Is.False); Assert.That(human.GetComponent<Collider>().enabled,Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator UnreachableMouthCannotKillAPlayerJustBecauseItOwnsTheHold()
    {
        grab.enableBite=true; Begin(); Capture(); body.mouthSocket.position+=Vector3.up*10;
        AdvanceTo(BiteAt()+.01f);
        Assert.That(grab.BiteContactCount,Is.EqualTo(1)); Assert.That(grab.BiteHitCount,Is.EqualTo(0));
        Assert.That(human.Health,Is.EqualTo(100)); Assert.That(human.IsHeld,Is.False);
        Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Recovery)); yield return null;
    }
    [UnityTest]
    public IEnumerator EscapeJustBeforeMouthClosurePreventsAnyLaterDamage()
    {
        grab.enableBite=true; Begin(); Capture(); AdvanceTo(BiteAt()-.01f);
        human.EscapeHold(); grab.TickAttack(20);
        Assert.That(human.Health,Is.EqualTo(100)); Assert.That(grab.BiteContactCount,Is.EqualTo(0));
        Assert.That(grab.IsBusy||human.IsHeld,Is.False); yield return null;
    }
    [UnityTest]
    public IEnumerator BiteWindupInterruptionMatrixReleasesAndNeverSchedulesDeath()
    {
        for(int cause=0;cause<6;cause++)
        {
            if(cause>0) { Object.Destroy(body.gameObject); yield return null; SpawnBody(); human.ResetHealth(); }
            grab.enableBite=true; Begin(); Capture(); AdvanceTo(BiteAt()-.04f);
            switch(cause)
            {
                case 0: body.StunEyes(); break;
                case 1: body.Sever(TitanHitRegion.LeftArm,Vector3.zero); break;
                case 2: body.Sever(TitanHitRegion.RightLeg,Vector3.zero); break;
                case 3: body.Die(); break;
                case 4: grab.enabled=false; break;
                case 5: human.ResetHealth(); break;
            }
            Assert.That(human.IsHeld,Is.False); Assert.That(grab.IsBusy,Is.False);
            grab.TickAttack(20); Assert.That(human.Health,Is.EqualTo(100));
            Assert.That(grab.BiteHitCount,Is.EqualTo(0)); Assert.That(human.transform.parent,Is.Null);
        }
    }
    [UnityTest]
    public IEnumerator CoverAddedAfterCaptureBlocksLiftOrMouthContact()
    {
        grab.enableBite=true; Begin(); Capture();
        var cover=new GameObject("Mouth transfer cover"); SceneManager.MoveGameObjectToScene(cover,scene);
        cover.transform.position=new Vector3(0,8,1.6f); cover.AddComponent<BoxCollider>().size=new Vector3(6,1,.8f);
        Physics.SyncTransforms(); AdvanceTo(BiteAt()+.01f);
        Assert.That(human.Health,Is.EqualTo(100),"A hand-to-mouth path cannot pass through cover");
        Assert.That(grab.BiteHitCount,Is.EqualTo(0)); Assert.That(human.IsHeld,Is.False); yield return null;
    }
    [UnityTest]
    public IEnumerator RenderedCarrySweepReleasesBeforeFollowingTheHandThroughNewCover()
    {
        grab.enableBite=true; Begin(); Capture();
        var cover=new GameObject("Lift obstruction"); SceneManager.MoveGameObjectToScene(cover,scene);
        cover.transform.position=new Vector3(0,4,3.5f); cover.AddComponent<BoxCollider>().size=new Vector3(8,.5f,2);
        Physics.SyncTransforms(); AdvanceTo(1.5f); body.animator.Update(0); grab.SendMessage("LateUpdate");
        Assert.That(human.IsHeld||grab.IsBusy,Is.False); Assert.That(human.Health,Is.EqualTo(100));
        grab.TickAttack(20); Assert.That(grab.BiteHitCount,Is.EqualTo(0)); yield return null;
    }
    [UnityTest]
    public IEnumerator BiteSettingsAreLatchedAndNonlethalContactAlsoReleasesSafely()
    {
        grab.enableBite=true; grab.biteDamage=40; Begin(); Capture(); float time=BiteAt();
        grab.enableBite=false; grab.biteDamage=100; grab.biteWindupDuration=.01f; grab.mouthTransferDuration=.01f;
        AdvanceTo(time-.15f); Assert.That(human.Health,Is.EqualTo(100));
        AdvanceTo(time+.01f);
        Assert.That(human.Health,Is.EqualTo(60),grab.LastResult); Assert.That(human.HitCount,Is.EqualTo(1));
        Assert.That(human.IsHeld,Is.False); Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Recovery));
        yield return null;
    }
    [UnityTest]
    public IEnumerator DamageCallbackDeathPreservesTheContactPoseInsteadOfRestoringOldAnimation()
    {
        grab.enableBite=true; Begin(); Capture(); Quaternion[] atDeath=null;
        human.Damaged+=amount=> { atDeath=body.ragdollBones.Select(b=>b.body.transform.rotation).ToArray(); body.Die(); };
        AdvanceTo(BiteAt()+.01f);
        Assert.That(atDeath,Is.Not.Null); Assert.That(body.IsDead,Is.True); Assert.That(grab.IsBusy,Is.False);
        for(int i=0;i<atDeath.Length;i++) Assert.That(Quaternion.Angle(atDeath[i],body.ragdollBones[i].body.transform.rotation),Is.LessThan(.1f));
        Assert.That(human.IsHeld,Is.False); yield return null;
    }
    [UnityTest]
    public IEnumerator DisabledHeldShapesUseActualGeometryNotEmptyBoundsOrAnAABB()
    {
        Assert.That(human.TryHold(grab,body.leftGripSocket),Is.True);
        human.transform.rotation=Quaternion.Euler(0,45,0); Vector3 hit;
        Assert.That(human.TryGetHeldContact(grab,human.transform.TransformPoint(new Vector3(.45f,1,0)),.06f,out hit),Is.True);
        Assert.That(human.TryGetHeldContact(grab,human.transform.TransformPoint(new Vector3(.7f,1,0)),.1f,out hit),Is.False);
        Assert.That(human.TryGetHeldContact(grab,human.transform.position, float.NaN,out hit),Is.False);
        human.EscapeHold(); Assert.That(human.TryGetHeldContact(grab,human.transform.position,10,out hit),Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator MouthTransferKeepsFeetSupportedAndOtherArmLossStillAllowsTheBite()
    {
        grab.enableBite=true; Begin(); Capture(); body.Sever(TitanHitRegion.RightArm,Vector3.zero);
        for(int frame=0;frame<70;frame++)
        {
            grab.TickAttack(.05f); body.animator.Update(0); grab.ApplyPose();
            foreach(var bone in body.ragdollBones.Where(b=>b.body.name.EndsWith("Foot")))
            {
                var box=bone.body.GetComponentsInChildren<BoxCollider>().First(c=>!c.isTrigger&&c.attachedRigidbody==bone.body);
                float min=float.PositiveInfinity;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    min=Mathf.Min(min,box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3(x,y,z))).y);
                Assert.That(min,Is.GreaterThan(-.12f)); Assert.That(min,Is.LessThan(.55f));
            }
        }
        Assert.That(human.IsDead,Is.True,grab.LastResult); Assert.That(grab.BiteHitCount,Is.EqualTo(1));
        Assert.That(human.IsHeld||grab.IsBusy,Is.False); yield return null;
    }
    [UnityTest]
    public IEnumerator NapeDeathDuringMouthTransferAndClosurePreservesPoseAndStablePhysics()
    {
        float worst=0;
        for(int phase=0;phase<4;phase++)
        {
            if(phase>0) { Object.Destroy(body.gameObject); yield return null; SpawnBody(); human.ResetHealth(); }
            grab.enableBite=true; Begin(phase%2==0); Capture(); AdvanceTo(phase<2?2.7f:BiteAt()-.04f);
            body.animator.Update(0); grab.ApplyPose(); human.TickHold(0);
            var rotations=body.ragdollBones.Select(b=>b.body.transform.rotation).ToArray();
            body.Die(); Assert.That(human.IsHeld,Is.False); Assert.That(grab.IsBusy,Is.False);
            for(int i=0;i<rotations.Length;i++) Assert.That(Quaternion.Angle(rotations[i],body.ragdollBones[i].body.transform.rotation),Is.LessThan(.1f));
            yield return null; float max=0;
            for(int step=0;step<250;step++)
            {
                scene.GetPhysicsScene().Simulate(.02f);
                foreach(var bone in body.ragdollBones) max=Mathf.Max(max,bone.body.velocity.magnitude);
            }
            worst=Mathf.Max(worst,max); Debug.Log("TITAN_MOUTH_DEATH_METRICS phase="+phase+" maxSpeed="+max);
        }
        Assert.That(worst,Is.LessThan(40));
    }
    [UnityTest]
    public IEnumerator NapeDeathFromCrouchAndCarryReleasesAndRemainsStable()
    {
        float worst=0;
        for(int phase=0;phase<4;phase++)
        {
            if(phase>0)
            {
                Object.Destroy(body.gameObject); yield return null; SpawnBody();
                human.ResetHealth(); human.transform.position=origin;
            }
            Begin(phase%2==0); grab.TickAttack(phase<2?.55f:1.4f);
            body.animator.Update(0); grab.ApplyPose(); human.TickHold(0);
            var transforms=body.ragdollBones.Select(b=>b.body.transform).ToArray();
            var rotations=transforms.Select(t=>t.rotation).ToArray();
            body.Die(); Assert.That(human.IsHeld,Is.False); Assert.That(grab.IsBusy,Is.False);
            for(int i=0;i<transforms.Length;i++) Assert.That(Quaternion.Angle(rotations[i],transforms[i].rotation),Is.LessThan(.1f));
            yield return null;
            float max=0;
            for(int i=0;i<250;i++)
            {
                scene.GetPhysicsScene().Simulate(.02f);
                foreach(var bone in body.ragdollBones)max=Mathf.Max(max,bone.body.velocity.magnitude);
            }
            worst=Mathf.Max(worst,max); Debug.Log("TITAN_GRAB_DEATH_METRICS phase="+phase+" maxSpeed="+max);
        }
        Assert.That(worst,Is.LessThan(40),"Grab death must not destabilize the ragdoll");
    }
}
#endif
