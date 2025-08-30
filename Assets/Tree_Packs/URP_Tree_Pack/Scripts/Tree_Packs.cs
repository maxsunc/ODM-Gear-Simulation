#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class Startup
{
    static Startup()    
    {
        EditorPrefs.SetInt("showCounts_URPTreeModels", EditorPrefs.GetInt("showCounts_URPTreeModels") + 1);

        if (EditorPrefs.GetInt("showCounts_URPTreeModels") == 1)       
        {
            Application.OpenURL("https://assetstore.unity.com/publishers/92929");
            // System.IO.File.Delete("Assets/SportCar/Racing_Game.cs");
        }
    }     
}
#endif
