using UnityEngine;
using UnityEngine.InputSystem;
public class SimplePlayerMovement : MonoBehaviour
{
    public float moveForce = 16f;
    private Rigidbody rb;
    private Vector3 moveDirection;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    void Update()
    {
        moveDirection = Vector3.zero;
        if (Keyboard.current == null)
            return;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            moveDirection += Vector3.forward;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            moveDirection += Vector3.back;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            moveDirection += Vector3.left;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            moveDirection += Vector3.right;
        moveDirection = moveDirection.normalized;
    }
    void FixedUpdate()
    {
        rb.AddForce(moveDirection * moveForce, ForceMode.Acceleration);
    }
}