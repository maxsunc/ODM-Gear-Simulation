"""Explicit Shiganshina-only asset authoring, map assembly, native capture and publication.
Never controls the user's Editor/Blender. Reuses the CLOSED marker-owned forest worker.
"""
from pathlib import Path
import argparse, hashlib, json, os, shutil, subprocess, stat
R=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--action',required=True,choices=['prepare','art','build','capture','publish']);p.add_argument('--revise',action='store_true',help='Explicit unpublished worker-only map revision');a=p.parse_args()
S=R/'Library/ShiganshinaBuildRun.json';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
if a.action=='prepare':
 w=Path(json.loads((R/'Library/GiantForest2BuildRun.json').read_text())['project'])
 assert (w/'.giant-forest-owned').read_text()==str(R)
 lock=w/'Temp/UnityLockfile'
 if lock.exists():
  with lock.open('r+b'):pass
 assert not S.exists(),'Explicit existing owned-run review required before resnapshot'
 before={f.relative_to(R).as_posix():sha(f) for base in ['Assets','ProjectSettings'] for f in (R/base).rglob('*') if f.is_file()}
 def remove_readonly(function,path,error):
  os.chmod(path,stat.S_IWRITE);function(path)
 shutil.rmtree(w/'Assets',onexc=remove_readonly);shutil.copytree(R/'Assets',w/'Assets');shutil.copytree(R/'ProjectSettings',w/'ProjectSettings',dirs_exist_ok=True)
 manifest=json.loads((R/'Packages/manifest.json').read_text());manifest['dependencies'].pop('com.coplaydev.unity-mcp',None);(w/'Packages/manifest.json').write_text(json.dumps(manifest,indent=2))
 (w/'.shiganshina-owned').write_text(str(R))
 state={'worker':str(w),'before':before,'built':False,'captured':False,'published':False}
 S.write_text(json.dumps(state,indent=2));print('Owned current-source snapshot ready:',w);raise SystemExit()
state=json.loads(S.read_text());w=Path(state['worker']);assert (w/'.shiganshina-owned').read_text()==str(R)
lock=w/'Temp/UnityLockfile'
if lock.exists():
 with lock.open('r+b'):pass
if a.action=='art':
 env=dict(os.environ,SG_PROJECT_ROOT=str(R),SG_WORKER=str(w));log=w/'sg-blender.log'
 if a.revise:
  assert not state['published'];env['SG_REVISE_ART_OWNED']='1'
 with log.open('w') as output:
  process=subprocess.Popen(['C:/Program Files/Blender Foundation/Blender 4.5/blender.exe','--background','--factory-startup','--python',str(R/'tools/blender/build_shiganshina_assets.py')],env=env,stdout=output,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL)
 state['blenderPID']=process.pid;state['artScriptHash']=sha(R/'tools/blender/build_shiganshina_assets.py');state['built']=False;state['captured']=False;S.write_text(json.dumps(state,indent=2));print('Owned Blender PID:',process.pid,'log:',log);raise SystemExit()
if a.action in ['build','capture']:
 if a.action=='build':
  assert 'SG_BLENDER_COMPLETE' in (w/'sg-blender.log').read_text(errors='replace'),'Wait for owned art export/render'
  assert sha(R/'tools/blender/build_shiganshina_assets.py')==state['artScriptHash'],'Art source changed; explicitly rebuild art first'
 if a.action=='build' and a.revise:
  assert not state['published'],'Published revisions require a new current-source baseline'
  for rel in ['Assets/Art/Environment/Shiganshina/Materials','Assets/Art/Environment/Shiganshina/Prefabs','Assets/Environment/Shiganshina','Assets/Navigation/Titans/Shiganshina']:
   if (w/rel).exists():shutil.rmtree(w/rel)
  (w/'Assets/Scenes/ShiganshinaDistrict.unity').unlink(missing_ok=True)
  state['built']=False;state['captured']=False;S.write_text(json.dumps(state,indent=2))
 for rel in ['Assets/Editor/ShiganshinaDistrictBuilder.cs']:
  shutil.copy2(R/rel,w/rel)
  if Path(str(R/rel)+'.meta').exists():shutil.copy2(str(R/rel)+'.meta',str(w/rel)+'.meta')
 log=w/('sg-'+a.action+'.log');cmd=['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe','-batchmode','-quit','-projectPath',str(w),'-executeMethod','ShiganshinaDistrictBuilder.'+('BuildOwned' if a.action=='build' else 'CaptureOwned'),'-logFile',str(log)]
 if a.action=='build':cmd.append('-nographics')
 result=subprocess.run(cmd,stdin=subprocess.DEVNULL,timeout=1200);text=log.read_text(errors='replace')
 for line in text.splitlines():
  if line.startswith('SG_') or any(q in line for q in ['error CS','Exception:']):print(line)
 assert result.returncode==0 and ('SG_BUILD_COMPLETE' if a.action=='build' else 'SG_CAPTURE_COMPLETE') in text,str(log)
 state['built' if a.action=='build' else 'captured']=True
 if a.action=='build':state['builderHash']=sha(R/'Assets/Editor/ShiganshinaDistrictBuilder.cs')
 if a.action=='capture':
  out=R/'ArtSource/Environment/Shiganshina/MapReview';out.mkdir(parents=True,exist_ok=True)
  for f in (w/'Library/Shiganshina').glob('*.png'):shutil.copy2(f,out/f.name)
  shutil.copy2(log,out/'native-map-preview.log')
 S.write_text(json.dumps(state,indent=2));raise SystemExit()
if a.action=='publish':
 assert state['built'] and state['captured'];assert sha(R/'Assets/Editor/ShiganshinaDistrictBuilder.cs')==state['builderHash']
 # Publish NEW map assets only. Every pre-existing asset must still match the starting source.
 for rel,h in state['before'].items():assert sha(R/rel)==h,('Concurrent or unintended source edit; refusing publication',rel)
 folders=['Assets/Art/Environment/Shiganshina','Assets/Environment/Shiganshina','Assets/Navigation/Titans/Shiganshina']
 files=['Assets/Scenes/ShiganshinaDistrict.unity','Assets/Scenes/ShiganshinaDistrict.unity.meta']
 for rel in folders+files:assert not (R/rel).exists(),('New owned destination already exists; explicit revision needed',rel)
 for rel in folders:
  shutil.copytree(w/rel,R/rel)
  if Path(str(w/rel)+'.meta').exists():shutil.copy2(str(w/rel)+'.meta',str(R/rel)+'.meta')
 for rel in files:shutil.copy2(w/rel,R/rel)
 # Append only this scene; retain exact original ordering/enabled flags/GUIDs.
 settings='ProjectSettings/EditorBuildSettings.asset';original=(R/settings).read_text();new=(w/settings).read_text()
 assert new.count('path: Assets/Scenes/ShiganshinaDistrict.unity')==1
 before_lines=[l for l in original.splitlines() if 'path:' in l];after_lines=[l for l in new.splitlines() if 'path:' in l and 'ShiganshinaDistrict' not in l];assert before_lines==after_lines
 shutil.copy2(w/settings,R/settings)
 out=R/'ArtSource/Environment/Shiganshina';shutil.copy2(w/'Library/Shiganshina/map-report.json',out/'map-report.json');shutil.copy2(w/'sg-build.log',out/'native-map-build.log');shutil.copy2(w/'sg-blender.log',out/'blender-build.log')
 state['published']=True;state['publishedHashes']={f.relative_to(R).as_posix():sha(f) for folder in folders for f in (R/folder).rglob('*') if f.is_file()};state['publishedHashes'].update({rel:sha(R/rel) for rel in files+[settings]});S.write_text(json.dumps(state,indent=2))
 print('Published NEW Shiganshina map/art/navigation only, and append-only Build Settings. All previous assets unchanged.')
