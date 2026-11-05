using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Explicit, create-only Unity 2021 mixer authoring. Never opens/saves scenes.</summary>
public static class GameAudioMixerBuilder
{
    public const string AssetPath="Assets/Resources/Audio/GameAudio.mixer";
    [MenuItem("Tools/Audio/Create game mixer if missing")]
    public static void Create()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Create the mixer in Edit Mode");
        if(Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Use Unity 2021.3.25f1");
        if(File.Exists(AssetPath))return; // Never replace authored mixer work.
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));AssetDatabase.Refresh();
        try
        {
        // Unity has no public mixer authoring API. Keep version-pinned reflection Editor-only.
        var controller=typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController",true);
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var mixer=(AudioMixer)controller.GetMethod("CreateMixerControllerAtPath",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{AssetPath});
        var master=controller.GetProperty("masterGroup",flags).GetValue(mixer);
        var groupType=master.GetType();var groups=Array.CreateInstance(groupType,2);
        for(int i=0;i<2;i++)
        {
            var group=controller.GetMethod("CreateNewGroup",flags).Invoke(mixer,new object[]{i==0?"SFX":"Music",false});groups.SetValue(group,i);
        }
        groupType.GetProperty("children",flags).SetValue(master,groups);
        var viewsProperty=controller.GetProperty("views",flags);var viewType=viewsProperty.PropertyType.GetElementType();
        var views=Array.CreateInstance(viewType,1);object view=Activator.CreateInstance(viewType);var guids=new GUID[3];
        for(int i=0;i<3;i++)guids[i]=(GUID)groupType.GetProperty("groupID",flags).GetValue(i==0?master:groups.GetValue(i-1));
        viewType.GetField("guids",flags).SetValue(view,guids);viewType.GetField("name",flags).SetValue(view,"All");views.SetValue(view,0);viewsProperty.SetValue(mixer,views);
        var property=controller.GetProperty("exposedParameters",flags);var parameterType=property.PropertyType.GetElementType();var parameters=Array.CreateInstance(parameterType,3);
        for(int i=0;i<3;i++)
        {
            object group=i==0?master:groups.GetValue(i-1),parameter=Activator.CreateInstance(parameterType);
            parameterType.GetField("guid",flags).SetValue(parameter,groupType.GetMethod("GetGUIDForVolume",flags).Invoke(group,null));
            parameterType.GetField("name",flags).SetValue(parameter,new[]{"MasterVolume","SFXVolume","MusicVolume"}[i]);parameters.SetValue(parameter,i);
        }
        property.SetValue(mixer,parameters);mixer.updateMode=AudioMixerUpdateMode.UnscaledTime;
        EditorUtility.SetDirty(mixer);EditorUtility.SetDirty((UnityEngine.Object)master);AssetDatabase.SaveAssetIfDirty(mixer);
        Debug.Log("GAME_AUDIO_MIXER_CREATED "+AssetPath);
        }
        catch
        {
            // Only this call's NEW asset: existing mixers were rejected above.
            AssetDatabase.DeleteAsset(AssetPath);throw;
        }
    }
    public static void CreateOwnedBatch()
    {
        if(!Application.isBatchMode || !File.Exists(".titan-validation-owned"))throw new InvalidOperationException("Owned batch worker required");
        Create();
    }
}
