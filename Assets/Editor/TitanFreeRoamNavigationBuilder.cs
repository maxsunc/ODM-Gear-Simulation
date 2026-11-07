using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Explicit Editor-only full-map TEST bake. Leaves shared prefabs, mesh
/// readability/import settings, existing agent types and actor transforms unchanged.</summary>
public static class TitanFreeRoamNavigationBuilder
{
    const string ScenePath="Assets/Scenes/FreeRoam.unity";
    const string Folder="Assets/Navigation/Titans/FreeRoam";
    const string Prefix="ODM Titan FreeRoam ";
    static Scene CheckScene()
    {
        var s=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || s.path!=ScenePath || s.isDirty)
            throw new InvalidOperationException("Open/save the FreeRoam scene in Edit Mode first.");
        return s;
    }
    [MenuItem("Tools/Titans/FreeRoam/Install and bake full-map test")]
    public static void Install()
    {
        var scene=CheckScene();Movement player=null;var titans=new List<ModularTitanBody>();var roots=new List<Transform>();
        foreach(var root in scene.GetRootGameObjects())
        {
            if(root.GetComponent<TitanFreeRoamIntegration>()!=null) throw new InvalidOperationException("Already installed: use Rebake full-map navigation.");
            var p=root.GetComponentInChildren<Movement>(true);if(p!=null) { if(player!=null)throw new InvalidOperationException("Ambiguous player");player=p; }
            titans.AddRange(root.GetComponentsInChildren<ModularTitanBody>(true));
            if(root.name=="Map Control" || root.name=="Cube")roots.Add(root.transform);
        }
        if(player==null || titans.Count==0 || roots.Count==0)throw new InvalidOperationException("Assign real player, placed modular Titans and map collision roots first.");
        var gear=player.GetComponent<ODM_Movement>();var attack=player.GetComponent<Attack>();
        if(gear==null || attack==null || attack.armatureAnimator==null || player.controller==null || player.cam==null || player.groundCheck==null)
            throw new InvalidOperationException("Existing ODM references must be valid.");
        Directory.CreateDirectory("Library/TitanFullMapBackup");
        if(!File.Exists("Library/TitanFullMapBackup/FreeRoam.unity")) File.Copy(ScenePath,"Library/TitanFullMapBackup/FreeRoam.unity");
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install FreeRoam full-map navigation test");
        try
        {
            if(player.GetComponent<TitanODMHealthSource>()==null)Undo.AddComponent<TitanODMHealthSource>(player.gameObject);
            if(player.GetComponent<TitanCombatTarget>()==null)Undo.AddComponent<TitanCombatTarget>(player.gameObject);
            if(player.GetComponent<TitanODMMotor>()==null)Undo.AddComponent<TitanODMMotor>(player.gameObject);
            if(attack.damageDealer==null)
            {
                var meshes=new List<Transform>();foreach(var m in player.GetComponentsInChildren<MeshFilter>(true))if(m.name=="obj_48459721")meshes.Add(m.transform);
                if(meshes.Count!=2)throw new InvalidOperationException("Expected two existing sword meshes");
                var window=new GameObject("Titan sword damage window");Undo.RegisterCreatedObjectUndo(window,"Sword window");window.transform.SetParent(player.transform,false);
                var dealer=Undo.AddComponent<DamageDealer>(window);var sweep=Undo.AddComponent<TitanODMSwordSweep>(window);sweep.bladeMeshes=meshes.ToArray();window.SetActive(false);
                Undo.RecordObject(attack,"Connect existing swords");attack.damageDealer=dealer;PrefabUtility.RecordPrefabInstancePropertyModifications(attack);
            }
            var go=new GameObject("FreeRoam Titan navigation TEST");Undo.RegisterCreatedObjectUndo(go,"Navigation test setup");
            var nav=Undo.AddComponent<TitanWorldNavigation>(go);nav.geometryRoots=roots.ToArray();
            var setup=Undo.AddComponent<TitanFreeRoamIntegration>(go);setup.player=player;setup.titans=titans.ToArray();setup.navigation=nav;
            Bake(setup);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(group);
        }
        catch { Undo.RevertAllDownToGroup(group);throw; }
    }
    [MenuItem("Tools/Titans/FreeRoam/Rebake full-map navigation")]
    public static void Rebake()
    {
        var scene=CheckScene();TitanFreeRoamIntegration setup=null;
        foreach(var root in scene.GetRootGameObjects())if(root.GetComponent<TitanFreeRoamIntegration>()!=null)setup=root.GetComponent<TitanFreeRoamIntegration>();
        if(setup==null)throw new InvalidOperationException("Install the navigation test first");
        Bake(setup);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static void Bake(TitanFreeRoamIntegration setup)
    {
        var nav=setup.navigation;var sources=new List<NavMeshBuildSource>();Bounds bounds;string error;
        if(!TitanNavigationGeometry.Collect(setup.gameObject.scene,nav.geometryRoots,sources,out bounds,out error))throw new InvalidOperationException(error);
        // Include discovered scene actors, retain already-baked sizes, and allow
        // future spawner sizes without requiring a placeholder actor in the map.
        float[] scales;
        if(!setup.TryGetBakeScales(out scales,out error))throw new InvalidOperationException(error);
        foreach(var s in scales)bounds.Expand(new Vector3(1.8f*s*4,12*s*2,1.8f*s*4));
        EnsureFolders();var profiles=new List<TitanWorldNavigation.Profile>();var createdIDs=new List<int>();var temporary=new List<NavMeshData>();
        try
        {
            foreach(float scale in scales)
            {
                string label=scale.ToString("0.###",CultureInfo.InvariantCulture)+"x",name=Prefix+label;int id=-1;
                for(int i=0;i<NavMesh.GetSettingsCount();i++){var s=NavMesh.GetSettingsByIndex(i);if(NavMesh.GetSettingsNameFromID(s.agentTypeID)==name){id=s.agentTypeID;break;}}
                if(id==-1){id=NavMesh.CreateSettings().agentTypeID;createdIDs.Add(id);}
                ConfigureOwnedSettings(id,name,1.8f*scale,12*scale,.6f*scale);
                var settings=NavMesh.GetSettingsByID(id);settings.overrideVoxelSize=true;settings.voxelSize=settings.agentRadius/4;settings.agentSlope=40;
                var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
                if(data==null)throw new InvalidOperationException("Full-map bake failed: "+name);
                data.name=name;temporary.Add(data);
                profiles.Add(new TitanWorldNavigation.Profile {label=label,scale=scale,agentTypeID=id,radius=settings.agentRadius,height=settings.agentHeight,data=data});
            }
            foreach(var p in profiles)
            {
                string path=Folder+"/Titan_"+p.label+".asset";var old=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                if(old==null)AssetDatabase.CreateAsset(p.data,path);
                else {EditorUtility.CopySerialized(p.data,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(p.data);p.data=old;}
            }
            nav.profiles=profiles.ToArray();nav.bakedBounds=bounds;nav.sourceCount=sources.Count;nav.bakedAt=DateTime.UtcNow.ToString("O");
            EditorUtility.SetDirty(nav);AssetDatabase.SaveAssets();SaveSettings();
            Debug.Log("FreeRoam nav TEST baked "+sources.Count+" physical sources, "+profiles.Count+" size profiles, "+bounds,nav);
        }
        catch
        {
            foreach(var data in temporary)if(data!=null && !AssetDatabase.Contains(data))UnityEngine.Object.DestroyImmediate(data);
            foreach(int id in createdIDs)NavMesh.RemoveSettings(id);SaveSettings();throw;
        }
    }
    static void ConfigureOwnedSettings(int id,string name,float radius,float height,float climb)
    {
        var obj=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];var serialized=new SerializedObject(obj);
        var entries=serialized.FindProperty("m_Settings");var names=serialized.FindProperty("m_SettingNames");
        for(int i=0;i<entries.arraySize;i++)
        {
            var e=entries.GetArrayElementAtIndex(i);if(e.FindPropertyRelative("agentTypeID").intValue!=id)continue;
            names.GetArrayElementAtIndex(i).stringValue=name;
            e.FindPropertyRelative("agentRadius").floatValue=radius;e.FindPropertyRelative("agentHeight").floatValue=height;
            e.FindPropertyRelative("agentClimb").floatValue=climb;e.FindPropertyRelative("agentSlope").floatValue=40;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SaveSettings()
    {
        UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset"),"ProjectSettings/NavMeshAreas.asset",true);
    }
    static void EnsureFolders()
    {
        string parent="Assets";foreach(string part in new[]{"Navigation","Titans","FreeRoam"})
        {string next=parent+"/"+part;if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(parent,part);parent=next;}
    }
}
