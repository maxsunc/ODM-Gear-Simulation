"""Controlled real-scene animation/IK review. NOT a normal-input ODM flight proof.
Each timed callback captures the native animated skin on a real unpaused frame.
Always exits Play Mode; no runtime placements/camera are saved.
"""
import json,subprocess,sys,tempfile,time
from pathlib import Path
CLIENT=Path(tempfile.gettempdir())/'titan_mcp_review.py'
def call(name,args):
 p=subprocess.run([sys.executable,str(CLIENT),'tools/call',json.dumps({'name':name,'arguments':args})],capture_output=True,text=True,check=True)
 r=json.loads(p.stdout);s=r.get('result',{}).get('structuredContent',{});print(json.dumps(s));return s

def code(s):return call('execute_code',{'action':'execute','code':s})
setup=r'''
var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var p=d.GetComponent<Movement>();var gear=d.GetComponent<ODM_Movement>();var attack=d.GetComponent<Attack>();
p.enabled=false;gear.enabled=false;p.controller.enabled=false;d.transform.position=new UnityEngine.Vector3(0,1000,0);d.transform.rotation=UnityEngine.Quaternion.identity;
foreach(var c in UnityEngine.Object.FindObjectsOfType<ThirdPersonCam>())c.enabled=false;
foreach(var b in UnityEngine.Object.FindObjectsOfType<TitanPatrolChaseAI>()){b.automaticTick=false;var a=b.GetComponent<UnityEngine.AI.NavMeshAgent>();if(a!=null&&a.isOnNavMesh){a.ResetPath();a.isStopped=true;}}
foreach(var t in d.model.GetComponentsInChildren<UnityEngine.Transform>())t.gameObject.layer=31;
return new{state=d.State,HP=d.GetComponent<PlayerHealth>().currentHP,root=d.transform.position.ToString()};
'''
# Captures native model, restores RenderSettings/RT and removes every owned temporary.
capture=r'''
var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var root=d.transform;
var o=new UnityEngine.GameObject("Scout runtime review camera");var c=o.AddComponent<UnityEngine.Camera>();c.enabled=false;c.orthographic=true;c.orthographicSize=4.1f;c.cullingMask=1<<31;c.clearFlags=UnityEngine.CameraClearFlags.SolidColor;c.backgroundColor=new UnityEngine.Color(.09f,.12f,.15f);c.farClipPlane=50;
var rt=new UnityEngine.RenderTexture(650,850,24);var old=UnityEngine.RenderTexture.active;bool fog=UnityEngine.RenderSettings.fog;
try {UnityEngine.RenderSettings.fog=false;c.transform.position=root.position+new UnityEngine.Vector3(5.2f,1.5f,8);c.transform.LookAt(root.position+UnityEngine.Vector3.up*.2f);c.targetTexture=rt;c.Render();UnityEngine.RenderTexture.active=rt;
var tex=new UnityEngine.Texture2D(650,850,UnityEngine.TextureFormat.RGB24,false);try{tex.ReadPixels(new UnityEngine.Rect(0,0,650,850),0,0);tex.Apply();System.IO.File.WriteAllBytes("Library/ScoutReview/Live_LABEL.png",tex.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(tex);}
var data=new{state=d.State,leftHandX=root.InverseTransformPoint(d.leftHand.position).x,rightHandX=root.InverseTransformPoint(d.rightHand.position).x,left=d.LeftAimWeight,right=d.RightAimWeight,rootAngle=UnityEngine.Quaternion.Angle(root.rotation,UnityEngine.Quaternion.identity),pivotAngle=UnityEngine.Quaternion.Angle(d.flipPivot.localRotation,UnityEngine.Quaternion.identity),attackActive=d.GetComponent<Attack>().damageDealer.gameObject.activeSelf,clipTime=d.animator.GetCurrentAnimatorStateInfo(0).normalizedTime,clipLength=d.animator.GetCurrentAnimatorStateInfo(0).length,inTransition=d.animator.IsInTransition(0),attackSide=d.MirroredAttack?"Right":"Left",currentAttack=d.animator.GetCurrentAnimatorStateInfo(0).IsName(d.AttackAnimationState("AttackAir")),nextTime=d.animator.GetNextAnimatorStateInfo(0).normalizedTime,animSpeed=d.animator.speed,gameTime=UnityEngine.Time.time,attackAge=UnityEngine.Time.time-UnityEditor.SessionState.GetFloat("ScoutAttackTime",UnityEngine.Time.time)};
System.IO.File.WriteAllText("Library/ScoutReview/Live_LABEL.json",Newtonsoft.Json.JsonConvert.SerializeObject(data));
}finally{c.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.RenderSettings.fog=fog;UnityEngine.Object.DestroyImmediate(o);UnityEngine.Object.DestroyImmediate(rt);}
'''
try:
 call('manage_editor',{'action':'play'})
 for i in range(10):
  r=code('return UnityEditor.EditorApplication.isPlaying && UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>()!=null;')
  if r.get('success'):break
  time.sleep(.2)
 code(setup)
 for label,hooks,gas in [('Left',1,True),('Right',2,True),('Both',3,True),('Coast',3,False)]:
  code('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var g=d.GetComponent<ODM_Movement>();var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ODM_Movement).GetField("leftGrappled",f).SetValue(g,'+str(bool(hooks&1)).lower()+');typeof(ODM_Movement).GetField("rightGrappled",f).SetValue(g,'+str(bool(hooks&2)).lower()+');typeof(ODM_Movement).GetField("gasing",f).SetValue(g,'+str(gas).lower()+');typeof(ODM_Movement).GetField("pointLeft",f).SetValue(g,d.transform.position+new UnityEngine.Vector3(-5,3,12));typeof(ODM_Movement).GetField("pointRight",f).SetValue(g,d.transform.position+new UnityEngine.Vector3(6,5,12));return "Set '+label+'";')
  time.sleep(.3);code(capture.replace('LABEL',label)+' return "Captured '+label+'";')
 # Install one-shot native-frame callbacks before triggering flip and attack.
 for label,delay,trigger in [('Flip',.14,'var g=d.GetComponent<ODM_Movement>();var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ScoutPlayerAnimation).GetProperty("MeasuredVelocity").SetValue(d,UnityEngine.Vector3.forward*20);typeof(ODM_Movement).GetField("leftGrappled",f).SetValue(g,false);typeof(ODM_Movement).GetField("rightGrappled",f).SetValue(g,false);typeof(ScoutPlayerAnimation).GetMethod("Update",f).Invoke(d,null);'),('Attack',.13,'UnityEditor.SessionState.SetFloat("ScoutAttackTime",UnityEngine.Time.time);var a=d.GetComponent<Attack>();var routine=(System.Collections.IEnumerator)typeof(Attack).GetMethod("ChargeAttack",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(a,null);a.StartCoroutine(routine);')]:
  delayed='var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();'+trigger+' double deadline=UnityEngine.Time.time+'+str(delay)+';UnityEditor.EditorApplication.CallbackFunction callback=null;callback=()=>{if(UnityEngine.Time.time<deadline)return;UnityEditor.EditorApplication.update-=callback;try{'+capture.replace('LABEL',label).replace('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var root=d.transform;','var root=d.transform;')+'}catch(System.Exception e){UnityEngine.Debug.LogException(e);}};UnityEditor.EditorApplication.update+=callback;return "Scheduled '+label+'";'
  code(delayed);time.sleep(.65)
 code('return new{state=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>().State,playing=UnityEditor.EditorApplication.isPlaying};')
finally:
 call('manage_editor',{'action':'stop'})
