using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 12f;
    public float jumpForce = 6f;

    [Header("GTA Style Camera")]
    public Transform cameraTransform;
    public float cameraDistance = 5f;
    public float cameraHeight = 2f;
    public float mouseSensitivity = 0.15f;
    public float minPitch = -25f;
    public float maxPitch = 60f;
    public float cameraSmoothness = 10f;

    [Header("Game Manager")]
    public GameManagerScript gameManagerScript;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float cameraYaw;
    private float cameraPitch = 12f;
    private bool jumpRequested;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints |= RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            cameraYaw = cameraTransform.eulerAngles.y;

        // Automatically find GameManager if not assigned
        if (gameManagerScript == null)
            gameManagerScript = FindFirstObjectByType<GameManagerScript>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Disable player input when game is over
        if (gameManagerScript != null && !gameManagerScript.isPlaying)
        {
            moveInput = Vector2.zero;
            jumpRequested = false;
            return;
        }

        if (Keyboard.current == null)
            return;

        // WASD movement
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed) vertical += 1f;

        moveInput = new Vector2(horizontal, vertical).normalized;

        // Jump request
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpRequested = true;

        // Release mouse with Escape
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null)
        {
            // Only lock mouse if game is running
            if (Mouse.current.leftButton.wasPressedThisFrame &&
                Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // GTA-style mouse camera
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();

                cameraYaw += mouseDelta.x * mouseSensitivity;
                cameraPitch -= mouseDelta.y * mouseSensitivity;

                cameraPitch = Mathf.Clamp(
                    cameraPitch, minPitch, maxPitch
                );
            }
        }
    }

    void FixedUpdate()
    {
        // Stop horizontal movement when game is over
        if (gameManagerScript != null && !gameManagerScript.isPlaying)
        {
            Vector3 velocity = rb.linearVelocity;

            rb.linearVelocity = new Vector3(
                0f,
                velocity.y,
                0f
            );

            jumpRequested = false;
            return;
        }

        Vector3 forward =
            Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.forward;

        Vector3 right =
            Quaternion.Euler(0f, cameraYaw, 0f) * Vector3.right;

        Vector3 movement =
            (forward * moveInput.y + right * moveInput.x).normalized;

        Vector3 velocityCurrent = rb.linearVelocity;

        rb.linearVelocity = new Vector3(
            movement.x * moveSpeed,
            velocityCurrent.y,
            movement.z * moveSpeed
        );

        // Rotate player toward camera direction
        if (cameraTransform != null)
        {
            Vector3 aimDirection = cameraTransform.forward;
            aimDirection.y = 0f;

            if (aimDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(aimDirection.normalized);

                rb.MoveRotation(Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                ));
            }
        }

        // Jump only when grounded
        if (jumpRequested && isGrounded)
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            isGrounded = false;
        }

        jumpRequested = false;
    }

    void LateUpdate()
    {
        if (cameraTransform == null)
            return;

        // Freeze camera movement when game is over
        if (gameManagerScript != null && !gameManagerScript.isPlaying)
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
            cameraSmoothness * Time.deltaTime
        );

        Vector3 lookTarget =
            transform.position + Vector3.up * cameraHeight;

        cameraTransform.LookAt(lookTarget);
    }

    void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                return;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}