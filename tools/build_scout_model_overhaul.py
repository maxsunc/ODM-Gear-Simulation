"""Model-only Scout art workflow: owned background Blender and closed Unity 2021.

Actions: prepare, art, native, test, publish. No live Editor/Blender automation.
Publishing is explicit and refuses concurrent changes to protected source files.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / 'Library/ScoutModelOverhaul'
ASSETS = Path('Assets/Art/Player/Scout/ModelOverhaul')
ART = Path('ArtSource/Player/Scout/ModelOverhaul')
PREFAB = Path('Assets/Prefabs/Player/ScoutVisual.prefab')
FILTER = 'ScoutPlayerPlayModeTests;ScoutHeldStrugglePlayModeTests;ScoutCapeClothPlayModeTests;PlayerOnlyRespawn;RealHooksBoostAndCoroutinesSuspendWithoutChangingFreeFlightTuning;RealGroundHoldAlsoSuspendsAndReleasesAtCurrentPoseNotCapturePose;RealHealthIsSingleSourceAndExternalDeathReleasesBeforeRagdoll;HeldAndReleasePendingRejectEvenForcedBaseDamageContacts;WallTeleportCancellationAndCaptureInvalidateImpactReceipts'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--action', required=True, choices=['prepare','art','native','test','publish'])
parser.add_argument('--native-worker', type=Path)
parser.add_argument('--confirm-reviewed-art', action='store_true', help='Required for explicit source publication after inspecting native previews')
args = parser.parse_args()

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding='utf-8')
def owned(path, marker):
    if path.resolve() == ROOT or not (path / marker).is_file() or Path((path / marker).read_text()).resolve() != ROOT:
        raise SystemExit('Not an owned worker: ' + str(path))
    lock = path / 'Temp/UnityLockfile'
    if lock.exists():
        with lock.open('r+b'):
            pass

def protect(allow_prefab=False, updating_authored=False):
    baseline = json.loads((STATE / 'baseline.json').read_text())
    changed = [p for p,h in baseline.items() if not (ROOT/p).is_file() or sha(ROOT/p) != h]
    allowed = [PREFAB.as_posix()] if allow_prefab else []
    published_file = STATE / 'published.json'
    if published_file.exists():
        published = json.loads(published_file.read_text())
        for p, h in published['authoredFiles'].items():
            if not updating_authored and (not (ROOT/p).is_file() or sha(ROOT/p) != h):
                raise SystemExit('Published art was edited; reconcile before overwriting: ' + p)
        if sha(ROOT/PREFAB) == published['prefabSha256']:
            allowed.append(PREFAB.as_posix())
    if any(p not in allowed for p in changed):
        raise SystemExit('Protected existing source changed; inspect without overwriting: ' + repr(changed))
    return baseline, changed

STATE.mkdir(parents=True, exist_ok=True)
if args.action == 'prepare':
    if (STATE/'run.json').exists():
        raise SystemExit('Existing overhaul state; review it instead of replacing the baseline')
    worker = Path(tempfile.mkdtemp(prefix='ODM-ScoutModel-'))
    (worker/'.scout-model-overhaul-owned').write_text(str(ROOT))
    files = [p for folder in ['Assets','ProjectSettings','ArtSource/Player/Scout'] for p in (ROOT/folder).rglob('*') if p.is_file()]
    write_json(STATE/'baseline.json',{p.relative_to(ROOT).as_posix():sha(p) for p in files})
    write_json(STATE/'run.json',{'source':str(ROOT),'blenderWorker':str(worker),'nativeWorker':str(args.native_worker) if args.native_worker else ''})
    for rel in [PREFAB,Path('Assets/Art/Player/Scout/ScoutPlayer.fbx'),Path('ArtSource/Player/Scout/ScoutPlayer.blend')]:
        target=STATE/'Original'/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/rel,target)
    print(worker)
    raise SystemExit(0)

state = json.loads((STATE/'run.json').read_text())
worker = Path(state['blenderWorker'])
owned(worker,'.scout-model-overhaul-owned')
if args.native_worker:
    state['nativeWorker']=str(args.native_worker.resolve())
native=Path(state['nativeWorker']) if state.get('nativeWorker') else None
env=os.environ.copy()
env.update({'SCOUT_SOURCE_ROOT':str(ROOT),'SCOUT_MODEL_WORKER':str(worker),'ODM_SCOUT_MODEL_OWNED':'1'})
editor=os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe')

if args.action == 'art':
    protect()
    blender=os.environ.get('SCOUT_BLENDER_EDITOR','C:/Program Files/Blender Foundation/Blender 5.2/blender.exe')
    with (worker/'blender-overhaul.log').open('w',encoding='utf-8') as log:
        subprocess.run([blender,'--background','--factory-startup','--disable-autoexec','--python',str(ROOT/'tools/blender/overhaul_scout_model.py')],env=env,stdout=log,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL,check=True,timeout=600)
    if not (worker/'body-geometry.json').exists():
        raise SystemExit('Blender did not produce geometry; inspect its log')
    protect()
elif args.action == 'native':
    protect()
    if native is None: raise SystemExit('Supply a closed worker created by tools/run_titan_validation.py')
    owned(native,'.titan-validation-owned')
    shutil.copytree(worker/ASSETS,native/ASSETS,dirs_exist_ok=True)
    shutil.copytree(ROOT/'Assets/Renderer',native/'Assets/Renderer',dirs_exist_ok=True)
    for rel in [PREFAB,Path('Assets/Editor/ScoutModelOverhaulBuilder.cs'),Path('Assets/Editor/ScoutModelOverhaulBuilder.cs.meta')]:
        (native/rel).parent.mkdir(parents=True,exist_ok=True)
        source = STATE/'Original'/rel if rel == PREFAB else ROOT/rel
        shutil.copy2(source,native/rel)
    subprocess.run([editor,'-batchmode','-quit','-projectPath',str(native),'-executeMethod','ScoutModelOverhaulBuilder.BuildOwned','-logFile',str(native/'scout-model-native.log')],env=env,stdin=subprocess.DEVNULL,check=True,timeout=300)
    receipt=json.loads((native/'Library/ScoutModelOverhaul/native-authoring-report.json').read_text())
    if receipt['geometrySha256']!=sha(worker/'body-geometry.json'):raise SystemExit('Stale native geometry receipt')
    protect()
elif args.action == 'test':
    # Do not use --reuse here: that runner refreshes Assets, deleting this unpublished model.
    owned(native,'.titan-validation-owned')
    subprocess.run([editor,'-batchmode','-nographics','-projectPath',str(native),'-runTests','-testPlatform','PlayMode','-testFilter',FILTER,'-testResults',str(native/'scout-model-regressions.xml'),'-logFile',str(native/'scout-model-regressions.log')],env=env,stdin=subprocess.DEVNULL,timeout=300)
    results=ET.parse(native/'scout-model-regressions.xml').getroot()
    print(json.dumps(results.attrib,indent=2))
    # Preserve actual failures, never change assertions or call a partial run a full pass.
    protect()
elif args.action == 'publish':
    if not args.confirm_reviewed_art:raise SystemExit('Inspect native before/after images and regression results before explicit publication')
    owned(native,'.titan-validation-owned')
    baseline,_=protect()
    receipt=json.loads((native/'Library/ScoutModelOverhaul/native-authoring-report.json').read_text())
    if receipt['geometrySha256']!=sha(worker/'body-geometry.json'):raise SystemExit('Stale native receipt')
    old=(STATE/'Original'/PREFAB).read_text();new=(native/PREFAB).read_text()
    oldmesh='  m_Mesh: {fileID: -2717223225658077861, guid: 5db373108df50de4b8ed617a078a58d7, type: 3}'
    material_guid=re.search(r'^guid: (\w+)',(native/ASSETS/'ScoutBody_Refined.mat.meta').read_text(),re.M)[1]
    mesh_guid=re.search(r'^guid: (\w+)',(native/ASSETS/'ScoutBody_Refined.asset.meta').read_text(),re.M)[1]
    oldmaterial='  - {fileID: 2100000, guid: c7b7517018d162d4da0e7f260aa78647, type: 2}'
    # The material is shared with the cape/weapons: replace ONLY in the body renderer block.
    renderer_start=old.rfind('--- !u!137',0,old.index(oldmesh))
    renderer_end=old.find('\n--- !u!',old.index(oldmesh))
    before=old[renderer_start:renderer_end]
    after=before.replace(oldmesh,'  m_Mesh: {fileID: 4300000, guid: '+mesh_guid+', type: 2}').replace(oldmaterial,'  - {fileID: 2100000, guid: '+material_guid+', type: 2}')
    expected=old[:renderer_start]+after+old[renderer_end:]
    if expected!=new:raise SystemExit('Prefab changed beyond the body mesh/material references; inspect diff')
    result=native/'scout-model-regressions.xml'
    if not result.exists():raise SystemExit('Run existing regressions before publication')
    tests=ET.parse(result).getroot().attrib
    shutil.copytree(native/ASSETS,ROOT/ASSETS,dirs_exist_ok=True)
    shutil.copy2(native/(str(ASSETS)+'.meta'),ROOT/(str(ASSETS)+'.meta'))
    shutil.copytree(worker/ART,ROOT/ART,dirs_exist_ok=True)
    for p in (native/'Library/ScoutModelOverhaul').glob('*'):
        if p.is_file():shutil.copy2(p,ROOT/ART/p.name)
    for src,target in [(native/'scout-model-native.log','native-authoring.log'),(native/'scout-model-regressions.xml','existing-regressions.xml'),(native/'scout-model-regressions.log','existing-regressions.log'),(worker/'blender-overhaul.log','blender-authoring.log')]:
        shutil.copy2(src,ROOT/ART/target)
    shutil.copy2(native/PREFAB,ROOT/PREFAB)
    baseline,changed=protect(allow_prefab=True, updating_authored=True)
    authored = [p for p in (ROOT/ASSETS).rglob('*') if p.is_file()] + [ROOT/(str(ASSETS)+'.meta'),ROOT/ART/'ScoutPlayer_Refined.blend',ROOT/ART/'ScoutPlayer_Refined.fbx']
    write_json(STATE/'published.json',{'prefabSha256':sha(ROOT/PREFAB),'authoredFiles':{p.relative_to(ROOT).as_posix():sha(p) for p in authored}})
    write_json(ROOT/ART/'preservation-and-validation.json',{'protectedExistingPaths':len(baseline),'unchangedExistingPaths':len(baseline)-len(changed),'changedExistingPaths':changed,'nativeReport':receipt,'existingRegressions':tests,'onlyPrefabChanges':'ScoutBody mesh and material references','originalFbxBlendCapeWeaponsRigClipsPhysicsUnchanged':True,'noLiveSceneOrBlenderChanges':True,'sourcePrefabSha256':sha(ROOT/PREFAB),'nativeMeshSha256':sha(ROOT/ASSETS/'ScoutBody_Refined.asset'),'publication':'Local files only; no commit/push','limits':['Static poses are not moving gameplay or cloth certification','Original short skinned cape shown in static previews; runtime simulated cape unchanged','Existing failures retained; no new test code/assertion edits','Higher body triangle count and 2K atlas require real-device profiling for Android']})
write_json(STATE/'run.json',state)
