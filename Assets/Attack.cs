using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public Animator armatureAnimator;


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


        // wait for attack anim to be done
        yield return new WaitForSeconds(0.6f);

        nextAttackReady = true;
    }

    void PlayAudio(AudioSource audio, float startPitch, float endPitch)
    {
        audio.pitch = Random.Range(startPitch, endPitch);
        audio.Play();
    }

}
