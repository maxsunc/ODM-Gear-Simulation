using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCam : MonoBehaviour
{
    public Transform rotator, movementRotator;
    public float scrollValue;
    // scroll to go in and out 

    private float xRotation, yRotation;
 
    public float mousesense;
    
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }
    // Update is called once per frame
    void Update()
    {
        
            CamWork();
            
       
    }

   private void CamWork()
    {
        float mouseX = Input.GetAxis("Mouse X") * mousesense * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mousesense * Time.deltaTime;
        // Multiplied by mousesense and time.delta because of update

        xRotation -= mouseY;
        yRotation -= mouseX;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        rotator.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        movementRotator.Rotate(Vector3.up * -mouseX);
    }

}
