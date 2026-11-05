using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public Animator armatureAnimator;

    [Header("Combat")]
    public DamageDealer damageDealer;

    [SerializeField] private AudioSource slashSfx;
    public bool HasSlashSound { get { return slashSfx!=null && slashSfx.clip!=null; } }

    [Min(0)] public float strikeDelay=0;
    [Min(.001f)] public float activeDuration=.6f;
    [Min(0)] public float recoveryDuration=0;
    public bool IsCharging { get; private set; }
    public bool IsAttacking { get; private set; }
    public event System.Action ChargeStarted;
    public event System.Action Released;
    public event System.Action Cancelled;
    public bool CanAttack { get { return isActiveAndEnabled && !PlayerCombatPermission.IsBlocked(this); } }
    public Quaternion StrikeRotation { get; private set; } = Quaternion.identity;
    public float LastReleaseTime { get; private set; } = float.NegativeInfinity;
    private bool nextAttackReady = true;
    



    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if(!CanAttack)
        {
            if(IsCharging || IsAttacking || (damageDealer!=null && damageDealer.gameObject.activeSelf))CancelAttack();
            return;
        }
        if (Input.GetKeyDown(KeyCode.F))TryBeginCharge();

    }

    public bool TryBeginCharge()
    {
        if(!CanAttack || !nextAttackReady)return false;
        StartCoroutine(ChargeAttack());return true;
    }
    public void CancelAttack()
    {
        StopAllCoroutines();nextAttackReady=true;IsCharging=IsAttacking=false;
        if(armatureAnimator!=null) armatureAnimator.SetBool("holdingAttack",false);
        if(damageDealer!=null) damageDealer.gameObject.SetActive(false);
        if(Cancelled!=null)Cancelled();
    }
    void OnDisable() { CancelAttack(); }

    IEnumerator ChargeAttack()
    {
        if(!CanAttack)yield break;
        nextAttackReady = false;IsCharging=true;
        if(ChargeStarted!=null)ChargeStarted();
        if(armatureAnimator!=null)armatureAnimator.SetBool("holdingAttack", true);
        while (Input.GetKey(KeyCode.F))
        {
            if(!CanAttack){CancelAttack();yield break;}
            yield return null;
        }
        if(!CanAttack){CancelAttack();yield break;}
        IsCharging=false;IsAttacking=true;
        var gear=GetComponent<ODM_Movement>();Vector3 aim=gear!=null && gear.camTransform!=null?gear.camTransform.forward:transform.forward;
        StrikeRotation=Quaternion.LookRotation(aim,Mathf.Abs(Vector3.Dot(aim,Vector3.up))>.98f?Vector3.forward:Vector3.up);
        LastReleaseTime=Time.time;
        if(armatureAnimator!=null)armatureAnimator.SetBool("holdingAttack", false);
        if(Released!=null)Released();
        if(slashSfx!=null)PlayAudio(slashSfx, 0.8f, 1.2f);
        // Defaults retain the legacy .6s window. Scout installer explicitly opts
        // its scene instance into anticipation / sharp cut / recovery timing.
        if(strikeDelay>0)yield return new WaitForSeconds(strikeDelay);
        if(!CanAttack){CancelAttack();yield break;}
        if (damageDealer != null) damageDealer.gameObject.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(.001f,activeDuration));
        if (damageDealer != null) damageDealer.gameObject.SetActive(false);
        if(recoveryDuration>0)yield return new WaitForSeconds(recoveryDuration);
        IsAttacking=false;nextAttackReady = true;
    }

    void PlayAudio(AudioSource audio, float startPitch, float endPitch)
    {
        GameAudio.Route(audio);
        audio.pitch = Random.Range(startPitch, endPitch);
        audio.Play();
    }

}
