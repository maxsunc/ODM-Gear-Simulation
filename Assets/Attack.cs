using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public Animator armatureAnimator;

    [Header("Combat")]
    public DamageDealer damageDealer;

    [SerializeField] private AudioSource slashSfx;

    [Min(0)] public float strikeDelay=0;
    [Min(.001f)] public float activeDuration=.6f;
    [Min(0)] public float recoveryDuration=0;
    public bool IsCharging { get; private set; }
    public bool IsAttacking { get; private set; }
    public event System.Action Released;
    private bool nextAttackReady = true;
    



    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.F) && nextAttackReady)
        {
            StartCoroutine(ChargeAttack());
        }

    }

    void OnDisable()
    {
        StopAllCoroutines();nextAttackReady=true;IsCharging=IsAttacking=false;
        if(armatureAnimator!=null) armatureAnimator.SetBool("holdingAttack",false);
        if(damageDealer!=null) damageDealer.gameObject.SetActive(false);
    }

    IEnumerator ChargeAttack()
    {
        nextAttackReady = false;IsCharging=true;
        if(armatureAnimator!=null)armatureAnimator.SetBool("holdingAttack", true);
        while (Input.GetKey(KeyCode.F))
        {
            yield return null;
        }
        IsCharging=false;IsAttacking=true;
        if(armatureAnimator!=null)armatureAnimator.SetBool("holdingAttack", false);
        if(Released!=null)Released();
        if(slashSfx!=null)PlayAudio(slashSfx, 0.8f, 1.2f);
        // Defaults retain the legacy .6s window. Scout installer explicitly opts
        // its scene instance into anticipation / sharp cut / recovery timing.
        if(strikeDelay>0)yield return new WaitForSeconds(strikeDelay);
        if (damageDealer != null) damageDealer.gameObject.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(.001f,activeDuration));
        if (damageDealer != null) damageDealer.gameObject.SetActive(false);
        if(recoveryDuration>0)yield return new WaitForSeconds(recoveryDuration);
        IsAttacking=false;nextAttackReady = true;
    }

    void PlayAudio(AudioSource audio, float startPitch, float endPitch)
    {
        audio.pitch = Random.Range(startPitch, endPitch);
        audio.Play();
    }

}
