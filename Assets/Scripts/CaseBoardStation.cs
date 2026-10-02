using UnityEngine;

/// <summary>The corkboard in the study: interacting with it opens the Case Board, same as pressing Tab.</summary>
public class CaseBoardStation : MonoBehaviour, IInteractable
{
    public CaseBoard Board;

    public string PromptMessage => "[E] Open Case Board";

    public void Interact(InspectionRaycaster interactor)
    {
        if (Board != null && !Board.IsOpen)
            Board.Open();
    }
}
