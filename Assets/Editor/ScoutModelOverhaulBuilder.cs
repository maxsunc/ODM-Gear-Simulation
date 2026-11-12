using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Explicit model-only authoring/capture in a closed owned worker. Never auto-runs.</summary>
public static class ScoutModelOverhaulBuilder
{
    const string PrefabPath = "Assets/Prefabs/Player/ScoutVisual.prefab";
    const string AssetRoot = "Assets/Art/Player/Scout/ModelOverhaul";
    const string MeshPath = AssetRoot + "/ScoutBody_Refined.asset";
    const string MaterialPath = AssetRoot + "/ScoutBody_Refined.mat";
    [Serializable] public sealed class Vertex { public float[] p, n, uv, weights; public string[] bones; }
    [Serializable] public sealed class Head { public string name; public float[] p; }
    [Serializable] public sealed class Geometry
    {
        public Vertex[] vertices; public int[] triangles; public Head[] boneHeads;
        public float[] originalMin, originalMax; public string protectedBefore, protectedAfter, sourceRoot;
    }
    [Serializable] sealed class Report
    {
        public int bodyVertices, bodyTriangles, bones, materials;
        public float maximumRestLandmarkError, originalBoundsError;
        public string protectedDccHash, bindingSpace, sourceGeometry, geometrySha256;
        public bool reusedOriginalBindposes, reusedOriginalBoneArray, noAnimationWrites, noPhysicsSimulation;
    }
    static void Guard()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string marker = Path.Combine(project, ".titan-validation-owned");
        string source = Environment.GetEnvironmentVariable("SCOUT_SOURCE_ROOT");
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            Environment.GetEnvironmentVariable("ODM_SCOUT_MODEL_OWNED") != "1" ||
            String.IsNullOrEmpty(source) || !File.Exists(marker) ||
            Path.GetFullPath(File.ReadAllText(marker)).TrimEnd('\\','/') != Path.GetFullPath(source).TrimEnd('\\','/') ||
            Path.GetFullPath(source).TrimEnd('\\','/') == project.TrimEnd('\\','/'))
            throw new InvalidOperationException("Scout model authoring requires its closed, marker-owned worker");
    }
    static Vector3 V(float[] a) { return new Vector3(a[0], a[1], a[2]); }
    static Transform Find(GameObject go, string name)
    { return go.GetComponentsInChildren<Transform>(true).Single(t => t.name == name); }
    static SkinnedMeshRenderer Body(GameObject go)
    { return go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.name == "ScoutBody"); }
    static Matrix4x4 Basis(Vector3 origin, Vector3 x, Vector3 y, Vector3 z)
    {
        var m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(x.x-origin.x, x.y-origin.y, x.z-origin.z, 0));
        m.SetColumn(1, new Vector4(y.x-origin.x, y.y-origin.y, y.z-origin.z, 0));
        m.SetColumn(2, new Vector4(z.x-origin.x, z.y-origin.y, z.z-origin.z, 0));
        m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1)); return m;
    }
    public static void BuildOwned()
    {
        Guard();
        PlayerSettings.companyName = "ODM Owned Workers";
        PlayerSettings.productName = "Scout Model Review";
        string work = Environment.GetEnvironmentVariable("SCOUT_MODEL_WORKER");
        if (String.IsNullOrEmpty(work) || !File.Exists(Path.Combine(work, ".scout-model-overhaul-owned")))
            throw new InvalidOperationException("Missing Blender worker ownership");
        string geometryPath = Path.Combine(work, "body-geometry.json");
        var data = JsonUtility.FromJson<Geometry>(File.ReadAllText(geometryPath));
        if (data.protectedBefore != data.protectedAfter || data.sourceRoot != Environment.GetEnvironmentVariable("SCOUT_SOURCE_ROOT"))
            throw new InvalidOperationException("DCC preservation receipt mismatch");
        var originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var original = Body(originalPrefab);
        var heads = data.boneHeads.ToDictionary(h => h.name, h => V(h.p));
        var boneIndices = original.bones.Select((b,i) => new { b.name, i }).ToDictionary(b => b.name, b => b.i);
        var bindposes = original.sharedMesh.bindposes;
        Func<string, Vector3> nativeHead = name => bindposes[boneIndices[name]].inverse.MultiplyPoint3x4(Vector3.zero);
        string[] landmarks = { "Hips", "LeftUpperLeg", "LeftThumb1", "Head" };
        var dcc = Basis(heads[landmarks[0]],heads[landmarks[1]],heads[landmarks[2]],heads[landmarks[3]]);
        var native = Basis(nativeHead(landmarks[0]),nativeHead(landmarks[1]),nativeHead(landmarks[2]),nativeHead(landmarks[3]));
        var conversion = native * dcc.inverse;
        float restError = 0;
        foreach (var pair in heads)
            if (boneIndices.ContainsKey(pair.Key))
                restError = Mathf.Max(restError,Vector3.Distance(conversion.MultiplyPoint3x4(pair.Value),nativeHead(pair.Key)));
        if (restError > .000002f) throw new InvalidOperationException("Rest-space mismatch: " + restError);
        var originalBounds = new Bounds(); bool first = true;
        for(int i=0;i<8;i++)
        {
            var p = new Vector3((i&1)==0?data.originalMin[0]:data.originalMax[0],(i&2)==0?data.originalMin[1]:data.originalMax[1],(i&4)==0?data.originalMin[2]:data.originalMax[2]);
            p=conversion.MultiplyPoint3x4(p); if(first){originalBounds=new Bounds(p,Vector3.zero);first=false;}else originalBounds.Encapsulate(p);
        }
        float boundsError=Mathf.Max(Vector3.Distance(originalBounds.min,original.sharedMesh.bounds.min),Vector3.Distance(originalBounds.max,original.sharedMesh.bounds.max));
        if(boundsError>.000003f) throw new InvalidOperationException("Original geometry coordinate mismatch: " + boundsError);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool create = mesh == null;
        if(create) mesh=new Mesh(); else mesh.Clear();
        mesh.name="ScoutBody_Refined";
        mesh.indexFormat=data.vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        var positions=new Vector3[data.vertices.Length]; var normals=new Vector3[positions.Length];
        var uv=new Vector2[positions.Length]; var skin=new BoneWeight[positions.Length];
        var normalConversion=conversion.inverse.transpose;
        for(int i=0;i<positions.Length;i++)
        {
            var v=data.vertices[i]; positions[i]=conversion.MultiplyPoint3x4(V(v.p));
            normals[i]=normalConversion.MultiplyVector(V(v.n)).normalized; uv[i]=new Vector2(v.uv[0],v.uv[1]);
            if(v.bones.Length>4 || v.bones.Length==0) throw new InvalidOperationException("Invalid skin influences");
            var indices=new int[4];var weights=new float[4];
            for(int k=0;k<v.bones.Length;k++){indices[k]=boneIndices[v.bones[k]];weights[k]=v.weights[k];}
            skin[i]=new BoneWeight { boneIndex0=indices[0],boneIndex1=indices[1],boneIndex2=indices[2],boneIndex3=indices[3],weight0=weights[0],weight1=weights[1],weight2=weights[2],weight3=weights[3] };
        }
        int[] triangles=(int[])data.triangles.Clone();
        if(conversion.determinant<0)for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
        mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.boneWeights=skin;
        mesh.bindposes=bindposes;mesh.triangles=triangles;mesh.RecalculateBounds();mesh.RecalculateTangents();
        if(create)AssetDatabase.CreateAsset(mesh,MeshPath);else EditorUtility.SetDirty(mesh);
        foreach(string name in new[]{"BaseColor","MetallicSmoothness","Normal"})
        {
            string path=AssetRoot+"/ScoutRefined_"+name+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize=2048;importer.sRGBTexture=name=="BaseColor";
            importer.textureType=name=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,MaterialPath);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot+"/ScoutRefined_BaseColor.png"));
        material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot+"/ScoutRefined_MetallicSmoothness.png"));
        material.SetFloat("_Smoothness",1);material.SetFloat("_SmoothnessTextureChannel",0);material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot+"/ScoutRefined_Normal.png"));
        material.SetFloat("_BumpScale",.3f);material.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        string output="Library/ScoutModelOverhaul";Directory.CreateDirectory(output);
        var pipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Renderer/Renderer.asset");
        if(pipeline==null)throw new InvalidOperationException("Source URP pipeline missing in review worker");
        GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Capture(originalPrefab,null,null,"Before",output);
        Capture(originalPrefab,mesh,material,"After",output);
        var contents=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var target=Body(contents);target.sharedMesh=mesh;target.sharedMaterial=material;
            PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var report=new Report { bodyVertices=mesh.vertexCount,bodyTriangles=triangles.Length/3,bones=original.bones.Length,materials=1,
            maximumRestLandmarkError=restError,originalBoundsError=boundsError,protectedDccHash=data.protectedBefore,
            bindingSpace=conversion.ToString("G9"),sourceGeometry=geometryPath,
            geometrySha256=BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(geometryPath))).Replace("-", "").ToLowerInvariant(),reusedOriginalBindposes=true,reusedOriginalBoneArray=true,noAnimationWrites=true,noPhysicsSimulation=true };
        File.WriteAllText(output+"/native-authoring-report.json",JsonUtility.ToJson(report,true));
        Debug.Log("SCOUT_MODEL_OVERHAUL_NATIVE "+JsonUtility.ToJson(report));
    }
    static void Capture(GameObject prefab,Mesh replacement,Material material,string prefix,string output)
    {
        var go=UnityEngine.Object.Instantiate(prefab);go.transform.localScale=Vector3.one*2;
        var animator=go.GetComponent<Animator>();animator.enabled=false;
        var body=Body(go);if(replacement!=null){body.sharedMesh=replacement;body.sharedMaterial=material;}
        foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        var cameraGo=new GameObject("Owned model camera");var camera=cameraGo.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.13f,.16f);
        camera.cullingMask=1<<31;camera.farClipPlane=50;camera.allowHDR=false;
        var lights=new List<GameObject>();
        for(int i=0;i<3;i++)
        {
            var lightGo=new GameObject("Owned model light");lights.Add(lightGo);
            var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<31;
            light.intensity=i==0?1.05f:i==1?.38f:.65f;
            lightGo.transform.rotation=Quaternion.Euler(i==0?35:20,i==0?-35:i==1?80:165,0);
        }
        RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.34f,.34f,.34f);
        string[] names={"Front","ThreeQuarter","Back","Face","Run","Flight","Sword"};
        string[] clips={"Idle","Idle","Idle","Idle","Run","GasBoth","AttackAir"};
        Vector3[] positions={new Vector3(0,2.25f,7),new Vector3(4,2.8f,7),new Vector3(-3,2.6f,-7),new Vector3(.4f,3.45f,4),new Vector3(4,2.8f,7),new Vector3(4,2.8f,7),new Vector3(4,2.8f,7)};
        try
        {
            for(int i=0;i<names.Length;i++)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/Scout/Animations/"+clips[i]+".anim");
                clip.SampleAnimation(go,i==4?.18f:i==5?.2f:i==6?.14f:0);
                var baked=new List<UnityEngine.Object>();
                foreach(var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var posed=new Mesh();sk.BakeMesh(posed);
                    var proxy=new GameObject("Owned static skin bake");proxy.layer=31;proxy.transform.SetParent(sk.transform,false);
                    Vector3 scale=sk.transform.lossyScale;proxy.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                    proxy.AddComponent<MeshFilter>().sharedMesh=posed;proxy.AddComponent<MeshRenderer>().sharedMaterials=sk.sharedMaterials;
                    sk.enabled=false;baked.Add(proxy);baked.Add(posed);
                }
                Vector3 target=i==3?Find(go,"Head").position+Vector3.up*.20f:new Vector3(0,1.85f,0);
                camera.transform.position=i==3?target+new Vector3(.4f,.08f,4):positions[i];camera.transform.LookAt(target);
                camera.orthographicSize=i==3?.40f:2.15f;
                int width=i==3?1000:900,height=i==3?1000:1100;
                var rt=new RenderTexture(width,height,24);var previous=RenderTexture.active;
                try
                {
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
                    File.WriteAllBytes(output+"/"+prefix+"_"+names[i]+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
                }
                finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);}
                foreach(var o in baked)UnityEngine.Object.DestroyImmediate(o);
                foreach(var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))sk.enabled=true;
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cameraGo);
            foreach(var light in lights)UnityEngine.Object.DestroyImmediate(light);
        }
    }
}
