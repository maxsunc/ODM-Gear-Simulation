#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Isolated alive injury poses and subsequent death; disposable validation project only.</summary>
public class TitanInjuryPosePlayModeTests
{
    Scene scene, previous;
    PhysicsScene physics;
    ModularTitanBody body;
    TitanInjuryPose injury;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previous = SceneManager.GetActiveScene();
        scene = SceneManager.CreateScene("Titan_Injury_Test", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        physics = scene.GetPhysicsScene();
        SceneManager.SetActiveScene(scene);
        var floor = new GameObject("Injury test floor"); floor.transform.position = new Vector3(0,-.25f,0);
        floor.AddComponent<BoxCollider>().size = new Vector3(80,.5f,80);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
        body = Object.Instantiate(prefab).GetComponent<ModularTitanBody>();
        injury = body.GetComponent<TitanInjuryPose>();
        Assert.That(injury, Is.Not.Null);
        injury.automaticTick = false;
        SceneManager.SetActiveScene(previous);
        yield return null;
        body.animator.Update(0);
        Physics.SyncTransforms();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(previous);
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator LosingLeftLegKneelsOnSurvivingRightKneeWithHeadDown()
    {
        yield return CheckSingleLeg(TitanHitRegion.LeftLeg, TitanHitRegion.RightLeg, TitanInjuryState.KneelingOnRight);
    }

    [UnityTest]
    public IEnumerator LosingRightLegMirrorsTheKneelOnSurvivingLeftKnee()
    {
        body.transform.rotation = Quaternion.Euler(0,57,0);
        yield return CheckSingleLeg(TitanHitRegion.RightLeg, TitanHitRegion.LeftLeg, TitanInjuryState.KneelingOnLeft);
    }

    IEnumerator CheckSingleLeg(TitanHitRegion cut, TitanHitRegion retained, TitanInjuryState expected)
    {
        Vector3 rootPosition = body.transform.position;
        Vector3 hipsBefore = body.hips.position;
        Assert.That(body.Sever(cut, Vector3.zero));
        Assert.That(injury.State, Is.EqualTo(expected));
        Assert.That(injury.IsTransitioning);
        Assert.That(Vector3.Distance(hipsBefore, body.hips.position), Is.LessThan(.001f), "No instantaneous teleport into final pose");
        for (int i = 0; i < 30; i++)
        {
            injury.TickPose(injury.kneelDuration / 30f);
            AssertAboveGround();
        }
        injury.TickPose(.01f);
        var leg = body.limbs.First(l => l.region == retained);
        Transform upper = leg.bodies[0].transform, knee = leg.bodies[1].transform, foot = leg.bodies[2].transform;
        Assert.That(body.hips.position.y, Is.InRange(2.4f,4.1f));
        Assert.That(knee.position.y, Is.InRange(.4f,1.3f), "Retained knee must reach the ground");
        Assert.That(foot.position.y, Is.LessThan(1.4f));
        Assert.That(Vector3.Dot((foot.position-knee.position).normalized, -body.transform.forward), Is.GreaterThan(.85f), "Shin folds behind the retained knee");
        Assert.That(Vector3.Dot((knee.position-upper.position).normalized, Vector3.down), Is.GreaterThan(.8f));
        Assert.That(Vector3.Dot(body.head.forward, Vector3.down), Is.GreaterThan(.5f), "Head must look down");
        Assert.That(Vector3.Distance(rootPosition, body.transform.position), Is.LessThan(.001f), "Pose must not move the NavMesh root");
        Assert.That(!body.IsDead && !body.CanWalk && !body.IsHelpless);
        Assert.That(body.CanGrabLeft && body.CanGrabRight, "A kneeling Titan still has its arms for later attack work");
        Assert.That(!body.animator.enabled && !injury.IsTransitioning);
        Assert.That(leg.bodies.All(b => b.isKinematic));
        AssertSkinAboveGround();
        yield return null;
    }

    [UnityTest]
    public IEnumerator SecondLegCutBlendsFromKneelToHelplessProneWithoutMovingDetachedBones()
    {
        body.transform.rotation = Quaternion.Euler(0,37,0);
        body.Sever(TitanHitRegion.LeftLeg, Vector3.zero);
        injury.TickPose(injury.kneelDuration + .1f);
        var second = body.limbs.First(l => l.region == TitanHitRegion.RightLeg);
        var rotations = second.bodies.Select(b => b.transform.rotation).ToArray();
        var positions = second.bodies.Select(b => b.transform.position).ToArray();
        Vector3 hipsBefore = body.hips.position;
        body.Sever(TitanHitRegion.RightLeg, Vector3.zero);
        Assert.That(injury.State, Is.EqualTo(TitanInjuryState.Prone));
        Assert.That(Vector3.Distance(hipsBefore, body.hips.position), Is.LessThan(.001f));
        for (int i = 0; i < second.bodies.Length; i++)
        {
            Assert.That(Vector3.Distance(positions[i], second.bodies[i].transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotations[i], second.bodies[i].transform.rotation), Is.LessThan(.1f), "Second leg cut must preserve the folded animated orientation");
        }
        for (int i = 0; i < 40; i++) { injury.TickPose(injury.proneDuration / 40); AssertAboveGround(); }
        injury.TickPose(.01f);
        AssertProne();
        for (int i = 0; i < second.bodies.Length; i++)
            Assert.That(Quaternion.Angle(rotations[i], second.bodies[i].transform.rotation), Is.LessThan(.1f), "Alive pose animation must never write to a severed branch");
        AssertSkinAboveGround();
        yield return null;
    }

    [UnityTest]
    public IEnumerator RapidDoubleLegCutAndMissingArmsStillSettleProne()
    {
        body.Sever(TitanHitRegion.LeftArm, Vector3.zero);
        body.Sever(TitanHitRegion.RightArm, Vector3.zero);
        body.Sever(TitanHitRegion.RightLeg, Vector3.zero);
        injury.TickPose(.2f);
        body.Sever(TitanHitRegion.LeftLeg, Vector3.zero);
        injury.TickPose(injury.proneDuration + .1f);
        AssertProne();
        AssertSkinAboveGround();
        Assert.That(body.hitZones.Where(z => z.region == TitanHitRegion.Nape || z.region == TitanHitRegion.Eyes).All(z => z.gameObject.activeSelf), "Helpless does not mean dead/invulnerable");
        yield return null;
    }

    [UnityTest]
    public IEnumerator BlindRecoveryCannotStandTheInjuredTitanBackUp()
    {
        body.Sever(TitanHitRegion.LeftLeg, Vector3.zero);
        injury.TickPose(injury.kneelDuration + .1f);
        Vector3 hips = body.hips.position;
        body.StunEyes();
        injury.automaticTick = true;
        yield return new WaitForSeconds(.15f);
        Assert.That(Vector3.Distance(hips, body.hips.position), Is.LessThan(.02f));
        Assert.That(Vector3.Dot(body.head.forward, Vector3.down), Is.GreaterThan(.5f));
        body.TickStatus(body.eyeStunDuration + 1);
        yield return null;
        Assert.That(!body.IsBlind && !body.CanWalk && !body.animator.enabled);
        Assert.That(injury.State, Is.EqualTo(TitanInjuryState.KneelingOnRight));
    }

    [UnityTest]
    public IEnumerator NapeDeathFromKneelingHandsCurrentPoseToStablePhysics()
    {
        body.Sever(TitanHitRegion.RightLeg, Vector3.zero);
        injury.TickPose(injury.kneelDuration + .1f);
        yield return CheckDeath();
    }

    [UnityTest]
    public IEnumerator NapeDeathDuringProneCollapseDoesNotResumeAlivePoseAnimation()
    {
        body.Sever(TitanHitRegion.LeftLeg, Vector3.zero);
        injury.TickPose(.4f);
        body.Sever(TitanHitRegion.RightLeg, Vector3.zero);
        injury.TickPose(.7f);
        yield return CheckDeath();
    }

    IEnumerator CheckDeath()
    {
        body.Die(Vector3.zero);
        Assert.That(body.IsDead && !body.animator.enabled && !injury.IsTransitioning);
        Vector3 before = body.hips.position; Quaternion rotation = body.hips.rotation;
        injury.TickPose(100);
        Assert.That(Vector3.Distance(before, body.hips.position), Is.LessThan(.001f));
        Assert.That(Quaternion.Angle(rotation, body.hips.rotation), Is.LessThan(.1f));
        // Complete Unity's deferred destruction of severed root joints before manually
        // stepping the local physics scene (as normal frame-end destruction would).
        yield return null;
        Physics.SyncTransforms();
        float maxSpeed = 0;
        for (int i = 0; i < 250; i++)
        {
            physics.Simulate(.02f);
            foreach (var entry in body.ragdollBones)
            {
                Assert.That(float.IsNaN(entry.body.position.y) || float.IsInfinity(entry.body.position.y), Is.False);
                Assert.That(entry.body.position.y, Is.GreaterThan(-1));
                maxSpeed = Mathf.Max(maxSpeed, entry.body.velocity.magnitude);
            }
            if (i % 25 == 0) yield return null;
        }
        Assert.That(maxSpeed, Is.LessThan(40), "Injured-pose death must not explode");
        Debug.Log("TITAN_INJURY_DEATH_METRICS state=" + injury.State + " maxSpeed=" + maxSpeed.ToString("F2") + " hipsY=" + body.hips.position.y.ToString("F2"));
    }

    void AssertProne()
    {
        Assert.That(injury.State, Is.EqualTo(TitanInjuryState.Prone));
        Assert.That(body.IsHelpless && !body.IsDead && !body.CanWalk && !body.CanGrabLeft && !body.CanGrabRight);
        Assert.That(body.hips.position.y, Is.InRange(.6f,1.6f));
        Assert.That(body.head.position.y, Is.LessThan(2));
        Assert.That(Mathf.Abs(Vector3.Dot((body.head.position-body.hips.position).normalized, Vector3.up)), Is.LessThan(.1f), "Torso lies horizontally");
        Assert.That(Vector3.Dot(body.head.forward, Vector3.down), Is.GreaterThan(.9f));
        Assert.That(body.ragdollBones.Where(b => b.body.transform.IsChildOf(body.transform)).All(b => b.body.isKinematic));
        AssertAboveGround();
    }

    void AssertAboveGround()
    {
        Physics.SyncTransforms();
        foreach (var entry in body.ragdollBones.Where(b => b.collider.transform.IsChildOf(body.transform)))
            Assert.That(entry.collider.bounds.min.y, Is.GreaterThan(-.02f), "Ground clipping: " + entry.body.name);
    }

    void AssertSkinAboveGround()
    {
        float min = float.PositiveInfinity;
        foreach (var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            // Direct linear skinning avoids BakeMesh's scale ambiguity on the FBX's
            // 100x skin transforms. These are actual world-space mesh positions.
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices; var weights = mesh.boneWeights;
            var bindposes = mesh.bindposes;
            var matrices = skin.bones.Select((bone, i) => bone.localToWorldMatrix * bindposes[i]).ToArray();
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                Vector3 point = matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i]) * w.weight0;
                if (w.weight1 > 0) point += matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i]) * w.weight1;
                if (w.weight2 > 0) point += matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i]) * w.weight2;
                if (w.weight3 > 0) point += matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i]) * w.weight3;
                min = Mathf.Min(min, point.y);
            }
        }
        Assert.That(min, Is.GreaterThan(-.15f), "Visible skin must not sink through floor");
    }
}
#endif
