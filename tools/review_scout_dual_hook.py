"""Native unpaused pose/IK review, NOT ordinary input ODM flight.
Keeps dirty Edit scene intact; motor suspension/anchor injection is Play-only.
Uses the established owned sky fixture and capture cleanup, never saves the scene.
"""
import ast,json,subprocess,sys,tempfile,time,shutil
from pathlib import Path
CLIENT=Path(tempfile.gettempdir())/'titan_mcp_review.py'
m=ast.parse(Path('tools/review_scout_runtime.py').read_text())
strings={n.targets[0].id:ast.literal_eval(n.value) for n in m.body if isinstance(n,ast.Assign) and isinstance(n.targets[0],ast.Name) and isinstance(n.value,ast.Constant) and isinstance(n.value.value,str)}
for n in m.body:
 if isinstance(n,ast.FunctionDef):exec(compile(ast.Module(body=[n],type_ignores=[]),'review-functions','exec'))
capture=strings['capture'].replace('new UnityEngine.Vector3(5.2f,1.5f,8)','new UnityEngine.Vector3(8,2,3)')
capture=capture.replace('var data=new{state=d.State,','var bones=new System.Collections.Generic.Dictionary<string,UnityEngine.Transform>();foreach(var t in d.model.GetComponentsInChildren<UnityEngine.Transform>())bones[t.name]=t;var data=new{feetAheadOfHead=((bones["LeftFoot"].position+bones["RightFoot"].position)*.5f-bones["Head"].position).z,state=d.State,')
def hooks(mask,gas):
 return code('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var g=d.GetComponent<ODM_Movement>();var f=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(ODM_Movement).GetField("leftGrappled",f).SetValue(g,'+str(bool(mask&1)).lower()+');typeof(ODM_Movement).GetField("rightGrappled",f).SetValue(g,'+str(bool(mask&2)).lower()+');typeof(ODM_Movement).GetField("gasing",f).SetValue(g,'+str(gas).lower()+');typeof(ODM_Movement).GetField("pointLeft",f).SetValue(g,d.transform.position+new UnityEngine.Vector3(-5,3,12));typeof(ODM_Movement).GetField("pointRight",f).SetValue(g,d.transform.position+new UnityEngine.Vector3(6,5,12));return "Set hook mask '+str(mask)+'";')
try:
 call('manage_editor',{'action':'play'})
 for i in range(10):
  if code('return UnityEditor.EditorApplication.isPlaying;').get('success'):break
  time.sleep(.2)
 code(strings['setup'])
 for label,mask,gas in [('FeetFirstGas',3,True),('FeetFirstCoast',3,False),('OneRemaining',1,False),('SingleFall',0,False)]:
  hooks(mask,gas);time.sleep(1.05 if mask==3 else .35)
  result=code(capture.replace('LABEL',label)+'return "Captured '+label+'";')
  if not result.get('success'):raise RuntimeError(result)
finally:call('manage_editor',{'action':'stop'})
from PIL import Image,ImageDraw,ImageFont
out=Path('ArtSource/Player/Scout');font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',23)
canvas=Image.new('RGB',(1300,910),(22,30,38));draw=ImageDraw.Draw(canvas)
for i,(label,title) in enumerate([('FeetFirstGas','Dual hook / gas'),('FeetFirstCoast','Dual hook / attached coast')]):
 im=Image.open(Path('Library/ScoutReview')/('Live_'+label+'.png')).convert('RGB');canvas.paste(im,(i*650,45));draw.text((i*650+18,12),title,font=font,fill='white')
 shutil.copyfile(Path('Library/ScoutReview')/('Live_'+label+'.png'),out/('Scout_'+label+'_Unity.png'))
 print(label,Path('Library/ScoutReview',f'Live_{label}.json').read_text())
canvas.save(out/'Scout_DualHook_FeetFirst_Unity.png')
print('Single release',Path('Library/ScoutReview/Live_SingleFall.json').read_text())
