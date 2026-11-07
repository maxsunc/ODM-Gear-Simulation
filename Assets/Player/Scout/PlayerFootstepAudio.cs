using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-only observer of the actual Scout ground-gait phase. Four bounded
/// personal SFX voices, no motor/Animator/physics writes or global Random use.</summary>
[DefaultExecutionOrder(145)]
[DisallowMultipleComponent]
public sealed class PlayerFootstepAudio : MonoBehaviour
{
    public PlayerFootstepAudioPalette palette;
    [Header("Runtime diagnostics")]
    [SerializeField] string status = "Waiting for player";
    [SerializeField] int footstepsPlayed;
    public string Status { get { return status; } }
    public int FootstepCount { get { return footstepsPlayed; } }
    public AudioClip LastFootstep { get; private set; }
    public int LastFoot { get; private set; }
    public float PlanarSpeed { get; private set; }
    public AudioSource LastSource { get; private set; }
    const int VoiceCount = 4;
    static readonly int Run = Animator.StringToHash("Run");
    readonly AudioSource[] voices = new AudioSource[VoiceCount];
    readonly RaycastHit[] supportHits = new RaycastHit[16];
    readonly Dictionary<AudioClip, int> cueStarts = new Dictionary<AudioClip, int>();
    readonly System.Random random = new System.Random();
    PlayerHealth health;
    Movement walking;
    ODM_Movement gear;
    CharacterController controller;
    ScoutPlayerAnimation scout;
    TitanODMMotor motor;
    TitanCombatTarget target;
    PlayerRespawn respawn;
    GameObject soundRoot;
    Vector3 previousPosition;
    float sampledAt, previousPhase;
    int validSamples, nextVoice, observedController, observedState;
    bool positioned, phaseTracked, allowEntry, focusLost, applicationPaused;

    public static PlayerFootstepAudio Install(PlayerHealth player)
    {
        if(player == null || player.GetComponent<Movement>() == null || player.GetComponent<ODM_Movement>() == null ||
            player.GetComponent<CharacterController>() == null) return null;
        var driver = player.GetComponent<ScoutPlayerAnimation>();
        if(driver == null || driver.animator == null || driver.model == null) return null;
        var existing = player.GetComponent<PlayerFootstepAudio>();
        return existing != null ? existing : player.gameObject.AddComponent<PlayerFootstepAudio>();
    }
    void Awake()
    {
        health = GetComponent<PlayerHealth>(); walking = GetComponent<Movement>(); gear = GetComponent<ODM_Movement>();
        controller = GetComponent<CharacterController>(); scout = GetComponent<ScoutPlayerAnimation>();
        if(palette == null) palette = Resources.Load<PlayerFootstepAudioPalette>("Audio/PlayerFootstepAudio");
        soundRoot = new GameObject("[Player SFX] Footsteps"); soundRoot.hideFlags = HideFlags.DontSave;
        soundRoot.transform.SetParent(transform, false);
        for(int i = 0; i < VoiceCount; i++)
        {
            var voice = new GameObject("Footstep voice " + (i + 1)); voice.transform.SetParent(soundRoot.transform, false);
            var source = voice.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = false;
            source.spatialBlend = 0; source.dopplerLevel = 0; voices[i] = source; GameAudio.Route(source);
        }
        if(palette != null && palette.footsteps != null)
            foreach(var clip in palette.footsteps)
                if(clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        RefreshReferences();
    }
    void RefreshReferences()
    {
        if(motor == null) motor = GetComponent<TitanODMMotor>();
        if(target == null) target = GetComponent<TitanCombatTarget>();
        if(respawn == null) respawn = GetComponent<PlayerRespawn>();
    }
    void OnEnable() { if(health != null) health.Died += SilenceAndReset; ResetMotionHistory(); }
    void OnDisable() { if(health != null) health.Died -= SilenceAndReset; SilenceAndReset(); }
    void OnDestroy() { if(soundRoot != null) Destroy(soundRoot); }
    void OnApplicationFocus(bool focused) { focusLost = !focused; if(!focused) SilenceAndReset(); }
    void OnApplicationPause(bool paused) { applicationPaused = paused; if(paused) SilenceAndReset(); }
    bool Blocked()
    {
        return !isActiveAndEnabled || focusLost || applicationPaused || Time.timeScale <= 0 || AudioListener.pause ||
            health == null || !health.isActiveAndEnabled || health.isDead || health.isGrabbed ||
            controller == null || !controller.enabled || walking == null || gear == null ||
            !walking.isActiveAndEnabled || !gear.isActiveAndEnabled || walking.TitanSuspended || gear.TitanSuspended ||
            scout == null || !scout.isActiveAndEnabled || scout.animator == null || !scout.animator.isActiveAndEnabled ||
            scout.animator.runtimeAnimatorController == null ||
            (motor != null && motor.Suspended) || (target != null && (target.IsHeld || target.ReleasePending)) ||
            (respawn != null && respawn.Pending);
    }
    bool HasStepSupport()
    {
        if(!scout.Grounded || walking.GravityVerticalSpeed > .01f) return false;
        if(controller.isGrounded) return true;
        // isGrounded describes only the LAST Move. ODM follows walking/gravity
        // with three more Moves (often zero/lateral), which can clear Below on
        // a perfectly supported player. Use a short read-only WORLD-UP support
        // sweep instead of resetting the entire audio history every frame.
        var bounds = controller.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * .8f;
        if(!Finite(radius) || radius <= 0) return false;
        var origin = new Vector3(bounds.center.x, bounds.min.y + radius + .02f, bounds.center.z);
        int count = gameObject.scene.GetPhysicsScene().SphereCast(origin, radius, Vector3.down, supportHits,
            .04f + controller.skinWidth, walking.targetLayer, QueryTriggerInteraction.Ignore);
        if(count >= supportHits.Length) return false;
        float minimumUp = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
        for(int i = 0; i < count; i++)
        {
            var hit = supportHits[i];
            if(hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.normal.y >= minimumUp) return true;
        }
        return false;
    }
    void FixedUpdate()
    {
        RefreshReferences(); Vector3 position = transform.position; float dt = Time.fixedDeltaTime;
        if(Blocked() || palette == null || !Finite(position) || !Finite(dt) || dt <= 0 || dt > .1f)
        { SilenceAndReset(); return; }
        if(!positioned) { previousPosition = position; positioned = true; sampledAt = Time.time; return; }
        Vector3 velocity = (position - previousPosition) / dt; previousPosition = position; sampledAt = Time.time;
        if(!Finite(velocity) || velocity.magnitude > Safe(palette.maximumTrustedSpeed, 160, 1, 10000))
        { SilenceAndReset(); return; }
        validSamples = Mathf.Min(2, validSamples + 1);
        PlanarSpeed = validSamples >= 2 ? Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude : 0;
    }
    void LateUpdate()
    {
        RefreshReferences();
        if(Blocked() || palette == null || !Finite(Time.unscaledDeltaTime) || Time.unscaledDeltaTime > .2f || Time.time - sampledAt > .12f)
        { SilenceAndReset(); status = palette == null ? "Missing audio palette" : "Paused, blocked, disabled or stale motion"; return; }
        // Do not call Movement.CheckGround(): that also rotates the gameplay root.
        if(!HasStepSupport()) { SilenceAndReset(); status = "Airborne / no walkable support"; return; }
        if(validSamples < 2) { status = "Collecting movement samples"; return; }
        if(scout.State != "Run" || PlanarSpeed < Safe(palette.minimumPlanarSpeed, .6f, 0, 100))
        {
            phaseTracked = false;
            status = scout.State == "Run" ? "Below minimum movement speed" : "Waiting for ground gait";
            allowEntry = scout.State == "Idle" || scout.State == "Land";
            return; // Let an already-confirmed impact tail finish during an ordinary stop.
        }
        var animator = scout.animator;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if(animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            // No old-Run footsteps on an attack/flight blend-out; only use Run
            // blend-in once it is the dominant visible pose.
            if(next.shortNameHash != Run || animator.GetAnimatorTransitionInfo(0).normalizedTime < .5f)
            { phaseTracked = false; status = "Waiting for visible Run blend"; return; }
            state = next;
        }
        if(state.shortNameHash != Run || !Finite(state.normalizedTime) || !Finite(state.length) || state.length <= 0 ||
            animator.speed <= 0 || state.speed * state.speedMultiplier <= 0)
        { phaseTracked = false; status = "Run Animator is inactive or not advancing"; return; }
        status = "Tracking ground-gait plants";
        float phase = state.normalizedTime;
        int controllerId = animator.runtimeAnimatorController.GetInstanceID();
        float left = Mathf.Repeat(Safe(palette.leftPlantPhase, .9666667f, 0, 1), 1);
        float right = Mathf.Repeat(Safe(palette.rightPlantPhase, .4666667f, 0, 1), 1);
        if(!phaseTracked || controllerId != observedController || state.fullPathHash != observedState)
        {
            phaseTracked = true; previousPhase = phase; observedController = controllerId; observedState = state.fullPathHash;
            float leftAge = Mathf.Repeat(phase - left, 1), rightAge = Mathf.Repeat(phase - right, 1);
            if(allowEntry && Mathf.Min(leftAge, rightAge) <= Safe(palette.entryPlantWindow, .08f, 0, .15f))
                PlayStep(leftAge <= rightAge ? 0 : 1);
            allowEntry = false; return;
        }
        float advance = phase - previousPhase;
        if(advance < 0 || advance > .75f) { previousPhase = phase; return; }
        bool hitLeft = Mathf.Floor(phase - left) > Mathf.Floor(previousPhase - left);
        bool hitRight = Mathf.Floor(phase - right) > Mathf.Floor(previousPhase - right);
        previousPhase = phase;
        // A slow frame can cross both contacts. Emit only the most recent one,
        // never a backlog/burst of missed footsteps.
        if(hitLeft || hitRight)
            PlayStep(hitLeft && (!hitRight || Mathf.Repeat(phase - left, 1) < Mathf.Repeat(phase - right, 1)) ? 0 : 1);
    }
    void PlayStep(int foot)
    {
        var clips = palette.footsteps;
        if(clips == null || clips.Length == 0) { status = "No footstep clips assigned"; return; }
        int choices = 0;
        foreach(var clip in clips) if(Ready(clip) && clip != LastFootstep) choices++;
        bool allowRepeat = choices == 0;
        if(allowRepeat) foreach(var clip in clips) if(Ready(clip)) choices++;
        if(choices == 0) { status = "No loaded footstep clips"; return; }
        int choice = random.Next(choices); AudioClip selected = null;
        foreach(var clip in clips)
            if(Ready(clip) && (allowRepeat || clip != LastFootstep) && choice-- == 0) { selected = clip; break; }
        if(selected == null) return;
        var source = voices[nextVoice]; nextVoice = (nextVoice + 1) % VoiceCount;
        if(source == null) return;
        int start = palette.skipLeadingSilence ? CueStart(selected) : 0;
        float low = Safe(palette.minimumPitch, .97f, .5f, 1.5f), high = Safe(palette.maximumPitch, 1.03f, low, 1.5f);
        source.Stop(); source.clip = selected; source.volume = Safe(palette.volume, .35f, 0, 1);
        source.pitch = Mathf.Lerp(low, high, (float)random.NextDouble());
        source.Play(); if(start > 0) source.timeSamples = start;
        LastSource = source; LastFootstep = selected; LastFoot = foot; footstepsPlayed++;
        status = foot == 0 ? "Played left foot" : "Played right foot";
    }
    int CueStart(AudioClip clip)
    {
        int start; if(cueStarts.TryGetValue(clip, out start)) return start;
        start = 0;
        // Read imported decompressed samples once, so MP3/Vorbis decoder padding
        // and unequal quiet pre-impact scuffs don't move the impact off the plant.
        // Keep 5ms before the attack (10% of early peak, with a noise floor).
        // Never edit/import/clone a clip.
        if(clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.frequency > 0 && clip.channels > 0)
        {
            int frames = Mathf.Min(clip.samples, Mathf.CeilToInt(clip.frequency * .15f));
            var data = new float[frames * clip.channels];
            if(frames > 0 && clip.GetData(data, 0))
            {
                float peak = 0;
                foreach(float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
                float threshold = Mathf.Max(.005f, peak * .1f);
                for(int i = 0; i < data.Length; i++) if(Mathf.Abs(data[i]) >= threshold)
                { start = Mathf.Max(0, i / clip.channels - Mathf.CeilToInt(clip.frequency * .005f)); break; }
            }
        }
        cueStarts[clip] = start; return start;
    }
    static bool Ready(AudioClip clip) { return clip != null && clip.loadState == AudioDataLoadState.Loaded; }
    public void ResetMotionHistory()
    {
        foreach(var source in voices) if(source != null) source.Stop();
        previousPosition = transform.position; sampledAt = Time.time; positioned = phaseTracked = allowEntry = false;
        validSamples = 0; PlanarSpeed = 0;
    }
    void SilenceAndReset() { ResetMotionHistory(); }
    static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    static float Safe(float value, float fallback, float low, float high) { return Mathf.Clamp(Finite(value) ? value : fallback, low, high); }
}
