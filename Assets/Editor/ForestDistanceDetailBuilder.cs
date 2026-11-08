#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Explicit visual-only distance authoring in a CLOSED project-owned worker.
/// Does not run automatically, enter Play, change scenes in a user Editor, bake or save maps.</summary>
public static class ForestDistanceDetailBuilder
{
    const string Art="Assets/Art/Environment/GiantForest2";
    const string Out="Library/ForestDistanceDetail";
    static readonly string[] Walls={"GF2_Wall_Straight_24x60","GF2_Wall_Corner_24x60","GF2_Wall_Gateway_48x60"};
    static readonly string[] Trees={"GF2_Tree_A_Sentinel","GF2_Tree_B_Leaning","GF2_Tree_C_OldFork"};
    [Serializable] class Report { public string unity;public bool play,physicsStepped,navigationBaked;public Wall[] walls;public SceneReceipt[] scenes; }
    [Serializable] class Wall { public string asset;public long nearTriangles,midTriangles,farTriangles;public float[] thresholds; }
    [Serializable] class SceneReceipt { public string scene;public int colliders;public string physicalDigest; }
    static void Guard()
    {
        if(!Application.isBatchMode || EditorApplication.isPlaying || !File.Exists(".shared-gameplay-prefabs-owned") || Environment.GetEnvironmentVariable("ODM_FOREST_DISTANCE_OWNED")!="1")
            throw new InvalidOperationException("Closed owned authoring worker only");
        Directory.CreateDirectory(Out);
    }
    static string PathOf(Transform t)
    {return t.parent==null?t.name+"#"+t.GetSiblingIndex():PathOf(t.parent)+"/"+t.name+"#"+t.GetSiblingIndex();}
    static string Key(Object value)
    {
        if(value==null)return "null";
        string guid;long id;if(EditorUtility.IsPersistent(value) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out guid,out id))return guid+":"+id;
        var c=value as Component;if(c!=null)return PathOf(c.transform)+":"+c.GetType().FullName+":"+Array.IndexOf(c.GetComponents(c.GetType()),c);
        var go=value as GameObject;if(go!=null)return PathOf(go.transform);
        return value.GetType().FullName+":"+value.name;
    }
    static string Hash(string text)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    static string Serialized(Object owner)
    {
        string json=EditorJsonUtility.ToJson(owner);
        return Regex.Replace(json,"\"instanceID\":(-?[0-9]+)",m=>"\"instanceID\":\""+Key(EditorUtility.InstanceIDToObject(int.Parse(m.Groups[1].Value)))+"\"");
    }
    static SceneReceipt Physical(Scene scene)
    {
        var entries=new List<string>();int count=0;
        foreach(var c in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>(true)))
        {
            count++;entries.Add(Key(c)+"|"+Serialized(c)+"|"+c.transform.localToWorldMatrix.ToString("R")+"|"+c.gameObject.layer+"|"+c.gameObject.activeInHierarchy);
        }
        entries.Sort(StringComparer.Ordinal);return new SceneReceipt{scene=scene.path,colliders=count,physicalDigest=Hash(string.Join("\n",entries))};
    }
    static string NearData(GameObject root)
    {
        var data=new List<string>();
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(r.transform.name.EndsWith("_LOD1") || r.transform.name.EndsWith("_LOD2"))continue;
            var f=r.GetComponent<MeshFilter>();data.Add(PathOf(r.transform)+"|"+Key(f.sharedMesh)+"|"+string.Join(";",r.sharedMaterials.Select(Key))+"|"+r.transform.localToWorldMatrix.ToString("R")+"|"+r.shadowCastingMode+"|"+r.receiveShadows);
        }
        data.Sort(StringComparer.Ordinal);return Hash(string.Join("\n",data));
    }
    static long Triangles(IEnumerable<Renderer> renderers)
    {
        long total=0;foreach(var r in renderers){var f=r.GetComponent<MeshFilter>();if(f==null)continue;for(int i=0;i<f.sharedMesh.subMeshCount;i++)total+=f.sharedMesh.GetIndexCount(i)/3;}return total;
    }
    public static void BuildOwned()
    {
        Guard();var before=new List<SceneReceipt>();
        foreach(string map in new[]{"GiantForest2","ShiganshinaDistrict"})before.Add(Physical(EditorSceneManager.OpenScene("Assets/Scenes/"+map+".unity",OpenSceneMode.Single)));
        CaptureOwned("Before");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var walls=new List<Wall>();
        foreach(string name in Walls)
        {
            string path=Art+"/Prefabs/"+name+".prefab",guid=AssetDatabase.AssetPathToGUID(path);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(root.GetComponent<LODGroup>()!=null)throw new InvalidOperationException("Refusing duplicate wall migration: "+name);
                string near=NearData(root);var original=root.GetComponentsInChildren<MeshRenderer>(true);var lods=new LOD[3];lods[0]=new LOD(.60f,original);
                for(int level=1;level<=2;level++)
                {
                    string model=Art+"/WallDistance/"+name+"_LOD"+level+".fbx";
                    var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.animationType=ModelImporterAnimationType.None;importer.addCollider=false;importer.isReadable=false;importer.globalScale=1;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
                    var data=AssetDatabase.LoadAssetAtPath<GameObject>(model);var child=(GameObject)PrefabUtility.InstantiatePrefab(data);child.name=name+"_LOD"+level;child.transform.SetParent(root.transform,false);
                    foreach(var t in child.GetComponentsInChildren<Transform>(true))t.gameObject.layer=root.layer;
                    foreach(var r in child.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        r.sharedMaterials=r.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/"+(m!=null && m.name.Contains("Recess")?"Recess":"Stone")+".mat")).ToArray();
                        r.shadowCastingMode=original[0].shadowCastingMode;r.receiveShadows=original[0].receiveShadows;r.reflectionProbeUsage=original[0].reflectionProbeUsage;r.lightProbeUsage=original[0].lightProbeUsage;
                    }
                    if(child.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Distance mesh must have NO colliders");
                    lods[level]=new LOD(level==1?.18f:.00001f,child.GetComponentsInChildren<Renderer>(true));
                }
                var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.fadeMode=LODFadeMode.None;group.RecalculateBounds();
                if(NearData(root)!=near)throw new InvalidOperationException("Original near visual data changed: "+name);
                var receipt=new Wall{asset=path,nearTriangles=Triangles(lods[0].renderers),midTriangles=Triangles(lods[1].renderers),farTriangles=Triangles(lods[2].renderers),thresholds=lods.Select(l=>l.screenRelativeTransitionHeight).ToArray()};
                if(receipt.midTriangles>=receipt.nearTriangles || receipt.farTriangles>=receipt.midTriangles)throw new InvalidOperationException("LOD triangle counts must decrease");
                PrefabUtility.SaveAsPrefabAsset(root,path);if(AssetDatabase.AssetPathToGUID(path)!=guid)throw new InvalidOperationException("Prefab GUID changed");walls.Add(receipt);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string name in Trees)
        {
            string path=Art+"/Prefabs/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var group=root.GetComponent<LODGroup>();var lods=group.GetLODs();
                // Existing approved meshes, full-detail near, no earlier tree cull.
                lods[0].screenRelativeTransitionHeight=.75f;lods[1].screenRelativeTransitionHeight=.24f;
                group.SetLODs(lods);PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();var after=new List<SceneReceipt>();
        foreach(var expected in before)
        {
            var actual=Physical(EditorSceneManager.OpenScene(expected.scene,OpenSceneMode.Single));
            if(actual.colliders!=expected.colliders || actual.physicalDigest!=expected.physicalDigest)throw new InvalidOperationException("Physical scene graph changed: "+expected.scene);
            after.Add(actual);Debug.Log("FOREST_DISTANCE_PHYSICAL_PRESERVED "+actual.scene+" colliders="+actual.colliders);
        }
        CaptureOwned("After");
        var report=new Report{unity=Application.unityVersion,walls=walls.ToArray(),scenes=after.ToArray()};
        File.WriteAllText(Out+"/authoring-report.json",JsonUtility.ToJson(report,true));
        Debug.Log("FOREST_DISTANCE_COMPLETE "+JsonUtility.ToJson(report));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
    static void CaptureOwned(string label)
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/GiantForest2.unity",OpenSceneMode.Single);
        var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single(c=>c.enabled && c.gameObject.activeInHierarchy && c.CompareTag("MainCamera"));
        camera.aspect=16f/9f;camera.fieldOfView=60;
        foreach(string shot in new[]{"Spawn","Canopy","NearWall","MidWall","Gate"})
        {
            Vector3 position,target;
            if(shot=="Spawn" || shot=="Canopy")
            {var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ScoutPlayerAnimation>()).Single();camera.transform.rotation=Quaternion.Euler(shot=="Spawn"?10:-25,0,0);position=player.transform.position+new Vector3(5,3,0)-camera.transform.forward*12;target=position+camera.transform.forward;}
            else if(shot=="NearWall"){position=new Vector3(400,65,-650);target=new Vector3(400,65,-768);}
            else if(shot=="MidWall"){position=new Vector3(280,90,-180);target=new Vector3(280,80,-768);}
            else{position=new Vector3(0,60,-250);target=new Vector3(0,65,-768);}
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            var rt=RenderTexture.GetTemporary(1280,720,24);var active=RenderTexture.active;var old=camera.targetTexture;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Out+"/"+label+"_"+shot+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=active;camera.targetTexture=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);}
        }
    }
}
#endif
