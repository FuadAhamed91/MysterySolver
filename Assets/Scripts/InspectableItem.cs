using UnityEngine;

/// <summary>
/// A world prop that can be picked up and examined in front of the camera.
/// Caches its resting pose so it can be returned exactly, and reports its clue to the case manager.
/// </summary>
[DisallowMultipleComponent]
public class InspectableItem : MonoBehaviour, IInteractable
{
    [Header("Content")]
    public string ItemName = "Unknown Object";
    [TextArea(3, 10)]
    public string ItemDescription = "";
    [Tooltip("Clue registered with the CaseDeductionManager on first inspection. Leave empty for flavour props.")]
    public string ClueID = "";
    [Tooltip("One-line summary written into the Case Board evidence list.")]
    public string NotebookNote = "";

    [Tooltip("False for fixtures (e.g. part of the mechanism): the player examines it in place instead of picking it up.")]
    public bool CanPickUp = true;

    [Header("Presentation")]
    [Tooltip("Where the item's visual centre sits relative to the camera's InspectionAnchor (anchor space).")]
    public Vector3 DisplayOffset = Vector3.zero;
    [Tooltip("Initial orientation relative to the InspectionAnchor when picked up.")]
    public Vector3 DisplayEulerRotation = Vector3.zero;

    public string PromptMessage => CanPickUp ? $"[E] Inspect {ItemName}" : $"[E] Examine {ItemName}";
    public bool IsInspecting { get; private set; }

    /// <summary>World-space resting pose captured when inspection began.</summary>
    public Vector3 OriginPosition { get; private set; }
    public Quaternion OriginRotation { get; private set; }
    public Transform OriginParent { get; private set; }

    /// <summary>Pivot-to-visual-centre vector expressed in the item's rotation frame (unscaled world units).</summary>
    public Vector3 PivotToCenter { get; private set; }

    Collider[] colliders;

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public void Interact(InspectionRaycaster interactor)
    {
        if (IsInspecting || interactor == null)
            return;

        if (!interactor.BeginInspection(this))
            return;

        if (!string.IsNullOrEmpty(ClueID) && CaseDeductionManager.Instance != null)
            CaseDeductionManager.Instance.RegisterClue(ClueID, ItemName, NotebookNote);
    }

    /// <summary>Called by the raycaster before it takes control of the transform.</summary>
    public void OnInspectionStarted()
    {
        OriginPosition = transform.position;
        OriginRotation = transform.rotation;
        OriginParent = transform.parent;
        PivotToCenter = Quaternion.Inverse(transform.rotation) * (ComputeVisualBounds().center - transform.position);

        SetCollidersEnabled(false);
        IsInspecting = true;
    }

    /// <summary>Called by the raycaster once the item is back at its resting pose.</summary>
    public void OnInspectionEnded()
    {
        transform.SetParent(OriginParent, true);
        transform.SetPositionAndRotation(OriginPosition, OriginRotation);
        SetCollidersEnabled(true);
        IsInspecting = false;
    }

    Bounds ComputeVisualBounds()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    void SetCollidersEnabled(bool enabled)
    {
        foreach (var c in colliders)
            if (c != null)
                c.enabled = enabled;
    }
}
