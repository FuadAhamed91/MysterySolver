using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Title card shown when a case starts: case number, title, location and the setup. Holds the player until they
/// click or press E, then fades away.
/// </summary>
public class CaseIntro : MonoBehaviour
{
    public CaseDeductionManager Case;
    public FirstPersonController Player;
    public PlayerInput PlayerInput;

    [Header("UI")]
    public CanvasGroup Group;
    public TMP_Text NumberText;
    public TMP_Text TitleText;
    public TMP_Text LocationText;
    public TMP_Text BodyText;
    public TMP_Text PromptText;

    public float FadeSeconds = 0.6f;

    public bool IsShowing { get; private set; }

    InputAction interactAction;
    float fade = -1f;
    float shownAt;

    void Start()
    {
        var def = Case != null ? Case.Definition : null;
        if (def == null || Group == null)
        {
            if (Group != null) Group.gameObject.SetActive(false);
            return;
        }

        interactAction = PlayerInput != null ? PlayerInput.actions.FindAction("Interact", false) : null;
        NumberText.text = $"CASE {def.Number}";
        TitleText.text = def.Title;
        LocationText.text = def.Location;
        BodyText.text = def.Intro;
        PromptText.text = "Click or press E to begin";

        Group.gameObject.SetActive(true);
        Group.alpha = 1f;
        IsShowing = true;
        shownAt = Time.unscaledTime;
        if (Player != null) Player.SetInputActive(false);
    }

    void Update()
    {
        if (fade >= 0f)
        {
            fade += Time.unscaledDeltaTime / FadeSeconds;
            Group.alpha = 1f - Mathf.Clamp01(fade);
            if (fade >= 1f)
            {
                Group.gameObject.SetActive(false);
                fade = -1f;
                enabled = false;
            }
            return;
        }
        if (!IsShowing || Time.unscaledTime - shownAt < 0.4f)
            return;

        bool go = (interactAction != null && interactAction.WasPressedThisFrame())
                  || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                  || (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame));
        if (go)
            Dismiss();
    }

    /// <summary>Hides the card and hands control to the player.</summary>
    public void Dismiss()
    {
        if (!IsShowing)
            return;
        IsShowing = false;
        fade = 0f;
        if (Player != null)
        {
            Player.SetInputActive(true);
            Player.SetCursorLocked(true); // the click that got us here is the user gesture browsers need for pointer lock
        }
    }
}
