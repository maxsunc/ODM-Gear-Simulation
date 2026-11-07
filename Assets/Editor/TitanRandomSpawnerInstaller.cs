#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit scene-only installation; no bake, scene reload/save, changes
/// to existing Titans, or scripted Play input. Save-as-copy protects current edits.</summary>
public static class TitanRandomSpawnerInstaller
{
    [MenuItem("Tools/Titans/FreeRoam/Install random Titan spawner")]
    public static void Install()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/Scenes/FreeRoam.unity")throw new InvalidOperationException("Open FreeRoam in Edit Mode first");
        TitanFreeRoamIntegration manager=null;int activePlaced=0;
        foreach(var root in scene.GetRootGameObjects())
        {
            foreach(var s in root.GetComponentsInChildren<TitanRandomSpawner>(true))throw new InvalidOperationException("A random Titan spawner is already installed; select "+s.name+" to configure it");
            foreach(var b in root.GetComponentsInChildren<ModularTitanBody>(true))if(b.gameObject.activeInHierarchy)activePlaced++;
            foreach(var m in root.GetComponentsInChildren<TitanFreeRoamIntegration>(true)) {if(manager!=null)throw new InvalidOperationException("Ambiguous scene manager");manager=m;}
        }
        if(manager==null || manager.navigation==null || manager.player==null)throw new InvalidOperationException("Install FreeRoam navigation/player integration first");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
        var lightning=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSpawnLightning.mat");
        var particles=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/TitanSwordImpact.mat");
        if(prefab==null || lightning==null || particles==null)throw new InvalidOperationException("Wait for Titan prefab/strike effect assets to import");
        System.IO.Directory.CreateDirectory("Library/TitanSpawnerBackup");
        string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        if(!EditorSceneManager.SaveScene(scene,"Library/TitanSpawnerBackup/FreeRoam.before-"+stamp+".unity",true))throw new InvalidOperationException("Could not copy-backup current scene");
        int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install random Titan spawner");
        var go=new GameObject("Titan random spawner");Undo.RegisterCreatedObjectUndo(go,"Create Titan random spawner");
        var spawner=Undo.AddComponent<TitanRandomSpawner>(go);spawner.sceneManager=manager;spawner.titanPrefab=prefab;
        spawner.lightningMaterial=lightning;spawner.particleMaterial=particles;
        spawner.maximumAlive=Mathf.Max(6,activePlaced+spawner.initialSpawnCount);
        // Default to the profiles ALREADY baked. Do not surprise-bake new sizes.
        var choices=new System.Collections.Generic.List<TitanRandomSpawner.SizeChoice>();
        foreach(var p in manager.navigation.profiles)if(p!=null&&p.data!=null)choices.Add(new TitanRandomSpawner.SizeChoice{scale=p.scale,weight=1});
        if(choices.Count>0)spawner.sizes=choices.ToArray();
        EditorUtility.SetDirty(spawner);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=go;Undo.CollapseUndoOperations(group);
        EditorSceneManager.SaveScene(scene,"Library/TitanSpawnerBackup/FreeRoam.after-"+stamp+".unity",true);
        Debug.Log("Random Titan spawner installed. Select it to set sizes/weights, cap, timing and Spawn Sound. No sound assigned; no bake performed. Scene left unsaved.",spawner);
    }
}

[CustomEditor(typeof(TitanRandomSpawner))]
public sealed class TitanRandomSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var s=(TitanRandomSpawner)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Sizes are weighted scale categories (Titan height is about 12m x scale). Unbaked sizes are skipped, not allowed to block other spawns. Positive-weight sizes join the next scene navigation bake. Bake once after adding sizes, never per spawn. Spawn Sound is optional: drag your own AudioClip into that field.",MessageType.Info);
        if(!EditorApplication.isPlaying)return;
        EditorGUILayout.LabelField("Spawned / owned alive",s.SpawnedCount+" / "+s.OwnedAliveCount);
        EditorGUILayout.LabelField("Preparing strike",s.HasPendingSpawn?"Yes":"No");
        if(!string.IsNullOrEmpty(s.SizeWarning))EditorGUILayout.HelpBox(s.SizeWarning,MessageType.Warning);
        if(!string.IsNullOrEmpty(s.LastError))EditorGUILayout.HelpBox(s.LastError,MessageType.Warning);
        using(new EditorGUI.DisabledScope(!s.isActiveAndEnabled))
        {
            if(GUILayout.Button("Spawn one (respects cap and safety)")){string error;if(!s.TryQueueSpawn(out error))Debug.LogWarning(error,s);}
            if(GUILayout.Button("Cancel preparing strike"))s.CancelPending();
            if(GUILayout.Button("Despawn this spawner's Titans"))s.DespawnOwned();
        }
    }
}
#endif
