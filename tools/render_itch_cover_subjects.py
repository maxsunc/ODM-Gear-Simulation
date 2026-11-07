"""Native posed promotional subjects in a CLOSED owned Unity2021 worker.
No Play/input/native physics, no scene/assets saved, no live Editor commands.
Neutral studio backgrounds support silhouette extraction for editable posters.
"""
from pathlib import Path
import json,hashlib,os,subprocess,shutil
R=Path(__file__).resolve().parents[1]
W=Path(json.loads((R/'Library/ShiganshinaBuildRun.json').read_text())['worker'])
assert (W/'.shiganshina-owned').read_text()==str(R)
lock=W/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
OUT=R/'ArtSource/Marketing/ItchCovers';OUT.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
state=R/'Library/ItchCoverRun.json'
baseline={f.relative_to(R).as_posix():sha(f) for folder in ['Assets','ProjectSettings'] for f in (R/folder).rglob('*') if f.is_file()}
state.write_text(json.dumps({'worker':str(W),'protected':baseline},indent=2))
# Current approved art only. Never rebuild prefabs/importers in source.
for folder in ['Assets/Art/Player/Scout','Assets/Prefabs/Player','Assets/Art/Titans/Prototype','Assets/Art/Titans/FaceVariants','Assets/Prefabs/Titans']:
 shutil.copytree(R/folder,W/folder,dirs_exist_ok=True)
for rel in ['Assets/Resources/TitanFacePalette.asset','Assets/Resources/TitanFacePalette.asset.meta']:
 shutil.copy2(R/rel,W/rel)
helper=r'''using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
public static class OwnedItchSubjects
{
 static string output;
 public static void Render()
 {
  if(!File.Exists(".shiganshina-owned")||!Application.isBatchMode||Application.isPlaying||Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Owned static2021 worker only");
  if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
  output=Environment.GetEnvironmentVariable("ODM_ITCH_OUTPUT");Directory.CreateDirectory(output);
  var original=GraphicsSettings.renderPipelineAsset;var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");var pipeline=UniversalRenderPipelineAsset.Create(renderer);
  try
  {
   GraphicsSettings.renderPipelineAsset=pipeline;
   Scout("Scout_Slash","AttackAir",.14f,new Vector3(3,1.6f,5));
   Scout("Scout_Flight","GasBoth",.2f,new Vector3(-4,1.6f,5));
   Scout("Scout_BackFlight","GasBoth",.2f,new Vector3(3,2.4f,-5));
   Scout("Scout_Guard","ChargeAir",.08f,new Vector3(-3,1.5f,5));
   Titan("Titan_Beard",1,13,new Vector3(3,10.6f,13));
   Titan("Titan_Blonde",2,23,new Vector3(-3,10.6f,13));
  }
  finally{GraphicsSettings.renderPipelineAsset=original;Object.DestroyImmediate(pipeline);}
  Debug.Log("ITCH_SUBJECTS_COMPLETE native="+SystemInfo.graphicsDeviceName+" play=false nativePhysicsStepped=false sourceSaved=false");
 }
 static void Setup(PreviewRenderUtility p)
 {
  p.camera.orthographic=true;p.camera.nearClipPlane=.05f;p.camera.farClipPlane=100;p.camera.clearFlags=CameraClearFlags.SolidColor;
  p.lights[0].intensity=1.65f;p.lights[0].color=new Color(1,.91f,.78f);p.lights[0].transform.rotation=Quaternion.Euler(32,145,0);
  p.lights[1].intensity=.95f;p.lights[1].color=new Color(.55f,.75f,1);p.lights[1].transform.rotation=Quaternion.Euler(15,-45,0);p.ambientColor=new Color(.43f,.43f,.43f);
 }
 static void Bake(GameObject go)
 {
  foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
  {
   if(!skin.enabled)continue;
   var mesh=new Mesh();skin.BakeMesh(mesh);var proxy=new GameObject("Promotional static skin snapshot");proxy.transform.SetParent(skin.transform,false);
   var scale=skin.transform.lossyScale;proxy.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
   proxy.AddComponent<MeshFilter>().sharedMesh=mesh;proxy.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
  }
 }
 static Bounds Bounds(GameObject go)
 {
  var renderers=go.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;
 }
 static void Scout(string name,string clipName,float time,Vector3 view)
 {
  var p=new PreviewRenderUtility();var host=new GameObject("Owned static Scout illustration");p.AddSingleGO(host);
  try
  {
   Setup(p);var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"),host.transform);
   foreach(var a in go.GetComponentsInChildren<Animator>())a.enabled=false;
   var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/Scout/Animations/"+clipName+".anim");clip.SampleAnimation(go,time);
   // Cosmetic fabric preview only; no native physics/character/motor is installed.
   var cape=host.AddComponent<ScoutCapeCloth>();cape.automaticSimulation=false;
   if(!cape.Initialize(go.transform))throw new InvalidOperationException(cape.Error);
   cape.ambientWind=new Vector3(6,2,-9);for(int i=0;i<90;i++)cape.Simulate(1/120f);
   Bake(go);var b=Bounds(host);Vector3 direction=view.normalized;p.camera.transform.position=b.center+direction*8;p.camera.transform.rotation=Quaternion.LookRotation(b.center-p.camera.transform.position);
   // Project every bounds corner to camera axes so extended blades never get cropped.
   float x=0,y=0;for(int i=0;i<8;i++){var point=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var delta=point-b.center;x=Mathf.Max(x,Mathf.Abs(Vector3.Dot(delta,p.camera.transform.right)));y=Mathf.Max(y,Mathf.Abs(Vector3.Dot(delta,p.camera.transform.up)));}
   p.camera.orthographicSize=Mathf.Max(y,x/(1200f/1400f))*1.08f;
   Capture(p,name);Debug.Log("ITCH_SUBJECT "+name+" actualClip="+clipName+" time="+time);
  }
  finally{p.Cleanup();}
 }
 static void Titan(string name,int variant,float jaw,Vector3 view)
 {
  var p=new PreviewRenderUtility();var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab"));p.AddSingleGO(go);
  try
  {
   Setup(p);foreach(var a in go.GetComponentsInChildren<Animator>())a.enabled=false;
   TitanFaceAppearance.Install(go.GetComponent<ModularTitanBody>());go.GetComponent<TitanFaceAppearance>().SetVariant(variant,AssetDatabase.LoadAssetAtPath<TitanFacePalette>("Assets/Resources/TitanFacePalette.asset"));
   var bone=go.GetComponentsInChildren<Transform>().Single(t=>t.name=="Jaw");bone.rotation=Quaternion.AngleAxis(jaw,go.transform.right)*bone.rotation;
   Bake(go);p.camera.orthographicSize=2.6f;p.camera.transform.position=view;p.camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,10.2f,0)-view);
   Capture(p,name);Debug.Log("ITCH_SUBJECT "+name+" approvedFace="+variant);
  }
  finally{p.Cleanup();}
 }
 static void Capture(PreviewRenderUtility p,string name)
 {
  p.BeginStaticPreview(new Rect(0,0,1200,1400));p.Render(true);var image=p.EndStaticPreview();File.WriteAllBytes(Path.Combine(output,name+"_Render.png"),image.EncodeToPNG());Object.DestroyImmediate(image);
 }
}'''
(W/'Assets/Editor/OwnedItchSubjects.cs').write_text(helper)
env=dict(os.environ,ODM_ITCH_OUTPUT=str(OUT/'Subjects'));log=W/'itch-cover-subjects.log'
result=subprocess.run(['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe','-batchmode','-quit','-projectPath',str(W),'-executeMethod','OwnedItchSubjects.Render','-logFile',str(log)],env=env,stdin=subprocess.DEVNULL,timeout=360)
t=log.read_text(errors='replace')
for line in t.splitlines():
 if line.startswith('ITCH_') or 'error CS' in line or 'Exception:' in line:print(line)
assert result.returncode==0 and 'ITCH_SUBJECTS_COMPLETE' in t,str(log)
assert all(sha(R/p)==h for p,h in baseline.items()),'Source changed during artwork; inspect, never overwrite it'
shutil.copy2(log,OUT/'native-subject-render.log')
print('Native promotional subjects:',OUT/'Subjects')
