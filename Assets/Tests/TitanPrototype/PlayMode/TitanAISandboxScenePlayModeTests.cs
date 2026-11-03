#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Integration check for the saved AI lab; run in the disposable project only.</summary>
public class TitanAISandboxScenePlayModeTests
{
    Scene scene;
    [UnityTest]
    public IEnumerator AuthoredLabStartsAndResetsWithoutLeakingActorsOrNavigation()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/TitanAISandbox.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        scene = SceneManager.GetSceneByPath("Assets/Scenes/TitanAISandbox.unity");
        yield return null;
        var demo = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TitanAISandboxDemo>()).Single();
        Assert.That(demo.surface.IsReady, Is.True, demo.surface.Error);
        Assert.That(demo.Brain, Is.Not.Null);
        Assert.That(demo.titan.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
        Assert.That(demo.Brain.target, Is.EqualTo(demo.target.transform));
        Assert.That(demo.Brain.patrolPoints.Length, Is.EqualTo(4));
        Assert.That(demo.target.GetComponent<CharacterController>().enabled, Is.True);
        Assert.That(demo.Brain.CanSeeTarget, Is.False, "Starting human is concealed behind wall");
        // Unlike the local-physics unit fixtures, this scene advances normal physics.
        // Catch stale Rigidbody interpolation pulling the animated skeleton away from
        // its moving navigation root (a root-only movement test cannot detect this).
        var bones = demo.titan.ragdollBones.Select(b => b.body.transform).ToArray();
        var localPositions = bones.Select(b => b.localPosition).ToArray();
        Vector3 hipsOffset = demo.titan.transform.InverseTransformPoint(demo.titan.hips.position);
        Vector3 start = demo.titan.transform.position;
        demo.target.Warp(new Vector3(-12,0,0));
        float elapsed = 0;
        while (elapsed < 3 && Vector3.Distance(start, demo.titan.transform.position) < 2.5f)
        {
            elapsed += Time.deltaTime;
            yield return null;
            Vector3 offset = demo.titan.transform.InverseTransformPoint(demo.titan.hips.position) - hipsOffset;
            Assert.That(Vector3.ProjectOnPlane(offset, Vector3.up).magnitude, Is.LessThan(.25f), "Rendered skeleton was left behind the moving agent");
            Assert.That(Mathf.Abs(offset.y), Is.LessThan(1.2f), "Only authored contact/bob may move the hips vertically");
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != demo.titan.hips)
                    Assert.That(Vector3.Distance(localPositions[i], bones[i].localPosition), Is.LessThan(.0001f), "Physics overwrote animated bone position: " + bones[i].name);
        }
        Assert.That(Vector3.Distance(start, demo.titan.transform.position), Is.GreaterThan(2), "Scene integration must exercise real locomotion");
        var oldBody = demo.titan;
        demo.titan.Sever(TitanHitRegion.LeftLeg, Vector3.zero);
        Assert.That(demo.Brain.State, Is.EqualTo(TitanAwarenessState.Immobilized));
        demo.ResetTitan();
        yield return null;
        Assert.That(oldBody == null, Is.True);
        Assert.That(demo.titan.gameObject.scene, Is.EqualTo(scene), "Reset must keep actors in the lab even when loaded additively");
        Assert.That(demo.titan.CanWalk && !demo.titan.IsDead);
        Assert.That(demo.titan.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
        Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TitanPatrolChaseAI>()).Count(), Is.EqualTo(1));
        Assert.That(scene.GetRootGameObjects().Any(r => r.name.EndsWith("_Detached")), Is.False, "Reset must remove detached branch objects");
        Assert.That(demo.surface.IsReady, Is.True);
    }

    [UnityTest]
    public IEnumerator AuthoredLabResetReleasesHeldProxyBeforeDestroyingItsOwner()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/TitanAISandbox.unity",new LoadSceneParameters(LoadSceneMode.Additive));
        scene=SceneManager.GetSceneByPath("Assets/Scenes/TitanAISandbox.unity"); yield return null;
        var demo=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitanAISandboxDemo>()).Single();
        demo.Director.SetAutomaticAttacks(false); demo.Grab.automaticTick=false; demo.Grab.drawTelegraph=false;
        demo.Brain.automaticTick=false;
        demo.target.Warp(demo.titan.transform.position+new Vector3(.7f,0,5.7f));
        Physics.SyncTransforms(); demo.Brain.TickAI(.2f);
        Assert.That(demo.Grab.TryBeginGrab(demo.HumanHealth,true),Is.True);
        demo.Grab.TickAttack(.84f);
        Assert.That(demo.HumanHealth.IsHeld,Is.True,demo.Grab.LastResult+" contact="+demo.Grab.LastContactPoint+
            " human="+demo.target.GetComponent<CharacterController>().bounds+" actor="+demo.titan.transform.position+
            " overlaps="+string.Join(",",Physics.OverlapSphere(demo.Grab.LastContactPoint,demo.Grab.contactRadius,~0,QueryTriggerInteraction.Ignore).Select(c=>c.name)));
        float beforeBite=demo.Grab.windupDuration+demo.Grab.reachDuration+demo.Grab.holdDuration+
            demo.Grab.mouthTransferDuration+demo.Grab.biteWindupDuration-.02f;
        demo.Grab.TickAttack(beforeBite-demo.Grab.Elapsed);
        Assert.That(demo.Grab.Stage,Is.EqualTo(TitanGrabStage.BiteWindup));
        demo.titan.animator.Update(0); demo.Grab.ApplyPose(); demo.HumanHealth.TickHold(0);
        var human=demo.target; var old=demo.titan;
        demo.ResetTitan();
        Assert.That(demo.HumanHealth.IsHeld,Is.False);
        Assert.That(human.GetComponent<CharacterController>().enabled,Is.True);
        yield return null;
        Assert.That(old==null,Is.True); Assert.That(human!=null,Is.True,"Victim must not be destroyed with an owner or limb");
        Assert.That(human.gameObject.scene,Is.EqualTo(scene)); Assert.That(human.transform.parent,Is.Null);
        Assert.That(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitanGrabAttack>()).Count(),Is.EqualTo(1));
        Assert.That(demo.HumanHealth.Health,Is.EqualTo(100));
    }

    [UnityTest]
    public IEnumerator AuthoredLabOptInBiteDamagesItsRealControllerProxyThenResets()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/TitanAISandbox.unity",new LoadSceneParameters(LoadSceneMode.Additive));
        scene=SceneManager.GetSceneByPath("Assets/Scenes/TitanAISandbox.unity"); yield return null;
        var demo=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitanAISandboxDemo>()).Single();
        demo.Director.SetAutomaticAttacks(false); demo.Grab.automaticTick=false; demo.Grab.drawTelegraph=false;
        demo.Brain.automaticTick=false;
        Assert.That(demo.Grab.enableBite,Is.True,"Only the runtime combat lab opts into eating");
        demo.target.Warp(demo.titan.transform.position+new Vector3(.7f,0,5.7f));
        Physics.SyncTransforms(); demo.Brain.TickAI(.2f);
        Assert.That(demo.Grab.TryBeginGrab(demo.HumanHealth,true),Is.True);
        demo.Grab.TickAttack(.84f); Assert.That(demo.HumanHealth.IsHeld,Is.True);
        float time=demo.Grab.windupDuration+demo.Grab.reachDuration+demo.Grab.holdDuration+
            demo.Grab.mouthTransferDuration+demo.Grab.biteWindupDuration+demo.Grab.biteDuration;
        demo.Grab.TickAttack(time+.01f-demo.Grab.Elapsed);
        Assert.That(demo.HumanHealth.IsDead,Is.True,demo.Grab.LastResult);
        Assert.That(demo.HumanHealth.IsHeld,Is.False); Assert.That(demo.target.GetComponent<CharacterController>().enabled,Is.True);
        Assert.That(demo.Grab.Stage,Is.EqualTo(TitanGrabStage.Recovery));
        demo.ResetTitan(); yield return null;
        Assert.That(demo.HumanHealth.Health,Is.EqualTo(100)); Assert.That(demo.Grab.enableBite,Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (!scene.IsValid()) scene = SceneManager.GetSceneByPath("Assets/Scenes/TitanAISandbox.unity");
        if (scene.IsValid() && scene.isLoaded)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var brain in root.GetComponentsInChildren<TitanPatrolChaseAI>()) brain.enabled = false;
                foreach (var agent in root.GetComponentsInChildren<NavMeshAgent>()) agent.enabled = false;
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
