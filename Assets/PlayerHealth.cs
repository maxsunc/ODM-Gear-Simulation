using UnityEngine;
using UnityEngine.SceneManagement;

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

    void Start()
    {
        currentHP = maxHP;
        movement = GetComponent<Movement>();
        odmMovement = GetComponent<ODM_Movement>();
        attack = GetComponent<Attack>();
        EnsureScoutRagdoll();
    }

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

    public void TakeDamage(float damage)
    {
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
        var presentation = EnsureScoutRagdoll();
        if (presentation != null) presentation.CaptureDeathPose();
        isDead = true;
        if(Died!=null) Died();

        // No live gravity/jump/rope coroutine may keep moving a dead controller.
        if (movement != null) { movement.StopAllCoroutines();movement.enabledMovement = false;movement.enabled=false; }
        if (odmMovement != null) { odmMovement.SuspendForTitan(this);odmMovement.enabled = false; }
        if (attack != null) attack.enabled = false;

        // Enable ragdoll on player
        bool scoutCorpse = presentation != null && presentation.Activate();
        if (!scoutCorpse && ragdollController != null) ragdollController.EnableRagdoll();

        // Reload scene after a short delay (matches GameElements.cs pattern)
        Invoke(nameof(ReloadScene), 3f);
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

        // Re-enable movement
        if (movement != null) movement.enabledMovement = true;
        if (odmMovement != null) odmMovement.enabled = true;
    }

    private void ReloadScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }
}
