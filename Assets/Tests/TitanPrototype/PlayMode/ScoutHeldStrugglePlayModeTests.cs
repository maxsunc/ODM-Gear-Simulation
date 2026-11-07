#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public class ScoutHeldStrugglePlayModeTests
{
    const string Root="Assets/Art/Player/Scout/Animations/";
    [Test]public void HeldLoopMovesRealSkinAndRestrainedLimbsWithoutTranslatingOrScalingAnyBone()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;var mesh=new Mesh();
        try
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Held.anim");Assert.That(clip.length,Is.EqualTo(1.2f).Within(.001f));Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime,Is.True);
            var bones=go.GetComponentsInChildren<Transform>();var by=bones.ToDictionary(t=>t.name);clip.SampleAnimation(go,0);var positions=bones.Select(t=>t.localPosition).ToArray();var scales=bones.Select(t=>t.localScale).ToArray();var first=bones.Select(t=>t.localRotation).ToArray();
            var skin=go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="ScoutBody");skin.BakeMesh(mesh);var vertices=mesh.vertices;float left=0,right=0,head=0;
            for(int k=1;k<=72;k++)
            {
                clip.SampleAnimation(go,clip.length*k/72);for(int i=0;i<bones.Length;i++){Assert.That(Vector3.Distance(positions[i],bones[i].localPosition),Is.LessThan(.00001f),bones[i].name);Assert.That(Vector3.Distance(scales[i],bones[i].localScale),Is.LessThan(.00001f),bones[i].name);}
                left=Mathf.Max(left,Quaternion.Angle(first[System.Array.IndexOf(bones,by["LeftUpperLeg"])],by["LeftUpperLeg"].localRotation));right=Mathf.Max(right,Quaternion.Angle(first[System.Array.IndexOf(bones,by["RightUpperLeg"])],by["RightUpperLeg"].localRotation));head=Mathf.Max(head,Quaternion.Angle(first[System.Array.IndexOf(bones,by["Head"])],by["Head"].localRotation));
                foreach(string side in new[]{"Left","Right"}){var u=by[side+"UpperArm"];var l=by[side+"LowerArm"];var h=by[side+"Hand"];Assert.That(Vector3.Angle(l.position-u.position,h.position-l.position),Is.GreaterThan(45));Assert.That(Vector3.Distance(h.position,by["Chest"].position),Is.LessThan(.65f));}
            }
            Assert.That(left,Is.GreaterThan(5));Assert.That(right,Is.GreaterThan(5));Assert.That(head,Is.GreaterThan(8));clip.SampleAnimation(go,.3f);skin.BakeMesh(mesh);Assert.That(vertices.Zip(mesh.vertices,(a,b)=>Vector3.Distance(a,b)).Max(),Is.GreaterThan(.025f));Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));Assert.That(go.transform.rotation,Is.EqualTo(Quaternion.identity));
            foreach(var binding in AnimationUtility.GetCurveBindings(clip)){Assert.That(binding.path,Is.Not.Empty.And.Not.EqualTo("ScoutRig"));Assert.That(binding.propertyName,Does.Not.Contain("Scale"));}
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(mesh);}
    }
    [Test]public void HeldLoopHasNoSeamSnapAt120HzAndDoesNotModifyTheModelRoot()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Held.anim");var bones=go.GetComponentsInChildren<Transform>();clip.SampleAnimation(go,0);var start=bones.Select(t=>t.localRotation).ToArray();var previous=start;float peak=0;
            for(int k=1;k<=144;k++){clip.SampleAnimation(go,clip.length*k/144);for(int i=0;i<bones.Length;i++)peak=Mathf.Max(peak,Quaternion.Angle(previous[i],bones[i].localRotation));previous=bones.Select(t=>t.localRotation).ToArray();}
            for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(start[i],previous[i]),Is.LessThan(.02f));Assert.That(peak,Is.LessThan(2));Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));
        }
        finally{Object.DestroyImmediate(go);}
    }
    [Test]public void ExistingControllerStillUsesHeldClipWithNoRootMotionOrAttackStateReplacement()
    {
        var controller=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Root+"ScoutPlayer.controller");var state=controller.layers[0].stateMachine.states.Single(s=>s.state.name=="Held").state;Assert.That(state.motion,Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Held.anim")));
        foreach(string name in new[]{"Idle","Run","Fall","AttackGround","AttackAir","Dead"})Assert.That(controller.layers[0].stateMachine.states.Count(s=>s.state.name==name),Is.EqualTo(1));
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab").GetComponent<Animator>().applyRootMotion,Is.False);
    }
}
#endif
