using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates ONLY the isolated navigation lab. Does not modify the body prefab or gameplay scenes.</summary>
public static class TitanAISandboxBuilder
{
    public const string ScenePath = "Assets/Scenes/TitanAISandbox.unity";

    [MenuItem("Tools/Titans/Build Patrol and Chase Sandbox")]
    public static void BuildSandbox()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring the AI lab.");
        if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
            throw new InvalidOperationException("Close the AI lab before rebuilding it; loaded scene contents are never overwritten.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TitanPrototypeBuilder.PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Build the modular body prefab first.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var floor = Box("AI Lab Ground", new Vector3(0, -.25f, 0), new Vector3(90, .5f, 90), Material("Ground", new Color(.12f,.17f,.20f)));
            var wall = Box("Sight-blocking navigation wall", new Vector3(0, 3.5f, 7), new Vector3(8, 7, 2.5f), Material("Wall", new Color(.64f,.42f,.16f)));
            var surface = new GameObject("Owned runtime NavMesh - lab geometry only").AddComponent<TitanNavigationSurface>();
            surface.geometry = new[] { floor, wall };
            var target = new GameObject("Controllable Human Proxy - WASD");
            target.transform.position = new Vector3(0, 0, 18);
            var controller = target.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .32f; controller.center = new Vector3(0,.9f,0);
            controller.stepOffset = .3f;
            var proxy = target.AddComponent<TitanSandboxTarget>();
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Teal human - 1.8m";
            visual.transform.SetParent(target.transform, false);
            visual.transform.localPosition = new Vector3(0,.9f,0);
            visual.transform.localScale = new Vector3(.65f,.9f,.65f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = Material("Human", new Color(.04f,.83f,.85f));
            var light = new GameObject("AI Lab Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45,-30,0);
            var fill = new GameObject("AI Lab Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .55f; fill.color = new Color(.6f,.8f,1);
            fill.transform.rotation = Quaternion.Euler(35,145,0);
            var camera = new GameObject("AI Lab Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(24,43,44);
            camera.transform.LookAt(new Vector3(0,3,2)); camera.fieldOfView = 52; camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.05f,.065f);
            camera.gameObject.AddComponent<AudioListener>();
            var demo = new GameObject("AI Lab Controls").AddComponent<TitanAISandboxDemo>();
            demo.titanPrefab = prefab; demo.surface = surface; demo.target = proxy; demo.previewCamera = camera;
            demo.titan = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<ModularTitanBody>();
            demo.titan.transform.position = demo.spawnPosition;
            var markerMaterial = Material("Route", new Color(.04f,.65f,.7f));
            for (int i = 0; i < demo.patrolPoints.Length; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Patrol point " + (i + 1);
                marker.transform.position = demo.patrolPoints[i] + Vector3.up * .025f;
                marker.transform.localScale = new Vector3(1.5f,.025f,1.5f);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + ScenePath + ". Body prefab and gameplay scenes left untouched.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static BoxCollider Box(string name, Vector3 center, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.position = center; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.GetComponent<BoxCollider>();
    }

    static Material Material(string name, Color color)
    {
        string path = "Assets/Art/Titans/Prototype/Materials/AI_Lab_" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
