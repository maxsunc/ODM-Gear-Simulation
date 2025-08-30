using UnityEngine;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 2f, 0); // Offset from target position
    
    [Header("Camera Settings")]
    public float distance = 6f;
    public float minDistance = 2f;
    public float maxDistance = 12f;
    
    [Header("Mouse Input")]
    public float mouseSensitivity = 2f;
    public bool invertY = false;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;
    
    [Header("Smoothing")]
    public float rotationSmoothing = 5f;
    public float positionSmoothing = 8f;
    
    [Header("Collision")]
    public LayerMask collisionLayers = -1;
    public float collisionBuffer = 0.3f;
    
    [Header("Dynamic FOV")]
    public float baseFOV = 60f;
    public float maxFOV = 90f;
    public float speedThreshold = 10f; // Speed at which max FOV is reached
    public float fovSmoothSpeed = 3f;
    
    // Private variables
    private float currentX = 0f;
    private float currentY = 0f;
    private float currentDistance;
    private Camera cam;
    private Vector3 lastTargetPosition;
    private float currentSpeed;
    
    void Start()
    {
        if (target == null)
        {
            Debug.LogError("No target assigned!");
            return;
        }
        
        // Get camera component
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("No Camera component found!");
            return;
        }
        
        // Initialize rotation to face target's forward direction
        Vector3 angles = target.eulerAngles;
        currentX = angles.y;
        currentY = 0f;
        currentDistance = distance;
        
        // Initialize FOV and speed tracking
        cam.fieldOfView = baseFOV;
        lastTargetPosition = target.position;
        
        // Lock cursor
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
    }
    
    void Update()
    {
        if (target == null) return;
        
        HandleInput();
        
        // Toggle cursor with Escape
        // if (Input.GetKeyDown(KeyCode.Escape))
        // {
        //     Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? 
        //                       CursorLockMode.None : CursorLockMode.Locked;
        //     Cursor.visible = !Cursor.visible;
        // }
    }
    
    void FixedUpdate()
    {
        if (target == null) return;
        
        UpdateSpeed();
        UpdateCamera();
        UpdateFOV();
    }
    
    void HandleInput()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        if (invertY) mouseY = -mouseY;
        
        // Update rotation
        currentX += mouseX;
        currentY -= mouseY;
        currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);
        
        // Handle zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(distance - scroll * 2f, minDistance, maxDistance);
        }
    }
    
    void UpdateSpeed()
    {
        // Calculate target's current speed
        Vector3 velocity = (target.position - lastTargetPosition) / Time.fixedDeltaTime;
        currentSpeed = velocity.magnitude;
        lastTargetPosition = target.position;
    }
    
    void UpdateCamera()
    {
        // Target position with offset
        Vector3 targetPosition = target.position + offset;
        
        // Calculate rotation
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        
        // Calculate desired position
        Vector3 direction = rotation * Vector3.back;
        Vector3 desiredPosition = targetPosition + direction * distance;
        
        // Check for collisions
        float finalDistance = CheckCollision(targetPosition, direction);
        Vector3 finalPosition = targetPosition + direction * finalDistance;
        
        // Smooth the distance using fixedDeltaTime
        currentDistance = Mathf.Lerp(currentDistance, finalDistance, Time.fixedDeltaTime * positionSmoothing);
        finalPosition = targetPosition + direction * currentDistance;
        
        // Apply position smoothing using fixedDeltaTime
        transform.position = Vector3.Lerp(transform.position, finalPosition, Time.fixedDeltaTime * positionSmoothing);
        
        // Simple rotation - no smoothing to avoid jitter
        transform.rotation = rotation;
    }
    
    void UpdateFOV()
    {
        // Calculate target FOV based on speed
        float speedRatio = Mathf.Clamp01(currentSpeed / speedThreshold);
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio);
        
        // Smoothly interpolate to target FOV
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.fixedDeltaTime * fovSmoothSpeed);
    }
    
    float CheckCollision(Vector3 targetPos, Vector3 direction)
    {
        RaycastHit hit;
        float checkDistance = distance + collisionBuffer;
        
        if (Physics.Raycast(targetPos, direction, out hit, checkDistance, collisionLayers))
        {
            return Mathf.Max(hit.distance - collisionBuffer, minDistance);
        }
        
        return distance;
    }
    
    // Public methods
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetDistance(float newDistance)
    {
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
    }
    
    public void SetBaseFOV(float newBaseFOV)
    {
        baseFOV = newBaseFOV;
    }
    
    public void SetMaxFOV(float newMaxFOV)
    {
        maxFOV = newMaxFOV;
    }
}