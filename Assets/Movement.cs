using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public Transform cam;
    public CharacterController controller;
    public float moveSpeed;
    [HideInInspector]
    public float copySpeed;
    public bool enabledMovement;
    public float turnSmoothTime = 0.1f;
    float smoothVelocity;
    public GameObject ODM;
    [HideInInspector]
    public Vector3 move;
    public Transform groundCheck;
    public LayerMask targetLayer;
    public AudioSource jumpSfx;
    public float gravityConstant = 6f;

    private Animator playerAnimator;

    private Vector3 _velocity;
    private bool gravity = true;
    private bool cursorLock = true;
    // reset x rotation whenever we hit the ground (trigger)
    private bool resetXRot = false;
    // work on jumping
    // move speed scales with this
    private float jumpForce;
    private bool isJumping = false;
    private float gravityScale = 1f;
    

    void Start()
    {
        playerAnimator = GetComponentInChildren<Animator>();
      //  swordAnimator = this.gameObject.transform.GetChild(0).GetChild(3).GetComponent<Animator>();

        copySpeed = moveSpeed;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log("Gravity " + gravity);
        if (CheckGround())
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                isJumping = true;
                StartCoroutine(Jump());
            }
            // we are grounded
            turnSmoothTime = 0.1f;

        }
        else
        {
            // we are in the air
            turnSmoothTime = 0.3f;
        }
        move.x = Input.GetAxisRaw("Horizontal");
            move.z = Input.GetAxisRaw("Vertical");
    }

    IEnumerator Jump()
    {
        jumpSfx.Play();
        yield return new WaitForSeconds(0.1f);
        ResetGravity(false);
        jumpForce = 55f;
        while (jumpForce > 0)
        {
            jumpForce -= 55f / 120;
            // initial force, somewhat big
            yield return null;
        }
        isJumping = false;
        ResetGravity(true);
        jumpForce = 0;
        gravityScale = 1.5f;
        // float time
        yield return new WaitForSeconds(0.5f);

        // reset gravity
        //ResetGravity(true);
        // lingering
        
    }
    void FixedUpdate()
    {
        if (enabledMovement)
        {
            Vector3 dir = new Vector3(move.x, 0f, move.z).normalized;

            if(dir.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref smoothVelocity, turnSmoothTime);

                playerAnimator.SetBool("isRunning", true);
              //  swordAnimator.SetBool("isRunning", true);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                controller.Move((moveDir.normalized * moveSpeed + Vector3.up * jumpForce)  * Time.fixedDeltaTime);
            }
            else
            {
                playerAnimator.SetBool("isRunning", false);
                //swordAnimator.SetBool("isRunning", false);
                // still move the jumping if pressed
                controller.Move((Vector3.up * jumpForce) * Time.fixedDeltaTime);
            }
        }
        if (gravity)
        {
            Gravity();
        }

      

    }

    void Gravity()
    {
        if (!isJumping)
        {
            _velocity.y += 9.8f * Time.fixedDeltaTime * gravityScale;
            _velocity.y = Mathf.Clamp(_velocity.y, 0f, 78.4f);
            controller.Move(-_velocity * Time.fixedDeltaTime);
        }
    }

    public void SlowDownGravity(float value)
    {
        _velocity.y -= value;
    }


    public void ResetGravity(bool option)
    {
        gravity = option;
        _velocity.y = 0;
        //reset gravity
        // stop doing gravity
    }
    public void zeroGravity()
    {
        _velocity.y = 0;
    }

    public void toggleGravity(bool val)
    {
        if (isJumping)
        {
            return;
        }
        gravity = val;
    }

    public bool CheckGround()
    {
        Collider[] coli = Physics.OverlapBox(groundCheck.position, new Vector3(0.1f, 0.4f, 0.1f), Quaternion.identity, targetLayer);
        // found something with a ground layer iwthin our feet
        if(coli.Length >= 1) 
        {
            // rsete the x rotation?? why
            if (!resetXRot)
            {
                transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, transform.eulerAngles.z);
                resetXRot = true;
            }
        return true;
        }
        resetXRot = false;
        return false;

    
   }


    public void ChangeCursorState(bool state)
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public bool _isJumping()
    {
        return isJumping;
    }

}
