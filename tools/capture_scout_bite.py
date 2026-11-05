"""Render actual Scout bite/physical death in the OWNED Unity2021 validation project.
Run tools/run_titan_validation.py --wait first. Never touches the live editor/scene/input.
"""
import json, os, subprocess, xml.etree.ElementTree as ET
from pathlib import Path
root = Path(__file__).resolve().parents[1]
project = Path(json.loads((root / 'Library/TitanValidationRun.json').read_text())['project'])
if not (project / '.titan-validation-owned').is_file():
    raise SystemExit('Not an owned disposable validation project')
lock = project / 'Temp/UnityLockfile'
if lock.exists():
    try:
        with lock.open('r+b'):
            pass
    except PermissionError:
        raise SystemExit('Owned validation project still running')
output = root / 'Library/ScoutBiteReview'
output.mkdir(parents=True, exist_ok=True)
for p in output.glob('ScoutBite_*.png'):
    p.unlink()
env = dict(os.environ, ODM_SCOUT_BITE_PREVIEW=str(output))
editor = os.environ.get('UNITY_2021_EDITOR', r'C:\Program Files\Unity\Hub\Editor\2021.3.25f1\Editor\Unity.exe')
results = project / 'bite-graphics-results.xml'
results.unlink(missing_ok=True)
command = [editor, '-batchmode', '-projectPath', str(project), '-runTests', '-testPlatform', 'PlayMode',
           '-testFilter', 'TitanODMIntegrationPlayModeTests.ChompCanRenderRealScoutRagdollAndBloodInOwnedGraphicsPreview',
           '-testResults', str(results), '-logFile', str(project / 'bite-graphics.log')]
r = subprocess.run(command, env=env, cwd=project, timeout=240)
if results.exists():
    print(ET.parse(results).getroot().attrib)
print('Owned preview directory:', output)
raise SystemExit(r.returncode)
