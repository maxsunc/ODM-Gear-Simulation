"""Read-only source survey / bounded cape microbenchmark in a CLOSED owned Unity worker.
No Play, synthetic input, physics simulation, user preferences or source scene saves.
This measures isolated Editor-Mono cloth CPU cost, NOT live gameplay FPS/GPU time.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

R = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--label', required=True)
p.add_argument('--inventory-only', action='store_true', help='Skip cloth timing; emit fixed 16:9 camera frustum/LOD estimates only')
p.add_argument('--keep-worker-assets', action='store_true', help='Inventory explicit owned authoring outputs without restoring source assets')
p.add_argument('--fps', type=int, choices=[30, 60, 120], default=60, help='Simulated cloth delta time, not measured FPS')
p.add_argument('--cape-revision', help='Optional historical Git revision for WORKER-ONLY cape A/B; source is never reverted')
p.add_argument('--worker', type=Path, default=Path('C:/Users/maxsu/AppData/Local/Temp/ODM-GiantForest2-iwrxdlei'))
a = p.parse_args()
if not a.label.replace('-', '').replace('_', '').isalnum():
    raise SystemExit('Label must be alphanumeric with optional - or _')
w = a.worker.resolve()
marker = w/'.shared-gameplay-prefabs-owned'
if not marker.is_file() or marker.read_text() != str(R):
    raise SystemExit('Explicit project-owned worker required')
lock = w/'Temp/UnityLockfile'
if lock.exists():
    with lock.open('r+b'): pass
sha = lambda x: hashlib.sha256(x.read_bytes()).hexdigest()
before = {f.relative_to(R).as_posix(): sha(f) for d in ['Assets', 'ProjectSettings'] for f in (R/d).rglob('*') if f.is_file()}
# Incremental current-asset snapshot, including linked prefabs and their scene bindings.
if a.keep_worker_assets and not a.inventory_only:
    raise SystemExit('--keep-worker-assets is restricted to explicit authoring inventories')
for rel, digest in before.items():
    if a.keep_worker_assets or not rel.startswith('Assets/'): continue
    target = w/rel
    if not target.exists() or sha(target) != digest:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(R/rel, target)
revision = None
if a.cape_revision:
    revision = subprocess.check_output(['git','rev-parse','--verify','--end-of-options',a.cape_revision+'^{commit}'],cwd=R,text=True).strip()
    historical = subprocess.check_output(['git','show',revision+':Assets/Player/Scout/ScoutCapeCloth.cs'],cwd=R)
    (w/'Assets/Player/Scout/ScoutCapeCloth.cs').write_bytes(historical)
for request in (w/'Library').glob('*.request'): request.unlink()
helper = r'''
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class OwnedPerformanceSurvey
{
    [Serializable] class Report { public string unity, label, capeSourceSha256, gpu; public int logicalProcessors, quality; public float lodBias; public Map[] maps; public Cloth[] cloth; }
    [Serializable] class Map { public string scene; public int roots, colliders, rigidbodies, activeAnimators, activeSkins, offscreenSkins, savedTitans, enabledCameras; public View[] views; }
    [Serializable] class View { public float pitch, lodBias, cameraAspect; public long estimatedMeshTriangles, estimatedMaterialSubmissions, treeTriangles, wallTriangles, otherTriangles; public int selectedLod0, selectedLod1, selectedLod2, candidateRenderers; }
    [Serializable] class Cloth { public string mode, trajectorySha256; public int warmup, samples; public float simulatedDeltaTime; public double meanMs, medianMs, p95Ms; public float maximumStretch; }
    static readonly string Folder="Library/PerformanceSurvey";
    public static void Run()
    {
        if(!Application.isBatchMode || !File.Exists(".shared-gameplay-prefabs-owned") || Environment.GetEnvironmentVariable("ODM_PERFORMANCE_SURVEY_OWNED")!="1" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Closed owned non-Play worker required");
        Directory.CreateDirectory(Folder);
        string label=Environment.GetEnvironmentVariable("ODM_PERFORMANCE_SURVEY_LABEL");
        var report=new Report{unity=Application.unityVersion,label=label,gpu=SystemInfo.graphicsDeviceName,logicalProcessors=SystemInfo.processorCount,quality=QualitySettings.GetQualityLevel(),lodBias=QualitySettings.lodBias};
        using(var hash=SHA256.Create())report.capeSourceSha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("Assets/Player/Scout/ScoutCapeCloth.cs"))).Replace("-","").ToLowerInvariant();
        var maps=new List<Map>();
        foreach(string name in new[]{"GiantForest2","ShiganshinaDistrict"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Single);
            var roots=scene.GetRootGameObjects();var all=roots.SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
            var skins=all.OfType<SkinnedMeshRenderer>().Where(s=>s.enabled && s.gameObject.activeInHierarchy).ToArray();
            var map=new Map{scene=scene.path,roots=roots.Length,colliders=all.OfType<Collider>().Count(c=>c.enabled && c.gameObject.activeInHierarchy),rigidbodies=all.OfType<Rigidbody>().Count(),activeAnimators=all.OfType<Animator>().Count(c=>c.enabled && c.gameObject.activeInHierarchy),activeSkins=skins.Length,offscreenSkins=skins.Count(s=>s.updateWhenOffscreen),savedTitans=all.OfType<ModularTitanBody>().Count(c=>c.gameObject.activeInHierarchy),enabledCameras=all.OfType<Camera>().Count(c=>c.enabled && c.gameObject.activeInHierarchy)};
            var player=all.OfType<ScoutPlayerAnimation>().Single();var camera=all.OfType<Camera>().Single(c=>c.enabled && c.gameObject.activeInHierarchy && c.CompareTag("MainCamera"));
            camera.aspect=16f/9f;
            var views=new List<View>();
            foreach(float bias in new[]{QualitySettings.lodBias,1f})foreach(float pitch in new[]{10f,-25f})
            {
                camera.transform.rotation=Quaternion.Euler(pitch,0,0);
                camera.transform.position=player.transform.position+new Vector3(5,3,0)-camera.transform.forward*12;
                views.Add(Estimate(all,camera,bias,pitch));
            }
            map.views=views.ToArray();maps.Add(map);
        }
        report.maps=maps.ToArray();
        var cloth=new List<Cloth>();
        if(Environment.GetEnvironmentVariable("ODM_PERFORMANCE_SURVEY_INVENTORY_ONLY")!="1")foreach(string mode in new[]{"idle","forward40","turning40"})cloth.Add(Benchmark(label,mode));
        report.cloth=cloth.ToArray();
        File.WriteAllText(Folder+"/"+label+".json",JsonUtility.ToJson(report,true));
        Debug.Log("PERFORMANCE_SURVEY_COMPLETE "+JsonUtility.ToJson(report));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
    static View Estimate(Component[] all,Camera camera,float bias,float pitch)
    {
        var selected=new HashSet<Renderer>();var grouped=new HashSet<Renderer>();var v=new View{pitch=pitch,lodBias=bias,cameraAspect=camera.aspect};
        foreach(var g in all.OfType<LODGroup>().Where(g=>g.enabled && g.gameObject.activeInHierarchy))
        {
            var lods=g.GetLODs();foreach(var l in lods)foreach(var r in l.renderers)if(r!=null)grouped.Add(r);
            Vector3 s=g.transform.lossyScale;float size=g.size*Mathf.Max(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
            float distance=Vector3.Distance(camera.transform.position,g.transform.TransformPoint(g.localReferencePoint));
            float h=size*bias/(2*Mathf.Max(.001f,distance)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
            for(int i=0;i<lods.Length;i++)if(h>=lods[i].screenRelativeTransitionHeight)
            {foreach(var r in lods[i].renderers)if(r!=null)selected.Add(r);break;}
        }
        foreach(var r in all.OfType<Renderer>())if(!grouped.Contains(r))selected.Add(r);
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);
        foreach(var r in selected)
        {
            if(!r.enabled || !r.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes,r.bounds))continue;
            Mesh mesh=null;var skin=r as SkinnedMeshRenderer;
            if(skin!=null)mesh=skin.sharedMesh;else{var f=r.GetComponent<MeshFilter>();if(f!=null)mesh=f.sharedMesh;}
            if(mesh==null)continue;v.candidateRenderers++;
            long triangles=0;for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)triangles+=mesh.GetIndexCount(i)/3;
            v.estimatedMeshTriangles+=triangles;
            if(r.name.Contains("GF2_Tree"))v.treeTriangles+=triangles;
            else if(r.name.Contains("GF2_Wall") || r.name.Contains("SG_Wall") || r.name.Contains("SG_Gate"))v.wallTriangles+=triangles;
            else v.otherTriangles+=triangles;
            v.estimatedMaterialSubmissions+=r.sharedMaterials.Length;
            if(r.name.Contains("LOD0"))v.selectedLod0++;else if(r.name.Contains("LOD1"))v.selectedLod1++;else if(r.name.Contains("LOD2"))v.selectedLod2++;
        }
        return v;
    }
    static Cloth Benchmark(string label,string mode)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SharedGameplay/Player.prefab");
        var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var driver=root.GetComponentInChildren<ScoutPlayerAnimation>(true);
        var cape=driver.GetComponent<ScoutCapeCloth>();if(cape==null)cape=driver.gameObject.AddComponent<ScoutCapeCloth>();
        var run=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Player/Scout/Animations/Run.anim");
        cape.automaticSimulation=false;
        if(!cape.Initialize(driver.model))throw new InvalidOperationException(cape.Error);
        const int warmup=60,samples=360;float dt=1f/int.Parse(Environment.GetEnvironmentVariable("ODM_PERFORMANCE_SURVEY_FPS"));
        var values=new double[samples];
        string trace=Folder+"/"+label+"-"+mode+".bin";
        using(var writer=new BinaryWriter(File.Create(trace)))
        for(int frame=0;frame<warmup+samples;frame++)
        {
            if(mode!="idle")
            {
                root.transform.position+=root.transform.forward*(40*dt);
                if(mode=="turning40")root.transform.rotation=Quaternion.Euler(0,frame*.5f,0);
                run.SampleAnimation(driver.model.gameObject,(frame*dt)%run.length);
            }
            long start=Stopwatch.GetTimestamp();cape.Simulate(dt);long end=Stopwatch.GetTimestamp();
            if(frame>=warmup)
            {
                values[frame-warmup]=(end-start)*1000.0/Stopwatch.Frequency;
                for(int i=0;i<ScoutCapeCloth.ParticleCount;i++){var p=cape.GetParticle(i);writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
            }
        }
        Array.Sort(values);var result=new Cloth{mode=mode,warmup=warmup,samples=samples,simulatedDeltaTime=dt,meanMs=values.Average(),medianMs=values[samples/2],p95Ms=values[(int)(samples*.95f)],maximumStretch=cape.MaximumStructuralStretch()};
        using(var hash=SHA256.Create())using(var input=File.OpenRead(trace))result.trajectorySha256=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();
        UnityEngine.Object.DestroyImmediate(root);return result;
    }
}
'''
helper_path = w/'Assets/Editor/OwnedPerformanceSurvey.cs'
helper_path.write_text(helper)
log = w/f'performance-survey-{a.label}.log'
cmd = ['C:/Program Files/Unity/Hub/Editor/2021.3.25f1/Editor/Unity.exe','-batchmode','-nographics','-quit','-projectPath',str(w),'-executeMethod','OwnedPerformanceSurvey.Run','-logFile',str(log)]
result = subprocess.run(cmd,env=dict(os.environ,ODM_PERFORMANCE_SURVEY_OWNED='1',ODM_PERFORMANCE_SURVEY_LABEL=a.label,ODM_PERFORMANCE_SURVEY_FPS=str(a.fps),ODM_PERFORMANCE_SURVEY_INVENTORY_ONLY='1' if a.inventory_only else '0'),stdin=subprocess.DEVNULL,timeout=900)
text = log.read_text(errors='replace')
for line in text.splitlines():
    if any(x in line for x in ['PERFORMANCE_SURVEY_', 'error CS', 'Exception:']):print(line)
if result.returncode or 'PERFORMANCE_SURVEY_COMPLETE' not in text:
    raise SystemExit('Survey failed: '+str(log))
changes=[rel for rel,digest in before.items() if not (R/rel).exists() or sha(R/rel)!=digest]
if changes:raise SystemExit('Concurrent source changes (survey never publishes): '+str(changes))
report=json.loads((w/f'Library/PerformanceSurvey/{a.label}.json').read_text())
out=R/'Library/PerformanceSurvey';out.mkdir(exist_ok=True)
(out/f'{a.label}.json').write_text(json.dumps(report,indent=2))
shutil.copy2(log,out/log.name)
(out/f'{a.label}-preservation.json').write_text(json.dumps({'protectedPaths':len(before),'unchanged':len(before),'workerOnlyCapeRevision':revision,'keptWorkerAuthoringAssets':a.keep_worker_assets,'helperSha256':sha(helper_path),'sourceSha256':before,'limitations':'Isolated Editor-Mono cloth CPU microbenchmark and frustum/LOD estimates; no gameplay FPS, GPU time or actual render counter measurement.'},indent=2))
print('Source Assets/ProjectSettings unchanged:',len(before),'paths; result:',out/f'{a.label}.json')
