using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bounded current-pose release of the configured controller AND attached
/// solid gear shapes. Native world-up CCT plus oriented gear primitives; never widens capture.
/// Unknown solids and saturated queries fail closed. Caller keeps motor suspended.</summary>
public sealed class TitanReleaseClearance
{
    readonly Transform root;
    readonly Collider[] shapes;
    readonly Collider[] overlaps=new Collider[64];
    readonly RaycastHit[] hits=new RaycastHit[64];
    public TitanReleaseClearance(Transform root,Collider[] shapes) { this.root=root;this.shapes=shapes; }
    public static bool Supported(Collider c) { return c is CharacterController || c is BoxCollider || c is SphereCollider || c is CapsuleCollider; }
    public bool TryFindExit(Vector3 ownerPosition,Transform owner,out Vector3 position)
    {
        position=root.position;
        if(!Clear(Vector3.zero,owner)) return false;
        Vector3 away=Vector3.ProjectOnPlane(position-ownerPosition,Vector3.up).normalized;
        if(away.sqrMagnitude<.5f) away=Vector3.forward;
        for(int lift=0;lift<3;lift++) for(int step=0;step<7;step++)
        {
            Vector3 delta=away*(step*.8f)+Vector3.up*(lift*.8f);
            if(Clear(delta,null) && ExitSweep(delta,owner)) { position=root.position+delta;return true; }
        }
        return false;
    }
    // Standing up at the physical ragdoll's supported location: no relocation
    // search through walls. Test every configured solid, ignoring only that copy.
    public bool CanStandAt(Vector3 point,Transform ragdoll)
    {return Clear(point-root.position,ragdoll);}
    bool Ignore(Transform t,Transform owner) { return t.IsChildOf(root) || (owner!=null && t.IsChildOf(owner)); }
    static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)); }
    void Capsule(Collider c,Vector3 delta,out Vector3 a,out Vector3 b,out float r)
    {
        var cc=c as CharacterController;var cap=c as CapsuleCollider;
        Vector3 scale=Abs(c.transform.lossyScale),center;float height;int axis;
        if(cc!=null) {center=cc.center;r=cc.radius;height=cc.height;axis=1;}
        else {center=cap.center;r=cap.radius;height=cap.height;axis=cap.direction;}
        r*=Mathf.Max(scale[(axis+1)%3],scale[(axis+2)%3]);
        float half=Mathf.Max(0,height*scale[axis]*.5f-r);
        Vector3 direction=Vector3.zero;direction[axis]=1;
        // Native CCT stays upright; real CapsuleColliders follow their transform.
        direction=(cc!=null?Vector3.up:c.transform.TransformDirection(direction))*half;
        center=c.transform.TransformPoint(center)+delta;a=center+direction;b=center-direction;
    }
    bool Clear(Vector3 delta,Transform owner)
    {
        PhysicsScene scene=root.gameObject.scene.GetPhysicsScene();
        foreach(var c in shapes)
        {
            if(c==null || !Supported(c)) return false;
            int n;var box=c as BoxCollider;var sphere=c as SphereCollider;
            if(box!=null) n=scene.OverlapBox(box.transform.TransformPoint(box.center)+delta,
                Vector3.Scale(box.size,Abs(box.transform.lossyScale))*.5f,overlaps,box.transform.rotation,~0,QueryTriggerInteraction.Ignore);
            else if(sphere!=null) {Vector3 s=Abs(sphere.transform.lossyScale);n=scene.OverlapSphere(sphere.transform.TransformPoint(sphere.center)+delta,
                sphere.radius*Mathf.Max(s.x,s.y,s.z),overlaps,~0,QueryTriggerInteraction.Ignore);}
            else {Vector3 a,b;float r;Capsule(c,delta,out a,out b,out r);n=scene.OverlapCapsule(a,b,r,overlaps,~0,QueryTriggerInteraction.Ignore);}
            if(n==overlaps.Length) return false;
            for(int i=0;i<n;i++) if(!Ignore(overlaps[i].transform,owner)) return false;
        }
        return true;
    }
    bool ExitSweep(Vector3 delta,Transform owner)
    {
        if(delta.sqrMagnitude<.00001f) return true;
        PhysicsScene scene=root.gameObject.scene.GetPhysicsScene();
        foreach(var c in shapes)
        {
            int n;var box=c as BoxCollider;var sphere=c as SphereCollider;
            if(box!=null) n=scene.BoxCast(box.transform.TransformPoint(box.center),Vector3.Scale(box.size,Abs(box.transform.lossyScale))*.5f,
                delta.normalized,hits,box.transform.rotation,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            else if(sphere!=null) {Vector3 s=Abs(sphere.transform.lossyScale);n=scene.SphereCast(sphere.transform.TransformPoint(sphere.center),
                sphere.radius*Mathf.Max(s.x,s.y,s.z),delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);}
            else {Vector3 a,b;float r;Capsule(c,Vector3.zero,out a,out b,out r);n=scene.CapsuleCast(a,b,r,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);}
            if(n==hits.Length) return false;
            for(int i=0;i<n;i++) if(!Ignore(hits[i].transform,owner)) return false;
        }
        return true;
    }
}
