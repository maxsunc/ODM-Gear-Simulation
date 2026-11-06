"""Native steam/darkening/transparency proof, owned Unity2021 Editor only; never live Play."""
import json,os,shutil,subprocess,time,xml.etree.ElementTree as E
from pathlib import Path
root=Path(__file__).resolve().parents[1];state=json.loads((root/'Library/TitanValidationRun.json').read_text());project=Path(state['project'])
assert state['testFilter'] is None,'Complete full suite before capture'
assert (project/'.titan-validation-owned').read_text()==str(root),'Not owned'
lock=project/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
for folder in ['Assets/Titan/Prototype','Assets/Titan/Integration','Assets/Tests/TitanPrototype','Assets/Player/Scout']:
 for f in (root/folder).rglob('*'):
  if f.is_file() and f.suffix in ['.cs','.shader']:assert f.read_bytes()==(project/f.relative_to(root)).read_bytes(),('Stale worker',f)
for name in ['TitanCorpseSteam','TitanCorpseFadeVariant']:
 for suffix in ['.mat','.mat.meta']:assert (root/('Assets/Resources/'+name+suffix)).read_bytes()==(project/('Assets/Resources/'+name+suffix)).read_bytes()
output=root/'Library/TitanDecompositionReview';output.mkdir(parents=True,exist_ok=True)
for f in output.glob('*.png'):f.unlink()
result=project/'titan-decomposition-graphics-results.xml';result.unlink(missing_ok=True);started=time.time()
cmd=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-projectPath',str(project),'-runTests','-testPlatform','PlayMode','-testFilter','TitanCorpseDecompositionPlayModeTests.CorpseDecompositionRendersDarkSteamAndTrueFadeInOwnedNativeGraphics','-testResults',str(result),'-logFile',str(project/'titan-decomposition-graphics.log')]
p=subprocess.run(cmd,cwd=project,env=dict(os.environ,ODM_TITAN_DECAY_PREVIEW=str(output)),stdin=subprocess.DEVNULL,timeout=300)
assert result.is_file() and result.stat().st_mtime>=started
stats=E.parse(result).getroot().attrib;print(stats);assert p.returncode==0 and stats['passed']=='1' and stats['failed']=='0',stats
folder=root/'ArtSource/Titan/Decomposition';folder.mkdir(parents=True,exist_ok=True)
for name in ['Corpse_Fresh','Corpse_Dark','Corpse_Steam','Corpse_Fading','Corpse_Gone']:
 f=output/(name+'.png');assert f.is_file() and f.stat().st_mtime>=started;shutil.copy2(f,folder/f.name)
shutil.copy2(result,folder/result.name);print('Native decomposition:',folder)
