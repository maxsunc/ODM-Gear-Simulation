"""Real three-second player-only respawn capture in an owned Unity2021 Editor, never live Play."""
import json,os,shutil,subprocess,time,xml.etree.ElementTree as E
from pathlib import Path
root=Path(__file__).resolve().parents[1];state=json.loads((root/'Library/TitanValidationRun.json').read_text());project=Path(state['project'])
assert state['testFilter'] is None,'Run a full suite before native capture'
assert (project/'.titan-validation-owned').read_text()==str(root),'Not owned'
lock=project/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
for folder in ['Assets/Titan/Prototype','Assets/Titan/Integration','Assets/Tests/TitanPrototype','Assets/Player/Scout']:
 for f in (root/folder).rglob('*.cs'):assert f.read_bytes()==(project/f.relative_to(root)).read_bytes(),('Stale worker',f)
for relative in ['Assets/PlayerRespawn.cs','Assets/PlayerHealth.cs','Assets/Movement.cs','Assets/ODM_Movement.cs','Assets/Attack.cs','Assets/GameElements.cs','Assets/ThirdPersonCameraController.cs','Assets/CameraJitterFix.cs','Assets/Scripts/FollowPos.cs']:
 assert (root/relative).read_bytes()==(project/relative).read_bytes(),('Stale legacy source',relative)
output=root/'Library/PlayerRespawnReview';output.mkdir(parents=True,exist_ok=True)
for f in output.glob('*.png'):f.unlink()
result=project/'player-respawn-graphics-results.xml';result.unlink(missing_ok=True);started=time.time()
command=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-projectPath',str(project),'-runTests','-testPlatform','PlayMode','-testFilter','TitanODMIntegrationPlayModeTests.PlayerOnlyRespawnRendersDeathAndRestoredScoutInOwnedNativeGraphics','-testResults',str(result),'-logFile',str(project/'player-respawn-graphics.log')]
p=subprocess.run(command,cwd=project,env=dict(os.environ,ODM_PLAYER_RESPAWN_PREVIEW=str(output)),stdin=subprocess.DEVNULL,timeout=300)
assert result.is_file() and result.stat().st_mtime>=started
stats=E.parse(result).getroot().attrib;print(stats);assert p.returncode==0 and stats['passed']=='1' and stats['failed']=='0',stats
folder=root/'ArtSource/Player/Scout/Respawn';folder.mkdir(parents=True,exist_ok=True)
for name in ['Player_Dead.png','Player_Respawned.png']:
 f=output/name;assert f.is_file() and f.stat().st_mtime>=started;shutil.copy2(f,folder/name)
shutil.copy2(result,folder/result.name);print('Actual player-only respawn rendering:',folder)
