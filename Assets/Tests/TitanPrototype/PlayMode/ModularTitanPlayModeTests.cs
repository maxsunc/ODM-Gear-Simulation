#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Use the disposable validation project runner, not the user's open gameplay scene.</summary>
public class ModularTitanPlayModeTests
{
    Scene scene, previous;
    PhysicsScene physics;
    ModularTitanBody body;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previous = SceneManager.GetActiveScene();
        scene = SceneManager.CreateScene("Titan_Runtime_Test", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        physics = scene.GetPhysicsScene();
        Assert.That(physics.IsValid() && physics != Physics.defaultPhysicsScene);
        SceneManager.SetActiveScene(scene);
        var floor = new GameObject("Isolated Test Floor");
        floor.transform.position = new Vector3(0,-.25f,0);
        floor.AddComponent<BoxCollider>().size = new Vector3(80,.5f,80);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
        Assert.That(prefab, Is.Not.Null);
        body = Object.Instantiate(prefab).GetComponent<ModularTitanBody>();
        SceneManager.SetActiveScene(previous);
        yield return null;
        body.animator.Update(0);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(previous);
        if (scene.IsValid() && scene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator IntactBodyHasWorkingAnimationAndKinematicPhysics()
    {
        Assert.That(body.ragdollBones.Length, Is.EqualTo(17));
        Assert.That(body.GetComponentsInChildren<CharacterJoint>().Length, Is.EqualTo(16));
        Assert.That(body.hitZones.Length, Is.EqualTo(14));
        Assert.That(body.limbs.Length, Is.EqualTo(4));
        Assert.That(body.ragdollBones.All(b => b.body.isKinematic && b.collider.enabled && !b.collider.isTrigger));
        Assert.That(body.ragdollBones.All(b => b.body.interpolation == RigidbodyInterpolation.None), "Animated bones must not replay stale physics world poses when the root moves");
        Assert.That(body.CanGrabLeft && body.CanGrabRight && body.CanWalk);
        var rendererBounds = new Bounds(body.hips.position, Vector3.zero);
        foreach (var renderer in body.GetComponentsInChildren<Renderer>()) rendererBounds.Encapsulate(renderer.bounds);
        Assert.That(rendererBounds.size.y, Is.InRange(11f, 13f), "12m model scale");
        foreach (var zone in body.hitZones.Where(z => z.region == TitanHitRegion.Nape || z.region == TitanHitRegion.Eyes))
            Assert.That(zone.GetComponent<Collider>().bounds.size.magnitude, Is.LessThan(2f), "Face zones must not inherit 100x FBX scale");
        body.SetWalking(true);
        body.animator.Update(.3f);
        var leg = body.limbs.First(l => l.region == TitanHitRegion.LeftLeg).rootBone;
        Quaternion rotation = leg.localRotation;
        float excursion = 0;
        // Per-Titan cycle offsets deliberately desynchronize the gait. Sample a
        // whole cycle instead of two potentially symmetric poses in that cycle.
        for (int i = 0; i < 10; i++)
        {
            body.animator.Update(.11f);
            excursion = Mathf.Max(excursion, Quaternion.Angle(rotation, leg.localRotation));
        }
        Assert.That(excursion, Is.GreaterThan(15f), "Native walk curves must move the legs through a complete stride");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ImportedSkinsMatchSourceMeshGeometryAndUvs()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Titans/Prototype/TitanPrototype.fbx");
        Assert.That(model, Is.Not.Null);
        var sources = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var source = sources.First(s => s.name.Replace("TitanDetailed_", "Titan_").Replace("TitanReference_", "Titan_") == skin.name).sharedMesh;
            var actual = skin.sharedMesh;
            Assert.That(actual.vertexCount, Is.EqualTo(source.vertexCount), "Stale native mesh buffer: " + skin.name);
            Assert.That(actual.subMeshCount, Is.EqualTo(source.subMeshCount));
            var vertices = actual.vertices; var original = source.vertices;
            for (int i = 0; i < vertices.Length; i++)
                Assert.That(Vector3.Distance(vertices[i], original[i]), Is.LessThan(.000001f), skin.name + " geometry mismatch");
            var uv = actual.uv; var sourceUv = source.uv;
            Assert.That(uv.Length, Is.EqualTo(sourceUv.Length), skin.name + " stale UV buffer");
            for (int i = 0; i < uv.Length; i++)
                Assert.That(Vector2.Distance(uv[i], sourceUv[i]), Is.LessThan(.000001f), skin.name + " texture coordinates changed");
            for (int sub = 0; sub < actual.subMeshCount; sub++)
                Assert.That(actual.GetIndexCount(sub), Is.EqualTo(source.GetIndexCount(sub)));
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator EyeStunRecoversAndOnlySurvivingHandsPose()
    {
        int events = 0;
        body.BlindnessChanged += value => events++;
        body.ApplyDamage(TitanHitRegion.Eyes, 1, Vector3.zero, Vector3.zero);
        Assert.That(body.IsBlind && !body.CanGrabLeft && !body.CanGrabRight && !body.CanWalk);
        yield return new WaitForSeconds(.25f);
        Assert.That(Vector3.Distance(body.leftHand.position, body.head.position), Is.LessThan(3.5f), "Eye reaction hand pose");
        body.TickStatus(body.eyeStunDuration + 1);
        Assert.That(!body.IsBlind && body.CanGrabLeft && body.CanGrabRight && body.CanWalk);
        Assert.That(events, Is.EqualTo(2));
        body.Sever(TitanHitRegion.LeftArm, Vector3.zero);
        body.StunEyes();
        yield return null;
        body.TickStatus(body.eyeStunDuration + 1);
        Assert.That(!body.CanGrabLeft && body.CanGrabRight, "Recovery must not restore a missing arm");
    }

    [UnityTest]
    public IEnumerator AllFourBranchesDetachTheirOwnSkinsAndIgnoreRepeatHits()
    {
        body.ApplyDamage(TitanHitRegion.LeftArm, 20, Vector3.zero, Vector3.zero);
        Assert.That(body.IsSevered(TitanHitRegion.LeftArm), Is.False);
        foreach (var region in new[] { TitanHitRegion.LeftArm, TitanHitRegion.RightArm, TitanHitRegion.LeftLeg, TitanHitRegion.RightLeg })
        {
            var limb = body.limbs.First(l => l.region == region);
            body.ApplyDamage(region, body.limbHealth, Vector3.zero, Vector3.zero);
            Assert.That(body.IsSevered(region));
            Assert.That(limb.bodies.All(b => !b.isKinematic));
            Assert.That(limb.rootBone.IsChildOf(body.transform), Is.False);
            Assert.That(limb.renderers.Length, Is.EqualTo(3));
            Assert.That(limb.renderers.All(r => r.transform.IsChildOf(limb.rootBone.parent)));
            Assert.That(limb.hitZones.All(z => !z.gameObject.activeSelf));
            foreach (var skin in limb.renderers.Cast<SkinnedMeshRenderer>())
                Assert.That(skin.bones.All(b => b == limb.rootBone || b.IsChildOf(limb.rootBone)), "Skin has external bone references");
            Assert.That(body.Sever(region, Vector3.zero), Is.False);
            yield return null;
            Assert.That(limb.rootBone.GetComponent<CharacterJoint>(), Is.Null, "Root joint must be removed");
        }
        Assert.That(!body.CanGrabLeft && !body.CanGrabRight && !body.CanWalk);
        body.Die();
        Assert.That(body.ragdollBones.All(b => !b.body.isKinematic), "Death must not re-freeze removed branches");
        yield return null;
    }

    [UnityTest]
    public IEnumerator SeveredLegsKeepTheirAnimatedPoseWhenAnimatorRebinds()
    {
        body.transform.rotation = Quaternion.Euler(0, 37, 0);
        body.SetWalking(true);
        body.animator.Update(.37f);
        foreach (var region in new[] { TitanHitRegion.LeftLeg, TitanHitRegion.RightLeg })
        {
            var limb = body.limbs.First(l => l.region == region);
            var bones = limb.bodies.Select(b => b.transform).ToArray();
            var positions = bones.Select(b => b.position).ToArray();
            var rotations = bones.Select(b => b.rotation).ToArray();
            var skins = limb.renderers.Cast<SkinnedMeshRenderer>().ToArray();
            var vertices = skins.Select(CaptureWorldVertices).ToArray();
            Assert.That(body.Sever(region, Vector3.zero));
            for (int i = 0; i < bones.Length; i++)
            {
                Assert.That(Vector3.Distance(positions[i], bones[i].position), Is.LessThan(.001f), region + " bone moved at cut: " + bones[i].name);
                Assert.That(Quaternion.Angle(rotations[i], bones[i].rotation), Is.LessThan(.1f), region + " bone flipped at cut: " + bones[i].name);
            }
            for (int i = 0; i < skins.Length; i++)
            {
                var after = CaptureWorldVertices(skins[i]);
                Assert.That(after.Length, Is.EqualTo(vertices[i].Length));
                for (int j = 0; j < after.Length; j++)
                    Assert.That(Vector3.Distance(vertices[i][j], after[j]), Is.LessThan(.002f), region + " skin flipped at cut: " + skins[i].name);
            }
            // No local-scene physics step: let the live Animator evaluate again to
            // ensure it does not keep writing its old pose into the removed branch.
            yield return null;
            for (int i = 0; i < bones.Length; i++)
                Assert.That(Quaternion.Angle(rotations[i], bones[i].rotation), Is.LessThan(.1f), region + " Animator still controls detached bone");
        }
    }

    static Vector3[] CaptureWorldVertices(SkinnedMeshRenderer skin)
    {
        var mesh = new Mesh();
        skin.BakeMesh(mesh);
        var vertices = mesh.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
        Object.Destroy(mesh);
        return vertices;
    }

    [UnityTest]
    public IEnumerator NapeKillSwitchesIntactBodyToStableRagdoll()
    {
        int deaths = 0;
        body.Died += () => deaths++;
        body.ApplyDamage(TitanHitRegion.Nape, 1, Vector3.zero, new Vector3(0,0,-80));
        body.Die();
        Assert.That(body.IsDead && !body.animator.enabled);
        Assert.That(deaths, Is.EqualTo(1));
        Assert.That(body.ragdollBones.All(b => !b.body.isKinematic && b.body.useGravity));
        Assert.That(body.hitZones.All(z => !z.gameObject.activeSelf));
        for (int a = 0; a < body.ragdollBones.Length; a++)
            for (int b = a + 1; b < body.ragdollBones.Length; b++)
                Assert.That(Physics.GetIgnoreCollision(body.ragdollBones[a].collider, body.ragdollBones[b].collider), "Internal ragdoll contacts must remain ignored");
        Physics.SyncTransforms();
        float maxSpeed = 0;
        string fastestBody = "";
        int fastestStep = 0;
        for (int i = 0; i < 250; i++)
        {
            physics.Simulate(.02f);
            foreach (var entry in body.ragdollBones)
            {
                Vector3 p = entry.body.position;
                Assert.That(!float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y));
                Assert.That(p.y, Is.GreaterThan(-1f), "Body fell through isolated floor");
                Assert.That(p.sqrMagnitude, Is.LessThan(10000));
                if (entry.body.velocity.magnitude > maxSpeed)
                {
                    maxSpeed = entry.body.velocity.magnitude;
                    fastestBody = entry.body.name;
                    fastestStep = i;
                }
            }
            if (i % 10 == 0) yield return null;
        }
        float hipsY = body.hips.GetComponent<Rigidbody>().position.y;
        Assert.That(hipsY, Is.LessThan(3.5f), "Body must fall under gravity");
        float gap = 0;
        foreach (var joint in body.GetComponentsInChildren<CharacterJoint>())
        {
            var own = joint.GetComponent<Rigidbody>();
            Vector3 a = own.position + own.rotation * Vector3.Scale(joint.anchor, own.transform.lossyScale);
            Vector3 b = joint.connectedBody.position + joint.connectedBody.rotation * Vector3.Scale(joint.connectedAnchor, joint.connectedBody.transform.lossyScale);
            gap = Mathf.Max(gap, Vector3.Distance(a,b));
        }
        Debug.Log("TITAN_RAGDOLL_METRICS maxSpeed=" + maxSpeed.ToString("0.00") + " body=" + fastestBody + " step=" + fastestStep + " maxJointGap=" + gap.ToString("0.000") + " hipsY=" + hipsY.ToString("0.00"));
        Assert.That(maxSpeed, Is.LessThan(40), "Explosive ragdoll motion");
        Assert.That(gap, Is.LessThan(.8f));
    }
}
#endif
