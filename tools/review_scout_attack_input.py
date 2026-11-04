"""Real F press/release in the focused Unity Game view. Native animation timing
proof, not ODM traversal. Uses the same temporary sky fixture as runtime review."""
import ast,json,subprocess,sys,tempfile,time,ctypes
from pathlib import Path
client=Path(tempfile.gettempdir())/'titan_mcp_review.py'
def call(name,args):
 p=subprocess.run([sys.executable,str(client),'tools/call',json.dumps({'name':name,'arguments':args})],capture_output=True,text=True,check=True)
 r=json.loads(p.stdout)['result'].get('structuredContent',{});print(json.dumps(r));return r

def code(s):return call('execute_code',{'action':'execute','code':s})
module=ast.parse(Path('tools/review_scout_runtime.py').read_text());strings={node.targets[0].id:ast.literal_eval(node.value) for node in module.body if isinstance(node,ast.Assign) and isinstance(node.targets[0],ast.Name) and isinstance(node.value,ast.Constant) and isinstance(node.value.value,str)}
try:
 call('manage_editor',{'action':'play'})
 for i in range(10):
  if code('return UnityEditor.EditorApplication.isPlaying;').get('success'):break
  time.sleep(.2)
 code(strings['setup'])
 capture=strings['capture'].replace('LABEL','InputAttack').replace('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var root=d.transform;','var root=d.transform;')
 code('var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var a=d.GetComponent<Attack>();System.Action handler=null;handler=()=>{a.Released-=handler;UnityEditor.SessionState.SetFloat("ScoutAttackTime",UnityEngine.Time.time);float deadline=UnityEngine.Time.time+.13f;UnityEditor.EditorApplication.CallbackFunction cb=null;cb=()=>{if(UnityEngine.Time.time<deadline)return;UnityEditor.EditorApplication.update-=cb;'+capture+'};UnityEditor.EditorApplication.update+=cb;};a.Released+=handler;var t=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");UnityEditor.EditorWindow.GetWindow(t).Focus();return "F listener installed";')
 u=ctypes.windll.user32;found=[]
 @ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
 def visit(hwnd,_):
  n=u.GetWindowTextLengthW(hwnd)
  if n:
   b=ctypes.create_unicode_buffer(n+1);u.GetWindowTextW(hwnd,b,n+1)
   if 'ODM Attempt # 1 -' in b.value and 'Unity' in b.value:found.append(hwnd)
  return True
 u.EnumWindows(visit,0)
 if not found:raise RuntimeError('Correct Unity window not found')
 foreground=u.GetForegroundWindow();thread=u.GetWindowThreadProcessId(foreground,None);current=ctypes.windll.kernel32.GetCurrentThreadId()
 attached=bool(u.AttachThreadInput(current,thread,True))
 try:u.ShowWindow(found[0],9);u.BringWindowToTop(found[0]);u.SetForegroundWindow(found[0])
 finally:
  if attached:u.AttachThreadInput(current,thread,False)
 time.sleep(.2)
 wanted=ctypes.c_ulong();active=ctypes.c_ulong();u.GetWindowThreadProcessId(found[0],ctypes.byref(wanted));u.GetWindowThreadProcessId(u.GetForegroundWindow(),ctypes.byref(active))
 if active.value!=wanted.value:raise RuntimeError('Refusing key input: Unity is not foreground')
 u.keybd_event(0x46,0,0,0);time.sleep(.22);u.keybd_event(0x46,0,2,0);time.sleep(.65)
 code('return UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>().State;')
finally:
 ctypes.windll.user32.keybd_event(0x46,0,2,0)
 call('manage_editor',{'action':'stop'})
