"""Build/publish visual-only forest wall LODs and retime existing tree LOD transitions.
Owned closed workers only; never saves/reloads live Editor scenes, enters Play or bakes nav.
"""
import argparse,hashlib,json,os,shutil,subprocess
from pathlib import Path
R=Path(__file__).resolve().parents[1];STATE=R/'Library/WorldLodBaseline.json'
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--action',required=True,choices=['art','build','publish']);a=p.parse_args()
s=json.loads(STATE.read_text());W=Path(s['worker'])
if (W/'.shared-gameplay-prefabs-owned').read_text()!=str(R):raise SystemExit('Project-owned worker required')
lock=W/'Temp/UnityLockfile'
if lock.exists():
    with lock.open('r+b'):pass
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
art='Assets/Art/Environment/GiantForest2';folder=art+'/WallDistance'
names=['GF2_Wall_Straight_24x60','GF2_Wall_Corner_24x60','GF2_Wall_Gateway_48x60','GF2_Tree_A_Sentinel','GF2_Tree_B_Leaning','GF2_Tree_C_OldFork']
prefabs=[art+'/Prefabs/'+n+'.prefab' for n in names]
for rel,h in s['before'].items():
    if not (R/rel).is_file() or sha(R/rel)!=h:raise SystemExit('Concurrent source edit: '+rel)
if a.action=='art':
    if (R/folder).exists():raise SystemExit('New distance folder already exists; no overwrite')
    for f in (R/'Assets').rglob('*'):
        if f.is_file():
            rel=f.relative_to(R);target=W/rel
            if not target.exists() or sha(f)!=sha(target):target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,target)
    for request in (W/'Library').glob('*.request'):request.unlink()
    log=W/'forest-distance-blender.log'
    with log.open('w') as out:
        process=subprocess.run(['C:/Program Files/Blender Foundation/Blender 4.5/blender.exe','--background','--factory-startup','--python',str(R/'tools/blender/build_forest_wall_lods.py')],env=dict(os.environ,ODM_SOURCE_ROOT=str(R),ODM_LOD_WORKER=str(W)),stdout=out,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL,timeout=600)
    text=log.read_text(errors='replace')
    for line in text.splitlines():
        if 'FOREST_WALL_LOD_' in line or 'Error' in line:print(line)
    if process.returncode or 'FOREST_WALL_LOD_ART_COMPLETE' not in text:raise SystemExit('Art failed: '+str(log))
    s['artComplete']=True;STATE.write_text(json.dumps(s,indent=2));raise SystemExit()
if a.action=='build':
    if not s.get('artComplete'):raise SystemExit('Run --action art first')
    for rel in prefabs+['Assets/Editor/ForestDistanceDetailBuilder.cs','Assets/Editor/ForestDistanceDetailBuilder.cs.meta']:
        shutil.copy2(R/rel,W/rel)
    log=W/'forest-distance-native.log'
    process=subprocess.run(['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe','-batchmode','-quit','-projectPath',str(W),'-executeMethod','ForestDistanceDetailBuilder.BuildOwned','-logFile',str(log)],env=dict(os.environ,ODM_FOREST_DISTANCE_OWNED='1'),stdin=subprocess.DEVNULL,timeout=1200)
    text=log.read_text(errors='replace')
    for line in text.splitlines():
        if any(x in line for x in ['FOREST_DISTANCE_', 'error CS', 'Exception:']):print(line)
    if process.returncode or 'FOREST_DISTANCE_COMPLETE' not in text:raise SystemExit('Native authoring failed; source prefabs unchanged: '+str(log))
    s['built']=True;s['builderSha256']=sha(R/'Assets/Editor/ForestDistanceDetailBuilder.cs')
    s['outputs']={rel:sha(W/rel) for rel in prefabs+[folder+'.meta']}
    s['outputs'].update({f.relative_to(W).as_posix():sha(f) for f in (W/folder).rglob('*') if f.is_file()})
    STATE.write_text(json.dumps(s,indent=2));raise SystemExit()
if not s.get('built') or s.get('published') or sha(R/'Assets/Editor/ForestDistanceDetailBuilder.cs')!=s['builderSha256']:raise SystemExit('Current unpublished build required')
if (R/folder).exists():raise SystemExit('Refusing existing distance assets')
for rel,h in s['outputs'].items():
    if sha(W/rel)!=h:raise SystemExit('Worker output changed: '+rel)
backup=R/'Library/WorldLodOriginalPrefabs';backup.mkdir(exist_ok=True)
for rel in prefabs:shutil.copy2(R/rel,backup/Path(rel).name)
shutil.copytree(W/folder,R/folder);shutil.copy2(W/(folder+'.meta'),R/(folder+'.meta'))
for rel in prefabs:shutil.copy2(W/rel,R/rel)
out=R/'ArtSource/Performance/ForestDistanceDetail';out.mkdir(parents=True,exist_ok=True)
for f in (W/'Library/ForestDistanceDetail').glob('*'):shutil.copy2(f,out/f.name)
for rel in ['Library/forest-wall-lod-art.json','forest-distance-native.log','forest-distance-blender.log']:shutil.copy2(W/rel,out/Path(rel).name)
s['published']=True;s['publishedSha256']={rel:sha(R/rel) for rel in s['outputs']};STATE.write_text(json.dumps(s,indent=2))
print('Published ONLY six visual prefabs + new wall distance assets. Saved scenes/physics/nav/source art untouched; no live Editor control.')
