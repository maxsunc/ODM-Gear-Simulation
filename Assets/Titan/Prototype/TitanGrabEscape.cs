using System;
using UnityEngine;

public enum TitanGrabEscapeState { Idle, Running, Succeeded, Failed, Cancelled }

/// <summary>Opt-in player QTE. The attack owns logical time, damage and release;
/// this component owns only prompts/input/HUD. No root/rig/physics writer.</summary>
[DefaultExecutionOrder(45)]
[DisallowMultipleComponent]
[RequireComponent(typeof(TitanCombatTarget))]
public sealed class TitanGrabEscape : MonoBehaviour
{
    public const float Window = 3f;
    public const int RequiredPresses = 3;
    public bool automaticInput = true;
    public bool showHud = true;
    static readonly KeyCode[] keys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Q, KeyCode.E };
    readonly int[] sequence = new int[RequiredPresses];
    System.Random random;
    TitanCombatTarget target;
    TitanGrabAttack attacker;
    int previousButtons, blockedButtons, startFrame;
    float age, startedAt, resultAt, wrongAt = -10;
    GUIStyle titleStyle, keyStyle, textStyle;
    public TitanGrabEscapeState State { get; private set; }
    public int Progress { get; private set; }
    public int WrongPresses { get; private set; }
    public float Remaining { get { return State == TitanGrabEscapeState.Running ? Mathf.Max(0, Window-age) : 0; } }
    public KeyCode CurrentKey { get { return State == TitanGrabEscapeState.Running ? keys[sequence[Progress]] : KeyCode.None; } }
    public TitanGrabAttack Attacker { get { return attacker; } }
    public static int Button(KeyCode key) { int i = Array.IndexOf(keys,key); return i < 0 ? 0 : 1 << i; }
    public void ResetVariation(int seed) { random = new System.Random(seed); }
    void Awake() { target = GetComponent<TitanCombatTarget>(); ResetVariation(Environment.TickCount ^ GetInstanceID()); }
    void OnEnable() { target.HoldReleased += OnReleased; }
    void OnDisable()
    {
        target.HoldReleased -= OnReleased;
        if(State == TitanGrabEscapeState.Running)
        {
            State = TitanGrabEscapeState.Cancelled;
            if(target.IsHeldBy(attacker))target.ReleaseHold(attacker,"Escape challenge disabled");
        }
        previousButtons = blockedButtons = 0;
    }
    int ReadButtons() { int mask=0;for(int i=0;i<keys.Length;i++)if(Input.GetKey(keys[i]))mask |= 1 << i;return mask; }
    internal bool Begin(TitanGrabAttack owner)
    {
        if(!isActiveAndEnabled || !target.IsHeldBy(owner) || target.IsDead)return false;
        attacker=owner;Progress=WrongPresses=0;age=0;startedAt=Time.time;startFrame=Time.frameCount;wrongAt=-10;
        if(automaticInput)previousButtons=ReadButtons();
        blockedButtons=previousButtons;
        int last=-1;
        for(int i=0;i<sequence.Length;i++) { int next=random.Next(i==0?6:5);if(i>0 && next>=last)next++;sequence[i]=last=next; }
        State=TitanGrabEscapeState.Running;return true;
    }
    void OnReleased()
    {
        if(State==TitanGrabEscapeState.Running) {State=TitanGrabEscapeState.Cancelled;resultAt=Time.time;}
    }
    void Update()
    {
        if(target.IsDead) { State=TitanGrabEscapeState.Idle;blockedButtons=0;return; }
        if(automaticInput)SampleButtons(ReadButtons());
    }
    /// <summary>Same six-button held-state sample used by actual input and deterministic tests.
    /// Only a fresh SINGLE edge counts. Holding/mashing/chords cannot clear prompts.</summary>
    public void SampleButtons(int heldButtons)
    {
        heldButtons &= 63;
        int pressed=heldButtons & ~previousButtons;previousButtons=heldButtons;
        blockedButtons &= heldButtons;
        if(State==TitanGrabEscapeState.Running || target.IsHeld || target.ReleasePending)blockedButtons |= heldButtons;
        if(!isActiveAndEnabled || State!=TitanGrabEscapeState.Running)return;
        if(automaticInput)Advance(Mathf.Max(age,Time.time-startedAt));
        if(State!=TitanGrabEscapeState.Running || Time.timeScale<=0 || (automaticInput && Time.frameCount==startFrame))return;
        if(pressed==0)return;
        if((pressed & (pressed-1))!=0 || pressed!=(1 << sequence[Progress]))
        { Progress=0;WrongPresses++;wrongAt=Time.time;return; }
        Progress++;
        if(Progress<RequiredPresses)return;
        // Set outcome BEFORE callbacks clear ownership, avoiding re-entrant cancellation.
        State=TitanGrabEscapeState.Succeeded;resultAt=Time.time;
        if(!target.TryEscapeGrab(attacker)) {State=TitanGrabEscapeState.Cancelled;resultAt=Time.time;}
    }
    public void Advance(float logicalAge)
    {
        if(State!=TitanGrabEscapeState.Running || float.IsNaN(logicalAge) || float.IsInfinity(logicalAge) || logicalAge<0)return;
        if(attacker==null || !target.IsHeldBy(attacker) || target.IsDead) {State=TitanGrabEscapeState.Cancelled;resultAt=Time.time;return;}
        age=Mathf.Max(age,logicalAge);
        if(age>=Window) {State=TitanGrabEscapeState.Failed;resultAt=Time.time;}
    }
    // Quarantine keys used while caught until UP, including the successful final key.
    // Grace does NOT block normal attacks or confer general damage immunity.
    public bool BlocksKey(KeyCode key)
    {
        int bit=Button(key);
        return bit!=0 && isActiveAndEnabled && ((State==TitanGrabEscapeState.Running || target.IsHeld || target.ReleasePending) || (blockedButtons & bit)!=0);
    }
    public float FilterAxis(float value, KeyCode negative, KeyCode positive)
    { return value<0 && BlocksKey(negative) || value>0 && BlocksKey(positive) ? 0 : value; }
    public static void Install(GameObject player)
    {
        if(player.GetComponent<TitanCombatTarget>()!=null && player.GetComponent<TitanAerialMotor>()!=null && player.GetComponent<TitanGrabEscape>()==null)
            player.AddComponent<TitanGrabEscape>();
    }
    void OnGUI()
    {
        if(!showHud || target.IsDead || State==TitanGrabEscapeState.Idle || State==TitanGrabEscapeState.Cancelled)return;
        if(State!=TitanGrabEscapeState.Running && !target.IsHeld && !target.ReleasePending && Time.time-resultAt>1.2f)return;
        if(titleStyle==null)
        {
            titleStyle=new GUIStyle(GUI.skin.label){fontSize=24,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            keyStyle=new GUIStyle(titleStyle){fontSize=44};textStyle=new GUIStyle(titleStyle){fontSize=16,fontStyle=FontStyle.Normal};
        }
        var oldMatrix=GUI.matrix;var oldColor=GUI.color;
        float scale=Mathf.Min(1.4f,Mathf.Min(Screen.width/600f,Screen.height/440f));
        GUI.matrix=Matrix4x4.TRS(new Vector3(Screen.width*.5f,Screen.height*.68f,0),Quaternion.identity,Vector3.one*scale);
        GUI.color=new Color(.05f,.07f,.1f,.96f);GUI.DrawTexture(new Rect(-270,-100,540,200),Texture2D.whiteTexture);
        GUI.color=Color.white;
        if(State==TitanGrabEscapeState.Running)
        {
            GUI.Label(new Rect(-260,-94,520,36),"BREAK FREE",titleStyle);
            GUI.color=Time.time-wrongAt<.35f?new Color(1,.4f,.35f):new Color(1,.85f,.25f);
            GUI.Box(new Rect(-44,-52,88,76),GUIContent.none);GUI.Label(new Rect(-44,-52,88,76),CurrentKey.ToString(),keyStyle);
            GUI.color=Color.white;
            GUI.Label(new Rect(-260,26,520,30),Progress+" / 3 correct   •   "+Remaining.ToString("F1")+"s",textStyle);
            GUI.color=new Color(.2f,.23f,.28f);GUI.DrawTexture(new Rect(-235,61,470,8),Texture2D.whiteTexture);
            GUI.color=new Color(1,.75f,.2f);GUI.DrawTexture(new Rect(-235,61,470*Remaining/Window,8),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.Label(new Rect(-260,72,520,24),Time.time-wrongAt<.35f?"Wrong key — progress reset!":"Press the shown key. Wrong key resets progress, not time.",textStyle);
        }
        else
        {
            bool success=State==TitanGrabEscapeState.Succeeded;
            GUI.color=success?new Color(.4f,1,.6f):new Color(1,.4f,.35f);
            GUI.Label(new Rect(-260,-45,520,45),success?"ESCAPED!":"TOO SLOW!",titleStyle);
            GUI.color=Color.white;GUI.Label(new Rect(-260,3,520,40),success?(target.ReleasePending?"Bite cancelled — waiting for a clear exit…":"Jump away! 1 second of re-grab protection."):"The Titan is bringing you to its mouth…",textStyle);
        }
        GUI.color=oldColor;GUI.matrix=oldMatrix;
    }
}
