using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Explicit GiantForest2-only authoring. No InitializeOnLoad, Play control,
/// runtime baking, old-map asset mutation, global Physics or pipeline changes.</summary>
public static class GiantForest2Builder
{
    public const string ScenePath="Assets/Scenes/GiantForest2.unity";
    public const string Art="Assets/Art/Environment/GiantForest2";
    public const string Map="Assets/Environment/GiantForest2";
    const string NavFolder="Assets/Navigation/Titans/GiantForest2";
    const string RootName="Giant Forest 2.0 [generated environment]";
    const float WorldSize=2048,WallEdge=768,WallHeightMultiplier=2;
    static readonly string[] TreeNames={"GF2_Tree_A_Sentinel","GF2_Tree_B_Leaning","GF2_Tree_C_OldFork"};
    static readonly Vector2[] ActorSites={new Vector2(-105,-285),new Vector2(125,-165),new Vector2(-225,30),new Vector2(250,155),new Vector2(-285,350),new Vector2(190,435)};
    static readonly Vector2 Start=new Vector2(0,-550);
    static System.Random random;
    static Transform environment;
    static Terrain terrain;
    static List<Vector3> trees;
    static List<float> heights;
    [Serializable] public class Report
    {
        public string scene,unity,builtAt;public int treeCount,wallBays,rocks,logs,navSources,placedTitans;
        public float treeMinimum,treeMaximum,terrainSize,highestTitan=72;
        public float wallHeightMultiplier,rockMinimum,rockMaximum;public int smallRocks,mediumRocks,largeRocks;
        public float[] navigationScales;public Vector3 playerSpawn;public int completePaths;public string[] removedRoots;
    }
    static Report report;
    static void RequireOwned()
    {
        if(!Application.isBatchMode || !File.Exists(".giant-forest-owned"))throw new InvalidOperationException("Owned GiantForest2 batch worker required");
        if(Application.unityVersion!="2021.3.25f1")throw new InvalidOperationException("Use Unity2021.3.25f1");
    }
    public static void InspectBatch()
    {
        RequireOwned();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Directory.CreateDirectory("Library/GiantForest2");File.WriteAllLines("Library/GiantForest2/roots.txt",scene.GetRootGameObjects().Select(r=>r.name+" @"+r.transform.position+" ["+string.Join(",",r.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name))+ "] renderers="+r.GetComponentsInChildren<Renderer>(true).Length+" colliders="+r.GetComponentsInChildren<Collider>(true).Length+" Titans="+string.Join(",",r.GetComponentsInChildren<ModularTitanBody>(true).Select(b=>b.name+":"+b.transform.localScale))));
        Debug.Log("GF2_INSPECT_COMPLETE");
    }
    public static void BuildBatch(){RequireOwned();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Build();}
    [MenuItem("Tools/Environment/Giant Forest 2/Build or rebuild this map")]
    public static void Build()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=ScenePath||scene.isDirty)throw new InvalidOperationException("Open the SAVED GiantForest2 scene in Edit Mode; no other scene is accepted");
        var roots=scene.GetRootGameObjects();var setup=roots.SelectMany(r=>r.GetComponentsInChildren<TitanFreeRoamIntegration>(true)).Single();
        var player=setup.player;if(player==null||player.controller==null)throw new InvalidOperationException("Copied real ODM player is required");
        var actors=roots.SelectMany(r=>r.GetComponentsInChildren<ModularTitanBody>(true)).OrderBy(b=>b.name,StringComparer.Ordinal).ToArray();
        if(actors.Length>ActorSites.Length)throw new InvalidOperationException("More placed actors than reserved sites; preserve them and extend the layout explicitly");
        Directory.CreateDirectory("Library/GiantForest2");EditorSceneManager.SaveScene(scene,"Library/GiantForest2/before-build.unity",true);
        EnsureFolders(Art+"/Materials");EnsureFolders(Art+"/Prefabs");EnsureFolders(Map);EnsureFolders(NavFolder);
        random=new System.Random(2722026);trees=new List<Vector3>();heights=new List<float>();
        report=new Report{scene=ScenePath,unity=Application.unityVersion,builtAt=DateTime.UtcNow.ToString("O"),terrainSize=WorldSize,placedTitans=actors.Length};
        var removed=new List<string>();
        foreach(var old in roots)
            if(old.name==RootName||old.name=="Map Control"||old.name=="Cube")
            {
                if(old.GetComponentInChildren<Movement>(true)!=null||old.GetComponentInChildren<ModularTitanBody>(true)!=null)throw new InvalidOperationException("Refusing to remove actors with environment");
                removed.Add(old.name);Object.DestroyImmediate(old);
            }
        report.removedRoots=removed.ToArray();environment=new GameObject(RootName).transform;
        MakeMaterials();MakePrefabs();MakeTerrain();MakeForest();MakeWalls();MakeProps();ApplyWallAndRockScale();
        Vector3 oldPosition=player.transform.position;Vector3 spawn=Point(Start)+Vector3.up*(player.controller.height*.5f+player.controller.skinWidth+.12f);
        player.transform.SetPositionAndRotation(spawn,Quaternion.identity);Vector3 delta=spawn-oldPosition;
        foreach(var root in scene.GetRootGameObjects())if(root.GetComponent<Camera>()!=null||root.GetComponent<Cinemachine.CinemachineFreeLook>()!=null||root.name=="PLayerFollower")root.transform.position+=delta;
        report.playerSpawn=spawn;
        for(int i=0;i<actors.Length;i++)actors[i].transform.SetPositionAndRotation(Point(ActorSites[i])+Vector3.up*.1f,Quaternion.Euler(0,180,0));
        setup.titans=actors;setup.navigation.geometryRoots=new[]{environment};setup.navigation.profiles=new TitanWorldNavigation.Profile[0];
        // Map-local reservations; do not change combat/sight/health/scale tuning.
        setup.additionalBakeScales=new[]{3f,5f,6f};setup.gameObject.name="Giant Forest 2 Titan navigation";
        foreach(var spawner in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitanRandomSpawner>(true)))
        {spawner.useCustomBounds=true;spawner.spawnBounds=new Bounds(new Vector3(0,100,0),new Vector3(1400,300,1400));}
        Lighting(scene);Physics.SyncTransforms();Bake(setup);
        // Existing ready size data, exact actual-rig clearance, no far relocation.
        if(!setup.navigation.Register())throw new InvalidOperationException(setup.navigation.Error);
        try
        {
            foreach(var actor in actors)
            {
                Vector3 projected;string error;TitanWorldNavigation.Profile profile;
                if(!TitanSpawnPlacement.TryFind(setup.navigation,scene,actor.transform.position,actor.transform.rotation,actor.transform.lossyScale,3,actor.transform,out projected,out profile,out error)||!TitanSpawnPlacement.BodyIsClear(actor,projected,out error))throw new InvalidOperationException(actor.name+": "+error);
                actor.transform.position=projected;
            }
            ValidatePaths(setup.navigation);
        }
        finally{setup.navigation.Release();}
        report.treeCount=trees.Count;report.treeMinimum=heights.Min();report.treeMaximum=heights.Max();
        File.WriteAllText(Map+"/map-manifest.json",JsonUtility.ToJson(report,true));File.WriteAllText("Library/GiantForest2/build-report.json",JsonUtility.ToJson(report,true));
        // Append, never reorder existing menu/game scene build indices.
        if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("GF2_BUILD_COMPLETE "+JsonUtility.ToJson(report));
    }
    static void EnsureFolders(string path){Directory.CreateDirectory(path);AssetDatabase.Refresh();}
    static T Store<T>(T value,string path)where T:Object
    {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}EditorUtility.CopySerialized(value,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(value);return old;}
    static Material Mat(string name){return AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/"+name+".mat");}
    static Texture2D Tex(string name){return AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+name+".png");}
    static void MakeMaterials()
    {
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"/GiantForestLit.shader");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Forest URP shader missing or has compiler errors");
        foreach(var entry in new[]{new[]{"Bark","GF2_Bark"},new[]{"Stone","GF2_Stone"},new[]{"Floor","GF2_Floor"},new[]{"Heartwood","GF2_EndGrain"},new[]{"Leaves","GF2_Foliage"},new[]{"Recess",""}})
        {
            var m=new Material(shader){name="GF2_"+entry[0],enableInstancing=true};bool leaves=entry[0]=="Leaves";
            if(entry[1]!="")m.SetTexture("_BaseMap",Tex(entry[1]+(leaves?"_BaseColorAlpha":"_BaseColor")));
            if(!leaves&&entry[1]!=""){m.SetTexture("_BumpMap",Tex(entry[1]+"_Normal"));m.SetTexture("_RoughnessMap",Tex(entry[1]+"_Roughness"));}
            if(entry[0]=="Recess")m.SetColor("_BaseColor",new Color(.25f,.27f,.24f));
            m.SetFloat("_Cull",leaves?0:2);m.SetFloat("_AlphaClip",leaves?1:0);m.SetFloat("_Cutoff",.35f);
            if(leaves){m.renderQueue=2450;m.SetOverrideTag("RenderType","TransparentCutout");}
            Store(m,Art+"/Materials/"+entry[0]+".mat");
        }
    }
    static GameObject Visual(string model,Transform parent)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/"+model+".fbx");if(asset==null)throw new InvalidOperationException("Missing model "+model);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.transform.SetParent(parent,false);
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            r.sharedMaterials=r.sharedMaterials.Select(m=>Mat(m.name.Contains("Leaves")?"Leaves":m.name.Contains("Bark")?"Bark":m.name.Contains("Heartwood")?"Heartwood":m.name.Contains("Recess")?"Recess":m.name.Contains("ForestFloor")?"Floor":"Stone")).ToArray();
            r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        return go;
    }
    static void Box(Transform parent,string name,Vector3 center,Vector3 size)
    {var go=new GameObject(name);go.layer=9;go.transform.SetParent(parent,false);var c=go.AddComponent<BoxCollider>();c.center=center;c.size=size;}
    static Bounds RenderBounds(GameObject go)
    {var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;}
    static void SavePrefab(GameObject root,string name){PrefabUtility.SaveAsPrefabAsset(root,Art+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);}
    static void MeshCollision(GameObject model)
    {
        foreach(var f in model.GetComponentsInChildren<MeshFilter>())
        {f.gameObject.layer=9;var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;var r=f.GetComponent<Renderer>();if(r!=null)Object.DestroyImmediate(r);Object.DestroyImmediate(f);}
    }
    static void MakePrefabs()
    {
        foreach(string tree in TreeNames)
        {
            var root=new GameObject(tree);var group=root.AddComponent<LODGroup>();var lods=new LOD[3];
            for(int i=0;i<3;i++){var visual=Visual(tree+"_LOD"+i,root.transform);lods[i]=new LOD(new[]{.55f,.16f,.025f}[i],visual.GetComponentsInChildren<Renderer>());}
            group.SetLODs(lods);group.RecalculateBounds();group.fadeMode=LODFadeMode.None;
            var collision=Visual(tree+"_Collision",root.transform);MeshCollision(collision);
            if(RenderBounds(root).size.y<90)throw new InvalidOperationException("Tree import axes/units are wrong: "+tree);
            SavePrefab(root,tree);
        }
        foreach(string name in new[]{"GF2_Wall_Straight_24x60","GF2_Wall_Corner_24x60","GF2_Wall_Gateway_48x60","GF2_FallenTrunk_32","GF2_Boulder_A","GF2_Boulder_B","GF2_Boulder_C","GF2_Ground_Loam_32","GF2_Ground_MossBank_32","GF2_Ground_WornTrail_32"})
        {
            var root=new GameObject(name);var visual=Visual(name,root.transform);
            if(name.Contains("Straight"))
            {
                Box(root.transform,"Stone core",new Vector3(0,29.6f,0),new Vector3(23.92f,59.2f,7.6f));
                Box(root.transform,"Foundation",new Vector3(0,.45f,0),new Vector3(24.18f,.9f,9.4f));
                Box(root.transform,"Walkway",new Vector3(0,59,0),new Vector3(24.18f,1,10));
                foreach(int side in new[]{-1,1})
                {
                    Box(root.transform,"Parapet",new Vector3(0,60,side*4.5f),new Vector3(24.18f,1.1f,.8f));
                    foreach(int end in new[]{-1,1})Box(root.transform,"Pilaster",new Vector3(end*11.35f,28,side*4.45f),new Vector3(2,56,1.8f));
                }
            }
            else foreach(var f in visual.GetComponentsInChildren<MeshFilter>()){f.gameObject.layer=9;f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;}
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=9;
            SavePrefab(root,name);
        }
    }
    static float Smooth(float a,float b,float value){float t=Mathf.Clamp01((value-a)/(b-a));return t*t*(3-2*t);}
    static float Surface(float x,float z)
    {
        float h=10+5*Mathf.Sin(x*.005f)*Mathf.Cos(z*.004f)+2.7f*Mathf.Sin(z*.012f+x*.006f)+1.4f*Mathf.Cos(x*.018f-z*.009f);
        h+=9*Mathf.Exp(-((x+290)*(x+290)+(z-270)*(z-270))/85000);
        float edge=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));h=Mathf.Lerp(h,12,Smooth(640,735,edge));
        float d=Vector2.Distance(new Vector2(x,z),Start);return Mathf.Lerp(10,h,Smooth(48,110,d));
    }
    static float PathX(float z){float t=(z+WallEdge)/(WallEdge*2);return 105*Mathf.Sin(t*Mathf.PI*2)*Mathf.Sin(t*Mathf.PI);}
    static Vector3 Point(Vector2 p){return new Vector3(p.x,Surface(p.x,p.y),p.y);}
    static GameObject Place(string prefab,Vector3 point,float yaw,float scale=1,Transform parent=null)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/"+prefab+".prefab");if(asset==null)throw new InvalidOperationException(prefab);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.transform.SetParent(parent!=null?parent:environment,false);go.transform.SetPositionAndRotation(point,Quaternion.Euler(0,yaw,0));go.transform.localScale=Vector3.one*scale;return go;
    }
    static void MakeTerrain()
    {
        var data=new TerrainData{name="GiantForest2_Heightfield",heightmapResolution=1025,size=new Vector3(WorldSize,96,WorldSize),alphamapResolution=512,baseMapResolution=1024};
        var h=new float[1025,1025];for(int z=0;z<1025;z++)for(int x=0;x<1025;x++)h[z,x]=(Surface(-1024+x*2,-1024+z*2)+24)/96;
        data.SetHeights(0,0,h);var layers=new TerrainLayer[3];
        for(int i=0;i<3;i++)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);texture.LoadImage(File.ReadAllBytes(Art+"/Textures/GF2_Floor_BaseColor.png"));
            var pixels=texture.GetPixels();for(int p=0;p<pixels.Length;p++)pixels[p]*=i==1?new Color(.72f,.96f,.60f,1):i==2?new Color(1.22f,1.13f,.95f,1):Color.white;
            texture.SetPixels(pixels);texture.Apply();string image=Art+"/Textures/GF2_Terrain_"+i+".png";File.WriteAllBytes(image,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(image);
            layers[i]=Store(new TerrainLayer{name="GF2_Terrain_"+i,diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(image),normalMapTexture=Tex("GF2_Floor_Normal"),tileSize=new Vector2(14,14),normalScale=.5f,smoothness=.08f},Map+"/Ground_"+i+".terrainlayer");
        }
        data.terrainLayers=layers;data=Store(data,Map+"/GiantForest2_Terrain.asset");
        var go=Terrain.CreateTerrainGameObject(data);go.name="Forest floor - actual heightfield";go.layer=9;go.transform.SetParent(environment,false);go.transform.position=new Vector3(-1024,-24,-1024);
        terrain=go.GetComponent<Terrain>();terrain.drawInstanced=true;terrain.heightmapPixelError=7;terrain.basemapDistance=450;terrain.treeDistance=0;terrain.detailObjectDistance=0;
        terrain.materialTemplate=Store(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")){name="GiantForest2_TerrainLit"},Art+"/Materials/Terrain.mat");
    }
    static bool Reserved(Vector2 p,float margin)
    {
        if(Mathf.Abs(p.x-PathX(p.y))<margin)return true;
        if(Vector2.Distance(p,Start)<95)return true;
        foreach(var actor in ActorSites)if(Vector2.Distance(p,actor)<53)return true;
        return false;
    }
    static void MakeForest()
    {
        var parent=new GameObject("Giant trees - 150 to 216m").transform;parent.SetParent(environment,false);
        for(int z=0;z<16;z++)for(int x=0;x<16;x++)
        {
            Vector2 p=new Vector2(-690+x*92+Range(-21,21),-690+z*92+Range(-21,21));
            if(Mathf.Abs(p.x)>715||Mathf.Abs(p.y)>715||Reserved(p,42)||trees.Any(t=>Vector2.Distance(new Vector2(t.x,t.z),p)<68))continue;
            string name=TreeNames[random.Next(TreeNames.Length)];var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/"+name+".prefab");float baseHeight=RenderBounds(asset).size.y;
            float height=Range(150,216);var go=Place(name,Point(p),Range(0,360),height/baseHeight,parent);go.name=name+" / "+height.ToString("F0")+"m";trees.Add(go.transform.position);heights.Add(height);
        }
        // Continuous heightfield textures with glades, winding soil route and moss
        // under root/canopy zones. These are material weights, NOT fake colliders.
        int n=terrain.terrainData.alphamapResolution;var weights=new float[n,n,3];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float wx=-1024+x*2048f/(n-1),wz=-1024+z*2048f/(n-1);float distance=Mathf.Abs(wx-PathX(wz));
            float trail=1-Smooth(9,30,distance);trail=Mathf.Max(trail,1-Smooth(42,82,Vector2.Distance(new Vector2(wx,wz),Start)));
            float nearest=10000;foreach(var tree in trees)nearest=Mathf.Min(nearest,(new Vector2(wx-tree.x,wz-tree.z)).sqrMagnitude);
            float moss=(1-Smooth(100,1100,nearest))*.72f+(Mathf.PerlinNoise(wx*.012f+51,wz*.012f+82)-.5f)*.22f;moss=Mathf.Clamp01(moss)*(1-trail);
            weights[z,x,0]=(1-trail)*(1-moss);weights[z,x,1]=(1-trail)*moss;weights[z,x,2]=trail;
        }
        terrain.terrainData.SetAlphamaps(0,0,weights);EditorUtility.SetDirty(terrain.terrainData);
    }
    static float Range(float min,float max){return Mathf.Lerp(min,max,(float)random.NextDouble());}
    static void MakeWalls()
    {
        var parent=new GameObject("Perimeter walls - 1536m enclosure").transform;parent.SetParent(environment,false);
        // Bays overlap their visible piers at the24m centerline repeat. No physics
        // invisible boundary, surrogate flat floor or renderer-bounds navigation.
        for(int side=0;side<4;side++)for(int i=0;i<64;i++)
        {
            float along=-756+i*24;if(side==0&&Mathf.Abs(along)<25)continue;
            Vector3 p=side<2?new Vector3(along,12,side==0?-768:768):new Vector3(side==2?-768:768,12,along);
            Place("GF2_Wall_Straight_24x60",p,side<2?0:90,1,parent);report.wallBays++;
        }
        Place("GF2_Wall_Gateway_48x60",new Vector3(0,12,-768),0,1,parent);
        // Corner model is furnished as a prefab. At the enclosure intersections
        // plain filled piers avoid doubled L-roofs/coplanar collision geometry.
        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
        {
            var pier=GameObject.CreatePrimitive(PrimitiveType.Cube);pier.name="Corner anchor pier";pier.layer=9;pier.transform.SetParent(parent,false);pier.transform.position=new Vector3(x*768,42,z*768);pier.transform.localScale=new Vector3(11,60,11);pier.GetComponent<Renderer>().sharedMaterial=Mat("Stone");
        }
    }
    static void MakeProps()
    {
        var parent=new GameObject("Fallen wood and rock clusters").transform;parent.SetParent(environment,false);int attempts=0;
        while(report.rocks<40&&attempts++<600)
        {
            Vector2 p=new Vector2(Range(-690,690),Range(-690,690));if(Reserved(p,65)||trees.Any(t=>Vector2.Distance(new Vector2(t.x,t.z),p)<27))continue;
            string name="GF2_Boulder_"+(char)('A'+random.Next(3));Place(name,Point(p)-Vector3.up*.35f,Range(0,360),Range(.75f,1.5f),parent);report.rocks++;
        }
        attempts=0;while(report.logs<9&&attempts++<500)
        {
            Vector2 p=new Vector2(Range(-650,650),Range(-650,650));if(Reserved(p,82)||trees.Any(t=>Vector2.Distance(new Vector2(t.x,t.z),p)<35))continue;
            Place("GF2_FallenTrunk_32",Point(p)-Vector3.up*.2f,Range(0,360),Range(1,1.7f),parent);report.logs++;
        }
    }
    // Scoped iteration: preserve trees, terrain, logs, actor poses, cameras and all
    // user tuning. Only wall/rock transforms + their own pre-baked data change.
    public static void ResizeBatch()
    {
        RequireOwned();var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        environment=scene.GetRootGameObjects().Single(r=>r.name==RootName).transform;
        terrain=environment.GetComponentInChildren<Terrain>();
        var setup=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitanFreeRoamIntegration>(true)).Single();
        report=JsonUtility.FromJson<Report>(File.ReadAllText(Map+"/map-manifest.json"));report.builtAt=DateTime.UtcNow.ToString("O");report.completePaths=0;
        Directory.CreateDirectory("Library/GiantForest2");EditorSceneManager.SaveScene(scene,"Library/GiantForest2/before-resize.unity",true);
        ApplyWallAndRockScale();Physics.SyncTransforms();Bake(setup);
        if(!setup.navigation.Register())throw new InvalidOperationException(setup.navigation.Error);
        try
        {
            foreach(var actor in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ModularTitanBody>(true)))
            {
                string error;if(!TitanSpawnPlacement.BodyIsClear(actor,actor.transform.position,out error))throw new InvalidOperationException(actor.name+": "+error);
            }
            ValidatePaths(setup.navigation);
        }
        finally{setup.navigation.Release();}
        File.WriteAllText(Map+"/map-manifest.json",JsonUtility.ToJson(report,true));File.WriteAllText("Library/GiantForest2/build-report.json",JsonUtility.ToJson(report,true));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("GF2_RESIZE_COMPLETE "+JsonUtility.ToJson(report));
    }
    static void ApplyWallAndRockScale()
    {
        var walls=environment.Find("Perimeter walls - 1536m enclosure");
        foreach(Transform wall in walls)
        {
            if(wall.name=="Corner anchor pier")
            {
                var p=wall.localPosition;p.y=12+30*WallHeightMultiplier;wall.localPosition=p;
                var s=wall.localScale;s.y=60*WallHeightMultiplier;wall.localScale=s;
            }
            else
            {
                var s=wall.localScale;s.y=WallHeightMultiplier;wall.localScale=s;
            }
        }
        report.wallHeightMultiplier=WallHeightMultiplier;
        var rocks=environment.Find("Fallen wood and rock clusters").Cast<Transform>().Where(t=>t.name.StartsWith("GF2_Boulder_",StringComparison.Ordinal)).ToArray();
        foreach(var rock in rocks)rock.gameObject.SetActive(false);
        Physics.SyncTransforms();var rng=new System.Random(581291);var hits=new Collider[64];
        report.rockMinimum=float.MaxValue;report.rockMaximum=0;report.smallRocks=report.mediumRocks=report.largeRocks=0;
        for(int i=0;i<rocks.Length;i++)
        {
            var rock=rocks[i];int tier=i%5==0?0:i%5<3?1:2;
            float target=Mathf.Lerp(tier==0?4:tier==1?10:22,tier==0?8:tier==1?17:32,(float)rng.NextDouble());
            var bounds=MeshBounds(rock);rock.localScale*=target/bounds.size.y;
            var original=rock.position;bool placed=false;rock.gameObject.SetActive(true);
            for(int attempt=0;attempt<300&&!placed;attempt++)
            {
                var point=attempt==0?new Vector2(original.x,original.z):new Vector2(Mathf.Lerp(-690,690,(float)rng.NextDouble()),Mathf.Lerp(-690,690,(float)rng.NextDouble()));
                if(Reserved(point,65))continue;
                rock.position=new Vector3(point.x,0,point.y);bounds=MeshBounds(rock);
                float ground=terrain.SampleHeight(new Vector3(point.x,0,point.y))+terrain.transform.position.y;
                rock.position+=Vector3.up*(ground-bounds.min.y-target*.12f);bounds=MeshBounds(rock);
                if(Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x),Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z))>740)continue;
                Physics.SyncTransforms();int n=rock.gameObject.scene.GetPhysicsScene().OverlapBox(bounds.center,bounds.extents+Vector3.one,hits,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                if(n==hits.Length)continue;placed=true;
                for(int h=0;h<n;h++)if(!hits[h].transform.IsChildOf(rock)&&hits[h]!=terrain.GetComponent<TerrainCollider>()){placed=false;break;}
            }
            if(!placed)throw new InvalidOperationException("No scenery-clear position for "+rock.name+" at "+target+"m; do not publish");
            rock.name=rock.name.Split(new[]{" / "},StringSplitOptions.None)[0]+" / "+target.ToString("F1")+"m";
            if(tier==0)report.smallRocks++;else if(tier==1)report.mediumRocks++;else report.largeRocks++;
            report.rockMinimum=Mathf.Min(report.rockMinimum,target);report.rockMaximum=Mathf.Max(report.rockMaximum,target);
        }
    }
    static Bounds MeshBounds(Transform root)
    {
        var b=new Bounds();bool first=true;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var local=filter.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var p=filter.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);
            }
        }
        return b;
    }
    static void Lighting(Scene scene)
    {
        var sun=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.type==LightType.Directional);
        if(sun!=null){sun.transform.rotation=Quaternion.Euler(48,-28,0);sun.intensity=1.15f;sun.color=new Color(1,.94f,.83f);sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;}
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.43f,.51f,.56f);RenderSettings.ambientEquatorColor=new Color(.30f,.34f,.29f);RenderSettings.ambientGroundColor=new Color(.16f,.18f,.13f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0015f;RenderSettings.fogColor=new Color(.49f,.57f,.57f);
        var sky=new Material(Shader.Find("Skybox/Procedural")){name="GF2_Sky"};sky.SetFloat("_AtmosphereThickness",.8f);sky.SetColor("_SkyTint",new Color(.46f,.53f,.57f));sky.SetColor("_GroundColor",new Color(.25f,.27f,.21f));RenderSettings.skybox=Store(sky,Art+"/Materials/Sky.mat");
        // Existing shared URP renderer/shadow distance/quality asset left untouched.
    }
    static void Bake(TitanFreeRoamIntegration setup)
    {
        var sources=new List<NavMeshBuildSource>();Bounds bounds;string error;
        if(!TitanNavigationGeometry.Collect(setup.gameObject.scene,new[]{environment},sources,out bounds,out error))throw new InvalidOperationException(error);
        bounds.Expand(new Vector3(100,160,100));var profiles=new List<TitanWorldNavigation.Profile>();
        foreach(float scale in new[]{3f,5f,6f})
        {
            string typeName=scale==6?"ODM Titan GiantForest2 6x":"ODM Titan FreeRoam "+scale+"x";int id=-1;
            for(int i=0;i<NavMesh.GetSettingsCount();i++){var s=NavMesh.GetSettingsByIndex(i);if(NavMesh.GetSettingsNameFromID(s.agentTypeID)==typeName){id=s.agentTypeID;break;}}
            if(id==-1)
            {
                id=NavMesh.CreateSettings().agentTypeID;var obj=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0];var so=new SerializedObject(obj);var entries=so.FindProperty("m_Settings");var names=so.FindProperty("m_SettingNames");
                for(int i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("agentTypeID").intValue==id)
                {var e=entries.GetArrayElementAtIndex(i);names.GetArrayElementAtIndex(i).stringValue=typeName;e.FindPropertyRelative("agentRadius").floatValue=1.8f*scale;e.FindPropertyRelative("agentHeight").floatValue=12*scale;e.FindPropertyRelative("agentClimb").floatValue=.6f*scale;e.FindPropertyRelative("agentSlope").floatValue=40;}
                so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);AssetDatabase.SaveAssets();
            }
            var settings=NavMesh.GetSettingsByID(id);if(Mathf.Abs(settings.agentRadius-1.8f*scale)>.01||Mathf.Abs(settings.agentHeight-12*scale)>.01)throw new InvalidOperationException("Refusing to mutate mismatched existing agent settings");
            settings.overrideVoxelSize=true;settings.voxelSize=settings.agentRadius/4;settings.overrideTileSize=true;settings.tileSize=128;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);if(data==null)throw new InvalidOperationException("Forest bake failed "+scale);
            data.name="GiantForest2 Titan "+scale+"x";data=Store(data,NavFolder+"/Titan_"+scale+"x.asset");
            profiles.Add(new TitanWorldNavigation.Profile{label=scale+"x",scale=scale,radius=settings.agentRadius,height=settings.agentHeight,agentTypeID=id,data=data});
        }
        setup.navigation.profiles=profiles.ToArray();setup.navigation.bakedBounds=bounds;setup.navigation.sourceCount=sources.Count;setup.navigation.bakedAt=DateTime.UtcNow.ToString("O");report.navSources=sources.Count;report.navigationScales=profiles.Select(p=>p.scale).ToArray();
    }
    static void ValidatePaths(TitanWorldNavigation navigation)
    {
        foreach(var profile in navigation.profiles)
        {
            var filter=new NavMeshQueryFilter{agentTypeID=profile.agentTypeID,areaMask=NavMesh.AllAreas};NavMeshHit start;
            if(!NavMesh.SamplePosition(Point(Start),out start,6,filter))throw new InvalidOperationException("No player-glade navigation for "+profile.label);
            foreach(var point in new[]{new Vector2(0,550),new Vector2(-510,440),new Vector2(510,440),new Vector2(-510,-440),new Vector2(510,-440)})
            {
                NavMeshHit end;if(!NavMesh.SamplePosition(Point(point),out end,45,filter))throw new InvalidOperationException("Missing route endpoint "+profile.label+point);
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(start.position,end.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)throw new InvalidOperationException("Disconnected forest route "+profile.label+point);report.completePaths++;
            }
        }
    }
    public static void CaptureBatch()
    {
        RequireOwned();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Directory.CreateDirectory("Library/GiantForest2");
        var cameraObject=new GameObject("OWNED unsaved review camera");var camera=cameraObject.AddComponent<Camera>();camera.nearClipPlane=.3f;camera.farClipPlane=4000;camera.fieldOfView=65;
        bool fog=RenderSettings.fog;
        try
        {
            Capture(camera,"Overview",new Vector3(750,880,-1120),new Vector3(0,50,0),false);
            Capture(camera,"Entry",new Vector3(0,Surface(0,-600)+12,-600),new Vector3(30,95,-250),true);
            Capture(camera,"ForestInterior",new Vector3(-150,Surface(-150,10)+12,10),new Vector3(50,95,220),true);
            Capture(camera,"Gate",new Vector3(0,45,-620),new Vector3(0,40,-768),true);
            Capture(camera,"Canopy",new Vector3(-260,170,-220),new Vector3(160,160,220),true);
        }
        finally{RenderSettings.fog=fog;Object.DestroyImmediate(cameraObject);}
        Debug.Log("GF2_CAPTURE_COMPLETE");
    }
    static void Capture(Camera camera,string name,Vector3 position,Vector3 target,bool fog)
    {
        RenderSettings.fog=fog;camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));var rt=RenderTexture.GetTemporary(1600,1000,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Library/GiantForest2/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);
    }
}

/// <summary>New owned forest assets ONLY; old models/textures never reconfigured.</summary>
public sealed class GiantForest2AssetImporter:AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if(!assetPath.StartsWith(GiantForest2Builder.Art+"/Models/",StringComparison.Ordinal))return;
        var m=(ModelImporter)assetImporter;m.importAnimation=false;m.importCameras=false;m.importLights=false;m.animationType=ModelImporterAnimationType.None;m.globalScale=1;m.addCollider=false;m.isReadable=assetPath.Contains("_Collision");m.importNormals=ModelImporterNormals.Import;m.importTangents=ModelImporterTangents.CalculateMikk;
    }
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith(GiantForest2Builder.Art+"/Textures/",StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;t.mipmapEnabled=true;t.anisoLevel=4;t.maxTextureSize=2048;t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Repeat;
        if(assetPath.Contains("_Normal")){t.textureType=TextureImporterType.NormalMap;t.sRGBTexture=false;}
        if(assetPath.Contains("_Roughness"))t.sRGBTexture=false;
        if(assetPath.Contains("BaseColorAlpha")){t.alphaIsTransparency=true;t.alphaSource=TextureImporterAlphaSource.FromInput;t.mipMapsPreserveCoverage=true;t.alphaTestReferenceValue=.35f;t.wrapMode=TextureWrapMode.Clamp;}
    }
}
