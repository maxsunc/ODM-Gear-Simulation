"""Actual Scout struggle in both real Titan palms, owned Unity2021 GameView only."""
import json,os,shutil,subprocess,time,xml.etree.ElementTree as E
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parents[1];state=json.loads((root/'Library/TitanValidationRun.json').read_text());worker=Path(state['project']);assert state['testFilter'] is None
assert (worker/'.titan-validation-owned').read_text()==str(root) and worker!=root
lock=worker/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
for folder in ['Assets/Titan/Prototype','Assets/Titan/Integration','Assets/Tests/TitanPrototype','Assets/Player/Scout']:
 for f in (root/folder).rglob('*.cs'):assert f.read_bytes()==(worker/f.relative_to(root)).read_bytes(),('Stale source',f)
for p in ['Assets/Art/Player/Scout/Animations/Held.anim','Assets/Art/Player/Scout/Animations/Held.anim.meta','Assets/Art/Player/Scout/Animations/ScoutPlayer.controller','Assets/Editor/ScoutPlayerBuilder.cs']:
 assert (root/p).read_bytes()==(worker/p).read_bytes(),('Stale clip/controller/recipe',p)
output=root/'Library/ScoutHeldGripReview';output.mkdir(parents=True,exist_ok=True)
for f in output.glob('*.png'):f.unlink()
result=worker/'scout-held-graphics-results.xml';result.unlink(missing_ok=True);started=time.time()
command=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-projectPath',str(worker),'-runTests','-testPlatform','PlayMode','-testFilter','TitanODMIntegrationPlayModeTests.HeldStruggle(Left|Right)RendersInOwnedNativeGraphics','-testResults',str(result),'-logFile',str(worker/'scout-held-graphics.log')]
p=subprocess.run(command,cwd=worker,env=dict(os.environ,ODM_HELD_STRUGGLE_PREVIEW=str(output)),stdin=subprocess.DEVNULL,timeout=300)
assert result.exists() and result.stat().st_mtime>=started
stats=E.parse(result).getroot().attrib;print(stats);assert p.returncode==0 and stats['passed']=='2' and stats['failed']=='0',stats
folder=root/'ArtSource/Player/Scout/HeldGripRevision';folder.mkdir(parents=True,exist_ok=True)
for side in ['Left','Right']:
 frames=[]
 for i in range(8):
  f=output/(side+'_'+str(i).zfill(2)+'.png');assert f.exists() and f.stat().st_mtime>=started;shutil.copy2(f,folder/f.name);frames.append(Image.open(f).convert('RGB'))
 frames[0].save(folder/(side+'_Struggle.gif'),save_all=True,append_images=frames[1:],duration=225,loop=0,disposal=2)
shutil.copy2(result,folder/result.name);print('Actual hand-held struggle captures and loops:',folder)
