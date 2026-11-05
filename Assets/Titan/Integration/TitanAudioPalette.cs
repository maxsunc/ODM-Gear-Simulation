using UnityEngine;

/// <summary>References supplied clips without changing their files or saved scenes.</summary>
[CreateAssetMenu(menuName="ODM/Titan audio")]
public sealed class TitanAudioPalette : ScriptableObject
{
    public AudioClip[] blindScreams=new AudioClip[0], footsteps=new AudioClip[0];
    public AudioClip spawnLightning, eaten;
    [Range(0,1)] public float screamVolume=.8f, footstepVolume=.75f, eatenVolume=.85f;
    public static TitanAudioPalette Load(){return Resources.Load<TitanAudioPalette>("TitanAudioPalette");}

    public static AudioSource CreateSource(string label,Transform parent,bool spatial)
    {
        var go=new GameObject("[Titan SFX] "+label);go.hideFlags=HideFlags.DontSave;
        if(parent!=null)go.transform.SetParent(parent,false);
        var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
        source.spatialBlend=spatial?1:0;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;
        source.minDistance=10;source.maxDistance=600;GameAudio.Route(source);return source;
    }
    public static AudioSource PlayEaten(GameObject player,Vector3 point)
    {
        var palette=Load();if(palette==null || palette.eaten==null)return null;
        // Independent of player/release/corpse transforms. Personal death cue is 2D.
        var source=CreateSource("Eaten",null,false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source.gameObject,player.scene);
        source.transform.position=point;source.clip=palette.eaten;source.volume=palette.eatenVolume;source.Play();
        Object.Destroy(source.gameObject,palette.eaten.length+.1f);return source;
    }
}
