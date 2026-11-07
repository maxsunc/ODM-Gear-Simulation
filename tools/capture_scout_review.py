"""Capture static authored poses in the real Unity Editor; no physics stepping.
Requires local titan_mcp_review.py client from the active authoring session.
"""
import json,subprocess,sys,tempfile
from pathlib import Path
code=r'''
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab");
var go=(UnityEngine.GameObject)UnityEngine.Object.Instantiate(prefab);go.name="Scout static review";go.transform.position=new UnityEngine.Vector3(0,1000,0);go.transform.localScale=UnityEngine.Vector3.one*2;
var objs=new System.Collections.Generic.List<UnityEngine.Object>();objs.Add(go);
var camObj=new UnityEngine.GameObject("Scout static camera");objs.Add(camObj);var camera=camObj.AddComponent<UnityEngine.Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3.35f;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.09f,.12f,.15f);camera.cullingMask=1<<31;camera.farClipPlane=50;
var oldFog=UnityEngine.RenderSettings.fog;var oldAmbient=UnityEngine.RenderSettings.ambientLight;var oldMode=UnityEngine.RenderSettings.ambientMode;
var rt=new UnityEngine.RenderTexture(650,850,24);objs.Add(rt);var oldRT=UnityEngine.RenderTexture.active;
try {
UnityEngine.RenderSettings.fog=false;UnityEngine.RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;UnityEngine.RenderSettings.ambientLight=new UnityEngine.Color(.42f,.42f,.42f);
foreach(var t in go.GetComponentsInChildren<UnityEngine.Transform>())t.gameObject.layer=31;
for(int i=0;i<2;i++){var l=new UnityEngine.GameObject("Scout studio light");objs.Add(l);var light=l.AddComponent<UnityEngine.Light>();light.type=UnityEngine.LightType.Directional;light.intensity=i==0?1.25f:.7f;light.cullingMask=1<<31;l.transform.rotation=UnityEngine.Quaternion.Euler(i==0?35:15,i==0?-35:145,0);}
go.GetComponent<UnityEngine.Animator>().enabled=false;
System.IO.Directory.CreateDirectory("Library/ScoutReview");
var names=new System.Collections.Generic.List<string>{"Idle","Run","GasBoth","Fall","AttackGround","AttackAir"};var times=new System.Collections.Generic.List<float>{0,.12f,.2f,0,.13f,.13f};
for(int k=0;k<16;k++){names.Add("AttackAir");times.Add(.39f*k/15);}
for(int i=0;i<names.Count;i++){
var clip=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Art/Player/Scout/Animations/"+names[i]+".anim");clip.SampleAnimation(go,times[i]);
var bakedObjects=new System.Collections.Generic.List<UnityEngine.Object>();
foreach(var sk in go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>()){
var mesh=new UnityEngine.Mesh();sk.BakeMesh(mesh);var proxy=new UnityEngine.GameObject("Static pose bake");proxy.layer=31;proxy.transform.SetParent(sk.transform,false);var scale=sk.transform.lossyScale;proxy.transform.localScale=new UnityEngine.Vector3(1/scale.x,1/scale.y,1/scale.z);proxy.AddComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;proxy.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial=sk.sharedMaterial;sk.enabled=false;bakedObjects.Add(proxy);bakedObjects.Add(mesh);
}
camera.transform.position=new UnityEngine.Vector3(5.2f,1003.3f,8);camera.transform.LookAt(new UnityEngine.Vector3(0,1002,0));camera.targetTexture=rt;camera.Render();UnityEngine.RenderTexture.active=rt;
var tex=new UnityEngine.Texture2D(650,850,UnityEngine.TextureFormat.RGB24,false);tex.ReadPixels(new UnityEngine.Rect(0,0,650,850),0,0);tex.Apply();System.IO.File.WriteAllBytes("Library/ScoutReview/"+(i<6?names[i]:"Swing_"+(i-6).ToString("D2"))+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
foreach(var o in bakedObjects)UnityEngine.Object.DestroyImmediate(o);foreach(var sk in go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>())sk.enabled=true;
}
return "Captured six authored poses +16 attack frames; no physics stepping";
} finally {camera.targetTexture=null;UnityEngine.RenderTexture.active=oldRT;UnityEngine.RenderSettings.fog=oldFog;UnityEngine.RenderSettings.ambientLight=oldAmbient;UnityEngine.RenderSettings.ambientMode=oldMode;foreach(var o in objs)if(o!=null)UnityEngine.Object.DestroyImmediate(o);}
'''
client=Path(tempfile.gettempdir())/'titan_mcp_review.py'
r=subprocess.run([sys.executable,str(client),'tools/call',json.dumps({'name':'execute_code','arguments':{'action':'execute','code':code}})],check=True)
