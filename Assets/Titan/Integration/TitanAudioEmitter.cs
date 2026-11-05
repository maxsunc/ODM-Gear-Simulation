using System;
using UnityEngine;

/// <summary>Read-only cosmetic observer of real blind events and animated sole
/// landings. Never drives the Animator, agent, bones, colliders or physics state.</summary>
[DefaultExecutionOrder(130)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ModularTitanBody))]
public sealed class TitanAudioEmitter : MonoBehaviour
{
    public TitanAudioPalette palette;
    public bool automaticSampling=true;
    public int ScreamCount {get;private set;}
    public int FootstepCount {get;private set;}
    public AudioClip LastScream {get;private set;}
    public AudioClip LastFootstep {get;private set;}
    public int LastFoot {get;private set;}
    ModularTitanBody body;TitanMotionStyle motion;TitanRuntimeActor actor;
    AudioSource voice;readonly AudioSource[] steps=new AudioSource[2];
    readonly BoxCollider[] soles=new BoxCollider[2];readonly bool[] lifted=new bool[2];
    readonly RaycastHit[] hits=new RaycastHit[16];
    System.Random random;Vector3 lastRoot;bool sampled;
    static readonly int Walking=Animator.StringToHash("IsWalking");
    void Awake()
    {
        body=GetComponent<ModularTitanBody>();motion=GetComponent<TitanMotionStyle>();actor=GetComponent<TitanRuntimeActor>();
        if(palette==null)palette=TitanAudioPalette.Load();ResetVariation(Guid.NewGuid().GetHashCode());
        voice=TitanAudioPalette.CreateSource("Blind scream",transform,true);
        for(int i=0;i<2;i++)
        {
            steps[i]=TitanAudioPalette.CreateSource(i==0?"Left foot":"Right foot",transform,true);
            var region=i==0?TitanHitRegion.LeftLeg:TitanHitRegion.RightLeg;
            foreach(var limb in body.limbs)if(limb.region==region && limb.bodies!=null && limb.bodies.Length>=3 && limb.bodies[2]!=null)
                foreach(var shape in limb.bodies[2].GetComponentsInChildren<BoxCollider>())
                    if(!shape.isTrigger && shape.attachedRigidbody==limb.bodies[2]){soles[i]=shape;break;}
        }
    }
    public void ResetVariation(int seed){random=new System.Random(seed);}
    void OnEnable(){body.BlindnessChanged+=OnBlind;body.Died+=StopSounds;ResetFeet();}
    void OnDisable(){if(body!=null){body.BlindnessChanged-=OnBlind;body.Died-=StopSounds;}StopSounds();}
    void OnDestroy()
    {
        if(voice!=null)Destroy(voice.gameObject);foreach(var source in steps)if(source!=null)Destroy(source.gameObject);
    }
    AudioClip Choose(AudioClip[] clips)
    {return clips==null || clips.Length==0?null:clips[random.Next(clips.Length)];}
    void OnBlind(bool blind)
    {
        if(!blind || !body.IsBlind || body.IsDead || palette==null)return;
        var clip=Choose(palette.blindScreams);if(clip==null)return;
        voice.transform.position=body.head!=null?body.head.position:transform.position;
        voice.PlayOneShot(clip,palette.screamVolume);LastScream=clip;ScreamCount++;
    }
    void ResetFeet(){lifted[0]=lifted[1]=false;sampled=false;lastRoot=transform.position;}
    void StopSounds(){ResetFeet();if(voice!=null)voice.Stop();foreach(var source in steps)if(source!=null)source.Stop();}
    void LateUpdate(){if(automaticSampling)SampleFeet(Time.deltaTime);}
    public void SampleFeet(float dt)
    {
        if(voice!=null && body.head!=null)voice.transform.position=body.head.position;
        var animator=body.animator;
        if(!isActiveAndEnabled || palette==null || !body.CanWalk || animator==null || !animator.enabled ||
            animator.runtimeAnimatorController==null || motion==null || motion.ActualSpeed<=.18f ||
            (actor!=null && !actor.IsReady) || !animator.GetBool(Walking) || dt<=0 || dt>.2f || float.IsNaN(dt) || float.IsInfinity(dt))
        {ResetFeet();return;}
        var state=animator.GetCurrentAnimatorStateInfo(0);
        if(!state.IsName("Locomotion") && !(animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName("Locomotion")))
        {ResetFeet();return;}
        float size=Mathf.Abs(transform.lossyScale.y);
        if(size<=0 || float.IsNaN(size) || float.IsInfinity(size)){ResetFeet();return;}
        if(sampled && Vector3.Distance(transform.position,lastRoot)>Mathf.Max(2,30*dt)){ResetFeet();return;}
        lastRoot=transform.position;sampled=true;
        for(int i=0;i<2;i++)
        {
            var sole=soles[i];if(sole==null || !sole.enabled){lifted[i]=false;continue;}
            // Compute current animated box bounds analytically, not stale native
            // Collider.bounds and not a global Physics.SyncTransforms() write.
            var matrix=sole.transform.localToWorldMatrix;var half=sole.size*.5f;
            var center=matrix.MultiplyPoint3x4(sole.center);
            float extentY=Mathf.Abs(matrix.m10)*half.x+Mathf.Abs(matrix.m11)*half.y+Mathf.Abs(matrix.m12)*half.z;
            Vector3 point=center-Vector3.up*extentY;
            float height;
            if(!GroundHeight(point,size,out height)){lifted[i]=true;continue;}
            float clearance=point.y-height;
            // Hysteresis: planted/bouncing support feet cannot fire every frame.
            // Uses real soles, so walk/run blending, cadence and cycle offsets follow automatically.
            if(clearance>=size*.20f){lifted[i]=true;continue;}
            if(clearance>size*.10f || clearance< -size*.15f)continue;
            if(lifted[i])
            {
                var clip=Choose(palette.footsteps);
                if(clip!=null){steps[i].transform.position=new Vector3(point.x,height,point.z);steps[i].PlayOneShot(clip,palette.footstepVolume);LastFootstep=clip;LastFoot=i;FootstepCount++;}
            }
            lifted[i]=false;
        }
    }
    bool GroundHeight(Vector3 point,float size,out float height)
    {
        height=0;float up=Mathf.Max(.25f,size*.75f);
        int count=gameObject.scene.GetPhysicsScene().Raycast(point+Vector3.up*up,Vector3.down,hits,up+size*3,~0,QueryTriggerInteraction.Ignore);
        if(count==hits.Length)return false;float nearest=float.PositiveInfinity;
        for(int n=0;n<count;n++)
        {
            var hit=hits[n];if(hit.collider==null || hit.normal.y<.5f || hit.collider.GetComponentInParent<ModularTitanBody>()!=null || hit.collider.GetComponentInParent<TitanCombatTarget>()!=null)continue;
            if(hit.distance<nearest){nearest=hit.distance;height=hit.point.y;}
        }
        return nearest<float.PositiveInfinity;
    }
}
