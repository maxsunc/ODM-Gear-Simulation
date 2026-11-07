"""Render the revised run in Unity, on an owned moving-preview fixture.
Translation is preview-only; the actual Run clip has no gameplay-root motion.
Reuses the existing static skin-bake/cleanup path, without physics simulation.
"""
import ast,json,subprocess,sys,tempfile
from pathlib import Path
module=ast.parse(Path('tools/capture_scout_review.py').read_text())
code=next(ast.literal_eval(n.value) for n in module.body if isinstance(n,ast.Assign) and isinstance(n.targets[0],ast.Name) and n.targets[0].id=='code')
start=code.index('var names=new ');end=code.index('for(int i=0;i<names.Count;i++){',start)
code=code[:start]+'''var names=new System.Collections.Generic.List<string>();var times=new System.Collections.Generic.List<float>();
for(int k=0;k<24;k++){names.Add("Run");times.Add(.56f*k/24);}
'''+code[end:]
code=code.replace('clip.SampleAnimation(go,times[i]);','clip.SampleAnimation(go,times[i]);go.transform.position=new UnityEngine.Vector3(0,1000,times[i]*(4*ScoutPlayerAnimation.RunHalfStride/(ScoutPlayerAnimation.RunCycleDuration*ScoutPlayerAnimation.RunStanceFraction)));')
rear='--rear' in sys.argv
view='7,2.7f,-6' if rear else '8,2.7f,4'
code=code.replace('camera.transform.position=new UnityEngine.Vector3(5.2f,1003.3f,8);camera.transform.LookAt(new UnityEngine.Vector3(0,1002,0));','camera.transform.position=go.transform.position+new UnityEngine.Vector3('+view+');camera.transform.LookAt(go.transform.position+UnityEngine.Vector3.up*1.65f);')
code=code.replace('(i<6?names[i]:"Swing_"+(i-6).ToString("D2"))','("Run_"+i.ToString("D2"))')
code=code.replace('for(int i=0;i<2;i++){var l=',r'''var ground=new UnityEngine.GameObject("Scout run preview floor");objs.Add(ground);ground.layer=31;ground.transform.position=UnityEngine.Vector3.up*1000;
var floorMesh=new UnityEngine.Mesh();objs.Add(floorMesh);floorMesh.vertices=new[]{new UnityEngine.Vector3(-20,0,-20),new UnityEngine.Vector3(-20,0,20),new UnityEngine.Vector3(20,0,20),new UnityEngine.Vector3(20,0,-20)};floorMesh.triangles=new[]{0,1,2,0,2,3};floorMesh.RecalculateNormals();
var floorMaterial=new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Unlit"));objs.Add(floorMaterial);floorMaterial.SetColor("_BaseColor",new UnityEngine.Color(.16f,.20f,.23f));ground.AddComponent<UnityEngine.MeshFilter>().sharedMesh=floorMesh;ground.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial=floorMaterial;
for(int i=0;i<2;i++){var l=''')
code=code.replace('Captured six authored poses +16 attack frames; no physics stepping','Captured24 revised run frames; preview translation only; no physics stepping')
client=Path(tempfile.gettempdir())/'titan_mcp_review.py'
r=subprocess.run([sys.executable,str(client),'tools/call',json.dumps({'name':'execute_code','arguments':{'action':'execute','code':code}})],capture_output=True,text=True,check=True)
print(r.stdout)
response=json.loads(r.stdout)['result'].get('structuredContent',{})
if not response.get('success'):raise RuntimeError(response)
from PIL import Image,ImageDraw,ImageFont
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21)
frames=[]
for i in range(24):
 im=Image.open(Path('Library/ScoutReview')/('Run_%02d.png'%i)).convert('RGB');draw=ImageDraw.Draw(im)
 draw.text((16,16),'Scout / Naruto-inspired run',font=font,fill='white');draw.text((16,46),'Clip review - preview translation only',font=font,fill=(170,215,235));frames.append(im)
out=Path('ArtSource/Player/Scout');out.mkdir(parents=True,exist_ok=True)
durations=[round((i+1)*560/24/10)*10-round(i*560/24/10)*10 for i in range(24)]
name='Scout_PowerRun_Rear' if rear else 'Scout_NarutoRun'
frames[0].save(out/(name+'.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
frames[10].save(out/(name+'.png'))
print('Saved',out/(name+'.gif'))
