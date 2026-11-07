using UnityEngine;

/// <summary>Personal Scout footsteps; presentation only, no motor or clip edits.</summary>
[CreateAssetMenu(menuName="ODM/Player footstep audio")]
public sealed class PlayerFootstepAudioPalette : ScriptableObject
{
    public AudioClip[] footsteps = new AudioClip[0];
    [Range(0, 1)] public float volume = .35f;
    [Range(.5f, 1.5f)] public float minimumPitch = .97f, maximumPitch = 1.03f;
    [Tooltip("Normalized Run phases where the protected clip's descending soles first reach the plant pad.")]
    [Range(0, 1)] public float leftPlantPhase = .9666667f, rightPlantPhase = .4666667f;
    [Tooltip("Allow a first step only when entering Run very close to a visible plant.")]
    [Range(0, .15f)] public float entryPlantWindow = .08f;
    [Min(0)] public float minimumPlanarSpeed = .6f;
    [Tooltip("Reject position jumps for audio only; this never limits gameplay velocity.")]
    [Min(1)] public float maximumTrustedSpeed = 160f;
    [Tooltip("Skip quiet pre-impact lead-ins at runtime to align the attack; original files/importers stay untouched.")]
    public bool skipLeadingSilence = true;
}
