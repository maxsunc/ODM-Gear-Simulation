#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-only opt-in. Never rebuilds Scout animations or the shared Titan/player prefabs.</summary>
public static class TitanCombatEffectsInstaller
{
    [MenuItem("Tools/Titans/FreeRoam/Install sword health and impact effects")]
    public static void Install()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/Scenes/FreeRoam.unity")throw new InvalidOperationException("Open FreeRoam in Edit Mode first.");
        var players=new List<Movement>();var bodies=new List<ModularTitanBody>();var cameras=new List<Camera>();
        foreach(var root in scene.GetRootGameObjects())
        { players.AddRange(root.GetComponentsInChildren<Movement>(true));bodies.AddRange(root.GetComponentsInChildren<ModularTitanBody>(true));cameras.AddRange(root.GetComponentsInChildren<Camera>(true)); }
        if(players.Count!=1||bodies.Count==0)throw new InvalidOperationException("Expected one player and at least one modular Titan.");
        var player=players[0];var dealer=player.GetComponentInChildren<DamageDealer>(true);var sweep=player.GetComponentInChildren<TitanODMSwordSweep>(true);
        var attack=player.GetComponent<Attack>();
        if(dealer==null||sweep==null||attack==null||sweep.bladeMeshes.Length!=2)throw new InvalidOperationException("Install the real dual-blade integration first.");
        Camera camera=null;foreach(var c in cameras)if(c.enabled&&c.CompareTag("MainCamera")){if(camera!=null)throw new InvalidOperationException("Ambiguous MainCamera");camera=c;}
        if(camera==null)throw new InvalidOperationException("Missing active player MainCamera.");
        var shader=Shader.Find("ODM/Titan/Impact");if(shader==null)throw new InvalidOperationException("Impact shader has not compiled.");
        const string folder="Assets/Art/Effects";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Art","Effects");
        const string materialPath=folder+"/TitanSwordImpact.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install Titan region health and sword impact feedback");
        foreach(var body in bodies)
        {
            Undo.RecordObject(body,"Regional Titan health");body.useRegionalHealth=true;
            body.armHealth=1000;body.legHealth=2500;body.faceHealth=500;body.napeHealth=500;
            body.severLaunchSpeed=7;body.severUpwardSpeed=.65f;body.severSpinSpeed=.8f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);EditorUtility.SetDirty(body);
        }
        Undo.RecordObject(dealer,"Speed-based sword damage");dealer.speedScaledDamage=true;dealer.baseDamage=250;
        dealer.speedBonusStartsAt=15;dealer.damagePerSpeed=35;dealer.maximumDamageSpeed=80;
        PrefabUtility.RecordPrefabInstancePropertyModifications(dealer);EditorUtility.SetDirty(dealer);
        var feedback=player.GetComponent<TitanSwordFeedback>();if(feedback==null)feedback=Undo.AddComponent<TitanSwordFeedback>(player.gameObject);
        Undo.RecordObject(feedback,"Sword presentation references");feedback.dealer=dealer;feedback.attack=attack;feedback.sweep=sweep;feedback.playerCamera=camera;feedback.effectMaterial=material;
        feedback.slashSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TitanCombat/SwordWhoosh.wav");
        feedback.hitSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TitanCombat/FleshHit.wav");
        feedback.severSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TitanCombat/LimbSever.wav");
        feedback.killSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/TitanCombat/NapeKill.wav");
        PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);EditorUtility.SetDirty(feedback);
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        Debug.Log("Installed scene-only Titan health: arms1000 / legs2500 / face500 / nape500. Speed damage250 +35 per m/s above15, capped80. Scene left unsaved for review.");
    }
}
#endif
