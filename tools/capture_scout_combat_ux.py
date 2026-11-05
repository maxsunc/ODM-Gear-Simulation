"""Native longer-cape review in the OWNED Unity2021 test worker; no live Editor input.
Run the full tools/run_titan_validation.py --wait first. Source equality and fresh
XML checks prevent stale pictures from being published as current validation.
"""
import json,os,shutil,subprocess,xml.etree.ElementTree as E
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path(__file__).resolve().parents[1]
state=json.loads((root/'Library/TitanValidationRun.json').read_text());project=Path(state['project'])
assert (project/'.titan-validation-owned').read_text()==str(root),'Not this project\'s owned worker'
lock=project/'Temp/UnityLockfile'
if lock.exists():
 try:
  with lock.open('r+b'):pass
 except PermissionError:raise SystemExit('Owned worker still running')
for folder in ['Assets/Player/Scout','Assets/Titan/Integration','Assets/Tests/TitanPrototype']:
 for f in (root/folder).rglob('*.cs'):assert f.read_bytes()==(project/f.relative_to(root)).read_bytes(),('Stale worker source',f)
output=root/'Library/ScoutCombatUXReview';output.mkdir(parents=True,exist_ok=True)
for f in output.glob('*.png'):f.unlink()
result=project/'combat-ux-graphics-results.xml';result.unlink(missing_ok=True)
command=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-batchmode','-projectPath',str(project),'-runTests','-testPlatform','PlayMode','-testFilter','ScoutCapeClothPlayModeTests.NativeGraphicsShowLongerSimulatedCapeWithOriginalScoutArt','-testResults',str(result),'-logFile',str(project/'combat-ux-graphics.log')]
r=subprocess.run(command,cwd=project,env=dict(os.environ,ODM_SCOUT_COMBAT_UX_PREVIEW=str(output)),timeout=240)
assert result.exists(),'No fresh native XML';stats=E.parse(result).getroot().attrib;print(stats)
assert r.returncode==0 and int(stats['failed'])==0 and int(stats['passed'])==1,stats
folder=root/'ArtSource/Player/Scout/CombatUX';folder.mkdir(parents=True,exist_ok=True)
for name in ['LongCape_Idle','LongCape_Flight80','CapeWind_12']:shutil.copy2(output/(name+'.png'),folder/(name+'.png'))
frames=[]
for f in sorted(output.glob('CapeWind_*.png')):frames.append(Image.open(f).convert('RGB').resize((600,600)))
frames[0].save(folder/'Scout_LongCape_Wind_Unity.gif',save_all=True,append_images=frames[1:],duration=50,loop=0)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
sheet=Image.new('RGB',(1800,720),(20,30,27));d=ImageDraw.Draw(sheet);d.text((20,15),'SCOUT / LONGER SIMULATED FABRIC — actual Unity 2021.3.25f1 frames',font=font,fill='white')
for i,(name,label) in enumerate([('LongCape_Idle','Longer cape / pinned shoulders'),('CapeWind_12','Wind-driven fabric / original atlas'),('LongCape_Flight80','Approved flight pose / simulated 80m/s')]):
 sheet.paste(Image.open(folder/(name+'.png')).resize((590,590)),(i*600+5,65));d.text((i*600+10,660),label,font=small,fill='white')
d.text((20,697),'Owned review fixture, not a full-game FPS/WebGL test. No new native bodies, colliders or animation/root edits.',font=small,fill=(190,210,196))
sheet.save(folder/'Scout_LongCape_Unity.png');shutil.copy2(result,folder/result.name)
(folder/'validation-summary.json').write_text(json.dumps({'nativeGraphics':stats,'deviceEvidence':'See owned combat-ux-graphics.log / SCOUT_CLOTH_NATIVE','sourceLength':.98,'originalSourceLength':.62,'sceneVisualScale':2,'particles':273,'triangles':1000,'vertices':586},indent=2))
print(folder/'Scout_LongCape_Unity.png')
