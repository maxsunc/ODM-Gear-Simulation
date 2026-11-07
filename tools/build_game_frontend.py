"""Author/publish Menu.unity in the existing marker-owned Unity2021 worker. Never opens/saves the live Editor."""
from pathlib import Path
import argparse, hashlib, json, os, re, shutil, subprocess

r = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--action', choices=['build', 'publish'], required=True)
p.add_argument('--revise', action='store_true', help='Explicitly replace only the generated frontend in the owned worker')
a = p.parse_args()
w = Path(json.loads((r/'Library/TitanValidationRun.json').read_text())['project'])
assert (w/'.titan-validation-owned').read_text() == str(r), 'Not our worker'
lock = w/'Temp/UnityLockfile'
if lock.exists():
    with lock.open('r+b'):
        pass
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
menu = 'Assets/Scenes/Menu.unity'
state_path = r/'Library/FrontendMenuRun.json'
if a.action == 'publish':
    state = json.loads(state_path.read_text())
    assert state['worker'] == str(w) and state.get('authored'), 'No completed authored output'
    assert sha(r/menu) == state['menuSourceHash'], 'User changed Menu; do not overwrite'
    assert sha(r/(menu+'.meta')) == state['menuMetaHash'] == sha(w/(menu+'.meta')), 'Scene GUID changed'
    assert all(sha(r/f) == h for f,h in state['inputs'].items()), 'Frontend source changed after authoring'
    assert sha(w/menu) == state['menuOutputHash'], 'Worker output changed'
    # Keep original Menu bytes outside version control as well as in Git history.
    backup = r/'Library/FrontendMenuOriginal.unity'
    if not backup.exists():
        shutil.copy2(r/menu, backup)
    shutil.copy2(w/menu, r/menu)
    state['published'] = True
    state_path.write_text(json.dumps(state, indent=2))
    print('Published only Menu.unity; original GUID, forest scene and legacy levels preserved.')
    raise SystemExit(0)

def copy(relative):
    src, dst = r/relative, w/relative
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)

# Exact runtime authority, including existing root script GUIDs used by Menu.
legacy = ['Movement.cs','ODM_Movement.cs','PlayerHealth.cs','PlayerRespawn.cs','Attack.cs','DamageDealer.cs',
          'ThirdPersonCameraController.cs','CameraJitterFix.cs','Scripts/FollowPos.cs','RagdollController.cs',
          'ThirdPersonCam.cs','Scripts/Audio.cs','Scripts/GameAudio.cs','GameElements.cs','SceneManagement.cs','MenuManager.cs',
          'Titan/TitanController.cs','Titan/TitanAttack.cs','Titan/TitanLimb.cs','Titan/TitanNape.cs','Titan/TitanState.cs']
inputs = {}
for relative in ['Assets/'+f for f in legacy] + ['Assets/Editor/FrontendMenuBuilder.cs'] + [f.relative_to(r).as_posix() for f in (r/'Assets/UI').rglob('*') if f.is_file()]:
    copy(relative); inputs[relative] = sha(r/relative)
    if (r/(relative+'.meta')).exists(): copy(relative+'.meta')
for folder in ['Assets/Titan/Prototype','Assets/Titan/Integration','Assets/Player/Scout']:
    shutil.copytree(r/folder,w/folder,dirs_exist_ok=True)
# Resolve the saved Menu's art/audio/font dependencies from GUIDs, without asking the live Editor.
index = {}
for meta in (r/'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([0-9a-f]{32})', meta.read_text(errors='replace'), re.M)
    if match: index[match[1]] = Path(str(meta)[:-5])
queue = [r/menu]; seen = set()
while queue:
    src = queue.pop()
    if src in seen or not src.is_file(): continue
    seen.add(src); relative = src.relative_to(r).as_posix(); copy(relative)
    if Path(str(src)+'.meta').exists(): copy(relative+'.meta')
    if src.suffix.lower() in ['.unity','.asset','.mat','.prefab','.lighting','.controller','.overridecontroller']:
        for guid in re.findall(r'guid: ([0-9a-f]{32})',src.read_text(errors='replace')):
            if guid in index: queue.append(index[guid])
copy('ProjectSettings/EditorBuildSettings.asset')
copy('Assets/Scenes/GiantForest2.unity'); copy('Assets/Scenes/GiantForest2.unity.meta')
copy('Assets/Scenes/ShiganshinaDistrict.unity'); copy('Assets/Scenes/ShiganshinaDistrict.unity.meta')
state = {'worker':str(w),'menuSourceHash':sha(r/menu),'menuMetaHash':sha(r/(menu+'.meta')),'inputs':inputs,'authored':False,'published':False}
state_path.write_text(json.dumps(state,indent=2))
log = w/'frontend-author.log'
editor = os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe')
env = dict(os.environ)
if a.revise: env['ODM_FRONTEND_REBUILD_OWNED'] = '1'
else: env.pop('ODM_FRONTEND_REBUILD_OWNED', None)
result = subprocess.run([editor,'-batchmode','-nographics','-quit','-projectPath',str(w),'-executeMethod','FrontendMenuBuilder.BuildOwned','-logFile',str(log)],env=env,stdin=subprocess.DEVNULL,timeout=300)
text = log.read_text(errors='replace')
for line in text.splitlines():
    if any(s in line for s in ['FRONTEND_','error CS','Exception:']): print(line)
assert result.returncode == 0 and 'FRONTEND_MENU_AUTHORED' in text, str(log)
# The new texture importer is authored natively. Publish its first GUID before the
# scene so the live project can never assign a different one on first import.
for texture_meta in ['Assets/UI/Frontend/ForestBackdrop.png.meta', 'Assets/UI/Frontend/ShiganshinaBackdrop.png.meta']:
    if not (r/texture_meta).exists(): shutil.copy2(w/texture_meta, r/texture_meta)
    source_guid = re.search(r'^guid: (.+)$', (r/texture_meta).read_text(), re.M)[1]
    worker_guid = re.search(r'^guid: (.+)$', (w/texture_meta).read_text(), re.M)[1]
    assert source_guid == worker_guid, 'Concurrent texture GUID change; rebuild required'
    inputs[texture_meta] = sha(r/texture_meta)
state.update(authored=True,menuOutputHash=sha(w/menu))
state_path.write_text(json.dumps(state,indent=2))
print('Authored Menu in closed owned worker:',w)
