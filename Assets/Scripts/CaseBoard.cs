using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tab-toggled Case Board: lists the evidence found so far and lets the player cycle answers to
/// the CaseDeductionManager's questions, then lodge an accusation.
/// </summary>
public class CaseBoard : MonoBehaviour
{
    [Header("References")]
    public CaseDeductionManager Case;
    public FirstPersonController Player;
    public PlayerInput PlayerInput;
    public InspectionRaycaster Raycaster;

    [Header("UI")]
    public GameObject Panel;
    public TMP_Text TitleText;
    public TMP_Text EvidenceText;
    public TMP_Text[] QuestionTexts;
    public TMP_Text[] AnswerTexts;
    public Button[] PrevButtons;
    public Button[] NextButtons;
    public Button AccuseButton;
    public TMP_Text AccuseButtonText;
    public TMP_Text StatusText;
    public TMP_Text FeedbackText;
    [Tooltip("Optional: leaves the case and returns to the case menu.")]
    public Button LeaveButton;

    public bool IsOpen => Panel != null && Panel.activeSelf;

    InputAction toggleAction;
    InputAction cancelAction;
    int[] answers;        // per question: the picked position in the shuffled list, or -1
    int[][] optionOrder;  // per question: shuffled position -> index into Options
    int openedFrame;

    void Awake()
    {
        if (Panel != null) Panel.SetActive(false);
    }

    void Start()
    {
        toggleAction = PlayerInput.actions.FindAction("CaseBoard", true);
        cancelAction = PlayerInput.actions.FindAction("Cancel", true);

        if (TitleText != null && Case.Definition != null)
            TitleText.text = $"CASE BOARD  ·  {Case.Definition.Title.ToUpperInvariant()}";

        answers = new int[Case.Questions.Length];
        for (int i = 0; i < answers.Length; i++)
            answers[i] = -1;

        // Shuffle each question's options for this visit. The case files list the right answer first,
        // so without this a single click on ">" would give every answer away.
        optionOrder = new int[Case.Questions.Length][];
        for (int q = 0; q < optionOrder.Length; q++)
        {
            int n = Case.Questions[q].Options.Length;
            var order = new int[n];
            for (int k = 0; k < n; k++)
                order[k] = k;
            for (int k = n - 1; k > 0; k--)
            {
                int j = UnityEngine.Random.Range(0, k + 1);
                (order[k], order[j]) = (order[j], order[k]);
            }
            optionOrder[q] = order;
        }

        for (int i = 0; i < Case.Questions.Length && i < AnswerTexts.Length; i++)
        {
            int q = i;
            PrevButtons[i].onClick.AddListener(() => Cycle(q, -1));
            NextButtons[i].onClick.AddListener(() => Cycle(q, +1));
            QuestionTexts[i].text = $"{i + 1}. {Case.Questions[i].Prompt}";
        }
        AccuseButton.onClick.AddListener(SubmitAccusation);
        if (LeaveButton != null)
            LeaveButton.onClick.AddListener(() => Case.GoToMenu());
        if (FeedbackText != null) FeedbackText.text = "";
    }

    void Update()
    {
        if (toggleAction == null)
            return;

        if (IsOpen)
        {
            if (Time.frameCount > openedFrame &&
                (toggleAction.WasPressedThisFrame() || cancelAction.WasPressedThisFrame() || Case.Outcome != CaseDeductionManager.CaseOutcome.Open))
                Close();
            return;
        }

        bool busy = (Raycaster != null && Raycaster.IsInspecting) || Case.VerdictOpen
                    || (Player != null && !Player.InputActive);
        if (!busy && toggleAction.WasPressedThisFrame())
            Open();
    }

    public void Open()
    {
        openedFrame = Time.frameCount;
        Panel.SetActive(true);
        if (Player != null)
        {
            Player.SetInputActive(false);
            Player.SetCursorLocked(false);
        }
        Refresh();
    }

    public void Close()
    {
        Panel.SetActive(false);
        if (Case.VerdictOpen)
            return; // the verdict screen owns the player now
        if (Player != null)
        {
            Player.SetCursorLocked(true);
            Player.SetInputActive(true);
        }
    }

    void Cycle(int question, int dir)
    {
        int count = Case.Questions[question].Options.Length;
        int current = answers[question] < 0 ? (dir > 0 ? -1 : 0) : answers[question];
        answers[question] = (current + dir + count) % count;
        if (FeedbackText != null) FeedbackText.text = "";
        Refresh();
    }

    void SubmitAccusation()
    {
        if (!Case.CanAccuse || System.Array.IndexOf(answers, -1) >= 0)
            return;

        var picked = new int[answers.Length];
        for (int i = 0; i < answers.Length; i++)
            picked[i] = optionOrder[i][answers[i]];
        int correct = Case.Accuse(picked);
        if (correct < 0 || Case.Outcome != CaseDeductionManager.CaseOutcome.Open)
            return; // solved or failed: the verdict screen takes over

        if (FeedbackText != null)
        {
            string left = Case.AccusationsLeft == 1 ? "1 accusation" : $"{Case.AccusationsLeft} accusations";
            FeedbackText.text = $"<color=#FF8A7A>The Inspector shakes his head.</color> Only <b>{correct} of {answers.Length}</b> " +
                                $"of your conclusions hold up. You have {left} left.";
        }
        Refresh();
    }

    void Refresh()
    {
        var sb = new StringBuilder();
        sb.Append($"<color=#7A1420><b>EVIDENCE  {Case.DiscoveredCount}/{Case.TotalClues}</b></color>\n\n");
        if (Case.DiscoveredCount == 0)
            sb.Append("<alpha=#88>Nothing yet. Search the tower: look closely at anything you can pick up, and at the mechanism itself.");
        foreach (var clue in Case.Discovered)
            sb.Append($"<b>{clue.Title}</b>\n<color=#4A3A2A>{clue.Note}</color>\n\n");
        EvidenceText.text = sb.ToString();

        for (int i = 0; i < answers.Length && i < AnswerTexts.Length; i++)
            AnswerTexts[i].text = answers[i] < 0 ? "<alpha=#66>choose...<alpha=#FF>" : Case.Questions[i].Options[optionOrder[i][answers[i]]];

        bool complete = System.Array.IndexOf(answers, -1) < 0;
        AccuseButton.interactable = Case.CanAccuse && complete;
        if (AccuseButtonText != null)
            AccuseButtonText.text = Case.CanAccuse ? "ACCUSE" : "LOCKED";

        if (StatusText != null)
        {
            StatusText.text = !Case.CanAccuse
                ? $"Find at least {Case.CluesRequiredToAccuse} pieces of evidence before you accuse anyone ({Case.DiscoveredCount}/{Case.CluesRequiredToAccuse})."
                : !complete
                    ? $"Answer all four questions.   Accusations left: {Case.AccusationsLeft}/{Case.MaxAccusations}"
                    : $"Ready.   Accusations left: {Case.AccusationsLeft}/{Case.MaxAccusations}. Get one wrong and you won't be told which.";
        }
    }
}
