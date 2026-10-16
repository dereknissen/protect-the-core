
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GTAPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 12f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("GTA Style Camera")]
    public Transform cameraTransform;
    public float cameraDistance = 5f;
    public float cameraHeight = 2f;
    public float mouseSensitivity = 0.15f;
    public float minPitch = -25f;
    public float maxPitch = 60f;
    public float cameraSmoothness = 12f;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float cameraYaw;
    private float cameraPitch = 12f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            cameraYaw = cameraTransform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (cameraTransform == null || Keyboard.current == null)
            return;

        // Mouse camera control
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                cameraYaw += mouseDelta.x * mouseSensitivity;
                cameraPitch -= mouseDelta.y * mouseSensitivity;
                cameraPitch = Mathf.Clamp(
                    cameraPitch, minPitch, maxPitch);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // WASD movement relative to the camera
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed) vertical += 1f;

        Vector3 forward =
            Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.forward;
        Vector3 right =
            Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.right;

        Vector3 moveDirection =
            (forward * vertical + right * horizontal).normalized;

        controller.Move(moveDirection * moveSpeed * Time.deltaTime);

        // Face the direction of movement
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        // Gravity and jump
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        if (controller.isGrounded &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity.y =
                Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (cameraTransform == null)
            return;

        Quaternion cameraRotation =
            Quaternion.Euler(cameraPitch, cameraYaw, 0f);

        Vector3 targetPosition =
            transform.position
            + Vector3.up * cameraHeight
            - cameraRotation * Vector3.forward * cameraDistance;

        cameraTransform.position = Vector3.Lerp(
            cameraTransform.position,
            targetPosition,
            cameraSmoothness * Time.deltaTime);

        Vector3 lookTarget =
            transform.position + Vector3.up * cameraHeight;

        Quaternion lookRotation = Quaternion.LookRotation(
            lookTarget - cameraTransform.position);

        cameraTransform.rotation = Quaternion.Slerp(
            cameraTransform.rotation,
            lookRotation,
            cameraSmoothness * Time.deltaTime);
    }
}
