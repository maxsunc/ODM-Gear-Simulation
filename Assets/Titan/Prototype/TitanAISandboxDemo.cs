using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Opt-in AI lab only. The reusable body prefab and the original body lab stay passive.</summary>
public class TitanAISandboxDemo : MonoBehaviour
{
    public GameObject titanPrefab;
    public ModularTitanBody titan;
    public TitanNavigationSurface surface;
    public TitanSandboxTarget target;
    public Camera previewCamera;
    public Vector3 spawnPosition = new Vector3(0, 0, -12);
    public Vector3[] patrolPoints = new[]
    {
        new Vector3(-14, 0, -12), new Vector3(-14, 0, 16),
        new Vector3(14, 0, 16), new Vector3(14, 0, -12)
    };
    public TitanPatrolChaseAI Brain { get; private set; }
    public TitanStompAttack Combat { get; private set; }
    public TitanGrabAttack Grab { get; private set; }
    public TitanCombatDirector Director { get; private set; }
    public TitanCombatTarget HumanHealth { get; private set; }
    float orbit = 30f;
    Vector2 panelScroll;
    string setupError;
    GUIStyle title, small;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (surface == null || (!surface.IsReady && !surface.Build()))
        {
            setupError = surface == null ? "Navigation surface missing" : surface.Error;
            return;
        }
        if (titan == null) ResetTitan(); else ConfigureTitan();
    }

    void ConfigureTitan()
    {
        var agent = titan.GetComponent<NavMeshAgent>();
        if (agent == null) agent = titan.gameObject.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.agentTypeID = surface.agentTypeID;
        agent.radius = surface.agentRadius;
        agent.height = surface.agentHeight;
        agent.baseOffset = 0;
        agent.acceleration = 26;
        agent.angularSpeed = 330;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.enabled = true;
        Brain = titan.gameObject.AddComponent<TitanPatrolChaseAI>();
        Brain.target = target != null ? target.transform : null;
        Brain.patrolPoints = patrolPoints;
        if (target != null)
        {
            HumanHealth = target.GetComponent<TitanCombatTarget>();
            if (HumanHealth == null) HumanHealth = target.gameObject.AddComponent<TitanCombatTarget>();
            HumanHealth.ResetHealth();
        }
        Combat = titan.gameObject.AddComponent<TitanStompAttack>();
        Grab = titan.gameObject.AddComponent<TitanGrabAttack>();
        Grab.enableBite = true;
        Director = titan.gameObject.AddComponent<TitanCombatDirector>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) ResetTitan();
        if (titan != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha2)) Slice(TitanHitRegion.LeftArm);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Slice(TitanHitRegion.RightArm);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Slice(TitanHitRegion.LeftLeg);
            if (Input.GetKeyDown(KeyCode.Alpha5)) Slice(TitanHitRegion.RightLeg);
            if (Input.GetKeyDown(KeyCode.Alpha6)) titan.StunEyes();
            if (Input.GetKeyDown(KeyCode.K)) titan.Die();
            if (Input.GetKeyDown(KeyCode.Alpha8) && Combat != null) Combat.TryBeginStomp(HumanHealth, true);
            if (Input.GetKeyDown(KeyCode.Alpha9) && Grab != null) Grab.TryBeginGrab(HumanHealth, true);
            if (Input.GetKeyDown(KeyCode.Alpha0) && Grab != null) Grab.TryBeginGrab(HumanHealth, false);
            if (Input.GetKeyDown(KeyCode.E) && HumanHealth != null) HumanHealth.EscapeHold();
            if (Input.GetKeyDown(KeyCode.T) && Director != null) Director.SetAutomaticAttacks(!Director.automaticAttack);
        }
        if (Input.GetKeyDown(KeyCode.P) && Brain != null) Brain.enabled = !Brain.enabled;
        if (Input.GetKey(KeyCode.LeftArrow)) orbit -= 30 * Time.deltaTime;
        if (Input.GetKey(KeyCode.RightArrow)) orbit += 30 * Time.deltaTime;
        if (previewCamera != null)
        {
            Vector3 look = new Vector3(0, 3, 2);
            previewCamera.transform.position = look + Quaternion.Euler(0, orbit, 0) * new Vector3(0, 40, 48);
            previewCamera.transform.LookAt(look);
        }
    }

    void Slice(TitanHitRegion region) { titan.ApplyDamage(region, 100, titan.transform.position, Vector3.zero); }
    public void ResetTitan()
    {
        if (surface == null || !surface.IsReady || titanPrefab == null) return;
        if (titan != null)
        {
            var detached = new HashSet<GameObject>();
            foreach (var limb in titan.limbs)
                if (limb.severed && limb.rootBone != null) detached.Add(limb.rootBone.root.gameObject);
            foreach (var root in detached) Destroy(root);
            if (Director != null) Director.CancelAttacks("Reset");
            if (Grab != null) Grab.Cancel("Reset");
            if (Brain != null) Brain.enabled = false;
            var oldAgent = titan.GetComponent<NavMeshAgent>();
            if (oldAgent != null) oldAgent.enabled = false;
            Destroy(titan.gameObject);
        }
        var instance = Instantiate(titanPrefab, spawnPosition, Quaternion.identity);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        titan = instance.GetComponent<ModularTitanBody>();
        titan.name = "Patrol Chase Titan";
        if (target != null) target.Warp(new Vector3(0, 0, 18));
        ConfigureTitan();
    }

    void OnGUI()
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        }
        GUILayout.BeginArea(new Rect(15, 15, 360, Mathf.Min(710, Mathf.Max(120, Screen.height - 30))), GUI.skin.box);
        panelScroll = GUILayout.BeginScrollView(panelScroll);
        GUILayout.Label("TITAN / COMBAT LAB", title);
        if (setupError != null) GUILayout.Label(setupError, small);
        if (Brain != null)
        {
            GUILayout.Label(Brain.enabled ? "State: " + Brain.State : "AI PAUSED");
            GUILayout.Label("Sees human: " + Brain.CanSeeTarget + " | Route: " + Brain.NavigationStatus, small);
            GUILayout.Label("Last seen: " + (Brain.HasLastKnownPosition ? Brain.LastKnownPosition.ToString("F1") : "none"), small);
            GUILayout.Label("Can walk: " + titan.CanWalk + " | Blind: " + titan.IsBlind, small);
            var motion = titan.GetComponent<TitanMotionStyle>();
            if (motion != null) GUILayout.Label("Speed " + motion.ActualSpeed.ToString("F1") + " m/s | run blend " + motion.Gait.ToString("F2") + " | pace x" + motion.PaceScale.ToString("F2"), small);
            var injury = titan.GetComponent<TitanInjuryPose>();
            if (injury != null) GUILayout.Label("Injury: " + injury.State + (injury.IsTransitioning ? " / collapsing" : ""), small);
        }
        if (Combat != null) GUILayout.Label("Stomp: " + Combat.Stage + " | cooldown " + Combat.CooldownRemaining.ToString("F1") + "s\n" + Combat.LastResult, small);
        if (Director != null) GUILayout.Label("Auto combat: " + (Director.automaticAttack ? "ON" : "OFF") + " | " + Director.LastChoice, small);
        if (Grab != null) GUILayout.Label("Grab: " + Grab.Stage + " | " + Grab.LastResult, small);
        if (HumanHealth != null && HumanHealth.IsHeld) GUILayout.Label("HELD — E to escape BEFORE the mouth closes!", small);
        if (HumanHealth != null) GUILayout.Label("Human HP: " + HumanHealth.Health.ToString("F0") + " / " + HumanHealth.maxHealth + (HumanHealth.IsDead ? " — R to reset" : ""), small);
        GUILayout.Space(8);
        GUILayout.Label("WASD: move teal human | Shift: sprint\nArrows: orbit | T: toggle auto combat\n8: stomp | 9/0: left/right grab | E: escape\nYellow/cyan aim locks BEFORE contact. Dodge it!", small);
        if (GUILayout.Button("Human: behind wall") && target != null) target.Warp(new Vector3(0, 0, 18));
        if (GUILayout.Button("Human: open left lane") && target != null) target.Warp(new Vector3(-12, 0, 0));
        GUI.enabled = titan != null && !titan.IsDead;
        if (GUILayout.Button("6  Blind eyes (5 seconds)")) titan.StunEyes();
        if (GUILayout.Button("2 / 3  Slice left / right arm")) Slice(titan.CanGrabLeft ? TitanHitRegion.LeftArm : TitanHitRegion.RightArm);
        if (GUILayout.Button("4  Slice left leg / kneel")) Slice(TitanHitRegion.LeftLeg);
        if (GUILayout.Button("K  Nape kill / ragdoll")) titan.Die();
        GUI.enabled = true;
        if (GUILayout.Button("P  Pause / resume AI") && Brain != null) Brain.enabled = !Brain.enabled;
        if (GUILayout.Button("R  Reset Titan and human")) ResetTitan();
        GUILayout.Space(8);
        GUILayout.Label("Cyan markers: patrol route. Yellow wall blocks sight AND navigation. This human is a lab proxy, not your ODM player. Stomp deals 40 HP once. Blindness, leg loss, death or AI pause cancels it. One leg lost: kneel; both lost: helpless/prone. Grab lifts to mouth, winds up, then bites for 100 HP only on actual mouth contact; E escapes. Orange mouth ring warns; red is bite. Grabbing-arm loss, blindness, leg loss or death releases immediately. Real-player/aerial-grab integration pending.", small);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
