"""Static native authoring previews of the actual peripheral-streak Graphic.
Closed marker-owned Unity2021 worker only: no Play/input/physics/prefs/scene saves.
Refresh current sources with the existing validation runner before capturing.
Backgrounds are existing saved map photos, not a live gameplay/FOV demonstration.
"""
from pathlib import Path
import json, hashlib, os, shutil, subprocess
r = Path(__file__).resolve().parents[1]
w = Path(json.loads((r / 'Library/TitanValidationRun.json').read_text())['project'])
if (w / '.titan-validation-owned').read_text() != str(r):
    raise SystemExit('Not this project\'s owned worker')
lock = w / 'Temp/UnityLockfile'
if lock.exists():
    with lock.open('r+b'):
        pass
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
inputs = {p: sha(r / p) for p in ['Assets/ThirdPersonCameraController.cs',
          'Assets/Player/Scout/PlayerSpeedStreaks.cs', 'Assets/UI/FrontendUI.cs']}
if not all(sha(w / p) == h for p, h in inputs.items()):
    raise SystemExit('Refresh current worker sources first')
helper = r'''using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class OwnedSpeedFeedbackPreview
{
 public static void Render()
 {
  if(!Application.isBatchMode||Application.isPlaying||!File.Exists(".titan-validation-owned")||Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Owned2021 static art only");
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  string output=Environment.GetEnvironmentVariable("ODM_SPEED_ART_OUTPUT");Directory.CreateDirectory(output);
  foreach(bool town in new[]{false,true})
  {
   Capture(output,town,0,Vector3.forward,1920,1080,"Off");
   Capture(output,town,1,Vector3.forward,1920,1080,"Forward");
  }
  Capture(output,true,1,Vector3.right,1440,1080,"Lateral");
  Debug.Log("SPEED_ART_COMPLETE device="+SystemInfo.graphicsDeviceName+" static=true play=false physicsStepped=false sceneSaved=false prefsWritten=false");
 }
 static void Setup(Canvas canvas,Camera camera)
 {
  var scaler=canvas.GetComponent<CanvasScaler>();if(scaler!=null)scaler.enabled=false;
  canvas.scaleFactor=1;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
 }
 static void Capture(string output,bool town,float strength,Vector3 direction,int width,int height,string name)
 {
  var camera=new GameObject("Owned native streak-art camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=FrontendUI.MinimalPanel;
  camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=100;
  var background=FrontendUI.Canvas(null,"Actual saved map-photo backdrop",-200,false);
  FrontendUI.Photo(background,"Map plate",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Frontend/"+(town?"Shiganshina":"Forest")+"Backdrop.png"),Color.white);
  var graphic=PlayerSpeedStreaks.Create(camera);
  // Art controls only: sample the visual at a fixed phase, no gameplay actor,
  // root displacement, speed observer, camera controller or native simulation.
  for(int i=0;i<19;i++)graphic.Present(strength,direction,1f/60,.16f,.14f,new Rect(0,0,1,1));
  var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;
  Setup(background.GetComponent<Canvas>(),camera);Setup(graphic.Overlay,camera);
  Canvas.ForceUpdateCanvases();camera.Render();
  var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
  image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
  string file=(town?"Shiganshina":"Forest")+"_"+name+"_"+width+"x"+height+".png";
  File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());
  RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
  UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(graphic.Overlay.gameObject);
  UnityEngine.Object.DestroyImmediate(background.gameObject);UnityEngine.Object.DestroyImmediate(camera.gameObject);
  Debug.Log("SPEED_ART "+file+" strength="+strength+" direction="+direction+" edgeBand=.14 maxOpacity=.16");
 }
}'''
(w / 'Assets/Editor/OwnedSpeedFeedbackPreview.cs').write_text(helper)
out = r / 'ArtSource/Camera/SpeedFeedback'
out.mkdir(parents=True, exist_ok=True)
log = w / 'speed-feedback-art.log'
env = dict(os.environ, ODM_SPEED_ART_OUTPUT=str(out))
result = subprocess.run(['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe', '-batchmode', '-quit',
                         '-projectPath', str(w), '-executeMethod', 'OwnedSpeedFeedbackPreview.Render', '-logFile', str(log)],
                        env=env, stdin=subprocess.DEVNULL, timeout=240)
text = log.read_text(errors='replace')
for line in text.splitlines():
    if any(x in line for x in ['SPEED_ART', 'error CS', 'Exception:']):
        print(line)
if result.returncode != 0 or 'SPEED_ART_COMPLETE' not in text:
    raise SystemExit(str(log))
if not all(sha(r / p) == h for p, h in inputs.items()):
    raise SystemExit('Source changed during art capture; inspect without overwriting it')
shutil.copy2(log, out / 'native-art.log')
(out / 'source-art-summary.json').write_text(json.dumps({'sourceHashes': inputs, 'nativeStaticArt': True,
    'background': 'Existing saved frontend map-photo sprites', 'gameplayOrInputTested': False,
    'fovPerspectiveDemonstrated': False, 'physicsStepped': False, 'prefsWritten': False,
    'sizes': [[1920, 1080], [1440, 1080]], 'appearance': {'edgeBand': .14, 'maximumOpacity': .16, 'peakStrength': 1}}, indent=2))
print('Static native speed-streak art:', out)
