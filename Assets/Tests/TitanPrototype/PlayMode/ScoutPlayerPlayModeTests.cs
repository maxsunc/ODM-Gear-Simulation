#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class ScoutPlayerPlayModeTests
{
    static Type Real(string name) { return AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="Assembly-CSharp").GetType(name,true); }
    static void Set(object o,string n,object v) { o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(o,v); }
    static T Prop<T>(object o,string n) { return (T)o.GetType().GetProperty(n).GetValue(o); }
    sealed class Brain
    {
        public object brain=Activator.CreateInstance(Real("ScoutAnimationBrain"));
        public object frame=Activator.CreateInstance(Real("ScoutAnimationBrain+Frame"));
        public Brain() { Set(frame,"speed",20f);Set(frame,"flipClear",true); }
        public Brain F(string n,object value) { Set(frame,n,value);return this; }
        public string Step(float dt=.02f) { return (string)brain.GetType().GetMethod("Step").Invoke(brain,new[]{frame,(object)dt}); }
    }
    [Test]public void LeftRightDualGasHaveSeparateStates()
    { var b=new Brain().F("gas",true);Assert.That(b.F("hooks",1).Step(),Is.EqualTo("GasLeft"));Assert.That(b.F("hooks",2).Step(),Is.EqualTo("GasRight"));Assert.That(b.F("hooks",3).Step(),Is.EqualTo("GasBoth")); }
    [Test]public void ReleasingGasWithoutDetachingDoesNotFlipOrFall()
    { var b=new Brain().F("hooks",3).F("gas",true);b.Step();Assert.That(b.F("gas",false).Step(),Is.EqualTo("AttachedBoth")); }
    [Test]public void DetachingOneHookKeepsRemainingHookPose()
    { var b=new Brain().F("hooks",3).F("gas",true);b.Step();Assert.That(b.F("hooks",2).Step(),Is.EqualTo("GasRight")); }
    [Test]public void FullDualReleaseFlipsThenFalls()
    {
        var b=new Brain().F("hooks",3).F("vertical",-10f);b.Step();Assert.That(b.F("hooks",0).Step(),Is.EqualTo("ReleaseFlip"));
        for(int i=0;i<26;i++)b.Step();Assert.That(b.Step(),Is.EqualTo("Fall"));
    }
    [Test]public void NearSimultaneousDualReleaseRecognizesTwoInputFrames()
    { var b=new Brain().F("hooks",3);b.Step();b.F("hooks",1).Step();Assert.That(b.F("hooks",0).Step(),Is.EqualTo("ReleaseFlip")); }
    [Test]public void BlockedOrSlowReleaseUsesNonFlippingVariant()
    {
        var b=new Brain().F("hooks",3).F("flipClear",false);b.Step();Assert.That(b.F("hooks",0).Step(),Is.EqualTo("ReleaseBoth"));
        b=new Brain().F("hooks",3).F("speed",2f);b.Step();Assert.That(b.F("hooks",0).Step(),Is.EqualTo("ReleaseBoth"));
    }
    [Test]public void RegrappleLandingAttackAndObstructionInterruptFlip()
    {
        var b=new Brain().F("hooks",3);b.Step();b.F("hooks",0).Step();Assert.That(b.F("hooks",1).Step(),Is.EqualTo("AttachedLeft"));
        b.F("hooks",0).Step();Assert.That(b.F("grounded",true).Step(),Is.EqualTo("Land"));
        Assert.That(b.F("attacking",true).Step(),Is.EqualTo("AttackGround"));
        b=new Brain().F("hooks",3);b.Step();b.F("hooks",0).Step();Assert.That(b.F("flipClear",false).F("vertical",-5f).Step(),Is.EqualTo("Fall"));
    }
    [Test]public void HeldCounterRemainsAvailableButDeathOverridesEverything()
    {
        var b=new Brain().F("held",true);Assert.That(b.Step(),Is.EqualTo("Held"));Assert.That(b.F("charging",true).Step(),Is.EqualTo("ChargeHeld"));
        Assert.That(b.F("charging",false).F("attacking",true).Step(),Is.EqualTo("AttackHeld"));Assert.That(b.F("dead",true).Step(),Is.EqualTo("Dead"));
    }
    const string Assets="Assets/Art/Player/Scout/Animations/";
    [UnityTest]public IEnumerator AuthoredSwingMovesLegsAndSkinWithoutMovingGameplayRoot()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab");Assert.That(prefab,Is.Not.Null);
        var go=Object.Instantiate(prefab);go.GetComponent<Animator>().enabled=false;
        var skin=go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="ScoutBody");var mesh=new Mesh();
        try
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"AttackAir.anim");Assert.That(clip.length,Is.EqualTo(.39f).Within(.001f));
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))Assert.That(binding.path,Is.Not.EqualTo("").And.Not.EqualTo("ScoutRig"));
            var leg=go.GetComponentsInChildren<Transform>().First(t=>t.name=="LeftUpperLeg");
            clip.SampleAnimation(go,0);Quaternion first=leg.rotation;skin.BakeMesh(mesh);Vector3[] before=mesh.vertices;
            clip.SampleAnimation(go,.135f);Assert.That(Quaternion.Angle(first,leg.rotation),Is.GreaterThan(25));skin.BakeMesh(mesh);var after=mesh.vertices;
            Assert.That(before.Zip(after,(a,b)=>Vector3.Distance(a,b)).Max(),Is.GreaterThan(.05f),"Skin, not only detached swords, must deform");
            Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));Assert.That(Quaternion.Angle(go.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
            Assert.That(skin.bones.Length,Is.EqualTo(43));
            var blades=go.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("ScoutBlade_")).ToArray();Assert.That(blades.Length,Is.EqualTo(2));
            foreach(var blade in blades)Assert.That(blade.transform.GetComponentsInParent<Transform>().Any(t=>t.name.EndsWith("Hand")),Is.True);
        }
        finally { Object.Destroy(go);Object.Destroy(mesh); }
        yield return null;
    }
    [TestCase("Ground")][TestCase("Air")][TestCase("Held")]
    public void MirroredSlashesCurlBehindNeckTraceAnArcAndFinishWithBothArmsExtended(string context)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var b=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);Vector3 forwardAxis=b["Chest"].InverseTransformDirection(Vector3.forward);
            foreach(string suffix in new[]{"","_Right"})
            {
                var charge=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Charge"+context+suffix+".anim");var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Attack"+context+suffix+".anim");
                Assert.That(charge,Is.Not.Null);Assert.That(clip.length,Is.EqualTo(.39f).Within(.001f));charge.SampleAnimation(go,0);float deepestFold=0;
                foreach(string side in new[]{"Left","Right"})
                {
                    var u=b[side+"UpperArm"];var l=b[side+"LowerArm"];var h=b[side+"Hand"];
                    float fold=Vector3.Angle(l.position-u.position,h.position-l.position);deepestFold=Mathf.Max(deepestFold,fold);
                    Assert.That(fold,Is.GreaterThan(55f),"The crossing arm stays bent while reaching the same loaded shoulder");
                    Assert.That(Vector3.Dot(h.position-b["Neck"].position,b["Chest"].TransformDirection(forwardAxis)),Is.LessThan(-.05f),"Hands chamber behind the neck/shoulder plane");
                    Assert.That(Vector3.Distance(h.position,b["Neck"].position),Is.LessThan(.45f));
                }
                Assert.That(deepestFold,Is.GreaterThan(110f),"The near arm remains deeply curled behind the neck");
                foreach(var blade in go.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("ScoutBlade_")))
                {
                    Vector3 a,start,end;clip.SampleAnimation(go,.065f);BladeEndpoints(blade,out a,out start);clip.SampleAnimation(go,.185f);BladeEndpoints(blade,out a,out end);
                    float deviation=0;for(int i=0;i<=24;i++){clip.SampleAnimation(go,.065f+.12f*i/24);Vector3 tip;BladeEndpoints(blade,out a,out tip);deviation=Mathf.Max(deviation,Vector3.Cross(tip-start,end-start).magnitude/(end-start).magnitude);}
                    Assert.That(deviation,Is.GreaterThan(.35f),"Actual cutting steel travels a curved arc, not a straight thrust");
                }
                foreach(float t in new[]{.20f,.30f,.39f})
                {
                    clip.SampleAnimation(go,t);
                    foreach(string side in new[]{"Left","Right"})
                    {
                        Vector3 u=b[side+"UpperArm"].position,l=b[side+"LowerArm"].position,h=b[side+"Hand"].position;
                        Assert.That(Vector3.Distance(u,h)/(Vector3.Distance(u,l)+Vector3.Distance(l,h)),Is.GreaterThan(.995f),"Both arms stay fully extended through the finish");
                        Assert.That((h-u).normalized.x*(suffix==""?1:-1),Is.GreaterThan(.5f));
                    }
                }
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))Assert.That(binding.path,Is.Not.EqualTo("").And.Not.EqualTo("ScoutRig"));
            }
            Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));Assert.That(Quaternion.Angle(go.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
        }
        finally{Object.DestroyImmediate(go);}
    }
    [TestCase("Ground")][TestCase("Air")][TestCase("Held")]
    public void BothHandsLoadOppositeTheSwingAndChargeMatchesTheReleasedSlash(string context)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var bones=go.GetComponentsInChildren<Transform>();var b=bones.ToDictionary(t=>t.name);
            foreach(string suffix in new[]{"","_Right"})
            {
                float swingSign=suffix==""?1:-1;
                var charge=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Charge"+context+suffix+".anim");var cut=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Attack"+context+suffix+".anim");
                charge.SampleAnimation(go,.6f);var rotations=bones.Select(t=>t.localRotation).ToArray();var positions=bones.Select(t=>t.localPosition).ToArray();
                foreach(string hand in new[]{"LeftHand","RightHand"})Assert.That((b[hand].position.x-b["Neck"].position.x)*swingSign,Is.LessThan(-.08f),"BOTH hands load the opposite shoulder; don't merely mirror a symmetric behind-head pose");
                foreach(float time in new[]{0f,.03f,.06f})
                {
                    cut.SampleAnimation(go,time);
                    for(int i=0;i<bones.Length;i++){Assert.That(Quaternion.Angle(rotations[i],bones[i].localRotation),Is.LessThan(.1f),bones[i].name+" at "+time+": releasing must start from the matching charge pose");Assert.That(Vector3.Distance(positions[i],bones[i].localPosition),Is.LessThan(.0001f));}
                }
                cut.SampleAnimation(go,.10f);
                foreach(string hand in new[]{"LeftHand","RightHand"})Assert.That((b[hand].position.x-b["Neck"].position.x)*swingSign,Is.LessThan(-.03f),"The early arc retains its loaded side rather than resetting to a symmetric pose");
                cut.SampleAnimation(go,.20f);
                foreach(string hand in new[]{"LeftHand","RightHand"})Assert.That((b[hand].position.x-b["Neck"].position.x)*swingSign,Is.GreaterThan(.2f),"Both arms finish on the other side after the slash");
            }
        }
        finally{Object.DestroyImmediate(go);}
    }
    [Test]public void LeftAndRightAttackClipsMirrorTheWholeBodyNotJustSwapOneArm()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab");var left=Object.Instantiate(prefab);var right=Object.Instantiate(prefab);
        left.GetComponent<Animator>().enabled=false;right.GetComponent<Animator>().enabled=false;
        try
        {
            var a=left.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);var b=right.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
            foreach(string context in new[]{"Ground","Air","Held"})foreach(float time in new[]{0f,.075f,.10f,.14f,.185f,.39f})
            {
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Attack"+context+".anim").SampleAnimation(left,time);
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Attack"+context+"_Right.anim").SampleAnimation(right,time);
                foreach(string name in new[]{"Hips","Chest","Head","LeftUpperArm","LeftLowerArm","LeftHand","RightUpperArm","RightLowerArm","RightHand","LeftFoot","RightFoot"})
                {
                    string mirror=name.StartsWith("Left")?"Right"+name.Substring(4):name.StartsWith("Right")?"Left"+name.Substring(5):name;
                    Vector3 p=a[name].position;Assert.That(Vector3.Distance(new Vector3(-p.x,p.y,p.z),b[mirror].position),Is.LessThan(.005f),name+" must reflect with torso/legs");
                }
            }
        }
        finally{Object.DestroyImmediate(left);Object.DestroyImmediate(right);}
    }
    [Test]public void AttackSideIsRandomizedOnceForTheWholeChargeAndSlashWithoutUsingGlobalRng()
    {
        var go=new GameObject("Owned cosmetic attack variation");go.SetActive(false);
        try
        {
            var driver=go.AddComponent(Real("ScoutPlayerAnimation"));Set(driver,"attackRandom",new System.Random(1729));
            var choose=driver.GetType().GetMethod("OnChargeStarted",BindingFlags.Instance|BindingFlags.NonPublic);var resolve=driver.GetType().GetMethod("AttackAnimationState");
            var global=UnityEngine.Random.state;int right=0,repeats=0;bool last=false;
            for(int i=0;i<128;i++)
            {
                choose.Invoke(driver,null);bool mirrored=Prop<bool>(driver,"MirroredAttack");if(mirrored)right++;if(i>0&&mirrored==last)repeats++;last=mirrored;
                foreach(string state in new[]{"ChargeGround","ChargeAir","ChargeHeld","AttackGround","AttackAir","AttackHeld"})
                    Assert.That((string)resolve.Invoke(driver,new object[]{state}),Is.EqualTo(state+(mirrored?"_Right":"")));
                Assert.That((string)resolve.Invoke(driver,new object[]{"Fall"}),Is.EqualTo("Fall"));Assert.That(Prop<bool>(driver,"MirroredAttack"),Is.EqualTo(mirrored),"Do not reroll during windup/release");
            }
            Assert.That(right,Is.InRange(40,88));Assert.That(repeats,Is.GreaterThan(10),"Random variation, not forced alternation");Assert.That(UnityEngine.Random.state,Is.EqualTo(global));
        }
        finally{Object.DestroyImmediate(go);}
    }
    [UnityTest]public IEnumerator FastAttackUsesSeparateAnticipationCutRecoveryAndDisableClosesWindow()
    {
        var player=new GameObject("Scout attack timing");var attack=(MonoBehaviour)player.AddComponent(Real("Attack"));
        var window=new GameObject("Physical damage window");window.transform.SetParent(player.transform);var dealer=window.AddComponent(Real("DamageDealer"));window.SetActive(false);
        Set(attack,"damageDealer",dealer);Set(attack,"strikeDelay",.075f);Set(attack,"activeDuration",.12f);Set(attack,"recoveryDuration",.195f);
        int starts=0;attack.GetType().GetEvent("ChargeStarted").AddEventHandler(attack,(Action)(()=>{starts++;Assert.That(Prop<bool>(attack,"IsCharging"),Is.True);}));
        var routine=(IEnumerator)attack.GetType().GetMethod("ChargeAttack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(attack,null);
        attack.StartCoroutine(routine);Assert.That(starts,Is.EqualTo(1));Assert.That(Prop<bool>(attack,"IsAttacking"),Is.True);Assert.That(window.activeSelf,Is.False);
        yield return new WaitForSeconds(.04f);Assert.That(window.activeSelf,Is.False);
        yield return new WaitForSeconds(.065f);Assert.That(window.activeSelf,Is.True);
        yield return new WaitForSeconds(.14f);Assert.That(window.activeSelf,Is.False);Assert.That(Prop<bool>(attack,"IsAttacking"),Is.True);
        attack.enabled=false;Assert.That(Prop<bool>(attack,"IsAttacking"),Is.False);Assert.That(window.activeSelf,Is.False);Object.Destroy(player);yield return null;
    }
    static void BladeEndpoints(MeshFilter blade,out Vector3 bottom,out Vector3 tip)
    {
        // Imported cutting mesh need not be CPU-readable: its local long axis
        // bounds give the actual tapered blade's base/tip centreline, not hand aim.
        var bounds=blade.sharedMesh.bounds;int axis=bounds.size.x>bounds.size.y?0:1;if(bounds.size.z>bounds.size[axis])axis=2;
        Vector3 a=bounds.center,b=bounds.center;a[axis]-=bounds.extents[axis];b[axis]+=bounds.extents[axis];
        bottom=blade.transform.TransformPoint(a);tip=blade.transform.TransformPoint(b);
        if((bottom-blade.transform.position).sqrMagnitude>(tip-blade.transform.position).sqrMagnitude){var temp=bottom;bottom=tip;tip=temp;}
    }
    [Test]public void FallPointsBothActualSwordsUpCrossingNearTheirTipsAndLooksDown()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var bones=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);var head=bones["Head"];
            Vector3 gaze=head.InverseTransformDirection(go.transform.forward);
            var left=go.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="ScoutBlade_L");var right=go.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="ScoutBlade_R");
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Fall.anim");
            foreach(float time in new[]{0f,.3f,.6f,.9f,clip.length})
            {
                clip.SampleAnimation(go,time);Vector3 lb,lt,rb,rt;BladeEndpoints(left,out lb,out lt);BladeEndpoints(right,out rb,out rt);
                Assert.That(Vector3.Angle(lt-lb,Vector3.up),Is.LessThan(20f));Assert.That(Vector3.Angle(rt-rb,Vector3.up),Is.LessThan(20f),"Actual swords point almost straight overhead, not diagonally sideways");
                Assert.That(Mathf.Min(lb.y,rb.y),Is.GreaterThan(head.position.y+.4f),"Both complete cutting blades clear the head");
                Assert.That(lb.x,Is.LessThan(-.1f));Assert.That(rb.x,Is.GreaterThan(.1f));
                Assert.That(lt.x,Is.GreaterThan(.01f));Assert.That(rt.x,Is.LessThan(-.01f),"Opposite tips cross the centreline slightly");
                // Intersection in front projection; maintain depth separation so
                // crossed-looking steel doesn't actually interpenetrate.
                Vector3 ld=lt-lb,rd=rt-rb,offset=rb-lb;float determinant=ld.x*rd.y-ld.y*rd.x;
                float u=(offset.x*rd.y-offset.y*rd.x)/determinant,v=(offset.x*ld.y-offset.y*ld.x)/determinant;
                Assert.That(u,Is.InRange(.65f,1f));Assert.That(v,Is.InRange(.65f,1f));
                Assert.That(Mathf.Abs((lb+u*ld).z-(rb+v*rd).z),Is.InRange(.02f,.12f));
                Assert.That(head.TransformDirection(gaze).y,Is.LessThan(-.5f),"Gaze points down toward the ground, not up between the swords");
                Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));Assert.That(Quaternion.Angle(go.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
            }
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))Assert.That(binding.path,Is.Not.EqualTo("").And.Not.EqualTo("ScoutRig"));
        }
        finally {Object.DestroyImmediate(go);}
    }
    [Test]public void SingleHookDetachesGoDirectlyToFallWithoutARecoilState()
    {
        foreach(int side in new[]{1,2})foreach(float vertical in new[]{-10f,0f,10f})
        {
            var b=new Brain().F("hooks",side).F("gas",true).F("vertical",vertical);b.Step();
            Assert.That(b.F("hooks",0).Step(),Is.EqualTo("Fall"));
            for(int i=0;i<30;i++)Assert.That(b.Step(),Is.EqualTo("Fall"),"Do not flash Fall for one frame then restart another release/coast animation");
        }
        var controller=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Assets+"ScoutPlayer.controller");
        Assert.That(controller.layers[0].stateMachine.states.Any(s=>s.state.name=="ReleaseLeft"||s.state.name=="ReleaseRight"),Is.False);
    }
    [Test]public void SingleDetachFallStillYieldsToRegrappleLandingAttackAndHold()
    {
        var b=new Brain().F("hooks",1).F("vertical",10f);b.Step();b.F("hooks",0).Step();
        Assert.That(b.F("hooks",2).Step(),Is.EqualTo("AttachedRight"));b.F("hooks",0).Step();
        Assert.That(b.F("attacking",true).Step(),Is.EqualTo("AttackAir"));
        Assert.That(b.F("attacking",false).Step(),Is.EqualTo("Rise"),"Attack clears the special detach-fall presentation, not movement momentum");
        b.F("hooks",1).Step();b.F("hooks",0).Step();Assert.That(b.F("held",true).Step(),Is.EqualTo("Held"));
        Assert.That(b.F("dead",true).Step(),Is.EqualTo("Dead"));
        b=new Brain().F("hooks",1);b.Step();b.F("hooks",0).Step();Assert.That(b.F("grounded",true).Step(),Is.EqualTo("Land"));
    }
    [TestCase("GasBoth")][TestCase("AttachedBoth")]
    public void DualHookFlightReclinesTheWholeBodyWithBothFeetLeading(string state)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var b=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+state+".anim");
            foreach(float time in new[]{0f,.3f,.6f,.9f})
            {
                clip.SampleAnimation(go,time);Vector3 torso=(b["Neck"].position-b["Chest"].position).normalized;
                Assert.That(torso.z,Is.LessThan(-.8f),"Shoulders/head trail behind the hips, not dive toward the anchors");
                Assert.That(torso.y,Is.InRange(.3f,.55f),"Recline around22–28 degrees above horizontal, less flat than the earlier near-supine pose");
                foreach(string side in new[]{"Left","Right"})
                {
                    var foot=b[side+"Foot"];Assert.That(foot.position.z,Is.GreaterThan(b["Hips"].position.z+.65f));
                    Assert.That(foot.position.z-b["Head"].position.z,Is.GreaterThan(1f));
                    float length=Vector3.Distance(b[side+"UpperLeg"].position,b[side+"LowerLeg"].position)+Vector3.Distance(b[side+"LowerLeg"].position,foot.position);
                    Assert.That(Vector3.Distance(b[side+"UpperLeg"].position,foot.position)/length,Is.GreaterThan(.97f),"Both legs lengthen forward instead of a cycling/tucked pose");
                }
                Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));Assert.That(Quaternion.Angle(go.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
            }
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))Assert.That(binding.path,Is.Not.EqualTo("").And.Not.EqualTo("ScoutRig"));
        }
        finally {Object.DestroyImmediate(go);}
    }
    [Test]public void DualHookEntryBlendsSlowlyAndGasTogglesCannotShortenIt()
    {
        var t=Real("ScoutPlayerAnimation");var method=t.GetMethod("PoseTransitionDuration",BindingFlags.Public|BindingFlags.Static);
        Func<string,string,float,float> duration=(a,b,age)=>(float)method.Invoke(null,new object[]{a,b});
        var defer=t.GetMethod("DeferDualPoseSwitch",BindingFlags.Public|BindingFlags.Static);
        Assert.That((bool)defer.Invoke(null,new object[]{"GasBoth","AttachedBoth",.15f}),Is.True);
        Assert.That((bool)defer.Invoke(null,new object[]{"AttachedBoth","GasBoth",.7f}),Is.True);
        Assert.That((bool)defer.Invoke(null,new object[]{"GasBoth","AttachedBoth",.75f}),Is.False);
        Assert.That((bool)defer.Invoke(null,new object[]{"GasBoth","Fall",.1f}),Is.False);
        Assert.That(duration("Fall","GasBoth",1),Is.EqualTo(.75f));
        Assert.That(duration("GasLeft","AttachedBoth",1),Is.EqualTo(.75f));
        Assert.That(duration("GasBoth","AttachedBoth",.15f),Is.EqualTo(.20f),"This switch is deferred until entry finishes");
        Assert.That(duration("AttachedBoth","GasBoth",.7f),Is.EqualTo(.20f));
        Assert.That(duration("GasBoth","AttachedBoth",1),Is.EqualTo(.20f));
        Assert.That(duration("GasBoth","Fall",.1f),Is.EqualTo(.5f));
        Assert.That(duration("GasBoth","Land",.1f),Is.EqualTo(.045f));
    }
    [Test]public void FallBlendDurationIsInspectorTunableWithoutSlowingInterrupts()
    {
        var go=new GameObject("Owned fall blend settings");go.SetActive(false);
        try
        {
            var driver=go.AddComponent(Real("ScoutPlayerAnimation"));var method=driver.GetType().GetMethod("GetPoseTransitionDuration");
            Func<string,float> duration=next=>(float)method.Invoke(driver,new object[]{"GasBoth",next});
            Assert.That(duration("Fall"),Is.EqualTo(.5f));Set(driver,"fallBlendDuration",.85f);Assert.That(duration("Fall"),Is.EqualTo(.85f));
            Set(driver,"fallBlendDuration",0f);Assert.That(duration("Fall"),Is.EqualTo(.05f));
            Assert.That(duration("GasLeft"),Is.EqualTo(.075f));Assert.That(duration("Land"),Is.EqualTo(.045f));
            Assert.That(duration("Held"),Is.EqualTo(.075f));Assert.That(duration("Dead"),Is.EqualTo(.075f));Assert.That(duration("AttackAir"),Is.EqualTo(.075f));
        }
        finally {Object.DestroyImmediate(go);}
    }
    [UnityTest]public IEnumerator FallingBlendsFromPreviousPosesInsteadOfImmediatelyRaisingSwordsAndDroppingHead()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab");var go=Object.Instantiate(prefab);var target=Object.Instantiate(prefab);
        target.GetComponent<Animator>().enabled=false;var animator=go.GetComponent<Animator>();animator.Rebind();animator.Update(0);
        try
        {
            var names=new[]{"Hips","Chest","Neck","Head","LeftUpperArm","LeftLowerArm","LeftHand","RightUpperArm","RightLowerArm","RightHand"};
            var bones=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);var targetBones=target.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Fall.anim").SampleAnimation(target,0);
            var goal=names.Select(n=>targetBones[n].localRotation).ToArray();
            foreach(string previous in new[]{"GasLeft","GasRight","GasBoth","AttachedBoth","Rise","AirCoast","ReleaseBoth","ReleaseFlip"})
            {
                animator.Play(previous,0,previous.StartsWith("Release")?1f:.2f);animator.Update(0);
                var last=names.Select(n=>bones[n].localRotation).ToArray();float startDistance=last.Select((q,i)=>Quaternion.Angle(q,goal[i])).Sum();
                Assert.That(startDistance,Is.GreaterThan(50f),previous+" fixture must differ from Fall");
                animator.CrossFadeInFixedTime("Fall",.5f,0,0);
                for(int step=1;step<=30;step++)
                {
                    animator.Update(.02f);var current=names.Select(n=>bones[n].localRotation).ToArray();
                    for(int i=0;i<names.Length;i++)Assert.That(Quaternion.Angle(last[i],current[i]),Is.LessThan(15f),previous+" -> Fall: no instant bone jump in20ms");
                    if(step==5)Assert.That(current.Select((q,i)=>Quaternion.Angle(q,goal[i])).Sum(),Is.GreaterThan(startDistance*.55f),"Still blending after .1s; don't immediately snap into overhead swords");
                    last=current;
                }
                Assert.That(last.Select((q,i)=>Quaternion.Angle(q,goal[i])).Sum(),Is.LessThan(2f),"Finish reaching the authored Fall pose");
                Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));Assert.That(Quaternion.Angle(go.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
            }
        }
        finally {Object.Destroy(go);Object.Destroy(target);}
        yield return null;
    }
    [UnityTest]public IEnumerator DualHookReclineProgressesWithoutSnappingWhileBothArmsTrackTheirAnchors()
    {
        var go=new GameObject("Owned slow recline and independent aim test");go.SetActive(false);
        foreach(string type in new[]{"Movement","ODM_Movement","Attack"})((Behaviour)go.AddComponent(Real(type))).enabled=false;
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"),go.transform);
        var animator=model.GetComponent<Animator>();var driver=(Behaviour)go.AddComponent(Real("ScoutPlayerAnimation"));driver.enabled=false;Set(driver,"animator",animator);
        var bones=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
        foreach(string side in new[]{"left","right"}){string title=char.ToUpper(side[0])+side.Substring(1);Set(driver,side+"Upper",bones[title+"UpperArm"]);Set(driver,side+"Lower",bones[title+"LowerArm"]);Set(driver,side+"Hand",bones[title+"Hand"]);}
        go.SetActive(true);
        try
        {
            animator.Rebind();animator.Play("Fall",0,0);animator.Update(0);
            var hips=bones["Hips"];Quaternion start=hips.localRotation,previous=start;
            animator.CrossFadeInFixedTime("GasBoth",.75f,0,0);
            var aim=driver.GetType().GetMethod("AimArm",BindingFlags.Instance|BindingFlags.NonPublic);
            bool switched=false;var defer=Real("ScoutPlayerAnimation").GetMethod("DeferDualPoseSwitch",BindingFlags.Public|BindingFlags.Static);
            for(int i=1;i<=55;i++)
            {
                // Gas switches off .24s into the recline. Do not restart/shorten
                // the entering fade; settle first, then blend to the coast variant.
                if(i>=12&&!switched&&!(bool)defer.Invoke(null,new object[]{"GasBoth","AttachedBoth",(i-1)*.02f}))
                {animator.CrossFadeInFixedTime("AttachedBoth",.20f,0,0);switched=true;}
                animator.Update(.02f);
                Assert.That(Quaternion.Angle(previous,hips.localRotation),Is.LessThan(5f),"No instant body flip in a20ms animation step");previous=hips.localRotation;
                if(i==5)Assert.That(Quaternion.Angle(start,hips.localRotation),Is.LessThan(15f),"Still mostly upright after .1s, not already lying back");
                foreach(string side in new[]{"Left","Right"})
                {
                    var upper=bones[side+"UpperArm"];var lower=bones[side+"LowerArm"];var hand=bones[side+"Hand"];
                    Vector3 target=new Vector3(side=="Left"?-3:4,side=="Left"?3:4,12);
                    Vector3 axis=(Vector3)driver.GetType().GetField(char.ToLower(side[0])+side.Substring(1)+"AimAxis",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(driver);
                    aim.Invoke(driver,new object[]{upper,lower,hand,target,axis,1f,side=="Left"?-1f:1f});
                    Assert.That(Vector3.Angle(hand.position-upper.position,target-upper.position),Is.LessThan(1f));
                    Assert.That(Vector3.Angle(hand.TransformDirection(axis),target-upper.position),Is.LessThan(1f),"Hand aim stays locked to its own anchor throughout recline");
                }
            }
            Assert.That(switched,Is.True);Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("AttachedBoth"),Is.True);
            Assert.That(Quaternion.Angle(start,hips.localRotation),Is.GreaterThan(45f),"Long transition and queued coast adjustment must actually finish");
            Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));Assert.That(Quaternion.Angle(go.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
        }
        finally {Object.Destroy(go);}
        yield return null;
    }
    [UnityTest]public IEnumerator GenericArmIKUsesIndependentAnchorsAndNeverTurnsGameplayRoot()
    {
        var go=new GameObject("Independent anchor presentation test");go.SetActive(false);
        var walk=(Behaviour)go.AddComponent(Real("Movement"));walk.enabled=false;var gear=(Behaviour)go.AddComponent(Real("ODM_Movement"));gear.enabled=false;
        var attack=(Behaviour)go.AddComponent(Real("Attack"));attack.enabled=false;
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"),go.transform);model.GetComponent<Animator>().enabled=false;
        var driver=(Behaviour)go.AddComponent(Real("ScoutPlayerAnimation"));driver.enabled=false;Set(driver,"animator",model.GetComponent<Animator>());
        var bones=model.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
        foreach(string side in new[]{"left","right"}) { string title=char.ToUpper(side[0])+side.Substring(1);Set(driver,side+"Upper",bones[title+"UpperArm"]);Set(driver,side+"Lower",bones[title+"LowerArm"]);Set(driver,side+"Hand",bones[title+"Hand"]); }
        go.transform.rotation=Quaternion.Euler(15,35,0);go.SetActive(true);
        AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"GasBoth.anim").SampleAnimation(model,.2f);
        Vector3 position=go.transform.position;Quaternion rotation=go.transform.rotation;
        var method=driver.GetType().GetMethod("AimArm",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(string side in new[]{"Left","Right"})
        {
            var upper=bones[side+"UpperArm"];var lower=bones[side+"LowerArm"];var hand=bones[side+"Hand"];
            Vector3 target=upper.position+go.transform.forward*8+go.transform.up*2+go.transform.right*(side=="Left"?-3:4);
            Vector3 axis=(Vector3)driver.GetType().GetField(char.ToLower(side[0])+side.Substring(1)+"AimAxis",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(driver);
            method.Invoke(driver,new object[]{upper,lower,hand,target,axis,1f,side=="Left"?-1f:1f});
            Assert.That(Vector3.Angle(hand.position-upper.position,target-upper.position),Is.LessThan(1));Assert.That(Vector3.Angle(hand.TransformDirection(axis),target-upper.position),Is.LessThan(1));
        }
        Assert.That(go.transform.position,Is.EqualTo(position));Assert.That(Quaternion.Angle(rotation,go.transform.rotation),Is.LessThan(.001f));Object.Destroy(go);yield return null;
    }
    [UnityTest]public IEnumerator FlipEnvelopeChecksTravelNotOnlyClearEndpoints()
    {
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var scene=UnityEngine.SceneManagement.SceneManager.CreateScene("Scout owned flip clearance",new UnityEngine.SceneManagement.CreateSceneParameters(UnityEngine.SceneManagement.LocalPhysicsMode.Physics3D));
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        var go=new GameObject("Visual flip envelope");go.SetActive(false);go.transform.position=Vector3.up*20;
        var driver=(Behaviour)go.AddComponent(Real("ScoutPlayerAnimation"));driver.enabled=false;
        foreach(string type in new[]{"Movement","ODM_Movement","Attack"})((Behaviour)go.GetComponent(Real(type))).enabled=false;
        go.SetActive(true);var method=driver.GetType().GetMethod("FlipClear",BindingFlags.Instance|BindingFlags.NonPublic);
        var wall=new GameObject("Thin obstacle between endpoints");wall.transform.position=new Vector3(5,20,0);wall.AddComponent<BoxCollider>().size=new Vector3(.1f,20,20);Physics.SyncTransforms();
        Assert.That((bool)method.Invoke(driver,null),Is.True);go.transform.position=new Vector3(10,20,0);Physics.SyncTransforms();
        Assert.That((bool)method.Invoke(driver,null),Is.False,"A fast cosmetic flip cannot tunnel its envelope through a wall");
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
    }
    [Test]public void NarutoRunPlantsTheStanceFootAndTrailsArmsBehindForwardTorso()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var b=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Run.anim");
            clip.SampleAnimation(go,.08f*clip.length);float ankleHeight=b["LeftFoot"].position.y;float start=b["LeftFoot"].position.z;
            foreach(float phase in new[]{.08f,.10f,.12f,.14f})
            {
                clip.SampleAnimation(go,phase*clip.length);
                Assert.That(b["LeftFoot"].position.y,Is.EqualTo(ankleHeight).Within(.003f),"Stance ankle must not float with the pelvis bob");
                Assert.That(b["LeftFoot"].position.z,Is.EqualTo(start-.86f*(phase-.08f)/.30f).Within(.004f),"Flat support retracts evenly before the heel lifts for toe-off");
                Vector3 torso=(b["Neck"].position-b["Chest"].position).normalized;
                Assert.That(Vector3.Angle(torso,Vector3.up),Is.InRange(30f,45f));
                foreach(string side in new[]{"Left","Right"})Assert.That(b[side+"Hand"].position.z,Is.LessThan(b["Hips"].position.z-.1f),"Naruto arms stay behind the body");
            }
            clip.SampleAnimation(go,.72f*clip.length);Assert.That(b["LeftFoot"].position.y-ankleHeight,Is.GreaterThan(.2f),"Recovery boot clears the ground");
            Assert.That(go.transform.position,Is.EqualTo(Vector3.zero));
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test]public void NarutoRunLoopsWithoutBoneSnapAndCadenceMatchesAuthoredFootTravel()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;go.transform.localScale=Vector3.one*2;
        try
        {
            var bones=go.GetComponentsInChildren<Transform>();var foot=bones.First(t=>t.name=="LeftFoot");var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Run.anim");
            clip.SampleAnimation(go,0);var rotations=bones.Select(t=>t.localRotation).ToArray();var positions=bones.Select(t=>t.localPosition).ToArray();
            clip.SampleAnimation(go,clip.length);
            for(int i=0;i<bones.Length;i++){Assert.That(Quaternion.Angle(rotations[i],bones[i].localRotation),Is.LessThan(.05f),bones[i].name);Assert.That(Vector3.Distance(positions[i],bones[i].localPosition),Is.LessThan(.0001f));}
            // Read the planted toe, NOT the ankle: rolling about the toe must
            // lift/advance the ankle without skating the contact point.
            clip.SampleAnimation(go,0);
            var rest=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));rest.GetComponent<Animator>().enabled=false;
            Vector3 toe;
            try {var rf=rest.GetComponentsInChildren<Transform>().First(t=>t.name=="LeftFoot");toe=rf.InverseTransformPoint(rf.position+Vector3.up*-.1185f+Vector3.forward*.18f);}
            finally {Object.DestroyImmediate(rest);}
            clip.SampleAnimation(go,.08f*clip.length);float first=foot.TransformPoint(toe).z;
            clip.SampleAnimation(go,.25f*clip.length);float authoredSpeed=(first-foot.TransformPoint(toe).z)/(.17f*clip.length);
            var playback=Real("ScoutPlayerAnimation").GetMethod("RunPlaybackSpeed",BindingFlags.Public|BindingFlags.Static);
            foreach(float speed in new[]{2f,6f,15f})Assert.That(authoredSpeed*(float)playback.Invoke(null,new object[]{speed,2f}),Is.EqualTo(speed).Within(.05f),"Visual cadence follows measured speed without changing the motor");
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test]public void PowerRunExtendsRearLegAndShowsSoleParallelToTorso()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var b=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);var foot=b["LeftFoot"];
            Vector3 soleNormal=foot.InverseTransformDirection(Vector3.down);
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Run.anim");clip.SampleAnimation(go,.42f*clip.length);
            Assert.That(foot.position.z-b["Hips"].position.z,Is.LessThan(-.60f),"Rear boot must drive well behind the hips, not stay tucked below them");
            float reach=Vector3.Distance(b["LeftUpperLeg"].position,foot.position);
            float length=Vector3.Distance(b["LeftUpperLeg"].position,b["LeftLowerLeg"].position)+Vector3.Distance(b["LeftLowerLeg"].position,foot.position);
            Assert.That(reach/length,Is.GreaterThan(.92f),"Rear drive needs a long, powerful leg silhouette");
            Vector3 normal=foot.TransformDirection(soleNormal).normalized,torso=(b["Neck"].position-b["Chest"].position).normalized;
            Assert.That(Mathf.Abs(Vector3.Dot(normal,torso)),Is.LessThan(.2f),"Sole plane should nearly follow the leaned body, not remain horizontal");
            Assert.That(normal.z,Is.LessThan(-.65f),"Rear sole faces backwards/up toward the viewer");
        }
        finally {Object.DestroyImmediate(go);}
    }
    [Test]public void PowerRunAlternatesSingleFootContactWithClearFlightBetweenSteps()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ScoutVisual.prefab"));go.GetComponent<Animator>().enabled=false;
        try
        {
            var b=go.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);var feet=new[]{b["LeftFoot"],b["RightFoot"]};
            var toe=new Vector3[2];var heel=new Vector3[2];
            for(int s=0;s<2;s++) {toe[s]=feet[s].InverseTransformPoint(feet[s].position+Vector3.up*-.1185f+Vector3.forward*.18f);heel[s]=feet[s].InverseTransformPoint(feet[s].position+Vector3.up*-.1185f-Vector3.forward*.05f);}
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Assets+"Run.anim");int flight=0;var support=new int[2];
            for(int i=0;i<120;i++)
            {
                clip.SampleAnimation(go,clip.length*i/120);int contacts=0;
                for(int s=0;s<2;s++)
                {
                    float sole=Mathf.Min(feet[s].TransformPoint(toe[s]).y,feet[s].TransformPoint(heel[s]).y);
                    Assert.That(sole,Is.GreaterThan(-.003f),"Boot must not punch through the nominal flat floor");
                    if(sole<.025f){contacts++;support[s]++;}
                }
                Assert.That(contacts,Is.LessThanOrEqualTo(1),"Running must never show double-foot support");if(contacts==0)flight++;
            }
            Assert.That(support[0],Is.GreaterThan(20));Assert.That(support[1],Is.GreaterThan(20));Assert.That(flight,Is.GreaterThan(25),"A powerful run needs genuine flight between alternating push-offs");
        }
        finally {Object.DestroyImmediate(go);}
    }
    [Test]public void LegacyAttackTimingRemainsOptIn()
    {
        var go=new GameObject("Legacy timing defaults");var a=go.AddComponent(Real("Attack"));
        Assert.That((float)a.GetType().GetField("strikeDelay").GetValue(a),Is.Zero);Assert.That((float)a.GetType().GetField("activeDuration").GetValue(a),Is.EqualTo(.6f));Assert.That((float)a.GetType().GetField("recoveryDuration").GetValue(a),Is.Zero);Object.DestroyImmediate(go);
    }
}
#endif
