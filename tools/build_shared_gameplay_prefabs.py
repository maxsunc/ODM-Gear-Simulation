"""Migrate saved gameplay roots to linked shared prefabs in a closed owned Unity worker.
No live Editor control, Play/input/physics simulation, navigation bake or scene reload.
"""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess

R = Path(__file__).resolve().parents[1]
STATE = R / 'Library/SharedGameplayPrefabsRun.json'
SCENES = ['Assets/Scenes/GiantForest2.unity', 'Assets/Scenes/ShiganshinaDistrict.unity']
BUILDER = 'Assets/Editor/SharedGameplayPrefabBuilder.cs'
FOLDER = 'Assets/Prefabs/SharedGameplay'
EDITOR = 'C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--action', required=True, choices=['prepare', 'build', 'publish'])
p.add_argument('--refresh', action='store_true', help='Explicit current-source resnapshot; no publication')
p.add_argument('--worker', type=Path, default=Path('C:/Users/maxsu/AppData/Local/Temp/ODM-GiantForest2-iwrxdlei'))
a = p.parse_args()


def closed(w):
    marker = w / '.shared-gameplay-prefabs-owned'
    if not marker.exists(): marker = w / '.shiganshina-owned'
    if not marker.is_file() or marker.read_text() != str(R):
        raise SystemExit('Worker must have a matching project-ownership marker')
    lock = w / 'Temp/UnityLockfile'
    if lock.exists():
        with lock.open('r+b'): pass


def remove_readonly(function, path, error):
    os.chmod(path, stat.S_IWRITE)
    function(path)


if a.action == 'prepare':
    if STATE.exists() and not a.refresh:
        raise SystemExit('Existing migration state: review first, or explicitly --refresh')
    w = a.worker.resolve(); closed(w)
    if (R / FOLDER).exists(): raise SystemExit('Shared prefab destination already exists; no implicit overwrite')
    before = {f.relative_to(R).as_posix(): sha(f) for folder in ['Assets', 'ProjectSettings'] for f in (R/folder).rglob('*') if f.is_file()}
    shutil.rmtree(w/'Assets', onexc=remove_readonly)
    shutil.copytree(R/'Assets', w/'Assets')
    shutil.copytree(R/'ProjectSettings', w/'ProjectSettings', dirs_exist_ok=True)
    manifest = json.loads((R/'Packages/manifest.json').read_text())
    manifest['dependencies'].pop('com.coplaydev.unity-mcp', None)
    (w/'Packages/manifest.json').write_text(json.dumps(manifest, indent=2))
    if (R/'Packages/packages-lock.json').exists():
        lock = json.loads((R/'Packages/packages-lock.json').read_text())
        lock.get('dependencies', {}).pop('com.coplaydev.unity-mcp', None)
        (w/'Packages/packages-lock.json').write_text(json.dumps(lock, indent=2))
    # Never reuse a historical one-shot authoring request from this disposable worker.
    for request in (w/'Library').glob('*.request'): request.unlink()
    (w/'.shared-gameplay-prefabs-owned').write_text(str(R))
    state = {'worker': str(w), 'before': before, 'built': False, 'published': False}
    STATE.write_text(json.dumps(state, indent=2))
    print('Current-source snapshot ready in CLOSED owned worker:', w)
    raise SystemExit()

state = json.loads(STATE.read_text()); w = Path(state['worker']); closed(w)
if a.action == 'build':
    if state['published']: raise SystemExit('Published revision needs a new explicit current-source baseline')
    if (w/FOLDER).exists(): shutil.rmtree(w/FOLDER, onexc=remove_readonly)
    Path(str(w/FOLDER)+'.meta').unlink(missing_ok=True)
    for rel in SCENES:
        if sha(R/rel) != state['before'][rel]: raise SystemExit('Scene changed since baseline: resnapshot before build')
        shutil.copy2(R/rel, w/rel)
    for rel in [BUILDER, BUILDER+'.meta']:
        if (R/rel).exists(): shutil.copy2(R/rel, w/rel)
    log = w/'shared-gameplay-prefabs.log'
    result = subprocess.run([EDITOR, '-batchmode', '-nographics', '-quit', '-projectPath', str(w),
        '-executeMethod', 'SharedGameplayPrefabBuilder.BuildOwned', '-logFile', str(log)],
        env=dict(os.environ, ODM_SHARED_PREFABS_OWNED='1'), stdin=subprocess.DEVNULL, timeout=1200)
    text = log.read_text(errors='replace')
    for line in text.splitlines():
        if any(q in line for q in ['SHARED_PREFABS_', 'error CS', 'Exception:', 'Failed to']): print(line)
    if result.returncode or 'SHARED_PREFABS_BUILD_COMPLETE' not in text:
        raise SystemExit('Migration did not complete; SOURCE SCENES UNCHANGED. See '+str(log))
    state['built'] = True; state['builderHash'] = sha(R/BUILDER)
    state['outputs'] = {f.relative_to(w).as_posix(): sha(f) for f in (w/FOLDER).rglob('*') if f.is_file()}
    state['outputs'].update({rel: sha(w/rel) for rel in SCENES+[FOLDER+'.meta']})
    STATE.write_text(json.dumps(state, indent=2))
    print('Closed-worker authoring complete; source scenes are not yet published.')
    raise SystemExit()

if not state['built'] or state['published'] or sha(R/BUILDER) != state['builderHash']:
    raise SystemExit('Requires completed, unpublished migration from the current builder')
for rel, digest in state['before'].items():
    if rel == BUILDER: continue  # Explicit authoring implementation may have been refined during build.
    if not (R/rel).is_file() or sha(R/rel) != digest:
        raise SystemExit('Concurrent source change: refusing publication: '+rel)
for rel, digest in state['outputs'].items():
    if sha(w/rel) != digest: raise SystemExit('Worker output changed after authoring: '+rel)
if (R/FOLDER).exists() or Path(str(R/FOLDER)+'.meta').exists(): raise SystemExit('Refusing to overwrite existing shared prefabs')
backup = R/'Library/SharedGameplayPrefabsOriginalScenes'; backup.mkdir(exist_ok=True)
for rel in SCENES: shutil.copy2(R/rel, backup/Path(rel).name)
shutil.copytree(w/FOLDER, R/FOLDER)
shutil.copy2(w/(FOLDER+'.meta'), R/(FOLDER+'.meta'))
for rel in SCENES: shutil.copy2(w/rel, R/rel)  # Original .unity.meta/GUIDs stay untouched.
out = R/'ArtSource/SharedGameplayPrefabs'; out.mkdir(parents=True, exist_ok=True)
shutil.copy2(w/'shared-gameplay-prefabs.log', out/'native-migration.log')
raw_report = (w/'Library/SharedGameplayPrefabs/migration-report.json').read_bytes()
(out/'migration-report.json.gz').write_bytes(gzip.compress(raw_report, mtime=0))
report = json.loads(raw_report)
summary = {'unity': report['unity'], 'authoredAt': report['authoredAt'], 'prefabs': report['prefabs'], 'scenes': []}
for scene in report['scenes']:
    assert scene['before'] == scene['after'], 'Native before/after owner digests differ'
    summary['scenes'].append({key: value for key, value in scene.items() if key not in ['before', 'after']})
(out/'migration-summary.json').write_text(json.dumps(summary, indent=2))
state['published'] = True; state['publishedHashes'] = {rel: sha(R/rel) for rel in state['outputs']}
STATE.write_text(json.dumps(state, indent=2))
print('Published linked prefabs and the TWO saved scenes only. No live Editor scene save/reload or Play manipulation.')
