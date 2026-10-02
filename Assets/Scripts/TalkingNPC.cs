using UnityEngine;

/// <summary>
/// Shared behaviour for characters the player can talk to: a slow idle breath, a head that turns to follow the
/// player, a one-time call-out subtitle when the player first comes near, and an interaction prompt.
/// Subclasses supply the name and the conversation.
/// </summary>
public abstract class TalkingNPC : MonoBehaviour, IInteractable
{
    [Header("References")]
    public DialogueUI Dialogue;
    public Transform HeadPivot;
    public Transform Body;
    public Transform PlayerCamera;

    [Header("Behaviour")]
    public float NoticeDistance = 4.5f;
    public float MaxHeadYaw = 70f;
    public float HeadTurnSpeed = 4f;

    protected const string Unknown = "???";

    /// <summary>True once the player has learned this character's name.</summary>
    protected bool Introduced;

    protected abstract string DisplayName { get; }
    protected abstract string StrangerPrompt { get; }
    protected abstract string CallOutLine { get; }

    /// <summary>Starts the conversation; the DialogueUI is already open.</summary>
    protected abstract void BeginConversation();

    protected CaseDeductionManager Case => CaseDeductionManager.Instance;
    protected string Speaker => Introduced ? DisplayName : Unknown;

    public string PromptMessage => Introduced ? $"[E] Talk to {DisplayName}" : $"[E] {StrangerPrompt}";

    bool calledOut;
    Quaternion headRest;
    Vector3 bodyScale;
    float breathOffset;

    protected virtual void Start()
    {
        if (PlayerCamera == null && Camera.main != null) PlayerCamera = Camera.main.transform;
        if (HeadPivot != null) headRest = HeadPivot.localRotation;
        if (Body != null) bodyScale = Body.localScale;
        breathOffset = Random.value * 10f;
    }

    protected virtual void Update()
    {
        if (Body != null)
            Body.localScale = new Vector3(bodyScale.x,
                bodyScale.y * (1f + Mathf.Sin((Time.time + breathOffset) * 1.3f) * 0.006f), bodyScale.z);

        if (PlayerCamera == null || HeadPivot == null)
            return;

        Vector3 toPlayer = PlayerCamera.position - HeadPivot.position;
        Quaternion target = headRest;
        if (toPlayer.magnitude < NoticeDistance)
        {
            // Yaw/pitch toward the player in the character's local frame, clamped to a natural range.
            Vector3 local = transform.InverseTransformDirection(toPlayer.normalized);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -MaxHeadYaw, MaxHeadYaw);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -25f, 25f);
            target = headRest * Quaternion.Euler(pitch, yaw, 0f);

            if (!calledOut && Dialogue != null && !Dialogue.IsOpen)
            {
                calledOut = true;
                Dialogue.Subtitle(Unknown, CallOutLine, 4f);
            }
        }
        HeadPivot.localRotation = Quaternion.Slerp(HeadPivot.localRotation, target,
                                                   1f - Mathf.Exp(-HeadTurnSpeed * Time.deltaTime));
    }

    public void Interact(InspectionRaycaster interactor)
    {
        if (Dialogue == null || Dialogue.IsOpen)
            return;
        Dialogue.Open(Leave);
        BeginConversation();
    }

    protected void Leave() => Dialogue.Close();
}
