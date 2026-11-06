"""Capture actual health HUD in this project's owned Unity2021 worker, never live input."""
import json,os,shutil,subprocess,time,xml.etree.ElementTree as E
from pathlib import Path
root=Path(__file__).resolve().parents[1]
state=json.loads((root/'Library/TitanValidationRun.json').read_text());project=Path(state['project'])
assert state['testFilter'] is None,'Run full validation first'
assert (project/'.titan-validation-owned').read_text()==str(root),'Not owned'
lock=project/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
for folder in ['Assets/Titan/Prototype','Assets/Titan/Integration','Assets/Tests/TitanPrototype']:
 for f in (root/folder).rglob('*.cs'):assert f.read_bytes()==(project/f.relative_to(root)).read_bytes(),('Stale worker',f)
assert (root/'Assets/PlayerHealth.cs').read_bytes()==(project/'Assets/PlayerHealth.cs').read_bytes()
output=root/'Library/PlayerHealthReview';output.mkdir(parents=True,exist_ok=True)
for f in output.glob('Health_*.png'):f.unlink()
result=project/'player-health-graphics-results.xml';result.unlink(missing_ok=True);started=time.time()
command=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-projectPath',str(project),'-runTests','-testPlatform','PlayMode','-testFilter','TitanODMIntegrationPlayModeTests.PlayerHealthBarRendersPersistentDamageInOwnedNativeGraphics','-testResults',str(result),'-logFile',str(project/'player-health-graphics.log')]
p=subprocess.run(command,cwd=project,env=dict(os.environ,ODM_PLAYER_HEALTH_PREVIEW=str(output)),stdin=subprocess.DEVNULL,timeout=300)
assert result.is_file() and result.stat().st_mtime>=started
stats=E.parse(result).getroot().attrib;print(stats);assert p.returncode==0 and stats['passed']=='1' and stats['failed']=='0',stats
folder=root/'ArtSource/Titan/StompDebris';folder.mkdir(parents=True,exist_ok=True)
for name in ['Health_Full.png','Health_StompDamage.png','Health_Persistent.png']:
 f=output/name;assert f.is_file() and f.stat().st_mtime>=started;shutil.copy2(f,folder/name)
shutil.copy2(result,folder/result.name)
print('Actual health HUD:',folder)
