using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Reproducible Generic key-pose clips. Contact/recovery have different timing;
/// run is a distinct pose sequence, not a sped-up copy of the walk.</summary>
public static class TitanMotionClipBuilder
{
    public static AnimatorController Build(Animator animator, Dictionary<string,Transform> bones, string folder)
    {
        string path = folder + "/Animations/TitanPrototype.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        // Remove only the generated state machine children/blend trees, not unrelated assets.
        foreach (var state in controller.layers[0].stateMachine.states)
        {
            var oldTree = state.state.motion as BlendTree;
            controller.layers[0].stateMachine.RemoveState(state.state);
            if (oldTree != null && AssetDatabase.GetAssetPath(oldTree) == path) Object.DestroyImmediate(oldTree,true);
        }
        controller.parameters = new[] {
            new AnimatorControllerParameter { name="IsWalking", type=AnimatorControllerParameterType.Bool },
            new AnimatorControllerParameter { name="Gait", type=AnimatorControllerParameterType.Float },
            new AnimatorControllerParameter { name="StrideRate", type=AnimatorControllerParameterType.Float, defaultFloat=1 },
            new AnimatorControllerParameter { name="CycleOffset", type=AnimatorControllerParameterType.Float }
        };
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle"); idle.motion = Clip("Idle", animator.transform,bones,folder);
        idle.cycleOffsetParameterActive = true; idle.cycleOffsetParameter = "CycleOffset";
        var locomotion = machine.AddState("Locomotion");
        var tree = new BlendTree { name="Titan Walk Run", blendType=BlendTreeType.Simple1D, blendParameter="Gait", useAutomaticThresholds=false };
        AssetDatabase.AddObjectToAsset(tree,controller);
        tree.AddChild(Clip("Walk",animator.transform,bones,folder),0);
        tree.AddChild(Clip("Run",animator.transform,bones,folder),1);
        locomotion.motion = tree;
        locomotion.speedParameterActive = true; locomotion.speedParameter = "StrideRate";
        locomotion.cycleOffsetParameterActive = true; locomotion.cycleOffsetParameter = "CycleOffset";
        machine.defaultState = idle;
        var start = idle.AddTransition(locomotion); start.hasExitTime=false; start.hasFixedDuration=true; start.duration=.09f;
        start.AddCondition(AnimatorConditionMode.If,0,"IsWalking");
        var stop = locomotion.AddTransition(idle); stop.hasExitTime=false; stop.hasFixedDuration=true; stop.duration=.10f;
        stop.AddCondition(AnimatorConditionMode.IfNot,0,"IsWalking");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static AnimationClip Clip(string name, Transform root, Dictionary<string,Transform> hierarchy, string folder)
    {
        string path = folder + "/Animations/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip,path); }
        clip.ClearCurves(); clip.name=name; clip.frameRate=60;
        bool moving=name!="Idle", run=name=="Run";
        float duration=run ? .68f : moving ? 1.02f : 2.6f;
        string[] names={"Hips","Spine","Chest","Neck","Head","Jaw","LeftShoulder","LeftUpperArm","LeftLowerArm","LeftHand","RightShoulder","RightUpperArm","RightLowerArm","RightHand","LeftUpperLeg","LeftLowerLeg","LeftFoot","LeftToes","RightUpperLeg","RightLowerLeg","RightFoot","RightToes"};
        var bones=names.Where(hierarchy.ContainsKey).Select(n=>hierarchy[n]).ToArray();
        var rest=bones.ToDictionary(b=>b,b=>b.localRotation);
        var positions=bones.ToDictionary(b=>b,b=>b.localPosition);
        var pitchAxes=bones.ToDictionary(b=>b,b=>b.parent.InverseTransformDirection(root.right));
        var yawAxes=bones.ToDictionary(b=>b,b=>b.parent.InverseTransformDirection(root.up));
        var rollAxes=bones.ToDictionary(b=>b,b=>b.parent.InverseTransformDirection(root.forward));
        var rotations=bones.ToDictionary(b=>b,b=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()});
        var hipCurves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
        var armBases=new Dictionary<Transform,Quaternion>();
        foreach(var bone in bones.Where(b=>b.name.EndsWith("UpperArm")))
        {
            float side=bone.name.StartsWith("Left")?-1:1;
            var lower=hierarchy[bone.name.Replace("UpperArm","LowerArm")];
            Vector3 down=root.TransformDirection(new Vector3(side*.14f,-1,.06f)).normalized;
            Quaternion hanging=Quaternion.FromToRotation(lower.position-bone.position,down)*bone.rotation;
            armBases[bone]=Quaternion.Inverse(bone.parent.rotation)*hanging;
            // Elbow flexion is authored relative to the hanging arm, not the old
            // T-pose parent axes (which would bend the elbows sideways).
            pitchAxes[lower]=Quaternion.Inverse(hanging)*root.right;
        }
        var hips=hierarchy["Hips"];
        // Explicit asymmetric contact/pass/recovery poses, with faster recovery than support.
        var times = run ? new[]{0f,.12f,.34f,.50f,.70f,.88f,1f} : new[]{0f,.12f,.50f,.62f,.78f,.94f,1f};
        var thighs=run ? new[]{-32f,-12f,24f,38f,4f,-29f,-32f} : new[]{-24f,-17f,21f,25f,4f,-21f,-24f};
        var knees=run ? new[]{16f,8f,4f,34f,78f,32f,16f} : new[]{5f,3f,4f,11f,50f,18f,5f};
        var thigh=PoseCurve(times,thighs); var knee=PoseCurve(times,knees);
        try
        {
            for(int frame=0;frame<=64;frame++)
            {
                float t=frame/64f, phase=t*Mathf.PI*2;
                foreach(var bone in bones)
                {
                    bone.localPosition=positions[bone];
                    float side=bone.name.StartsWith("Left") ? -1 : 1;
                    float legPhase=Mathf.Repeat(t+(side>0 ? .5f : 0),1);
                    float legPitch=thigh.Evaluate(legPhase), bend=knee.Evaluate(legPhase);
                    float pitch=0,yaw=0,roll=0;
                    Quaternion basis=rest[bone];
                    if(bone.name.EndsWith("UpperArm"))
                    {
                        basis=armBases[bone];
                        pitch=moving ? -legPitch*(run ? 1.15f : .72f) : Mathf.Sin(phase+(side<0?.2f:.7f))*2.2f;
                        roll=side*(run ? 7 : 2);
                    }
                    else if(bone.name.EndsWith("LowerArm")) pitch=moving ? (run ? -48 : -10) + Mathf.Sin(phase+(side>0?Mathf.PI:0))*(run?18:5) : -7;
                    else if(bone.name.EndsWith("UpperLeg") && moving) { pitch=legPitch; roll=side*(run?2:1); }
                    else if(bone.name.EndsWith("LowerLeg") && moving) pitch=bend;
                    else if((bone.name.EndsWith("Foot") || bone.name.EndsWith("Toes")) && moving)
                        pitch=bone.name.EndsWith("Foot") ? -(legPitch+bend)*.88f + Mathf.Sin(legPhase*Mathf.PI*2)*6 : Mathf.Max(0,legPitch)*.18f;
                    else if(bone.name=="Hips") { yaw=moving ? Mathf.Sin(phase)*(run?5:3) : 0; roll=moving ? Mathf.Cos(phase)*(run?2:1.5f) : 0; }
                    else if(bone.name=="Spine") { pitch=moving ? (run?9:3) : 1; yaw=moving ? -Mathf.Sin(phase)*(run?7:4) : 0; }
                    else if(bone.name=="Chest") { pitch=(run?7:1)+Mathf.Sin(phase*2)*(moving?1.5f:1.1f); yaw=moving ? -Mathf.Sin(phase+.15f)*(run?8:4) : Mathf.Sin(phase)*1.4f; }
                    else if(bone.name=="Neck") pitch=run ? -7 : -1;
                    else if(bone.name=="Head") { pitch=run ? -5 : Mathf.Sin(phase+.5f)*1.5f; yaw=!moving ? Mathf.Sin(phase+.6f)*3 : 0; }
                    else if(bone.name.EndsWith("Shoulder")) roll=moving ? side*Mathf.Sin(phase+(side>0?Mathf.PI:0))*(run?4:2) : 0;
                    bone.localRotation=Quaternion.AngleAxis(roll,rollAxes[bone])*Quaternion.AngleAxis(yaw,yawAxes[bone])*Quaternion.AngleAxis(pitch,pitchAxes[bone])*basis;
                }
                if(moving)
                {
                    float lowest=Mathf.Min(FootMinimum(hierarchy["LeftFoot"]),FootMinimum(hierarchy["RightFoot"]));
                    float bounce=(run?.13f:.035f)*(1-Mathf.Cos(phase*2))*.5f;
                    hips.position+=root.up*(.035f-lowest+bounce);
                    hips.position+=root.right*Mathf.Sin(phase)*(run?.075f:.045f);
                }
                else hips.position+=root.up*Mathf.Sin(phase)*.012f;
                foreach(var bone in bones)
                {
                    Quaternion q=bone.localRotation;
                    var c=rotations[bone];
                    if(frame>0)
                    {
                        var previous=new Quaternion(c[0].keys[frame-1].value,c[1].keys[frame-1].value,c[2].keys[frame-1].value,c[3].keys[frame-1].value);
                        if(Quaternion.Dot(previous,q)<0) q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                    }
                    c[0].AddKey(t*duration,q.x);c[1].AddKey(t*duration,q.y);c[2].AddKey(t*duration,q.z);c[3].AddKey(t*duration,q.w);
                }
                hipCurves[0].AddKey(t*duration,hips.localPosition.x);hipCurves[1].AddKey(t*duration,hips.localPosition.y);hipCurves[2].AddKey(t*duration,hips.localPosition.z);
                // Always start the next sample from the imported rest hierarchy.
                foreach(var bone in bones) { bone.localRotation=rest[bone];bone.localPosition=positions[bone]; }
            }
        }
        finally { foreach(var bone in bones) { bone.localRotation=rest[bone];bone.localPosition=positions[bone]; } }
        string[] components={"x","y","z","w"};
        foreach(var bone in bones)
        {
            string bonePath=AnimationUtility.CalculateTransformPath(bone,root);
            for(int channel=0;channel<4;channel++) clip.SetCurve(bonePath,typeof(Transform),"m_LocalRotation."+components[channel],rotations[bone][channel]);
        }
        for(int channel=0;channel<3;channel++) clip.SetCurve(AnimationUtility.CalculateTransformPath(hips,root),typeof(Transform),"m_LocalPosition."+components[channel],hipCurves[channel]);
        clip.EnsureQuaternionContinuity();
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimationCurve PoseCurve(float[] times,float[] values)
    {
        var curve=new AnimationCurve();
        for(int i=0;i<times.Length;i++) curve.AddKey(times[i],values[i]);
        for(int i=0;i<curve.length;i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }
    static float FootMinimum(Transform foot)
    {
        var box=foot.GetComponentsInChildren<BoxCollider>().First(c=>!c.isTrigger && c.attachedRigidbody==foot.GetComponent<Rigidbody>());
        float min=float.PositiveInfinity;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            min=Mathf.Min(min,box.transform.TransformPoint(box.center+Vector3.Scale(box.size*.5f,new Vector3(x,y,z))).y);
        return min;
    }
}
