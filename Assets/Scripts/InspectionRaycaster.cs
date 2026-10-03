using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lives on the Main Camera. Raycasts from screen centre for IInteractables, and runs the
/// pick-up / rotate / put-back loop for InspectableItems.
/// Uses the "Interact", "Look", "InspectRotate" and "Cancel" actions of the player's PlayerInput.
/// </summary>
[RequireComponent(typeof(Camera))]
public class InspectionRaycaster : MonoBehaviour
{
    enum State { Idle, Holding, Returning }

    [Header("References")]
    public Transform InspectionAnchor;
    public FirstPersonController Player;
    public PlayerInput PlayerInput;
    [Tooltip("Optional soft light enabled while an item is held so it stays readable in dark corners.")]
    public Light InspectionLight;

    [Header("Raycast")]
    public float InteractRange = 3f;
    public LayerMask InteractMask = ~0;

    [Header("Inspection")]
    [Tooltip("Exponential smoothing rate for moving the item to and from the anchor.")]
    public float MoveSharpness = 10f;
    [Tooltip("Degrees per pixel of mouse drag.")]
    public float MouseRotateSpeed = 0.35f;
    [Tooltip("Degrees per second at full gamepad stick deflection.")]
    public float GamepadRotateSpeed = 180f;

    [Header("UI")]
    public GameObject Crosshair;
    public TMP_Text PromptText;
    public GameObject InspectionPanel;
    public TMP_Text InspectTitleText;
    public TMP_Text InspectDescriptionText;

    public bool IsInspecting => state != State.Idle;

    Camera cam;
    InputAction interactAction;
    InputAction lookAction;
    InputAction rotateAction;
    InputAction cancelAction;

    State state = State.Idle;
    InspectableItem held;
    Quaternion heldLocalRotation;
    int inspectStartFrame;
    IInteractable currentTarget;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (PlayerInput == null)
            PlayerInput = GetComponentInParent<PlayerInput>();
        if (Player == null)
            Player = GetComponentInParent<FirstPersonController>();

        if (InspectionPanel != null) InspectionPanel.SetActive(false);
        if (InspectionLight != null) InspectionLight.enabled = false;
        SetPrompt(null);
    }

    void Start()
    {
        // Resolved in Start: PlayerInput may swap in a private copy of its actions during OnEnable.
        var actions = PlayerInput.actions;
        interactAction = actions.FindAction("Interact", true);
        lookAction = actions.FindAction("Look", true);
        rotateAction = actions.FindAction("InspectRotate", true);
        cancelAction = actions.FindAction("Cancel", true);
    }

    void Update()
    {
        if (interactAction == null)
            return;
        switch (state)
        {
            case State.Idle: UpdateTargeting(); break;
            case State.Holding: UpdateHolding(); break;
            case State.Returning: UpdateReturning(); break;
        }
    }

    // ------------------------------------------------------------------ targeting

    void UpdateTargeting()
    {
        bool canInteract = Player == null || (Player.InputActive && Time.frameCount > Player.InputEnabledFrame);
        if (Crosshair != null) Crosshair.SetActive(Player == null || Player.InputActive);
        if (!canInteract)
        {
            currentTarget = null;
            SetPrompt(null);
            return;
        }

        currentTarget = null;
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            SetPrompt("Click to look around"); // browsers need a click before they hand over the mouse
            return;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, InteractRange, InteractMask, QueryTriggerInteraction.Ignore))
            currentTarget = hit.collider.GetComponentInParent<IInteractable>();

        SetPrompt(currentTarget?.PromptMessage);

        if (currentTarget != null && interactAction.WasPressedThisFrame())
            currentTarget.Interact(this);
    }

    /// <summary>Takes control of an item and starts lifting it to the anchor. Returns false if busy.</summary>
    public bool BeginInspection(InspectableItem item)
    {
        if (state != State.Idle || item == null || InspectionAnchor == null)
            return false;

        held = item;
        held.OnInspectionStarted();
        if (held.CanPickUp)
            held.transform.SetParent(InspectionAnchor, true);
        heldLocalRotation = Quaternion.Euler(held.DisplayEulerRotation);
        inspectStartFrame = Time.frameCount;
        state = State.Holding;

        if (Player != null) Player.SetInputActive(false);
        if (InspectionLight != null) InspectionLight.enabled = held.CanPickUp;
        if (Crosshair != null) Crosshair.SetActive(false);
        SetPrompt(null);

        if (InspectionPanel != null) InspectionPanel.SetActive(true);
        if (InspectTitleText != null) InspectTitleText.text = held.ItemName;
        if (InspectDescriptionText != null) InspectDescriptionText.text = held.ItemDescription;
        return true;
    }

    // ------------------------------------------------------------------ holding

    void UpdateHolding()
    {
        bool fresh = Time.frameCount == inspectStartFrame;
        if (!fresh && (cancelAction.WasPressedThisFrame() || interactAction.WasPressedThisFrame()))
        {
            EndInspection();
            return;
        }
        if (!held.CanPickUp)
            return; // examined in place

        if (rotateAction.IsPressed())
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            bool fromGamepad = lookAction.activeControl != null && lookAction.activeControl.device is Gamepad;
            Vector2 delta = fromGamepad ? look * (GamepadRotateSpeed * Time.deltaTime) : look * MouseRotateSpeed;
            // Anchor-local axes are camera axes: drag right spins about camera up, drag up tips about camera right.
            heldLocalRotation = Quaternion.AngleAxis(-delta.x, Vector3.up)
                              * Quaternion.AngleAxis(delta.y, Vector3.right)
                              * heldLocalRotation;
        }

        float t = 1f - Mathf.Exp(-MoveSharpness * Time.deltaTime);
        Transform tr = held.transform;
        Vector3 targetPos = held.DisplayOffset - heldLocalRotation * held.PivotToCenter;
        tr.localPosition = Vector3.Lerp(tr.localPosition, targetPos, t);
        tr.localRotation = Quaternion.Slerp(tr.localRotation, heldLocalRotation, t);
    }

    /// <summary>Starts returning the held item to where it was picked up.</summary>
    public void EndInspection()
    {
        if (state != State.Holding)
            return;

        if (InspectionPanel != null) InspectionPanel.SetActive(false);
        if (InspectionLight != null) InspectionLight.enabled = false;

        if (!held.CanPickUp)
        {
            FinishInspection();
            return;
        }
        held.transform.SetParent(null, true);
        state = State.Returning;
    }

    // ------------------------------------------------------------------ returning

    void UpdateReturning()
    {
        float t = 1f - Mathf.Exp(-MoveSharpness * 1.3f * Time.deltaTime);
        Transform tr = held.transform;
        tr.position = Vector3.Lerp(tr.position, held.OriginPosition, t);
        tr.rotation = Quaternion.Slerp(tr.rotation, held.OriginRotation, t);

        bool arrived = (tr.position - held.OriginPosition).sqrMagnitude < 0.0005f * 0.0005f
                       || ((tr.position - held.OriginPosition).sqrMagnitude < 0.003f * 0.003f
                           && Quaternion.Angle(tr.rotation, held.OriginRotation) < 0.5f);
        if (arrived)
            FinishInspection();
    }

    void FinishInspection()
    {
        held.OnInspectionEnded();
        held = null;
        state = State.Idle;

        if (Player != null) Player.SetInputActive(true);
        if (CaseDeductionManager.Instance != null)
            CaseDeductionManager.Instance.NotifyInspectionClosed();
    }

    void SetPrompt(string message)
    {
        if (PromptText == null)
            return;
        bool show = !string.IsNullOrEmpty(message);
        PromptText.gameObject.SetActive(show);
        if (show) PromptText.text = message;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * InteractRange);
    }
}
