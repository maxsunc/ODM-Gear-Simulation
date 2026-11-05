using UnityEngine;

/// <summary>The independent corpse owns its cape snapshot, not the hidden player
/// or its live simulation mesh. Destroying either cannot invalidate the other.</summary>
public sealed class ScoutCapeMeshLifetime : MonoBehaviour
{
    public Mesh mesh;
    void OnDestroy(){if(mesh!=null)Destroy(mesh);}
}
