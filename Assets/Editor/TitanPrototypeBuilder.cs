using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Reproducible authoring for the NEW prototype only. Does not edit legacy Titan prefabs/scenes.</summary>
[InitializeOnLoad]
public static class TitanPrototypeBuilder
{
    public const string Folder = "Assets/Art/Titans/Prototype";
    public const string ModelPath = Folder + "/TitanPrototype.fbx";
    public const string PrefabPath = "Assets/Prefabs/Titans/ModularTitanPrototype.prefab";
    public const string ScenePath = "Assets/Scenes/TitanPrototypeSandbox.unity";
    const string Request = "Library/TitanPrototypeBuild.request";

    static TitanPrototypeBuilder()
    {
        // A one-shot local authoring request, never an automatic scene switch.
        EditorApplication.delayCall += ProcessBuildRequest;
    }

    static void ProcessBuildRequest()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessBuildRequest;
            return;
        }
        bool connectOnly = File.ReadAllText(Request).Trim() == "connect-only";
        File.Delete(Request);
        try
        {
            if (!connectOnly) BuildAssets();
            // Reconnect the already-configured local MCP bridge after this domain reload.
            // Reflection keeps the authoring tool usable if MCP is later uninstalled.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var locator = assembly.GetType("MCPForUnity.Editor.Services.MCPServiceLocator");
                if (locator == null) continue;
                var bridge = locator.GetProperty("Bridge").GetValue(null, null);
                bridge.GetType().GetMethod("StartAsync").Invoke(bridge, null);
                break;
            }
            File.WriteAllText("Library/TitanPrototypeBuild.result.txt", ValidatePrototype());
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            File.WriteAllText("Library/TitanPrototypeBuild.result.txt", ex.ToString());
        }
    }

    [MenuItem("Tools/Titans/Build Modular Prototype Assets")]
    public static void BuildAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!File.Exists(ModelPath)) throw new FileNotFoundException("Export the Blender model first.", ModelPath);
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Animations");
        Directory.CreateDirectory(Folder + "/Meshes");
        Directory.CreateDirectory("Assets/Prefabs/Titans");
        AssetDatabase.Refresh();
        ConfigureDetailedTextures();
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.isReadable = true;
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.SaveAndReimport();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (asset == null) throw new InvalidOperationException("Model import is not ready.");
        var root = new GameObject("ModularTitanPrototype");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = "Visual";
            model.transform.SetParent(root.transform, false);
            // Unpack only our new temporary instance, so we can configure mesh roots and physics.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var body = root.AddComponent<ModularTitanBody>();
            body.animator = model.GetComponentInChildren<Animator>();
            if (body.animator == null) body.animator = model.AddComponent<Animator>();
            body.animator.applyRootMotion = false;
            body.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var bones = model.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
            Func<string, Transform> bone = name => bones[name];
            body.hips = bone("Hips"); body.head = bone("Head");
            body.leftUpperArm = bone("LeftUpperArm"); body.leftLowerArm = bone("LeftLowerArm"); body.leftHand = bone("LeftHand");
            body.rightUpperArm = bone("RightUpperArm"); body.rightLowerArm = bone("RightLowerArm"); body.rightHand = bone("RightHand");
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool referenceArt = renderers.Any(r => r.name.StartsWith("TitanReference_"));
            var materials = MakeMaterials();
            foreach (var renderer in renderers)
            {
                // Keep canonical skin names in the prefab even when the source
                // collection coexists with the original blockout in Blender.
                renderer.gameObject.name = CanonicalSkinName(renderer.name);
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                    string name = m != null ? m.name.Replace("_Detailed", "").Replace("_Reference", "") : "";
                    return materials.ContainsKey(name) ? materials[name] : materials["Titan_WarmClay"];
                }).ToArray();
                TrimSkinBindings(renderer);
                renderer.updateWhenOffscreen = true;
                string part = renderer.name.Replace("Titan_", "");
                renderer.rootBone = bones.ContainsKey(part) ? bone(part) : body.hips;
                renderer.receiveShadows = true;
            }
            var physics = new List<TitanRagdollBone>();
            var bodies = new Dictionary<string, Rigidbody>();
            var colliders = new Dictionary<string, Collider>();
            Action<string, Vector3, Vector3, float, string> boxBody = (name, center, size, mass, parent) =>
            {
                var transform = bone(name);
                Transform shape = transform;
                if (name.EndsWith("Foot"))
                {
                    // Foot bones point diagonally toward the toes. A box on that
                    // axis was oversized vertically and made the visible feet hover.
                    shape = new GameObject("FootPhysicsShape").transform;
                    shape.SetParent(transform, false);
                    shape.position = root.transform.TransformPoint(center);
                    shape.rotation = root.transform.rotation;
                    shape.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f / transform.lossyScale.z);
                }
                var col = shape.gameObject.AddComponent<BoxCollider>();
                col.center = shape.InverseTransformPoint(root.transform.TransformPoint(center));
                col.size = Abs(shape.InverseTransformVector(root.transform.TransformVector(size)));
                AddBody(name, transform, col, mass, parent, bodies, physics);
                colliders.Add(name, col);
            };
            Action<string, string, float, float, string> capsuleBody = (name, endName, radius, mass, parent) =>
            {
                var transform = bone(name);
                Vector3 end = transform.InverseTransformPoint(bone(endName).position);
                var col = transform.gameObject.AddComponent<CapsuleCollider>();
                col.center = end * 0.5f;
                col.direction = DominantAxis(end);
                col.radius = radius / Mathf.Max(0.001f, transform.lossyScale.x);
                col.height = Mathf.Max(col.radius * 2, end.magnitude * 0.92f);
                AddBody(name, transform, col, mass, parent, bodies, physics);
                colliders.Add(name, col);
            };
            boxBody("Hips", new Vector3(0, 5.95f, 0), referenceArt ? new Vector3(2.85f, 1.25f, 1.55f) : new Vector3(1.8f, 1.25f, 1.05f), 75, null);
            boxBody("Spine", new Vector3(0, 7.17f, 0), referenceArt ? new Vector3(2.85f, 1.35f, 1.55f) : new Vector3(1.55f, 1.35f, 0.95f), 55, "Hips");
            boxBody("Chest", new Vector3(0, 8.65f, 0), new Vector3(2.8f, 1.75f, 1.3f), 90, "Spine");
            capsuleBody("Neck", "Head", 0.43f, 12, "Chest");
            float headDrop = referenceArt ? .38f : 0;
            boxBody("Head", new Vector3(0, 10.95f - headDrop, 0), new Vector3(referenceArt ? 1.50f : 1.35f, 1.65f, 1.20f), 25, "Neck");
            foreach (string side in new[] { "Left", "Right" })
            {
                float sign = Mathf.Sign(bone(side + "Hand").position.x - root.transform.position.x);
                if (referenceArt)
                {
                    // Broad reference limbs need broad hit zones, not the old thin capsules.
                    boxBody(side + "UpperArm", new Vector3(sign * 2.42f, 9.03f, 0), new Vector3(1.98f, 1.08f, 1.02f), 22, "Chest");
                    boxBody(side + "LowerArm", new Vector3(sign * 4.03f, 9.03f, 0), new Vector3(1.48f, 1.06f, 1.00f), 15, side + "UpperArm");
                    boxBody(side + "Hand", new Vector3(sign * 5.11f, 9.03f, 0), new Vector3(.88f, 1.04f, 1.00f), 6, side + "LowerArm");
                    boxBody(side + "UpperLeg", new Vector3(sign * .70f, 4.35f, .025f), new Vector3(1.17f, 2.72f, 1.13f), 50, "Hips");
                    boxBody(side + "LowerLeg", new Vector3(sign * .70f, 1.84f, 0), new Vector3(1.15f, 2.57f, 1.11f), 30, side + "UpperLeg");
                    boxBody(side + "Foot", new Vector3(sign * .70f, .36f, .20f), new Vector3(1.17f, .70f, 1.44f), 10, side + "LowerLeg");
                }
                else
                {
                    capsuleBody(side + "UpperArm", side + "LowerArm", 0.43f, 22, "Chest");
                    capsuleBody(side + "LowerArm", side + "Hand", 0.33f, 15, side + "UpperArm");
                    boxBody(side + "Hand", new Vector3(sign * 5.22f, 9.03f, 0), new Vector3(1.1f, .70f, .40f), 6, side + "LowerArm");
                    capsuleBody(side + "UpperLeg", side + "LowerLeg", 0.52f, 50, "Hips");
                    capsuleBody(side + "LowerLeg", side + "Foot", 0.33f, 30, side + "UpperLeg");
                    float footX = root.transform.InverseTransformPoint(bone(side + "Foot").position).x;
                    boxBody(side + "Foot", new Vector3(footX, .36f, .38f), new Vector3(.75f, .60f, 1.6f), 10, side + "LowerLeg");
                }
            }
            body.ragdollBones = physics.ToArray();
            var zones = new List<TitanHitZone>();
            var limbs = new List<TitanDetachableLimb>();
            foreach (string side in new[] { "Left", "Right" })
            {
                foreach (string type in new[] { "Arm", "Leg" })
                {
                    var names = type == "Arm" ? new[] { side+"UpperArm", side+"LowerArm", side+"Hand" } :
                        new[] { side+"UpperLeg", side+"LowerLeg", side+"Foot" };
                    var region = (TitanHitRegion)Enum.Parse(typeof(TitanHitRegion), side + type);
                    var limbZones = new List<TitanHitZone>();
                    foreach (var name in names)
                    {
                        var source = colliders[name];
                        var zone = CopyZone(source, region, body);
                        zones.Add(zone); limbZones.Add(zone);
                    }
                    limbs.Add(new TitanDetachableLimb {
                        region = region, rootBone = bone(names[0]), bodies = names.Select(n => bodies[n]).ToArray(),
                        renderers = renderers.Where(r => names.Any(n => r.name == "Titan_" + n)).Cast<Renderer>().ToArray(),
                        hitZones = limbZones.ToArray()
                    });
                }
            }
            body.limbs = limbs.ToArray();
            zones.Add(BoxZone(bone("Head"), "Eyes_DamageZone", new Vector3(0, 11.03f - headDrop, referenceArt ? .70f : .82f), new Vector3(referenceArt ? 1.15f : 1.0f, .40f, .35f), TitanHitRegion.Eyes, root.transform, body));
            zones.Add(BoxZone(bone("Neck"), "Nape_DamageZone", new Vector3(0, 10.04f - headDrop, -.56f), new Vector3(.64f, .64f, .32f), TitanHitRegion.Nape, root.transform, body));
            body.hitZones = zones.ToArray();
            body.mouthSocket = Socket(bone("Jaw"), "MouthSocket", root.transform.TransformPoint(new Vector3(0, 10.35f - headDrop, .90f)));
            body.leftGripSocket = Socket(body.leftHand, "LeftGripSocket", body.leftHand.position + (body.leftHand.position - body.leftLowerArm.position).normalized * .55f + root.transform.forward * .20f);
            body.rightGripSocket = Socket(body.rightHand, "RightGripSocket", body.rightHand.position + (body.rightHand.position - body.rightLowerArm.position).normalized * .55f + root.transform.forward * .20f);
            body.animator.runtimeAnimatorController = BuildAnimator(body.animator, bones);
            root.AddComponent<TitanMotionStyle>();
            root.AddComponent<TitanInjuryPose>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        BuildSandbox();
        AssetDatabase.SaveAssets();
        Debug.Log("Titan prototype built: " + PrefabPath + " | Sandbox: " + ScenePath + ". Gameplay scenes left unchanged.");
    }

    static void TrimSkinBindings(SkinnedMeshRenderer renderer)
    {
        // Blender FBX lists the entire skeleton for every mesh, including zero-weight
        // bones. Remove those entries so detached skins have NO external references.
        var source = renderer.sharedMesh;
        var weights = source.boneWeights;
        var used = new SortedSet<int>();
        foreach (var w in weights)
        {
            if (w.weight0 > 0) used.Add(w.boneIndex0);
            if (w.weight1 > 0) used.Add(w.boneIndex1);
            if (w.weight2 > 0) used.Add(w.boneIndex2);
            if (w.weight3 > 0) used.Add(w.boneIndex3);
        }
        var indices = used.ToArray();
        var map = new Dictionary<int, int>();
        for (int i = 0; i < indices.Length; i++) map[indices[i]] = i;
        for (int i = 0; i < weights.Length; i++)
        {
            var w = weights[i];
            w.boneIndex0 = w.weight0 > 0 ? map[w.boneIndex0] : 0;
            w.boneIndex1 = w.weight1 > 0 ? map[w.boneIndex1] : 0;
            w.boneIndex2 = w.weight2 > 0 ? map[w.boneIndex2] : 0;
            w.boneIndex3 = w.weight3 > 0 ? map[w.boneIndex3] : 0;
            weights[i] = w;
        }
        var mesh = Object.Instantiate(source);
        mesh.name = renderer.name;
        mesh.boneWeights = weights;
        mesh.bindposes = indices.Select(i => source.bindposes[i]).ToArray();
        string path = Folder + "/Meshes/" + renderer.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else
        {
            // CopySerialized does not reliably replace a Mesh's native vertex/UV
            // buffers when a replacement model has different topology. Copy the
            // complete geometry explicitly while preserving this asset's GUID.
            existing.Clear(false);
            existing.name = mesh.name;
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.tangents = mesh.tangents;
            existing.colors32 = mesh.colors32;
            for (int channel = 0; channel < 8; channel++)
            {
                var uvs = new List<Vector4>();
                mesh.GetUVs(channel, uvs);
                if (uvs.Count > 0) existing.SetUVs(channel, uvs);
            }
            existing.bindposes = mesh.bindposes;
            existing.boneWeights = mesh.boneWeights;
            existing.subMeshCount = mesh.subMeshCount;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                existing.SetIndices(mesh.GetIndices(sub, false), mesh.GetTopology(sub), sub, false, (int)mesh.GetBaseVertex(sub));
            existing.bounds = mesh.bounds;
            Object.DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        renderer.sharedMesh = mesh;
        var originalBones = renderer.bones;
        renderer.bones = indices.Select(i => originalBones[i]).ToArray();
    }

    static Vector3 Abs(Vector3 vector) { return new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z)); }
    static int DominantAxis(Vector3 vector)
    {
        vector = Abs(vector);
        return vector.x > vector.y && vector.x > vector.z ? 0 : vector.z > vector.y ? 2 : 1;
    }

    static void AddBody(string name, Transform bone, Collider collider, float mass, string parent,
        Dictionary<string, Rigidbody> bodies, List<TitanRagdollBone> entries)
    {
        var rb = bone.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass; rb.isKinematic = true; rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.drag = .08f; rb.angularDrag = .5f;
        rb.solverIterations = 12; rb.solverVelocityIterations = 4;
        if (parent != null)
        {
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = bodies[parent];
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(bone.position);
            joint.axis = Vector3.up;
            joint.swingAxis = Vector3.right;
            bool knee = name.EndsWith("LowerLeg");
            bool shoulder = name.EndsWith("UpperArm");
            joint.lowTwistLimit = new SoftJointLimit { limit = knee ? -5 : shoulder ? -65 : -30 };
            joint.highTwistLimit = new SoftJointLimit { limit = knee ? 5 : shoulder ? 65 : 30 };
            // The bind pose is a T-pose; the idle arms hang ~80 degrees lower.
            // A 45-degree shoulder limit violently snaps the corpse toward T-pose.
            joint.swing1Limit = new SoftJointLimit { limit = knee ? 65 : shoulder ? 110 : 45 };
            joint.swing2Limit = new SoftJointLimit { limit = knee ? 8 : shoulder ? 110 : 35 };
            joint.enableProjection = true; joint.projectionDistance = .15f; joint.projectionAngle = 15;
            joint.enablePreprocessing = false; joint.enableCollision = false;
        }
        bodies.Add(name, rb);
        entries.Add(new TitanRagdollBone { body = rb, collider = collider });
    }

    static TitanHitZone CopyZone(Collider source, TitanHitRegion region, ModularTitanBody body)
    {
        var go = new GameObject(region + "_DamageZone");
        go.transform.SetParent(source.transform, false);
        var capsule = source as CapsuleCollider;
        if (capsule != null)
        {
            var col = go.AddComponent<CapsuleCollider>();
            col.center = capsule.center; col.direction = capsule.direction;
            col.radius = capsule.radius * 1.08f; col.height = capsule.height * 1.04f;
            col.isTrigger = true;
        }
        else
        {
            var sourceBox = (BoxCollider)source;
            var col = go.AddComponent<BoxCollider>();
            col.center = sourceBox.center; col.size = sourceBox.size * 1.08f; col.isTrigger = true;
        }
        var zone = go.AddComponent<TitanHitZone>(); zone.region = region; zone.owner = body;
        return zone;
    }

    static TitanHitZone BoxZone(Transform parent, string name, Vector3 center, Vector3 size,
        TitanHitRegion region, Transform root, ModularTitanBody body)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = root.TransformPoint(center);
        go.transform.rotation = root.rotation;
        // FBX skeleton scale is 100; these dimensions are authored in WORLD metres.
        go.transform.localScale = new Vector3(1f / parent.lossyScale.x, 1f / parent.lossyScale.y, 1f / parent.lossyScale.z);
        var col = go.AddComponent<BoxCollider>(); col.size = size; col.isTrigger = true;
        var zone = go.AddComponent<TitanHitZone>(); zone.region = region; zone.owner = body;
        return zone;
    }

    static Transform Socket(Transform parent, string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false); go.transform.position = position;
        go.transform.localScale = new Vector3(1f / parent.lossyScale.x, 1f / parent.lossyScale.y, 1f / parent.lossyScale.z);
        return go.transform;
    }

    public static string CanonicalSkinName(string name)
    {
        return name.Replace("TitanDetailed_", "Titan_").Replace("TitanReference_", "Titan_");
    }

    static Dictionary<string, Material> MakeMaterials()
    {
        var colors = new Dictionary<string, Color> {
            {"Titan_WarmClay", new Color(.48f,.255f,.145f)}, {"Titan_RaisedPlanes", new Color(.60f,.36f,.215f)},
            {"Titan_DeepPlanes", new Color(.235f,.095f,.06f)}, {"Titan_SeamCap", new Color(.30f,.075f,.055f)},
            {"Titan_CharcoalHair", new Color(.035f,.027f,.023f)}, {"Titan_EyeIvory", new Color(.86f,.78f,.60f)},
            {"Titan_EyeDark", new Color(.012f,.009f,.008f)}, {"Titan_Teeth", new Color(.69f,.58f,.38f)},
            {"Titan_Iris", new Color(.16f,.21f,.13f)}, {"Titan_HairStrand", new Color(.055f,.030f,.013f)}
        };
        var result = new Dictionary<string, Material>();
        foreach (var pair in colors) result.Add(pair.Key, MakeMaterial(pair.Key, pair.Value));
        return result;
    }

    static void ConfigureDetailedTextures()
    {
        foreach (string map in new[] { "BaseColor", "Normal", "MetallicSmoothness" })
        {
            string path = Folder + "/Textures/TitanDetailed_" + map + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = map == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = map == "BaseColor";
            importer.convertToNormalmap = false;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }

    static Material MakeMaterial(string name, Color color)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        var atlas = name.StartsWith("Titan_") ? AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/TitanDetailed_BaseColor.png") : null;
        mat.color = atlas != null ? Color.white : color;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", atlas != null ? 1 : .22f);
        if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", atlas != null ? 1 : .22f);
        var normal = atlas != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/TitanDetailed_Normal.png") : null;
        var mask = atlas != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/TitanDetailed_MetallicSmoothness.png") : null;
        if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
        if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", .65f);
        if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", mask);
        if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
        if (mask != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP"); else mat.DisableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static AnimatorController BuildAnimator(Animator animator, Dictionary<string, Transform> bones)
    {
        return TitanMotionClipBuilder.Build(animator, bones, Folder);
    }

    static void BuildSandbox()
    {
        if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
        {
            Debug.Log("Titan sandbox is already open; leaving its scene contents untouched. Close it before rebuilding the lab scene.");
            return;
        }
        // Author a dedicated additive scene, save ONLY it, then close it. Never save,
        // replace or unload the user's current scene (which may contain unsaved work).
        Scene previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Lab Floor"; floor.transform.position = new Vector3(0,-.2f,0); floor.transform.localScale = new Vector3(60,.4f,60);
            floor.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Lab_Floor", new Color(.085f,.105f,.12f));
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "12m Titan Scale Pad"; pedestal.transform.localScale = new Vector3(15,.025f,15);
            pedestal.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Lab_Pad", new Color(.14f,.20f,.22f));
            Object.DestroyImmediate(pedestal.GetComponent<Collider>());
            var human = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            human.name = "Human Scale Reference - 1.8m"; human.transform.position = new Vector3(4,.9f,0);
            human.transform.localScale = new Vector3(.45f,.9f,.45f);
            human.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Lab_Human", new Color(.12f,.62f,.65f));
            Object.DestroyImmediate(human.GetComponent<Collider>());
            var label = new GameObject("Scale Label").AddComponent<TextMesh>();
            label.text = "HUMAN / 1.8m"; label.characterSize = .10f; label.fontSize = 60;
            label.anchor = TextAnchor.MiddleCenter; label.transform.position = new Vector3(4,2.25f,0);
            label.transform.rotation = Quaternion.Euler(0,180,0);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var titan = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(35,-35,0);
            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .65f; fill.color = new Color(.60f,.76f,1);
            fill.transform.rotation = Quaternion.Euler(30,145,0);
            var camera = new GameObject("Lab Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(10,8.7f,20);
            camera.transform.LookAt(new Vector3(0,5.5f,0)); camera.fieldOfView = 42;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.05f,.065f);
            camera.farClipPlane = 150;
            camera.gameObject.AddComponent<AudioListener>();
            var demo = new GameObject("Titan Body Lab Controls").AddComponent<TitanSandboxDemo>();
            demo.titanPrefab = prefab; demo.titan = titan.GetComponent<ModularTitanBody>(); demo.previewCamera = camera;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Tools/Titans/Validate Modular Prototype")]
    public static void ValidateMenu() { Debug.Log(ValidatePrototype()); }

    public static string ValidatePrototype()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Prototype prefab missing.");
        var body = prefab.GetComponent<ModularTitanBody>();
        var errors = new List<string>();
        if (body == null) throw new InvalidOperationException("Body component missing.");
        if (prefab.GetComponent<TitanInjuryPose>() == null) errors.Add("Leg-loss injury animation missing.");
        if (prefab.GetComponent<TitanMotionStyle>() == null) errors.Add("Velocity-matched motion style missing.");
        if (body.ragdollBones.Length != 17) errors.Add("Expected 17 ragdoll bodies.");
        if (body.limbs.Length != 4) errors.Add("Expected four detachable limbs.");
        if (body.hitZones.Length != 14) errors.Add("Expected 12 limb zones + eyes + nape.");
        if (body.animator == null || body.animator.runtimeAnimatorController == null) errors.Add("Animator/controller missing.");
        else if (!body.animator.runtimeAnimatorController.animationClips.Any(c => c.name == "Run")) errors.Add("Distinct chase run clip missing.");
        foreach (var entry in body.ragdollBones)
        {
            if (entry.body == null || entry.collider == null) { errors.Add("Missing physics reference."); continue; }
            if (!entry.body.isKinematic || !entry.collider.enabled || entry.collider.isTrigger) errors.Add("Invalid animated physics configuration.");
            if (entry.body.transform != body.hips && entry.body.GetComponent<CharacterJoint>().connectedBody == null) errors.Add("Disconnected joint.");
        }
        foreach (var limb in body.limbs)
        {
            if (limb.rootBone == null || limb.bodies.Length != 3 || limb.renderers.Length != 3 || limb.hitZones.Length != 3)
                errors.Add("Incomplete detachable limb: " + limb.region);
            foreach (var renderer in limb.renderers)
            {
                var skin = renderer as SkinnedMeshRenderer;
                if (skin == null || skin.bones.Any(b => b != limb.rootBone && !b.IsChildOf(limb.rootBone)))
                    errors.Add("Limb skin references an external bone: " + limb.region);
            }
        }
        foreach (var zone in body.hitZones)
        {
            if (zone == null || zone.owner != body || !zone.GetComponent<Collider>().isTrigger) errors.Add("Invalid damage zone.");
            if (zone != null && (zone.region == TitanHitRegion.Eyes || zone.region == TitanHitRegion.Nape) &&
                Mathf.Abs(zone.transform.lossyScale.x - 1) > .01f) errors.Add("Face/nape zone scale must be in metres.");
        }
        var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var sourceSkins = sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var source = sourceSkins.FirstOrDefault(s => CanonicalSkinName(s.name) == skin.name);
            if (source == null || source.sharedMesh.vertexCount != skin.sharedMesh.vertexCount ||
                source.sharedMesh.uv.Length != skin.sharedMesh.uv.Length)
                errors.Add("Stale generated mesh/UV buffers: " + skin.name);
        }
        if (body.mouthSocket == null || body.leftGripSocket == null || body.rightGripSocket == null) errors.Add("Attack socket missing.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        string result = "PASS: 17 physics bodies, 16 joints, 4 capped detachable limbs, 14 damage zones, idle/walk/run Animator, velocity-matched motion, injury poses, 3 attack sockets. Sandbox and legacy scenes isolated.";
        return result;
    }
}
