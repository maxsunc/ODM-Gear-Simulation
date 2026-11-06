using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHP = 100f;
    public float currentHP;
    public bool showHealthBar=true;
    float lastDamageAt=-10;
    GUIStyle healthText;

    [Header("References")]
    public RagdollController ragdollController;

    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isGrabbed = false;
    [HideInInspector] public Transform grabbedByTitan;

    // Integration listens before ragdoll ownership takes over; it does not use
    // legacy OnGrabbed's skeleton parenting path.
    public event System.Action Died;
    private Movement movement;
    private ODM_Movement odmMovement;
    private Attack attack;

    bool initialized;
    void Start(){Initialize();EnsureRespawn();}
    void Initialize()
    {
        if(initialized)return;initialized=true;
        currentHP = maxHP;
        movement = GetComponent<Movement>();
        odmMovement = GetComponent<ODM_Movement>();
        attack = GetComponent<Attack>();
        EnsureScoutRagdoll();
    }

    PlayerRespawn EnsureRespawn()
    {var respawn=GetComponent<PlayerRespawn>();if(respawn==null)respawn=gameObject.AddComponent<PlayerRespawn>();respawn.Initialize();return respawn;}
    internal void CompleteRespawn()
    {currentHP=Finite(maxHP)&&maxHP>0?maxHP:100;isDead=isGrabbed=false;grabbedByTitan=null;lastDamageAt=-10;}
    static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
    void OnGUI()
    {
        if(!showHealthBar || !Finite(maxHP) || maxHP<=0)return;
        if(healthText==null)healthText=new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        var matrix=GUI.matrix;var color=GUI.color;float scale=Mathf.Min(1.4f,Mathf.Min(Screen.width/600f,Screen.height/440f));
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float value=isDead?0:currentHP,fill=Mathf.Clamp01(value/maxHP);
        GUI.color=Time.time-lastDamageAt<.2f?new Color(1,.3f,.2f,.9f):new Color(.04f,.06f,.08f,.7f);
        GUI.DrawTexture(new Rect(18,18,264,30),Texture2D.whiteTexture);
        GUI.color=Color.Lerp(new Color(.85f,.15f,.13f),new Color(.25f,.85f,.45f),fill);
        GUI.DrawTexture(new Rect(21,21,258*fill,24),Texture2D.whiteTexture);
        string text=Mathf.CeilToInt(value)+" / "+Mathf.CeilToInt(maxHP);
        GUI.color=Color.black;GUI.Label(new Rect(19,19,264,30),text,healthText);GUI.color=Color.white;GUI.Label(new Rect(18,18,264,30),text,healthText);
        GUI.color=color;GUI.matrix=matrix;
    }

    public ScoutPlayerRagdoll EnsureScoutRagdoll()
    {
        var existing = GetComponent<ScoutPlayerRagdoll>();
        if (existing != null) return existing;
        var scout = GetComponent<ScoutPlayerAnimation>();
        return scout != null && scout.model != null ? gameObject.AddComponent<ScoutPlayerRagdoll>() : null;
    }

    public void PrepareStomp(Transform attacker,Vector3 point,float damage)
    {
        if(isDead || damage<currentHP)return;
        var presentation=EnsureScoutRagdoll();if(presentation==null)return;
        var reaction=GetComponent<ScoutStompReaction>();
        if(reaction!=null && reaction.IsActive){reaction.Begin(point,attacker);return;}
        Vector3 away=Vector3.ProjectOnPlane(transform.position-point,Vector3.up).normalized;if(away.sqrMagnitude<.5f)away=-transform.forward;
        var motor=GetComponent<TitanODMMotor>();Vector3 incoming=motor!=null?Vector3.ClampMagnitude(motor.Velocity,6):Vector3.zero;
        presentation.PrepareImpactDeath(attacker,incoming+away*9+Vector3.up*3);
    }
    public void ReactToStomp(Transform attacker,Vector3 point)
    {
        if(isDead || EnsureScoutRagdoll()==null || GetComponent<TitanODMMotor>()==null)return;
        var reaction=GetComponent<ScoutStompReaction>();if(reaction==null)reaction=gameObject.AddComponent<ScoutStompReaction>();reaction.Begin(point,attacker);
    }
    public void TakeDamage(float damage)
    {
        Initialize();
        if (isDead || !Finite(damage) || damage<=0) return;
        lastDamageAt=Time.time;
        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    public void Die()
    {
        if (isDead) return;
        Initialize();var respawn=EnsureRespawn();respawn.CaptureLivingState();
        var presentation = EnsureScoutRagdoll();
        if (presentation != null)
        {
            var motor=GetComponent<TitanODMMotor>();var target=GetComponent<TitanCombatTarget>();
            bool held=target!=null&&target.IsHeld&&target.HoldOwner!=null;
            presentation.PrepareNaturalDeath(held?target.CurrentHoldVelocity:motor!=null?motor.Velocity:Vector3.zero,held?target.HoldOwner.transform:null);presentation.CaptureDeathPose();
        }
        isDead = true;
        if(Died!=null) Died();

        // No live gravity/jump/rope coroutine may keep moving a dead controller.
        if (movement != null) { movement.StopAllCoroutines();movement.enabledMovement = false;movement.enabled=false; }
        if (odmMovement != null) { odmMovement.SuspendForTitan(this);odmMovement.enabled = false; }
        if (attack != null) attack.enabled = false;

        // Enable ragdoll on player
        bool scoutCorpse = presentation != null && presentation.Activate();
        if(ragdollController==null)ragdollController=GetComponent<RagdollController>();
        if (!scoutCorpse && ragdollController != null) ragdollController.EnableRagdoll();
        var feedback=GetComponent<PlayerDeathFeedback>();if(feedback==null)feedback=gameObject.AddComponent<PlayerDeathFeedback>();feedback.PlayDeath(presentation);

        // SAME scene and player references survive. Scaled-time3s delay; blocked
        // births retry safely rather than reloading or enabling overlapping physics.
        respawn.BeginDeath();InvokeRepeating(nameof(RespawnPlayer),PlayerRespawn.Delay,.25f);
    }

    public void OnGrabbed(Transform hand)
    {
        if (isDead) return;

        isGrabbed = true;
        grabbedByTitan = hand;

        // Disable movement while grabbed
        if (movement != null) movement.enabledMovement = false;
        if (odmMovement != null) odmMovement.enabled = false;

        // Parent player to titan hand
        transform.SetParent(hand);
    }

    public void OnReleased()
    {
        isGrabbed = false;
        grabbedByTitan = null;

        // Unparent from titan
        transform.SetParent(null);

        // A late legacy hand callback must not resurrect dead controls.
        if(isDead)return;
        // Re-enable movement
        if (movement != null) movement.enabledMovement = true;
        if (odmMovement != null) odmMovement.enabled = true;
    }

    private void RespawnPlayer()
    {if(!isDead || EnsureRespawn().TryRespawn())CancelInvoke(nameof(RespawnPlayer));}
}
