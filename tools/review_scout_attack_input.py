"""Real F press/release in focused Unity Game view, both mirrored variants.
Seeds cosmetic RNG for coverage; production remains random. Controlled sky fixture,
not ODM traversal. Always releases F and exits Play."""
import ast,json,subprocess,sys,tempfile,time,ctypes
from pathlib import Path
client=Path(tempfile.gettempdir())/'titan_mcp_review.py'
def call(name,args):
 p=subprocess.run([sys.executable,str(client),'tools/call',json.dumps({'name':name,'arguments':args})],capture_output=True,text=True,check=True)
 r=json.loads(p.stdout)['result'].get('structuredContent',{});print(json.dumps(r));return r
def code(s):return call('execute_code',{'action':'execute','code':s})
module=ast.parse(Path('tools/review_scout_runtime.py').read_text());strings={node.targets[0].id:ast.literal_eval(node.value) for node in module.body if isinstance(node,ast.Assign) and isinstance(node.targets[0],ast.Name) and isinstance(node.value,ast.Constant) and isinstance(node.value.value,str)}
u=ctypes.windll.user32
try:
 call('manage_editor',{'action':'play'})
 for i in range(10):
  if code('return UnityEditor.EditorApplication.isPlaying;').get('success'):break
  time.sleep(.2)
 code(strings['setup'])
 found=[]
 @ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
 def visit(hwnd,_):
  n=u.GetWindowTextLengthW(hwnd)
  if n:
   b=ctypes.create_unicode_buffer(n+1);u.GetWindowTextW(hwnd,b,n+1)
   if 'ODM Attempt # 1 -' in b.value and 'Unity' in b.value:found.append(hwnd)
  return True
 u.EnumWindows(visit,0)
 if not found:raise RuntimeError('Correct Unity window not found')
 for seed in [0,1]:
  capture=strings['capture'].replace('LABEL','InputAttack_'+str(seed)).replace('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var root=d.transform;','var root=d.transform;').replace('var data=new{state=d.State,','var data=new{chargedRight=UnityEditor.SessionState.GetBool("ScoutChargeSide",false),state=d.State,')
  chargeCapture=capture.replace('InputAttack_'+str(seed),'InputCharge_'+str(seed))
  result=code('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();typeof(ScoutPlayerAnimation).GetField("attackRandom",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(d,new System.Random('+str(seed)+'));var a=d.GetComponent<Attack>();System.Action charge=null;charge=()=>{a.ChargeStarted-=charge;UnityEditor.SessionState.SetBool("ScoutChargeSide",d.MirroredAttack);float chargeDeadline=UnityEngine.Time.time+.12f;UnityEditor.EditorApplication.CallbackFunction chargeFrame=null;chargeFrame=()=>{if(!UnityEditor.EditorApplication.isPlaying||d==null){UnityEditor.EditorApplication.update-=chargeFrame;return;}if(UnityEngine.Time.time<chargeDeadline)return;UnityEditor.EditorApplication.update-=chargeFrame;'+chargeCapture+'};UnityEditor.EditorApplication.update+=chargeFrame;};a.ChargeStarted+=charge;System.Action handler=null;handler=()=>{a.Released-=handler;UnityEditor.SessionState.SetFloat("ScoutAttackTime",UnityEngine.Time.time);float deadline=UnityEngine.Time.time+.13f;UnityEditor.EditorApplication.CallbackFunction cb=null;cb=()=>{if(!UnityEditor.EditorApplication.isPlaying||d==null){UnityEditor.EditorApplication.update-=cb;return;}if(UnityEngine.Time.time<deadline)return;UnityEditor.EditorApplication.update-=cb;'+capture+'};UnityEditor.EditorApplication.update+=cb;};a.Released+=handler;var t=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");UnityEditor.EditorWindow.GetWindow(t).Focus();return "F listener installed";')
  if not result.get('success'):raise RuntimeError(result)
  foreground=u.GetForegroundWindow();thread=u.GetWindowThreadProcessId(foreground,None);current=ctypes.windll.kernel32.GetCurrentThreadId();attached=bool(u.AttachThreadInput(current,thread,True))
  try:u.ShowWindow(found[0],9);u.BringWindowToTop(found[0]);u.SetForegroundWindow(found[0])
  finally:
   if attached:u.AttachThreadInput(current,thread,False)
  time.sleep(.2)
  wanted=ctypes.c_ulong();active=ctypes.c_ulong();u.GetWindowThreadProcessId(found[0],ctypes.byref(wanted));u.GetWindowThreadProcessId(u.GetForegroundWindow(),ctypes.byref(active))
  if active.value!=wanted.value:raise RuntimeError('Refusing key input: Unity is not foreground')
  u.keybd_event(0x46,0,0,0);time.sleep(.35);u.keybd_event(0x46,0,2,0);time.sleep(.65)
  row=json.loads(Path('Library/ScoutReview/Live_InputAttack_'+str(seed)+'.json').read_text())
  assert row['currentAttack'] and row['attackActive'] and not row['inTransition'],row
  assert row['attackSide']==('Right' if seed==0 else 'Left') and row['chargedRight']==(row['attackSide']=='Right'),row
  assert abs(row['clipTime']*.39-row['attackAge'])<.035,row
  chamber=json.loads(Path('Library/ScoutReview/Live_InputCharge_'+str(seed)+'.json').read_text());loadSign=1 if seed==0 else -1
  assert chamber['state'].startswith('Charge') and not chamber['inTransition'],chamber
  assert chamber['attackSide']==row['attackSide'] and chamber['leftHandX']*loadSign>.15 and chamber['rightHandX']*loadSign>.15,chamber
  print('Actual F directional chamber proof:',json.dumps(chamber))
  print('Actual F variant proof:',json.dumps(row))
finally:
 u.keybd_event(0x46,0,2,0)
 call('manage_editor',{'action':'stop'})

# Paired native F-hold frames make the two different starting poses explicit.
from PIL import Image,ImageDraw,ImageFont
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22);sheet=Image.new('RGB',(1300,850))
for i,(seed,label) in enumerate([(0,'Slash left / both hands start right'),(1,'Slash right / both hands start left')]):
 im=Image.open('Library/ScoutReview/Live_InputCharge_'+str(seed)+'.png').convert('RGB');ImageDraw.Draw(im).text((16,16),label,font=font,fill='white');sheet.paste(im,(650*i,0))
sheet.save('ArtSource/Player/Scout/Scout_Directional_Windup_Unity.png')
