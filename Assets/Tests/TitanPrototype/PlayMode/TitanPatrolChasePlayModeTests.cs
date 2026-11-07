#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Run only with the disposable-project runner. Owns all of its NavMesh data.</summary>
public class TitanPatrolChasePlayModeTests
{
    Scene scene, previous;
    TitanNavigationSurface surface;
    ModularTitanBody body;
    NavMeshAgent agent;
    TitanPatrolChaseAI brain;
    Transform target;
    BoxCollider wall;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previous = SceneManager.GetActiveScene();
        scene = SceneManager.CreateScene("Titan_AI_Test", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SceneManager.SetActiveScene(scene);
        var floor = new GameObject("AI test floor");
        floor.transform.position = new Vector3(0,-.25f,0);
        var floorCollider = floor.AddComponent<BoxCollider>(); floorCollider.size = new Vector3(90,.5f,90);
        var obstacle = new GameObject("Occluding wall"); obstacle.transform.position = new Vector3(0,3.5f,7);
        wall = obstacle.AddComponent<BoxCollider>(); wall.size = new Vector3(8,7,2.5f);
        surface = new GameObject("Owned test navigation").AddComponent<TitanNavigationSurface>();
        surface.enabled = false;
        surface.geometry = new[] { floorCollider, wall };
        Assert.That(surface.Build(), Is.True, surface.Error);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
        body = Object.Instantiate(prefab, new Vector3(0,0,-12), Quaternion.identity).GetComponent<ModularTitanBody>();
        agent = body.gameObject.AddComponent<NavMeshAgent>();
        agent.enabled = false; agent.radius = surface.agentRadius; agent.height = surface.agentHeight;
        agent.acceleration = 12; agent.angularSpeed = 150; agent.enabled = true;
        Assert.That(agent.isOnNavMesh, Is.True);
        brain = body.gameObject.AddComponent<TitanPatrolChaseAI>();
        brain.automaticTick = false;
        // Fixture tests the original bounded sight cone, independent of game tuning.
        brain.sightRange = 38; brain.fieldOfView = 120;
        brain.patrolPoints = new[] { new Vector3(-14,0,-12), new Vector3(-14,0,16), new Vector3(14,0,16), new Vector3(14,0,-12) };
        target = new GameObject("Human target ROOT").transform;
        target.position = new Vector3(-10,0,4);
        var capsule = target.gameObject.AddComponent<CapsuleCollider>(); capsule.height = 1.8f; capsule.radius = .3f; capsule.center = Vector3.up * .9f;
        brain.target = target;
        SceneManager.SetActiveScene(previous);
        yield return null;
        Physics.SyncTransforms();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (brain != null) brain.enabled = false;
        if (agent != null) agent.enabled = false;
        if (surface != null) surface.Release();
        SceneManager.SetActiveScene(previous);
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator StompLocksNavigationWithoutStoppingPerceptionAndReleasesOnPause()
    {
        target.position = new Vector3(.5f,0,-6.2f);
        var health = target.gameObject.AddComponent<TitanCombatTarget>();
        var attack = body.gameObject.AddComponent<TitanStompAttack>();
        attack.automaticTick = false; attack.drawTelegraph = false;
        Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(attack.TryBeginStomp(health,true), Is.True);
        Assert.That(brain.IsMovementLocked && agent.isStopped && !agent.hasPath);
        Vector3 root = body.transform.position, known = brain.LastKnownPosition;
        target.position = new Vector3(20,0,-6.2f); Physics.SyncTransforms();
        for(int i=0;i<8;i++)
        {
            brain.TickAI(.05f); attack.TickAttack(.05f);
            yield return null;
            Assert.That(Vector3.Distance(root,body.transform.position), Is.LessThan(.03f));
            Assert.That(agent.isStopped && !agent.hasPath, "Chase must not restart navigation during windup");
        }
        Assert.That(brain.CanSeeTarget, Is.False);
        Assert.That(brain.LastKnownPosition, Is.EqualTo(known), "Attack lock must not cause through-wall/out-of-FOV tracking");
        brain.enabled = false; attack.TickAttack(.1f);
        Assert.That(attack.IsBusy || brain.IsMovementLocked, Is.False);
        Assert.That(attack.ImpactCount, Is.EqualTo(0));
        brain.enabled = true; brain.TickAI(.2f);
        Assert.That(brain.IsMovementLocked, Is.False);
    }

    [UnityTest]
    public IEnumerator DirectorSelectsExclusivelyWithoutConsumingGameplayRandomAndPauseReleasesGrab()
    {
        target.position = new Vector3(.7f,0,-6.3f);
        var health=target.gameObject.AddComponent<TitanCombatTarget>();
        var stomp=body.gameObject.AddComponent<TitanStompAttack>(); stomp.automaticTick=false; stomp.drawTelegraph=false;
        var grab=body.gameObject.AddComponent<TitanGrabAttack>(); grab.automaticTick=false; grab.drawTelegraph=false; grab.enableBite=true;
        var director=body.gameObject.AddComponent<TitanCombatDirector>(); director.automaticTick=false; director.ResetDecisions(1);
        Physics.SyncTransforms(); brain.TickAI(.2f);
        var state=Random.state;
        try
        {
            Random.InitState(4761); float expected=Random.value; Random.InitState(4761);
            director.TickSelection(.2f); Assert.That(Random.value,Is.EqualTo(expected));
        }
        finally { Random.state=state; }
        Assert.That(grab.IsBusy,Is.True,director.LastChoice); Assert.That(stomp.IsBusy,Is.False);
        Assert.That(stomp.automaticAttack,Is.False,"Director must be the sole auto-selection owner");
        Assert.That(brain.IsMovementLocked && agent.isStopped && !agent.hasPath);
        brain.TickAI(.2f); grab.TickAttack(.84f);
        Assert.That(health.IsHeldBy(grab),Is.True,grab.LastResult);
        float warning=grab.windupDuration+grab.reachDuration+grab.holdDuration+grab.mouthTransferDuration+grab.biteWindupDuration-.02f;
        grab.TickAttack(warning-grab.Elapsed); Assert.That(grab.Stage,Is.EqualTo(TitanGrabStage.BiteWindup));
        brain.enabled=false; grab.TickAttack(.01f);
        Assert.That(health.IsHeld || grab.IsBusy || brain.IsMovementLocked,Is.False);
        Assert.That(health.GetComponent<Collider>().enabled,Is.True);
        grab.TickAttack(20); Assert.That(health.Health,Is.EqualTo(100),"AI pause cannot leave a delayed bite");
        yield return null;
    }

    [UnityTest]
    public IEnumerator SightRequiresRangeFieldOfViewAndUnobstructedRay()
    {
        agent.updateRotation = false;
        brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Chase));
        Assert.That(brain.CanSeeTarget, Is.True, "Own body and target colliders must not occlude sight");
        Vector3 seen = brain.LastKnownPosition;
        target.position = new Vector3(0,0,18); Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget, Is.False, "Tall wall must block sight");
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Investigate));
        Assert.That(brain.LastKnownPosition, Is.EqualTo(seen), "Cannot track hidden target through walls");
        target.position = new Vector3(0,0,-25); Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget, Is.False, "Target behind Titan is outside FOV");
        target.position = new Vector3(-10,0,60); Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget, Is.False, "Target beyond sight range");
        target.position = seen;
        var trigger = new GameObject("Non-blocking trigger").AddComponent<BoxCollider>();
        SceneManager.MoveGameObjectToScene(trigger.gameObject, scene);
        trigger.transform.position = new Vector3(-5,5,-4); trigger.size = new Vector3(4,10,2); trigger.isTrigger = true;
        Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget, Is.True, "Damage/interaction triggers must not block sight");
        yield return null;
    }

    [UnityTest]
    public IEnumerator LostTargetIsInvestigatedSearchedThenPatrolResumes()
    {
        brain.TickAI(.2f);
        Vector3 seen = brain.LastKnownPosition;
        target.position = new Vector3(200,0,200);
        Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Investigate));
        Assert.That(brain.LastKnownPosition, Is.EqualTo(seen));
        Assert.That(agent.Warp(seen), Is.True);
        brain.TickAI(.4f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Search));
        Assert.That(agent.isStopped, Is.True);
        brain.TickAI(brain.searchDuration + .1f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Patrol));
        Assert.That(brain.HasLastKnownPosition, Is.False);
        target.gameObject.SetActive(false);
        brain.TickAI(.4f);
        Assert.That(brain.CanSeeTarget, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator BlindnessLegLossAndDeathStopNavigationImmediately()
    {
        brain.TickAI(.2f);
        Vector3 seen = brain.LastKnownPosition;
        Assert.That(agent.hasPath, Is.True);
        body.StunEyes();
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Blind));
        Assert.That(agent.isStopped && !agent.hasPath);
        Vector3 stoppedAt = body.transform.position;
        yield return new WaitForSeconds(.25f);
        Assert.That(Vector3.Distance(stoppedAt, body.transform.position), Is.LessThan(.02f), "Blind Titan must not drift along its old route");
        target.position = new Vector3(0,0,18); Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(!brain.CanSeeTarget && brain.LastKnownPosition == seen);
        body.TickStatus(body.eyeStunDuration + 1); brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Investigate));
        body.Sever(TitanHitRegion.LeftArm, Vector3.zero); brain.TickAI(.2f);
        Assert.That(brain.State, Is.Not.EqualTo(TitanAwarenessState.Immobilized), "Arm loss must not disable walking");
        body.Sever(TitanHitRegion.LeftLeg, Vector3.zero);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Immobilized));
        Assert.That(agent.isStopped && !agent.hasPath);
        body.Die(); brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Dead));
        Assert.That(agent.enabled, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator UnreachablePatrolAndMissingNavigationFailSafely()
    {
        brain.target = null;
        brain.patrolPoints = new[] { new Vector3(200,0,200) };
        brain.TickAI(.2f);
        Assert.That(brain.NavigationStatus, Is.EqualTo("Destination unreachable"));
        Assert.That(agent.isStopped && !agent.hasPath);
        agent.enabled = false;
        brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.NavigationUnavailable));
        Assert.That(body.animator.GetBool("IsWalking"), Is.False);
        agent.enabled = true;
        brain.patrolPoints = new[] { new Vector3(-14,0,-12) };
        brain.TickAI(.6f); brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Patrol));
        Assert.That(agent.hasPath, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PatrolActuallyMovesRootAndAdvancesWaypoints()
    {
        brain.target = null;
        brain.patrolPoints = new[] { new Vector3(-3,0,-12), new Vector3(-12,0,-12) };
        brain.patrolPause = .1f;
        Vector3 start = body.transform.position;
        brain.automaticTick = true;
        float time = 0;
        while (time < 6 && brain.PatrolIndex == 0) { time += Time.deltaTime; yield return null; }
        Assert.That(Vector3.Distance(start, body.transform.position), Is.GreaterThan(2));
        Assert.That(brain.PatrolIndex, Is.EqualTo(1), "Patrol must pause then advance");
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Patrol));
        Debug.Log("TITAN_PATROL_METRICS moved=" + Vector3.Distance(start, body.transform.position).ToString("F2") + " index=" + brain.PatrolIndex);
    }

    [UnityTest]
    public IEnumerator ExpiredMemoryAndReplacementTargetMustEarnDetection()
    {
        agent.updateRotation = false;
        brain.targetMemoryDuration = .5f;
        brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget, Is.True);
        body.StunEyes();
        brain.TickAI(.6f);
        target.position = new Vector3(0,0,18); Physics.SyncTransforms();
        body.TickStatus(body.eyeStunDuration + 1); brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Patrol));
        Assert.That(brain.HasLastKnownPosition, Is.False, "Blindness must not preserve stale memory forever");
        target.position = new Vector3(-10,0,4); Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Chase));
        brain.enabled = false;
        Assert.That(agent.isStopped && !agent.hasPath, "Pausing the AI must stop navigation");
        brain.enabled = true;
        var replacement = new GameObject("Undetected replacement target").transform;
        SceneManager.MoveGameObjectToScene(replacement.gameObject, scene);
        replacement.position = new Vector3(0,0,18);
        brain.target = replacement; Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget, Is.False);
        Assert.That(brain.HasLastKnownPosition, Is.False, "New target must not inherit previous target's detection");
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Patrol));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ChaseAndInvestigationUseCompletePathsAroundWall()
    {
        target.position = new Vector3(0,0,18);
        // Reveal target once without altering the baked navigation geometry.
        wall.enabled = false; Physics.SyncTransforms(); brain.TickAI(.2f);
        Assert.That(brain.CanSeeTarget, Is.True);
        Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
        Assert.That(agent.path.corners.Length, Is.GreaterThan(2), "Path must bend around baked wall");
        wall.enabled = true; Physics.SyncTransforms(); brain.TickAI(.4f);
        Assert.That(brain.State, Is.EqualTo(TitanAwarenessState.Investigate));
        brain.targetMemoryDuration = 60;
        brain.automaticTick = true;
        float time = 0;
        while (time < 14 && body.transform.position.z < 10)
        {
            time += Time.deltaTime;
            Vector3 p = body.transform.position;
            Assert.That(!(Mathf.Abs(p.x) < 4 && p.z > 5.75f && p.z < 8.25f), "Agent cut through wall");
            yield return null;
        }
        Assert.That(body.transform.position.z, Is.GreaterThanOrEqualTo(10), "Titan must navigate around the obstacle, not only animate in place");
        Assert.That(brain.State == TitanAwarenessState.Chase || brain.State == TitanAwarenessState.Investigate);
        Debug.Log("TITAN_CHASE_METRICS position=" + body.transform.position.ToString("F2") + " elapsed=" + time.ToString("F2") + " state=" + brain.State);
    }
}
#endif
