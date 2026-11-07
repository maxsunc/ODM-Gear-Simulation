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
    [Header("Gravity")]
    [Tooltip("Downward acceleration in metres/second squared, used throughout jumping, falling and braking upward Titan-release momentum. Positive values pull down; 0 adds no acceleration. Gas still owns movement while powered. Try 9.8 for Earth gravity or 20-30 for a heavier fall.")]
    [Min(0)] public float gravityConstant = 9.8f;
    [Tooltip("Maximum gravity-driven falling speed in metres/second. Does not cap hook thrust, inertia, or Titan physics.")]
    [Min(.1f)] public float maximumFallSpeed = 78.4f;
    public float GravityAcceleration { get { return float.IsNaN(gravityConstant) || float.IsInfinity(gravityConstant) ? 9.8f : Mathf.Max(0, gravityConstant); } }
    public float GravityVerticalSpeed { get { return jumpForce > 0 ? jumpForce : titanReleaseUp > 0 ? titanReleaseUp : -_velocity.y; } }
    float FallSpeedLimit { get { return float.IsNaN(maximumFallSpeed) || float.IsInfinity(maximumFallSpeed) ? 78.4f : Mathf.Max(.1f, maximumFallSpeed); } }

    [Header("Jump")]
    [Tooltip("Desired jump height in metres at the current Gravity Constant. 0 disables jumping. Requires positive gravity; changing gravity in flight changes the trajectory.")]
    [Min(0)] public float jumpHeight = 4f;
    [Tooltip("Seconds between pressing C and takeoff. 0 is immediate on the next physics tick. Walking off the ground during this delay cancels takeoff.")]
    [Min(0)] public float jumpDelay = .1f;
    [Tooltip("Minimum seconds between takeoffs. Landing is still required; this does not enable double-jumping.")]
    [Min(0)] public float jumpCooldown = .15f;
    public float JumpLaunchSpeed
    {
        get
        {
            if (float.IsNaN(jumpHeight) || float.IsInfinity(jumpHeight) || jumpHeight <= 0 || GravityAcceleration <= 0) return 0;
            double speed = Math.Sqrt(2d * GravityAcceleration * jumpHeight);
            return speed <= float.MaxValue ? (float)speed : 0;
        }
    }
    public bool JumpPending { get { return jumpDelayRemaining >= 0; } }

    [Tooltip("Optional visual-only replacement; null retains the original player.")]
    public Animator visualAnimator;
    private Animator playerAnimator;

    private Vector3 _velocity;
    private bool gravity = true;
    private bool cursorLock = true;
    // reset x rotation whenever we hit the ground (trigger)
    private bool resetXRot = false;
    // Upward jump velocity (m/s), advanced ONLY by physics time, not render frames.
    private float jumpForce;
    private bool isJumping = false;
    private float jumpDelayRemaining = -1;
    private float jumpCooldownRemaining;
    private readonly RaycastHit[] jumpGroundHits = new RaycastHit[16];
    private UnityEngine.Object titanOwner;
    private bool titanSuspended;
    private bool movementBeforeTitan;
    private float titanReleaseUp;
    public bool TitanSuspended { get { return titanSuspended; } }
    public bool SuspendForTitan(UnityEngine.Object owner)
    {
        if (owner == null || titanSuspended) return false;
        titanOwner = owner; titanSuspended = true; movementBeforeTitan = enabledMovement;
        StopAllCoroutines(); CancelJump(); titanReleaseUp = 0;
        enabledMovement = false; move = Vector3.zero; _velocity = Vector3.zero;
        if (playerAnimator != null) playerAnimator.SetBool("isRunning", false);
        return true;
    }
    public void ResumeAfterTitan(UnityEngine.Object owner, Vector3 velocity)
    {
        if (!titanSuspended || titanOwner != owner) return;
        titanOwner = null; titanSuspended = false; enabledMovement = movementBeforeTitan;
        gravity = true; CancelJump();
        _velocity = new Vector3(0, Mathf.Max(0, -velocity.y), 0);
        titanReleaseUp = Mathf.Max(0, velocity.y);
    }


    internal void ResetForRespawn(bool allowMovement)
    {
        StopAllCoroutines();titanOwner=null;titanSuspended=false;enabledMovement=allowMovement;
        CancelJump();jumpCooldownRemaining=0;titanReleaseUp=0;_velocity=move=Vector3.zero;
        gravity=true;resetXRot=false;smoothVelocity=0;
    }
    void OnDisable() { CancelJump(); }

    void Start()
    {
        playerAnimator = visualAnimator != null ? visualAnimator : GetComponentInChildren<Animator>();
        //  swordAnimator = this.gameObject.transform.GetChild(0).GetChild(3).GetComponent<Animator>();

        copySpeed = moveSpeed;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        if (titanSuspended) return;
        if (Input.GetKeyDown(KeyCode.C)) TryJump();
        if (CheckGround())
        {
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
        var escape=GetComponent<TitanGrabEscape>();
        if(escape!=null)
        {
            move.x=escape.FilterAxis(move.x,KeyCode.A,KeyCode.D);
            move.z=escape.FilterAxis(move.z,KeyCode.S,KeyCode.W);
        }
    }

    static float JumpSeconds(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0, value);
    }

    bool JumpControlsAvailable()
    {
        var health = GetComponent<PlayerHealth>();
        return enabledMovement && gravity && !titanSuspended && controller != null && controller.enabled &&
            (health == null || (!health.isDead && !health.isGrabbed));
    }

    bool HasJumpSupport()
    {
        if (controller.isGrounded) return true;
        // A short WORLD-UP foot cast, not the legacy .4m animation probe.
        // Exclude triggers/self and reject side walls or over-steep support.
        var bounds = controller.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * .8f;
        if (radius <= 0) return false;
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + radius + .02f, bounds.center.z);
        int n = gameObject.scene.GetPhysicsScene().SphereCast(origin, radius, Vector3.down, jumpGroundHits,
            .04f + controller.skinWidth, targetLayer, QueryTriggerInteraction.Ignore);
        if (n >= jumpGroundHits.Length) return false;
        float minUp = Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < n; i++)
        {
            var hit = jumpGroundHits[i];
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.normal.y >= minUp) return true;
        }
        return false;
    }

    // Accepts one C/button request. The delay and final ground check run in FixedUpdate.
    public bool TryJump()
    {
        if (!isActiveAndEnabled || !JumpControlsAvailable() || isJumping || jumpCooldownRemaining > 0 ||
            titanReleaseUp > 0 || JumpLaunchSpeed <= 0 || !HasJumpSupport()) return false;
        isJumping = true;
        jumpDelayRemaining = JumpSeconds(jumpDelay, .1f);
        return true;
    }

    void CancelJump()
    {
        isJumping = false; jumpForce = 0; jumpDelayRemaining = -1;
    }

    void TickJumpRequest(float dt)
    {
        jumpCooldownRemaining = Mathf.Max(0, jumpCooldownRemaining - dt);
        if (!JumpPending) return;
        if (!JumpControlsAvailable() || JumpLaunchSpeed <= 0 || !HasJumpSupport()) { CancelJump(); return; }
        jumpDelayRemaining -= dt;
        if (jumpDelayRemaining > .000001f) return;
        jumpDelayRemaining = -1;
        jumpForce = JumpLaunchSpeed;
        _velocity.y = 0; titanReleaseUp = 0;
        jumpCooldownRemaining = JumpSeconds(jumpCooldown, .15f);
        if (jumpSfx != null) { GameAudio.Route(jumpSfx);jumpSfx.Play(); }
    }

    void AdvanceJump(float dt)
    {
        float g = gravity ? GravityAcceleration : 0;
        float timeToApex = g > 0 ? jumpForce / g : float.PositiveInfinity;
        float upTime = Mathf.Min(dt, timeToApex);
        var flags = controller.Move(Vector3.up * (jumpForce * upTime - .5f * g * upTime * upTime));
        if ((flags & CollisionFlags.Above) != 0) { jumpForce = 0; _velocity.y = 0; return; }
        if (timeToApex > dt) { jumpForce = Mathf.Max(0, jumpForce - g * dt); return; }

        // Split at the real apex so even very high gravity cannot skip the
        // upward sweep/ceiling contact when rise and fall fit in one fixed tick.
        jumpForce = 0;
        float remaining = Mathf.Max(0, dt - upTime);
        float accelerating = g > 0 ? Mathf.Min(remaining, FallSpeedLimit / g) : remaining;
        float down = .5f * g * accelerating * accelerating + FallSpeedLimit * (remaining - accelerating);
        _velocity.y = Mathf.Min(g * remaining, FallSpeedLimit);
        if (remaining > 0 && (controller.Move(Vector3.down * down) & CollisionFlags.Below) != 0)
        { _velocity.y = 0; isJumping = false; }
    }
    void FixedUpdate()
    {
        if (titanSuspended || controller == null || !controller.enabled) return;
        if (enabledMovement)
        {
            Vector3 dir = new Vector3(move.x, 0f, move.z).normalized;

            if (dir.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref smoothVelocity, turnSmoothTime);

                if (playerAnimator != null) playerAnimator.SetBool("isRunning", true);
                //  swordAnimator.SetBool("isRunning", true);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                controller.Move(moveDir.normalized * moveSpeed * Time.fixedDeltaTime);
            }
            else
            {
                if (playerAnimator != null) playerAnimator.SetBool("isRunning", false);
            }
        }
        TickJumpRequest(Time.fixedDeltaTime);
        TickVerticalMotion(Time.fixedDeltaTime);
    }

    void TickVerticalMotion(float dt)
    {
        if (jumpForce > 0) { AdvanceJump(dt); return; }
        if (titanReleaseUp > 0)
        {
            var flags = controller.Move(Vector3.up * titanReleaseUp * dt);
            titanReleaseUp -= GravityAcceleration * dt;
            if ((flags & CollisionFlags.Above) != 0) titanReleaseUp = 0;
            if (titanReleaseUp <= 0) { _velocity.y = Mathf.Min(-titanReleaseUp, FallSpeedLimit); titanReleaseUp = 0; }
        }
        else if (gravity)
        {
            Gravity(dt);
        }



    }

    void Gravity(float dt)
    {
        // Windup does not disable gravity. Native support clears stale fall speed.
        if (controller.isGrounded) _velocity.y = 0;
        _velocity.y = Mathf.Clamp(_velocity.y + GravityAcceleration * dt, 0f, FallSpeedLimit);
        var flags = controller.Move(-_velocity * dt);
        if ((flags & CollisionFlags.Below) != 0)
        {
            _velocity.y = 0;
            if (!JumpPending) isJumping = false;
        }
    }

    public void SlowDownGravity(float value)
    {
        if (!float.IsNaN(value) && !float.IsInfinity(value)) _velocity.y = Mathf.Max(0, _velocity.y - value);
    }


    public void ResetGravity(bool option)
    {
        gravity = option;
        CancelJump();
        _velocity.y = 0;
        titanReleaseUp = 0;
        //reset gravity
        // stop doing gravity
    }
    public void zeroGravity()
    {
        // Ground support clears falling, not an upward release/throw.
        _velocity.y = 0;
    }

    public void toggleGravity(bool val)
    {
        gravity = val;
    }

    public bool CheckGround()
    {
        Collider[] coli = Physics.OverlapBox(groundCheck.position, new Vector3(0.1f, 0.4f, 0.1f), Quaternion.identity, targetLayer);
        // found something with a ground layer iwthin our feet
        if (coli.Length >= 1)
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
