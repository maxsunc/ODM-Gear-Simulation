using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>ONE death receipt from PlayerHealth, independent of the cause or bite adapter.
/// Scene-owned blood/audio tails survive hidden live visuals; no gameplay/physics writes.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDeathFeedback : MonoBehaviour
{
    public PlayerImpactPalette palette;
    public int DeathCount {get;private set;}
    public int BloodSoundCount {get;private set;}
    public int BoneSoundCount {get;private set;}
    public ParticleSystem Blood {get;private set;}
    public AudioSource BloodSource {get;private set;}
    public AudioSource BoneSource {get;private set;}
    Transform follow;float sprayUntil;GameObject effect;bool playedThisLife;
    public bool PlayedThisLife {get{return playedThisLife;}}
    internal void ResetForRespawn()
    {playedThisLife=false;follow=null;effect=null;Blood=null;BloodSource=BoneSource=null;}
    public void PlayDeath(ScoutPlayerRagdoll ragdoll)
    {
        if(playedThisLife)return;playedThisLife=true;DeathCount++;
        if(palette==null)palette=Resources.Load<PlayerImpactPalette>("PlayerImpactPalette");
        if(palette==null)return;
        if(ragdoll!=null && ragdoll.Corpse!=null)
            foreach(var rb in ragdoll.Bodies)if(rb.name=="Chest"){follow=rb.transform;break;}
        Vector3 point=follow!=null?follow.position:transform.position;
        effect=new GameObject("[Player death] Blood and bone impact");effect.hideFlags=HideFlags.DontSave;SceneManager.MoveGameObjectToScene(effect,gameObject.scene);effect.transform.position=point;
        BloodSource=Sound("Blood splat",palette.bloodSplat,palette.bloodVolume);if(BloodSource!=null)BloodSoundCount++;
        BoneSource=Sound("Bone crush",palette.boneCrush,palette.boneVolume);if(BoneSource!=null)BoneSoundCount++;
        if(palette.bloodMaterial!=null)
        {
            Blood=effect.AddComponent<ParticleSystem>();Blood.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Blood.useAutoRandomSeed=false;Blood.randomSeed=1937;
            var main=Blood.main;main.loop=false;main.playOnAwake=false;main.duration=.5f;main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,1);main.startSpeed=new ParticleSystem.MinMaxCurve(3,9);main.startSize=new ParticleSystem.MinMaxCurve(.08f,.23f);main.gravityModifier=1;main.useUnscaledTime=false;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(.28f,.002f,.008f),new Color(.8f,.012f,.025f));
            var emission=Blood.emission;emission.enabled=true;emission.rateOverTime=65;
            var shape=Blood.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.18f;
            var fade=Blood.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.6f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=Blood.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=palette.bloodMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            Blood.Play();Blood.Emit(100);sprayUntil=Time.time+.4f;
        }
        float tail=Mathf.Max(palette.bloodSplat!=null?palette.bloodSplat.length:0,palette.boneCrush!=null?palette.boneCrush.length:0);Destroy(effect,Mathf.Max(2,tail+.2f));
    }
    AudioSource Sound(string label,AudioClip clip,float volume)
    {
        if(clip==null)return null;var go=new GameObject(label);go.transform.SetParent(effect.transform,false);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;
        source.spatialBlend=0;source.dopplerLevel=0;source.clip=clip;source.volume=Mathf.Clamp01(volume);GameAudio.Route(source);source.Play();return source;
    }
    void LateUpdate()
    {
        if(Blood==null)return;if(follow!=null)Blood.transform.position=follow.position;
        if(Time.time>=sprayUntil){var emission=Blood.emission;emission.enabled=false;}
    }
}
