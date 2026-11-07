#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class TitanAerialGrabPlayModeTests
{
    Scene scene,previous;
    ModularTitanBody body;
    TitanGrabAttack grab;
    TitanCombatTarget human;
    TitanSandboxFlight flight;
    CharacterController controller;
    [UnitySetUp] public IEnumerator SetUp()
    {
        previous=SceneManager.GetActiveScene();
        scene=SceneManager.CreateScene("Titan_Aerial_Test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SceneManager.SetActiveScene(scene);
        var floor=new GameObject("Floor"); floor.transform.position=Vector3.down*.25f;
        floor.AddComponent<BoxCollider>().size=new Vector3(80,.5f,80);
        body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
        body.GetComponent<TitanMotionStyle>().automaticTick=false;
        grab=body.gameObject.AddComponent<TitanGrabAttack>(); grab.automaticTick=false; grab.drawTelegraph=false;
        var go=new GameObject("Airborne human"); controller=go.AddComponent<CharacterController>();
        controller.center=Vector3.up; controller.height=2; controller.radius=.4f;
        human=go.AddComponent<TitanCombatTarget>(); flight=go.AddComponent<TitanSandboxFlight>(); flight.automaticTick=false;
        SceneManager.SetActiveScene(previous); yield return null;
    }
    [UnityTearDown] public IEnumerator TearDown()
    { SceneManager.SetActiveScene(previous); if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene); }
    void Begin(bool left=true,bool fast=false)
    {
        // Keep the configurable 1s legacy timeline for its explicit timing/CCD
        // regressions. New default-speed checks below use the actual fast defaults.
        if(!fast) { grab.aerialWindupDuration=.7f;grab.aerialReachDuration=.3f; }
        human.ResetHealth();
        flight.Launch(new Vector3(left?-.7f:.7f,7,2.6f),Vector3.zero);
        body.animator.Update(0); Physics.SyncTransforms();
        Assert.That(grab.TryBeginGrab(human,left),Is.False,"Ground variant must still reject flyers");
        Assert.That(grab.TryBeginAerialGrab(human,left),Is.True,"3D reach selection");
    }
    void Capture()
    {
        for(int i=0;i<140 && !human.IsHeld;i++)
        {
            grab.TickAttack(1f/120);
            if(human.IsHeld) break;
            body.animator.Update(1f/120); grab.ApplyPose();
        }
        Assert.That(human.IsHeldBy(grab),Is.True,grab.LastResult+" aim="+grab.LockedPoint+" hit="+grab.LastContactPoint);
    }
    [UnityTest] public IEnumerator RotatingControllerCenterSweepUsesNativeWorldUprightCapsuleGeometry()
    {
        controller.center=Vector3.up;controller.height=3.6f;controller.radius=.5f;
        Vector3 root=Vector3.up*7;controller.transform.SetPositionAndRotation(root,Quaternion.Euler(0,0,45));Physics.SyncTransforms();
        Assert.That(controller.bounds.size.x,Is.EqualTo(1).Within(.001f),"Native CCT capsule does NOT tilt with the avatar");
        Assert.That(controller.bounds.size.y,Is.EqualTo(3.6f).Within(.001f));
        Assert.That(Vector3.Distance(controller.bounds.center,controller.transform.TransformPoint(controller.center)),Is.LessThan(.001f));
        Vector3 nearTop=controller.bounds.center+Vector3.up*1.75f;
        Assert.That(Vector3.Distance(TitanCombatTarget.ClosestCombatPoint(controller,nearTop),nearTop),Is.LessThan(.0001f));
        var palm=new TitanAerialContact.HandPose {center=root+Vector3.left*1.4f,half=Vector3.one*.05f,rotation=Quaternion.identity};
        Quaternion start=Quaternion.identity,end=Quaternion.Euler(0,0,160);Vector3 contact;float fraction;
        Assert.That(TitanAerialContact.CapsuleGap(controller,palm,root,start,out contact),Is.GreaterThan(0));
        Assert.That(TitanAerialContact.CapsuleGap(controller,palm,root,end,out contact),Is.GreaterThan(0));
        Assert.That(TitanAerialContact.SweepHand(controller,palm,palm,root,root,start,end,out fraction),Is.True);
        Assert.That(fraction,Is.InRange(0f,1f));
        Assert.That(TitanAerialContact.CapsuleGap(controller,palm,root,Quaternion.Slerp(start,end,fraction),out contact),Is.LessThan(.0001f));yield return null;
    }
    [UnityTest] public IEnumerator FastDefaultReachStartsFromThePalmWithoutWarningLiftOrPosePop()
    {
        Assert.That(grab.aerialWindupDuration,Is.Zero);Assert.That(grab.aerialReachDuration,Is.LessThanOrEqualTo(.25f));
        grab.drawTelegraph=true;Begin(true,true);
        Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Reach));
        Assert.That(grab.transform.Find("Grab fixed aim telegraph"),Is.Null,"No aerial aim-warning ring");
        Vector3 start;float radius;TitanAerialContact.HandSphere(body.leftHand,out start,out radius);
        Quaternion before=body.leftUpperArm.rotation;grab.ApplyPose();
        Assert.That(Quaternion.Angle(before,body.leftUpperArm.rotation),Is.LessThan(.01f),"No selection-frame snap");
        var bones=new[]{body.leftUpperArm,body.leftLowerArm,body.leftHand};var rotations=new Quaternion[3];
        for(int j=0;j<3;j++) rotations[j]=bones[j].rotation;
        float maximum=0;
        for(int i=0;i<30 && !human.IsHeld;i++)
        {
            grab.TickAttack(1f/120);body.animator.Update(1f/120);grab.ApplyPose();
            Vector3 palm;TitanAerialContact.HandSphere(body.leftHand,out palm,out radius);
            Assert.That(palm.y,Is.LessThanOrEqualTo(Mathf.Max(start.y,grab.LockedPoint.y)+.25f),"No upward warning excursion");
            for(int j=0;j<3;j++) {maximum=Mathf.Max(maximum,Quaternion.Angle(rotations[j],bones[j].rotation));rotations[j]=bones[j].rotation;}
        }
        Debug.Log("TITAN_FAST_REACH_CONTINUITY max120HzStep="+maximum+" contactTime="+grab.Elapsed);
        Assert.That(human.IsHeldBy(grab),Is.True,grab.LastResult);Assert.That(grab.Elapsed,Is.LessThanOrEqualTo(.23f));
        Assert.That(maximum,Is.LessThan(8));yield return null;
    }
    [UnityTest] public IEnumerator FastDirectReachStillMissesAnActualDodgeWithoutRetargeting()
    {
        Begin(false,true);Vector3 locked=grab.LockedPoint;
        controller.enabled=false;human.transform.position=new Vector3(15,7,3);controller.enabled=true;
        grab.TickAttack(.23f);
        Assert.That(grab.LockedPoint,Is.EqualTo(locked));Assert.That(grab.CaptureCount,Is.Zero);
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.ContactCount,Is.EqualTo(1));yield return null;
    }
    [UnityTest] public IEnumerator FastMissRecoveryAndCooldownAllowAnotherReachInAboutOneSecond()
    {
        Begin(true,true);
        controller.enabled=false;human.transform.position=new Vector3(15,7,3);controller.enabled=true;
        grab.TickAttack(.23f);grab.TickAttack(.26f);
        Assert.That(grab.IsBusy,Is.False);Assert.That(grab.CooldownRemaining,Is.LessThanOrEqualTo(.65f));
        grab.TickAttack(.66f);Begin(true,true);
        Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Reach));Assert.That(grab.Elapsed,Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator BothHandsReachActualPhysicalContactAboveGround()
    {
        foreach(bool left in new[]{true,false})
        {
            Begin(left); Vector3 original=human.transform.position; Capture();
            Vector3 center;float radius;Assert.That(TitanAerialContact.HandSphere(left?body.leftHand:body.rightHand,out center,out radius),Is.True);
            float gap=Vector3.Distance(grab.LastContactPoint,TitanCombatTarget.ClosestCombatPoint(controller,grab.LastContactPoint));
            Assert.That(gap,Is.LessThanOrEqualTo(radius+.001f),"Physical mitten, not selection radius");
            Assert.That(Vector3.Distance(original,human.transform.position),Is.LessThan(.01f),"No socket-center snap at catch");
            Assert.That(human.transform.parent,Is.Null); Assert.That(flight.Suspended,Is.True);
            human.EscapeHold(); Assert.That(human.IsHeld,Is.False); Assert.That(human.ReleasePending,Is.False);
            Assert.That(controller.enabled,Is.True); Assert.That(flight.Suspended,Is.False);
            grab.TickAttack(10);
        }
        yield return null;
    }
    [UnityTest] public IEnumerator AboveHeadLateralAndLowAirborneTargetsUseRealThreeDimensionalReach()
    {
        grab.aerialWindupDuration=.7f;grab.aerialReachDuration=.3f;
        var positions=new[]{new Vector3(-.7f,10,1.5f),new Vector3(2.8f,7.8f,1.8f),new Vector3(-.7f,5.8f,1.3f)};
        for(int i=0;i<positions.Length;i++)
        {
            human.ResetHealth();flight.Launch(positions[i],Vector3.zero);body.animator.Update(0);Physics.SyncTransforms();
            Assert.That(grab.TryBeginAerialGrab(human,i!=1),Is.True,"Height/side candidate "+i);
            Capture();Assert.That(human.transform.parent,Is.Null);human.EscapeHold();grab.TickAttack(10);
        }
        yield return null;
    }
    [UnityTest] public IEnumerator FixedAerialTelegraphAndLockedAimRemainDodgeable()
    {
        Begin();Vector3 point=grab.LockedPoint;
        grab.TickAttack(.3f); Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Windup)); Assert.That(human.IsHeld,Is.False);
        controller.enabled=false;human.transform.position=new Vector3(15,7,3);controller.enabled=true;
        grab.TickAttack(.3f); grab.TickAttack(.41f);
        Assert.That(grab.LockedPoint,Is.EqualTo(point));Assert.That(human.IsHeld,Is.False);
        Assert.That(grab.ContactCount,Is.EqualTo(1));Assert.That(grab.CaptureCount,Is.Zero);
        yield return null;
    }
    [UnityTest] public IEnumerator FullPhysicalMittenIncludesCornerContactWithoutEnlargingTheHand()
    {
        Vector3 root=new Vector3(.72f,7,.5f);flight.Launch(root,Vector3.zero);
        var box=new TitanAerialContact.HandPose {center=new Vector3(0,8,0),half=new Vector3(.4f,.7f,.3f),rotation=Quaternion.identity};
        float fraction;Vector3 contact;
        Assert.That(TitanAerialContact.CapsuleGap(controller,box,root,out contact),Is.LessThan(0));
        Assert.That(TitanAerialContact.SweepHand(controller,box,box,root,root,out fraction),Is.True);
        Assert.That(TitanAerialContact.Sweep(controller,box.center,box.center,.3f,root,root,root,out fraction),Is.False,"An inscribed sphere wrongly excludes the real hand corner");
        yield return null;
    }
    [UnityTest] public IEnumerator RotatingHandSweepCatchesBetweenSeparatedEndpointPoses()
    {
        Vector3 root=new Vector3(1.5f,7,0);flight.Launch(root,Vector3.zero);
        var a=new TitanAerialContact.HandPose {center=new Vector3(0,8,0),half=new Vector3(1.5f,.15f,.15f),rotation=Quaternion.Euler(0,-45,0)};
        var b=a;b.rotation=Quaternion.Euler(0,45,0);Vector3 contact;float fraction;
        Assert.That(TitanAerialContact.CapsuleGap(controller,a,root,out contact),Is.GreaterThan(0));
        Assert.That(TitanAerialContact.CapsuleGap(controller,b,root,out contact),Is.GreaterThan(0));
        Assert.That(TitanAerialContact.SweepHand(controller,a,b,root,root,out fraction),Is.True);
        Assert.That(fraction,Is.InRange(.1f,.9f));yield return null;
    }
    [UnityTest] public IEnumerator RelativeSweepFindsFastFlyerWithBothEndpointsOutside()
    {
        Vector3 from=new Vector3(-8,7,0),to=new Vector3(8,7,0),hand=new Vector3(0,8,0);
        flight.Launch(to,new Vector3(160,0,0));Physics.SyncTransforms();float fraction;
        Assert.That(TitanAerialContact.Sweep(controller,hand,hand,.25f,from,to,to,out fraction),Is.True);
        Assert.That(fraction,Is.InRange(.4f,.6f));
        Assert.That(Vector3.Distance(hand,TitanCombatTarget.ClosestCombatPoint(controller,hand)),Is.GreaterThan(7));
        Assert.That(TitanAerialContact.Sweep(controller,hand+Vector3.forward*2,hand+Vector3.forward*2,.25f,from,to,to,out fraction),Is.False,"No giant catch radius");
        yield return null;
    }
    [UnityTest] public IEnumerator AttackCatchesCrossingFlyerRatherThanOnlyEndFrameOverlap()
    {
        Begin(false,true);
        controller.enabled=false;human.transform.position=new Vector3(-8,7,2.6f);controller.enabled=true;
        grab.TickAttack(.21f);
        body.animator.Update(0);grab.ApplyPose();Vector3 hand;float radius;
        TitanAerialContact.HandSphere(body.rightHand,out hand,out radius);
        // Cross the ACTUAL direct-reach pose, not the removed raised warning pose.
        controller.enabled=false;human.transform.position=new Vector3(-8,hand.y-1,hand.z);controller.enabled=true;
        grab.TickAttack(.001f);
        controller.enabled=false;human.transform.position=new Vector3(8,hand.y-1,hand.z);controller.enabled=true;
        grab.TickAttack(.009f);
        Assert.That(human.IsHeldBy(grab),Is.True,grab.LastResult);
        Assert.That(human.transform.position.x,Is.LessThan(4),"CCD contact time, not remote capture at the far endpoint");
        Assert.That(grab.ContactCount,Is.EqualTo(1));Assert.That(grab.CaptureCount,Is.EqualTo(1));
        yield return null;
    }
    [UnityTest] public IEnumerator SelectionRequiresMotorAndActualThreeDimensionalReach()
    {
        flight.Launch(new Vector3(0,20,3),Vector3.zero);body.animator.Update(0);Physics.SyncTransforms();
        Assert.That(grab.TryBeginAerialGrab(human,true),Is.False);
        flight.Launch(new Vector3(-.7f,7,2.6f),new Vector3(80,0,0));Physics.SyncTransforms();
        Assert.That(grab.TryBeginAerialGrab(human,true),Is.True,"A currently reachable flyer may be attempted even if the full-horizon prediction leaves range");
        Assert.That(human.IsHeld,Is.False,"Selection is not contact");grab.Cancel();grab.TickAttack(10);
        Object.DestroyImmediate(flight); Physics.SyncTransforms();
        Assert.That(grab.TryBeginAerialGrab(human,true),Is.False,"Never reuse ground release policy");
        yield return null;
    }
    [UnityTest] public IEnumerator CoverAddedDuringWindupBlocksThePhysicalReach()
    {
        Begin(); var wall=new GameObject("Cover");SceneManager.MoveGameObjectToScene(wall,scene);
        wall.transform.position=grab.LockedPoint;wall.AddComponent<BoxCollider>().size=new Vector3(3,4,1);
        Physics.SyncTransforms();grab.TickAttack(1.01f);
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.CaptureCount,Is.Zero);
        yield return null;
    }
    [UnityTest] public IEnumerator EscapeDropsFromCurrentAirPoseWithMomentumNotOldGroundPose()
    {
        Begin();Capture();var captured=human.transform.position;
        body.animator.Update(0);grab.ApplyPose();human.TickHold(.02f);
        human.EscapeHold();
        Assert.That(human.IsHeld,Is.False);Assert.That(human.ReleasePending,Is.False);
        Assert.That(controller.enabled,Is.True);Assert.That(flight.IsFlying,Is.True);
        Assert.That(human.transform.position.y,Is.GreaterThan(5));
        Assert.That(Vector3.Distance(human.transform.position,captured),Is.LessThan(6));
        Vector3 before=human.transform.position;flight.TickFlight(.02f);
        Assert.That(flight.Velocity.y,Is.LessThan(0));Assert.That(human.transform.position.y,Is.LessThan(before.y));
        yield return null;
    }
    [UnityTest] public IEnumerator BlockedReleaseClearsOwnershipThenRetriesWithoutTheAttack()
    {
        Begin();Capture();var blocker=new GameObject("Release obstruction");SceneManager.MoveGameObjectToScene(blocker,scene);
        blocker.transform.position=human.transform.position+Vector3.up;blocker.AddComponent<BoxCollider>().size=Vector3.one*16;
        Physics.SyncTransforms();human.EscapeHold();
        Assert.That(human.IsHeld,Is.False);Assert.That(human.HoldOwner,Is.Null);
        Assert.That(human.ReleasePending,Is.True);Assert.That(controller.enabled,Is.False);
        Object.DestroyImmediate(blocker);Physics.SyncTransforms();human.TickHold(0);
        Assert.That(human.ReleasePending,Is.False);Assert.That(controller.enabled,Is.True);
        Assert.That(flight.Suspended,Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator BlindnessAndOwnerDisableIndependentlyReleaseAirborneHumans()
    {
        Begin();Capture();grab.enabled=false;
        Assert.That(human.IsHeld,Is.False);Assert.That(controller.enabled,Is.True);
        grab.enabled=true;grab.TickAttack(10);Begin(false);Capture();body.StunEyes();
        Assert.That(human.IsHeld,Is.False);Assert.That(controller.enabled,Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator SeveringTheCatchingArmReleasesWithoutParentingIntoDebris()
    {
        Begin();Capture();body.Sever(TitanHitRegion.LeftArm,Vector3.zero);
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.IsBusy,Is.False);Assert.That(human.transform.parent,Is.Null);
        foreach(var limb in body.limbs) if(limb.severed && limb.rootBone!=null) Object.DestroyImmediate(limb.rootBone.gameObject);
        Physics.SyncTransforms();human.TickHold(0);
        Assert.That(controller.enabled,Is.True);Assert.That(flight.Suspended,Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator LosingTheOtherArmDoesNotCancelAnAerialHold()
    {
        Begin();Capture();body.Sever(TitanHitRegion.RightArm,Vector3.zero);
        Assert.That(human.IsHeldBy(grab),Is.True);Assert.That(grab.IsBusy,Is.True);
        human.EscapeHold();Assert.That(human.IsHeld,Is.False);yield return null;
    }
    [UnityTest] public IEnumerator LegLossHandsPoseOwnershipToInjuryNotTheAerialAttack()
    {
        Begin();Capture();body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);
        Vector3 pose=body.hips.position;
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.IsBusy,Is.False);Assert.That(body.animator.enabled,Is.False);
        grab.ApplyPose();Assert.That(Vector3.Distance(pose,body.hips.position),Is.LessThan(.00001f));
        yield return null;
    }
    [UnityTest] public IEnumerator LongTickUsesActualEarlyAerialCaptureTimeForTheBiteLease()
    {
        human.maximumHoldDuration=3;grab.enableBite=true;Begin();Capture();
        Assert.That(grab.Elapsed,Is.LessThan(1),"Aerial contact is before the fixed reach-end timeline");
        grab.TickAttack(5);
        Assert.That(human.IsHeld,Is.False);Assert.That(human.Health,Is.EqualTo(100));
        Assert.That(grab.BiteHitCount,Is.Zero);yield return null;
    }
    [UnityTest] public IEnumerator VictimLeaseReleasesEvenWhenTheAttackClockStops()
    {
        Begin();Capture();human.TickHold(human.maximumHoldDuration+.01f);
        Assert.That(human.IsHeld,Is.False);Assert.That(controller.enabled,Is.True);Assert.That(grab.IsBusy,Is.False);
        Assert.That(human.transform.position.y,Is.GreaterThan(5));Assert.That(human.HitCount,Is.Zero);
        yield return null;
    }
    [UnityTest] public IEnumerator DisablingTheAerialMotorReleasesItsController()
    {
        Begin();Capture();flight.enabled=false;
        Assert.That(human.IsHeld,Is.False);Assert.That(controller.enabled,Is.True);Assert.That(grab.IsBusy,Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator ChangingCapsuleTiltFailsClosedRatherThanInventingRotationalContact()
    {
        Begin();grab.TickAttack(.6f);human.transform.rotation=Quaternion.Euler(30,0,0);grab.TickAttack(.41f);
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.CaptureCount,Is.Zero);
        yield return null;
    }
    [UnityTest] public IEnumerator AerialVictimLayerMaskIsRespected()
    {
        grab.victimMask=0;Begin();grab.TickAttack(1.01f);
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.CaptureCount,Is.Zero);
        yield return null;
    }
    [UnityTest] public IEnumerator SaturatedAerialSceneryQueriesFailClosed()
    {
        Begin();for(int i=0;i<70;i++)
        {
            var go=new GameObject("Saturation "+i);SceneManager.MoveGameObjectToScene(go,scene);
            go.transform.position=new Vector3(0,9,3);go.AddComponent<BoxCollider>().size=Vector3.one*10;
        }
        Physics.SyncTransforms();grab.TickAttack(1.01f);
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.LastQueryOverflow,Is.True);
        yield return null;
    }
    [UnityTest] public IEnumerator RemovedTargetComponentCannotStrandABlockedController()
    {
        Begin();Capture();var blocker=new GameObject("Blocked release on component removal");SceneManager.MoveGameObjectToScene(blocker,scene);
        blocker.transform.position=human.transform.position+Vector3.up;blocker.AddComponent<BoxCollider>().size=Vector3.one*16;
        Physics.SyncTransforms();human.EscapeHold();Assert.That(human.ReleasePending,Is.True);
        Object.DestroyImmediate(human);Object.DestroyImmediate(blocker);Physics.SyncTransforms();
        yield return null;yield return null;
        Assert.That(controller.enabled,Is.True);Assert.That(flight.Suspended,Is.False);
    }
    [UnityTest] public IEnumerator AerialMouthDeathPreservesPoseAndStableRagdollPhysics()
    {
        grab.enableBite=true;Begin(false);Capture();grab.TickAttack(2.5f);
        body.animator.Update(0);grab.ApplyPose();human.TickHold(.02f);
        Quaternion pose=body.rightHand.rotation;body.Die();
        Assert.That(Quaternion.Angle(pose,body.rightHand.rotation),Is.LessThan(.01f));
        Assert.That(human.IsHeld,Is.False);Assert.That(grab.IsBusy,Is.False);
        yield return null;float max=0;
        for(int i=0;i<250;i++)
        {
            flight.TickFlight(.02f);scene.GetPhysicsScene().Simulate(.02f);
            foreach(var bone in body.ragdollBones) if(bone.body!=null) max=Mathf.Max(max,bone.body.velocity.magnitude);
        }
        Debug.Log("TITAN_AERIAL_DEATH_METRICS maxSpeed="+max);
        Assert.That(max,Is.LessThan(40),"Same strict loaded-death threshold, no caps or limit changes");
    }
    [UnityTest] public IEnumerator LongTickCannotBiteThroughSceneryOnTheForwardMouthCarryArc()
    {
        grab.enableBite=true;Begin(true,true);Capture();
        Vector3 offset=human.HeldCenterOffset(grab),horizontal=Vector3.ProjectOnPlane(offset,Vector3.up);
        float yaw=Vector3.SignedAngle(horizontal,body.transform.right,Vector3.up);
        Quaternion turn=Quaternion.AngleAxis(yaw*.5f,Vector3.up);
        Vector3 carry=body.transform.TransformPoint(new Vector3(-1.2f,8.2f,2));
        Vector3 goal=body.mouthSocket.position+body.transform.forward*.6f-turn*offset;
        Vector3 arc=Vector3.Lerp(carry,goal,.5f)+body.transform.forward*.9f+Vector3.up*.35f;
        var obstruction=new GameObject("Forward carry arc obstruction");SceneManager.MoveGameObjectToScene(obstruction,scene);
        obstruction.transform.position=arc;obstruction.AddComponent<BoxCollider>().size=Vector3.one*.2f;
        Physics.SyncTransforms();grab.TickAttack(3.15f-grab.Elapsed);
        Assert.That(grab.BiteContactCount,Is.EqualTo(1));Assert.That(grab.BiteHitCount,Is.Zero);
        Assert.That(human.Health,Is.EqualTo(100));Assert.That(human.IsHeld,Is.False);yield return null;
    }
    float DistalArmTorsoPenetration(bool left)
    {
        float maximum=0;
        var all=body.GetComponentsInChildren<Collider>();
        foreach(var arm in all)
        {
            if(!arm.enabled || arm.isTrigger || arm.attachedRigidbody==null) continue;
            Transform rb=arm.attachedRigidbody.transform;
            if(rb!=(left?body.leftLowerArm:body.rightLowerArm) && rb!=(left?body.leftHand:body.rightHand)) continue;
            foreach(var torso in all)
            {
                if(!torso.enabled || torso.isTrigger || torso.attachedRigidbody==null) continue;
                string name=torso.attachedRigidbody.transform.name;if(name!="Chest" && name!="Spine") continue;
                Vector3 direction;float depth;
                if(Physics.ComputePenetration(arm,arm.transform.position,arm.transform.rotation,
                    torso,torso.transform.position,torso.transform.rotation,out direction,out depth)) maximum=Mathf.Max(maximum,depth);
            }
        }
        return maximum;
    }
    [UnityTest] public IEnumerator AerialCarryAndMouthKeepForearmAndPalmOutsideTheTorsoForBothHands()
    {
        grab.enableBite=true;float maximum=0;string worst="";int expectedBites=0;
        foreach(bool left in new[]{true,false}) foreach(float phase in new[]{0f,.5f})
        {
            body.animator.SetFloat("CycleOffset",phase);body.animator.Play("Idle",0,0);body.animator.Update(0);
            Begin(left,true);Capture();grab.ApplyPose();human.TickHold(0);
            Transform socket=left?body.leftGripSocket:body.rightGripSocket;
            Vector3 localGrip=human.transform.InverseTransformPoint(socket.position);
            float gripDistance=Vector3.Distance(human.transform.position,socket.position);
            while(grab.IsBusy)
            {
                grab.TickAttack(1f/120);body.animator.Update(1f/120);grab.ApplyPose();human.TickHold(1f/120);
                scene.GetPhysicsScene().Simulate(1f/120);
                if(human.IsHeldBy(grab))
                {
                    Assert.That(Vector3.Distance(human.transform.TransformPoint(localGrip),socket.position),Is.LessThan(.003f),"Same physical grip point through wrist yaw; no recentering");
                    Assert.That(Vector3.Distance(human.transform.position,socket.position),Is.EqualTo(gripDistance).Within(.003f));
                    Assert.That(Vector3.Dot(human.transform.up,Vector3.up),Is.GreaterThan(.999f));
                    Assert.That(human.transform.parent,Is.Null);
                }
                if(grab.Elapsed>=.9f && grab.Elapsed<=3.14f)
                {
                    float depth=DistalArmTorsoPenetration(left);
                    if(depth>maximum) {maximum=depth;worst=" hand="+left+" phase="+phase+" elapsed="+grab.Elapsed;}
                }
            }
            Assert.That(grab.BiteHitCount,Is.EqualTo(++expectedBites),grab.LastResult);grab.TickAttack(10);
        }
        Debug.Log("TITAN_CARRY_TORSO_PENETRATION maximum="+maximum+worst);
        Assert.That(maximum,Is.LessThan(.005f),"Forearm/palm must not pass through chest/spine; shoulder attachment is not measured"+worst);
        yield return null;
    }
    [UnityTest] public IEnumerator FastMovingControllerCanBeCaughtAndBittenWithoutAnyWarningPose()
    {
        controller.height=1.8f;controller.radius=.32f;controller.center=Vector3.up*.9f;controller.stepOffset=.3f;
        flight.Launch(new Vector3(-10,7,9),new Vector3(11.5f,0,-7.65f));grab.enableBite=true;
        bool began=false;float maximum=0,torsoDepth=0;var bones=new[]{body.rightUpperArm,body.rightLowerArm,body.rightHand};
        var rotations=new Quaternion[3];float selectedAt=0;
        for(int i=0;i<620;i++)
        {
            Physics.SyncTransforms();flight.TickFlight(1f/120);
            if(!began)
            {
                body.animator.Update(1f/120);
                if(!grab.TryBeginAerialGrab(human,false)) {scene.GetPhysicsScene().Simulate(1f/120);continue;}
                began=true;selectedAt=i/120f;Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.Reach));
                for(int j=0;j<3;j++) rotations[j]=bones[j].rotation;
            }
            grab.TickAttack(1f/120);body.animator.Update(1f/120);grab.ApplyPose();human.TickHold(1f/120);
            scene.GetPhysicsScene().Simulate(1f/120);
            for(int j=0;j<3;j++) {maximum=Mathf.Max(maximum,Quaternion.Angle(rotations[j],bones[j].rotation));rotations[j]=bones[j].rotation;}
            if(grab.Elapsed>=.9f && grab.Elapsed<=3.14f) torsoDepth=Mathf.Max(torsoDepth,DistalArmTorsoPenetration(false));
            if(!grab.IsBusy) break;
        }
        Debug.Log("TITAN_FAST_MOVING_CONTINUITY max120HzStep="+maximum+" selectedAt="+selectedAt);
        Assert.That(began,Is.True);Assert.That(grab.CaptureCount,Is.EqualTo(1),grab.LastResult);
        Assert.That(grab.BiteHitCount,Is.EqualTo(1),grab.LastResult);Assert.That(maximum,Is.LessThan(8));
        Debug.Log("TITAN_MOVING_CARRY_TORSO_PENETRATION maximum="+torsoDepth);
        Assert.That(torsoDepth,Is.LessThan(.005f));yield return null;
    }
    [UnityTest] public IEnumerator MovingControllerInterceptionDoesNotRollTheArmAcrossTheCatch()
    {
        grab.aerialWindupDuration=.7f;grab.aerialReachDuration=.3f;
        // Reproduce the authored moving lab proxy, including its actual capsule.
        // A larger human on the same path can strike the shoulder before the palm.
        controller.height=1.8f;controller.radius=.32f;controller.center=Vector3.up*.9f;controller.stepOffset=.3f;
        flight.Launch(new Vector3(-10,7,9),new Vector3(11.5f,0,-7.65f));grab.enableBite=true;
        body.animator.Update(0);Physics.SyncTransforms();Assert.That(grab.TryBeginAerialGrab(human,false),Is.True);
        var bones=new[]{body.rightUpperArm,body.rightLowerArm,body.rightHand};var previousRotation=new Quaternion[3];
        for(int j=0;j<3;j++) previousRotation[j]=bones[j].rotation;
        float maximum=0;string worst="",biteResult="";
        for(int i=0;i<550 && grab.IsBusy;i++)
        {
            int bites=grab.BiteContactCount;
            Physics.SyncTransforms();flight.TickFlight(1f/120);grab.TickAttack(1f/120);
            if(grab.BiteContactCount!=bites) biteResult=grab.LastResult;
            body.animator.Update(1f/120);grab.ApplyPose();human.TickHold(1f/120);
            scene.GetPhysicsScene().Simulate(1f/120);
            for(int j=0;j<3;j++)
            {
                float step=Quaternion.Angle(previousRotation[j],bones[j].rotation);
                if(grab.Elapsed>.25f && step>maximum) {maximum=step;worst=" bone="+bones[j].name+" time="+grab.Elapsed+" stage="+grab.Stage;}
                previousRotation[j]=bones[j].rotation;
            }
        }
        Debug.Log("TITAN_MOVING_AERIAL_CONTINUITY max120HzStep="+maximum+worst);
        Assert.That(grab.CaptureCount,Is.EqualTo(1),"Moving catch: "+grab.LastResult);Assert.That(grab.BiteHitCount,Is.EqualTo(1),"Moving bite: "+biteResult);
        Assert.That(maximum,Is.LessThan(8),worst);yield return null;
    }
    [UnityTest] public IEnumerator AerialArmRemainsContinuousThroughCatchBiteAndRelease()
    {
        grab.enableBite=true;float maximum=0;string worst="";
        foreach(bool left in new[]{true,false})
        {
            Begin(left);
            var bones=new[]{left?body.leftUpperArm:body.rightUpperArm,left?body.leftLowerArm:body.rightLowerArm,left?body.leftHand:body.rightHand};
            var rotations=new Quaternion[3];for(int j=0;j<3;j++) rotations[j]=bones[j].rotation;
            for(int i=0;i<550 && grab.IsBusy;i++)
            {
                grab.TickAttack(1f/120);body.animator.Update(1f/120);grab.ApplyPose();human.TickHold(1f/120);
                for(int j=0;j<3;j++)
                {
                    float step=Quaternion.Angle(rotations[j],bones[j].rotation);
                    if(grab.Elapsed>.25f && step>maximum) { maximum=step; worst=" bone="+bones[j].name+" time="+grab.Elapsed+" stage="+grab.Stage; }
                    rotations[j]=bones[j].rotation;
                }
            }
            Assert.That(grab.CaptureCount,Is.EqualTo(left?1:2));grab.TickAttack(10);
        }
        Debug.Log("TITAN_AERIAL_CONTINUITY max120HzStep="+maximum+worst);
        Assert.That(maximum,Is.LessThan(8),"Catch offsets must remain latched through lethal release/recovery too"+worst);
        yield return null;
    }
    [UnityTest] public IEnumerator AerialCaptureCanContinueIntoExistingContactValidatedBite()
    {
        grab.enableBite=true;Begin(false);Capture();
        for(int i=0;i<500 && grab.IsBusy;i++)
        { grab.TickAttack(1f/120); body.animator.Update(1f/120);grab.ApplyPose();human.TickHold(1f/120); }
        Assert.That(grab.BiteContactCount,Is.EqualTo(1));
        Assert.That(grab.BiteHitCount,Is.EqualTo(1),grab.LastResult);
        Assert.That(human.IsDead,Is.True);Assert.That(human.IsHeld,Is.False);
        Assert.That(human.transform.position.y,Is.GreaterThan(5));
        yield return null;
    }
}
#endif
