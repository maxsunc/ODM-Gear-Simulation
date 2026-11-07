using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Explicit NEW-map authoring in a marker-owned batch worker only.
/// No import-time execution, live-scene tools, physics settings or existing art edits.</summary>
public static class ShiganshinaDistrictBuilder
{
    public const string ScenePath="Assets/Scenes/ShiganshinaDistrict.unity";
    public const string Art="Assets/Art/Environment/Shiganshina";
    const string Map="Assets/Environment/Shiganshina", Nav="Assets/Navigation/Titans/Shiganshina";
    static Transform environment,bed,outskirts;
    static System.Random random;
    static readonly string[] Houses={"SG_House_A_Gable","SG_House_B_Tall","SG_House_C_Shop","SG_House_D_Wide","SG_House_E_Cottage","SG_House_F_Guild"};
    [Serializable] public class MapReport
    {
        public string scene,unity,authoredAt,sourceScene,sceneGUID,graphicsDevice;
        public int buildings,wallBays,bridges,landmarks,props,physicalSources,nonWalkableSources;
        public float groundFootprint=2048,enclosureWidth=1400,enclosureDepth=1388,wallHeight=120;
        public Vector3 playerSpawn;
        public float hookRange;
        public int hookMask,environmentLayer=9;
        public string[] removedCloneRoots;
        public float[] navigationScales;
        public int[] agentIDs;
        public int initialSpawns,maximumAlive;
        public string collision="Closed Blender structure meshes; gabled roofs match slopes; layer9 real physical hook surfaces";
        public string layout="Rounded south district, straight inner wall, canal, three bridges, 100m main avenue, 64m cross streets, roof/alley routes";
    }
    static MapReport report;
    static void RequireOwned()
    {
        if(!Application.isBatchMode || Application.isPlaying || Application.unityVersion!="2021.3.25f1" || !File.Exists(".shiganshina-owned"))
            throw new InvalidOperationException("Closed owned Unity2021.3.25f1 authoring worker required");
    }
    static T Save<T>(T value,string path)where T:Object
    {
        if(AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("Refusing implicit asset overwrite: "+path);
        AssetDatabase.CreateAsset(value,path);return value;
    }
    static Transform Group(string name,Transform parent=null)
    {
        var t=new GameObject(name).transform;t.SetParent(parent==null?environment:parent,false);return t;
    }
    static Material Mat(string name){return AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/"+name+".mat");}
    static GameObject Box(string name,Vector3 position,Vector3 size,Material material,Transform parent,bool collision=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=size;g.layer=9;
        g.GetComponent<Renderer>().sharedMaterial=material;
        if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
    public static void BuildOwned()
    {
        RequireOwned();if(File.Exists(ScenePath))throw new InvalidOperationException("New-map-only authoring; explicit revision required");
        foreach(string folder in new[]{Art+"/Materials",Art+"/Prefabs",Map,Nav,"Library/Shiganshina"})Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/GiantForest2.unity",OpenSceneMode.Single);
        // Save AS a NEW scene before changing anything. Source forest is not saved.
        EditorSceneManager.SaveScene(scene,ScenePath);
        report=new MapReport{scene=ScenePath,sourceScene="Assets/Scenes/GiantForest2.unity",unity=Application.unityVersion,authoredAt=DateTime.UtcNow.ToString("O"),sceneGUID=AssetDatabase.AssetPathToGUID(ScenePath)};
        var roots=scene.GetRootGameObjects();var setup=roots.SelectMany(r=>r.GetComponentsInChildren<TitanFreeRoamIntegration>(true)).Single();
        var player=setup.player;if(player==null||player.controller==null)throw new InvalidOperationException("Real copied ODM player required");
        var gear=player.GetComponent<ODM_Movement>();report.hookRange=gear.range;report.hookMask=gear.targetLayer;
        if((gear.targetLayer.value & (1<<9))==0)throw new InvalidOperationException("Existing gear must accept environment layer9");
        var oldProfiles=setup.navigation.profiles;
        var removed=new List<string>();
        foreach(var root in roots)
        {
            if(root==player.transform.root.gameObject || root==setup.transform.root.gameObject)continue;
            bool actor=root.GetComponentInChildren<ModularTitanBody>(true)!=null;
            // External line renderers, hook FX and camera helpers are real gear references,
            // not scenery. Remove ONLY the explicitly identified cloned environment.
            bool scenery=root.name=="Giant Forest 2.0 [generated environment]";
            if(actor||scenery){removed.Add(root.name);Object.DestroyImmediate(root);}
        }
        report.removedCloneRoots=removed.ToArray();environment=new GameObject("Shiganshina District [owned generated environment]").transform;
        random=new System.Random(73481);Materials();Prefabs();Ground();Walls();Streets();Buildings();Landmarks();Lighting(scene);
        var spawn=new Vector3(0,player.controller.height*.5f+player.controller.skinWidth+.12f,-585);
        var delta=spawn-player.transform.position;player.transform.SetPositionAndRotation(spawn,Quaternion.identity);report.playerSpawn=spawn;
        foreach(var root in scene.GetRootGameObjects())
            if(root.GetComponent<Camera>()!=null || root.GetComponent<Cinemachine.CinemachineFreeLook>()!=null || root.name=="PLayerFollower")root.transform.position+=delta;
        foreach(var camera in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))camera.farClipPlane=Mathf.Max(2600,camera.farClipPlane);
        setup.gameObject.name="Shiganshina Titan navigation";setup.titans=new ModularTitanBody[0];setup.additionalBakeScales=new[]{3f,5f,6f};
        setup.navigation.geometryRoots=new[]{environment};setup.navigation.profiles=new TitanWorldNavigation.Profile[0];
        foreach(var spawner in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TitanRandomSpawner>(true)))
        {
            // New-map population uses only actual baked categories. Original forest weights stay untouched.
            spawner.sizes=new[]{new TitanRandomSpawner.SizeChoice{scale=3,weight=1},new TitanRandomSpawner.SizeChoice{scale=5,weight=1},new TitanRandomSpawner.SizeChoice{scale=6,weight=1}};
            spawner.useCustomBounds=true;spawner.spawnBounds=new Bounds(new Vector3(0,50,-50),new Vector3(1300,120,1200));
            report.initialSpawns=spawner.initialSpawnCount;report.maximumAlive=spawner.maximumAlive;
        }
        Physics.SyncTransforms();Bake(setup.navigation,oldProfiles);
        if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        File.WriteAllText(Map+"/map-manifest.json",JsonUtility.ToJson(report,true));File.WriteAllText("Library/Shiganshina/map-report.json",JsonUtility.ToJson(report,true));
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("SG_BUILD_COMPLETE "+JsonUtility.ToJson(report));
    }
    static void Materials()
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP12 Lit is required");
        foreach(string name in new[]{"SG_Plaster","SG_Timber","SG_Roof","SG_Stone","SG_Brick","SG_Window","SG_Iron","SG_Cloth","SG_Paver"})
        {
            var m=new Material(shader){name=name,enableInstancing=true};m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+name+"_BaseColor.png"));
            m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+name+"_Normal.png"));m.SetFloat("_BumpScale",name=="SG_Plaster"?.10f:.40f);m.EnableKeyword("_NORMALMAP");m.SetFloat("_Smoothness",name=="SG_Window"?.25f:.12f);
            if(name=="SG_Cloth")m.SetFloat("_Cull",0);
            Save(m,Art+"/Materials/"+name+".mat");
        }
        for(int i=0;i<4;i++)
        {
            var m=new Material(Mat("SG_Plaster")){name="SG_Plaster_"+i};m.SetColor("_BaseColor",new[]{new Color(.99f,.98f,.93f),new Color(.91f,.96f,1),new Color(1,.90f,.84f),new Color(.86f,.97f,.87f)}[i]);Save(m,Art+"/Materials/SG_Plaster_"+i+".mat");
        }
        var ground=new Material(Mat("SG_Paver")){name="SG_Courtyard"};ground.SetColor("_BaseColor",new Color(.79f,.74f,.60f));ground.SetTextureScale("_BaseMap",new Vector2(210,210));ground.SetTextureScale("_BumpMap",new Vector2(210,210));Save(ground,Art+"/Materials/SG_Courtyard.mat");
        var outer=new Material(Mat("SG_Plaster")){name="SG_Outskirts"};outer.SetColor("_BaseColor",new Color(.42f,.53f,.29f));outer.SetTextureScale("_BaseMap",new Vector2(160,160));Save(outer,Art+"/Materials/SG_Outskirts.mat");
        var water=new Material(shader){name="SG_CanalWater",enableInstancing=true};water.SetColor("_BaseColor",new Color(.10f,.25f,.25f));water.SetFloat("_Smoothness",.82f);Save(water,Art+"/Materials/SG_CanalWater.mat");
    }
    static void Prefabs()
    {
        foreach(var file in Directory.GetFiles(Art+"/Models","*_LOD0.fbx"))
        {
            string name=Path.GetFileNameWithoutExtension(file).Replace("_LOD0","");var root=new GameObject(name);var group=root.AddComponent<LODGroup>();var lods=new LOD[3];
            for(int i=0;i<3;i++)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/"+name+"_LOD"+i+".fbx");if(asset==null)throw new InvalidOperationException("Missing LOD "+name+i);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(asset);visual.transform.SetParent(root.transform,false);
                foreach(var r in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterials=r.sharedMaterials.Select(m=>Mat(m.name)).ToArray();r.receiveShadows=true;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
                }
                lods[i]=new LOD(new[]{.16f,.04f,.002f}[i],visual.GetComponentsInChildren<Renderer>());
            }
            group.SetLODs(lods);group.RecalculateBounds();
            var collision=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/"+name+"_Collision.fbx");var c=(GameObject)PrefabUtility.InstantiatePrefab(collision);c.transform.SetParent(root.transform,false);
            foreach(var f in c.GetComponentsInChildren<MeshFilter>())
            {
                f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;var r=f.GetComponent<Renderer>();if(r!=null)Object.DestroyImmediate(r);Object.DestroyImmediate(f);
            }
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=9;
            var bounds=root.GetComponentInChildren<Renderer>().bounds;
            if(name=="SG_House_A_Gable" && (bounds.size.y<20||bounds.size.y>30))throw new InvalidOperationException("FBX axes/units not metres: "+bounds);
            PrefabUtility.SaveAsPrefabAsset(root,Art+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);
        }
    }
    static GameObject Place(string name,Vector3 position,float yaw,Transform parent,int tint=-1)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/"+name+".prefab");if(asset==null)throw new InvalidOperationException("Missing architecture "+name);
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset);g.transform.SetParent(parent,false);g.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        if(tint>=0)foreach(var r in g.GetComponentsInChildren<MeshRenderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>m.name=="SG_Plaster"?Mat("SG_Plaster_"+tint):m).ToArray();
        return g;
    }
    static void Ground()
    {
        var ground=Group("Visible physical ground - 2048m footprint");
        // Shaped physical district and separate physical outskirts, not an infinite
        // walkable square that would allow random enemies to spawn outside the walls.
        outskirts=Group("Visible physical outskirts - not walkable navigation");
        GroundStrip("Southern district ground",-778,94,0,ground);
        GroundStrip("Northern district ground",126,630,0,ground);
        GroundStrip("Southern outskirts",-1024,-778,3,outskirts);
        GroundStrip("Northern outskirts",630,1024,3,outskirts);
        foreach(int side in new[]{-1,1})
        {
            GroundStrip("Outer bank",-778,94,side,outskirts);
            GroundStrip("Outer bank",126,630,side,outskirts);
        }
        bed=Group("Canal bed - not walkable navigation");Box("Visible stone canal bed",new Vector3(0,-8.5f,110),new Vector3(2048,1,32),Mat("SG_Stone"),bed);
        var canal=Group("Stone quays and visible canal water");
        foreach(int sign in new[]{-1,1})
        {
            Box("Quay retaining wall",new Vector3(0,-4,110+sign*16.8f),new Vector3(2048,8,1.6f),Mat("SG_Stone"),canal);
            // Narrow coping follows the visible wall, not an invisible river bridge.
            Box("Stone quay coping",new Vector3(0,.12f,110+sign*17.3f),new Vector3(2048,.24f,2),Mat("SG_Stone"),canal);
        }
        Box("Water - visual surface only",new Vector3(0,-3.8f,110),new Vector3(2048,.04f,32),Mat("SG_CanalWater"),canal,false);
        // Physical stair runs provide several honest escape paths from the shallow channel.
        for(int x=-540;x<=540;x+=180)for(int i=0;i<16;i++)
            Box("Canal escape stair",new Vector3(x,-8+(i+1)*.25f,108-i*.9f),new Vector3(5,(i+1)*.5f,.96f),Mat("SG_Stone"),canal);
    }
    static void GroundStrip(string name,float start,float end,int part,Transform parent)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        const int strips=96;
        for(int i=0;i<strips;i++)
        {
            float z0=Mathf.Lerp(start,end,i/(float)strips),z1=Mathf.Lerp(start,end,(i+1)/(float)strips);
            float w0=z0< -768?0:WidthAt(Mathf.Min(620,z0))+8,w1=z1< -768?0:WidthAt(Mathf.Min(620,z1))+8;
            float l0=part==0?-w0:part<0?-1024:part==1?w0:-1024;
            float r0=part==0?w0:part<0?-w0:1024;
            float l1=part==0?-w1:part<0?-1024:part==1?w1:-1024;
            float r1=part==0?w1:part<0?-w1:1024;
            var corners=new[]{new Vector3(l0,0,z0),new Vector3(l1,0,z1),new Vector3(r1,0,z1),new Vector3(r0,0,z0),new Vector3(l0,-2,z0),new Vector3(l1,-2,z1),new Vector3(r1,-2,z1),new Vector3(r0,-2,z0)};
            foreach(var face in new[]{new[]{0,1,2,3},new[]{7,6,5,4},new[]{0,4,5,1},new[]{1,5,6,2},new[]{2,6,7,3},new[]{3,7,4,0}})
            {
                int o=vertices.Count;foreach(int v in face)vertices.Add(corners[v]);
                foreach(int v in new[]{0,1,2,0,2,3})triangles.Add(o+v);
            }
        }
        foreach(var v in vertices)uv.Add(new Vector2(v.x/2048,v.z/2048));
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
        Save(mesh,Map+"/Ground_"+parent.childCount+"_"+part+"_"+Mathf.RoundToInt(start)+".asset");
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));g.transform.SetParent(parent,false);g.layer=9;
        g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshCollider>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=Mat(part==0?"SG_Courtyard":"SG_Outskirts");
    }
    static void Walls()
    {
        var walls=Group("Curved outer district and inner great wall");
        const int n=106;Vector3 previous=new Vector3(700,0,620);
        for(int i=1;i<=n;i++)
        {
            float theta=i*Mathf.PI/n;var next=new Vector3(700*Mathf.Cos(theta),0,620-1388*Mathf.Sin(theta));var middle=(previous+next)*.5f;var direction=next-previous;previous=next;
            if(middle.z< -738 && Mathf.Abs(middle.x)<60)continue;
            var g=Place("SG_Wall_Bay_32x120",middle,-Mathf.Atan2(direction.z,direction.x)*Mathf.Rad2Deg,walls);g.transform.localScale=new Vector3((direction.magnitude+.4f)/32,1,1);report.wallBays++;
        }
        for(int x=-1008;x<=1008;x+=32)
        {
            if(Mathf.Abs(x)<65)continue;
            Place("SG_Wall_Bay_32x120",new Vector3(x,0,620),0,walls);report.wallBays++;
        }
        Place("SG_Gate_Fortress",new Vector3(0,0,-768),0,walls);Place("SG_Gate_Fortress",new Vector3(0,0,620),180,walls);report.landmarks+=2;
    }
    static void Road(Transform group,string name,Vector3 p,Vector3 size)
    {
        // Thin visual paving overlay over the actual continuous ground; no substitute nav geometry.
        var mesh=new Mesh{name=name};
        var vertices=new[]{new Vector3(p.x-size.x/2,.003f,p.z-size.z/2),new Vector3(p.x-size.x/2,.003f,p.z+size.z/2),new Vector3(p.x+size.x/2,.003f,p.z+size.z/2),new Vector3(p.x+size.x/2,.003f,p.z-size.z/2)};
        mesh.vertices=vertices;mesh.triangles=new[]{0,1,2,0,2,3};mesh.uv=vertices.Select(v=>new Vector2(v.x/10,v.z/10)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        Save(mesh,Map+"/Paving_"+group.childCount+".asset");var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(group,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=Mat("SG_Paver");
    }
    static void Streets()
    {
        var roads=Group("Main avenue, cross streets and three canal bridges");
        Road(roads,"Southern 100m gate avenue",new Vector3(0,0,-337),new Vector3(100,0,860));
        Road(roads,"Northern 100m gate avenue",new Vector3(0,0,373),new Vector3(100,0,494));
        foreach(float x in new[]{-336f,336f})
        {
            Road(roads,"West/east boulevard",new Vector3(x,0,-195),new Vector3(64,0,570));
            Road(roads,"North boulevard",new Vector3(x,0,381),new Vector3(64,0,510));
        }
        foreach(float z in new[]{-420f,-140f,350f,560f})Road(roads,"Cross-district avenue",new Vector3(0,0,z),new Vector3(WidthAt(z)*2-30,0,64));
        foreach(float x in new[]{-336f,0,336f}){Place("SG_Canal_Bridge",new Vector3(x,0,110),0,roads);report.bridges++;}
        Road(roads,"Northern civic square",new Vector3(0,0,470),new Vector3(250,0,170));
        foreach(float z in new[]{-280f,240f})
        {
            float span=WidthAt(z);foreach(int sign in new[]{-1,1})
                Road(roads,"Secondary roof-route lane",new Vector3(sign*(span*.5f+33),0,z),new Vector3(span-70,0,18));
        }
    }
    static float WidthAt(float z){return 700*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((z-620)/1388,2)));}
    static bool Reserved(float x,float z)
    {
        if(Mathf.Abs(x)<72 || Mathf.Abs(Mathf.Abs(x)-336)<51 || Mathf.Abs(z-110)<59)return true;
        if(new[]{-420f,-140f,350f,560f}.Any(road=>Mathf.Abs(z-road)<49))return true;
        if(new[]{-280f,240f}.Any(road=>Mathf.Abs(z-road)<23))return true;
        if(Mathf.Abs(x)<155&&z>380)return true;
        if(z< -660||z>558)return true;
        return false;
    }
    static void Buildings()
    {
        var town=Group("Residential, merchant and warehouse roofscape");
        for(int row=0;row<34;row++)for(int col=0;col<38;col++)
        {
            float z=-638+row*36,x=-666+col*36;
            if(Mathf.Abs(x)>WidthAt(z)-38||Reserved(x,z))continue;
            int choice=random.Next(Houses.Length);string name=Houses[choice];
            if(Mathf.Abs(z-110)<105)
            {
                if(col%2!=0)continue; // Large waterfront lots have room for38m depth and awnings.
                name="SG_Warehouse";
            }
            //36m residential lots give usable small-player alleys between real closed buildings.
            float yaw=x<0?90:270;
            if(row%4==0)yaw=random.Next(2)*180;
            var g=Place(name,new Vector3(x,0,z),yaw,town,random.Next(4));g.name=name+" / block "+col+"-"+row;report.buildings++;
        }
    }
    static void Landmarks()
    {
        var civic=Group("Civic square and elevated ODM landmarks");
        Place("SG_Belltower",new Vector3(-90,0,458),0,civic);Place("SG_CivicHall",new Vector3(94,0,468),0,civic);Place("SG_Plaza_Fountain",new Vector3(0,0,478),0,civic);
        Place("SG_Windmill",new Vector3(-580,0,510),20,civic);Place("SG_Windmill",new Vector3(580,0,510),-20,civic);report.landmarks+=5;
        foreach(float x in new[]{-180f,180f,-500f,500f})
        {
            Place("SG_Rooftop_Gantry",new Vector3(x,0,65),0,civic);Place("SG_Rooftop_Gantry",new Vector3(x,0,155),0,civic);report.props+=2;
        }
        foreach(int side in new[]{-1,1})for(int i=0;i<4;i++)
        {
            Place("SG_Market_Stall",new Vector3(side*58,0,425+i*18),side>0?270:90,civic);report.props++;
        }
        // Opening sightline: continuous flanking roofs, not a barren spawn pad.
        foreach(int side in new[]{-1,1})
        {
            Place("SG_House_B_Tall",new Vector3(side*70,0,-583),side>0?270:90,civic,side>0?1:0);report.buildings++;
            Place("SG_House_F_Guild",new Vector3(side*76,0,-535),side>0?270:90,civic,side>0?2:3);report.buildings++;
        }
    }
    static void Bake(TitanWorldNavigation nav,TitanWorldNavigation.Profile[] old)
    {
        var sources=new List<NavMeshBuildSource>();Bounds bounds;string error;
        if(!TitanNavigationGeometry.Collect(environment.gameObject.scene,new[]{environment},sources,out bounds,out error))throw new InvalidOperationException(error);
        for(int i=0;i<sources.Count;i++)
        {
            var source=sources[i];
            if(source.component!=null)
            {
                var architecture=source.component.GetComponentInParent<LODGroup>();
                bool closedStructure=architecture!=null && !architecture.name.StartsWith("SG_Canal_Bridge",StringComparison.Ordinal);
                // Actual structure geometry still obstructs agents. Only its TOPS are
                // non-walkable for giants: never randomly spawn enemies on house roofs.
                if(closedStructure || source.component.transform.IsChildOf(bed) || source.component.transform.IsChildOf(outskirts))
                {source.area=1;sources[i]=source;report.nonWalkableSources++;}
            }
        }
        bounds.Expand(new Vector3(60,160,60));var profiles=new List<TitanWorldNavigation.Profile>();
        foreach(float scale in new[]{3f,5f,6f})
        {
            var original=old.Single(p=>Mathf.Abs(p.scale-scale)<.001f);var settings=NavMesh.GetSettingsByID(original.agentTypeID);
            if(settings.agentTypeID==-1 || Mathf.Abs(settings.agentRadius-scale*1.8f)>.01f || Mathf.Abs(settings.agentHeight-scale*12)>.01f)throw new InvalidOperationException("Existing exact-size nav settings mismatch");
            // Local build sampling only; never modify shared agent settings or clearance dimensions.
            settings.overrideVoxelSize=true;settings.voxelSize=settings.agentRadius/6;settings.overrideTileSize=true;settings.tileSize=128;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);if(data==null)throw new InvalidOperationException("Town bake failed "+scale);
            data.name="Shiganshina Titan "+scale+"x";Save(data,Nav+"/Titan_"+scale+"x.asset");
            profiles.Add(new TitanWorldNavigation.Profile{label=scale+"x",scale=scale,agentTypeID=settings.agentTypeID,radius=settings.agentRadius,height=settings.agentHeight,data=data});
        }
        nav.profiles=profiles.ToArray();nav.bakedBounds=bounds;nav.sourceCount=sources.Count;nav.bakedAt=DateTime.UtcNow.ToString("O");
        report.physicalSources=sources.Count;report.navigationScales=profiles.Select(p=>p.scale).ToArray();report.agentIDs=profiles.Select(p=>p.agentTypeID).ToArray();
    }
    static void Lighting(Scene scene)
    {
        var sun=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.type==LightType.Directional);
        if(sun!=null){sun.transform.rotation=Quaternion.Euler(43,-32,0);sun.intensity=1.28f;sun.color=new Color(1,.94f,.83f);sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;}
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.52f,.61f,.70f);RenderSettings.ambientEquatorColor=new Color(.45f,.43f,.36f);RenderSettings.ambientGroundColor=new Color(.25f,.23f,.20f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.00065f;RenderSettings.fogColor=new Color(.65f,.70f,.72f);
        var sky=new Material(Shader.Find("Skybox/Procedural")){name="Shiganshina Sky"};sky.SetFloat("_AtmosphereThickness",.85f);sky.SetColor("_SkyTint",new Color(.52f,.60f,.71f));RenderSettings.skybox=Save(sky,Art+"/Materials/Sky.mat");
    }
    public static void CaptureOwned()
    {
        RequireOwned();if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Directory.CreateDirectory("Library/Shiganshina");
        var camera=new GameObject("OWNED UNSAVED architecture review camera").AddComponent<Camera>();camera.nearClipPlane=.3f;camera.farClipPlane=4500;camera.fieldOfView=65;
        try
        {
            Capture(camera,"District_Overview",new Vector3(900,770,-1110),new Vector3(0,20,-40),false);
            Capture(camera,"Gate_Avenue",new Vector3(0,9,-620),new Vector3(0,26,-200),true);
            Capture(camera,"Rooftop_Route",new Vector3(-170,47,-250),new Vector3(70,26,5),true);
            Capture(camera,"Canal_Bridges",new Vector3(-85,25,70),new Vector3(210,3,110),true);
            Capture(camera,"Civic_Square",new Vector3(0,16,350),new Vector3(-20,40,480),true);
            Capture(camera,"Outer_Gate",new Vector3(0,48,-585),new Vector3(0,60,-768),true);
            Capture(camera,"Town_Plan",new Vector3(0,1800,-90),new Vector3(0,0,-89),false);
        }
        finally{Object.DestroyImmediate(camera.gameObject);}
        Debug.Log("SG_CAPTURE_COMPLETE native="+SystemInfo.graphicsDeviceName+" play=false sceneSaved=false");
    }
    static void Capture(Camera camera,string name,Vector3 position,Vector3 target,bool fog)
    {
        RenderSettings.fog=fog;camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
        var rt=RenderTexture.GetTemporary(1600,1000,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Library/Shiganshina/"+name+".png",image.EncodeToPNG());
        Object.DestroyImmediate(image);RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Debug.Log("SG_NATIVE_ART "+name);
    }
}

public sealed class ShiganshinaAssetImporter:AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if(!assetPath.StartsWith(ShiganshinaDistrictBuilder.Art+"/Models/",StringComparison.Ordinal))return;
        var m=(ModelImporter)assetImporter;m.importAnimation=false;m.importCameras=false;m.importLights=false;m.animationType=ModelImporterAnimationType.None;m.globalScale=1;m.addCollider=false;
        m.isReadable=assetPath.Contains("_Collision");m.importNormals=ModelImporterNormals.Import;m.importTangents=ModelImporterTangents.CalculateMikk;
        m.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith(ShiganshinaDistrictBuilder.Art+"/Textures/",StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;t.mipmapEnabled=true;t.anisoLevel=4;t.filterMode=FilterMode.Trilinear;t.wrapMode=TextureWrapMode.Repeat;t.maxTextureSize=1024;
        if(assetPath.Contains("_Normal")){t.textureType=TextureImporterType.NormalMap;t.sRGBTexture=false;}
    }
}
