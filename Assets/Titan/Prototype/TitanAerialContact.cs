using UnityEngine;

/// <summary>Bounded continuous contact against the ACTUAL physical hand box and
/// configured upright human capsule. Rotating hand, translation-relative target;
/// capsule tilt changes are rejected by the caller. No expanded catch radius.</summary>
public static class TitanAerialContact
{
    public struct HandPose { public Vector3 center,half; public Quaternion rotation; }
    [System.ThreadStatic] static float[] cuts;
    public static bool HandBox(Transform hand,out HandPose pose)
    {
        pose=new HandPose();if(hand==null) return false;
        foreach(var box in hand.GetComponentsInChildren<BoxCollider>())
        {
            if(!box.enabled || box.isTrigger || box.attachedRigidbody==null || box.attachedRigidbody.transform!=hand) continue;
            Vector3 size=Vector3.Scale(box.size,box.transform.lossyScale);
            pose.center=box.transform.TransformPoint(box.center);pose.rotation=box.transform.rotation;
            pose.half=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z))*.5f;
            return Mathf.Min(pose.half.x,pose.half.y,pose.half.z)>.01f;
        }
        return false;
    }
    public static bool HandSphere(Transform hand,out Vector3 center,out float radius)
    {
        HandPose pose;bool found=HandBox(hand,out pose);center=pose.center;
        radius=Mathf.Min(pose.half.x,pose.half.y,pose.half.z);return found;
    }
    // Exact distance between a line segment and an oriented box. Squared distance
    // is piecewise quadratic; split at the six face crossings and solve each piece.
    static float SegmentBox(Vector3 a,Vector3 b,Vector3 half,out Vector3 closest)
    {
        if(cuts==null) cuts=new float[8];int count=2;cuts[0]=0;cuts[1]=1;
        Vector3 delta=b-a;
        for(int axis=0;axis<3;axis++) if(Mathf.Abs(delta[axis])>.0000001f)
        {
            float lo=(-half[axis]-a[axis])/delta[axis],hi=(half[axis]-a[axis])/delta[axis];
            if(lo>0 && lo<1) cuts[count++]=lo;if(hi>0 && hi<1) cuts[count++]=hi;
        }
        System.Array.Sort(cuts,0,count);float best=float.PositiveInfinity;closest=Vector3.zero;
        for(int i=0;i<count;i++)
        {
            float t=cuts[i];
            if(i<count-1)
            {
                float mid=(cuts[i]+cuts[i+1])*.5f,numerator=0,denominator=0;
                for(int axis=0;axis<3;axis++)
                {
                    float value=a[axis]+delta[axis]*mid;
                    if(value>=-half[axis] && value<=half[axis]) continue;
                    float edge=value<0?-half[axis]:half[axis];
                    numerator+=delta[axis]*(a[axis]-edge);denominator+=delta[axis]*delta[axis];
                }
                if(denominator>.00000001f) t=Mathf.Clamp(-numerator/denominator,cuts[i],cuts[i+1]);
            }
            Vector3 p=a+delta*t,q=new Vector3(Mathf.Clamp(p.x,-half.x,half.x),Mathf.Clamp(p.y,-half.y,half.y),Mathf.Clamp(p.z,-half.z,half.z));
            float distance=(p-q).sqrMagnitude;
            if(distance<best) {best=distance;closest=q;}
        }
        return Mathf.Sqrt(best);
    }
    public static float CapsuleGap(CharacterController target,HandPose hand,Vector3 root,out Vector3 contact)
    {
        Vector3 scale=target.transform.lossyScale;
        float radius=target.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
        float half=Mathf.Max(0,target.height*Mathf.Abs(scale.y)*.5f-radius);
        Vector3 center=root+target.transform.TransformVector(target.center),axis=target.transform.up*half;
        Quaternion inverse=Quaternion.Inverse(hand.rotation);Vector3 local;
        float distance=SegmentBox(inverse*(center+axis-hand.center),inverse*(center-axis-hand.center),hand.half,out local);
        contact=hand.center+hand.rotation*local;return distance-radius;
    }
    public static bool SweepHand(CharacterController target,HandPose from,HandPose to,Vector3 victimFrom,Vector3 victimTo,out float fraction)
    {
        fraction=0;
        float bound=((to.center-from.center)-(victimTo-victimFrom)).magnitude+
            Quaternion.Angle(from.rotation,to.rotation)*Mathf.Deg2Rad*Mathf.Max(from.half.magnitude,to.half.magnitude);
        for(int i=0;i<160;i++)
        {
            HandPose pose=new HandPose {center=Vector3.Lerp(from.center,to.center,fraction),half=from.half,
                rotation=Quaternion.Slerp(from.rotation,to.rotation,fraction)};
            Vector3 contact;float gap=CapsuleGap(target,pose,Vector3.Lerp(victimFrom,victimTo,fraction),out contact);
            if(gap<=.00001f) return true;
            if(bound<.000001f) return false;
            fraction+=Mathf.Max(gap/bound,.0000001f);
            if(fraction>1) return false;
        }
        return false; // bounded conservative advancement fails closed
    }
    /// <summary>Translation-relative sphere/convex sweep, also useful for regressions
    /// where both observed endpoints miss. Not the hand's capture geometry.</summary>
    public static bool Sweep(Collider shape,Vector3 handFrom,Vector3 handTo,float radius,
        Vector3 victimFrom,Vector3 victimTo,Vector3 victimCurrent,out float fraction)
    {
        fraction=0;
        if(shape==null || !shape.enabled || shape.isTrigger || !shape.gameObject.activeInHierarchy ||
            !(shape is BoxCollider || shape is SphereCollider || shape is CapsuleCollider || shape is CharacterController)) return false;
        Vector3 from=handFrom+victimCurrent-victimFrom,delta=(handTo+victimCurrent-victimTo)-from;
        float length=delta.magnitude;
        for(int i=0;i<80;i++)
        {
            Vector3 point=from+delta*fraction;
            float gap=Vector3.Distance(point,TitanCombatTarget.ClosestCombatPoint(shape,point))-radius;
            if(gap<=.00001f) return true;
            if(length<.000001f) return false;
            fraction+=Mathf.Max(gap/length,.0000001f);if(fraction>1) return false;
        }
        return false;
    }
}
