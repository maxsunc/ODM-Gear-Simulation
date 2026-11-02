using UnityEngine;

/// <summary>Ground-only movable human proxy for the AI lab, NOT a replacement player controller.</summary>
[RequireComponent(typeof(CharacterController))]
public class TitanSandboxTarget : MonoBehaviour
{
    public float speed = 8f;
    public float sprintSpeed = 14f;
    CharacterController controller;
    float verticalSpeed;
    void Awake() { controller = GetComponent<CharacterController>(); }
    void Update()
    {
        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) direction.z++;
        if (Input.GetKey(KeyCode.S)) direction.z--;
        if (Input.GetKey(KeyCode.D)) direction.x++;
        if (Input.GetKey(KeyCode.A)) direction.x--;
        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        verticalSpeed += Physics.gravity.y * Time.deltaTime;
        Vector3 motion = direction.normalized * (Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed);
        motion.y = verticalSpeed;
        controller.Move(motion * Time.deltaTime);
        if (transform.position.y < -10 || Mathf.Abs(transform.position.x) > 40 || Mathf.Abs(transform.position.z) > 40)
            Warp(new Vector3(0, 0, 18));
    }
    public void Warp(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        verticalSpeed = 0;
        controller.enabled = true;
    }
}
