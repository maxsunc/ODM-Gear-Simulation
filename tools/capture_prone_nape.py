"""Render the real prone injury pose in the marker-owned Unity2021 worker, never live Play."""
import argparse,json,os,shutil,subprocess,time,xml.etree.ElementTree as E
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--label',choices=['before','after'],default='after');args=p.parse_args()
root=Path(__file__).resolve().parents[1];state=json.loads((root/'Library/TitanValidationRun.json').read_text());project=Path(state['project'])
assert args.label=='before' or state['testFilter'] is None,'Run full validation before final capture'
assert (project/'.titan-validation-owned').read_text()==str(root),'Not owned'
lock=project/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
for folder in ['Assets/Titan/Prototype','Assets/Titan/Integration','Assets/Tests/TitanPrototype','Assets/Player/Scout']:
 for f in (root/folder).rglob('*.cs'):
  relative=f.relative_to(root);expected=f.read_bytes()
  if args.label=='before' and str(relative).replace('\\','/')=='Assets/Titan/Prototype/TitanInjuryPose.cs':expected=subprocess.check_output(['git','show','ef405492:Assets/Titan/Prototype/TitanInjuryPose.cs'],cwd=root)
  assert expected==(project/relative).read_bytes(),('Stale worker or wrong before-pose revision',f)
assert (root/'Assets/Prefabs/Titans/ModularTitanPrototype.prefab').read_bytes()==(project/'Assets/Prefabs/Titans/ModularTitanPrototype.prefab').read_bytes()
output=root/'Library/ProneNapeReview'/args.label;output.mkdir(parents=True,exist_ok=True)
for f in output.glob('*.png'):f.unlink()
result=project/'prone-nape-graphics-results.xml';result.unlink(missing_ok=True);started=time.time()
command=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-projectPath',str(project),'-runTests','-testPlatform','PlayMode','-testFilter','TitanInjuryPosePlayModeTests.ProneNapeRendersInOwnedNativeGraphics','-testResults',str(result),'-logFile',str(project/'prone-nape-graphics.log')]
r=subprocess.run(command,cwd=project,env=dict(os.environ,ODM_PRONE_NAPE_PREVIEW=str(output)),stdin=subprocess.DEVNULL,timeout=300)
assert result.is_file() and result.stat().st_mtime>=started
stats=E.parse(result).getroot().attrib;print(stats);assert r.returncode==0 and stats['passed']=='1' and stats['failed']=='0',stats
folder=root/'ArtSource/Titan/ProneNape';folder.mkdir(parents=True,exist_ok=True)
for name in ['Rear.png','Side.png']:
 f=output/name;assert f.is_file() and f.stat().st_mtime>=started;shutil.copy2(f,folder/(args.label+'_'+name))
shutil.copy2(result,folder/(args.label+'-graphics-results.xml'));print('Actual prone rendering:',folder)
