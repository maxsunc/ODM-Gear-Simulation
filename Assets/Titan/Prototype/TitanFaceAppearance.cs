using System;
using UnityEngine;

/// <summary>One-time render-only face choice. No animation, bone, physics or HP ownership.</summary>
[DisallowMultipleComponent]
public sealed class TitanFaceAppearance : MonoBehaviour
{
    public int VariantIndex { get; private set; } // 0 original, 1 bald/bearded, 2 blonde/smiling
    SkinnedMeshRenderer head,jaw;
    Mesh originalHead,originalJaw;
    Material[] originalHeadMaterials,originalJawMaterials;
    static System.Random random;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRandom(){random=new System.Random(Environment.TickCount);}
    public static void Install(ModularTitanBody body)
    {
        if(body==null||body.GetComponent<TitanFaceAppearance>()!=null)return;
        var palette=Resources.Load<TitanFacePalette>("TitanFacePalette");if(palette==null||palette.faces.Length==0)return;
        if(random==null)ResetRandom();
        body.gameObject.AddComponent<TitanFaceAppearance>().SetVariant(random.Next(palette.faces.Length+1),palette);
    }
    public void SetVariant(int index,TitanFacePalette palette)
    {
        if(head==null||jaw==null)
        {
            foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(skin.name=="Titan_Head")head=skin;
                else if(skin.name=="Titan_Jaw")jaw=skin;
            }
            if(head==null||jaw==null)return;
            originalHead=head.sharedMesh;originalJaw=jaw.sharedMesh;
            originalHeadMaterials=head.sharedMaterials;originalJawMaterials=jaw.sharedMaterials;
        }
        if(index<0||palette==null||index>palette.faces.Length)return;
        var face=index==0?null:palette.faces[index-1];
        if(face!=null&&(face.head==null||face.jaw==null))return;
        head.sharedMesh=face==null?originalHead:face.head;jaw.sharedMesh=face==null?originalJaw:face.jaw;
        head.sharedMaterials=face==null||face.material==null?originalHeadMaterials:Materials(face.material,head.sharedMesh.subMeshCount);
        jaw.sharedMaterials=face==null||face.material==null?originalJawMaterials:Materials(face.material,jaw.sharedMesh.subMeshCount);
        head.localBounds=head.sharedMesh.bounds;jaw.localBounds=jaw.sharedMesh.bounds;VariantIndex=index;
    }
    static Material[] Materials(Material material,int count)
    {
        var slots=new Material[count];for(int i=0;i<count;i++)slots[i]=material;return slots;
    }
}
