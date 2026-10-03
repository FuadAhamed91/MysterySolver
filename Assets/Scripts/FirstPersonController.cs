using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CharacterController-driven first-person movement and mouse look using the Input System.
/// Reads the "Move", "Look" and "Sprint" actions from the attached PlayerInput.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform that pitches up and down (usually the Main Camera).")]
    public Transform CameraPivot;

    [Header("Movement")]
    public float WalkSpeed = 2.4f;
    public float SprintSpeed = 4.0f;
    [Tooltip("How quickly horizontal velocity reaches its target (higher = snappier).")]
    public float Acceleration = 12f;
    public float Gravity = -19.62f;
    [Tooltip("Small downward velocity applied while grounded so the controller stays snapped to the floor.")]
    public float GroundedStickVelocity = -2f;

    [Header("Look")]
    [Tooltip("Degrees per pixel of mouse movement.")]
    public float MouseSensitivity = 0.08f;
    [Tooltip("Degrees per second at full gamepad stick deflection.")]
    public float GamepadLookSpeed = 160f;
    public float MinPitch = -85f;
    public float MaxPitch = 85f;

    public bool InputActive { get; private set; } = true;
    public bool IsGrounded { get; private set; }
    /// <summary>Frame on which input was last re-enabled; lets other systems ignore the button press that caused it.</summary>
    public int InputEnabledFrame { get; private set; } = -1;

    CharacterController controller;
    InputAction moveAction;
    InputAction lookAction;
    InputAction sprintAction;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float pitch;
    bool wantCursorLocked = true;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (CameraPivot == null && Camera.main != null)
            CameraPivot = Camera.main.transform;
    }

    void Start()
    {
        // Resolved in Start: PlayerInput may swap in a private copy of its actions during OnEnable.
        var actions = GetComponent<PlayerInput>().actions;
        moveAction = actions.FindAction("Move", true);
        lookAction = actions.FindAction("Look", true);
        sprintAction = actions.FindAction("Sprint", false);

        if (CameraPivot != null)
        {
            pitch = CameraPivot.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }
        SetCursorLocked(true);
    }

    void Update()
    {
        if (moveAction == null)
            return;

        // Browsers (and the Editor) release the pointer on Esc and only grant it back on a click.
        if (wantCursorLocked && Cursor.lockState != CursorLockMode.Locked &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        UpdateMovement();
        if (InputActive)
            UpdateLook();
    }

    void UpdateMovement()
    {
        float dt = Time.deltaTime;
        IsGrounded = controller.isGrounded;
        if (IsGrounded && verticalVelocity < 0f)
            verticalVelocity = GroundedStickVelocity;

        Vector2 input = InputActive ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        input = Vector2.ClampMagnitude(input, 1f);
        bool sprinting = InputActive && sprintAction != null && sprintAction.IsPressed();
        float speed = sprinting ? SprintSpeed : WalkSpeed;

        Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, target, 1f - Mathf.Exp(-Acceleration * dt));

        verticalVelocity += Gravity * dt;
        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
    }

    void UpdateLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();
        bool fromGamepad = lookAction.activeControl != null && lookAction.activeControl.device is Gamepad;
        if (!fromGamepad && Cursor.lockState != CursorLockMode.Locked)
            return; // a free cursor (before the first click in a browser) shouldn't spin the camera
        Vector2 delta = fromGamepad ? look * (GamepadLookSpeed * Time.deltaTime) : look * MouseSensitivity;

        transform.Rotate(0f, delta.x, 0f, Space.Self);
        pitch = Mathf.Clamp(pitch - delta.y, MinPitch, MaxPitch);
        if (CameraPivot != null)
            CameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>Enables or disables movement and look (e.g. while inspecting an item or reading a modal).</summary>
    public void SetInputActive(bool active)
    {
        if (active && !InputActive)
            InputEnabledFrame = Time.frameCount;
        InputActive = active;
    }

    public void SetCursorLocked(bool locked)
    {
        wantCursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            SetCursorLocked(wantCursorLocked);
    }
}
