using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in bounded random population over existing map navigation. Scale
/// choices use exact pre-baked clearance categories, never per-spawn world baking.</summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public sealed class TitanRandomSpawner : MonoBehaviour
{
    [Serializable]public class SizeChoice { [Min(.001f)]public float scale=3;[Min(0)]public float weight=1; }
    public TitanFreeRoamIntegration sceneManager;
    public GameObject titanPrefab;
    public SizeChoice[] sizes=new[]{new SizeChoice{scale=3,weight=1},new SizeChoice{scale=5,weight=1}};
    [Header("Population / schedule")]
    public bool automaticSpawning=true;
    [Min(0)]public int initialSpawnCount=4;
    [Min(1)]public int maximumAlive=6;
    [Tooltip("Placed Titans and other spawns owned by this scene manager also consume the cap.")]
    public bool countPlacedTitans=true;
    [Min(0)]public float initialDelay=2;
    [Min(.1f)]public float minimumSpawnInterval=8,maximumSpawnInterval=16;
    [Min(.1f)]public float failedAttemptRetryDelay=2;
    [Tooltip("0 chooses a fresh local seed each Play; nonzero makes proposals repeatable. Never changes Unity's gameplay RNG.")]
    public int randomSeed;
    [Header("Placement (world units)")]
    [Min(0)]public float minimumPlayerDistance=80;
    [Tooltip("0 permits the whole baked map. Distance is horizontal, including when the player is flying.")]
    [Min(0)]public float maximumPlayerDistance;
    [Min(0)]public float minimumTitanSeparation=15;
    [Range(1,128)]public int candidateAttempts=24;
    public bool useCustomBounds;
    public Bounds spawnBounds=new Bounds(Vector3.zero,new Vector3(300,200,300));
    [Header("Arrival strike")]
    [Min(0)]public float warningDuration=.65f;
    public bool effectsEnabled=true,localFlashLight=true;
    public Material lightningMaterial,particleMaterial;
    [Tooltip("Optional: drag your own Titan strike AudioClip here. Empty means silent.")]
    public AudioClip spawnSound;
    [Range(0,1)]public float soundVolume=.7f;
    [Min(11)]public float soundMaxDistance=600;
    [Range(1,8)]public int effectPoolSize=4;
    [Header("Owned corpses only")]
    [Tooltip("0 disables time-based cleanup. Does not remove other placed Titans or their corpses.")]
    [Min(0)]public float corpseLifetime=25;
    [Min(0)]public int maximumRetainedCorpses=6;
    public bool despawnOwnedOnDestroy=true;
    public string LastError {get;private set;}
    public int SpawnedCount {get;private set;}
    public int CancelledCount {get;private set;}
    public int OwnedAliveCount {get{int n=0;foreach(var e in owned)if(e.body!=null&&!e.body.IsDead&&e.body.gameObject.activeInHierarchy)n++;return n;}}
    public bool HasPendingSpawn {get{return pending!=null;}}
    public ModularTitanBody PendingBody {get{return pending;}}
    public int CachedTriangleCount {get{return sampler.TriangleCount;}}
    sealed class Entry {public ModularTitanBody body;public float death=-1;}
    readonly List<Entry> owned=new List<Entry>();
    readonly TitanSpawnSurfaceSampler sampler=new TitanSpawnSurfaceSampler();
    readonly List<TitanSpawnStrikeEffect> pool=new List<TitanSpawnStrikeEffect>();
    System.Random random;
    ModularTitanBody pending;
    TitanSpawnStrikeEffect pendingEffect;
    float due,nextAttempt;
    int initialRemaining,poolCursor;
    bool initialized,cached;

    void OnEnable(){if(Application.isPlaying && !initialized)RestartSchedule();}
    public void RestartSchedule()
    {
        CancelPending();random=new System.Random(randomSeed==0?Guid.NewGuid().GetHashCode():randomSeed);
        initialRemaining=Mathf.Max(0,initialSpawnCount);
        float delay=Mathf.Max(0,initialDelay);
        if(initialRemaining==0)delay+=Mathf.Lerp(minimumSpawnInterval,maximumSpawnInterval,(float)random.NextDouble());
        nextAttempt=Time.time+delay;initialized=true;cached=false;
    }
    void OnDisable(){CancelPending();DestroyEffects();}
    void OnDestroy(){if(despawnOwnedOnDestroy)DespawnOwned();else CancelPending();DestroyEffects();}
    int Population {get{return countPlacedTitans?sceneManager.LivingCount+sceneManager.PreparedCount:OwnedAliveCount+(HasPendingSpawn?1:0);}}
    public bool ValidateConfiguration(out string error)
    {
        error=null;
        if(!isActiveAndEnabled || sceneManager==null || !sceneManager.isActiveAndEnabled || sceneManager.gameObject.scene!=gameObject.scene ||
            sceneManager.player==null || sceneManager.navigation==null || !sceneManager.navigation.IsReady)
            error="Assign the active scene Titan manager and registered navigation";
        else if(titanPrefab==null || titanPrefab.GetComponent<ModularTitanBody>()==null)error="Assign the passive modular Titan prefab";
        else if(maximumAlive<1 || initialSpawnCount<0 || candidateAttempts<1 || candidateAttempts>128 || effectPoolSize<1 || effectPoolSize>8 || maximumRetainedCorpses<0)
            error="Invalid population, attempt or pool limits";
        else if(!FiniteNonnegative(initialDelay,warningDuration,minimumPlayerDistance,maximumPlayerDistance,minimumTitanSeparation,corpseLifetime,soundVolume) ||
            !TitanSpawnPlacement.Finite(minimumSpawnInterval)||minimumSpawnInterval<.1f || !TitanSpawnPlacement.Finite(maximumSpawnInterval)||maximumSpawnInterval<minimumSpawnInterval ||
            !TitanSpawnPlacement.Finite(failedAttemptRetryDelay)||failedAttemptRetryDelay<.1f || !TitanSpawnPlacement.Finite(soundMaxDistance)||soundMaxDistance<11 || soundVolume>1)
            error="Spawn timing/distances/volume must be finite and valid";
        else if(maximumPlayerDistance>0 && maximumPlayerDistance<minimumPlayerDistance)error="Maximum player distance must be 0 (unlimited) or at least the minimum";
        else if(useCustomBounds && (!TitanSpawnPlacement.Finite(spawnBounds.center)||!TitanSpawnPlacement.Finite(spawnBounds.size)||spawnBounds.size.x<=0||spawnBounds.size.y<=0||spawnBounds.size.z<=0))error="Custom world-space bounds must be finite with positive size";
        else if(effectsEnabled && (lightningMaterial==null||particleMaterial==null))error="Assign the strike lightning and particle materials, or disable effects";
        if(error!=null)return false;
        double total=0;
        if(sizes!=null)foreach(var s in sizes)
        {
            if(s==null)continue;
            if(!TitanSpawnPlacement.Finite(s.weight)||s.weight<0){error="Size weights must be finite and nonnegative";return false;}
            if(s.weight==0)continue;
            if(!TitanSpawnPlacement.Finite(s.scale)||s.scale<=0 || sceneManager.navigation.ForScale(s.scale)==null)
            {error="Size "+s.scale+" has no matching navigation profile; rebake the requested sizes once in Edit Mode";return false;}
            total+=s.weight;
        }
        if(total<=0){error="Enable at least one weighted Titan size";return false;}
        return true;
    }
    static bool FiniteNonnegative(params float[] values){foreach(float f in values)if(!TitanSpawnPlacement.Finite(f)||f<0)return false;return true;}
    public bool TryChooseScale(out float scale,out string error)
    {
        scale=0;if(!ValidateConfiguration(out error))return false;if(random==null)RestartSchedule();
        double total=0;foreach(var s in sizes)if(s!=null&&s.weight>0)total+=s.weight;
        double value=random.NextDouble()*total;
        foreach(var s in sizes)if(s!=null&&s.weight>0){value-=s.weight;if(value<=0){scale=s.scale;return true;}}
        error="Could not select a weighted size";return false;
    }
    bool Allowed(Vector3 point,ModularTitanBody ignore=null)
    {
        if(useCustomBounds&&!spawnBounds.Contains(point))return false;
        float d=Vector3.ProjectOnPlane(point-sceneManager.player.transform.position,Vector3.up).magnitude;
        return d>=minimumPlayerDistance && (maximumPlayerDistance<=0||d<=maximumPlayerDistance) &&
            !sceneManager.HasLivingActorWithin(point,minimumTitanSeparation,ignore);
    }
    public bool TryQueueSpawn(out string error)
    {
        if(!initialized)RestartSchedule();
        if(!ValidateConfiguration(out error)){LastError=error;return false;}
        if(HasPendingSpawn){error="One strike is already preparing a Titan";return false;}
        if(Population>=maximumAlive){error="Population cap reached";return false;}
        if(!cached){if(!sampler.Build(sceneManager.navigation,out error)){LastError=error;return false;}cached=true;}
        float size;if(!TryChooseScale(out size,out error)){LastError=error;return false;}
        string last="No candidate inside the distance/bounds/separation limits";
        for(int i=0;i<candidateAttempts;i++)
        {
            Vector3 point;if(!sampler.TrySample(random,out point)||!Allowed(point))continue;
            ModularTitanBody body;
            if(!sceneManager.TryPrepareSpawn(titanPrefab,point,Quaternion.Euler(0,(float)random.NextDouble()*360,0),size,out body,out last))continue;
            if(!Allowed(body.transform.position,body)){sceneManager.Despawn(body);continue;}
            pending=body;due=Time.time+warningDuration;
            if(effectsEnabled || spawnSound!=null)
            {
                pendingEffect=GetEffect();
                if(effectsEnabled)pendingEffect.Warn(body.transform.position,size,random.Next());
                else pendingEffect.transform.position=body.transform.position;
            }
            LastError=error=null;return true;
        }
        LastError=error="No safe random spawn after "+candidateAttempts+" attempts: "+last;return false;
    }
    TitanSpawnStrikeEffect GetEffect()
    {
        int limit=Mathf.Clamp(effectPoolSize,1,8);
        while(pool.Count>limit){var last=pool[pool.Count-1];last.StopEffect();Destroy(last.gameObject);pool.RemoveAt(pool.Count-1);}
        if(pool.Count<limit)
        {
            var go=new GameObject("[Titan spawn FX] Strike "+pool.Count);go.hideFlags=HideFlags.DontSave;go.transform.SetParent(transform,false);
            var fx=go.AddComponent<TitanSpawnStrikeEffect>();fx.Configure(lightningMaterial,particleMaterial);pool.Add(fx);return fx;
        }
        var chosen=pool[poolCursor++%pool.Count];chosen.StopEffect();return chosen;
    }
    void Update()
    {
        CleanupCorpses();
        if(pending==null && pendingEffect!=null)
        {pendingEffect.StopEffect();pendingEffect=null;LastError="Prepared Titan was removed before activation";nextAttempt=Time.time+Mathf.Max(.1f,failedAttemptRetryDelay);}
        if(sceneManager==null || !sceneManager.isActiveAndEnabled || sceneManager.navigation==null || !sceneManager.navigation.IsReady)
        {CancelPending();cached=false;LastError="Waiting for scene manager/navigation";return;}
        if(HasPendingSpawn)
        {
            string error;
            if(!ValidateConfiguration(out error) || Population>maximumAlive || !Allowed(pending.transform.position,pending))
            {CancelPending();LastError=error??"Pending spawn cancelled: population or player/actor clearance changed";nextAttempt=Time.time+failedAttemptRetryDelay;return;}
            if(Time.time<due)return;
            var body=pending;
            if(!sceneManager.TryActivatePreparedSpawn(body,out error))
            {CancelPending();LastError=error;nextAttempt=Time.time+failedAttemptRetryDelay;return;}
            pending=null;owned.Add(new Entry{body=body});SpawnedCount++;initialRemaining=Mathf.Max(0,initialRemaining-1);
            if(pendingEffect!=null)
            {
                if(effectsEnabled)pendingEffect.Strike(spawnSound,soundVolume,soundMaxDistance,localFlashLight);
                else pendingEffect.PlaySound(spawnSound,soundVolume,soundMaxDistance);
                pendingEffect=null;
            }
            nextAttempt=Time.time+(initialRemaining>0?0:Mathf.Lerp(minimumSpawnInterval,maximumSpawnInterval,(float)random.NextDouble()));return;
        }
        if(!automaticSpawning || Time.time<nextAttempt)return;
        if(Population>=maximumAlive){initialRemaining=0;nextAttempt=Time.time+minimumSpawnInterval;return;}
        string reason;if(!TryQueueSpawn(out reason))nextAttempt=Time.time+Mathf.Max(.1f,failedAttemptRetryDelay);
    }
    public void CancelPending()
    {
        if(pending!=null){if(sceneManager!=null)sceneManager.Despawn(pending);pending=null;CancelledCount++;}
        if(pendingEffect!=null){pendingEffect.StopEffect();pendingEffect=null;}
    }
    void CleanupCorpses()
    {
        int corpses=0;
        for(int i=owned.Count-1;i>=0;i--)
        {
            var e=owned[i];if(e.body==null){owned.RemoveAt(i);continue;}
            if(!e.body.IsDead)continue;
            if(e.death<0)e.death=Time.time;
            if(corpseLifetime>0 && Time.time-e.death>=corpseLifetime){if(sceneManager!=null)sceneManager.Despawn(e.body);owned.RemoveAt(i);}else corpses++;
        }
        while(corpses>Mathf.Max(0,maximumRetainedCorpses))
        {
            int oldest=-1;float age=float.MaxValue;
            for(int i=0;i<owned.Count;i++)if(owned[i].body!=null&&owned[i].body.IsDead&&owned[i].death<age){oldest=i;age=owned[i].death;}
            if(oldest<0)break;if(sceneManager!=null)sceneManager.Despawn(owned[oldest].body);owned.RemoveAt(oldest);corpses--;
        }
    }
    public void DespawnOwned()
    {
        CancelPending();foreach(var e in owned)if(e.body!=null&&sceneManager!=null)sceneManager.Despawn(e.body);owned.Clear();
    }
    void DestroyEffects(){foreach(var fx in pool)if(fx!=null)Destroy(fx.gameObject);pool.Clear();poolCursor=0;pendingEffect=null;}
    void OnDrawGizmosSelected()
    {
        if(useCustomBounds){Gizmos.color=Color.cyan;Gizmos.DrawWireCube(spawnBounds.center,spawnBounds.size);}
        if(sceneManager!=null&&sceneManager.player!=null){Gizmos.color=new Color(1,.7f,.1f);Gizmos.DrawWireSphere(sceneManager.player.transform.position,minimumPlayerDistance);}
    }
}
