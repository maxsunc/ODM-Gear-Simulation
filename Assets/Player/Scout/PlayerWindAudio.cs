using UnityEngine;

/// <summary>Read-only personal airspeed observer. No motors, scene scans, ground
/// queries, root writes or changes to other AudioSources. One loop per real player.</summary>
[DefaultExecutionOrder(140)]
[DisallowMultipleComponent]
public sealed class PlayerWindAudio : MonoBehaviour
{
    public PlayerWindAudioPalette palette;
    public float Airspeed {get;private set;}
    public float TargetVolume {get;private set;}
    public bool Airborne {get;private set;}
    public AudioSource Source {get{return source;}}
    PlayerHealth health;Movement walking;ODM_Movement gear;CharacterController controller;
    ScoutPlayerAnimation scout;TitanODMMotor motor;TitanCombatTarget target;PlayerRespawn respawn;
    AudioSource source;Vector3 previousPosition;int validSamples;bool positioned,applicationPaused,focusLost;
    float sampledAt;

    public static PlayerWindAudio Install(PlayerHealth player)
    {
        if(player==null || player.GetComponent<Movement>()==null || player.GetComponent<ODM_Movement>()==null || player.GetComponent<CharacterController>()==null)return null;
        var result=player.GetComponent<PlayerWindAudio>();
        return result!=null?result:player.gameObject.AddComponent<PlayerWindAudio>();
    }
    void Awake()
    {
        health=GetComponent<PlayerHealth>();walking=GetComponent<Movement>();gear=GetComponent<ODM_Movement>();controller=GetComponent<CharacterController>();
        if(palette==null)palette=Resources.Load<PlayerWindAudioPalette>("Audio/PlayerWindAudio");
        var go=new GameObject("[Player SFX] Flight wind");go.hideFlags=HideFlags.DontSave;go.transform.SetParent(transform,false);
        source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.dopplerLevel=0;source.volume=0;
        GameAudio.Route(source);RefreshReferences();
    }
    void RefreshReferences()
    {
        if(scout==null)scout=GetComponent<ScoutPlayerAnimation>();if(motor==null)motor=GetComponent<TitanODMMotor>();
        if(target==null)target=GetComponent<TitanCombatTarget>();if(respawn==null)respawn=GetComponent<PlayerRespawn>();
    }
    void OnEnable(){if(health!=null)health.Died+=SilenceAndReset;ResetMotionHistory();}
    void OnDisable(){if(health!=null)health.Died-=SilenceAndReset;SilenceAndReset();}
    void OnDestroy(){if(source!=null)Destroy(source.gameObject);}
    void OnApplicationPause(bool paused){applicationPaused=paused;if(paused)SilenceAndReset();}
    void OnApplicationFocus(bool focused){focusLost=!focused;if(!focused)SilenceAndReset();}
    bool Blocked()
    {
        return !isActiveAndEnabled || applicationPaused || focusLost || Time.timeScale<=0 ||
            health==null || !health.isActiveAndEnabled || health.isDead || health.isGrabbed ||
            controller==null || !controller.enabled || walking==null || gear==null ||
            !walking.isActiveAndEnabled || !gear.isActiveAndEnabled || walking.TitanSuspended || gear.TitanSuspended ||
            (motor!=null && motor.Suspended) || (target!=null && (target.IsHeld || target.ReleasePending)) || (respawn!=null && respawn.Pending);
    }
    // Sample at the physics cadence, AFTER the existing motors; render-rate
    // displacement would alternate between zero and inflated speed at high FPS.
    void FixedUpdate()
    {
        RefreshReferences();var position=transform.position;float dt=Time.fixedDeltaTime;
        if(Blocked() || !Finite(position) || !Finite(dt) || dt<=0 || dt>.1f)
        {SilenceAndReset();return;}
        if(!positioned){previousPosition=position;positioned=true;sampledAt=Time.time;return;}
        float speed=(position-previousPosition).magnitude/dt;previousPosition=position;sampledAt=Time.time;
        float limit=palette!=null?Safe(palette.maximumTrustedSpeed,160,1,10000):160;
        if(!Finite(speed) || speed>limit){SilenceAndReset();return;}
        validSamples=Mathf.Min(2,validSamples+1);Airspeed=validSamples>=2?speed:0;
    }
    void LateUpdate()
    {
        RefreshReferences();
        if(Blocked() || palette==null || palette.loop==null || source==null || !Finite(Time.unscaledDeltaTime) || Time.unscaledDeltaTime>.2f || Time.time-sampledAt>.12f)
        {SilenceAndReset();return;}
        if(source.clip!=palette.loop){source.Stop();source.clip=palette.loop;source.volume=0;}
        Airborne=!controller.isGrounded && !(scout!=null && scout.isActiveAndEnabled && scout.Grounded);
        float low=Safe(palette.minimumSpeed,8,0,10000),high=Safe(palette.fullVolumeSpeed,55,low+.1f,10001);
        float amount=Airborne && validSamples>=2?Mathf.SmoothStep(0,1,Mathf.InverseLerp(low,high,Airspeed)):0;
        float maximum=Safe(palette.maximumVolume,.35f,0,1);TargetVolume=maximum*amount;
        float fade=TargetVolume>source.volume?Safe(palette.fadeInDuration,.35f,.01f,10):Safe(palette.fadeOutDuration,.25f,.01f,10);
        source.volume=Mathf.MoveTowards(source.volume,TargetVolume,Mathf.Max(maximum,.001f)*Time.unscaledDeltaTime/fade);
        source.pitch=Mathf.Lerp(Safe(palette.minimumPitch,.95f,.5f,1.5f),Safe(palette.maximumPitch,1.08f,.5f,1.5f),amount);
        if(source.volume>.0001f){if(!source.isPlaying)source.Play();}
        else if(source.isPlaying)source.Stop();
    }
    public void ResetMotionHistory()
    {positioned=false;validSamples=0;previousPosition=transform.position;sampledAt=Time.time;Airspeed=TargetVolume=0;Airborne=false;}
    void SilenceAndReset(){if(source!=null){source.Stop();source.volume=0;}ResetMotionHistory();}
    static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
    static bool Finite(Vector3 v){return Finite(v.x)&&Finite(v.y)&&Finite(v.z);}
    static float Safe(float v,float fallback,float min,float max){return Mathf.Clamp(Finite(v)?v:fallback,min,max);}
}
