#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class TitanStompPlayModeTests
{
    Scene scene, previous;
    ModularTitanBody body;
    TitanStompAttack attack;
    TitanCombatTarget human;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previous=SceneManager.GetActiveScene();
        scene=SceneManager.CreateScene("Titan_Stomp_Test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SceneManager.SetActiveScene(scene);
        var floor=new GameObject("Stomp floor");floor.transform.position=Vector3.down*.25f;
        floor.AddComponent<BoxCollider>().size=new Vector3(80,.5f,80);
        SpawnBody();
        human=Target(new Vector3(.7f,0,5.7f));
        SceneManager.SetActiveScene(previous);
        yield return null;
        Physics.SyncTransforms();
    }
    void SpawnBody()
    {
        body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
        if(body.gameObject.scene!=scene)SceneManager.MoveGameObjectToScene(body.gameObject,scene);
        body.GetComponent<TitanMotionStyle>().automaticTick=false;
        attack=body.gameObject.AddComponent<TitanStompAttack>();attack.automaticTick=false;attack.drawTelegraph=false;
    }
    TitanCombatTarget Target(Vector3 position)
    {
        var go=new GameObject("Sandbox victim");SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=position;
        var col=go.AddComponent<BoxCollider>();col.center=Vector3.up;col.size=new Vector3(.8f,2,.8f);
        return go.AddComponent<TitanCombatTarget>();
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(previous);
        if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
    }
    void Begin(bool left=true)
    {
        Physics.SyncTransforms();Assert.That(attack.TryBeginStomp(human,left),Is.True);
    }
    void Contact() { attack.TickAttack(attack.windupDuration+attack.strikeDuration+.01f); }

    [UnityTest]
    public IEnumerator WindupIsReadableContactIsOnceAndLongFramesCannotSkipIt()
    {
        Begin();attack.TickAttack(.6f);
        Assert.That(attack.Stage,Is.EqualTo(TitanStompStage.Windup));Assert.That(human.Health,Is.EqualTo(100));
        attack.TickAttack(.3f);
        Assert.That(attack.Stage,Is.EqualTo(TitanStompStage.Recovery));Assert.That(human.Health,Is.EqualTo(60));
        Assert.That(human.HitCount,Is.EqualTo(1));Assert.That(attack.ImpactCount,Is.EqualTo(1));
        attack.TickAttack(1);Assert.That(attack.IsBusy,Is.False);
        Assert.That(attack.TryBeginStomp(human,false),Is.False,"Cooldown must prevent immediate repeated damage");
        attack.TickAttack(3);Begin(false);attack.TickAttack(4);
        Assert.That(human.Health,Is.EqualTo(20));Assert.That(human.HitCount,Is.EqualTo(2));
        Assert.That(attack.ImpactCount,Is.EqualTo(2));Assert.That(attack.IsBusy,Is.False);
        attack.TickAttack(3);Begin();Contact();Assert.That(human.IsDead);
        attack.TickAttack(.1f);
        Assert.That(attack.Stage,Is.EqualTo(TitanStompStage.Recovery),"A killing hit must still finish recovery, not snap back to idle");
        attack.TickAttack(1);Assert.That(attack.LastResult,Is.EqualTo("Recovered"));
        yield return null;
    }
    [UnityTest]
    public IEnumerator DodgingLeavesLockedLandingPointBehindAndMisses()
    {
        Begin();Vector3 point=attack.LockedPoint;
        attack.TickAttack(.3f);human.transform.position=new Vector3(12,0,8);Physics.SyncTransforms();Contact();
        Assert.That(attack.LockedPoint,Is.EqualTo(point));Assert.That(human.Health,Is.EqualTo(100));
        Assert.That(attack.ImpactCount,Is.EqualTo(1),"Committed stomp lands at the old point, rather than cancelling or homing");
        yield return null;
    }
    [UnityTest]
    public IEnumerator ImpactDeduplicatesCompoundVictimsAndExcludesAirborneTargets()
    {
        Begin();var other=Target(attack.LockedPoint+new Vector3(-.8f,0,.5f));
        var extra=new GameObject("Second hit collider");extra.transform.SetParent(other.transform,false);
        extra.AddComponent<BoxCollider>().center=Vector3.up;
        var airborne=Target(attack.LockedPoint+new Vector3(.4f,3.2f,0));
        Contact();Assert.That(human.HitCount,Is.EqualTo(1));Assert.That(other.HitCount,Is.EqualTo(1));
        Assert.That(other.Health,Is.EqualTo(60));Assert.That(airborne.Health,Is.EqualTo(100));
        yield return null;
    }
    [UnityTest]
    public IEnumerator StompDamagesControllerOnlyProxyAndUsesItsConfiguredCapsule()
    {
        Object.Destroy(human.GetComponent<BoxCollider>());
        var cc=human.gameObject.AddComponent<CharacterController>(); cc.height=1.8f; cc.center=Vector3.up*.9f; cc.radius=.32f;
        yield return null;
        Assert.That(TitanCombatTarget.ClosestCombatPoint(cc,human.transform.position+Vector3.up*3).y,
            Is.EqualTo(human.transform.position.y+1.8f).Within(.001f));
        Begin(); Contact();
        Assert.That(human.Health,Is.EqualTo(60)); Assert.That(human.HitCount,Is.EqualTo(1));
    }
    [UnityTest]
    public IEnumerator SceneryBlocksSelectionAndAlsoBlocksImpactDamage()
    {
        var wall=new GameObject("Stomp cover");SceneManager.MoveGameObjectToScene(wall,scene);
        wall.transform.position=new Vector3(.7f,4,4.95f);var col=wall.AddComponent<BoxCollider>();col.size=new Vector3(4,8,.15f);
        Physics.SyncTransforms();Assert.That(attack.TryBeginStomp(human,true),Is.False);
        col.enabled=false;Begin();col.enabled=true;Physics.SyncTransforms();Contact();
        Assert.That(human.Health,Is.EqualTo(100),"Stomp shock area cannot damage through a wall");
        yield return null;
    }
    [UnityTest]
    public IEnumerator BlindnessCancelsImmediatelyAndCannotLeaveDelayedDamage()
    {
        Begin();attack.TickAttack(.4f);body.StunEyes();
        Assert.That(attack.IsBusy,Is.False);attack.TickAttack(2);
        Assert.That(attack.ImpactCount,Is.EqualTo(0));Assert.That(human.Health,Is.EqualTo(100));
        Assert.That(attack.TryBeginStomp(human,true),Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator StrikingLegLossHandsCurrentPoseToInjuryAndDoesNotRotateDetachedLeg()
    {
        Begin();attack.TickAttack(.5f);body.animator.Update(0);attack.ApplyPose();
        var leg=body.limbs.First(l=>l.region==TitanHitRegion.LeftLeg);
        body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);
        var rotations=leg.bodies.Select(b=>b.transform.rotation).ToArray();
        Assert.That(attack.IsBusy,Is.False);attack.TickAttack(2);attack.ApplyPose();
        Assert.That(attack.ImpactCount,Is.EqualTo(0));Assert.That(body.animator.enabled,Is.False);
        for(int i=0;i<rotations.Length;i++)Assert.That(Quaternion.Angle(rotations[i],leg.bodies[i].transform.rotation),Is.LessThan(.1f));
        body.GetComponent<TitanInjuryPose>().TickPose(.8f);
        Assert.That(body.GetComponent<TitanInjuryPose>().State,Is.EqualTo(TitanInjuryState.KneelingOnRight));
        yield return null;
    }
    [UnityTest]
    public IEnumerator SupportingLegLossAlsoCancelsButArmLossDoesNot()
    {
        Begin();attack.TickAttack(.3f);body.Sever(TitanHitRegion.LeftArm,Vector3.zero);
        Assert.That(attack.IsBusy,Is.True,"An intact-legged Titan can stomp without arms");
        var arm=body.limbs.First(l=>l.region==TitanHitRegion.LeftArm);
        var rotations=arm.bodies.Select(b=>b.transform.rotation).ToArray();
        body.animator.Update(.02f);attack.ApplyPose();
        for(int i=0;i<rotations.Length;i++)Assert.That(Quaternion.Angle(rotations[i],arm.bodies[i].transform.rotation),Is.LessThan(.1f));
        body.Sever(TitanHitRegion.RightLeg,Vector3.zero);attack.TickAttack(1);
        Assert.That(attack.IsBusy,Is.False);Assert.That(attack.ImpactCount,Is.EqualTo(0));
        Assert.That(human.Health,Is.EqualTo(100));
        yield return null;
    }
    [UnityTest]
    public IEnumerator InvalidTargetsDisabledAttackAndSaturatedQueriesFailSafely()
    {
        human.transform.position=Vector3.back*5;Physics.SyncTransforms();Assert.That(attack.TryBeginStomp(human,true),Is.False);
        human.transform.position=Vector3.forward*18;Physics.SyncTransforms();Assert.That(attack.TryBeginStomp(human,true),Is.False);
        human.transform.position=new Vector3(.7f,0,5.7f);Begin();human.enabled=false;Contact();
        Assert.That(attack.IsBusy,Is.False);Assert.That(human.Health,Is.EqualTo(100));
        human.enabled=true;attack.TickAttack(3);Begin();attack.enabled=false;
        Assert.That(attack.IsBusy,Is.False);Assert.That(attack.TryBeginStomp(human,true),Is.False);
        attack.enabled=true;attack.TickAttack(3);Begin();
        for(int i=0;i<140;i++)
        {
            var go=new GameObject("Overlap saturation");SceneManager.MoveGameObjectToScene(go,scene);
            go.transform.position=attack.LockedPoint+Vector3.up;go.AddComponent<BoxCollider>().size=Vector3.one*.05f;
        }
        Contact();Assert.That(attack.LastQueryOverflow,Is.True);Assert.That(human.Health,Is.EqualTo(100));
        yield return null;
    }
    [UnityTest]
    public IEnumerator BothFeetHaveMirroredSupportedPosesAndRootDoesNotLungeWithTheSkeleton()
    {
        foreach(bool left in new[]{true,false})
        {
            body.transform.rotation=Quaternion.Euler(0,35,0);
            human.transform.position=body.transform.position+body.transform.forward*5.7f;
            body.animator.Update(0);Begin(left);
            Vector3 root=body.transform.position;
            var leg=body.limbs.First(l=>l.region==(left?TitanHitRegion.LeftLeg:TitanHitRegion.RightLeg));
            var support=body.limbs.First(l=>l.region==(left?TitanHitRegion.RightLeg:TitanHitRegion.LeftLeg));
            float lifted=0;
            for(int i=0;i<32;i++)
            {
                body.animator.Update(.025f);attack.TickAttack(.025f);attack.ApplyPose();Physics.SyncTransforms();
                lifted=Mathf.Max(lifted,leg.bodies[2].position.y);
                float supportMin=body.ragdollBones.First(b=>b.body==support.bodies[2]).collider.bounds.min.y;
                Assert.That(supportMin,Is.GreaterThan(-.12f),"Support foot cannot penetrate the floor");
                Assert.That(Vector3.Distance(root,body.transform.position),Is.LessThan(.001f));
            }
            Assert.That(lifted,Is.GreaterThan(2.5f),"Stomp must visibly lift its foot, not just run a damage timer");
            Assert.That(Vector3.ProjectOnPlane(leg.bodies[2].position-attack.LockedPoint,Vector3.up).magnitude,Is.LessThan(.8f),"Contact must agree with the fixed telegraph");
            float sole=body.ragdollBones.First(b=>b.body==leg.bodies[2]).collider.bounds.min.y;
            Assert.That(sole,Is.InRange(-.1f,.25f));
            for(int i=0;i<20;i++)
            {
                body.animator.Update(.025f);attack.TickAttack(.025f);attack.ApplyPose();Physics.SyncTransforms();
                foreach(var footBody in new[]{leg.bodies[2],support.bodies[2]})
                    Assert.That(body.ragdollBones.First(b=>b.body==footBody).collider.bounds.min.y,Is.GreaterThan(-.12f),"Recovery must keep feet above ground while the pelvis rises");
            }
            attack.TickAttack(4);attack.TickAttack(3);human.ResetHealth();
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator CurrentPoseJointFramesDoNotGenerateEnergyWithoutExternalForces()
    {
        body.animator.SetFloat("CycleOffset",0);body.animator.Play("Idle",0,0);body.animator.Update(0);
        Begin();attack.TickAttack(.55f);body.animator.Update(0);attack.ApplyPose();
        foreach(var root in scene.GetRootGameObjects())
            if(root!=body.gameObject)foreach(var col in root.GetComponentsInChildren<Collider>())col.enabled=false;
        body.Die();foreach(var bone in body.ragdollBones)bone.body.useGravity=false;
        float max=0;
        for(int i=0;i<50;i++)
        {
            scene.GetPhysicsScene().Simulate(.02f);
            foreach(var bone in body.ragdollBones)max=Mathf.Max(max,bone.body.velocity.magnitude);
        }
        Debug.Log("TITAN_UNFORCED_HANDOFF_METRICS maxSpeed="+max);
        Assert.That(max,Is.LessThan(.1f),"A neutral current-pose handoff must not inject energy with gravity and external contacts disabled");
        yield return null;
    }

    [UnityTest]
    public IEnumerator NapeDeathFromLoadedStompCancelsAndRemainsStable()
    {
        float worst=0;
        for(int phase=0;phase<8;phase++)
        {
            if(phase>0){Object.Destroy(body.gameObject);yield return null;SpawnBody();}
            body.animator.SetFloat("CycleOffset",phase/8f);body.animator.Play("Idle",0,0);body.animator.Update(0);
            Begin(phase%2==0);attack.TickAttack(.55f);body.animator.Update(0);attack.ApplyPose();body.Die();
            Assert.That(attack.IsBusy,Is.False);Assert.That(body.animator.enabled,Is.False);
            foreach(var joint in body.GetComponentsInChildren<CharacterJoint>())
                if(joint.connectedBody!=null)
                    Assert.That(Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)),Is.LessThan(.002f),"Current-pose anchors must coincide despite animated non-physical spine/shoulder intermediaries");
            var rotations=body.ragdollBones.Select(b=>b.body.transform.rotation).ToArray();
            attack.TickAttack(1);attack.ApplyPose();
            for(int i=0;i<rotations.Length;i++)Assert.That(Quaternion.Angle(rotations[i],body.ragdollBones[i].body.transform.rotation),Is.LessThan(.1f));
            yield return null;
            float max=0;int peakStep=0;string peakBody="";var physics=scene.GetPhysicsScene();
            for(int i=0;i<250;i++)
            {
                physics.Simulate(.02f);
                if(i%50==0)
                    for(int a=0;a<body.ragdollBones.Length;a++)
                        for(int b=a+1;b<body.ragdollBones.Length;b++)
                            Assert.That(Physics.GetIgnoreCollision(body.ragdollBones[a].collider,body.ragdollBones[b].collider),"Internal collision exclusion was lost during simulation");
                foreach(var bone in body.ragdollBones)
                    if(bone.body.velocity.magnitude>max)
                    {
                        if(max<=40 && bone.body.velocity.magnitude>40)
                        {
                            var joint=bone.body.GetComponent<CharacterJoint>();
                            var enabledPairs=new System.Collections.Generic.List<string>();
                            for(int a=0;a<body.ragdollBones.Length;a++)
                                for(int b=a+1;b<body.ragdollBones.Length;b++)
                                    if(!Physics.GetIgnoreCollision(body.ragdollBones[a].collider,body.ragdollBones[b].collider))enabledPairs.Add(body.ragdollBones[a].body.name+"/"+body.ragdollBones[b].body.name);
                            Debug.Log("TITAN_STOMP_PAIR_DIAGNOSTIC "+string.Join(",",enabledPairs));
                            Debug.Log("TITAN_STOMP_SPIKE phase="+phase+" step="+i+" body="+bone.body.name+" colliderMinY="+bone.collider.bounds.min.y+
                                " inertia="+bone.body.inertiaTensor+" angular="+bone.body.angularVelocity+" localRotation="+bone.body.transform.localEulerAngles+
                                " gap="+(joint!=null&&joint.connectedBody!=null?Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)):0));
                        }
                        max=bone.body.velocity.magnitude;peakStep=i;peakBody=bone.body.name;
                    }
            }
            Debug.Log("TITAN_STOMP_DEATH_METRICS phase="+phase+" maxSpeed="+max.ToString("F2")+" body="+peakBody+" step="+peakStep);
            worst=Mathf.Max(worst,max);
        }
        Assert.That(worst,Is.LessThan(40));Assert.That(human.Health,Is.EqualTo(100));
    }
}
#endif
