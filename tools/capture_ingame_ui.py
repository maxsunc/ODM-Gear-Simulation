"""Render actual runtime Stats/J-menu visuals in a CLOSED owned Unity2021 worker.
Static authoring only: no Play, input, physics, source scene saves or prefs writes.
Run the existing scoped validation runner first to refresh/compile current UI code.
"""
from pathlib import Path
import json,hashlib,os,shutil,subprocess
r=Path(__file__).resolve().parents[1];w=Path(json.loads((r/'Library/TitanValidationRun.json').read_text())['project'])
assert (w/'.titan-validation-owned').read_text()==str(r)
lock=w/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inputs={p.relative_to(r).as_posix():sha(p) for p in (r/'Assets/UI').rglob('*') if p.is_file()}
assert all(sha(w/f)==h for f,h in inputs.items()),'Refresh current worker source before art capture'
helper=r'''using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class OwnedIngamePreview
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(object owner,string name){return (T)owner.GetType().GetField(name,Flags).GetValue(owner);}
 public static void Render()
 {
  if(!Application.isBatchMode||Application.isPlaying||!File.Exists(".titan-validation-owned")||Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Owned2021 static art only");
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  string output=Environment.GetEnvironmentVariable("ODM_INGAME_OUTPUT");Directory.CreateDirectory(output);
  foreach(bool town in new[]{false,true})
  {
   string map=town?"Shiganshina":"GiantForest";
   var background=FrontendUI.Canvas(null,"Art background",0,false);
   FrontendUI.Photo(background,"Actual saved map plate",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Frontend/"+(town?"Shiganshina":"Forest")+"Backdrop.png"),Color.white);
   var stats=new GameObject("Static Stats art").AddComponent<GameStats>();typeof(GameStats).GetMethod("BuildOverlay",Flags).Invoke(stats,null);
   var statsRoot=Field<RectTransform>(stats,"overlay");statsRoot.gameObject.SetActive(true);
   Capture(statsRoot.GetComponent<Canvas>(),Field<RectTransform>(stats,"design"),background.GetComponent<Canvas>(),output,"Stats_"+map,1920,1080);
   Capture(statsRoot.GetComponent<Canvas>(),Field<RectTransform>(stats,"design"),background.GetComponent<Canvas>(),output,"Stats_"+map,1440,1080);
   statsRoot.gameObject.SetActive(false);
   var pause=new GameObject("Static J menu art").AddComponent<GamePauseMenu>();pause.CreateVisuals(town?"Shiganshina":"Giant Forest");
   var pauseRoot=Field<RectTransform>(pause,"canvasRoot");pauseRoot.gameObject.SetActive(true);
   Capture(pauseRoot.GetComponent<Canvas>(),Field<RectTransform>(pause,"design"),background.GetComponent<Canvas>(),output,"Pause_"+map,1920,1080);
   Capture(pauseRoot.GetComponent<Canvas>(),Field<RectTransform>(pause,"design"),background.GetComponent<Canvas>(),output,"Pause_"+map,1440,1080);
   UnityEngine.Object.DestroyImmediate(pause.gameObject);UnityEngine.Object.DestroyImmediate(stats.gameObject);UnityEngine.Object.DestroyImmediate(background.gameObject);
  }
  Debug.Log("INGAME_UI_NATIVE_COMPLETE device="+SystemInfo.graphicsDeviceName+" static=true play=false physicsStepped=false prefsWritten=false");
 }
 static void Setup(Canvas c,Camera camera)
 {c.GetComponent<CanvasScaler>().enabled=false;c.scaleFactor=1;c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
 static void Capture(Canvas foreground,RectTransform design,Canvas background,string output,string name,int width,int height)
 {
  var camera=new GameObject("Owned native UI camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=FrontendUI.MinimalPanel;camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=100;
  var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;
  Setup(background,camera);Setup(foreground,camera);Canvas.ForceUpdateCanvases();FrontendUI.Fit(design);Canvas.ForceUpdateCanvases();camera.Render();
  var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
  File.WriteAllBytes(Path.Combine(output,name+"_"+width+"x"+height+".png"),image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
  UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);
  Debug.Log("INGAME_UI_NATIVE "+name+" size="+width+"x"+height+" activeText="+foreground.GetComponentsInChildren<Text>().Length);
 }
}'''
(w/'Assets/Editor/OwnedIngamePreview.cs').write_text(helper)
out=r/'ArtSource/UI/InGameMinimal';out.mkdir(parents=True,exist_ok=True)
log=w/'ingame-ui-preview.log';env=dict(os.environ,ODM_INGAME_OUTPUT=str(out))
result=subprocess.run(['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe','-batchmode','-quit','-projectPath',str(w),'-executeMethod','OwnedIngamePreview.Render','-logFile',str(log)],env=env,stdin=subprocess.DEVNULL,timeout=240)
t=log.read_text(errors='replace')
for line in t.splitlines():
 if any(x in line for x in ['INGAME_UI_','error CS','Exception:']):print(line)
assert result.returncode==0 and 'INGAME_UI_NATIVE_COMPLETE' in t,str(log)
assert all(sha(r/f)==h for f,h in inputs.items()),'Source changed during authoring'
shutil.copy2(log,out/'native-ui-preview.log')
(out/'source-art-summary.json').write_text(json.dumps({'sourceHashes':inputs,'nativeStaticArt':True,'gameplayInputTested':False,'prefsWritten':False,'sizes':[[1920,1080],[1440,1080]],'maps':['GiantForest','Shiganshina']},indent=2))
print('Native UI art:',out)
