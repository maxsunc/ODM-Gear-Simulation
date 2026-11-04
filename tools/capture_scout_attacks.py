"""Unity skin-baked mirrored attack previews. Authored clips, not gameplay video.
Initial/final GIF holds are review pauses, not combat timing. No physics stepping."""
import ast,json,subprocess,sys,tempfile
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
m=ast.parse(Path('tools/capture_scout_review.py').read_text())
code=next(ast.literal_eval(n.value) for n in m.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='code' for t in n.targets))
code=code.replace('camera.orthographicSize=3.35f','camera.orthographicSize=4.1f')
code=code.replace('{"Idle","Run","GasBoth","Fall","AttackGround","AttackAir"}','{"AttackGround","AttackGround","AttackGround","AttackGround","AttackGround_Right","AttackGround_Right","AttackGround_Right","AttackGround_Right"}').replace('{0,.12f,.2f,0,.13f,.13f}','{0,.1f,.14f,.22f,0,.1f,.14f,.22f}')
code=code.replace('for(int k=0;k<16;k++){names.Add("AttackAir");times.Add(.39f*k/15);}','for(int side=0;side<2;side++)for(int k=0;k<25;k++){names.Add(side==0?"AttackAir":"AttackAir_Right");times.Add(.39f*k/24);}')
code=code.replace('(i<6?names[i]:"Swing_"+(i-6).ToString("D2"))','("AttackArc_"+i.ToString("D2"))')
code=code.replace('Captured six authored poses +16 attack frames; no physics stepping','Captured mirrored chamber/arc/finish and25 frames per aerial slash; no physics stepping')
client=Path(tempfile.gettempdir())/'titan_mcp_review.py'
r=subprocess.run([sys.executable,str(client),'tools/call',json.dumps({'name':'execute_code','arguments':{'action':'execute','code':code}})],capture_output=True,text=True,check=True)
print(r.stdout)
if not json.loads(r.stdout).get('result',{}).get('structuredContent',{}).get('success'):raise RuntimeError('Unity capture failed')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22);folder=Path('Library/ScoutReview');out=Path('ArtSource/Player/Scout')
sheet=Image.new('RGB',(2080,1360))
for i in range(8):
 im=Image.open(folder/('AttackArc_%02d.png'%i)).convert('RGB');ImageDraw.Draw(im).text((16,16),('Slash right' if i<4 else 'Slash left')+' / '+['Load left' if i<4 else 'Load right','Arc top','Cut','Extended'][i%4],font=font,fill='white');sheet.paste(im.resize((520,680)),((i%4)*520,(i//4)*680))
sheet.save(out/'Scout_Mirrored_Attack_Unity.png')
for side in range(2):
 frames=[]
 for i in range(25):
  im=Image.open(folder/('AttackArc_%02d.png'%(8+side*25+i))).convert('RGB');ImageDraw.Draw(im).text((16,16),('Left load -> right slash' if side==0 else 'Right load -> left slash')+' / %.3fs'%(.39*i/24),font=font,fill='white');frames.append(im)
 durations=[round((i+1)*39/24)*10-round(i*39/24)*10 for i in range(24)]+[240];durations[0]+=200
 frames[0].save(out/('Scout_Slash_To'+('Right' if side==0 else 'Left')+'.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
