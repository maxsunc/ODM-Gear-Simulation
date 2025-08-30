using UnityEngine;

public class CameraJitterFix : MonoBehaviour
{
    [Header("Jitter Fix Settings")]
    public bool enablePositionInterpolation = true;
    public bool enableRotationInterpolation = true;
    public float interpolationFactor = 0.1f;
    
    [Header("Frame Rate Stabilization")]
    public bool lockFramerate = true;
    public int targetFramerate = 60;
    
    [Header("Physics Settings")]
    public bool useFixedDeltaTime = true;
    public float fixedDeltaTime = 0.02f; // 50 FPS physics
    
    [Header("Debug")]
    public bool showFrameRate = true;
    
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float frameRate;
    private float frameTimer;
    
    void Start()
    {
        // Apply frame rate settings
        if (lockFramerate)
        {
            Application.targetFrameRate = targetFramerate;
            QualitySettings.vSyncCount = 1; // Enable VSync for smoother frame delivery
        }
        
        // Apply physics settings
        if (useFixedDeltaTime)
        {
            Time.fixedDeltaTime = fixedDeltaTime;
        }
        
        // Initialize interpolation
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
    
    void Update()
    {
        // Calculate frame rate
        if (showFrameRate)
        {
            frameTimer += Time.unscaledDeltaTime;
            if (frameTimer >= 1f)
            {
                frameRate = 1f / Time.unscaledDeltaTime;
                frameTimer = 0f;
            }
        }
    }
    
    void LateUpdate()
    {
        // Apply interpolation to reduce jitter
        if (enablePositionInterpolation)
        {
            Vector3 targetPos = transform.position;
            transform.position = Vector3.Lerp(lastPosition, targetPos, interpolationFactor);
            lastPosition = transform.position;
        }
        
        if (enableRotationInterpolation)
        {
            Quaternion targetRot = transform.rotation;
            transform.rotation = Quaternion.Lerp(lastRotation, targetRot, interpolationFactor);
            lastRotation = transform.rotation;
        }
    }
    
    void OnGUI()
    {
        if (showFrameRate)
        {
            GUI.Label(new Rect(10, 10, 200, 30), $"FPS: {frameRate:F1}");
            GUI.Label(new Rect(10, 30, 200, 30), $"Delta Time: {Time.unscaledDeltaTime * 1000:F1}ms");
        }
    }
}