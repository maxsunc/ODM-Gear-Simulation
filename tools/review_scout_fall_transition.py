"""Native timed single-hook -> Fall blend review. Controlled Play-only fixture,
not ordinary-input flight. Never saves/reloads the gameplay scene."""
import ast,json,subprocess,sys,tempfile,time
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
CLIENT=Path(tempfile.gettempdir())/'titan_mcp_review.py'
m=ast.parse(Path('tools/review_scout_runtime.py').read_text())
strings={n.targets[0].id:ast.literal_eval(n.value) for n in m.body if isinstance(n,ast.Assign) and isinstance(n.targets[0],ast.Name) and isinstance(n.value,ast.Constant) and isinstance(n.value.value,str)}
for n in m.body:
 if isinstance(n,ast.FunctionDef):exec(compile(ast.Module(body=[n],type_ignores=[]),'review-functions','exec'))
capture=strings['capture'].replace('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var root=d.transform;','var root=d.transform;').replace('Live_LABEL','FallBlend_"+index+"')
capture=capture.replace('var data=new{state=d.State,','var data=new{age=age,fallBlendDuration=d.fallBlendDuration,leftHandHeight=d.leftHand.position.y-root.position.y,rightHandHeight=d.rightHand.position.y-root.position.y,state=d.State,')
try:
 call('manage_editor',{'action':'play'})
 for i in range(10):
  if code('return UnityEditor.EditorApplication.isPlaying;').get('success'):break
  time.sleep(.2)
 code(strings['setup'])
 code('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var g=d.GetComponent<ODM_Movement>();var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ODM_Movement).GetField("leftGrappled",f).SetValue(g,true);typeof(ODM_Movement).GetField("pointLeft",f).SetValue(g,d.transform.position+new UnityEngine.Vector3(-5,3,12));typeof(ODM_Movement).GetField("gasing",f).SetValue(g,true);return "Prepare single-hook flight";');time.sleep(.35)
 trigger='''var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var g=d.GetComponent<ODM_Movement>();typeof(ODM_Movement).GetField("leftGrappled",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(g,false);
float start=-1;int index=0;UnityEditor.EditorApplication.CallbackFunction cb=null;cb=()=>{
if(!UnityEditor.EditorApplication.isPlaying||d==null){UnityEditor.EditorApplication.update-=cb;return;}
if(start<0){if(d.State!="Fall")return;start=UnityEngine.Time.time;}
float age=UnityEngine.Time.time-start;if(age<index*.03f)return;
try{'''+capture+'''}catch(System.Exception e){UnityEditor.EditorApplication.update-=cb;UnityEngine.Debug.LogException(e);return;}
index++;if(index>=27)UnityEditor.EditorApplication.update-=cb;};UnityEditor.EditorApplication.update+=cb;return "Scheduled native Fall blend review";'''
 result=code(trigger)
 if not result.get('success'):raise RuntimeError(result)
 time.sleep(2.4)
finally:call('manage_editor',{'action':'stop'})
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21);frames=[];data=[]
for i in range(27):
 p=Path('Library/ScoutReview')/('FallBlend_%d'%i);row=json.loads(p.with_suffix('.json').read_text());data.append(row)
 im=Image.open(p.with_suffix('.png')).convert('RGB');draw=ImageDraw.Draw(im);draw.text((16,16),'Single-hook flight -> Fall / %.2fs'%row['age'],font=font,fill='white');draw.text((16,44),'Native controlled pose review - no root motion',font=font,fill=(170,215,235));frames.append(im)
durations=[max(10,round((data[i+1]['age']-data[i]['age'])*100)*10) for i in range(len(data)-1)]+[600]
frames[0].save('ArtSource/Player/Scout/Scout_Fall_Smooth_Transition.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
print(json.dumps([data[i] for i in [0,3,6,12,18,26]],indent=2))
