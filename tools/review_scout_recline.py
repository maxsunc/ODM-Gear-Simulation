"""Timed native upright->dual-hook blend review. Controlled Play-only sky fixture,
not normal-input flight; never saves/reloads the user's dirty scene."""
import ast,json,subprocess,sys,tempfile,time
from pathlib import Path
CLIENT=Path(tempfile.gettempdir())/'titan_mcp_review.py'
m=ast.parse(Path('tools/review_scout_runtime.py').read_text())
strings={n.targets[0].id:ast.literal_eval(n.value) for n in m.body if isinstance(n,ast.Assign) and isinstance(n.targets[0],ast.Name) and isinstance(n.value,ast.Constant) and isinstance(n.value.value,str)}
for n in m.body:
 if isinstance(n,ast.FunctionDef):exec(compile(ast.Module(body=[n],type_ignores=[]),'review-functions','exec'))
def set_hooks(mask):
 return 'var g=d.GetComponent<ODM_Movement>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ODM_Movement).GetField("leftGrappled",flags).SetValue(g,'+str(bool(mask&1)).lower()+');typeof(ODM_Movement).GetField("rightGrappled",flags).SetValue(g,'+str(bool(mask&2)).lower()+');typeof(ODM_Movement).GetField("gasing",flags).SetValue(g,true);typeof(ODM_Movement).GetField("pointLeft",flags).SetValue(g,d.transform.position+new UnityEngine.Vector3(-5,3,12));typeof(ODM_Movement).GetField("pointRight",flags).SetValue(g,d.transform.position+new UnityEngine.Vector3(6,5,12));'
capture=strings['capture'].replace('new UnityEngine.Vector3(5.2f,1.5f,8)','new UnityEngine.Vector3(8,2,3)').replace('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var root=d.transform;','var root=d.transform;')
capture=capture.replace('Live_LABEL','Recline_"+index+"')
capture=capture.replace('var data=new{state=d.State,','var b=new System.Collections.Generic.Dictionary<string,UnityEngine.Transform>();foreach(var t in d.model.GetComponentsInChildren<UnityEngine.Transform>())b[t.name]=t;var aimGear=d.GetComponent<ODM_Movement>();var data=new{age=age,torsoDegrees=UnityEngine.Vector3.Angle(b["Neck"].position-b["Chest"].position,root.up),leftAimError=UnityEngine.Vector3.Angle(d.leftHand.position-d.leftUpper.position,aimGear.LeftAnchor-d.leftUpper.position),rightAimError=UnityEngine.Vector3.Angle(d.rightHand.position-d.rightUpper.position,aimGear.RightAnchor-d.rightUpper.position),state=d.State,')
# Nested snapshot block has its own gear variable.
try:
 call('manage_editor',{'action':'play'})
 for i in range(10):
  if code('return UnityEditor.EditorApplication.isPlaying;').get('success'):break
  time.sleep(.2)
 code(strings['setup'])
 code('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();'+set_hooks(1)+'return "Prepare single hook";');time.sleep(.25)
 code('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();'+set_hooks(0)+'return "Upright Fall baseline";');time.sleep(.2)
 trigger='var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();'+set_hooks(3)+'''float start=-1;int index=0;bool toggled=false;float[] times=new float[27];for(int i=0;i<times.Length;i++)times[i]=i*.04f;UnityEditor.EditorApplication.CallbackFunction cb=null;cb=()=>{
if(!UnityEditor.EditorApplication.isPlaying||d==null){UnityEditor.EditorApplication.update-=cb;return;}
if(start<0){if(d.State!="GasBoth")return;start=UnityEngine.Time.time;}
float age=UnityEngine.Time.time-start;if(TOGGLE_GAS&&!toggled&&age>.2f){typeof(ODM_Movement).GetField("gasing",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(g,false);toggled=true;}if(age<times[index])return;
try{'''+capture+'''}catch(System.Exception e){UnityEditor.EditorApplication.update-=cb;UnityEngine.Debug.LogException(e);return;}
index++;if(index>=times.Length)UnityEditor.EditorApplication.update-=cb;};UnityEditor.EditorApplication.update+=cb;return "Scheduled native recline sequence";'''
 trigger=trigger.replace('TOGGLE_GAS',str('--gas-toggle' in sys.argv).lower())
 result=code(trigger)
 if not result.get('success'):raise RuntimeError(result)
 time.sleep(2.4)
finally:call('manage_editor',{'action':'stop'})
from PIL import Image,ImageDraw,ImageFont
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21);frames=[];data=[]
for i in range(27):
 p=Path('Library/ScoutReview')/('Recline_%d'%i);row=json.loads(p.with_suffix('.json').read_text());data.append(row)
 im=Image.open(p.with_suffix('.png')).convert('RGB');draw=ImageDraw.Draw(im);draw.text((16,16),'Dual-hook recline / %.2fs'%row['age'],font=font,fill='white');draw.text((16,44),'Native controlled pose review - no root motion',font=font,fill=(170,215,235));frames.append(im)
out=Path('ArtSource/Player/Scout');name='Scout_DualHook_Smooth_Recline_GasToggle' if '--gas-toggle' in sys.argv else 'Scout_DualHook_Smooth_Recline'
durations=[max(10,round((data[i+1]['age']-data[i]['age'])*100)*10) for i in range(len(data)-1)]+[600]
frames[0].save(out/(name+'.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
frames[-1].save(out/(name+'.png'))
print(json.dumps([data[i] for i in [0,3,6,11,19,26]],indent=2))
