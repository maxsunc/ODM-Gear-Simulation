using UnityEngine;

[CreateAssetMenu(menuName="ODM/Player impact audio and blood")]
public sealed class PlayerImpactPalette : ScriptableObject
{
    public AudioClip bloodSplat,boneCrush;
    public Material bloodMaterial;
    [Range(0,1)] public float bloodVolume=.85f,boneVolume=.8f;
}
