"""Run Titan PlayMode tests in a disposable Unity 2021 project, NOT the user's live project.
Usage: python tools/run_titan_validation.py [--wait] [--reuse OWNED_TEMP_PROJECT]
No editor preview-scene simulation or automated scene changes in the working project.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--wait', action='store_true')
parser.add_argument('--reuse', type=Path)
parser.add_argument('--filter', help='Optional targeted diagnostics, NOT a full-suite result')
args = parser.parse_args()
if args.reuse:
    project = args.reuse.resolve()
    if not (project / '.titan-validation-owned').is_file():
        raise SystemExit('Refusing to reuse a project not created by this runner.')
    lock = project / 'Temp' / 'UnityLockfile'
    if lock.exists():
        try:
            with lock.open('r+b'):
                pass
        except PermissionError:
            raise SystemExit('Validation project is still open; wait for that Editor to exit.')
    shutil.rmtree(project / 'Assets')
else:
    project = Path(tempfile.mkdtemp(prefix='ODM-TitanValidation-'))
    (project / '.titan-validation-owned').write_text(str(root), encoding='utf-8')

for relative in ['Assets/Art/Titans/Prototype', 'Assets/Titan/Prototype', 'Assets/Tests/TitanPrototype',
                 'Assets/Art/Player/Scout', 'Assets/Player/Scout', 'Assets/Prefabs/Player',
                 'Assets/Art/Effects', 'Assets/Audio/TitanCombat', 'Assets/Resources/Audio']:
    shutil.copytree(root / relative, project / relative, dirs_exist_ok=True)
# Real user-supplied finisher/NPC clips and their small Resources reference palettes.
for relative in ['Assets/Audio/blood1.wav','Assets/Audio/blood1.wav.meta',
                 'Assets/Audio/blood2.wav','Assets/Audio/blood2.wav.meta',
                 'Assets/Resources/TitanSwordAudioPalette.asset','Assets/Resources/TitanSwordAudioPalette.asset.meta',
                 'Assets/Resources/TitanAudioPalette.asset','Assets/Resources/TitanAudioPalette.asset.meta',
                 'Assets/Resources/TitanStompDust.mat','Assets/Resources/TitanStompDust.mat.meta',
                 'Assets/Resources/TitanStompChunks.mat','Assets/Resources/TitanStompChunks.mat.meta',
                 'Assets/Resources/PlayerImpactPalette.asset','Assets/Resources/PlayerImpactPalette.asset.meta',
                 'Assets/Resources/PlayerDeathBlood.mat','Assets/Resources/PlayerDeathBlood.mat.meta',
                 'Assets/Audio/boneCrush.wav','Assets/Audio/boneCrush.wav.meta',
                 *[str(p.relative_to(root)) for p in (root/'Assets/Audio').iterdir() if p.is_file() and (p.name.startswith(('titanScream','titanFootstep','titanLightning','bloodSplat')))]]:
    target=project / relative;target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(root / relative,target)
# Compile the real opt-in adapter and its narrow legacy dependencies as the default
# assembly. Tests access that assembly by reflection (Unity asmdefs cannot reference it).
shutil.copytree(root / 'Assets/Titan/Integration', project / 'Assets/Titan/Integration', dirs_exist_ok=True)
# Compile the scoped bake-size UI as well; it never autoruns or bakes in tests.
(project / 'Assets/Editor').mkdir(exist_ok=True)
for name in ['TitanFreeRoamNavigationBuilder', 'TitanRandomSpawnerInstaller', 'GameAudioMixerBuilder']:
    for suffix in ['', '.meta']:
        relative='Assets/Editor/'+name+'.cs'+suffix
        shutil.copy2(root / relative, project / relative)
legacy = ['Movement.cs','ODM_Movement.cs','PlayerHealth.cs','PlayerRespawn.cs','Attack.cs','DamageDealer.cs',
          'ThirdPersonCameraController.cs','CameraJitterFix.cs','Scripts/FollowPos.cs',
          'RagdollController.cs','ThirdPersonCam.cs','Scripts/Audio.cs','Scripts/GameAudio.cs','GameElements.cs','SceneManagement.cs',
          'Titan/TitanController.cs','Titan/TitanAttack.cs','Titan/TitanLimb.cs',
          'Titan/TitanNape.cs','Titan/TitanState.cs']
for relative in legacy:
    target=project / 'Assets' / relative; target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(root / 'Assets' / relative,target)
for relative in ['Assets/Prefabs/Titans/ModularTitanPrototype.prefab',
                 'Assets/Scenes/TitanPrototypeSandbox.unity',
                 'Assets/Scenes/TitanAISandbox.unity']:
    target = project / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / relative, target)
    shutil.copy2(root / (relative + '.meta'), str(target) + '.meta')
(project / 'Packages').mkdir(exist_ok=True)
(project / 'Packages' / 'manifest.json').write_text(json.dumps({'dependencies': {
    'com.unity.render-pipelines.universal': '12.1.11',
    'com.unity.test-framework': '1.1.31',
    'com.unity.textmeshpro': '3.0.6',
    'com.unity.ugui': '1.0.0',
    'com.unity.modules.ui': '1.0.0',
    'com.unity.modules.ai': '1.0.0',
    'com.unity.modules.animation': '1.0.0',
    'com.unity.modules.audio': '1.0.0',
    'com.unity.modules.imgui': '1.0.0',
    'com.unity.modules.imageconversion': '1.0.0',
    'com.unity.modules.screencapture': '1.0.0',
    'com.unity.modules.physics': '1.0.0',
    'com.unity.modules.jsonserialize': '1.0.0',
    'com.unity.modules.uielements': '1.0.0',
    'com.unity.modules.particlesystem': '1.0.0',
    'com.unity.modules.terrain': '1.0.0',
    'com.unity.modules.terrainphysics': '1.0.0',
}}, indent=2), encoding='utf-8')
(project / 'ProjectSettings').mkdir(exist_ok=True)
shutil.copy2(root / 'ProjectSettings/ProjectVersion.txt', project / 'ProjectSettings/ProjectVersion.txt')
editor = Path(os.environ.get('UNITY_2021_EDITOR', r'C:\Program Files\Unity\Hub\Editor\2021.3.25f1\Editor\Unity.exe'))
if not editor.is_file():
    raise SystemExit('Set UNITY_2021_EDITOR to the Unity 2021.3.25f1 Editor executable.')
results = project / 'test-results.xml'
if results.exists():
    results.unlink()
command = [str(editor), '-batchmode', '-nographics', '-projectPath', str(project),
           '-runTests', '-testPlatform', 'PlayMode', '-testResults', str(results),
           '-logFile', str(project / 'validation.log')]
if args.filter:command+=['-testFilter',args.filter]
process = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL,
                           creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0)
report = {'project': str(project), 'pid': process.pid, 'results': str(results),
          'log': str(project / 'validation.log'), 'testFilter': args.filter}
(root / 'Library').mkdir(exist_ok=True)
(root / 'Library/TitanValidationRun.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2), flush=True)
if args.wait:
    code = process.wait(timeout=900)
    print('Unity exit code:', code)
    if results.is_file():
        tree = ET.parse(results)
        print('Results:', tree.getroot().attrib)
    raise SystemExit(code)
