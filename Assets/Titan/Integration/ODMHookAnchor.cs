using UnityEngine;

/// <summary>A hook owns one actual collider-local hit, never a cached world-space
/// target or the player's parent. Animated bones and moving scenery both work.</summary>
public sealed class ODMHookAnchor
{
    Collider collider;
    Transform surface;
    Vector3 localPoint;
    ModularTitanBody titan;
    TitanHitRegion region;
    bool wasTitan,hasRegion;

    public void Clear() { collider=null;surface=null;titan=null;wasTitan=hasRegion=false; }
    public bool Bind(Collider hit,Vector3 worldPoint)
    {
        Clear();
        if(hit==null || !hit.enabled || !hit.gameObject.activeInHierarchy || !Finite(worldPoint))return false;
        collider=hit;surface=hit.transform;localPoint=surface.InverseTransformPoint(worldPoint);
        titan=hit.GetComponentInParent<ModularTitanBody>();wasTitan=titan!=null;
        if(wasTitan)
        {
            foreach(var limb in titan.limbs)
                if(limb.rootBone!=null && surface.IsChildOf(limb.rootBone))
                { region=limb.region;hasRegion=true;break; }
        }
        Vector3 point;return TryGetPoint(out point);
    }
    public bool TryGetPoint(out Vector3 point)
    {
        point=Vector3.zero;
        if(collider==null || surface==null || !collider.enabled || !collider.gameObject.activeInHierarchy)return false;
        if(wasTitan && (titan==null || !titan.isActiveAndEnabled || titan.IsDead || !surface.IsChildOf(titan.transform) || (hasRegion && titan.IsSevered(region))))return false;
        point=surface.TransformPoint(localPoint);return Finite(point);
    }
    static bool Finite(Vector3 p) { return !float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z); }
}
