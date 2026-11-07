using UnityEngine;

/// <summary>Personal airflow presentation only; never changes flight or mixer preferences.</summary>
[CreateAssetMenu(menuName="ODM/Player wind audio")]
public sealed class PlayerWindAudioPalette : ScriptableObject
{
    public AudioClip loop;
    [Range(0,1)] public float maximumVolume=.35f;
    [Min(0)] public float minimumSpeed=8;
    [Min(.1f)] public float fullVolumeSpeed=55;
    [Min(.01f)] public float fadeInDuration=.35f,fadeOutDuration=.25f;
    [Range(.5f,1.5f)] public float minimumPitch=.95f,maximumPitch=1.08f;
    [Tooltip("Reject implausible position jumps for audio only. Does not cap player velocity.")]
    [Min(1)] public float maximumTrustedSpeed=160;
}
