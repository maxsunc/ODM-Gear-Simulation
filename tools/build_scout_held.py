"""Rebuild/publish ONLY Held.anim in a marker-owned Unity2021 worker; never live Editor."""
import hashlib,json,os,shutil,subprocess,time
from pathlib import Path
root=Path(__file__).resolve().parents[1];state=json.loads((root/'Library/TitanValidationRun.json').read_text());worker=Path(state['project'])
assert worker!=root and (worker/'.titan-validation-owned').read_text()==str(root)
lock=worker/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
clip=Path('Assets/Art/Player/Scout/Animations/Held.anim');baseline=json.loads((root/'Library/HeldStruggleSourceBaseline.json').read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for p,h in baseline.items():assert sha(root/p)==h,('Protected main source changed; re-audit',p)
for p in baseline:
 if p.startswith(('Assets/Art/Player/Scout/','Assets/Prefabs/Player/')):assert (root/p).read_bytes()==(worker/p).read_bytes(),('Stale authoring input',p)
before=sha(root/clip);backup=root/'Library/HeldStruggle';backup.mkdir(parents=True,exist_ok=True)
if not (backup/'Held.before.anim').exists():shutil.copy2(root/clip,backup/'Held.before.anim')
for p in ['Assets/Editor/ScoutPlayerBuilder.cs','Assets/Editor/ScoutPlayerBuilder.cs.meta','Assets/Player/Scout/ScoutPlayerAnimation.cs']:
 (worker/p).parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/p,worker/p)
started=time.time();cmd=[os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'),'-batchmode','-nographics','-quit','-projectPath',str(worker),'-executeMethod','ScoutPlayerBuilder.RebuildHeldOwnedBatch','-logFile',str(worker/'scout-held-build.log')]
result=subprocess.run(cmd,cwd=worker,stdin=subprocess.DEVNULL,timeout=240);assert result.returncode==0,'Owned held-only authoring failed'
for p,h in baseline.items():
 assert sha(root/p)==h,('Concurrent main edit; refusing publication',p)
 if p.startswith(('Assets/Art/Player/Scout/','Assets/Prefabs/Player/')):assert (root/p).read_bytes()==(worker/p).read_bytes(),('Scoped builder touched protected Scout asset',p)
assert sha(root/clip)==before and sha(worker/clip)!=before and (worker/clip).stat().st_mtime>=started
assert (root/(str(clip)+'.meta')).read_bytes()==(worker/(str(clip)+'.meta')).read_bytes()
shutil.copy2(worker/clip,root/clip)
report={'worker':str(worker),'started':started,'publishedOnly':clip.as_posix(),'before':before,'after':sha(root/clip),'sameGuid':True,'protectedHashCount':len(baseline),'noScenePrefabControllerOtherClipOrImportPublication':True}
(root/'Library/HeldStruggleBuild.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
