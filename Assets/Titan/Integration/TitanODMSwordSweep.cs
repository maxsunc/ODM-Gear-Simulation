using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in real sword mesh sweeps. Active only during Attack's existing .6s
/// damage window. No native victim collider needed: sword counters remain active
/// while movement is held. DamageDealer owns exact per-swing region deduplication.</summary>
[DefaultExecutionOrder(110)]
[RequireComponent(typeof(DamageDealer))]
public class TitanODMSwordSweep : MonoBehaviour
{
    public Transform[] bladeMeshes=new Transform[0];
    [Min(.001f)] public float bladeRadius=.035f;
    readonly Collider[] overlaps=new Collider[64];
    DamageDealer damage;
    struct Blade { public Transform root;public Vector3 a,b,position,scale;public Quaternion rotation; }
    Blade[] blades;
    void OnEnable()
    {
        damage=GetComponent<DamageDealer>();blades=new Blade[bladeMeshes.Length];
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
        Physics.SyncTransforms();
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
                    for(int k=0;k<n;k++) if(overlaps[k]!=null && overlaps[k].GetComponent<TitanHitZone>()!=null) damage.ProcessContact(overlaps[k]);
                }
            old.position=t.position;old.rotation=t.rotation;old.scale=t.lossyScale;blades[i]=old;
        }
    }
}
