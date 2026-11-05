"""Render the strike in the OWNED Unity2021 validation project, never live FreeRoam.
Run tools/run_titan_validation.py --wait first to copy current sources/assets.
Uses graphics (no -nographics) and a disposable runtime PlayMode fixture.
"""
import json,os,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[1]
metadata=json.loads((root/'Library/TitanValidationRun.json').read_text())
project=Path(metadata['project'])
if not (project/'.titan-validation-owned').is_file():raise SystemExit('Not an owned validation project')
lock=project/'Temp/UnityLockfile'
if lock.exists():
 try:
  with lock.open('r+b'):pass
 except PermissionError:raise SystemExit('Validation project is still running')
output=root/'Library/TitanSpawnerReview';output.mkdir(parents=True,exist_ok=True)
env=dict(os.environ,ODM_TITAN_STRIKE_PREVIEW=str(output))
editor=os.environ.get('UNITY_2021_EDITOR',r'C:\Program Files\Unity\Hub\Editor\2021.3.25f1\Editor\Unity.exe')
results=project/'strike-graphics-results.xml'
results.unlink(missing_ok=True)
command=[editor,'-batchmode','-projectPath',str(project),'-runTests','-testPlatform','PlayMode','-testFilter','TitanRandomSpawnerPlayModeTests.StrikeMaterialsCompileAndCanRenderInOwnedGraphicsPreview','-testResults',str(results),'-logFile',str(project/'strike-graphics.log')]
r=subprocess.run(command,env=env,cwd=project,timeout=240)
if results.exists():print(ET.parse(results).getroot().attrib)
print('Preview directory:',output)
raise SystemExit(r.returncode)
