"""Author/inspect/capture GiantForest2 in an OWNED full-project Unity2021 worker.
Never opens/stops/saves the live Editor. Publication is an explicit scoped step.
"""
import argparse,json,os,shutil,subprocess,tempfile,hashlib,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path);p.add_argument('--action',choices=['inspect','snapshot','prepare','build','resize','capture','test','graphics','publish'],default='inspect');a=p.parse_args()
state=ROOT/'Library/GiantForest2BuildRun.json'
if a.project:project=a.project.resolve()
elif state.exists():project=Path(json.loads(state.read_text())['project'])
else:
 project=Path(tempfile.mkdtemp(prefix='ODM-GiantForest2-'));(project/'.giant-forest-owned').write_text(str(ROOT))
 for folder in ['Assets','ProjectSettings']:shutil.copytree(ROOT/folder,project/folder)
 (project/'Packages').mkdir();manifest=json.loads((ROOT/'Packages/manifest.json').read_text());manifest['dependencies'].pop('com.coplaydev.unity-mcp',None);(project/'Packages/manifest.json').write_text(json.dumps(manifest,indent=2))
 snapshot={}
 for path in ['Assets/Scenes/GiantForest2.unity','Assets/Scenes/GiantForest2.unity.meta','ProjectSettings/NavMeshAreas.asset','ProjectSettings/EditorBuildSettings.asset']:
  snapshot[path]=hashlib.sha256((ROOT/path).read_bytes()).hexdigest()
 state.write_text(json.dumps({'project':str(project),'before':snapshot},indent=2))
assert (project/'.giant-forest-owned').read_text()==str(ROOT),'Not this project\'s owned worker'
lock=project/'Temp/UnityLockfile'
if lock.exists():
 try:
  with lock.open('r+b'):pass
 except PermissionError:raise SystemExit('Owned Unity worker is still running')
if a.action=='snapshot':
 # Explicit authorized iteration: copy CURRENT saved map, preserving user tuning
 # and prefab/meta GUIDs. Never take in-memory/live Play scene state.
 info=json.loads(state.read_text());assert Path(info['project'])==project
 info.setdefault('baseline_history',[]).append(info['before'])
 info['before']={rel:hashlib.sha256((ROOT/rel).read_bytes()).hexdigest() for rel in info['before']}
 info['generated_before']={}
 for rel in ['Assets/Art/Environment/GiantForest2','Assets/Environment/GiantForest2','Assets/Navigation/Titans/GiantForest2']:
  shutil.copytree(ROOT/rel,project/rel,dirs_exist_ok=True)
  for f in (ROOT/rel).rglob('*'):
   if f.is_file():info['generated_before'][f.relative_to(ROOT).as_posix()]=hashlib.sha256(f.read_bytes()).hexdigest()
 for rel in info['before']:shutil.copy2(ROOT/rel,project/rel)
 state.write_text(json.dumps(info,indent=2));print('Snapshotted current saved map into owned worker; protected new publication baseline.');raise SystemExit()
# Sync only authoring/test code, never overwrite generated scene/assets on reuse.
for rel in ([] if a.action=='publish' else ['Assets/Editor/GiantForest2Builder.cs','Assets/Tests/GiantForest2']):
 src=ROOT/rel;dst=project/rel
 if src.is_dir():shutil.copytree(src,dst,dirs_exist_ok=True)
 elif src.exists():dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
 if Path(str(src)+'.meta').exists():shutil.copy2(str(src)+'.meta',str(dst)+'.meta')
editor=Path(os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe'))
if a.action=='prepare':
 blender=os.environ.get('BLENDER_45','C:/Program Files/Blender Foundation/Blender 4.5/blender.exe')
 env=dict(os.environ,GF2_PROJECT_ROOT=str(ROOT),GF2_GAME_PROJECT_ROOT=str(project))
 subprocess.run([blender,'--background','--factory-startup','--python',str(ROOT/'tools/blender/prepare_giant_forest_game_models.py')],env=env,check=True)
 shutil.copytree(project/'GiantForestArtStaging',project/'Assets/Art/Environment/GiantForest2',dirs_exist_ok=True)
 shutil.copy2(ROOT/'tools/templates/GiantForestLit.shader',project/'Assets/Art/Environment/GiantForest2/GiantForestLit.shader')
 raise SystemExit()
if a.action=='publish':
 import xml.etree.ElementTree as E
 info=json.loads(state.read_text());report=project/'Library/GiantForest2/build-report.json';assert report.exists()
 for action,minimum in [('test',6),('graphics',1)]:
  result=project/('gf2-'+action+'-results.xml');assert result.exists(),('Missing validation',action)
  stats=E.parse(result).getroot().attrib
  assert int(stats.get('failed','1'))==0 and int(stats.get('passed','0'))>=minimum,stats
  assert result.stat().st_mtime>report.stat().st_mtime,('Stale validation',action)
 for rel in ['Assets/Editor/GiantForest2Builder.cs','Assets/Tests/GiantForest2/GiantForest2PlayModeTests.cs','Assets/Tests/GiantForest2/ODM.GiantForest2.PlayModeTests.asmdef']:
  assert (ROOT/rel).read_bytes()==(project/rel).read_bytes(),('Validated code differs',rel)
 assert (ROOT/'tools/templates/GiantForestLit.shader').read_bytes()==(project/'Assets/Art/Environment/GiantForest2/GiantForestLit.shader').read_bytes()
 for rel,sha in {**info['before'],**info.get('generated_before',{})}.items():assert hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()==sha,('Source changed during owned build; refusing overwrite',rel)
 # Generated OWNED folders + this cloned scene only. Old art/nav/FreeRoam untouched.
 for rel in ['Assets/Art/Environment/GiantForest2','Assets/Environment/GiantForest2','Assets/Navigation/Titans/GiantForest2']:
  src=project/rel;assert src.is_dir(),rel;shutil.copytree(src,ROOT/rel,dirs_exist_ok=True)
  if Path(str(src)+'.meta').exists():shutil.copy2(str(src)+'.meta',str(ROOT/rel)+'.meta')
 for rel in info['before']:shutil.copy2(project/rel,ROOT/rel)
 review=ROOT/'Library/GiantForest2';shutil.copytree(project/'Library/GiantForest2',review,dirs_exist_ok=True)
 print('Published ONLY generated map folders, cloned scene, new nav types and appended BuildSettings.');raise SystemExit()
command=[str(editor),'-batchmode','-projectPath',str(project),'-logFile',str(project/('gf2-'+a.action+'.log'))]
if a.action not in ['capture','graphics']:command+=['-nographics']
if a.action in ['test','graphics']:
 result=project/('gf2-'+a.action+'-results.xml');result.unlink(missing_ok=True)
 command+=['-runTests','-testPlatform','PlayMode','-testFilter','GiantForest2PlayModeTests'+('.NativeGraphicsShowTheActualPlayableScene' if a.action=='graphics' else ''),'-testResults',str(result)]
else:command+=['-quit','-executeMethod','GiantForest2Builder.'+{'inspect':'InspectBatch','build':'BuildBatch','resize':'ResizeBatch','capture':'CaptureBatch'}[a.action]]
print(json.dumps({'project':str(project),'action':a.action}),flush=True)
r=subprocess.run(command,timeout=1200,stdin=subprocess.DEVNULL)
print('Owned Unity exit:',r.returncode,flush=True)
if a.action in ['test','graphics']:
 import xml.etree.ElementTree as E
 f=project/('gf2-'+a.action+'-results.xml')
 if not f.exists():raise SystemExit('Owned Unity produced no fresh test results')
 results=E.parse(f).getroot().attrib;print(results)
 if int(results.get('failed','0')):raise SystemExit(1)
raise SystemExit(r.returncode)
