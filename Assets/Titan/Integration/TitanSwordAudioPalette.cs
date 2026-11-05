using UnityEngine;

/// <summary>One small resource reference asset keeps user-supplied extra sounds
/// available in existing saved scenes without rewriting those scenes.</summary>
[CreateAssetMenu(menuName="ODM/Sword finisher audio")]
public sealed class TitanSwordAudioPalette : ScriptableObject
{
    public AudioClip[] finisherSounds=new AudioClip[0];
    [Range(0,1)] public float volume=.65f;
}
