using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scout uses a bounded, movement-swept slash box independent of blade
/// animation. Legacy labs retain exact blade sweeps. Only Attack's active window
/// enables this component; DamageDealer shares per-region/per-swing deduplication.</summary>
[DefaultExecutionOrder(110)]
[RequireComponent(typeof(DamageDealer))]
public class TitanODMSwordSweep : MonoBehaviour
{
    public Transform[] bladeMeshes=new Transform[0];
    [Min(.001f)] public float bladeRadius=.035f;
    [Header("Dedicated Scout slash (world metres, not Titan-scaled)")]
    public bool useDedicatedVolume;
    public Vector3 slashCenter=new Vector3(0,.15f,1.35f);
    public Vector3 slashHalfExtents=new Vector3(1.8f,1.2f,1.45f);
    public bool DedicatedVolumeActive { get; private set; }
    public int RejectedSweeps { get; private set; }
    readonly Collider[] overlaps=new Collider[64];
    readonly RaycastHit[] crossings=new RaycastHit[64],obstructions=new RaycastHit[32];
    Transform playerRoot;
    Attack attack;
    Vector3 previousRoot,center,halfExtents;
    Quaternion strikeRotation;
    DamageDealer damage;
    struct Blade { public Transform root;public Vector3 a,b,position,scale;public Quaternion rotation; }
    Blade[] blades;
    void OnEnable()
    {
        damage=GetComponent<DamageDealer>();attack=GetComponentInParent<Attack>();
        var scout=GetComponentInParent<ScoutPlayerAnimation>();
        DedicatedVolumeActive=useDedicatedVolume || (damage.speedScaledDamage && scout!=null);
        playerRoot=attack!=null?attack.transform:scout!=null?scout.transform:transform.parent!=null?transform.parent:transform;
        previousRoot=playerRoot.position;
        strikeRotation=attack!=null && attack.IsAttacking?attack.StrikeRotation:playerRoot.rotation;
        center=SafeVector(slashCenter,new Vector3(0,.15f,1.35f),new Vector3(-1,-1,0),new Vector3(1,1,2));
        halfExtents=SafeVector(slashHalfExtents,new Vector3(1.8f,1.2f,1.45f),Vector3.one*.1f,new Vector3(2.2f,1.6f,1.8f));
        blades=new Blade[bladeMeshes.Length];
        for(int i=0;i<blades.Length;i++)
        {
            var t=bladeMeshes[i];if(t==null) continue;var filter=t.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null) continue;
            Bounds bounds=filter.sharedMesh.bounds;int axis=0;
            if(bounds.size.y>bounds.size.x) axis=1;if(bounds.size.z>bounds.size[axis]) axis=2;
            Vector3 a=bounds.center,b=a;a[axis]=bounds.min[axis];b[axis]=bounds.max[axis];
            blades[i]=new Blade {root=t,a=a,b=b,position=t.position,rotation=t.rotation,scale=t.lossyScale};
        }
    }
    void LateUpdate()
    {
        if(PlayerCombatPermission.IsBlocked(this))return;
        Physics.SyncTransforms();
        if(DedicatedVolumeActive){SweepDedicatedVolume();return;}
        for(int i=0;i<blades.Length;i++)
        {
            Blade old=blades[i];if(old.root==null) continue;var t=old.root;
            float extent=Mathf.Max(Vector3.Scale(old.a,old.scale).magnitude,Vector3.Scale(old.b,old.scale).magnitude);
            float travel=Vector3.Distance(old.position,t.position)+Quaternion.Angle(old.rotation,t.rotation)*Mathf.Deg2Rad*extent;
            int steps=Mathf.Max(1,Mathf.CeilToInt(travel/.025f));
            // Teleports/shape scaling are not a sword attack across the whole map.
            if(steps<=256 && (old.scale-t.lossyScale).sqrMagnitude<.000001f)
                for(int s=0;s<=steps;s++)
                {
                    float f=s/(float)steps;Quaternion q=Quaternion.Slerp(old.rotation,t.rotation,f);
                    Vector3 p=Vector3.Lerp(old.position,t.position,f);
                    int n=gameObject.scene.GetPhysicsScene().OverlapCapsule(p+q*Vector3.Scale(old.a,old.scale),
                        p+q*Vector3.Scale(old.b,old.scale),Mathf.Max(.001f,bladeRadius),overlaps,~0,QueryTriggerInteraction.Collide);
                    if(n==overlaps.Length) continue;
                    Vector3 center=p+q*Vector3.Scale((old.a+old.b)*.5f,old.scale);
                    Vector3 slash=(t.position+t.rotation*Vector3.Scale((old.a+old.b)*.5f,old.scale))-
                        (old.position+old.rotation*Vector3.Scale((old.a+old.b)*.5f,old.scale));
                    for(int k=0;k<n;k++) if(overlaps[k]!=null && overlaps[k].GetComponent<TitanHitZone>()!=null)
                        damage.ProcessContact(overlaps[k],overlaps[k].ClosestPoint(center),slash);
                }
            old.position=t.position;old.rotation=t.rotation;old.scale=t.lossyScale;blades[i]=old;
        }
    }
    static Vector3 SafeVector(Vector3 value,Vector3 fallback,Vector3 min,Vector3 max)
    {
        for(int i=0;i<3;i++)value[i]=float.IsNaN(value[i])||float.IsInfinity(value[i])?fallback[i]:Mathf.Clamp(value[i],min[i],max[i]);return value;
    }
    void SweepDedicatedVolume()
    {
        Vector3 current=playerRoot.position,travel=current-previousRoot;
        float distance=travel.magnitude;
        if(float.IsNaN(distance)||float.IsInfinity(distance)||distance>160*Mathf.Clamp(Time.deltaTime,1f/120,.1f)+.1f)
        {previousRoot=current;RejectedSweeps++;return;}
        var physics=gameObject.scene.GetPhysicsScene();Vector3 offset=strikeRotation*center;
        int start=physics.OverlapBox(previousRoot+offset,halfExtents,overlaps,strikeRotation,~0,QueryTriggerInteraction.Collide);
        int crossed=distance>.0001f?physics.BoxCast(previousRoot+offset,halfExtents,travel/distance,crossings,strikeRotation,distance,~0,QueryTriggerInteraction.Collide):0;
        // The whole sweep fails closed on saturation, rather than accepting an
        // arbitrary subset that can omit an occluder or repeat a region.
        if(start>=overlaps.Length || crossed>=crossings.Length){previousRoot=current;RejectedSweeps++;return;}
        for(int i=0;i<start;i++)TryVolumeContact(overlaps[i],previousRoot,travel);
        for(int i=0;i<crossed;i++)
        {
            var hit=crossings[i];
            // Test reach/occlusion from the actual swept player position, NOT
            // today's endpoint (which may already be beyond a thin weakpoint).
            Vector3 origin=previousRoot+(distance>.0001f?travel/distance*Mathf.Clamp(hit.distance,0,distance):Vector3.zero);
            TryVolumeContact(hit.collider,origin,travel);
        }
        previousRoot=current;
    }
    void TryVolumeContact(Collider collider,Vector3 origin,Vector3 travel)
    {
        if(collider==null)return;var zone=collider.GetComponent<TitanHitZone>();
        if(zone==null || zone.owner==null || zone.owner.IsDead || collider.transform.IsChildOf(playerRoot))return;
        Vector3 side=origin-collider.bounds.center;
        if(zone.region==TitanHitRegion.Nape && Vector3.Dot(side,zone.transform.forward)>=0)return;
        if(zone.region==TitanHitRegion.Eyes && Vector3.Dot(side,zone.transform.forward)<=0)return;
        Vector3 point=collider.ClosestPoint(origin),delta=point-origin;float distance=delta.magnitude;
        if(distance>4.5f)return; // Never grows with target size or blade animation.
        if(distance>.001f)
        {
            int n=gameObject.scene.GetPhysicsScene().Raycast(origin,delta/distance,obstructions,distance,~0,QueryTriggerInteraction.Ignore);
            if(n>=obstructions.Length)return;
            for(int i=0;i<n;i++)
            {
                var solid=obstructions[i].collider;
                if(solid!=null && !solid.transform.IsChildOf(playerRoot) && obstructions[i].distance<distance-.015f)return;
            }
        }
        damage.ProcessContact(collider,point,travel.sqrMagnitude>.0001f?travel:strikeRotation*Vector3.right);
    }
    void OnDrawGizmosSelected()
    {
        if(!useDedicatedVolume && GetComponentInParent<ScoutPlayerAnimation>()==null)return;
        var root=playerRoot!=null?playerRoot:GetComponentInParent<Attack>()!=null?GetComponentInParent<Attack>().transform:transform;
        Gizmos.color=new Color(.1f,1,.8f,.65f);var old=Gizmos.matrix;
        Gizmos.matrix=Matrix4x4.TRS(root.position,Application.isPlaying?strikeRotation:root.rotation,Vector3.one);
        Gizmos.DrawWireCube(slashCenter,slashHalfExtents*2);Gizmos.matrix=old;
    }
}
