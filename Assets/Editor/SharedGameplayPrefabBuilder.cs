using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit saved-scene migration in a CLOSED disposable worker only.
/// Existing roots stay roots; no runtime binder, physics, input or navigation bake.</summary>
public static class SharedGameplayPrefabBuilder
{
    const string Folder = "Assets/Prefabs/SharedGameplay";
    static readonly string[] Scenes = { "Assets/Scenes/GiantForest2.unity", "Assets/Scenes/ShiganshinaDistrict.unity" };
    static readonly string[] Roots = { "Player", "Main Camera", "PLayerFollower", "LeftHook", "RightLine", "Canvas", "EventSystem", "GameManager", "TmpMusicElement", "Titan random spawner" };
    static readonly string[] Names = { "Player", "MainCamera", "PlayerFollower", "LeftHook", "RightLine", "HUD", "EventSystem", "GameManager", "MusicSource", "TitanSpawner" };
    static readonly HashSet<string> Identity = new HashSet<string> { "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Component", "m_Children", "m_Father", "m_RootOrder" };
    sealed class SavedProperty { public string path, canonical; public SerializedPropertyType type; public object value; }
    sealed class SavedOwner { public Object owner; public List<SavedProperty> properties; }
    static readonly System.Reflection.PropertyInfo GradientValue = typeof(SerializedProperty).GetProperty("gradientValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    static object ReadValue(SerializedProperty p)
    {
        switch(p.propertyType)
        {
            case SerializedPropertyType.Integer: return p.longValue;
            case SerializedPropertyType.Boolean: return p.boolValue;
            case SerializedPropertyType.Float: return p.doubleValue;
            case SerializedPropertyType.String: return p.stringValue;
            case SerializedPropertyType.Color: return p.colorValue;
            case SerializedPropertyType.ObjectReference: return p.objectReferenceValue;
            case SerializedPropertyType.ExposedReference: return p.exposedReferenceValue;
            case SerializedPropertyType.LayerMask: case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize: case SerializedPropertyType.Character: return p.intValue;
            case SerializedPropertyType.Vector2: return p.vector2Value;
            case SerializedPropertyType.Vector3: return p.vector3Value;
            case SerializedPropertyType.Vector4: return p.vector4Value;
            case SerializedPropertyType.Quaternion: return p.quaternionValue;
            case SerializedPropertyType.Rect: return p.rectValue;
            case SerializedPropertyType.Bounds: return p.boundsValue;
            case SerializedPropertyType.Vector2Int: return p.vector2IntValue;
            case SerializedPropertyType.Vector3Int: return p.vector3IntValue;
            case SerializedPropertyType.RectInt: return p.rectIntValue;
            case SerializedPropertyType.BoundsInt: return p.boundsIntValue;
            case SerializedPropertyType.AnimationCurve: var c = p.animationCurveValue; return new AnimationCurve(c.keys) { preWrapMode = c.preWrapMode, postWrapMode = c.postWrapMode };
            case SerializedPropertyType.Gradient: var g = (Gradient)GradientValue.GetValue(p, null); return new Gradient { colorKeys = g.colorKeys, alphaKeys = g.alphaKeys, mode = g.mode };
            case SerializedPropertyType.Hash128: return p.hash128Value;
            default: throw new InvalidOperationException("Explicit value migration required: " + p.propertyType);
        }
    }
    static void WriteValue(SerializedProperty p, SavedProperty saved)
    {
        switch(saved.type)
        {
            case SerializedPropertyType.Integer: p.longValue = (long)saved.value; break;
            case SerializedPropertyType.Boolean: p.boolValue = (bool)saved.value; break;
            case SerializedPropertyType.Float: p.doubleValue = (double)saved.value; break;
            case SerializedPropertyType.String: p.stringValue = (string)saved.value; break;
            case SerializedPropertyType.Color: p.colorValue = (Color)saved.value; break;
            case SerializedPropertyType.ObjectReference: p.objectReferenceValue = (Object)saved.value; break;
            case SerializedPropertyType.ExposedReference: p.exposedReferenceValue = (Object)saved.value; break;
            case SerializedPropertyType.LayerMask: case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize: case SerializedPropertyType.Character: p.intValue = (int)saved.value; break;
            case SerializedPropertyType.Vector2: p.vector2Value = (Vector2)saved.value; break;
            case SerializedPropertyType.Vector3: p.vector3Value = (Vector3)saved.value; break;
            case SerializedPropertyType.Vector4: p.vector4Value = (Vector4)saved.value; break;
            case SerializedPropertyType.Quaternion: p.quaternionValue = (Quaternion)saved.value; break;
            case SerializedPropertyType.Rect: p.rectValue = (Rect)saved.value; break;
            case SerializedPropertyType.Bounds: p.boundsValue = (Bounds)saved.value; break;
            case SerializedPropertyType.Vector2Int: p.vector2IntValue = (Vector2Int)saved.value; break;
            case SerializedPropertyType.Vector3Int: p.vector3IntValue = (Vector3Int)saved.value; break;
            case SerializedPropertyType.RectInt: p.rectIntValue = (RectInt)saved.value; break;
            case SerializedPropertyType.BoundsInt: p.boundsIntValue = (BoundsInt)saved.value; break;
            case SerializedPropertyType.AnimationCurve: p.animationCurveValue = (AnimationCurve)saved.value; break;
            case SerializedPropertyType.Gradient: GradientValue.SetValue(p, saved.value, null); break;
            case SerializedPropertyType.Hash128: p.hash128Value = (Hash128)saved.value; break;
            default: throw new InvalidOperationException("Explicit setter required: " + saved.type);
        }
    }
    static List<SavedOwner> CaptureOwners(GameObject[] roots)
    {
        var result = new List<SavedOwner>();
        foreach(var root in roots) foreach(var owner in Owners(root))
        {
            var record = new SavedOwner { owner = owner, properties = new List<SavedProperty>() };
            var so = new SerializedObject(owner); var p = so.GetIterator(); bool children = true;
            while(p.Next(children))
            {
                children = p.propertyType == SerializedPropertyType.Generic;
                if(Skip(p)) { children = false; continue; }
                if(p.propertyType == SerializedPropertyType.Generic || p.propertyType == SerializedPropertyType.FixedBufferSize) continue;
                record.properties.Add(new SavedProperty { path = p.propertyPath, canonical = PropertyValue(p), type = p.propertyType, value = ReadValue(p) });
            }
            result.Add(record);
        }
        return result;
    }
    static void RestoreOwners(List<SavedOwner> records)
    {
        // Cache real serialized values, not cloned active scripts/Canvases. This
        // avoids edit-time RigBuilder callbacks and preview-driven UI state.
        foreach(var record in records)
        {
            var so = new SerializedObject(record.owner);
            foreach(var saved in record.properties)
            {
                var p = so.FindProperty(saved.path); if(p == null) throw new InvalidOperationException("Lost original property " + saved.path);
                if(PropertyValue(p) != saved.canonical) WriteValue(p, saved);
            }
            so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(record.owner);
        }
    }
    [Serializable] public class OwnerReceipt { public string key, sha256; }
    [Serializable] public class OverrideReceipt { public string prefab, target, property, value, reference; }
    [Serializable] public class SceneReceipt
    {
        public string scene; public int objects, components, preservedOwners;
        public string[] roots; public OwnerReceipt[] before, after; public OverrideReceipt[] overrides;
    }
    [Serializable] public class Report { public string unity, authoredAt; public SceneReceipt[] scenes; public string[] prefabs; }
    static void RequireOwned()
    {
        if(!Application.isBatchMode || Application.isPlaying || Application.unityVersion != "2021.3.25f1" ||
            Environment.GetEnvironmentVariable("ODM_SHARED_PREFABS_OWNED") != "1" || !File.Exists(".shared-gameplay-prefabs-owned"))
            throw new InvalidOperationException("Closed marker-owned Unity 2021.3.25f1 authoring worker required");
    }
    static string Asset(int index) { return Folder + "/" + Names[index] + ".prefab"; }
    static bool Skip(SerializedProperty property)
    {
        string top = property.propertyPath.Split('.')[0];
        if(Identity.Contains(top)) return true;
        var rect = property.serializedObject.targetObject as RectTransform;
        var canvas = rect != null && rect.parent == null ? rect.GetComponent<Canvas>() : null;
        // Screen-space root RectTransform layout is driven by the native Canvas,
        // display and editor window, not authored scene data (saved source is zero).
        // All child UI transforms and Canvas/Scaler settings remain in the audit.
        return canvas != null && canvas.renderMode != RenderMode.WorldSpace &&
            (top == "m_LocalPosition" || top == "m_LocalScale" || top == "m_AnchorMin" || top == "m_AnchorMax" || top == "m_AnchoredPosition" || top == "m_SizeDelta" || top == "m_Pivot");
    }
    static string PathOf(Transform transform)
    {
        var parts = new List<string>();
        while(transform.parent != null) { parts.Add(transform.GetSiblingIndex() + ":" + transform.name); transform = transform.parent; }
        parts.Add(transform.name); parts.Reverse(); return string.Join("/", parts);
    }
    static string Key(Object value)
    {
        if(value == null) return "null";
        var go = value as GameObject; if(go != null && !EditorUtility.IsPersistent(go)) return "go:" + PathOf(go.transform);
        var component = value as Component;
        if(component != null && !EditorUtility.IsPersistent(component))
        {
            var same = component.GetComponents(component.GetType()); int index = Array.IndexOf(same, component);
            return "component:" + PathOf(component.transform) + ":" + component.GetType().FullName + "#" + index;
        }
        string guid; long local;
        if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out local)) return "asset:" + guid + ":" + local;
        return "builtin:" + value.GetType().FullName + ":" + AssetDatabase.GetAssetPath(value) + ":" + value.name;
    }
    static IEnumerable<Object> Owners(GameObject root)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            yield return t.gameObject;
            foreach(var c in t.GetComponents<Component>())
            { if(c == null) throw new InvalidOperationException("Missing script at " + PathOf(t)); yield return c; }
        }
    }
    static Dictionary<string, Object> RelativeOwners(GameObject root)
    {
        var result = new Dictionary<string, Object>();
        foreach(var owner in Owners(root))
        {
            var go = owner as GameObject; var c = owner as Component; var t = go != null ? go.transform : c.transform;
            var indices = new List<int>(); while(t != root.transform) { indices.Add(t.GetSiblingIndex()); t = t.parent; }
            indices.Reverse(); string path = string.Join("/", indices.Select(i => i.ToString()).ToArray());
            string slot = go != null ? "GO" : c.GetType().AssemblyQualifiedName + "#" + Array.IndexOf(c.GetComponents(c.GetType()), c);
            result.Add(path + ":" + slot, owner);
        }
        return result;
    }
    static void Pair(GameObject source, GameObject destination, Dictionary<Object, Object> map)
    {
        var from = RelativeOwners(source); var to = RelativeOwners(destination);
        if(from.Count != to.Count || from.Keys.Any(k => !to.ContainsKey(k)))
            throw new InvalidOperationException("Shared hierarchy/component layout differs: " + source.name);
        foreach(var entry in from) map[entry.Value] = to[entry.Key];
    }
    static void CopyValues(Object source, Object destination, Dictionary<Object, Object> map)
    {
        var from = new SerializedObject(source); var to = new SerializedObject(destination);
        var p = from.GetIterator(); bool children = true;
        while(p.Next(children))
        {
            children = p.propertyType == SerializedPropertyType.Generic;
            if(Skip(p)) { children = false; continue; }
            // Copy only non-reference leaves / array sizes. Never temporarily put
            // a preview-scene object reference or native prefab identity on a target.
            if(p.propertyType == SerializedPropertyType.Generic) continue;
            if(p.propertyType == SerializedPropertyType.ManagedReference)
                throw new InvalidOperationException("Explicit managed-reference migration required: " + p.propertyPath);
            if(p.propertyType == SerializedPropertyType.ObjectReference || p.propertyType == SerializedPropertyType.ExposedReference) continue;
            if(to.FindProperty(p.propertyPath) == null) throw new InvalidOperationException("Missing destination property " + p.propertyPath);
            to.CopyFromSerializedPropertyIfDifferent(p);
        }
        to.ApplyModifiedPropertiesWithoutUndo();
        from.Update(); to.Update(); p = from.GetIterator(); children = true;
        while(p.Next(children))
        {
            children = p.propertyType == SerializedPropertyType.Generic; if(Skip(p)) { children = false; continue; }
            bool exposed = p.propertyType == SerializedPropertyType.ExposedReference;
            if(!exposed && p.propertyType != SerializedPropertyType.ObjectReference) continue;
            var value = exposed ? p.exposedReferenceValue : p.objectReferenceValue; Object replacement;
            if(value != null && map.TryGetValue(value, out replacement)) value = replacement;
            var target = to.FindProperty(p.propertyPath);
            if(target == null) throw new InvalidOperationException("Missing reference property " + p.propertyPath);
            var current = exposed ? target.exposedReferenceValue : target.objectReferenceValue;
            if(current == value) continue;
            if(current != null && value != null && EditorUtility.IsPersistent(current) && EditorUtility.IsPersistent(value) && Key(current) == Key(value)) continue;
            if(exposed) target.exposedReferenceValue = value; else target.objectReferenceValue = value;
        }
        to.ApplyModifiedPropertiesWithoutUndo();
        if(PrefabUtility.IsPartOfPrefabInstance(destination)) PrefabUtility.RecordPrefabInstancePropertyModifications(destination);
    }
    static void RemapReferences(Scene scene, Dictionary<Object, Object> map, HashSet<GameObject> excluded)
    {
        foreach(var root in scene.GetRootGameObjects())
        {
            if(excluded.Contains(root)) continue;
            foreach(var owner in Owners(root))
            {
                var so = new SerializedObject(owner); var p = so.GetIterator(); bool changed = false, children = true;
                while(p.Next(children))
                {
                    children = p.propertyType == SerializedPropertyType.Generic; if(Skip(p)) { children = false; continue; }
                    bool exposed = p.propertyType == SerializedPropertyType.ExposedReference;
                    if(!exposed && p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = exposed ? p.exposedReferenceValue : p.objectReferenceValue, replacement;
                    if(value == null || !map.TryGetValue(value, out replacement) || value == replacement) continue;
                    if(exposed) p.exposedReferenceValue = replacement; else p.objectReferenceValue = replacement; changed = true;
                }
                if(changed) { so.ApplyModifiedPropertiesWithoutUndo(); if(PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner); }
            }
        }
    }
    static void RemoveRedundantRootAssetOverrides(GameObject[] roots)
    {
        // Native component copying can mark equal material references as explicit
        // overrides. Leave those inherited so later shared-asset edits propagate.
        for(int i = 0; i < roots.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Asset(i));
            foreach(var component in roots[i].GetComponents<Component>())
            {
                if(component is Transform) continue;
                int slot = Array.IndexOf(component.GetComponents(component.GetType()), component);
                var source = asset.GetComponents(component.GetType())[slot];
                var from = new SerializedObject(source); var to = new SerializedObject(component);
                var paths = new List<string>(); var p = to.GetIterator(); bool children = true;
                while(p.Next(children))
                {
                    children = p.propertyType == SerializedPropertyType.Generic;
                    if(Skip(p) || p.propertyPath == "m_Script") { children = false; continue; }
                    if(p.propertyType != SerializedPropertyType.ObjectReference || !p.prefabOverride || p.objectReferenceValue == null || !EditorUtility.IsPersistent(p.objectReferenceValue)) continue;
                    var original = from.FindProperty(p.propertyPath);
                    if(original != null && original.propertyType == p.propertyType && Key(original.objectReferenceValue) == Key(p.objectReferenceValue)) paths.Add(p.propertyPath);
                }
                foreach(string path in paths)
                { to.Update(); PrefabUtility.RevertPropertyOverride(to.FindProperty(path), InteractionMode.AutomatedAction); }
            }
        }
    }
    static string PropertyValue(SerializedProperty p)
    {
        switch(p.propertyType)
        {
            case SerializedPropertyType.ObjectReference: return Key(p.objectReferenceValue);
            case SerializedPropertyType.ExposedReference: return Key(p.exposedReferenceValue);
            case SerializedPropertyType.Generic: return "generic";
            case SerializedPropertyType.Integer: return p.longValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.Float: return p.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            case SerializedPropertyType.String: return p.stringValue;
            case SerializedPropertyType.Color: return p.colorValue.ToString("R");
            case SerializedPropertyType.LayerMask: case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize: case SerializedPropertyType.Character: return p.intValue.ToString();
            case SerializedPropertyType.Vector2: return p.vector2Value.ToString("R");
            case SerializedPropertyType.Vector3: return p.vector3Value.ToString("R");
            case SerializedPropertyType.Vector4: return p.vector4Value.ToString("R");
            case SerializedPropertyType.Quaternion: return p.quaternionValue.ToString("R");
            case SerializedPropertyType.Rect: return p.rectValue.ToString("R");
            case SerializedPropertyType.Bounds: return p.boundsValue.ToString("R");
            case SerializedPropertyType.Vector2Int: return p.vector2IntValue.ToString();
            case SerializedPropertyType.Vector3Int: return p.vector3IntValue.ToString();
            case SerializedPropertyType.RectInt: return p.rectIntValue.ToString();
            case SerializedPropertyType.BoundsInt: return p.boundsIntValue.ToString();
            case SerializedPropertyType.AnimationCurve:
                var curve = p.animationCurveValue;
                return curve.preWrapMode + "/" + curve.postWrapMode + "/" + string.Join(";", curve.keys.Select(k => k.time.ToString("R") + "," + k.value.ToString("R") + "," + k.inTangent.ToString("R") + "," + k.outTangent.ToString("R") + "," + k.inWeight.ToString("R") + "," + k.outWeight.ToString("R") + "," + k.weightedMode).ToArray());
            case SerializedPropertyType.Gradient:
                var gradient = (Gradient)typeof(SerializedProperty).GetProperty("gradientValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(p, null);
                return gradient.mode + "/" + string.Join(";", gradient.colorKeys.Select(k => k.time.ToString("R") + ":" + k.color.ToString("R")).ToArray()) + "/" + string.Join(";", gradient.alphaKeys.Select(k => k.time.ToString("R") + ":" + k.alpha.ToString("R")).ToArray());
            case SerializedPropertyType.FixedBufferSize: return p.fixedBufferSize.ToString();
            case SerializedPropertyType.Hash128: return p.hash128Value.ToString();
            default: throw new InvalidOperationException("Unreviewed serialized property type " + p.propertyType + ":" + p.propertyPath);
        }
    }
    static string Sha(string text)
    { using(var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
    static SortedDictionary<string, string> Snapshot(Scene scene)
    {
        var result = new SortedDictionary<string, string>();
        foreach(var root in scene.GetRootGameObjects()) foreach(var owner in Owners(root))
        {
            var text = new StringBuilder(); var so = new SerializedObject(owner); var p = so.GetIterator(); bool children = true;
            var go = owner as GameObject;
            if(go != null) text.Append("component-order:").AppendLine(string.Join(";", go.GetComponents<Component>()
                // Unity normalizes the order of the legacy FBX's added editor
                // BoneRenderer on prefab conversion. Its presence/data are still
                // audited as an owner; all runtime component ordering stays exact.
                .Where(c => !(PathOf(go.transform) == "Player/0:CharacterAnimated" && c.GetType().FullName == "UnityEngine.Animations.Rigging.BoneRenderer"))
                .Select(c => c.GetType().FullName).ToArray()));
            while(p.Next(children))
            {
                children = p.propertyType == SerializedPropertyType.Generic; if(Skip(p)) { children = false; continue; }
                text.Append(p.propertyPath).Append('=').Append(p.propertyType).Append(':').AppendLine(PropertyValue(p));
            }
            string key = Key(owner); if(result.ContainsKey(key)) throw new InvalidOperationException("Ambiguous scene identity " + key);
            result.Add(key, text.ToString());
        }
        return result;
    }
    static OwnerReceipt[] Digests(SortedDictionary<string, string> values)
    { return values.Select(p => new OwnerReceipt { key = p.Key, sha256 = Sha(p.Value) }).ToArray(); }
    static void Preserve(SortedDictionary<string, string> before, SortedDictionary<string, string> after, string stage)
    {
        var differences = before.Keys.Union(after.Keys).Where(k => !before.ContainsKey(k) || !after.ContainsKey(k) || before[k] != after[k]).ToArray();
        if(differences.Length == 0) return;
        Directory.CreateDirectory("Library/SharedGameplayPrefabs");
        File.WriteAllText("Library/SharedGameplayPrefabs/migration-differences.txt", string.Join("\n\n", differences.Take(30).Select(k => k + "\nBEFORE\n" + (before.ContainsKey(k) ? before[k] : "MISSING") + "\nAFTER\n" + (after.ContainsKey(k) ? after[k] : "MISSING")).ToArray()));
        throw new InvalidOperationException(stage + ": serialized scene preservation failed for " + differences.Length + " owners; inspect migration-differences.txt");
    }
    static GameObject[] Select(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        return Roots.Select(name => roots.Single(g => g.name == name)).ToArray();
    }
    static OverrideReceipt[] Overrides(GameObject[] roots)
    {
        var result = new List<OverrideReceipt>();
        for(int i = 0; i < roots.Length; i++)
            foreach(var m in PrefabUtility.GetPropertyModifications(roots[i]) ?? new PropertyModification[0])
                result.Add(new OverrideReceipt { prefab = Asset(i), target = m.target != null ? m.target.name + ":" + m.target.GetType().Name : "null", property = m.propertyPath, value = m.value, reference = Key(m.objectReference) });
        return result.ToArray();
    }
    public static void BuildOwned()
    {
        RequireOwned(); if(Directory.Exists(Folder)) throw new InvalidOperationException("New prefabs only; explicitly refresh a current-source worker before another run");
        AssetDatabase.CreateFolder("Assets/Prefabs", "SharedGameplay"); Directory.CreateDirectory("Library/SharedGameplayPrefabs");
        var receipts = new List<SceneReceipt>();
        for(int sceneIndex = 0; sceneIndex < Scenes.Length; sceneIndex++)
        {
            var scene = EditorSceneManager.OpenScene(Scenes[sceneIndex], OpenSceneMode.Single);
            var originalRoots = scene.GetRootGameObjects().OrderBy(g => g.transform.GetSiblingIndex()).ToArray();
            var roots = Select(scene); var before = Snapshot(scene);
            if(sceneIndex == 0)
            {
                var originals = CaptureOwners(roots);
                for(int i = 0; i < roots.Length; i++)
                {
                    bool success; PrefabUtility.SaveAsPrefabAssetAndConnect(roots[i], Asset(i), InteractionMode.AutomatedAction, out success);
                    if(!success) throw new InvalidOperationException("Prefab authoring failed: " + Asset(i));
                }
                RestoreOwners(originals);
            }
            else
            {
                var old = roots; roots = new GameObject[old.Length]; var map = new Dictionary<Object, Object>();
                for(int i = 0; i < old.Length; i++)
                {
                    roots[i] = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Asset(i)), scene);
                    roots[i].name = old[i].name; Pair(old[i], roots[i], map);
                }
                for(int i = 0; i < old.Length; i++) foreach(var owner in Owners(old[i])) CopyValues(owner, map[owner], map);
                RemapReferences(scene, map, new HashSet<GameObject>(old));
                for(int i = 0; i < originalRoots.Length; i++)
                { Object replacement; if(map.TryGetValue(originalRoots[i], out replacement)) originalRoots[i] = (GameObject)replacement; }
                foreach(var root in old) Object.DestroyImmediate(root);
            }
            for(int i = 0; i < originalRoots.Length; i++) originalRoots[i].transform.SetSiblingIndex(i);
            RemoveRedundantRootAssetOverrides(roots);
            var after = Snapshot(scene); Preserve(before, after, Scenes[sceneIndex] + " before save");
            var receipt = new SceneReceipt { scene = Scenes[sceneIndex], roots = originalRoots.Select(g => g.name).ToArray(), objects = originalRoots.Sum(g => g.GetComponentsInChildren<Transform>(true).Length), components = originalRoots.Sum(g => g.GetComponentsInChildren<Component>(true).Length), preservedOwners = before.Count, before = Digests(before), overrides = Overrides(roots) };
            EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Worker scene save failed");
            scene = EditorSceneManager.OpenScene(Scenes[sceneIndex], OpenSceneMode.Single); after = Snapshot(scene); Preserve(before, after, Scenes[sceneIndex] + " after reopen");
            foreach(var root in Select(scene)) if(!PrefabUtility.IsAnyPrefabInstanceRoot(root)) throw new InvalidOperationException("Scene object lost its prefab link: " + root.name);
            receipt.after = Digests(after); receipts.Add(receipt); Debug.Log("SHARED_PREFABS_SCENE_PRESERVED " + Scenes[sceneIndex] + " owners=" + before.Count);
        }
        var report = new Report { unity = Application.unityVersion, authoredAt = DateTime.UtcNow.ToString("O"), scenes = receipts.ToArray(), prefabs = Enumerable.Range(0, Names.Length).Select(Asset).ToArray() };
        File.WriteAllText("Library/SharedGameplayPrefabs/migration-report.json", JsonUtility.ToJson(report, true));
        Debug.Log("SHARED_PREFABS_BUILD_COMPLETE linkedPrefabs=" + Names.Length + " scenes=2 play=false physicsStepped=false navigationBaked=false");
    }
}
