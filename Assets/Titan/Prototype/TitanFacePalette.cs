using System;
using UnityEngine;

/// <summary>Face-only skins on the unchanged Titan skeleton; optional identity atlas material.</summary>
public sealed class TitanFacePalette : ScriptableObject
{
    [Serializable] public sealed class Face
    {
        public string name;
        public Mesh head,jaw;
        public Material material;
    }
    public Face[] faces=new Face[0];
}
