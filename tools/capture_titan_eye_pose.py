"""Owned native art preview of the existing eye-hurt pose. No tests or live Editor commands."""
from pathlib import Path
import argparse,hashlib,json,os,shutil,subprocess
p=argparse.ArgumentParser();p.add_argument('--label',choices=['before','after'],required=True);args=p.parse_args()
r=Path(__file__).resolve().parents[1];w=Path(json.loads((r/'Library/TitanValidationRun.json').read_text())['project']);assert (w/'.titan-validation-owned').read_text()==str(r)
lock=w/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
out=r/'Library/TitanEyePoseReview'/args.label;out.mkdir(parents=True,exist_ok=True)
relative='Assets/Titan/Prototype/ModularTitanBody.cs'
source=subprocess.check_output(['git','show','fa9c161c:'+relative],cwd=r) if args.label=='before' else (r/relative).read_bytes();(w/relative).write_bytes(source)
for folder in ['Assets/Art/Titans/FaceVariants']:
 shutil.copytree(r/folder,w/folder,dirs_exist_ok=True)
for f in ['Assets/Prefabs/Titans/ModularTitanPrototype.prefab','Assets/Resources/TitanFacePalette.asset','Assets/Resources/TitanFacePalette.asset.meta']:
 shutil.copy2(r/f,w/f)
helper=r'''using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class OwnedEyePosePreview
{
 public static void Render()
 {
  if(!File.Exists(".titan-validation-owned")||Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Owned2021 only");
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
  string output=Environment.GetEnvironmentVariable("ODM_EYE_POSE_OUTPUT");Directory.CreateDirectory(output);
  var data=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
  var pipeline=UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(data);var previous=UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;
  UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=pipeline;
  try{foreach(float scale in new[]{1f,3f,5f,10f})foreach(float yaw in new[]{0f,40f})Picture(output,scale,yaw);}
  finally{UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset=previous;UnityEngine.Object.DestroyImmediate(pipeline);}
  Debug.Log("EYE_POSE_NATIVE_PREVIEW_COMPLETED device="+SystemInfo.graphicsDeviceName);
 }
 static void Picture(string output,float scale,float yaw)
 {
  var preview=new PreviewRenderUtility();
  try
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab");
   var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Titans/Prototype/Animations/Idle.anim");
   var palette=Resources.Load<TitanFacePalette>("TitanFacePalette");
   var tick=typeof(ModularTitanBody).GetMethod("TickEyePose");
   var old=typeof(ModularTitanBody).GetMethod("CoverEye",BindingFlags.Instance|BindingFlags.NonPublic);
   for(int i=0;i<3;i++)
   {
    var go=UnityEngine.Object.Instantiate(prefab);go.transform.position=new Vector3((1-i)*5.4f*scale,0,0);go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
    var body=go.GetComponent<ModularTitanBody>();body.Initialize();body.animator.enabled=false;
    foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
    TitanFaceAppearance.Install(body);go.GetComponent<TitanFaceAppearance>().SetVariant(i,palette);
    var arms=new[]{body.leftUpperArm,body.leftLowerArm,body.leftHand,body.rightUpperArm,body.rightLowerArm,body.rightHand};
    clip.SampleAnimation(body.animator.gameObject,0);var last=arms.Select(b=>b.rotation).ToArray();float maxStep=0;
    body.StunEyes();
    for(int f=0;f<120;f++)
    {
     clip.SampleAnimation(body.animator.gameObject,(f+1)/120f);body.TickStatus(1f/120f);
     if(tick!=null)tick.Invoke(body,new object[]{1f/120f});
     else
     {
      old.Invoke(body,new object[]{body.leftUpperArm,body.leftLowerArm,body.leftHand,TitanHitRegion.LeftArm,1f});
      old.Invoke(body,new object[]{body.rightUpperArm,body.rightLowerArm,body.rightHand,TitanHitRegion.RightArm,1f});
     }
     for(int b=0;b<arms.Length;b++){maxStep=Mathf.Max(maxStep,Quaternion.Angle(last[b],arms[b].rotation));last[b]=arms[b].rotation;}
    }
    Physics.SyncTransforms();float penetration=0;
    foreach(var arm in new[]{body.leftLowerArm,body.leftHand,body.rightLowerArm,body.rightHand})
    foreach(var entry in body.ragdollBones)
    {
     var c=entry.collider;if(c==null||!(c.transform.name=="Chest"||c.transform.name=="Spine"||c.transform.name=="Hips"))continue;
     var a=arm.GetComponent<Collider>();Vector3 direction;float depth;
     if(a!=null&&Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,c,c.transform.position,c.transform.rotation,out direction,out depth))penetration=Mathf.Max(penetration,depth/scale);
    }
    float fold=Vector3.Angle(body.leftLowerArm.position-body.leftUpperArm.position,body.leftHand.position-body.leftLowerArm.position);
    Debug.Log("EYE_POSE_ART scale="+scale+" yaw="+yaw+" face="+i+" elbowFold="+fold+" maxWorldArmStep120="+maxStep+" forearmPalmTorsoOverlapModelUnits="+penetration);
    if(tick!=null)
    {
     // Authoring review only: sample the unchanged idle while fading back after
     // the real status clock expires. No new NUnit/PlayMode test is created.
     body.TickStatus(body.BlindTimeRemaining+.001f);
     for(int f=0;f<120;f++)
     {
      // Deliberately do NOT resample: emulate constant Animator channels retaining
      // the previous overlay. The production pose owner must clear itself.
      tick.Invoke(body,new object[]{1f/120f});
      for(int b=0;b<arms.Length;b++){maxStep=Mathf.Max(maxStep,Quaternion.Angle(last[b],arms[b].rotation));last[b]=arms[b].rotation;}
     }
     var recovered=arms.Select(b=>b.rotation).ToArray();clip.SampleAnimation(body.animator.gameObject,1);float residual=0;
     for(int b=0;b<arms.Length;b++)residual=Mathf.Max(residual,Quaternion.Angle(recovered[b],arms[b].rotation));
     Debug.Log("EYE_POSE_RECOVERY scale="+scale+" yaw="+yaw+" face="+i+" maxWorldArmStep120="+maxStep+" residualIdleDegrees="+residual);
     // Restore the real held pose for the picture, not the recovered one.
     body.StunEyes();for(int f=0;f<120;f++){clip.SampleAnimation(body.animator.gameObject,(f+1)/120f);body.TickStatus(1f/120f);tick.Invoke(body,new object[]{1f/120f});}
    }
    preview.AddSingleGO(go);
   }
   preview.camera.transform.position=new Vector3(0,9.0f,25)*scale;preview.camera.transform.rotation=Quaternion.LookRotation(Vector3.back);
   preview.camera.orthographic=true;preview.camera.orthographicSize=4.15f*scale;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100*scale;
   preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.15f,.18f);
   preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,150,0);preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.38f,.38f,.38f);
   preview.BeginStaticPreview(new Rect(0,0,1800,900));preview.Render(true);var image=preview.EndStaticPreview();
   File.WriteAllBytes(Path.Combine(output,"EyeCover_"+scale+"x_"+yaw+"deg.png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
  }
  finally{preview.Cleanup();}
 }
}
'''
(w/'Assets/Editor/OwnedEyePosePreview.cs').write_text(helper)
env=dict(os.environ,ODM_EYE_POSE_OUTPUT=str(out));log=w/('eye-pose-'+args.label+'.log')
p=subprocess.run(['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe','-quit','-projectPath',str(w),'-executeMethod','OwnedEyePosePreview.Render','-logFile',str(log)],stdin=subprocess.DEVNULL,env=env,timeout=240)
text=log.read_text(errors='replace')
for line in text.splitlines():
 if any(s in line for s in ['EYE_POSE_','Exception:','error CS']):print(line)
assert p.returncode==0 and 'EYE_POSE_NATIVE_PREVIEW_COMPLETED' in text
folder=r/'ArtSource/Titan/EyeCover';folder.mkdir(exist_ok=True)
for f in out.glob('*.png'):shutil.copy2(f,folder/(args.label+'_'+f.name))
shutil.copy2(log,folder/('eye-pose-'+args.label+'.log'))
print('Native eye pose art review:',folder)
