/// <summary>
/// Anything the player can look at and press Interact on.
/// </summary>
public interface IInteractable
{
    /// <summary>Text shown under the crosshair while the object is targeted.</summary>
    string PromptMessage { get; }

    /// <summary>Called when the player presses Interact while targeting this object.</summary>
    void Interact(InspectionRaycaster interactor);
}
