using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public Animator armatureAnimator;

    [Header("Combat")]
    public DamageDealer damageDealer;

    [SerializeField] private AudioSource slashSfx;

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

    IEnumerator ChargeAttack()
    {
        nextAttackReady = false;
        armatureAnimator.SetBool("holdingAttack", true);
        while (Input.GetKey(KeyCode.F))
        {
            yield return null;
        }
        armatureAnimator.SetBool("holdingAttack", false);
        PlayAudio(slashSfx, 0.8f, 1.2f);

        // Enable sword damage collider during attack window
        if (damageDealer != null) damageDealer.gameObject.SetActive(true);

        // wait for attack anim to be done
        yield return new WaitForSeconds(0.6f);

        // Disable sword damage collider after attack window
        if (damageDealer != null) damageDealer.gameObject.SetActive(false);

        nextAttackReady = true;
    }

    void PlayAudio(AudioSource audio, float startPitch, float endPitch)
    {
        audio.pitch = Random.Range(startPitch, endPitch);
        audio.Play();
    }

}
