"""Front/profile Unity renders of Fall's actual blade meshes and downward gaze.
Static skin-bake only: no physics stepping, no scene save/reload."""
import ast,json,subprocess,sys,tempfile
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
module=ast.parse(Path('tools/capture_scout_review.py').read_text())
code=next(ast.literal_eval(n.value) for n in module.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='code' for t in n.targets))
code=code.replace('{"Idle","Run","GasBoth","Fall","AttackGround","AttackAir"}','{"Fall","Fall"}').replace('{0,.12f,.2f,0,.13f,.13f}','{0,0}')
code=code.replace('for(int k=0;k<16;k++){names.Add("AttackAir");times.Add(.39f*k/15);}','')
code=code.replace('new UnityEngine.Vector3(5.2f,1003.3f,8)','new UnityEngine.Vector3(i==0?0:7,1002.7f,i==0?8:3)').replace('new UnityEngine.Vector3(0,1002,0)','new UnityEngine.Vector3(0,1002.7f,0)')
code=code.replace('(i<6?names[i]:"Swing_"+(i-6).ToString("D2"))','(i==0?"Fall_Overhead_Front":"Fall_Overhead_Profile")')
code=code.replace('Captured six authored poses +16 attack frames; no physics stepping','Captured Fall front/profile; no physics stepping')
client=Path(tempfile.gettempdir())/'titan_mcp_review.py'
r=subprocess.run([sys.executable,str(client),'tools/call',json.dumps({'name':'execute_code','arguments':{'action':'execute','code':code}})],capture_output=True,text=True,check=True)
print(r.stdout)
response=json.loads(r.stdout);structured=response.get('result',{}).get('structuredContent',{})
if not structured.get('success'):raise RuntimeError('Unity capture failed')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
frames=[]
for name,label in [('Fall_Overhead_Front','Fall - overhead crossed swords'),('Fall_Overhead_Profile','Profile - looking down')]:
 im=Image.open(Path('Library/ScoutReview')/(name+'.png')).convert('RGB');ImageDraw.Draw(im).text((16,16),label,font=font,fill='white');frames.append(im)
canvas=Image.new('RGB',(1300,850))
for i,im in enumerate(frames):canvas.paste(im,(i*650,0))
canvas.save('ArtSource/Player/Scout/Scout_Fall_Overhead_Unity.png')
