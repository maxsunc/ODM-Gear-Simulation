"""Native static uGUI art review in a closed, marker-owned Unity2021 worker. No Play, input, physics automation or tests."""
from pathlib import Path
import hashlib, json, os, shutil, subprocess
r=Path(__file__).resolve().parents[1]
w=Path(json.loads((r/'Library/TitanValidationRun.json').read_text())['project'])
assert (w/'.titan-validation-owned').read_text()==str(r)
lock=w/'Temp/UnityLockfile'
if lock.exists():
    with lock.open('r+b'): pass
state=json.loads((r/'Library/FrontendMenuRun.json').read_text())
assert state['authored'] and state['worker']==str(w)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert (w/'Assets/Scenes/Menu.unity').is_file() and sha(w/'Assets/Scenes/Menu.unity')==state['menuOutputHash'],'Owned snapshot replaced; rebuild with --revise before recapture'
assert all(sha(r/f)==h for f,h in state['inputs'].items()),'Source changed after authoring'
helper=r'''using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class OwnedFrontendPreview
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(object owner,string name) {return (T)owner.GetType().GetField(name,Flags).GetValue(owner);}
 public static void Render()
 {
  if(!File.Exists(".titan-validation-owned")||Application.unityVersion!="2021.3.25f1"||Application.isPlaying)throw new InvalidOperationException("Owned2021 authoring only");
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
  string output=Environment.GetEnvironmentVariable("ODM_FRONTEND_OUTPUT");Directory.CreateDirectory(output);
  EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity",OpenSceneMode.Single);
  var menu=UnityEngine.Object.FindObjectOfType<GameMenu>();
  typeof(GameMenu).GetMethod("SyncOptions",Flags).Invoke(menu,null); // Reads settings, never invokes slider callbacks.
  var canvas=Field<RectTransform>(menu,"canvasRoot").GetComponent<Canvas>();
  Capture(canvas,Field<RectTransform>(menu,"design"),output,"Menu_FreeRoam_1920x1080",1920,1080);
  menu.SelectDestination(true);
  Capture(canvas,Field<RectTransform>(menu,"design"),output,"Menu_Shiganshina_1920x1080",1920,1080);
  Capture(canvas,Field<RectTransform>(menu,"design"),output,"Menu_Shiganshina_1440x1080",1440,1080);
  menu.SelectDestination(false);menu.ShowOptions();
  Capture(canvas,Field<RectTransform>(menu,"design"),output,"Menu_Options_1920x1080",1920,1080);
  Capture(canvas,Field<RectTransform>(menu,"design"),output,"Menu_Options_1440x1080",1440,1080);
  menu.gameObject.SetActive(false);
  // Author-only zero-value plate of the ACTUAL runtime overlay. No invented kill receipts or persistent writes.
  var backdrop=FrontendUI.Canvas(null,"Art review background",0,false);
  FrontendUI.Photo(backdrop,"Forest",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Frontend/ForestBackdrop.png"),new Color(.6f,.7f,.6f));
  var stats=new GameObject("Stats art authoring").AddComponent<GameStats>();
  typeof(GameStats).GetMethod("BuildOverlay",Flags).Invoke(stats,null);
  foreach(string field in new[]{"killsValue","totalKillsValue","damageValue","limbsValue"})Field<Text>(stats,field).text="0";
  var overlay=Field<RectTransform>(stats,"overlay");overlay.gameObject.SetActive(true);
  Capture(overlay.GetComponent<Canvas>(),Field<RectTransform>(stats,"design"),output,"Stats_1920x1080",1920,1080,backdrop.GetComponent<Canvas>());
  Debug.Log("FRONTEND_NATIVE_ART_COMPLETED device="+SystemInfo.graphicsDeviceName+" staticUGUI=true play=false prefsWritten=false");
 }
 static void Capture(Canvas canvas,RectTransform design,string output,string name,int width,int height,Canvas background=null)
 {
  var camera=new GameObject("Owned UI render camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=FrontendUI.Ink;
  camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=100;
  var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;
  Setup(canvas,camera);if(background!=null)Setup(background,camera);
  Canvas.ForceUpdateCanvases();FrontendUI.Fit(design);Canvas.ForceUpdateCanvases();camera.Render();
  var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
  image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
  RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);
  Debug.Log("FRONTEND_NATIVE_ART image="+name+" activeText="+canvas.GetComponentsInChildren<Text>().Length);
 }
 static void Setup(Canvas canvas,Camera camera)
 {
  canvas.GetComponent<CanvasScaler>().enabled=false;canvas.scaleFactor=1;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
 }
}
'''
(w/'Assets/Editor/OwnedFrontendPreview.cs').write_text(helper)
out=r/'ArtSource/UI/Frontend';out.mkdir(parents=True,exist_ok=True)
log=w/'frontend-preview.log';env=dict(os.environ,ODM_FRONTEND_OUTPUT=str(out))
result=subprocess.run(['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe','-quit','-projectPath',str(w),'-executeMethod','OwnedFrontendPreview.Render','-logFile',str(log)],env=env,stdin=subprocess.DEVNULL,timeout=240)
text=log.read_text(errors='replace')
for line in text.splitlines():
    if any(x in line for x in ['FRONTEND_','error CS','Exception:']):print(line)
assert result.returncode==0 and 'FRONTEND_NATIVE_ART_COMPLETED' in text,str(log)
shutil.copy2(log,out/'native-ui-preview.log')
print('Native static frontend art:',out)
