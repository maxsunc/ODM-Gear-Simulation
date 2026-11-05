using UnityEngine;

/// <summary>Reusable cosmetic lightning/flash/dust/steam. No colliders, forces,
/// camera authority, timescale change or scene-global light/fog writes.</summary>
public sealed class TitanSpawnStrikeEffect : MonoBehaviour
{
    LineRenderer[] bolts;
    ParticleSystem sparks,steam,flash,dust;
    Light glow;
    AudioSource sound;
    float scale,started;
    bool warning,impact;
    public bool IsPlaying {get{return warning||impact;}}
    public int ImpactCount {get;private set;}
    public int SoundCount {get;private set;}
    public void Configure(Material lightning,Material particles)
    {
        bolts=new LineRenderer[4];
        for(int i=0;i<bolts.Length;i++)
        {
            var go=new GameObject("Bolt "+i);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lightning;line.useWorldSpace=false;
            line.positionCount=9;line.numCapVertices=2;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;bolts[i]=line;
        }
        sparks=Particles("Warning sparks",particles,48);steam=Particles("Rising steam",particles,128);
        dust=Particles("Ground burst",particles,64);flash=Particles("Strike flash",particles,4);
        glow=gameObject.AddComponent<Light>();glow.type=LightType.Point;glow.shadows=LightShadows.None;glow.color=new Color(1,.65f,.18f);glow.enabled=false;
        sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.loop=false;sound.spatialBlend=1;sound.rolloffMode=AudioRolloffMode.Linear;GameAudio.Route(sound);
    }
    ParticleSystem Particles(string name,Material material,int max)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=3;main.maxParticles=max;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startSpeed=0;main.startLifetime=2;main.gravityModifier=0;main.startSize=1;
        var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
        var colors=ps.colorOverLifetime;colors.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.65f,.6f),new GradientAlphaKey(0,1)});colors.color=gradient;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return ps;
    }
    public void Warn(Vector3 point,float size,int seed)
    {
        StopEffect();transform.position=point+Vector3.up*.06f;scale=size;started=Time.time;warning=true;
        var rng=new System.Random(seed);float height=12*size*1.8f;
        for(int b=0;b<bolts.Length;b++)
        {
            var line=bolts[b];Vector3 end=new Vector3(((float)rng.NextDouble()-.5f)*size*1.2f,0,((float)rng.NextDouble()-.5f)*size*1.2f);
            for(int i=0;i<9;i++)
            {
                float t=i/8f;Vector3 p=Vector3.Lerp(end,Vector3.up*height,t);
                if(i>0&&i<8)p+=new Vector3(((float)rng.NextDouble()-.5f)*size*1.8f,0,((float)rng.NextDouble()-.5f)*size*1.8f);
                line.SetPosition(i,p);
            }
            line.widthMultiplier=.1f*Mathf.Sqrt(size);line.startColor=line.endColor=new Color(1,.7f,.15f,.4f);line.enabled=true;
        }
        Emit(sparks,12,new Color(1,.75f,.2f,.6f),size*.07f,size*.16f,.3f,.7f,size*.5f,size*2,rng);
    }
    public void Strike(AudioClip clip,float volume,float maxDistance,bool lightEnabled)
    {
        warning=false;impact=true;started=Time.time;ImpactCount++;
        var rng=new System.Random(ImpactCount*977+31);
        foreach(var line in bolts){line.widthMultiplier=.5f*Mathf.Sqrt(scale);line.startColor=line.endColor=new Color(1,.9f,.55f,1);line.enabled=true;}
        Emit(flash,1,new Color(1,.8f,.3f,.75f),scale*6,scale*6,.16f,.16f,0,0,rng);
        Emit(sparks,36,new Color(1,.65f,.1f,1),scale*.08f,scale*.19f,.3f,.8f,scale,scale*5,rng);
        Emit(dust,56,new Color(.58f,.49f,.35f,.5f),scale*.6f,scale*1.6f,1,2,scale*1.8f,scale*2,rng);
        Emit(steam,96,new Color(.85f,.87f,.87f,.34f),scale*1.6f,scale*3,1.4f,2.8f,scale*1.2f,scale*3.2f,rng,true);
        glow.range=scale*15;glow.intensity=3;glow.enabled=lightEnabled;
        PlaySound(clip,volume,maxDistance);
    }
    public void PlaySound(AudioClip clip,float volume,float maxDistance)
    {
        if(sound!=null && clip!=null && volume>0){sound.volume=Mathf.Clamp01(volume);sound.minDistance=10;sound.maxDistance=Mathf.Max(11,maxDistance);sound.PlayOneShot(clip);SoundCount++;}
    }
    void Emit(ParticleSystem ps,int count,Color color,float minSize,float maxSize,float minLife,float maxLife,float spread,float speed,System.Random rng,bool upward=false)
    {
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed=false;ps.randomSeed=(uint)rng.Next(1,int.MaxValue);ps.Play();
        for(int i=0;i<count;i++)
        {
            float angle=(float)rng.NextDouble()*Mathf.PI*2,radius=Mathf.Sqrt((float)rng.NextDouble())*spread;
            var p=new ParticleSystem.EmitParams {position=transform.position+new Vector3(Mathf.Cos(angle)*radius,upward?(float)rng.NextDouble()*scale*7:0,Mathf.Sin(angle)*radius),
                velocity=new Vector3(Mathf.Cos(angle),upward?1.4f:.2f,Mathf.Sin(angle))*speed*((float)rng.NextDouble()*.5f+.5f),
                startColor=color,startSize=Mathf.Lerp(minSize,maxSize,(float)rng.NextDouble()),startLifetime=Mathf.Lerp(minLife,maxLife,(float)rng.NextDouble())};
            ps.Emit(p,1);
        }
    }
    void Update()
    {
        if(warning)
        {
            float alpha=.3f+.2f*Mathf.Abs(Mathf.Sin((Time.time-started)*29));foreach(var b in bolts)b.startColor=b.endColor=new Color(1,.7f,.15f,alpha);
        }
        if(!impact)return;
        float age=Time.time-started,alphaFade=Mathf.Clamp01(1-age/.25f);
        foreach(var b in bolts){b.enabled=alphaFade>0;b.startColor=b.endColor=new Color(1,.85f,.45f,alphaFade);}
        glow.intensity=3*Mathf.Clamp01(1-age/.15f);if(age>=.15f)glow.enabled=false;
        if(age>3 && !sound.isPlaying)StopEffect();
    }
    public void StopEffect()
    {
        warning=impact=false;
        if(bolts!=null)foreach(var b in bolts)if(b!=null)b.enabled=false;
        foreach(var ps in new[]{sparks,steam,dust,flash})if(ps!=null)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(glow!=null)glow.enabled=false;if(sound!=null)sound.Stop();
    }
    void OnDisable(){StopEffect();}
}
