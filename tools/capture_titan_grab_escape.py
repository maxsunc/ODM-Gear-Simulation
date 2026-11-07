"""Review the ACTUAL QTE HUD in an owned Unity2021 graphics worker. No live input.
Run tools/run_titan_validation.py --wait first; stale source/results are rejected.
"""
import json,os,shutil,subprocess,time,xml.etree.ElementTree as E
from pathlib import Path
root=Path(__file__).resolve().parents[1]
state=json.loads((root/'Library/TitanValidationRun.json').read_text());project=Path(state['project'])
assert state['testFilter'] is None,'Run the full suite first'
assert (project/'.titan-validation-owned').read_text()==str(root),'Not this project\'s owned worker'
lock=project/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
for folder in ['Assets/Titan/Prototype','Assets/Titan/Integration','Assets/Tests/TitanPrototype']:
 for f in (root/folder).rglob('*.cs'):assert f.read_bytes()==(project/f.relative_to(root)).read_bytes(),('Stale worker source',f)
for name in ['Movement.cs','ODM_Movement.cs']:assert (root/'Assets'/name).read_bytes()==(project/'Assets'/name).read_bytes()
output=root/'Library/TitanGrabEscapeReview';output.mkdir(parents=True,exist_ok=True)
for f in output.glob('*.png'):f.unlink()
result=project/'grab-escape-graphics-results.xml';result.unlink(missing_ok=True);started=time.time()
command=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-projectPath',str(project),'-runTests','-testPlatform','PlayMode','-testFilter','TitanODMIntegrationPlayModeTests.EscapeQteHudRendersRealPromptsAndOutcomesInOwnedNativeGraphics','-testResults',str(result),'-logFile',str(project/'grab-escape-graphics.log')]
r=subprocess.run(command,cwd=project,env=dict(os.environ,ODM_GRAB_ESCAPE_PREVIEW=str(output)),stdin=subprocess.DEVNULL,timeout=300)
assert result.is_file() and result.stat().st_mtime>=started,'No fresh graphics result'
stats=E.parse(result).getroot().attrib;print(stats)
assert r.returncode==0 and stats['failed']=='0' and stats['passed']=='1',stats
folder=root/'ArtSource/Titan/GrabEscape';folder.mkdir(parents=True,exist_ok=True)
for f in output.glob('GrabEscape_*.png'):
 assert f.stat().st_mtime>=started,('Stale screenshot',f)
 shutil.copy2(f,folder/f.name)
shutil.copy2(result,folder/result.name)
print('Native screenshots:',folder)
