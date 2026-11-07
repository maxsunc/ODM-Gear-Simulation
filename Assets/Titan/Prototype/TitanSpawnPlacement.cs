using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Read-only, bounded spawn queries against existing data and real solids.
/// Never bakes, resizes hitboxes, teleports to distant ground, or ignores other actors.</summary>
public static class TitanSpawnPlacement
{
    public static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    public static bool Finite(Vector3 v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z); }
    static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)); }
    public static bool TryFind(TitanWorldNavigation navigation, Scene scene, Vector3 requested,
        Quaternion rotation, Vector3 scale, float projectionDistance, Transform ignore,
        out Vector3 position, out TitanWorldNavigation.Profile profile, out string error)
    {
        position=requested;profile=null;error=null;
        if(navigation==null || !navigation.isActiveAndEnabled || !navigation.IsReady || navigation.gameObject.scene!=scene)
        {error="Scene navigation is not registered";return false;}
        if(!Finite(requested)||!Finite(scale)||scale.x<=0||Mathf.Abs(scale.x-scale.y)>.001f||Mathf.Abs(scale.x-scale.z)>.001f)
        {error="Titan needs a finite positive uniform scale";return false;}
        if(!Finite(rotation.x)||!Finite(rotation.y)||!Finite(rotation.z)||!Finite(rotation.w) ||
            Mathf.Abs(rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w-1)>.001f ||
            Vector3.Dot(rotation*Vector3.up,Vector3.up)<.999f)
        {error="Titan spawn rotation must be an upright yaw";return false;}
        profile=navigation.ForScale(scale.x);
        if(profile==null) {error="No baked Titan navigation profile for scale "+scale.x+"; add/bake that size once, not per Titan";return false;}
        if(!Finite(projectionDistance)||projectionDistance<=0 || !Finite(profile.radius)||!Finite(profile.height)||profile.radius<=0||profile.height<profile.radius*2)
        {error="Invalid spawn projection distance or navigation dimensions";return false;}
        NavMeshHit hit;var filter=new NavMeshQueryFilter {agentTypeID=profile.agentTypeID,areaMask=NavMesh.AllAreas};
        if(!NavMesh.SamplePosition(requested,out hit,projectionDistance,filter))
        {error="No ground on this size's existing NavMesh within "+projectionDistance+"m; move the spawn to clear ground (no rebake needed)";return false;}
        position=hit.position;
        // Keep the foot plane above the sampled surface, without shrinking the
        // navigation clearance. The tiny inset avoids counting mere ground contact.
        float r=Mathf.Max(.001f,profile.radius-.01f);
        var a=position+Vector3.up*(profile.radius+.02f);
        var b=position+Vector3.up*(profile.height-profile.radius+.02f);
        var hits=new Collider[64];int count=scene.GetPhysicsScene().OverlapCapsule(a,b,r,hits,~0,QueryTriggerInteraction.Ignore);
        return Clear(hits,count,ignore,out error);
    }
    static bool Clear(Collider[] hits,int count,Transform ignore,out string error)
    {
        error=null;
        if(count==hits.Length) {error="Spawn clearance query saturated; refusing unsafe placement";return false;}
        for(int i=0;i<count;i++)
        {
            if(ignore!=null && hits[i].transform.IsChildOf(ignore))continue;
            error="Spawn blocked by solid "+hits[i].name;return false;
        }
        return true;
    }
    public static Bounds RigBounds(ModularTitanBody body)
    {
        var bounds=new Bounds(body.transform.position,Vector3.zero);
        foreach(var entry in body.ragdollBones)
        {
            var c=entry.collider;if(c==null || c.isTrigger || !c.enabled)continue;
            var box=c as BoxCollider;var cap=c as CapsuleCollider;var sphere=c as SphereCollider;
            if(box!=null)
            {
                for(int i=0;i<8;i++)bounds.Encapsulate(box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            }
            else
            {
                Vector3 scale=Abs(c.transform.lossyScale);float r;Vector3 center,extent;
                if(cap!=null)
                {
                    int a=cap.direction;r=cap.radius*Mathf.Max(scale[(a+1)%3],scale[(a+2)%3]);
                    Vector3 axis=Vector3.zero;axis[a]=1;axis=c.transform.TransformDirection(axis)*Mathf.Max(0,cap.height*scale[a]*.5f-r);
                    center=c.transform.TransformPoint(cap.center);extent=Abs(axis)+Vector3.one*r;
                }
                else if(sphere!=null) {r=sphere.radius*Mathf.Max(scale.x,scale.y,scale.z);center=c.transform.TransformPoint(sphere.center);extent=Vector3.one*r;}
                else {bounds.Encapsulate(c.bounds);continue;}
                bounds.Encapsulate(center-extent);bounds.Encapsulate(center+extent);
            }
        }
        return bounds;
    }
    public static bool BodyIsClear(ModularTitanBody body,Vector3 position,out string error)
    {
        var scene=body.gameObject.scene.GetPhysicsScene();var hits=new Collider[64];Vector3 delta=position-body.transform.position;
        foreach(var entry in body.ragdollBones)
        {
            var c=entry.collider;
            if(c==null || !c.enabled || c.isTrigger)continue;
            var box=c as BoxCollider;var capsule=c as CapsuleCollider;var sphere=c as SphereCollider;
            Vector3 scale=Abs(c.transform.lossyScale);int count;
            if(box!=null)
            {
                Vector3 h=Vector3.Scale(box.size,scale)*.5f-Vector3.one*.01f;
                h=new Vector3(Mathf.Max(.001f,h.x),Mathf.Max(.001f,h.y),Mathf.Max(.001f,h.z));
                count=scene.OverlapBox(c.transform.TransformPoint(box.center)+delta,h,hits,c.transform.rotation,~0,QueryTriggerInteraction.Ignore);
            }
            else if(capsule!=null)
            {
                int axis=capsule.direction;float radius=capsule.radius*Mathf.Max(scale[(axis+1)%3],scale[(axis+2)%3]);
                float half=Mathf.Max(0,capsule.height*scale[axis]*.5f-radius);Vector3 dir=Vector3.zero;dir[axis]=1;
                dir=c.transform.TransformDirection(dir)*half;Vector3 center=c.transform.TransformPoint(capsule.center)+delta;
                count=scene.OverlapCapsule(center-dir,center+dir,Mathf.Max(.001f,radius-.01f),hits,~0,QueryTriggerInteraction.Ignore);
            }
            else if(sphere!=null)count=scene.OverlapSphere(c.transform.TransformPoint(sphere.center)+delta,
                Mathf.Max(.001f,sphere.radius*Mathf.Max(scale.x,scale.y,scale.z)-.01f),hits,~0,QueryTriggerInteraction.Ignore);
            else {error="Unsupported solid collider in Titan spawn rig: "+c.name;return false;}
            if(!Clear(hits,count,body.transform,out error))return false;
        }
        error=null;return true;
    }
}
