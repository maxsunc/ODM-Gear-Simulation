using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit disposable-worker authoring only. Never runs on import or touches a live scene.</summary>
public static class FrontendMenuBuilder
{
    public static void BuildOwned()
    {
        if (!Application.isBatchMode || Application.unityVersion != "2021.3.25f1" || !File.Exists(".titan-validation-owned"))
            throw new InvalidOperationException("Owned Unity2021.3.25f1 batch worker only");
        if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == GameMenu.ForestScenePath))
            throw new InvalidOperationException("Primary Giant Forest route must already be enabled");
        if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == GameMenu.TownScenePath))
            throw new InvalidOperationException("New Shiganshina route must already be enabled");
        const string path = "Assets/Scenes/Menu.unity";
        string guid = AssetDatabase.AssetPathToGUID(path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var previousMenu = scene.GetRootGameObjects().SingleOrDefault(g => g.GetComponent<GameMenu>() != null);
        if (previousMenu != null)
        {
            if (Environment.GetEnvironmentVariable("ODM_FRONTEND_REBUILD_OWNED") != "1" || previousMenu.name != "ODM Main Menu")
                throw new InvalidOperationException("Menu already authored; explicit owned revision required");
            UnityEngine.Object.DestroyImmediate(previousMenu); // Generated frontend only, in the disposable worker.
        }
        // Keep all old serialized callbacks and original music/camera objects as archived data.
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var oldCanvas in root.GetComponentsInChildren<Canvas>(true))
            { oldCanvas.gameObject.name = "Legacy Menu UI [archived]"; oldCanvas.gameObject.SetActive(false); }
            foreach (var oldController in root.GetComponentsInChildren<SceneManagement>(true)) oldController.enabled = false;
        }
        const string backdrop = "Assets/UI/Frontend/ForestBackdrop.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(backdrop);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048; importer.SaveAndReimport();
        const string townBackdrop = "Assets/UI/Frontend/ShiganshinaBackdrop.png";
        var townImporter = (TextureImporter)AssetImporter.GetAtPath(townBackdrop);
        townImporter.textureType = TextureImporterType.Sprite; townImporter.spriteImportMode = SpriteImportMode.Single;
        townImporter.sRGBTexture = true; townImporter.mipmapEnabled = false; townImporter.maxTextureSize = 2048; townImporter.SaveAndReimport();
        var menu = new GameObject("ODM Main Menu").AddComponent<GameMenu>();
        menu.CreateVisuals(AssetDatabase.LoadAssetAtPath<Sprite>(backdrop), AssetDatabase.LoadAssetAtPath<Sprite>(townBackdrop));
        var serializedMenu = new SerializedObject(menu);
        serializedMenu.FindProperty("buttonClick").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/uiClick.mp3");
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene, path);
        if (AssetDatabase.AssetPathToGUID(path) != guid) throw new InvalidOperationException("Menu scene GUID changed");
        Debug.Log("FRONTEND_MENU_AUTHORED guid=" + guid + " forestPath=" + GameMenu.ForestScenePath + " legacyUI=archived originalAudio=retained");
    }
}
