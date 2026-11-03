using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit, undoable installation into the user-requested scene only.
/// Does not overwrite shared player/body/old Titan prefabs or unrelated scenes.</summary>
public static class TitanShiganshinaInstaller
{
    public const string ScenePath="Assets/Scenes/FreeRoam (Shiganshina.unity";
    [MenuItem("Tools/Titans/Integrate into open Shiganshina scene")]
    public static void Install()
    {
        Scene scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=ScenePath || scene.isDirty) throw new InvalidOperationException("Open the clean Shiganshina scene in Edit Mode first.");
        foreach(var r in scene.GetRootGameObjects()) if(r.GetComponent<TitanShiganshinaIntegration>()!=null) throw new InvalidOperationException("Already installed; inspect existing integration instead of duplicating.");
        Movement player=null;foreach(var r in scene.GetRootGameObjects()) {var p=r.GetComponentInChildren<Movement>(true);if(p!=null) {if(player!=null) throw new InvalidOperationException("Ambiguous player");player=p;}}
        if(player==null) throw new InvalidOperationException("No real ODM player found");
        var gear=player.GetComponent<ODM_Movement>();var attack=player.GetComponent<Attack>();
        if(player.controller==null || player.cam==null || player.groundCheck==null || gear==null || gear.controller==null ||
            gear.camTransform==null || gear.camAnim==null || gear.gasParticle==null || attack==null || attack.armatureAnimator==null)
            throw new InvalidOperationException("Existing player references must be valid before installation");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
        if(prefab==null) throw new InvalidOperationException("Missing approved body prefab");
        string backup="Library/TitanIntegrationBackup/FreeRoam-Shiganshina.before.unity";Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if(!File.Exists(backup)) File.Copy(ScenePath,backup);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Integrate modular Titans into Shiganshina");
        try
        {
            var removed=new List<GameObject>();
            foreach(var r in scene.GetRootGameObjects())
            {
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r);
                if((r.name=="Titan" && path=="Assets/Prefabs/Titans/Titan.prefab") ||
                   (r.name=="TitanV2" && path=="Assets/Prefabs/Titans/TitanV2.prefab")) removed.Add(r);
            }
            foreach(var r in removed) Undo.DestroyObjectImmediate(r);
            Undo.AddComponent<TitanODMHealthSource>(player.gameObject);
            Undo.AddComponent<TitanCombatTarget>(player.gameObject);
            Undo.AddComponent<TitanODMMotor>(player.gameObject);
            if(attack.damageDealer==null)
            {
                var window=new GameObject("Titan sword damage window");Undo.RegisterCreatedObjectUndo(window,"Sword damage window");window.transform.SetParent(player.transform,false);
                var dealer=Undo.AddComponent<DamageDealer>(window);var sweep=Undo.AddComponent<TitanODMSwordSweep>(window);
                var meshes=new List<Transform>();foreach(var m in player.GetComponentsInChildren<MeshFilter>(true)) if(m.name=="obj_48459721") meshes.Add(m.transform);
                if(meshes.Count!=2) throw new InvalidOperationException("Expected the two existing sword meshes");
                sweep.bladeMeshes=meshes.ToArray();window.SetActive(false);
                Undo.RecordObject(attack,"Connect real swords");attack.damageDealer=dealer;PrefabUtility.RecordPrefabInstancePropertyModifications(attack);
            }
            var setupGO=new GameObject("Modular Titan scene integration");Undo.RegisterCreatedObjectUndo(setupGO,"Scene integration");
            var setup=Undo.AddComponent<TitanShiganshinaIntegration>(setupGO);setup.player=player;
            var nav=Undo.AddComponent<TitanNavigationSurface>(setupGO);nav.localBakeSize=256;setup.navigation=nav;
            var sources=new List<BoxCollider>();foreach(var r in scene.GetRootGameObjects()) if(r.name=="Map Control" || r.name=="Cube")
                foreach(var c in r.GetComponentsInChildren<BoxCollider>()) if(c.enabled && !c.isTrigger) sources.Add(c);
            nav.geometry=sources.ToArray();if(sources.Count==0) throw new InvalidOperationException("No explicit level collision sources");
            var bodies=new List<ModularTitanBody>();
            foreach(var point in new[]{new Vector3(-12,-1.84f,18),new Vector3(20,-1.84f,25)})
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);Undo.RegisterCreatedObjectUndo(go,"Place approved Titan");
                go.name="Modular Titan "+(bodies.Count+1);go.transform.position=point;
                go.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position-point,Vector3.up));
                foreach(var c in go.GetComponentsInChildren<Collider>(true))
                { Undo.RecordObject(c.gameObject,"Allow existing ODM grapple layer");c.gameObject.layer=9;PrefabUtility.RecordPrefabInstancePropertyModifications(c.gameObject); }
                bodies.Add(go.GetComponent<ModularTitanBody>());
            }
            setup.titans=bodies.ToArray();EditorSceneManager.MarkSceneDirty(scene);Undo.CollapseUndoOperations(group);
        }
        catch { Undo.RevertAllDownToGroup(group);throw; }
    }
}
