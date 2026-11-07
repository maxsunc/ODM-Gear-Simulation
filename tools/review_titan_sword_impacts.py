"""Actual F + actual Scout blade sweeps against owned frozen Titan fixtures.
Controlled stationary sky setup, NOT ordinary ODM traversal/combat proof. Screenshot
captures include native OnGUI numbers. Refuses an existing user Play session;
releases F/stops only its own Play session, with no fixture saves.
"""
import ctypes,json,subprocess,sys,tempfile,time
from pathlib import Path
client=Path(tempfile.gettempdir())/'titan_mcp_review.py'
def call(name,args):
 p=subprocess.run([sys.executable,str(client),'tools/call',json.dumps({'name':name,'arguments':args})],capture_output=True,text=True,check=True)
 r=json.loads(p.stdout)['result'].get('structuredContent',{})
 if not r.get('success'):raise RuntimeError(r)
 return (r.get('data') or {}).get('result')
def code(s):return call('execute_code',{'action':'execute','code':s})
u=ctypes.windll.user32
out=Path('Library/TitanImpactReview');out.mkdir(parents=True,exist_ok=True)
def foreground():
 found=[]
 @ctypes.WINFUNCTYPE(ctypes.c_bool,ctypes.c_void_p,ctypes.c_void_p)
 def visit(hwnd,_):
  n=u.GetWindowTextLengthW(hwnd);b=ctypes.create_unicode_buffer(n+1);u.GetWindowTextW(hwnd,b,n+1)
  if 'ODM Attempt # 1 -' in b.value and 'Unity' in b.value:found.append(hwnd)
  return True
 u.EnumWindows(visit,0)
 if not found:raise RuntimeError('Correct Unity window not found')
 hwnd=found[0];wanted=ctypes.c_ulong();u.GetWindowThreadProcessId(hwnd,ctypes.byref(wanted))
 for attempt in range(5):
  thread=u.GetWindowThreadProcessId(u.GetForegroundWindow(),None);current=ctypes.windll.kernel32.GetCurrentThreadId();attached=u.AttachThreadInput(current,thread,True)
  try:u.ShowWindow(hwnd,9);u.BringWindowToTop(hwnd);u.SetForegroundWindow(hwnd)
  finally:
   if attached:u.AttachThreadInput(current,thread,False)
  time.sleep(.15);active=ctypes.c_ulong();u.GetWindowThreadProcessId(u.GetForegroundWindow(),ctypes.byref(active))
  if wanted.value==active.value:return
 raise RuntimeError('Unity not foreground; refusing input')
owns_play=False
try:
 call('set_active_instance',{'instance':'34b6b0a61e38ef2f'})
 if code('return UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;'):raise RuntimeError('Refusing review over an existing user Play session')
 owns_play=True;call('manage_editor',{'action':'play'});time.sleep(1)
 for _ in range(8):
  try:
   if code('return UnityEditor.EditorApplication.isPlaying && UnityEngine.Object.FindObjectOfType<TitanSwordFeedback>()!=null;'):break
  except RuntimeError:time.sleep(.4)
 print(code('''var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();d.GetComponent<Movement>().enabled=false;d.GetComponent<ODM_Movement>().enabled=false;d.GetComponent<UnityEngine.CharacterController>().enabled=false;d.transform.position=new UnityEngine.Vector3(0,1000,0);d.transform.rotation=UnityEngine.Quaternion.identity;
 foreach(var r in UnityEngine.Object.FindObjectsOfType<ThirdPersonCam>())r.enabled=false;
 foreach(var f in UnityEngine.Object.FindObjectsOfType<ThirdPersonCameraController>())f.enabled=false;
 var fx=d.GetComponent<TitanSwordFeedback>();var c=fx.playerCamera;c.transform.position=d.transform.position+new UnityEngine.Vector3(7,4,-10);c.transform.LookAt(d.transform.position+new UnityEngine.Vector3(.3f,.8f,1));c.clearFlags=UnityEngine.CameraClearFlags.SolidColor;c.backgroundColor=new UnityEngine.Color(.07f,.1f,.13f);c.farClipPlane=100;c.fieldOfView=48;UnityEngine.RenderSettings.fog=false;
 var view=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");UnityEditor.EditorWindow.GetWindow(view).Focus();return "Controlled native F/mesh/GUI fixture ready";'''))
 for region,swings in [('LeftArm',4),('Nape',2),('Eyes',2)]:
  print(code('''var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var old=UnityEngine.GameObject.Find("Owned impact Titan");if(old!=null)UnityEngine.Object.DestroyImmediate(old);
  var pose=UnityEngine.Object.Instantiate(d.model.gameObject);pose.transform.position=d.model.position;pose.transform.rotation=d.model.rotation;pose.transform.localScale=d.model.lossyScale;pose.GetComponent<UnityEngine.Animator>().enabled=false;
  var clip=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Art/Player/Scout/Animations/AttackAir.anim");clip.SampleAnimation(pose,.14f);UnityEngine.Vector3 contact=d.transform.position;
  foreach(var m in pose.GetComponentsInChildren<UnityEngine.MeshFilter>())if(m.name=="ScoutBlade_R")contact=m.transform.TransformPoint(m.sharedMesh.bounds.center);UnityEngine.Object.DestroyImmediate(pose);
  var titan=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Titans/ModularTitanPrototype.prefab"));titan.name="Owned impact Titan";var body=titan.GetComponent<ModularTitanBody>();body.useRegionalHealth=true;body.GetHealth(TitanHitRegion.REGION);body.animator.Update(0);body.animator.enabled=false;UnityEngine.Physics.SyncTransforms();
  foreach(var zone in body.hitZones)if(zone.region==TitanHitRegion.REGION){titan.transform.position+=contact-zone.GetComponent<UnityEngine.Collider>().bounds.center;break;}UnityEngine.Physics.SyncTransforms();return new{region="REGION",health=body.GetHealth(TitanHitRegion.REGION),contact=contact.ToString()};'''.replace('REGION',region)))
  for swing in range(swings):
   label=f'{region}_{swing+1}'
   for extension in ['.json','.png']:(out/(label+extension)).unlink(missing_ok=True)
   print(code('''var d=UnityEngine.Object.FindObjectOfType<ScoutPlayerAnimation>();var body=UnityEngine.GameObject.Find("Owned impact Titan").GetComponent<ModularTitanBody>();typeof(ScoutPlayerAnimation).GetField("attackRandom",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(d,new System.Random(1));
   var dealer=d.GetComponent<Attack>().damageDealer;System.Action<TitanDamageResult> handler=null;handler=hit=>{if(hit.Body!=body||hit.Region!=TitanHitRegion.REGION)return;dealer.HitConfirmed-=handler;
   var fx=d.GetComponent<TitanSwordFeedback>();System.IO.File.WriteAllText("Library/TitanImpactReview/LABEL.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{outcome=hit.Outcome.ToString(),damage=hit.Damage,applied=hit.AppliedDamage,remaining=hit.RemainingHealth,speed=fx.CurrentDamageSpeed,point=hit.Point.ToString(),direction=hit.Direction.ToString(),root=d.transform.position.ToString(),count=fx.ConfirmedHitCount}));
   float deadline=UnityEngine.Time.time+.025f;UnityEditor.EditorApplication.CallbackFunction shot=null;shot=()=>{if(!UnityEditor.EditorApplication.isPlaying){UnityEditor.EditorApplication.update-=shot;return;}if(UnityEngine.Time.time<deadline)return;UnityEditor.EditorApplication.update-=shot;UnityEngine.ScreenCapture.CaptureScreenshot("Library/TitanImpactReview/LABEL.png");};UnityEditor.EditorApplication.update+=shot;};dealer.HitConfirmed+=handler;
   var view=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");UnityEditor.EditorWindow.GetWindow(view).Focus();return "Armed LABEL";'''.replace('REGION',region).replace('LABEL',label)))
   foreground();u.keybd_event(0x46,0,0,0);time.sleep(.3);u.keybd_event(0x46,0,2,0);time.sleep(.7)
   p=out/(label+'.json')
   if not p.exists():raise RuntimeError('No actual blade hit for '+label)
   receipt=json.loads(p.read_text());print(receipt)
   expected='Severed' if region=='LeftArm' and swing==3 else 'Killed' if region=='Nape' and swing==1 else 'Blinded' if region=='Eyes' and swing==1 else 'Hit'
   assert receipt['outcome']==expected and receipt['damage']==250,receipt
finally:
 if owns_play:
  u.keybd_event(0x46,0,2,0)
  call('manage_editor',{'action':'stop'})
