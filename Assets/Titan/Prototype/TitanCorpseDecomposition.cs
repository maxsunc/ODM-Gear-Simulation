using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Death/sever-only presentation and bounded disposal. Never writes bones,
/// colliders, joints, velocities, navigation, health, or global physics/RNG.</summary>
[DisallowMultipleComponent]
public sealed class TitanCorpseDecomposition : MonoBehaviour
{
    public const float DefaultLifetime=18f;
    public float Age {get;private set;}
    public float Lifetime {get;private set;}
    public float Opacity {get;private set;}=1;
    public float Shade {get;private set;}=1;
    public bool IsBranch {get;private set;}
    public int EmittedCount {get;private set;}
    public ParticleSystem Steam {get;private set;}
    public int OwnedMaterialCount {get{return materials.Count;}}
    sealed class Skin {public Renderer renderer;public Material[] originals;}
    sealed class Tint {public Material material;public Color color,emission;public bool transparent;}
    readonly List<Skin> skins=new List<Skin>();
    readonly List<Tint> materials=new List<Tint>();
    readonly List<Collider> anchors=new List<Collider>();
    System.Random random;
    float scale,emissionDebt;
    bool initialized,finished;

    public static TitanCorpseDecomposition BeginDeath(ModularTitanBody body)
    {
        if(!Application.isPlaying||body==null||!body.IsDead)return null;
        var fx=body.GetComponent<TitanCorpseDecomposition>();if(fx!=null)return fx;
        fx=body.gameObject.AddComponent<TitanCorpseDecomposition>();
        var colliders=new List<Collider>();foreach(var bone in body.ragdollBones)if(bone.collider!=null&&bone.collider.transform.IsChildOf(body.transform))colliders.Add(bone.collider);
        fx.Initialize(body.GetComponentsInChildren<Renderer>(true),colliders.ToArray(),Mathf.Abs(body.transform.lossyScale.y),DefaultLifetime,false);return fx;
    }
    public static TitanCorpseDecomposition BeginBranch(ModularTitanBody body,TitanDetachableLimb limb,GameObject root)
    {
        if(!Application.isPlaying||body==null||limb==null||root==null||!limb.severed)return null;
        var fx=root.GetComponent<TitanCorpseDecomposition>();if(fx!=null)return fx;
        fx=root.AddComponent<TitanCorpseDecomposition>();
        var colliders=new List<Collider>();foreach(var rb in limb.bodies)if(rb!=null)colliders.AddRange(rb.GetComponents<Collider>());
        float life=body.detachedLifetime>0?Mathf.Min(body.detachedLifetime,DefaultLifetime):DefaultLifetime;
        fx.Initialize(limb.renderers,colliders.ToArray(),Mathf.Abs(body.transform.lossyScale.y),life,true);return fx;
    }
    void Initialize(Renderer[] renderers,Collider[] colliders,float geometryScale,float lifetime,bool branch)
    {
        if(initialized)return;initialized=true;IsBranch=branch;Lifetime=Mathf.Max(.01f,lifetime);scale=Mathf.Clamp(geometryScale,.25f,10);
        random=new System.Random(Environment.TickCount^GetInstanceID());
        var clones=new Dictionary<Material,Material>();var seen=new HashSet<Renderer>();
        foreach(var renderer in renderers)
        {
            if(renderer==null||!seen.Add(renderer)||(!(renderer is SkinnedMeshRenderer)&&!(renderer is MeshRenderer)))continue;
            var originals=renderer.sharedMaterials;var replacements=(Material[])originals.Clone();skins.Add(new Skin{renderer=renderer,originals=originals});
            for(int i=0;i<originals.Length;i++)
            {
                var source=originals[i];if(source==null)continue;Material clone;
                if(!clones.TryGetValue(source,out clone))
                {
                    clone=new Material(source){name=source.name+" (decomposing)",hideFlags=HideFlags.DontSave};clones.Add(source,clone);
                    materials.Add(new Tint{material=clone,color=clone.HasProperty("_BaseColor")?clone.GetColor("_BaseColor"):clone.color,
                        emission=clone.HasProperty("_EmissionColor")?clone.GetColor("_EmissionColor"):Color.black});
                }
                replacements[i]=clone;
            }
            renderer.sharedMaterials=replacements;
        }
        foreach(var c in colliders)if(c!=null&&!c.isTrigger)anchors.Add(c);
        var steamMaterial=Resources.Load<Material>("TitanCorpseSteam");
        // The reference also retains the URP12 transparent Lit variants in builds;
        // live clones KEEP their exact original shader/textures/normal/metal maps.
        Resources.Load<Material>("TitanCorpseFadeVariant");
        if(steamMaterial!=null)CreateSteam(steamMaterial);
    }
    void CreateSteam(Material material)
    {
        var go=new GameObject("[Titan FX] Decomposition steam");go.layer=2;go.transform.SetParent(transform,false);
        Steam=go.AddComponent<ParticleSystem>();Steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Steam.useAutoRandomSeed=false;Steam.randomSeed=1937;
        var main=Steam.main;main.playOnAwake=false;main.loop=true;main.startSpeed=0;main.maxParticles=IsBranch?32:128;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=false;main.gravityModifier=0;
        var emission=Steam.emission;emission.enabled=false;var shape=Steam.shape;shape.enabled=false;
        var color=Steam.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.75f,.18f),new GradientAlphaKey(.45f,.55f),new GradientAlphaKey(0,1)});color.color=gradient;
        var size=Steam.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.6f,1,2.4f));
        var renderer=Steam.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        Steam.Play();
    }
    void Update(){TickDecomposition(Time.deltaTime);}
    public void TickDecomposition(float deltaTime)
    {
        if(!initialized||finished||deltaTime<=0||float.IsNaN(deltaTime)||float.IsInfinity(deltaTime))return;
        Age=Mathf.Min(Lifetime,Age+deltaTime);
        Shade=Mathf.Lerp(1,.55f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(Lifetime*.06f,Lifetime*.34f,Age)));
        Opacity=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(Lifetime/3,Lifetime*.97f,Age));
        foreach(var tint in materials)
        {
            if(tint.material==null)continue;
            if(Opacity<.99999f&&!tint.transparent){MakeTransparent(tint.material);tint.transparent=true;}
            var c=tint.color;c.r*=Shade;c.g*=Shade;c.b*=Shade;c.a*=Opacity;
            if(tint.material.HasProperty("_BaseColor"))tint.material.SetColor("_BaseColor",c);
            if(tint.material.HasProperty("_Color"))tint.material.SetColor("_Color",c);
            if(tint.material.HasProperty("_EmissionColor"))tint.material.SetColor("_EmissionColor",tint.emission*Shade*Opacity);
        }
        if(Age>=Lifetime){finished=true;Destroy(gameObject);return;}
        if(Steam==null||anchors.Count==0)return;
        // No hitch catch-up bursts. Allocation-free, bounded manual emissions from
        // CURRENT physical colliders, not the now-stationary animated root.
        float ramp=Mathf.SmoothStep(0,1,Age/Mathf.Min(.8f,Lifetime*.1f));
        float tail=1-Mathf.InverseLerp(Lifetime*.72f,Lifetime*.86f,Age);
        emissionDebt+=Mathf.Min(deltaTime,.1f)*(IsBranch?7:30)*ramp*tail;
        int count=Mathf.Min(8,Mathf.FloorToInt(emissionDebt));emissionDebt-=count;
        for(int i=0;i<count;i++)EmitSteam();
    }
    static void MakeTransparent(Material material)
    {
        material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
        material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
        material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        if(material.HasProperty("_SrcBlendAlpha"))material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
        if(material.HasProperty("_DstBlendAlpha"))material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.DisableKeyword("_ALPHATEST_ON");
        material.SetShaderPassEnabled("ShadowCaster",false);material.SetShaderPassEnabled("DepthOnly",false);material.SetShaderPassEnabled("DepthNormals",false);
    }
    void EmitSteam()
    {
        Collider c=null;for(int attempt=0;attempt<4;attempt++){c=anchors[random.Next(anchors.Count)];if(c!=null&&c.enabled&&c.gameObject.activeInHierarchy)break;c=null;}if(c==null)return;
        Vector3 direction=new Vector3(Range(-1,1),Range(.15f,1),Range(-1,1)).normalized;
        Vector3 point=c.ClosestPoint(c.bounds.center+direction*(c.bounds.extents.magnitude+scale));
        float energy=Mathf.Sqrt(scale);
        var p=new ParticleSystem.EmitParams{position=point+Vector3.up*.08f*scale,velocity=new Vector3(Range(-.25f,.25f),Range(1.2f,2.4f),Range(-.25f,.25f))*energy,
            startLifetime=Range(2.4f,3.6f),startSize=Range(.55f,1.1f)*scale*(IsBranch?.65f:1),startColor=new Color(.88f,.89f,.87f,Range(.3f,.48f)),rotation3D=new Vector3(0,0,Range(0,360))};
        Steam.Emit(p,1);EmittedCount++;
    }
    float Range(float a,float b){return Mathf.Lerp(a,b,(float)random.NextDouble());}
    void OnDestroy()
    {
        // Destroying just the observer must never leave renderers referring to
        // destroyed native materials. Scene/root disposal also takes this path.
        foreach(var skin in skins)if(skin.renderer!=null)skin.renderer.sharedMaterials=skin.originals;
        foreach(var tint in materials)if(tint.material!=null)Destroy(tint.material);materials.Clear();
        if(Steam!=null)Destroy(Steam.gameObject);
    }
}
