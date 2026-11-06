using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Create NEW face meshes/catalog only. Never rebuild/save the Titan prefab or scenes.</summary>
public static class TitanFaceVariantBuilder
{
    const string Folder="Assets/Art/Titans/FaceVariants";
    public static void BuildOwned()
    {
        if(!Application.isBatchMode||!File.Exists(".titan-validation-owned")||Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Owned Unity2021 worker only");
        Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        if(AssetDatabase.LoadAssetAtPath<TitanFacePalette>("Assets/Resources/TitanFacePalette.asset")!=null&&!ReplaceOwned)throw new InvalidOperationException("Explicit owned revision required");
        var palette=ScriptableObject.CreateInstance<TitanFacePalette>();palette.faces=new TitanFacePalette.Face[2];
        string[] names={"Broad","Gaunt"};
        for(int i=0;i<names.Length;i++)
        {
            string name=names[i],path=Folder+"/TitanFace_"+name+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            palette.faces[i]=new TitanFacePalette.Face{name=i==0?"Bald with gray beard":"Blonde smiling",material=MakeMaterial(i==0?"BaldBeard":"BlondeSmile"),head=MakeMesh(skins.Single(s=>s.name.EndsWith("_Head")),name,"Head"),jaw=MakeMesh(skins.Single(s=>s.name.EndsWith("_Jaw")),name,"Jaw")};
        }
        string output="Assets/Resources/TitanFacePalette.asset";
        SaveOwned(palette,output);AssetDatabase.Refresh();
        Debug.Log("TITAN_FACE_CATALOG_CREATED unchangedRigAnimationPhysics=true baldBeardAndBlondeSmile=2");
    }
    static bool ReplaceOwned=>Environment.GetEnvironmentVariable("ODM_FACE_REBUILD_OWNED")=="1";
    static void SaveOwned(UnityEngine.Object value,string path)
    {
        var old=AssetDatabase.LoadMainAssetAtPath(path);
        if(old!=null)
        {
            if(!ReplaceOwned)throw new InvalidOperationException("Refusing existing face output without explicit owned revision: "+path);
            if(value is Mesh)
            {
                // Native mesh buffers cannot be replaced with CopySerialized when topology changes.
                string guid=AssetDatabase.AssetPathToGUID(path);AssetDatabase.CreateAsset(value,path);AssetDatabase.SaveAssetIfDirty(value);
                if(AssetDatabase.AssetPathToGUID(path)!=guid)throw new InvalidOperationException("Face mesh GUID changed during owned rebuild: "+path);
            }
            else{EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);}
        }
        else{AssetDatabase.CreateAsset(value,path);AssetDatabase.SaveAssetIfDirty(value);}
    }
    static Material MakeMaterial(string identity)
    {
        string texturePath=Folder+"/Textures/"+identity+"_BaseColor.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
        var original=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="Titan_Head").sharedMaterial;
        var material=new Material(original);material.name="TitanFace_"+identity;
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);material.SetTexture("_BaseMap",texture);material.SetTexture("_MainTex",texture);
        string output=Folder+"/Materials/"+material.name+".mat";SaveOwned(material,output);return AssetDatabase.LoadAssetAtPath<Material>(output);
    }
    public static void PreviewOwned()
    {
        if(!File.Exists(".titan-validation-owned")||Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Owned Unity2021 worker only");
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
        var previous=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;
        var data=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
        var pipeline=UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(data);
        var preview=new PreviewRenderUtility();
        try
        {
            UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;
            var palette=AssetDatabase.LoadAssetAtPath<TitanFacePalette>("Assets/Resources/TitanFacePalette.asset");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
            for(int i=0;i<3;i++)
            {
                var go=UnityEngine.Object.Instantiate(prefab);go.transform.position=new Vector3((1-i)*2.7f,0,0);go.transform.localScale=Vector3.one;
                foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;
                foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
                TitanFaceAppearance.Install(go.GetComponent<ModularTitanBody>());
                var appearance=go.GetComponent<TitanFaceAppearance>();if(appearance==null)throw new InvalidOperationException("Runtime face installation failed");
                appearance.SetVariant(i,palette);
                if(Environment.GetEnvironmentVariable("ODM_FACE_JAW_PREVIEW")=="1")
                {
                    var jaw=go.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Jaw");
                    jaw.rotation=Quaternion.AngleAxis(28,go.transform.right)*jaw.rotation; // Same axis/angle as the existing bite pose.
                }
                preview.AddSingleGO(go);
                Debug.Log("TITAN_FACE_PREVIEW index="+appearance.VariantIndex+" skins="+go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length+" bodies="+go.GetComponentsInChildren<Rigidbody>(true).Length+" joints="+go.GetComponentsInChildren<CharacterJoint>(true).Length);
            }
            preview.camera.transform.position=new Vector3(0,10.65f,14);preview.camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,10.65f,0)-preview.camera.transform.position);
            preview.camera.orthographic=true;preview.camera.orthographicSize=1.65f;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.15f,.18f);
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,150,0);preview.lights[1].intensity=.8f;
            preview.ambientColor=new Color(.38f,.38f,.38f);
            preview.BeginStaticPreview(new Rect(0,0,1800,700));preview.Render(true);var image=preview.EndStaticPreview();
            Directory.CreateDirectory("Library/TitanFaceReview");
            string filename=Environment.GetEnvironmentVariable("ODM_FACE_JAW_PREVIEW")=="1"?"Faces_Unity_JawOpen.png":"Faces_Unity_Lineup.png";
            File.WriteAllBytes("Library/TitanFaceReview/"+filename,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            Debug.Log("TITAN_FACE_NATIVE_PREVIEW_OK device="+SystemInfo.graphicsDeviceName);
        }
        finally{preview.Cleanup();UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=previous;UnityEngine.Object.DestroyImmediate(pipeline);}
    }
    static Mesh MakeMesh(SkinnedMeshRenderer source,string name,string part)
    {
        var original=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Titans/Prototype/Meshes/Titan_"+part+".asset");
        var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);var weights=mesh.boneWeights;var used=weights.Where(w=>w.weight0>0).Select(w=>w.boneIndex0).Distinct().ToArray();
        if(used.Length!=1||source.bones[used[0]].name!=part||weights.Any(w=>Mathf.Abs(w.weight0-1)>.00001f||w.weight1>0||w.weight2>0||w.weight3>0))throw new InvalidOperationException("Unexpected face skin bindings: "+name+part);
        Matrix4x4 bind=mesh.bindposes[used[0]];
        if(original.bindposes.Length!=1)throw new InvalidOperationException("Base face is not single-bone");
        for(int r=0;r<4;r++)for(int c=0;c<4;c++)if(Mathf.Abs(bind[r,c]-original.bindposes[0][r,c])>.0001f)throw new InvalidOperationException("Face rest bind differs from approved "+part+" at"+r+","+c+": "+bind[r,c]+" vs "+original.bindposes[0][r,c]);
        for(int j=0;j<weights.Length;j++){var w=weights[j];w.boneIndex0=w.boneIndex1=w.boneIndex2=w.boneIndex3=0;weights[j]=w;}
        mesh.boneWeights=weights;mesh.bindposes=new[]{bind};mesh.name="TitanFace_"+name+"_"+part;
        string path=Folder+"/Meshes/"+mesh.name+".asset";
        int count=mesh.vertexCount,slots=mesh.subMeshCount;SaveOwned(mesh,path);
        Debug.Log("TITAN_FACE_MESH "+name+" "+part+" vertices="+count+" original="+original.vertexCount+" bind="+part+" slots="+slots);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }
}
