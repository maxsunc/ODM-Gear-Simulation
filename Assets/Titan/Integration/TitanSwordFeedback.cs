using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Scene-only sword presentation and continuous real-root speed sampling.
/// Confirmed volume/blade contacts alone authorize feedback.
/// Render-only shake never writes the motor, camera transform, aiming or global time.</summary>
[DefaultExecutionOrder(120)]
[DisallowMultipleComponent]
public class TitanSwordFeedback : MonoBehaviour
{
    public DamageDealer dealer;
    public Attack attack;
    public TitanODMSwordSweep sweep;
    public Camera playerCamera;
    public Material effectMaterial;
    public AudioClip slashSound, hitSound, severSound, killSound;
    public TitanSwordAudioPalette audioPalette;
    [Header("Same-Titan physical impact grace")]
    [Range(0,.25f)] public float impactSpeedGrace=.18f;
    public int FinisherSoundCount { get; private set; }
    public int KillNotificationCount { get; private set; }
    public bool KillNotificationVisible { get { return Time.unscaledTime<killToastUntil; } }
    [Header("Feedback")]
    [Range(0,1)] public float shakeIntensity=.65f;
    [Range(0,1)] public float soundVolume=.65f;
    public bool bloodEnabled=true, trailsEnabled=true, numbersEnabled=true, hitMarkerEnabled=true;
    [Tooltip("Project the contact once, then let the number rise on-screen so fast ODM movement cannot leave it behind.")]
    public bool screenAnchoredNumbers=true;
    [Range(.2f,2)] public float numberLifetime=.85f;
    [Min(1)] public float maximumTrustedMotionSpeed=160;
    public int BiteBloodCount { get; private set; }
    public int ConfirmedHitCount { get; private set; }
    public TitanDamageResult LastHit { get; private set; }
    public float CurrentDamageSpeed { get { return MovementBlocked()?0:damageSpeed; } }
    struct Impact { public ModularTitanBody body;public float speed,time; }
    readonly Impact[] impacts=new Impact[4];
    readonly HashSet<int> killIds=new HashSet<int>();
    readonly Queue<int> killOrder=new Queue<int>();
    Vector3 lastMotion;
    float killToastUntil;
    bool pendingFinisher;
    int finisherIndex;

    struct Number { public Vector3 point,viewport;public float born;public string text;public Color color; }
    sealed class Spray { public ParticleSystem particles;public Transform follow;public float followUntil; }
    readonly List<Number> numbers=new List<Number>(32);
    readonly List<TrailRenderer> trails=new List<TrailRenderer>();
    readonly List<GameObject> owned=new List<GameObject>();
    Spray[] sprays;
    AudioSource audioSource;
    TitanODMMotor motor;
    TitanCombatTarget target;
    PlayerHealth health;
    Vector3 lastPosition;
    float damageSpeed,shakeUntil,shakePower,markerUntil;
    int validMotionSamples,poolIndex,serial,pendingSound=-1;
    Matrix4x4 originalProjection;
    Camera shakingCamera;
    bool automaticProjection;
    GUIStyle textStyle;
    Color markerColor=Color.white;

    void OnEnable()
    {
        motor=GetComponent<TitanODMMotor>();target=GetComponent<TitanCombatTarget>();health=GetComponent<PlayerHealth>();
        lastPosition=transform.position;validMotionSamples=0;damageSpeed=0;
        if(dealer!=null)dealer.HitConfirmed+=OnHit;
        if(attack!=null){attack.Released+=OnSlash;attack.Cancelled+=ResetDamageHistory;}
        if(audioPalette==null)audioPalette=Resources.Load<TitanSwordAudioPalette>("TitanSwordAudioPalette");
        if(!Application.isPlaying)return;
        audioSource=NewObject("Sword feedback audio").AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
        if(playerCamera==null)playerCamera=GetComponentInChildren<Camera>();
        if(playerCamera==null && Camera.main!=null && Camera.main.gameObject.scene==gameObject.scene)playerCamera=Camera.main;
        if(effectMaterial!=null) { CreateSprays();CreateTrails(); }
        RenderPipelineManager.beginCameraRendering+=BeginCamera;
        RenderPipelineManager.endCameraRendering+=EndCamera;
        Camera.onPreCull+=BeginBuiltin;Camera.onPostRender+=EndBuiltin;
    }
    bool MovementBlocked()
    {
        return (motor!=null&&motor.Suspended)||(target!=null&&(target.IsHeld||target.ReleasePending))||(health!=null&&(health.isDead||health.isGrabbed));
    }
    void FixedUpdate() { SampleDamageMotion(Time.fixedDeltaTime); }
    public void SampleDamageMotion(float dt)
    {
        Vector3 delta=transform.position-lastPosition;lastPosition=transform.position;
        float speed=dt>0?delta.magnitude/dt:float.PositiveInfinity;
        if(MovementBlocked()||dt<=0||dt>.1f||float.IsNaN(speed)||float.IsInfinity(speed)||speed>Mathf.Max(1,maximumTrustedMotionSpeed))
        { ResetDamageHistory();return; }
        validMotionSamples=Mathf.Min(2,validMotionSamples+1);damageSpeed=validMotionSamples>=2?speed:0;lastMotion=delta;
    }
    public void ResetDamageHistory()
    {
        validMotionSamples=0;damageSpeed=0;lastMotion=Vector3.zero;lastPosition=transform.position;
        for(int i=0;i<impacts.Length;i++)impacts[i]=default(Impact);
    }
    void OnControllerColliderHit(ControllerColliderHit hit)
    { RecordSolidContact(hit.collider,hit.moveDirection,hit.normal); }
    public void RecordSolidContact(Collider solid,Vector3 direction,Vector3 normal)
    {
        if(MovementBlocked()){ResetDamageHistory();return;}
        if(solid==null || solid.isTrigger || solid.transform.IsChildOf(transform) || direction.sqrMagnitude<.0001f || Vector3.Dot(direction.normalized,normal)>=-.05f)return;
        var body=solid.GetComponentInParent<ModularTitanBody>();
        if(body==null || body.IsDead){ResetDamageHistory();return;} // A wall cannot bank approach speed.
        if(lastMotion.sqrMagnitude<.000001f || Vector3.Dot(lastMotion.normalized,normal)>=-.05f || validMotionSamples<2 || damageSpeed<=0)return;
        float now=Time.time,grace=Mathf.Clamp(impactSpeedGrace,0,.25f);int slot=0;
        for(int i=0;i<impacts.Length;i++)
        {
            if(impacts[i].body==body && now-impacts[i].time<=grace)
            {
                // Repeated pushing must not keep refreshing an old high-speed receipt.
                if(damageSpeed>impacts[i].speed+.1f)impacts[i]=new Impact{body=body,speed=damageSpeed,time=now};
                return;
            }
            if(impacts[i].body==null || impacts[i].time<impacts[slot].time)slot=i;
        }
        impacts[slot]=new Impact{body=body,speed=damageSpeed,time=now};
    }
    public float DamageSpeedFor(ModularTitanBody body)
    {
        if(MovementBlocked()){ResetDamageHistory();return 0;}
        float speed=damageSpeed;
        if(body==null || body.IsDead || attack==null || !attack.IsAttacking || !attack.CanAttack)return speed;
        float now=Time.time,grace=Mathf.Clamp(impactSpeedGrace,0,.25f);
        if(now-attack.LastReleaseTime>attack.strikeDelay+attack.activeDuration+.05f)return speed;
        foreach(var impact in impacts)
            if(impact.body==body && now-impact.time<=grace && impact.time>=attack.LastReleaseTime-.12f)
                speed=Mathf.Max(speed,impact.speed);
        return speed;
    }
    GameObject NewObject(string label)
    {
        var go=new GameObject("[Sword FX] "+label);go.hideFlags=HideFlags.DontSave;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);owned.Add(go);return go;
    }
    void CreateSprays()
    {
        sprays=new Spray[8];
        for(int i=0;i<sprays.Length;i++)
        {
            var go=NewObject("blood pool "+i);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=1;main.maxParticles=96;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=false;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.22f,.65f);main.startSpeed=new ParticleSystem.MinMaxCurve(3f,8f);
            main.startSize=new ParticleSystem.MinMaxCurve(.07f,.19f);main.startColor=new ParticleSystem.MinMaxGradient(new Color(.35f,.008f,.015f),new Color(.75f,.025f,.018f));
            main.gravityModifier=1;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=24;shape.radius=.07f;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.45f,.45f,.45f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.85f,.55f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=effectMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            sprays[i]=new Spray{particles=ps};
        }
    }
    void CreateTrails()
    {
        if(sweep==null)return;
        foreach(var blade in sweep.bladeMeshes)
        {
            if(blade==null)continue;var filter=blade.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
            Bounds bounds=filter.sharedMesh.bounds;int axis=bounds.size.x>bounds.size.y?0:1;if(bounds.size.z>bounds.size[axis])axis=2;
            Vector3 a=bounds.center,b=bounds.center;a[axis]=bounds.min[axis];b[axis]=bounds.max[axis];
            var go=NewObject("blade tip trail");go.transform.SetParent(blade,false);go.transform.localPosition=a.sqrMagnitude>b.sqrMagnitude?a:b;
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=effectMaterial;trail.time=.095f;trail.minVertexDistance=.025f;
            trail.startWidth=.15f;trail.endWidth=.008f;trail.numCapVertices=2;trail.emitting=false;
            trail.startColor=new Color(1,.96f,.73f,.8f);trail.endColor=new Color(1,.9f,.65f,0);
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trails.Add(trail);
        }
    }
    void SprayBlood(Vector3 point, Vector3 direction, float scale, int count, Transform follow=null)
    {
        if(!bloodEnabled||sprays==null)return;
        var spray=sprays[poolIndex++%sprays.Length];var ps=spray.particles;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.transform.position=point;ps.transform.rotation=Quaternion.LookRotation(direction.sqrMagnitude>.001f?direction:Vector3.up);
        var main=ps.main;main.startSpeed=new ParticleSystem.MinMaxCurve(3*scale,8*scale);main.startSize=new ParticleSystem.MinMaxCurve(.07f*scale,.19f*scale);
        ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(++serial);spray.follow=follow;spray.followUntil=Time.time+.18f;
        var emission=ps.emission;emission.enabled=follow!=null;emission.rateOverTime=90;
        ps.Play();ps.Emit(count);
    }
    public void PlayBiteBlood(Vector3 point, Vector3 direction)
    {
        if (!isActiveAndEnabled || !bloodEnabled || sprays == null) return;
        BiteBloodCount++;
        SprayBlood(point, direction + Vector3.up * .25f, 1.5f, 80);
    }
    public static Color OutcomeColor(TitanDamageOutcome outcome)
    {
        return outcome==TitanDamageOutcome.Killed?new Color(1,.12f,.08f):
            outcome==TitanDamageOutcome.Severed||outcome==TitanDamageOutcome.Blinded?new Color(1,.83f,.05f):Color.white;
    }
    void OnHit(TitanDamageResult hit)
    {
        if(!hit.Accepted)return;ConfirmedHitCount++;LastHit=hit;
        if(hit.Outcome==TitanDamageOutcome.Killed && hit.Body!=null && killIds.Add(hit.Body.GetInstanceID()))
        {
            killOrder.Enqueue(hit.Body.GetInstanceID());if(killOrder.Count>64)killIds.Remove(killOrder.Dequeue());
            KillNotificationCount++;killToastUntil=Time.unscaledTime+1.5f;
        }
        if(hit.Outcome==TitanDamageOutcome.Killed || (hit.Outcome==TitanDamageOutcome.Severed && (hit.Region==TitanHitRegion.LeftArm || hit.Region==TitanHitRegion.RightArm)))pendingFinisher=true;
        int severity=hit.Outcome==TitanDamageOutcome.Killed?2:hit.Outcome==TitanDamageOutcome.Hit?0:1;
        Color color=OutcomeColor(hit.Outcome);float now=Time.unscaledTime;
        if(numbersEnabled)
        {
            if(numbers.Count>=32)numbers.RemoveAt(0);
            Vector3 viewport=playerCamera!=null?playerCamera.WorldToViewportPoint(hit.Point):new Vector3(.5f,.6f,1);
            if(viewport.z<=0)viewport=new Vector3(.5f,.6f,1);
            viewport.x=Mathf.Clamp(viewport.x,.08f,.92f);viewport.y=Mathf.Clamp(viewport.y,.12f,.88f);
            numbers.Add(new Number{point=hit.Point,viewport=viewport,born=now,text=Mathf.CeilToInt(hit.Damage).ToString(),color=color});
        }
        markerUntil=now+.14f;markerColor=color;pendingSound=Mathf.Max(pendingSound,severity);
        shakePower=Mathf.Max(shakePower,severity==2?.012f:severity==1?.008f:.0035f);shakeUntil=Mathf.Max(shakeUntil,now+(severity==2?.22f:severity==1?.16f:.09f));
        float scale=hit.Body!=null?Mathf.Clamp(Mathf.Sqrt(Mathf.Abs(hit.Body.transform.lossyScale.x)),1,2.25f):1;
        SprayBlood(hit.Point,hit.Direction,scale,severity==2?72:severity==1?48:20);
        if(hit.Outcome==TitanDamageOutcome.Severed && hit.Body!=null)
            foreach(var limb in hit.Body.limbs)if(limb.region==hit.Region && limb.rootBone!=null)
            { SprayBlood(limb.rootBone.position,hit.Direction+Vector3.up*.3f,scale,64,limb.rootBone);break; }
    }
    void OnSlash() { if(audioSource!=null && slashSound!=null && (attack==null||!attack.HasSlashSound))audioSource.PlayOneShot(slashSound,soundVolume*.65f); }
    void LateUpdate()
    {
        bool cutting=trailsEnabled && dealer!=null && dealer.isActiveAndEnabled;
        foreach(var trail in trails)if(trail!=null)trail.emitting=cutting;
        if(pendingSound>=0)
        {
            AudioClip clip=pendingSound==2?killSound:pendingSound==1?severSound:hitSound;
            if(audioSource!=null && clip!=null)audioSource.PlayOneShot(clip,soundVolume);pendingSound=-1;
        }
        if(pendingFinisher)
        {
            if(audioSource!=null && audioPalette!=null && audioPalette.finisherSounds!=null && audioPalette.finisherSounds.Length>0)
            {
                var clip=audioPalette.finisherSounds[finisherIndex++%audioPalette.finisherSounds.Length];
                if(clip!=null){audioSource.PlayOneShot(clip,soundVolume*Mathf.Clamp01(audioPalette.volume));FinisherSoundCount++;}
            }
            pendingFinisher=false;
        }
        for(int i=numbers.Count-1;i>=0;i--)if(Time.unscaledTime-numbers[i].born>numberLifetime)numbers.RemoveAt(i);
        if(Time.unscaledTime>=shakeUntil)shakePower=0;
        if(sprays!=null)foreach(var spray in sprays)
        {
            if(bloodEnabled && spray.follow!=null && Time.time<spray.followUntil)spray.particles.transform.position=spray.follow.position;
            else { var emission=spray.particles.emission;emission.enabled=false;spray.follow=null; }
        }
    }
    void BeginCamera(ScriptableRenderContext context, Camera camera) { BeginShake(camera); }
    void EndCamera(ScriptableRenderContext context, Camera camera) { EndShake(camera); }
    void BeginBuiltin(Camera camera) { BeginShake(camera); }
    void EndBuiltin(Camera camera) { EndShake(camera); }
    public void BeginShake(Camera camera)
    {
        if(shakingCamera!=null||camera==null||camera!=playerCamera||shakeIntensity<=0||Time.unscaledTime>=shakeUntil)return;
        originalProjection=camera.projectionMatrix;
        camera.ResetProjectionMatrix();automaticProjection=camera.projectionMatrix==originalProjection;
        var projection=originalProjection;float t=Time.unscaledTime;
        float fade=Mathf.Clamp01((shakeUntil-t)/.09f)*shakePower*shakeIntensity;
        projection.m02+=Mathf.Sin(t*147)*fade;projection.m12+=Mathf.Cos(t*173)*fade*.65f;
        shakingCamera=camera;camera.projectionMatrix=projection;
    }
    public void EndShake(Camera camera)
    {
        if(camera==null||shakingCamera!=camera)return;
        if(automaticProjection)camera.ResetProjectionMatrix();else camera.projectionMatrix=originalProjection;
        shakingCamera=null;
    }
    void OnGUI()
    {
        if(playerCamera==null)return;
        if(textStyle==null)textStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
        textStyle.normal.textColor=Color.white;
        var oldColor=GUI.color;
        foreach(var number in numbers)
        {
            Vector3 p=screenAnchoredNumbers?new Vector3(number.viewport.x*Screen.width,number.viewport.y*Screen.height,1):playerCamera.WorldToScreenPoint(number.point);if(p.z<=0)continue;
            float age=(Time.unscaledTime-number.born)/Mathf.Max(.2f,numberLifetime);float alpha=Mathf.Clamp01((1-age)*3);
            float size=Mathf.Lerp(34,25,Mathf.Clamp01(age*5));textStyle.fontSize=Mathf.RoundToInt(size);
            var rect=new Rect(p.x-65,Screen.height-p.y-32-age*60,130,50);
            GUI.color=new Color(0,0,0,alpha);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)GUI.Label(new Rect(rect.x+x*2,rect.y+y*2,rect.width,rect.height),number.text,textStyle);
            GUI.color=new Color(number.color.r,number.color.g,number.color.b,alpha);GUI.Label(rect,number.text,textStyle);
        }
        if(hitMarkerEnabled && Time.unscaledTime<markerUntil)
        { textStyle.fontSize=25;GUI.color=markerColor;GUI.Label(new Rect(Screen.width/2f-20,Screen.height/2f-20,40,40),"×",textStyle); }
        if(KillNotificationVisible)
        {
            float alpha=Mathf.Clamp01((killToastUntil-Time.unscaledTime)/.35f);textStyle.fontSize=28;
            var rect=new Rect(Screen.width*.5f-170,Screen.height*.18f,340,44);
            GUI.color=new Color(0,0,0,alpha);GUI.Label(new Rect(rect.x+2,rect.y+2,rect.width,rect.height),"+Titan Kill",textStyle);
            GUI.color=new Color(1,.91f,.65f,alpha);GUI.Label(rect,"+Titan Kill",textStyle);
        }
        GUI.color=oldColor;
    }
    void OnDisable()
    {
        if(dealer!=null)dealer.HitConfirmed-=OnHit;if(attack!=null){attack.Released-=OnSlash;attack.Cancelled-=ResetDamageHistory;}
        if(shakingCamera!=null)EndShake(shakingCamera);
        RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderPipelineManager.endCameraRendering-=EndCamera;
        Camera.onPreCull-=BeginBuiltin;Camera.onPostRender-=EndBuiltin;
        foreach(var go in owned)if(go!=null)Destroy(go);owned.Clear();trails.Clear();numbers.Clear();sprays=null;
        shakeUntil=shakePower=markerUntil=killToastUntil=0;pendingSound=-1;pendingFinisher=false;
        ResetDamageHistory();killIds.Clear();killOrder.Clear();
    }
}
