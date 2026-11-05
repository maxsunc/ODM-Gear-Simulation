"""Append missing GiantForest2 navigation in an owned Unity2021 worker.
Snapshot CURRENT saved assets; never rebuild scenery or control the live Editor.
Publication merges ONLY the navigation component and appends new data/agent types.
"""
import argparse,hashlib,json,os,re,shutil,stat,subprocess,tempfile,time
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
STATE=ROOT/'Library/GiantForest2NavigationRun.json'
SCENE='Assets/Scenes/GiantForest2.unity'
SETTINGS='ProjectSettings/NavMeshAreas.asset'
NAV='Assets/Navigation/Titans/GiantForest2'
GUID=b'69a5bdbbfcef14f42ac1f6ce0bbc8ec1'
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--action',choices=['snapshot','bake','test','publish'],required=True)
p.add_argument('--reuse',type=Path)
a=p.parse_args()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def owned(project):
 assert project!=ROOT and (project/'.giant-forest-owned').read_text()==str(ROOT),'Not this project\'s owned worker'
 lock=project/'Temp/UnityLockfile'
 if lock.exists():
  with lock.open('r+b'):pass

def navblock(data):
 blocks=re.split(rb'(?m)(?=^--- !u!)',data)
 matches=[b for b in blocks if GUID in b and b.startswith(b'--- !u!114 ')]
 assert len(matches)==1,'Expected one navigation component'
 return matches[0]

if a.action=='snapshot':
 if a.reuse:project=a.reuse.resolve()
 else:
  project=Path(tempfile.mkdtemp(prefix='ODM-GiantForestNav-'))
  (project/'.giant-forest-owned').write_text(str(ROOT))
 owned(project)
 # Only disposable, marker-owned Assets are replaced. Reuse its import cache.
 def writable_retry(function,path,error):
  assert Path(path).resolve().is_relative_to((project/'Assets').resolve()),'Owned Assets only'
  os.chmod(path,stat.S_IWRITE);function(path)
 if (project/'Assets').exists():shutil.rmtree(project/'Assets',onexc=writable_retry)
 shutil.copytree(ROOT/'Assets',project/'Assets')
 shutil.copytree(ROOT/'ProjectSettings',project/'ProjectSettings',dirs_exist_ok=True)
 (project/'Packages').mkdir(exist_ok=True)
 manifest=json.loads((ROOT/'Packages/manifest.json').read_text());manifest['dependencies'].pop('com.coplaydev.unity-mcp',None)
 (project/'Packages/manifest.json').write_text(json.dumps(manifest,indent=2))
 before={f.relative_to(ROOT).as_posix():sha(f) for f in (ROOT/'Assets').rglob('*') if f.is_file()}
 before.update({f.relative_to(ROOT).as_posix():sha(f) for f in (ROOT/'ProjectSettings').rglob('*') if f.is_file()})
 state={'project':str(project),'before':before,'snapshotAt':time.time()}
 STATE.write_text(json.dumps(state,indent=2));print('Snapshotted current saved project into',project);raise SystemExit()

state=json.loads(STATE.read_text());project=Path(state['project']);owned(project)
report=project/'Library/GiantForest2/navigation-build-report.json'
result=project/'gf2-navigation-results.xml'
if a.action=='publish':
 assert report.exists() and result.exists() and result.stat().st_mtime>report.stat().st_mtime,'Fresh bake/tests required'
 stats=ET.parse(result).getroot().attrib;assert stats.get('failed')=='0' and int(stats.get('passed','0'))>=3,stats
 for rel,old in state['before'].items():assert sha(ROOT/rel)==old,('Source changed during bake; resnapshot rather than overwrite',rel)
 before=(ROOT/SCENE).read_bytes();after=(project/SCENE).read_bytes();oldblock=navblock(before);newblock=navblock(after)
 newline=b'\r\n' if b'\r\n' in before else b'\n';newblock=newblock.replace(b'\r\n',b'\n').replace(b'\n',newline)
 assert oldblock.split(b'  profiles:')[0]==newblock.split(b'  profiles:')[0],'Navigation identity/geometry roots changed'
 merged=before.replace(oldblock,newblock);assert merged.replace(newblock,b'')==before.replace(oldblock,b''),'Non-navigation scene edit'
 old=(ROOT/SETTINGS).read_text();new=(project/SETTINGS).read_text()
 def area_prefix(text):return re.sub(r'  m_LastAgentTypeID: -?\d+', '  m_LastAgentTypeID: <new allocation>',text.split('  m_Settings:\n')[0])
 assert area_prefix(old)==area_prefix(new),'Navigation areas changed'
 def agents(text):return re.findall(r'  - serializedVersion:.*?(?=  - serializedVersion:|  m_SettingNames:)',text,re.S)
 oldagents,newagents=agents(old),agents(new);assert newagents[:len(oldagents)]==oldagents and len(newagents)==len(oldagents)+2,'Only append two agent types'
 assert new.split('  m_SettingNames:\n')[1].startswith(old.split('  m_SettingNames:\n')[1]),'Existing agent names changed'
 for size in [3,5,6]:
  for suffix in ['.asset','.asset.meta']:assert (ROOT/f'{NAV}/Titan_{size}x{suffix}').read_bytes()==(project/f'{NAV}/Titan_{size}x{suffix}').read_bytes(),'Old bake changed'
 for size in [7,10]:
  for suffix in ['.asset','.asset.meta']:
   rel=f'{NAV}/Titan_{size}x{suffix}';assert not (ROOT/rel).exists(),('Refusing to replace an existing asset',rel);assert (project/rel).is_file()
 backup=ROOT/'Library/GiantForest2NavigationBackup'/time.strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True)
 shutil.copy2(ROOT/SCENE,backup/'GiantForest2.unity');shutil.copy2(ROOT/SETTINGS,backup/'NavMeshAreas.asset')
 for size in [7,10]:
  for suffix in ['.asset','.asset.meta']:
   rel=f'{NAV}/Titan_{size}x{suffix}';shutil.copy2(project/rel,ROOT/rel)
 shutil.copy2(project/SETTINGS,ROOT/SETTINGS);(ROOT/SCENE).write_bytes(merged)
 evidence=ROOT/'ArtSource/Environment/GiantForest2/NavigationReview';evidence.mkdir(parents=True,exist_ok=True)
 shutil.copy2(report,evidence/report.name);shutil.copy2(result,evidence/'navigation-results.xml')
 state['published']={'at':time.time(),'nonNavigationSceneBytesPreserved':True,'tests':stats,'scene':sha(ROOT/SCENE),'backup':str(backup)}
 STATE.write_text(json.dumps(state,indent=2));print('Published ONLY 7x/10x assets, two new agent types and navigation scene component. All other scene bytes preserved.');raise SystemExit()

# No stale report/result can be mistaken for this run's evidence.
if a.action=='bake':report.unlink(missing_ok=True)
else:result.unlink(missing_ok=True)
editor=os.environ.get('UNITY_2021_EDITOR','C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe')
command=[editor,'-batchmode','-nographics','-projectPath',str(project),'-logFile',str(project/('gf2-navigation-'+a.action+'.log'))]
if a.action=='bake':command+=['-quit','-executeMethod','GiantForest2Builder.BakeMissingNavigationBatch']
else:command+=['-runTests','-testPlatform','PlayMode','-testFilter','GiantForest2NavigationPlayModeTests','-testResults',str(result)]
print(json.dumps({'project':str(project),'action':a.action}),flush=True)
r=subprocess.run(command,timeout=1200,stdin=subprocess.DEVNULL)
if a.action=='bake':assert r.returncode==0 and report.is_file(),'Bake failed; inspect owned log'
else:
 assert result.is_file(),'No fresh test results';stats=ET.parse(result).getroot().attrib;print(stats)
 assert stats.get('failed')=='0' and int(stats.get('passed','0'))>=3,stats
raise SystemExit(r.returncode)
