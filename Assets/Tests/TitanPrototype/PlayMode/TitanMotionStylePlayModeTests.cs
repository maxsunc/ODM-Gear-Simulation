#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class TitanMotionStylePlayModeTests
{
    Scene scene, previous;
    ModularTitanBody body;
    TitanMotionStyle motion;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previous=SceneManager.GetActiveScene();
        scene=SceneManager.CreateScene("Titan_Motion_Test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SceneManager.SetActiveScene(scene);
        var floor=new GameObject("Motion test floor");floor.transform.position=new Vector3(0,-.25f,0);
        floor.AddComponent<BoxCollider>().size=new Vector3(80,.5f,80);
        body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();
        motion=body.GetComponent<TitanMotionStyle>();Assert.That(motion,Is.Not.Null);
        motion.automaticTick=false;
        SceneManager.SetActiveScene(previous);
        yield return null;
        body.animator.Update(0);
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(previous);
        if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator GaitAndCadenceFollowMeasuredSpeedRatherThanRequestedPace()
    {
        motion.ResetStyle(31);
        motion.TickMotion(.5f,4.6f,false,0);
        Assert.That(motion.Gait,Is.LessThan(.05f));
        Assert.That(motion.StrideRate,Is.InRange(.90f,1.10f));
        motion.TickMotion(.5f,9.5f,true,0);
        Assert.That(motion.Gait,Is.GreaterThan(.95f));
        Assert.That(body.animator.GetFloat("Gait"),Is.EqualTo(motion.Gait).Within(.001f));
        Assert.That(motion.StrideRate,Is.InRange(.90f,1.10f));
        motion.TickMotion(.3f,3,false,0);
        Assert.That(motion.Gait,Is.LessThan(.05f));
        Assert.That(motion.StrideRate,Is.LessThan(.75f),"Stride rate must slow with actual root speed");
        motion.TickMotion(.3f,0,true,0);
        Assert.That(body.animator.GetBool("IsWalking"),Is.False,"A chase intent at rest must not run in place");
        Assert.That(body.animator.speed,Is.EqualTo(1),"Do not globally accelerate all Animator states");
        yield return null;
    }

    [UnityTest]
    public IEnumerator StyleVariationIsBoundedSmoothRepeatableAndDoesNotConsumeUnityRandom()
    {
        var global=UnityEngine.Random.state;
        UnityEngine.Random.InitState(441);var before=UnityEngine.Random.state;
        float[] first=Profile(123);
        float expected=UnityEngine.Random.value;UnityEngine.Random.state=before;
        Assert.That(UnityEngine.Random.value,Is.EqualTo(expected),"Local style must not perturb gameplay randomness");
        float[] second=Profile(123), different=Profile(987);
        for(int i=0;i<first.Length;i++)Assert.That(second[i],Is.EqualTo(first[i]).Within(.00001f));
        Assert.That(first.Zip(different,(a,b)=>Mathf.Abs(a-b)).Max(),Is.GreaterThan(.02f));
        Assert.That(first.Max()-first.Min(),Is.GreaterThan(.04f));
        UnityEngine.Random.state=global;
        yield return null;
    }
    float[] Profile(int seed)
    {
        motion.ResetStyle(seed);var values=new float[500];float old=1;
        for(int i=0;i<values.Length;i++)
        {
            motion.TickMotion(.02f,9.5f,true,0);values[i]=motion.PaceScale;
            Assert.That(values[i],Is.InRange(.51f,1.201f));
            Assert.That(Mathf.Abs(values[i]-old),Is.LessThan(.057f),"No per-frame random jitter");old=values[i];
        }
        return values;
    }

    [UnityTest]
    public IEnumerator WalkAndRunAreDistinctFastLoopingPosesWithSupportedFeet()
    {
        body.animator.enabled=false;
        var knee=body.limbs.First(l=>l.region==TitanHitRegion.LeftLeg).bodies[1].transform;
        Quaternion rest=knee.localRotation;
        float walkBend=0,runBend=0;
        foreach(string name in new[]{"Walk","Run"})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Titans/Prototype/Animations/"+name+".anim");
            Assert.That(clip,Is.Not.Null);Assert.That(clip.length,Is.LessThan(name=="Run"?.8f:1.1f));
            clip.SampleAnimation(body.animator.gameObject,0);
            var bones=body.ragdollBones.Select(b=>b.body.transform).ToArray();
            var rotations=bones.Select(b=>b.localRotation).ToArray();var positions=bones.Select(b=>b.localPosition).ToArray();
            for(int frame=0;frame<=64;frame++)
            {
                clip.SampleAnimation(body.animator.gameObject,clip.length*frame/64f);
                float bend=Quaternion.Angle(rest,knee.localRotation);
                if(name=="Run")runBend=Mathf.Max(runBend,bend);else walkBend=Mathf.Max(walkBend,bend);
                Physics.SyncTransforms();
                float footMin=body.limbs.Where(l=>l.region==TitanHitRegion.LeftLeg||l.region==TitanHitRegion.RightLeg).Min(l=>l.bodies[2].GetComponentsInChildren<Collider>().First(c=>!c.isTrigger && c.attachedRigidbody==l.bodies[2]).bounds.min.y);
                Assert.That(footMin,Is.GreaterThan(-.04f),name+" foot clipping");
                float soleMin=LowestVisibleFoot();
                Assert.That(soleMin,Is.InRange(-.12f,.25f),name+" visible sole must track support, not float above a tilted collider");
                Assert.That(body.hips.position.y,Is.InRange(4.3f,6.0f),"Contact correction must not teleport the pelvis");
            }
            for(int i=0;i<bones.Length;i++)
            {
                Assert.That(Quaternion.Angle(rotations[i],bones[i].localRotation),Is.LessThan(.2f),name+" rotational loop seam");
                Assert.That(Vector3.Distance(positions[i],bones[i].localPosition),Is.LessThan(.001f),name+" position loop seam");
            }
        }
        Assert.That(runBend,Is.GreaterThan(walkBend+15),"Run must have its own stronger recovery, not only faster playback");
        yield return null;
    }

    float LowestVisibleFoot()
    {
        float min=float.PositiveInfinity;
        foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name.EndsWith("Foot")))
        {
            Assert.That(skin.bones.Length,Is.EqualTo(1),"Current foot skin is rigidly weighted to its foot bone");
            Matrix4x4 matrix=skin.bones[0].localToWorldMatrix*skin.sharedMesh.bindposes[0];
            foreach(var vertex in skin.sharedMesh.vertices) min=Mathf.Min(min,matrix.MultiplyPoint3x4(vertex).y);
        }
        return min;
    }

    [UnityTest]
    public IEnumerator HeadLeadsSharpPivotThenRootCommitsAndInjuryStopsTurning()
    {
        motion.TickMotion(.1f,8,true,110);
        Assert.That(motion.CornerSpeedScale,Is.LessThan(.5f));
        Quaternion root=body.transform.rotation,head=body.head.rotation;
        motion.TickTurning(.04f,110,true);
        Assert.That(Quaternion.Angle(root,body.transform.rotation),Is.LessThan(.01f),"Brief head lead before root pivot");
        Assert.That(Quaternion.Angle(head,body.head.rotation),Is.GreaterThan(8));
        body.animator.Update(.04f);
        motion.TickTurning(.10f,110,true);
        Assert.That(Quaternion.Angle(root,body.transform.rotation),Is.GreaterThan(15));
        body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);
        root=body.transform.rotation;
        motion.TickTurning(1,-150,true);
        Assert.That(Quaternion.Angle(root,body.transform.rotation),Is.LessThan(.01f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FasterCollapseHasBuckleDropAndImpactRatherThanUniformEase()
    {
        var injury=body.GetComponent<TitanInjuryPose>();
        Assert.That(injury.kneelDuration,Is.InRange(.5f,.7f));Assert.That(injury.proneDuration,Is.InRange(.6f,.9f));
        Assert.That(TitanInjuryPose.FallWeight(.1f),Is.LessThan(.08f));
        Assert.That(TitanInjuryPose.FallWeight(.5f),Is.GreaterThan(.75f));
        Assert.That(TitanInjuryPose.FallWeight(.7f),Is.GreaterThan(1),"Small impact overshoot before settling");
        Assert.That(TitanInjuryPose.FallWeight(1),Is.EqualTo(1).Within(.00001f));
        body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);injury.TickPose(.7f);
        Assert.That(injury.IsTransitioning,Is.False);
        body.Sever(TitanHitRegion.RightLeg,Vector3.zero);injury.TickPose(.9f);
        Assert.That(injury.IsTransitioning,Is.False);Assert.That(body.IsHelpless);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RunningCutPreservesCurrentSurvivingAndSeveredVisualPose()
    {
        motion.TickMotion(.5f,9.5f,true,0);body.SetWalking(true);body.animator.Update(.25f);
        var limb=body.limbs.First(l=>l.region==TitanHitRegion.LeftArm);
        var rotations=limb.bodies.Select(b=>b.transform.rotation).ToArray();
        Vector3 hips=body.hips.position;
        float cycle=body.animator.GetFloat("CycleOffset"),gait=body.animator.GetFloat("Gait");
        body.Sever(TitanHitRegion.LeftArm,Vector3.zero);
        Assert.That(body.animator.GetFloat("CycleOffset"),Is.EqualTo(cycle).Within(.001f));
        Assert.That(body.animator.GetFloat("Gait"),Is.EqualTo(gait).Within(.001f));
        Assert.That(Vector3.Distance(hips,body.hips.position),Is.LessThan(.001f),"Rebind must not erase running hip contact pose");
        for(int i=0;i<rotations.Length;i++)Assert.That(Quaternion.Angle(rotations[i],limb.bodies[i].transform.rotation),Is.LessThan(.1f));
        yield return null;
        for(int i=0;i<rotations.Length;i++)Assert.That(Quaternion.Angle(rotations[i],limb.bodies[i].transform.rotation),Is.LessThan(.1f),"Style must not pose detached arms");
    }

    [UnityTest]
    public IEnumerator EyeHitSnapsHeadThenCoversEyesQuickly()
    {
        Quaternion before=body.head.rotation;body.StunEyes();
        yield return new WaitForSeconds(.07f);
        Assert.That(Quaternion.Angle(before,body.head.rotation),Is.GreaterThan(1.5f),"Immediate flinch before the sustained eye cover");
        yield return new WaitForSeconds(.13f);
        Assert.That(Vector3.Distance(body.leftHand.position,body.head.position),Is.LessThan(3.5f));
        Assert.That(Vector3.Distance(body.rightHand.position,body.head.position),Is.LessThan(3.5f));
        Assert.That(body.IsBlind && !body.CanWalk);
    }
}
#endif
