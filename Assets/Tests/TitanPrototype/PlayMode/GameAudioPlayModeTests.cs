#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class GameAudioPlayModeTests
{
    Type audio;GameObject root;string[] keys;bool[] existed;float[] values;
    static Type Real(string n){return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).First(t=>t!=null);}
    object Call(string name,params object[] args){return audio.GetMethod(name,BindingFlags.Public|BindingFlags.Static).Invoke(null,args);}
    float Volume(string name){return (float)audio.GetProperty(name).GetValue(null);}
    [SetUp] public void Setup()
    {
        audio=Real("GameAudio");keys=new[]{"MasterKey","SfxKey","MusicKey","MusicRestoreKey"}.Select(n=>(string)audio.GetField(n).GetValue(null)).ToArray();
        existed=keys.Select(PlayerPrefs.HasKey).ToArray();values=keys.Select(k=>PlayerPrefs.GetFloat(k)).ToArray();
        root=new GameObject("Owned audio test");Call("SetMasterVolume",1f);Call("SetSfxVolume",1f);Call("SetMusicVolume",1f);
    }
    [UnityTearDown] public IEnumerator Teardown()
    {
        Object.Destroy(root);yield return null;
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetFloat(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();
        audio.GetField("preferencesLoaded",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);Call("Apply");
    }
    AudioSource Source(string name)
    {var go=new GameObject(name);go.transform.SetParent(root.transform);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;return source;}
    AudioMixer Mixer {get{return (AudioMixer)audio.GetProperty("Mixer").GetValue(null);}}
    float Gain(string name){float value;Assert.That(Mixer.GetFloat(name,out value),Is.True,"Named exposed mixer volume must exist");return value;}
    [Test] public void RealMixerContainsMusicAndSfxUnderOneMasterWithNoGainAtDefaults()
    {
        Assert.That(AssetDatabase.GetAssetPath(Mixer),Is.EqualTo("Assets/Resources/Audio/GameAudio.mixer"));
        var groups=Mixer.FindMatchingGroups("");Assert.That(groups.Select(g=>g.name),Is.EquivalentTo(new[]{"Master","SFX","Music"}));
        var master=groups.Single(g=>g.name=="Master");var data=new SerializedObject(master);var children=data.FindProperty("m_Children");
        Assert.That(children.arraySize,Is.EqualTo(2));
        Assert.That(Enumerable.Range(0,2).Select(i=>children.GetArrayElementAtIndex(i).objectReferenceValue.name),Is.EquivalentTo(new[]{"Music","SFX"}));
        foreach(string n in new[]{"MasterVolume","SFXVolume","MusicVolume"})Assert.That(Gain(n),Is.EqualTo(0).Within(.001f));
        Assert.That(Mixer.updateMode,Is.EqualTo(AudioMixerUpdateMode.UnscaledTime));
    }
    [Test] public void GainsAreIndependentMusicMuteRemembersLevelAndAuthoredSourceSettingsRemainIntact()
    {
        var source=Source("Authored SFX");source.volume=.17f;source.pitch=1.23f;source.spatialBlend=.8f;source.loop=true;Call("Route",source,false);
        Call("SetMasterVolume",.5f);Call("SetSfxVolume",.25f);Call("SetMusicVolume",.75f);
        Assert.That(Gain("MasterVolume"),Is.EqualTo(-6.0206f).Within(.001f));Assert.That(Gain("SFXVolume"),Is.EqualTo(-12.0412f).Within(.001f));
        Assert.That(Gain("MusicVolume"),Is.EqualTo(-2.4988f).Within(.001f));
        Call("ToggleMusicMute");Assert.That(Gain("MusicVolume"),Is.EqualTo(-80));Assert.That(Gain("SFXVolume"),Is.EqualTo(-12.0412f).Within(.001f));
        Call("ToggleMusicMute");Assert.That(Volume("MusicVolume"),Is.EqualTo(.75f));Call("SetSfxVolume",0f);Assert.That(Gain("SFXVolume"),Is.EqualTo(-80));Assert.That(Volume("MusicVolume"),Is.EqualTo(.75f));
        Assert.That(source.volume,Is.EqualTo(.17f));Assert.That(source.pitch,Is.EqualTo(1.23f));Assert.That(source.spatialBlend,Is.EqualTo(.8f));Assert.That(source.loop,Is.True);Assert.That(source.mute,Is.False);
    }
    [Test] public void RoutingFindsInactiveAuthoredMusicAndFallbackSfxWithoutMovingOrPlayingThem()
    {
        AudioSource music=Source("Authored music"),sfx=Source("Authored hook"),manual=Source("Manually grouped music");music.volume=.05f;sfx.volume=.03f;
        var game=root.AddComponent(Real("GameElements"));((Behaviour)game).enabled=false;game.GetType().GetField("musics").SetValue(game,new[]{music});
        Call("Route",manual,true);root.SetActive(false);Call("RouteLoadedSources");
        Assert.That(music.outputAudioMixerGroup.name,Is.EqualTo("Music"));Assert.That(sfx.outputAudioMixerGroup.name,Is.EqualTo("SFX"));Assert.That(manual.outputAudioMixerGroup.name,Is.EqualTo("Music"));
        Assert.That(music.volume,Is.EqualTo(.05f));Assert.That(sfx.volume,Is.EqualTo(.03f));Assert.That(music.isPlaying||sfx.isPlaying,Is.False);
    }
    [Test] public void ExistingMainMenuSoundtrackUsesMusicBusAndKeepsItsAuthoredPitch()
    {
        var track=Source("Existing menu soundtrack");track.volume=.1f;track.pitch=.9f;track.gameObject.AddComponent(Real("SceneManagement"));
        Assert.That(track.outputAudioMixerGroup.name,Is.EqualTo("Music"));Call("RouteLoadedSources");
        Assert.That(track.outputAudioMixerGroup.name,Is.EqualTo("Music"));Assert.That(track.volume,Is.EqualTo(.1f));Assert.That(track.pitch,Is.EqualTo(.9f));
    }
    [Test] public void PreferencesReloadAndSavedMusicChoiceOverridesLegacyInitialMute()
    {
        Call("SetMasterVolume",.6f);Call("SetSfxVolume",.3f);Call("SetMusicVolume",.4f);
        audio.GetField("preferencesLoaded",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);Call("InitializeMusicDefault",true);
        Assert.That(Volume("MasterVolume"),Is.EqualTo(.6f));Assert.That(Volume("SfxVolume"),Is.EqualTo(.3f));Assert.That(Volume("MusicVolume"),Is.EqualTo(.4f));
        Call("ToggleMusicMute");audio.GetField("preferencesLoaded",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);
        Call("ToggleMusicMute");Assert.That(Volume("MusicVolume"),Is.EqualTo(.4f),"Reloaded muted settings restore the previous music level");
        PlayerPrefs.DeleteKey(keys[2]);Call("InitializeMusicDefault",true);Assert.That(Volume("MusicVolume"),Is.Zero);
        Call("InitializeMusicDefault",false);Assert.That(Volume("MusicVolume"),Is.EqualTo(1));
        Call("SetMusicVolume",float.NaN);Call("SetMasterVolume",float.PositiveInfinity);Assert.That(Volume("MusicVolume"),Is.EqualTo(1));Assert.That(Volume("MasterVolume"),Is.EqualTo(.6f));
        Call("SetSfxVolume",5f);Assert.That(Volume("SfxVolume"),Is.EqualTo(1));Call("SetSfxVolume",-1f);Assert.That(Volume("SfxVolume"),Is.Zero);
    }
    [Test] public void NewSwordAndSpawnAudioRouteImmediatelyWithoutWaitingForFallbackScan()
    {
        var feedback=root.AddComponent(Real("TitanSwordFeedback"));
        var sword=(AudioSource)feedback.GetType().GetField("audioSource",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(feedback);
        Assert.That(sword.outputAudioMixerGroup.name,Is.EqualTo("SFX"));
        var go=new GameObject("Owned strike audio");go.transform.SetParent(root.transform);var strike=go.AddComponent(Real("TitanSpawnStrikeEffect"));
        strike.GetType().GetMethod("Configure").Invoke(strike,new object[]{null,null});Assert.That(go.GetComponent<AudioSource>().outputAudioMixerGroup.name,Is.EqualTo("SFX"));
    }
    [UnityTest] public IEnumerator DuplicateManagerCannotDestroyAnUnrelatedPlayerOrMenuObject()
    {
        var marker=root.AddComponent<MeshFilter>();root.AddComponent(audio);yield return null;
        Assert.That(root!=null && marker!=null,Is.True,"Duplicate audio manager removes only itself, never its host object");
        Assert.That(root.GetComponent(audio)==null,Is.True);
    }
    [UnityTest] public IEnumerator VolumeGainAndDebouncedSaveStillWorkWhileTheMenuPausesGameplay()
    {
        float old=Time.timeScale;
        try
        {
            Time.timeScale=0;Call("SetSfxVolume",.37f);yield return new WaitForSecondsRealtime(.35f);
            Assert.That(Volume("SfxVolume"),Is.EqualTo(.37f));Assert.That(Gain("SFXVolume"),Is.EqualTo(20*Mathf.Log10(.37f)).Within(.001f));
            Assert.That((bool)audio.GetField("preferencesDirty",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null),Is.False,"Pause must not stall settings persistence");
        }
        finally{Time.timeScale=old;}
    }
    [UnityTest] public IEnumerator PlaylistWithOneTrackDoesNotHangAndMusicStartupPreservesAuthoredVolume()
    {
        var track=Source("Only music track");track.volume=.05f;var game=root.AddComponent(Real("GameElements"));
        game.GetType().GetField("musics").SetValue(game,new[]{track});yield return null;yield return null;
        Assert.That(track.outputAudioMixerGroup.name,Is.EqualTo("Music"));Assert.That(track.volume,Is.EqualTo(.05f));Assert.That(Volume("MusicVolume"),Is.EqualTo(1));
    }
}
#endif
