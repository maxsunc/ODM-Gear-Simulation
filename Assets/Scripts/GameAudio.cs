using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>One persistent mixer, independent gains, no AudioSource volume/pitch rewrites.</summary>
[DisallowMultipleComponent]
public sealed class GameAudio : MonoBehaviour
{
    public const string MasterKey="ODM.Audio.Master.v1", SfxKey="ODM.Audio.SFX.v1", MusicKey="ODM.Audio.Music.v1", MusicRestoreKey="ODM.Audio.MusicRestore.v1";
    static GameAudio instance;
    static AudioMixer mixer;
    static AudioMixerGroup sfxGroup,musicGroup;
    static bool loaded,preferencesLoaded,preferencesDirty;
    static float saveAfter;
    static float master=1,sfx=1,music=1,lastMusic=1;
    float nextScan;
    public static AudioMixer Mixer { get { Load();return mixer; } }
    public static float MasterVolume {get{Preferences();return master;}}
    public static float SfxVolume {get{Preferences();return sfx;}}
    public static float MusicVolume {get{Preferences();return music;}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){instance=null;mixer=null;sfxGroup=musicGroup=null;loaded=preferencesLoaded=preferencesDirty=false;saveAfter=0;master=sfx=music=lastMusic=1;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if(instance!=null)return;
        instance=FindObjectOfType<GameAudio>();
        if(instance==null)new GameObject("Game Audio [Master / SFX / Music]").AddComponent<GameAudio>();
    }
    void Awake()
    {
        if(instance!=null && instance!=this){Destroy(this);return;}
        instance=this;DontDestroyOnLoad(gameObject);Load();Preferences();
    }
    void OnEnable(){SceneManager.sceneLoaded+=SceneLoaded;}
    void OnDisable(){SceneManager.sceneLoaded-=SceneLoaded;}
    void OnDestroy(){if(instance==this)instance=null;}
    void Start(){Apply();RouteLoadedSources();}
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.M))ToggleMusicMute();
        if(preferencesDirty && Time.unscaledTime>=saveAfter)Save();
        // Low-frequency fallback catches third-party/new prefab sources too, even paused.
        // Known runtime sources route immediately at creation, not one second later.
        if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+1;RouteLoadedSources();}
    }
    void SceneLoaded(Scene scene,LoadSceneMode mode){RouteLoadedSources();}
    void OnApplicationPause(bool paused){if(paused)Save();}
    void OnApplicationQuit(){Save();}
    static void Load()
    {
        if(loaded)return;loaded=true;mixer=Resources.Load<AudioMixer>("Audio/GameAudio");
        if(mixer==null){Debug.LogWarning("GameAudio mixer is missing; create it using Tools > Audio");return;}
        foreach(var group in mixer.FindMatchingGroups(""))
        {if(group.name=="SFX")sfxGroup=group;else if(group.name=="Music")musicGroup=group;}
    }
    static void Preferences()
    {
        if(preferencesLoaded)return;preferencesLoaded=true;
        master=Safe(PlayerPrefs.GetFloat(MasterKey,1),1);sfx=Safe(PlayerPrefs.GetFloat(SfxKey,1),1);music=Safe(PlayerPrefs.GetFloat(MusicKey,1),1);
        lastMusic=music>0?music:Safe(PlayerPrefs.GetFloat(MusicRestoreKey,1),1);if(lastMusic<=0)lastMusic=1;
    }
    static float Safe(float value,float fallback){return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp01(value);}
    public static float ToDecibels(float linear){linear=Safe(linear,0);return linear<=.0001f?-80:20*Mathf.Log10(linear);}
    public static void Apply()
    {
        Load();Preferences();if(mixer==null)return;
        mixer.SetFloat("MasterVolume",ToDecibels(master));mixer.SetFloat("SFXVolume",ToDecibels(sfx));mixer.SetFloat("MusicVolume",ToDecibels(music));
    }
    public static void SetMasterVolume(float value){Preferences();master=Safe(value,master);PlayerPrefs.SetFloat(MasterKey,master);Changed();Apply();}
    public static void SetSfxVolume(float value){Preferences();sfx=Safe(value,sfx);PlayerPrefs.SetFloat(SfxKey,sfx);Changed();Apply();}
    public static void SetMusicVolume(float value)
    {
        Preferences();music=Safe(value,music);if(music>0){lastMusic=music;PlayerPrefs.SetFloat(MusicRestoreKey,lastMusic);}PlayerPrefs.SetFloat(MusicKey,music);Changed();Apply();
    }
    public static void ToggleMusicMute(){Preferences();SetMusicVolume(music>0?0:lastMusic);}
    public static void InitializeMusicDefault(bool muted)
    {
        Preferences();if(PlayerPrefs.HasKey(MusicKey))return;music=muted?0:1;Apply();
    }
    static void Changed(){preferencesDirty=true;saveAfter=Time.unscaledTime+.25f;}
    public static void Save(){if(preferencesDirty){PlayerPrefs.Save();preferencesDirty=false;}}
    public static void Route(AudioSource source,bool isMusic=false)
    {
        if(source==null)return;Load();var group=isMusic?musicGroup:sfxGroup;if(group!=null)source.outputAudioMixerGroup=group;
    }
    public static void RouteLoadedSources()
    {
        Load();var musicSources=new HashSet<AudioSource>();
        foreach(var game in FindObjectsOfType<GameElements>(true))if(game.musics!=null)
            foreach(var source in game.musics)if(source!=null)musicSources.Add(source);
        foreach(var source in FindObjectsOfType<AudioSource>(true))
            Route(source,musicSources.Contains(source) || (musicGroup!=null && source.outputAudioMixerGroup==musicGroup));
    }
    public static void DrawMenu()
    {
        float width=Mathf.Min(320,Screen.width-20);var area=new Rect(Screen.width-width-10,Mathf.Max(10,Screen.height-220),width,210);
        GUILayout.BeginArea(area,GUI.skin.box);
        GUILayout.Label("Audio");DrawSlider("Master",MasterVolume,SetMasterVolume);DrawSlider("SFX",SfxVolume,SetSfxVolume);DrawSlider("Music",MusicVolume,SetMusicVolume);
        GUILayout.Label("M: mute / restore music");GUILayout.EndArea();
    }
    static void DrawSlider(string label,float value,System.Action<float> set)
    {
        GUILayout.Label(label+"  "+Mathf.RoundToInt(value*100)+"%");float next=GUILayout.HorizontalSlider(value,0,1);
        if(Mathf.Abs(next-value)>.00001f)set(next);
    }
}
