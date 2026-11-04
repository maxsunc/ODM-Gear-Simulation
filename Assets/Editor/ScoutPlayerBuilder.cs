using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Owned Scout assets + explicit reversible scene opt-in. Never rebuilds Titans.</summary>
public static class ScoutPlayerBuilder
{
    const string Root="Assets/Art/Player/Scout";
    const string Anim=Root+"/Animations";
    public const string Prefab="Assets/Prefabs/Player/ScoutVisual.prefab";
    static readonly string[] States={"Idle","Run","Rise","Fall","AirCoast","Land","GasLeft","GasRight","GasBoth","AttachedLeft","AttachedRight","AttachedBoth","ReleaseBoth","ReleaseFlip","ChargeGround","ChargeAir","ChargeHeld","AttackGround","AttackAir","AttackHeld","Held","Dead","ChargeGround_Right","ChargeAir_Right","ChargeHeld_Right","AttackGround_Right","AttackAir_Right","AttackHeld_Right"};
    sealed class Pose
    {
        public Dictionary<string,Vector3> angles=new Dictionary<string,Vector3>();
        public Vector3 hip;
        public void Set(string n,float x=0,float y=0,float z=0) { angles[n]=new Vector3(x,y,z); }
        public void Arm(string side,float drop,float pitch=0,float bend=12)
        { float sign=side=="Left"?1:-1;Set(side+"UpperArm",pitch,0,sign*drop);Set(side+"LowerArm",0,sign*bend,0); }
    }
    static Pose Blend(Pose a,Pose b,float f)
    {
        var p=new Pose();p.hip=Vector3.Lerp(a.hip,b.hip,f);
        var names=new HashSet<string>(a.angles.Keys);names.UnionWith(b.angles.Keys);
        foreach(var n in names){Vector3 x,y;a.angles.TryGetValue(n,out x);b.angles.TryGetValue(n,out y);p.angles[n]=Vector3.Lerp(x,y,f);}return p;
    }
    static Vector3 Cubic(Vector3 a,Vector3 b,Vector3 da,Vector3 db,float f,float span)
    {
        float f2=f*f,f3=f2*f;
        return (2*f3-3*f2+1)*a+(f3-2*f2+f)*span*da+(-2*f3+3*f2)*b+(f3-f2)*span*db;
    }
    static Vector3 Angle(Pose p,string name) { Vector3 a;p.angles.TryGetValue(name,out a);return a; }
    static Pose Stroke(float t,Pose[] keys)
    {
        float[] times={.065f,.10f,.14f,.185f};
        if(t<=times[0])return keys[0];if(t>=times[3])return keys[3];
        int i=t<times[1]?0:t<times[2]?1:2;float span=times[i+1]-times[i],f=(t-times[i])/span;
        var p=new Pose();var names=new HashSet<string>();foreach(var key in keys)names.UnionWith(key.angles.Keys);
        foreach(string n in names)
        {
            Vector3 da=i==0?Vector3.zero:(Angle(keys[i+1],n)-Angle(keys[i-1],n))/(times[i+1]-times[i-1]);
            Vector3 db=i==2?Vector3.zero:(Angle(keys[i+2],n)-Angle(keys[i],n))/(times[i+2]-times[i]);
            p.angles[n]=Cubic(Angle(keys[i],n),Angle(keys[i+1],n),da,db,f,span);
        }
        Vector3 ha=i==0?Vector3.zero:(keys[i+1].hip-keys[i-1].hip)/(times[i+1]-times[i-1]);
        Vector3 hb=i==2?Vector3.zero:(keys[i+2].hip-keys[i].hip)/(times[i+2]-times[i]);
        p.hip=Cubic(keys[i].hip,keys[i+1].hip,ha,hb,f,span);return p;
    }
    static Pose Idle(float t)
    {
        var p=new Pose();p.Arm("Left",78,0,12);p.Arm("Right",78,0,12);
        p.Set("LeftUpperLeg",0,0,-2);p.Set("RightUpperLeg",0,0,2);
        p.Set("Chest",2+Mathf.Sin(t*Mathf.PI*2)*.7f);p.Set("Neck",-2);
        p.Set("CapeUpper",-3);p.Set("CapeLower",2);return p;
    }
    static Pose Mirror(Pose source)
    {
        var p=new Pose();p.hip=new Vector3(-source.hip.x,source.hip.y,source.hip.z);
        foreach(var pair in source.angles)
        {
            string n=pair.Key.StartsWith("Left")?"Right"+pair.Key.Substring(4):pair.Key.StartsWith("Right")?"Left"+pair.Key.Substring(5):pair.Key;
            p.angles[n]=new Vector3(pair.Value.x,-pair.Value.y,-pair.Value.z);
        }
        return p;
    }
    static Pose Charge(bool air,bool held=false)
    {
        // BOTH hands load behind the LEFT shoulder for the rightward cut. The
        // near elbow folds deeply; the opposite arm crosses behind the neck.
        // Mirroring this entire pose puts BOTH hands on the right for a left cut.
        var p=Idle(0);p.Set("Hips",air?18:6,held?0:-10);p.Set("Chest",4,-10);p.Set("Head",-10,12);
        p.Set("LeftUpperArm",-55,0,-25);p.Set("RightUpperArm",-75,100,40);
        p.Set("LeftLowerArm",0,0,-125);p.Set("RightLowerArm",0,0,60);
        p.Set("LeftHand",35,-70);p.Set("RightHand",-35,70);
        if(!held){p.Set("LeftUpperLeg",air?-45:8);p.Set("RightUpperLeg",air?10:-8);p.Set("LeftLowerLeg",air?90:15);p.Set("RightLowerLeg",air?35:18);}
        return p;
    }
    static Pose ArcTop(bool air,bool held)
    {
        var p=Charge(air,held);p.Set("Hips",air?22:9,held?0:-6);p.Set("Chest",-6,-18);
        // Carry the loaded side into the arc instead of opening into the same
        // symmetric hands-above-head waypoint for both swing directions.
        p.Set("LeftUpperArm",-10,0,-70);p.Set("RightUpperArm",-10,70,120);
        p.Set("LeftLowerArm",0,0,-50);p.Set("RightLowerArm",0,0,20);
        p.Set("LeftHand",35,-15);p.Set("RightHand",-35,15);return p;
    }
    static Pose Cut(bool air,bool held=false,bool extended=false)
    {
        var p=Idle(0);p.Set("Hips",air?30:12,held?0:extended?12:0);p.Set("Chest",extended?8:12,extended?36:6);p.Set("Head",-10,extended?-22:-6);
        p.Set("LeftUpperArm",-12,extended?125:78,-8);p.Set("RightUpperArm",-12,extended?-55:-78,8);
        p.Set("LeftLowerArm",0,extended?4:8);p.Set("RightLowerArm",0,extended?-4:-8);
        p.Set("LeftHand",35);p.Set("RightHand",-35);
        if(!held){p.Set("LeftUpperLeg",air?12:-12);p.Set("RightUpperLeg",air?-35:12);p.Set("LeftLowerLeg",air?35:18);p.Set("RightLowerLeg",air?80:18);p.hip=new Vector3(.025f,-.025f,0);}
        p.Set("CapeUpper",-25);p.Set("CapeLower",-15);return p;
    }
    // C1 authored phase curves: distinct support, rear drive, heel recovery and
    // forward reach. Keeping the rear drive separate avoids a tucked bicycle gait.
    static float RunCurve(float time,Vector3[] keys)
    {
        for(int i=1;i<keys.Length;i++)if(time<=keys[i].x)
        {
            Vector3 a=keys[i-1],b=keys[i];float h=b.x-a.x,s=Mathf.Clamp01((time-a.x)/h),s2=s*s,s3=s2*s;
            return (2*s3-3*s2+1)*a.y+(s3-2*s2+s)*h*a.z+(-2*s3+3*s2)*b.y+(s3-s2)*h*b.z;
        }
        return keys[keys.Length-1].y;
    }
    const float RunFront=.22f,RunToeForward=.18f,RunHeelForward=-.05f,RunSoleDepth=.1185f;
    static readonly Vector3[] RunPitch={new Vector3(0,-6,0),new Vector3(.07f,0,0),new Vector3(.14f,0,0),new Vector3(.30f,65,0),new Vector3(.42f,128,0),new Vector3(.56f,128,-100),new Vector3(.70f,65,-450),new Vector3(.88f,-10,0),new Vector3(1,-6,0)};
    static float RunContactLift(float pitch)
    {
        float a=pitch*Mathf.Deg2Rad,pivot=pitch>=0?RunToeForward:RunHeelForward;
        return Mathf.Max(0,.0115f+RunSoleDepth*Mathf.Cos(a)+pivot*Mathf.Sin(a)-.13f);
    }
    static float RunContactRoll(float pitch)
    {
        float a=pitch*Mathf.Deg2Rad,pivot=pitch>=0?RunToeForward:RunHeelForward;
        return pivot-(pivot*Mathf.Cos(a)-RunSoleDepth*Mathf.Sin(a));
    }
    static readonly Vector3[] RunSwingForward={
        new Vector3(.30f,RunFront-2*ScoutPlayerAnimation.RunHalfStride+RunContactRoll(65),-2*ScoutPlayerAnimation.RunHalfStride/ScoutPlayerAnimation.RunStanceFraction),
        new Vector3(.42f,-.66f,0),new Vector3(.56f,-.38f,2.4f),new Vector3(.70f,-.08f,2.5f),new Vector3(.88f,.27f,0),
        new Vector3(1,RunFront+RunContactRoll(-6),-2*ScoutPlayerAnimation.RunHalfStride/ScoutPlayerAnimation.RunStanceFraction)};
    static readonly Vector3[] RunSwingLift={new Vector3(.30f,RunContactLift(65),0),new Vector3(.42f,.39f,.5f),new Vector3(.56f,.56f,0),new Vector3(.70f,.44f,-1.4f),new Vector3(.88f,.115f,-1.7f),new Vector3(1,RunContactLift(-6),0)};
    static readonly Vector3[] RunPelvis={new Vector3(0,-.07f,-.35f),new Vector3(.10f,-.10f,0),new Vector3(.26f,-.085f,.15f),new Vector3(.32f,-.065f,.65f),new Vector3(.42f,-.025f,0),new Vector3(.50f,-.07f,-.35f)};
    // Dimensions/rest pitch match the owned Scout FBX, in authoring metres.
    static void RunLeg(Pose p,string side,float phase,float pelvisPitch)
    {
        phase=Mathf.Repeat(phase,1);float stance=ScoutPlayerAnimation.RunStanceFraction;
        float forward,lift,footPitch=RunCurve(phase,RunPitch);
        if(phase<stance)
        {
            // Plant the heel, then roll about the actual toe. Compensating ankle
            // position prevents a planted toe skating backwards during push-off.
            forward=RunFront-2*ScoutPlayerAnimation.RunHalfStride*phase/stance+RunContactRoll(footPitch);
            lift=RunContactLift(footPitch);
        }
        else {forward=RunCurve(phase,RunSwingForward);lift=RunCurve(phase,RunSwingLift);}
        const float thigh=.420415f,shin=.390447f;
        float down=.81f+p.hip.y-lift;
        float distance=Mathf.Clamp(Mathf.Sqrt(forward*forward+down*down),.1f,thigh+shin-.0001f);
        float aim=Mathf.Atan2(forward,down)*Mathf.Rad2Deg;
        float hipBend=Mathf.Acos(Mathf.Clamp((thigh*thigh+distance*distance-shin*shin)/(2*thigh*distance),-1,1))*Mathf.Rad2Deg;
        float knee=Mathf.Acos(Mathf.Clamp((distance*distance-thigh*thigh-shin*shin)/(2*thigh*shin),-1,1))*Mathf.Rad2Deg;
        float upper=-aim-hipBend+2.454f-pelvisPitch,lower=knee-5.096f;
        p.Set(side+"UpperLeg",upper);p.Set(side+"LowerLeg",lower);
        p.Set(side+"Foot",footPitch-pelvisPitch-upper-lower);
        p.Set(side+"Toes",-Mathf.Max(0,footPitch)*.45f);
    }
    static Pose Sample(string state,float t,float length)
    {
        if(state.EndsWith("_Right"))return Mirror(Sample(state.Substring(0,state.Length-6),t,length));
        float f=Mathf.Clamp01(t/length),wave=Mathf.Sin(f*Mathf.PI*2);var p=Idle(f);
        if(state=="Run")
        {
            // Naruto-inspired silhouette: drive forward through the torso, keep
            // nearly straight arms/blades trailing, but retain a grounded stride.
            const float pelvisPitch=12;
            p.Set("Hips",pelvisPitch);p.Set("Spine",8,2*wave);p.Set("Chest",18,-4*wave,wave);
            p.Set("Neck",-8);p.Set("Head",-23,2*wave);
            p.hip=new Vector3(.004f*wave,RunCurve(Mathf.Repeat(f,.5f),RunPelvis),0);
            p.Arm("Left",70,38+wave*2,8);p.Arm("Right",70,38-wave*2,8);
            RunLeg(p,"Left",f,pelvisPitch);RunLeg(p,"Right",f+.5f,pelvisPitch);
            p.Set("CapeUpper",-27);p.Set("CapeLower",-16+Mathf.Sin(f*Mathf.PI*2-.5f)*3);
        }
        else if(state=="GasBoth"||state=="AttachedBoth")
        {
            // Feet-first, reclined rather than head-first diving. Only bones move:
            // gameplay root/camera/capsule and anchor ownership remain untouched.
            bool gas=state=="GasBoth";
            p.Set("Hips",gas?-62:-56);p.Set("Spine",-2);p.Set("Chest",-4+wave*.5f);
            p.Set("Neck",34);p.Set("Head",gas?20:18);
            p.Arm("Left",48,16,22);p.Arm("Right",48,16,22);
            foreach(string side in new[]{"Left","Right"})
            {
                p.Set(side+"UpperLeg",gas?-20:-24,0,side=="Left"?-2:2);
                p.Set(side+"LowerLeg",gas?10:12);p.Set(side+"Foot",gas?62:58);
            }
            p.Set("CapeUpper",gas?-20:-12);p.Set("CapeLower",-8+wave*2);
        }
        else if(state.StartsWith("Gas")||state.StartsWith("Attached"))
        {
            bool gas=state.StartsWith("Gas");p.Set("Hips",gas?53:15);p.Set("Chest",gas?7:3);
            p.Arm("Left",state.EndsWith("Right")?78:45,20,25);p.Arm("Right",state.EndsWith("Left")?78:45,20,25);
            bool rightOnly=state.EndsWith("Right");
            p.Set("LeftUpperLeg",rightOnly?4-wave*4:-12+wave*4);p.Set("RightUpperLeg",rightOnly?-12+wave*4:4-wave*4);
            p.Set("LeftLowerLeg",gas?(rightOnly?52-wave*8:66+wave*8):22);p.Set("RightLowerLeg",gas?(rightOnly?66+wave*8:52-wave*8):28);
            p.Set("Chest",gas?7:3,0,state.EndsWith("Both")?0:rightOnly?-5:5);
            p.Set("CapeUpper",gas?-48:-20);p.Set("CapeLower",-20+wave*5);
        }
        else if(state=="Fall")
        {
            p.Set("Hips",-8);p.Set("Chest",-6);p.Set("Neck",20);p.Set("Head",32);
            // Raised elbows and inward forearms: almost vertical blades cross only
            // near their tips. Slight opposite depth pitches keep the steel apart.
            p.Arm("Left",-70,15.5f,0);p.Arm("Right",-70,12.5f,0);
            p.Set("LeftLowerArm",0,0,-39.5f);p.Set("RightLowerArm",0,0,39.5f);
            p.Set("LeftHand",65);p.Set("RightHand",-65);
            p.Set("LeftUpperLeg",-8,0,-8);p.Set("RightUpperLeg",12,0,8);p.Set("LeftLowerLeg",22);p.Set("RightLowerLeg",35);p.Set("CapeUpper",-30);p.Set("CapeLower",-24);
        }
        else if(state=="Rise"||state=="AirCoast")
        {p.Set("Hips",state=="Rise"?8:20);p.Arm("Left",65,18,25);p.Arm("Right",65,18,25);p.Set("LeftUpperLeg",-15);p.Set("RightUpperLeg",10);p.Set("LeftLowerLeg",55);p.Set("RightLowerLeg",30);p.Set("CapeUpper",-30);}
        else if(state=="Land")
        {float bend=Mathf.Sin(f*Mathf.PI);p.Set("Hips",bend*20);p.hip.y=-.055f*bend;p.Set("LeftUpperLeg",-bend*18);p.Set("RightUpperLeg",-bend*18);p.Set("LeftLowerLeg",bend*38);p.Set("RightLowerLeg",bend*38);}
        else if(state.StartsWith("Charge"))return Charge(state!="ChargeGround",state=="ChargeHeld");
        else if(state.StartsWith("Attack"))
        {
            bool air=state!="AttackGround",held=state=="AttackHeld";var a=Charge(air,held);var top=ArcTop(air,held);var cut=Cut(air,held);var finish=Cut(air,held,true);
            // Swift curved cut inside the unchanged .075-.195 physical window.
            // Distinct back/overhead/front/side waypoints, not a straight thrust.
            return Stroke(t,new[]{a,top,cut,finish}); // C1 arc, then hold the fully extended finish.
        }
        else if(state=="ReleaseFlip")
        {
            var tuck=Idle(0);tuck.Set("Hips",30);tuck.Arm("Left",55,-35,75);tuck.Arm("Right",55,-35,75);tuck.Set("LeftUpperLeg",-70);tuck.Set("RightUpperLeg",-70);tuck.Set("LeftLowerLeg",115);tuck.Set("RightLowerLeg",115);tuck.Set("CapeUpper",-55);
            return f<.25f?Blend(Sample("AirCoast",0,1),tuck,f*4):f<.7f?tuck:Blend(tuck,Sample("Fall",0,1),(f-.7f)/.3f);
        }
        else if(state.StartsWith("Release"))
        {
            var recoil=Sample("AirCoast",0,1);bool left=state!="ReleaseRight",right=state!="ReleaseLeft";
            if(left)recoil.Arm("Left",38,-28,72);if(right)recoil.Arm("Right",38,-28,72);
            recoil.Set("Chest",8,state=="ReleaseLeft"?-12:state=="ReleaseRight"?12:0);
            recoil.Set("LeftLowerLeg",left?65:30);recoil.Set("RightLowerLeg",right?65:30);
            return f<.4f?Blend(Sample("AirCoast",0,1),recoil,Mathf.SmoothStep(0,1,f/.4f)):Blend(recoil,Sample("Fall",0,1),Mathf.SmoothStep(0,1,(f-.4f)/.6f));
        }
        else if(state=="Held"||state=="Dead") {p.Arm("Left",65,0,30);p.Arm("Right",65,0,30);p.Set("LeftLowerLeg",35);p.Set("RightLowerLeg",35);}
        return p;
    }
    static AnimationCurve CompactConstant(AnimationCurve curve)
    {
        var keys=curve.keys;if(keys.Length<3)return curve;float value=keys[0].value;
        foreach(var key in keys)if(Mathf.Abs(key.value-value)>1e-7f)return curve;
        return AnimationCurve.Linear(keys[0].time,value,keys[keys.Length-1].time,value);
    }
    static void KeepWindupFlat(AnimationClip clip)
    {
        // Unity's automatic quaternion tangents can anticipate the next moving
        // key and subtly drift away from Charge before the stroke begins.
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve=AnimationUtility.GetEditorCurve(clip,binding);bool changed=false;
            for(int i=0;i<curve.length&&curve[i].time<=.06501f;i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Free);
                var key=curve[i];key.inTangent=key.outTangent=0;curve.MoveKey(i,key);changed=true;
            }
            if(changed)AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
    }
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    [MenuItem("Tools/Player/Scout/Build visual assets and animations")]
    public static void Build() { BuildAssets(false); }
    [MenuItem("Tools/Player/Scout/Rebuild dual-hook flight poses only")]
    public static void RebuildFlight() { BuildAssets(true); }
    [MenuItem("Tools/Player/Scout/Rebuild fall pose only")]
    public static void RebuildFall() { BuildAssets(false,true); }
    [MenuItem("Tools/Player/Scout/Rebuild mirrored attack poses only")]
    public static void RebuildAttacks() { BuildAssets(false,false,true); }
    static void BuildAssets(bool flightOnly,bool fallOnly=false,bool attackOnly=false)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first");
        bool poseOnly=flightOnly||fallOnly||attackOnly;
        AssetDatabase.Refresh();Material material=null;
        if(!poseOnly)
        {
        Folder(Anim);Folder("Assets/Prefabs/Player");
        foreach(string file in new[]{"Scout_BaseColor","Scout_MetallicSmoothness","Scout_Normal"})
        {
            var t=(TextureImporter)AssetImporter.GetAtPath(Root+"/Textures/"+file+".png");t.maxTextureSize=1024;t.sRGBTexture=file=="Scout_BaseColor";t.alphaSource=TextureImporterAlphaSource.FromInput;
            t.textureType=file=="Scout_Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;t.SaveAndReimport();
        }
        material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Scout_PBR.mat");if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,Root+"/Scout_PBR.mat");}
        material.shader=Shader.Find(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null?"Universal Render Pipeline/Lit":"Standard");
        var baseMap=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Scout_BaseColor.png");
        material.SetTexture("_MainTex",baseMap);material.SetTexture("_BaseMap",baseMap);material.SetColor("_Color",Color.white);material.SetColor("_BaseColor",Color.white);
        material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Scout_MetallicSmoothness.png"));material.SetFloat("_GlossMapScale",1);material.SetFloat("_SmoothnessTextureChannel",0);material.EnableKeyword("_METALLICGLOSSMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");material.SetFloat("_Smoothness",1);
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Scout_Normal.png"));material.SetFloat("_BumpScale",.4f);material.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(material);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/ScoutPlayer.fbx");importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=false;importer.isReadable=false;importer.preserveHierarchy=true;importer.SaveAndReimport();
        }
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ScoutPlayer.fbx");var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);
        try
        {
            go.name="ScoutModel";PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            if(!poseOnly)foreach(var r in go.GetComponentsInChildren<Renderer>()){r.sharedMaterial=material;var sk=r as SkinnedMeshRenderer;if(sk!=null)sk.updateWhenOffscreen=true;}
            var animator=go.GetComponent<Animator>();if(animator==null)animator=go.AddComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var bones=new Dictionary<string,Transform>();foreach(var t in go.GetComponentsInChildren<Transform>())bones[t.name]=t;
            Vector3 forward=(bones["ScoutForward"].position-bones["ScoutRig"].position).normalized,up=(bones["ScoutUp"].position-bones["ScoutRig"].position).normalized,right=Vector3.Cross(up,forward).normalized;
            // Signed roll definition assumes anatomical Left is on model -right.
            float leftSide=Vector3.Dot(bones["LeftHand"].position-bones["Chest"].position,right);if(leftSide>0)throw new InvalidOperationException("Unexpected model handedness: inspect FBX axes before authoring poses");
            string controllerPath=Anim+"/ScoutPlayer.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(poseOnly&&controller==null)throw new InvalidOperationException("Build the initial Scout controller first");
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach(var s in controller.layers[0].stateMachine.states)
                if(!poseOnly||(flightOnly&&(s.state.name=="ReleaseLeft"||s.state.name=="ReleaseRight")))controller.layers[0].stateMachine.RemoveState(s.state);
            if(!poseOnly){controller.parameters=new AnimatorControllerParameter[0];foreach(string param in new[]{"isRunning","isFalling","isGasing","holdingAttack"})controller.AddParameter(param,AnimatorControllerParameterType.Bool);}
            foreach(string state in States)
            {
                if(flightOnly&&state!="GasBoth"&&state!="AttachedBoth")continue;
                if(fallOnly&&state!="Fall")continue;
                if(attackOnly&&!state.StartsWith("Attack")&&!state.StartsWith("Charge"))continue;
                float length=state=="Run"?ScoutPlayerAnimation.RunCycleDuration:state.StartsWith("Attack")?.39f:state=="ReleaseFlip"?.48f:state.StartsWith("Release")?.22f:state=="Land"?.12f:1.2f;
                bool loop=!(state.StartsWith("Attack")||state.StartsWith("Release")||state=="Land");
                var clip=new AnimationClip {name=state,frameRate=60};int frames=Mathf.CeilToInt(length*60);
                foreach(var pair in bones)
                {
                    var t=pair.Value;if(t==go.transform||t.name.StartsWith("Scout")||t.GetComponent<Renderer>()!=null)continue;
                    string path=AnimationUtility.CalculateTransformPath(t,go.transform);Quaternion rest=t.localRotation,parent=t.parent!=null?t.parent.rotation:Quaternion.identity;
                    var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                    for(int i=0;i<=frames;i++)
                    {
                        float time=length*i/frames;var pose=Sample(state,time,length);Vector3 a;pose.angles.TryGetValue(t.name,out a);
                        Quaternion delta=Quaternion.AngleAxis(a.y,up)*Quaternion.AngleAxis(a.x,right)*Quaternion.AngleAxis(a.z,forward);
                        Quaternion q=Quaternion.Inverse(parent)*delta*parent*rest;
                        for(int k=0;k<4;k++)curves[k].AddKey(time,q[k]);
                    }
                    for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]),CompactConstant(curves[k]));
                    if(t.name=="Hips")for(int k=0;k<3;k++)
                    {
                        var curve=new AnimationCurve();for(int i=0;i<=frames;i++){float time=length*i/frames;Vector3 offset=Sample(state,time,length).hip;Vector3 worldOffset=right*offset.x+up*offset.y+forward*offset.z;Vector3 local=t.parent.InverseTransformVector(worldOffset);curve.AddKey(time,t.localPosition[k]+local[k]);}
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalPosition."+"xyz"[k]),CompactConstant(curve));
                    }
                }
                clip.EnsureQuaternionContinuity();if(state.StartsWith("Attack"))KeepWindupFlat(clip);var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
                string clipPath=Anim+"/"+state+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(existing==null)AssetDatabase.CreateAsset(clip,clipPath);else{EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;}
                AnimatorState st=null;foreach(var candidate in controller.layers[0].stateMachine.states)if(candidate.state.name==state){st=candidate.state;break;}
                if(st==null)st=controller.layers[0].stateMachine.AddState(state);st.motion=clip;st.writeDefaultValues=true;if(state=="Idle")controller.layers[0].stateMachine.defaultState=st;
                if(poseOnly)AssetDatabase.SaveAssetIfDirty(clip);
            }
            EditorUtility.SetDirty(controller);
            if(poseOnly)AssetDatabase.SaveAssetIfDirty(controller);
            else {animator.runtimeAnimatorController=controller;PrefabUtility.SaveAsPrefabAsset(go,Prefab);AssetDatabase.SaveAssets();}
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/Player/Scout/Install on current gameplay player")]
    public static void Install()
    {
        var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlayingOrWillChangePlaymode||scene.isDirty||(scene.path!="Assets/Scenes/FreeRoam.unity"&&scene.path!="Assets/Scenes/FreeRoam (Shiganshina.unity"))throw new InvalidOperationException("Open a saved gameplay scene in Edit Mode");
        Movement player=null;foreach(var root in scene.GetRootGameObjects()){var p=root.GetComponentInChildren<Movement>();if(p!=null){if(player!=null)throw new InvalidOperationException("Ambiguous player");player=p;}}
        if(player==null||player.GetComponent<ScoutPlayerAnimation>()!=null)throw new InvalidOperationException("Missing player / already installed");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);if(prefab==null)throw new InvalidOperationException("Build Scout assets first");
        var gear=player.GetComponent<ODM_Movement>();var attack=player.GetComponent<Attack>();var sweep=player.GetComponentInChildren<TitanODMSwordSweep>(true);
        if(gear==null||attack==null||sweep==null)throw new InvalidOperationException("Install existing real ODM/Titan sword integration first");
        Directory.CreateDirectory("Library/ScoutBackup");string backup="Library/ScoutBackup/"+Path.GetFileName(scene.path);if(!File.Exists(backup))File.Copy(scene.path,backup);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install Scout visual and fast full-body animation");
        try
        {
            var driver=Undo.AddComponent<ScoutPlayerAnimation>(player.gameObject);
            driver.originalMovementAnimator=player.visualAnimator;driver.originalGearAnimator=gear.visualAnimator;driver.originalAttackAnimator=attack.armatureAnimator;
            driver.originalShooters=(Transform[])gear.raycastShooters.Clone();driver.originalBlades=(Transform[])sweep.bladeMeshes.Clone();driver.originalStrikeDelay=attack.strikeDelay;driver.originalActiveDuration=attack.activeDuration;driver.originalRecoveryDuration=attack.recoveryDuration;
            var hidden=new List<Renderer>();foreach(var r in player.GetComponentsInChildren<Renderer>(true))if(r.enabled&&(r is MeshRenderer||r is SkinnedMeshRenderer||(r is TrailRenderer&&r.transform.IsChildOf(player.transform.Find("CharacterAnimated")))))hidden.Add(r);
            var animators=new List<Animator>();foreach(var a in player.GetComponentsInChildren<Animator>(true))if(a.enabled)animators.Add(a);
            driver.hiddenRenderers=hidden.ToArray();driver.stoppedAnimators=animators.ToArray();
            foreach(var r in hidden){Undo.RecordObject(r,"Hide legacy mesh, keep physics");r.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            foreach(var a in animators){Undo.RecordObject(a,"Stop legacy bone writer");a.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(a);}
            var pivot=new GameObject("Scout visual flip pivot");Undo.RegisterCreatedObjectUndo(pivot,"Scout pivot");pivot.transform.SetParent(player.transform,false);driver.flipPivot=pivot.transform;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);Undo.RegisterCreatedObjectUndo(model,"Scout model");model.transform.SetParent(pivot.transform,false);model.transform.localPosition=new Vector3(0,-player.controller.height/2,0);model.transform.localScale=Vector3.one*2;driver.model=model.transform;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            foreach(var t in model.GetComponentsInChildren<Transform>()){t.gameObject.layer=player.gameObject.layer;PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
            var dict=new Dictionary<string,Transform>();foreach(var t in model.GetComponentsInChildren<Transform>())dict[t.name]=t;
            driver.animator=model.GetComponent<Animator>();driver.leftUpper=dict["LeftUpperArm"];driver.leftLower=dict["LeftLowerArm"];driver.leftHand=dict["LeftHand"];driver.rightUpper=dict["RightUpperArm"];driver.rightLower=dict["RightLowerArm"];driver.rightHand=dict["RightHand"];
            Undo.RecordObject(player,"Bind explicit visual animator");Undo.RecordObject(gear,"Bind explicit visual animator");Undo.RecordObject(attack,"Fast Scout swing timing");Undo.RecordObject(sweep,"Bind actual Scout blades");
            player.visualAnimator=driver.animator;gear.visualAnimator=driver.animator;attack.armatureAnimator=driver.animator;
            gear.raycastShooters=new[]{dict["ScoutLaunch_R"],dict["ScoutLaunch_L"]};
            attack.strikeDelay=.075f;attack.activeDuration=.12f;attack.recoveryDuration=.195f;
            sweep.bladeMeshes=new[]{dict["ScoutBlade_L"],dict["ScoutBlade_R"]};
            foreach(var changed in new UnityEngine.Object[]{player,gear,attack,sweep})PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            EditorUtility.SetDirty(driver);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(group);
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
    }
    [MenuItem("Tools/Player/Scout/Restore legacy player visuals")]
    public static void Restore()
    {
        var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlayingOrWillChangePlaymode||scene.isDirty)throw new InvalidOperationException("Save scene in Edit Mode first");
        var drivers=new List<ScoutPlayerAnimation>();foreach(var root in scene.GetRootGameObjects())drivers.AddRange(root.GetComponentsInChildren<ScoutPlayerAnimation>());
        if(drivers.Count!=1)throw new InvalidOperationException("Expected one Scout player");var d=drivers[0];
        var move=d.GetComponent<Movement>();var gear=d.GetComponent<ODM_Movement>();var attack=d.GetComponent<Attack>();var sweep=d.GetComponentInChildren<TitanODMSwordSweep>(true);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Restore original player visuals");
        foreach(var r in d.hiddenRenderers)if(r!=null){Undo.RecordObject(r,"Show original mesh");r.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
        foreach(var a in d.stoppedAnimators)if(a!=null){Undo.RecordObject(a,"Restore original animator");a.enabled=true;PrefabUtility.RecordPrefabInstancePropertyModifications(a);}
        Undo.RecordObject(move,"Original visual binding");Undo.RecordObject(gear,"Original visual binding");Undo.RecordObject(attack,"Original attack timing");Undo.RecordObject(sweep,"Original blades");
        move.visualAnimator=d.originalMovementAnimator;gear.visualAnimator=d.originalGearAnimator;attack.armatureAnimator=d.originalAttackAnimator;
        if(d.originalShooters!=null&&d.originalShooters.Length==2)gear.raycastShooters=d.originalShooters;
        attack.strikeDelay=d.originalStrikeDelay;attack.activeDuration=d.originalActiveDuration;attack.recoveryDuration=d.originalRecoveryDuration;sweep.bladeMeshes=d.originalBlades;
        foreach(var changed in new UnityEngine.Object[]{move,gear,attack,sweep})PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
        if(d.flipPivot!=null)Undo.DestroyObjectImmediate(d.flipPivot.gameObject);Undo.DestroyObjectImmediate(d);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(group);
    }
}
