using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ODM_Movement : MonoBehaviour
{
    public CharacterController controller;
    public float acceleration;
    private float additionalAccel; // use thiss for skills and stuff
    public float maxSpeed;
 
    public float resistanceInertia;
    
    
    private float currentBurstSpeed;

    private Vector3 burstDirection;
    private float burstMeter = 100f;
    private Vector3 direction;
    private float accelCopy;
    private float currentSpeed;
    private Animator grappleIndict;

    public float strafeSpeed;
    private float strafeDir;
    private Movement movement;
    public Transform[] raycastShooters;
    public LayerMask targetLayer;
    public float range;
    public Audio audio;
    public LineRenderer[] lines;
    private bool rightGrappled, leftGrappled;
    private Vector3 pointLeft, pointRight;
    readonly ODMHookAnchor leftAttachment=new ODMHookAnchor(),rightAttachment=new ODMHookAnchor();
    public Transform camTransform;
    public TrailRenderer gasTrail;
    public Animator camAnim;
    public Transform characterMesh;
    public ParticleSystem gasParticle;
    public GameObject collisionParticle;


    [Tooltip("Optional visual-only replacement; null retains the original player.")]
    public Animator visualAnimator;
    private Animator playerAnimator;
    private Animator swordAnimator;
    private Vector3 residualDir;
    private float residualSpeed;
    private bool superGasing;

    // Public read-only accessors for DamageDealer momentum calculation
    public float CurrentSpeed => currentSpeed;
    public float ResidualSpeed => residualSpeed;
    private bool gasing;
    private bool isInertia;
    private bool qPress, ePress;

    private Vector3 pastForce;
    private UnityEngine.Object titanOwner;
    private bool titanSuspended;
    public bool TitanSuspended { get { return titanSuspended; } }
    public bool HasActiveHooks { get { return leftGrappled || rightGrappled; } }
    public bool LeftHooked { get { return leftGrappled; } }
    public bool RightHooked { get { return rightGrappled; } }
    public bool IsGasing { get { return gasing; } }
    public Vector3 LeftAnchor { get { RefreshHookAnchors();return pointLeft; } }
    public Vector3 RightAnchor { get { RefreshHookAnchors();return pointRight; } }
    public bool TryAttachHook(bool right,Collider surface,Vector3 hitPoint)
    {
        if(titanSuspended || PlayerCombatPermission.IsBlocked(this))return false;
        var anchor=right?rightAttachment:leftAttachment;
        if(!anchor.Bind(surface,hitPoint))return false;
        if(right){rightGrappled=true;pointRight=hitPoint;}
        else {leftGrappled=true;pointLeft=hitPoint;}
        SetHookLine(right,true,hitPoint);return true;
    }
    void SetHookLine(bool right,bool enabled,Vector3 point)
    {
        int index=right?0:1;
        if(lines==null || index>=lines.Length || lines[index]==null)return;
        lines[index].enabled=enabled;
        if(enabled && lines[index].positionCount>=2)lines[index].SetPosition(1,point);
    }
    public void ReleaseHook(bool right)
    {
        (right?rightAttachment:leftAttachment).Clear();
        if(right)rightGrappled=false;else leftGrappled=false;
        SetHookLine(right,false,Vector3.zero);
    }
    public void RefreshHookAnchors()
    {
        Vector3 p;
        if(leftGrappled){if(leftAttachment.TryGetPoint(out p)){pointLeft=p;SetHookLine(false,true,p);}else ReleaseHook(false);}
        if(rightGrappled){if(rightAttachment.TryGetPoint(out p)){pointRight=p;SetHookLine(true,true,p);}else ReleaseHook(true);}
    }
    void OnDisable(){ReleaseHook(false);ReleaseHook(true);}
    public bool SuspendForTitan(UnityEngine.Object owner)
    {
        if(owner==null || titanSuspended) return false;
        titanOwner=owner;titanSuspended=true;StopAllCoroutines();
        ReleaseHook(false);ReleaseHook(true);
        leftGrappled=rightGrappled=qPress=ePress=gasing=isInertia=checkingStuck=false;
        currentSpeed=residualSpeed=strafeDir=0;direction=residualDir=Vector3.zero;
        acceleration=accelCopy;additionalAccel=0;superGasing=false;
        foreach(var line in lines) if(line!=null) line.enabled=false;
        if(gasTrail!=null) gasTrail.emitting=false;
        if(gasParticle!=null) gasParticle.Stop(true,ParticleSystemStopBehavior.StopEmitting);
        if(audio!=null && audio.container!=null && audio.container.Length>3 && audio.container[3]!=null)
        { audio.container[3].Stop();audio.container[3].volume=.03f;audio.container[3].pitch=1; }
        if(camAnim!=null) camAnim.SetBool("StartGas",false);
        if(playerAnimator!=null) playerAnimator.SetBool("isGasing",false);
        return true;
    }
    public void ResumeAfterTitan(UnityEngine.Object owner,Vector3 velocity)
    {
        if(!titanSuspended || titanOwner!=owner) return;
        titanOwner=null;titanSuspended=false;
        if(characterMesh!=null) {var a=characterMesh.localEulerAngles;a.z=0;characterMesh.localEulerAngles=a;}
        Vector3 planar=Vector3.ProjectOnPlane(velocity,Vector3.up);
        if(planar.sqrMagnitude>.0001f && gameObject.activeInHierarchy) StartCoroutine(Deaccelerate(planar.normalized,planar.magnitude));
    }

    internal void ResetForRespawn()
    {
        // Explicit new-life reset, not a release over a foreign hold owner.
        StopAllCoroutines();titanOwner=null;titanSuspended=false;
        ReleaseHook(false);ReleaseHook(true);
        leftGrappled=rightGrappled=qPress=ePress=gasing=isInertia=checkingStuck=superGasing=false;
        currentSpeed=residualSpeed=currentBurstSpeed=strafeDir=additionalAccel=0;burstMeter=100;
        direction=residualDir=burstDirection=pastForce=pointLeft=pointRight=Vector3.zero;acceleration=accelCopy;
        if(gasTrail!=null){gasTrail.emitting=false;gasTrail.Clear();}
        if(gasParticle!=null)gasParticle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(audio!=null&&audio.container!=null&&audio.container.Length>3&&audio.container[3]!=null)
        {audio.container[3].Stop();audio.container[3].volume=.03f;audio.container[3].pitch=1;}
        if(camAnim!=null)camAnim.SetBool("StartGas",false);
    }

    //POA:
    /*
     Super gas:
     PRessing E increases acceleration and max speed temperaroly 
     inClude:
      gas burst effect on click
      Gas burst sound
      layered gas effect 1
         */

    void Start()
    {
        if(audio!=null && audio.container!=null)foreach(var source in audio.container)GameAudio.Route(source);
        movement = GetComponent<Movement>();
        accelCopy = acceleration;
        playerAnimator = visualAnimator!=null?visualAnimator:GetComponentInChildren<Animator>();
      //  swordAnimator = this.gameObject.transform.GetChild(0).GetChild(3).GetComponent<Animator>();
        // when acceleration is treated as speed
        grappleIndict = GameObject.Find("GrappleIndict").GetComponent<Animator>();
        lines[0].SetPosition(1, raycastShooters[0].position);
        // left line
        lines[1].SetPosition(1, raycastShooters[1].position);
    }

    

    void UpdateLines()
    {
        // right line
        lines[0].SetPosition(0, raycastShooters[0].position);
        // left line
        lines[1].SetPosition(0, raycastShooters[1].position);
    }

    void LateUpdate()
    {
        if(titanSuspended) return;
        RefreshHookAnchors();UpdateLines();
    }

    void Update()
    {
        if(titanSuspended) return;
        RefreshHookAnchors();
        if (movement.CheckGround())
        {
            // The generous foot probe may overlap ground before the controller
            // lands. Don't erase falling speed every render frame in that gap.
            if (controller.isGrounded) movement.zeroGravity();
            playerAnimator.SetBool("isFalling", false);
        }
        else
        {
            movement.toggleGravity(true);

            playerAnimator.SetBool("isFalling", true);
        }
        CheckHooks();
        

       
        if (Input.GetButton("Jump") && (leftGrappled || rightGrappled))
        {
            // on first frame clicked
            if (!gasing)
            {
                movement.ResetGravity(false);
                gasParticle.Play();
                gasing = true;
                playerAnimator.SetBool("isGasing", gasing);
            }

            
            Gas();
            if (!audio.container[3].isPlaying)
            {
                audio.container[3].Play();
            }
        }
        //STATE: not adding gas
        else
        {
            // ONE TIME USES GO HERE (STATE: AFTER DONE GASING)
            if (audio.container[3].isPlaying)
            {
                
                // reset rotation of mesh
                characterMesh.rotation = Quaternion.Euler(characterMesh.eulerAngles.x, characterMesh.eulerAngles.y, 0f);
                gasing = false;
                playerAnimator.SetBool("isGasing", gasing);
                gasTrail.emitting = false;
                gasParticle.loop = false;
                camAnim.SetBool("StartGas", false);
                Vector3 forceVector = residualDir * residualSpeed + (transform.forward + transform.right * strafeDir) * currentSpeed;
                // after gasing start deaccelerating uses latest direction to keep the direction
                StartCoroutine(Deaccelerate(forceVector.normalized, forceVector.magnitude));
            // current speed Only used for going no forces
            currentSpeed = 0;
                // strafe dir = 0 after deaccelerate
                strafeDir = 0;

                //transform.eulerAngles = new Vector3(0, transform.eulerAngles.x, transform.eulerAngles.z);
                audio.container[3].Stop();
                movement.ResetGravity(true);
                // turn back on gravity
            }
            // not grappling
            movement.enabledMovement = true;
        }

        // State: Press e and not gasing
        // only check if u inertiaing
        if (isInertia)
        {
            // check if hit side of wall or something idk lmao
            Collider[] coli = Physics.OverlapSphere(transform.position, 1f, targetLayer);

            if (coli.Length > 0 && !checkingStuck)
            {
                StartCoroutine(InertiaCheck());
            }
        }

    }
    bool checkingStuck = false;
    IEnumerator InertiaCheck()
    {
        // NOTE this doesn't work for colliding with multiple object, you would have to check if the raycasts in an aray are equal
        // shoot a bunch of raycasts in a circle to detect a hit point with the wall then spawn wall particle hit
        RaycastHit hit;
        for(int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0, 45f*i, 0) * Vector3.forward;
            if(Physics.Raycast(transform.position, direction, out hit, 1f, targetLayer))
            {
                GameObject particle = Instantiate(collisionParticle, hit.point, Quaternion.Euler(0, i, 0));
                // change the particle dude
                ParticleSystem p = particle.GetComponent<ParticleSystem>();
                var rend = p.GetComponent<ParticleSystemRenderer>();
                Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
                if (hitRenderer != null)
                {
                    rend.material = hitRenderer.material;
                }
                break;
            }
        }
        // also reduce the residual speed while hitting a wall or something ikd
        residualSpeed -= (2f + residualSpeed / 20f);
        checkingStuck = true;
        Vector3 oldPos = transform.position;
        yield return new WaitForSeconds(0.1f);
        if(Vector3.Distance(oldPos, transform.position) < 0.1f)
        {
            HaltInertia();
        }

        if (residualSpeed <= 0)
        {// stop inertia
            residualSpeed = 0;
            isInertia = false;
        }
        checkingStuck = false;
    }



    void HaltInertia()
    {
        residualSpeed = 0;
    }

    bool EscapeKeyBlocked(KeyCode key)
    { var escape=GetComponent<TitanGrabEscape>();return escape!=null && escape.BlocksKey(key); }

    void CheckHooks()
    {
        // check if cant grapple for the sake of UI
        RaycastHit hitInfo;


        
        grappleIndict.SetBool("canGrapple",  Physics.Raycast(camTransform.position, camTransform.forward, out hitInfo, range, targetLayer)) ;


        // LEFT
        if (Input.GetButtonDown("Fire1"))
        {
            qPress = false;
            //left
            Grapple("left");
        }
       // check if not hold grapple
        if (!Input.GetButton("Fire1"))
        {
            if (!qPress && leftGrappled )
            {

                audio.container[2].Play();
                leftGrappled = false;
                lines[1].enabled = false;
                // get rid of left hook
            }
        }

        if (Input.GetKeyDown(KeyCode.Q) && !EscapeKeyBlocked(KeyCode.Q))
        {
            qPress = true;
            Grapple("left");
        }
        if (!Input.GetKey(KeyCode.Q))
        {
            if (qPress && leftGrappled)
            {
                qPress = false;
                audio.container[2].Play();
                leftGrappled = false;
                lines[1].enabled = false;
                // get rid of left hook
            }
        }

        // //RIGHT
        if (Input.GetButtonDown("Fire2"))
        {
            ePress = false;
            // right
            Grapple();
        }
        // check if not hold grapple
        if (!Input.GetButton("Fire2"))
        {
            if (!ePress && rightGrappled)
            {

                audio.container[1].Play();
                rightGrappled = false;
                lines[0].enabled = false;
                // get rid of right hook
            }
        }

        if (Input.GetKeyDown(KeyCode.E) && !EscapeKeyBlocked(KeyCode.E))
        {
            ePress = true;
            Grapple();
        }
        if (!Input.GetKey(KeyCode.E))
        {
            if (ePress && rightGrappled)
            {
                ePress = false;
                audio.container[1].Play();
                rightGrappled = false;
                lines[0].enabled = false;
                // get rid of right hook
            }
        }
    }

    void FixedUpdate()
    {
        if(titanSuspended || controller==null || !controller.enabled) return;
        RefreshHookAnchors();Throttle();
    }

    void Throttle()
    {
        // cap speed
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);



        controller.Move(transform.right * strafeSpeed * strafeDir * Time.deltaTime);

        controller.Move(direction * currentSpeed * Time.fixedDeltaTime);

        controller.Move((residualDir) * residualSpeed * Time.fixedDeltaTime);
    }

    void Grapple(string dir = "right")
    {
        Transform grappleTransform = (dir == "right") ? raycastShooters[0] : raycastShooters[1];

        RaycastHit hitInfo; 
    

        // Old stuff
        //if(Physics.Raycast(grappleTransform.position, grappleTransform.forward, out hitInfo, range, targetLayer))
        if(gameObject.scene.GetPhysicsScene().Raycast(camTransform.position, camTransform.forward, out hitInfo, range, targetLayer,QueryTriggerInteraction.Ignore) && TryAttachHook(dir=="right",hitInfo.collider,hitInfo.point))
        {

            if (dir == "right")
            {
                // clamp it
                // maxes out at grapple duration
                // right hooked
                lines[0].enabled = true;
                lines[0].SetPosition(1, hitInfo.point);
                rightGrappled = true;
                pointRight = hitInfo.point;
                if (currentSpeed > 10f)
                {
                    // everytime new hook subtract speed
                    currentSpeed -= 2f;
                    
                }
            }
            else
            {// left hooked
                lines[1].enabled = true;
                lines[1].SetPosition(1, hitInfo.point);
                leftGrappled = true;
                 pointLeft = hitInfo.point;
                // everytime new hook subtract speed
                if (currentSpeed > 10f)
                {
                    // everytime new hook subtract speed
                    currentSpeed -= 2f;
                }
            }

            
            audio.container[0].Play();
            
            
        }

    }

    void Gas()
    {

        movement.ResetGravity(false);
        camAnim.SetBool("StartGas", true);
        gasTrail.emitting = true;
        gasParticle.loop = true;
        FaceGrapplePoint();
        // account for sudden changes in dir


        // when gasing stop deaccelerating TEMP CHANGE
        // StopCoroutine(Deaccelerate(transform.forward, 0f));
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            SuperGas(true);
        }
        else if (additionalAccel != 0 && !Input.GetKey(KeyCode.LeftShift))
        {
            SuperGas(false);
        }

        

        // add speed to currentSpeed
        currentSpeed += (acceleration + additionalAccel) * Time.deltaTime;
        

        // when gasing stafing enabled ONCE ONLY USES GO HERE
        if (movement.enabledMovement)
        {
            movement.enabledMovement = false;
            movement.ResetGravity(false);
            // turn off gravity
        }
        strafeDir = Input.GetAxisRaw("Horizontal");
        var escape=GetComponent<TitanGrabEscape>();
        if(escape!=null)strafeDir=escape.FilterAxis(strafeDir,KeyCode.A,KeyCode.D);
        // depending on strafe dir change rotation
        if(strafeDir < 0)
        {
             if (transform.rotation.z >= -60f)
                 characterMesh.rotation = Quaternion.Lerp(characterMesh.rotation, Quaternion.Euler(characterMesh.eulerAngles.x, characterMesh.eulerAngles.y, -60f)
 , Time.deltaTime);
        }
        else if(strafeDir > 0)
        {
            if (transform.rotation.z <= 60f)
                characterMesh.rotation = Quaternion.Lerp(characterMesh.rotation, Quaternion.Euler(characterMesh.eulerAngles.x, characterMesh.eulerAngles.y, 60)
, Time.deltaTime);
        }
        else
        {
            characterMesh.rotation = Quaternion.Lerp(characterMesh.rotation, Quaternion.Euler(characterMesh.eulerAngles.x, characterMesh.eulerAngles.y, 0f)
 , Time.deltaTime);
        }
        
        // x is just left or right in this case
        if(strafeDir != 0 && accelCopy == acceleration)
        {// we are strafing so acceleration? (do speed later)
            acceleration -= 2f;
            
        }
        else if(!superGasing)
        {
            acceleration = accelCopy;
        }

       /* // direction has changed and not strafing
        if (direction != transform.forward && strafeDir == 0)
        {
            
            // we are facing a different angle
            // past direction
            StartCoroutine(Deaccelerate(direction, currentSpeed));
            // keeps momentum in the other dir not immediantly switching
            // reset speed
            currentSpeed = 0;
        }*/

        // direction is updated
        direction = transform.forward;

    }

    void SuperGas(bool In)
    {
        if (In)
        {
            audio.container[3].volume = 0.1f;
            audio.container[3].pitch = 1.2f;
            // change this to allow for other stuff to edit it
            additionalAccel = Mathf.Round(accelCopy / 1.5f);
            superGasing = true;
        }
        else
        {
            audio.container[3].volume = 0.03f;
            audio.container[3].pitch = 1f;
            superGasing = false;
            additionalAccel = 0f;
        }
    }

  void FaceGrapplePoint()
    {
        Vector3 midpoint = Vector3.zero;
        // check if both hooked
        if (rightGrappled && leftGrappled)
        {
            // midpoint formula
            midpoint = new Vector3((pointLeft.x + pointRight.x) / 2, (pointLeft.y + pointRight.y) / 2, (pointLeft.z + pointRight.z) / 2);
        }
        else
        {
            // only 1 grappled find out which one
            midpoint = (rightGrappled) ? pointRight : pointLeft;
            // only move towards grappled position
        }

        // face point
        Vector3 dir = new Vector3(midpoint.x - transform.position.x, midpoint.y - transform.position.y, midpoint.z - transform.position.z);

        Vector3 newDirection = Vector3.RotateTowards(transform.forward, dir, 0.02f, 0.0f);

        transform.rotation = Quaternion.LookRotation(newDirection);
        // transform.forward = dir;
    }
    // more like inertia than deaccelertae
    IEnumerator Deaccelerate(Vector3 pastDir, float pastSpeed)
    {
        // update residuals
        //case of strafing lower inertia
        if (strafeDir != 0)
        {
            residualSpeed = pastSpeed * 0.85f;
        }
        // not strafing inertia full
        else
        {
            residualSpeed = pastSpeed;
        }
        residualDir = pastDir;
        isInertia = true;
        // deaccelerating will stop when pastSpeed is zero OR manually through
        // adding more gas in another direction OR When grounded
        while (true)
        {
            yield return new WaitForSeconds(0.1f);

            // grounded add more resistance
            if (movement.CheckGround() || gasing)
            {
                residualSpeed -= 4f;
                // we are sliding against stuff

            }



            residualSpeed -= resistanceInertia;
            residualSpeed = Mathf.Clamp(residualSpeed, 0f, maxSpeed);
            // reduce speed

            // check currentSpeed == 0 then stop
            if (residualSpeed <= 0)
            {// stop inertia
                residualSpeed = 0;
                isInertia = false;
                break;
            }
            // if it jumps get rid of the y dir
            if (movement._isJumping())
            {
                // get rid of y dir
                residualDir = new Vector3(residualDir.x, 0, residualDir.z);
            }

            if (gasing)
            {
                // additionally if is gasing lower residual speed according to direction gasing
                // calculate angles
                float angle2 = Mathf.Atan2(direction.x, direction.z);
                // we want the reciprocal for angle 1
                float angle1 = Mathf.Atan2(pastDir.z, pastDir.x);

                // Calculate the angular difference between angle1 and angle2
                float angularDifference = Mathf.Abs(Mathf.Rad2Deg * Mathf.DeltaAngle(angle1, angle2));

                // Normalize the angular difference to a value between 0 and 1
                float proximity = 1f - angularDifference / 180f;
                proximity = (proximity < 0) ? 0 : proximity;

                float gasingStopSpeed = 4f;
                // lower residual based on proxinity
                residualSpeed -= proximity * gasingStopSpeed;
                // Use the proximity value for further calculations or display
            }
        }
        
    }

    public float AddAccel(int amount)
    {
        accelCopy += amount;
        acceleration += amount;

        return accelCopy;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(this.transform.position, 1f);
        //Draws the same circle the physics drew but you can see it
    }


}



