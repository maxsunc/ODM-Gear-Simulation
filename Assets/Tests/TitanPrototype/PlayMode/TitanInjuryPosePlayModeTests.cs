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

    [TestCase(1f,0)][TestCase(3f,0)][TestCase(5f,0)][TestCase(10f,0)]
    [TestCase(1f,1)][TestCase(3f,1)][TestCase(5f,1)][TestCase(10f,1)]
    [TestCase(1f,2)][TestCase(3f,2)][TestCase(5f,2)][TestCase(10f,2)]
    [TestCase(1f,3)][TestCase(3f,3)][TestCase(5f,3)][TestCase(10f,3)]
    public void RaisedProneExposesUnchangedNapeAndPreservesBonesRootAndGround(float size,int missingArms)
    {
        scene.GetRootGameObjects().Single(g=>g.name=="Injury test floor").GetComponent<BoxCollider>().size=new Vector3(1000,.5f,1000);
        body.transform.localScale=Vector3.one*size;body.transform.position=new Vector3(4,0,-7);body.transform.rotation=Quaternion.Euler(0,missingArms%2==0?37:217,0);body.GetComponent<TitanMotionStyle>().automaticTick=false;
        var zone=body.hitZones.Single(z=>z.region==TitanHitRegion.Nape).GetComponent<BoxCollider>();Vector3 zoneSize=zone.size,zoneCenter=zone.center,rootPosition=body.transform.position;Quaternion rootRotation=body.transform.rotation;float napeHp=body.GetHealth(TitanHitRegion.Nape);
        if((missingArms&1)!=0)body.Sever(TitanHitRegion.LeftArm,Vector3.zero);if((missingArms&2)!=0)body.Sever(TitanHitRegion.RightArm,Vector3.zero);
        var first=missingArms%2==0?TitanHitRegion.LeftLeg:TitanHitRegion.RightLeg;body.Sever(first,Vector3.zero);injury.TickPose(.31f);body.Sever(first==TitanHitRegion.LeftLeg?TitanHitRegion.RightLeg:TitanHitRegion.LeftLeg,Vector3.zero);
        var detached=body.ragdollBones.Where(e=>!e.body.transform.IsChildOf(body.transform)).Select(e=>e.body.transform).ToArray();var detachedPositions=detached.Select(t=>t.position).ToArray();var detachedRotations=detached.Select(t=>t.rotation).ToArray();
        var arms=body.ragdollBones.Where(e=>e.body.transform.IsChildOf(body.transform)&&(e.body.name.Contains("Arm")||e.body.name.Contains("Hand"))).Select(e=>e.body.transform).ToArray();var previousRotations=arms.Select(t=>t.rotation).ToArray();float maxStep=0;
        for(int step=0;step<100;step++)
        {
            injury.TickPose(1f/120);AssertAboveGround();for(int i=0;i<arms.Length;i++){maxStep=Mathf.Max(maxStep,Quaternion.Angle(previousRotations[i],arms[i].rotation));previousRotations[i]=arms[i].rotation;}
        }
        Assert.That(maxStep,Is.LessThan(8),"Arm continuity remains strict at120Hz");
        for(int i=0;i<detached.Length;i++){Assert.That(Vector3.Distance(detached[i].position,detachedPositions[i]),Is.LessThan(.001f));Assert.That(Quaternion.Angle(detached[i].rotation,detachedRotations[i]),Is.LessThan(.1f));}
        Assert.That(body.IsHelpless&&!body.IsDead&&!body.CanWalk&&!body.CanGrabLeft&&!body.CanGrabRight);Assert.That(Vector3.Distance(rootPosition,body.transform.position),Is.LessThan(.001f));Assert.That(Quaternion.Angle(rootRotation,body.transform.rotation),Is.LessThan(.01f));
        Assert.That(zone.size,Is.EqualTo(zoneSize));Assert.That(zone.center,Is.EqualTo(zoneCenter));Assert.That(body.GetHealth(TitanHitRegion.Nape),Is.EqualTo(napeHp));
        Assert.That(zone.bounds.center.y/size,Is.GreaterThan(2.6f),"Actual nape must rise above the old1.83m-per-size flat pose");Assert.That(Vector3.Dot(body.head.forward,Vector3.down),Is.GreaterThan(.9f));AssertSkinAboveGround();
        Vector3 origin=zone.bounds.center+body.transform.rotation*new Vector3(0,2,-3)*size;var hits=new RaycastHit[128];int count=physics.Raycast(origin,(zone.bounds.center-origin).normalized,hits,Vector3.Distance(origin,zone.bounds.center)+.1f,~0,QueryTriggerInteraction.Collide);
        var nearest=hits.Take(count).Where(h=>h.collider.transform.IsChildOf(body.transform)).OrderBy(h=>h.distance).First();Assert.That(nearest.collider,Is.EqualTo(zone),"Real nape has an unobstructed rear/above approach, no shoulder/head/arm cover bypass");
        Debug.Log("RAISED_PRONE size="+size+" missingArms="+missingArms+" napeHeightPerSize="+zone.bounds.center.y/size+" maxArmStep="+maxStep);
    }

    [TestCase(0f)][TestCase(.68f)]
    public void RapidAndSettledSecondCutsKeepProneArmsContinuous(float firstAge)
    {
        body.GetComponent<TitanMotionStyle>().automaticTick=false;body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);injury.TickPose(firstAge);body.Sever(TitanHitRegion.RightLeg,Vector3.zero);
        var arms=body.ragdollBones.Where(e=>e.body.transform.IsChildOf(body.transform)&&(e.body.name.Contains("Arm")||e.body.name.Contains("Hand"))).Select(e=>e.body.transform).ToArray();var rotations=arms.Select(t=>t.rotation).ToArray();float maxStep=0;
        for(int step=0;step<100;step++){injury.TickPose(1f/120);for(int i=0;i<arms.Length;i++){maxStep=Mathf.Max(maxStep,Quaternion.Angle(rotations[i],arms[i].rotation));rotations[i]=arms[i].rotation;}}
        Assert.That(maxStep,Is.LessThan(8));Debug.Log("PRONE_SECOND_CUT age="+firstAge+" maxArmStep="+maxStep);
    }
    [UnityTest] public IEnumerator RaisedProneDeathAtLargeSizesKeepsNativeSpeedBound()
    {
        foreach(float size in new[]{3f,5f,10f})
        {
            foreach(var go in scene.GetRootGameObjects().Where(g=>g.name!="Injury test floor"))Object.DestroyImmediate(go);
            scene.GetRootGameObjects().Single(g=>g.name=="Injury test floor").GetComponent<BoxCollider>().size=new Vector3(1000,.5f,1000);
            SceneManager.SetActiveScene(scene);body=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab")).GetComponent<ModularTitanBody>();SceneManager.SetActiveScene(previous);
            body.transform.localScale=Vector3.one*size;injury=body.GetComponent<TitanInjuryPose>();injury.automaticTick=false;body.GetComponent<TitanMotionStyle>().automaticTick=false;yield return null;body.animator.Update(0);
            body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);injury.TickPose(.3f);body.Sever(TitanHitRegion.RightLeg,Vector3.zero);injury.TickPose(injury.proneDuration+1);yield return CheckDeath();
        }
    }

    [UnityTest] public IEnumerator ProneNapeRendersInOwnedNativeGraphics()
    {
        string folder=System.Environment.GetEnvironmentVariable("ODM_PRONE_NAPE_PREVIEW");if(string.IsNullOrEmpty(folder))yield break;
        Assert.That(System.IO.File.Exists(".titan-validation-owned"));Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(folder);
        var oldPipeline=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;var oldQuality=QualitySettings.renderPipeline;
        var type=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")).First(t=>t!=null);
        var renderer=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");var pipeline=(UnityEngine.Rendering.RenderPipelineAsset)type.GetMethod("Create").Invoke(null,new object[]{renderer});
        var camera=new GameObject("Owned prone comparison camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.19f,.23f);camera.orthographic=true;camera.orthographicSize=13;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Owned comparison ground";SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.position=new Vector3(0,-.25f,0);ground.transform.localScale=new Vector3(80,.5f,80);ground.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("TitanStompChunks");
        var sun=new GameObject("Owned prone sun").AddComponent<Light>();SceneManager.MoveGameObjectToScene(sun.gameObject,scene);sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;body.transform.localScale=Vector3.one*3;
            body.Sever(TitanHitRegion.LeftLeg,Vector3.zero);injury.TickPose(injury.kneelDuration+1);body.Sever(TitanHitRegion.RightLeg,Vector3.zero);injury.TickPose(injury.proneDuration+1);
            foreach(var limb in body.limbs.Where(l=>l.region==TitanHitRegion.LeftLeg||l.region==TitanHitRegion.RightLeg))limb.rootBone.parent.gameObject.SetActive(false);
            var nape=body.hitZones.Single(z=>z.region==TitanHitRegion.Nape).GetComponent<BoxCollider>();Physics.SyncTransforms();Debug.Log("PRONE_NAPE_RENDER center="+nape.bounds.center+" hips="+body.hips.position+" head="+body.head.position);
            camera.transform.position=new Vector3(-25,18,-17);camera.transform.LookAt(new Vector3(0,4.5f,5));yield return null;yield return null;yield return CaptureProne(folder+"/Rear.png");
            camera.transform.position=new Vector3(-28,11,8);camera.transform.LookAt(new Vector3(0,4.5f,5));yield return null;yield return null;yield return CaptureProne(folder+"/Side.png");
        }
        finally{UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=oldPipeline;QualitySettings.renderPipeline=oldQuality;Object.Destroy(camera.gameObject);Object.Destroy(sun.gameObject);Object.Destroy(ground);Object.Destroy(pipeline);}
    }
    static IEnumerator CaptureProne(string path)
    {
        System.IO.File.Delete(path);ScreenCapture.CaptureScreenshot(path);for(int i=0;i<120&&!System.IO.File.Exists(path);i++)yield return null;Assert.That(System.IO.File.Exists(path),Is.True);
    }

    void AssertProne()
    {
        Assert.That(injury.State, Is.EqualTo(TitanInjuryState.Prone));
        Assert.That(body.IsHelpless && !body.IsDead && !body.CanWalk && !body.CanGrabLeft && !body.CanGrabRight);
        Assert.That(body.hips.position.y, Is.InRange(.6f,1.6f));
        Assert.That(body.head.position.y, Is.LessThan(3.2f));
        Assert.That(Vector3.Dot((body.head.position-body.hips.position).normalized, Vector3.up), Is.InRange(.2f,.6f), "Prone upper back is lifted, not flat or standing");
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
