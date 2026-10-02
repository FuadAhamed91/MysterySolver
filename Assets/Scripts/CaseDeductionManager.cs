using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene singleton that owns the case file for "The 3:15 Escapement": which clues have been found,
/// the accusation questions, how many accusations remain, and the final verdict screen.
/// </summary>
public class CaseDeductionManager : MonoBehaviour
{
    public enum CaseOutcome { Open, Solved, Failed }

    public const string CLUE_POCKET_WATCH = "CLUE_POCKET_WATCH";
    public const string CLUE_DESK_LAMP = "CLUE_DESK_LAMP";
    public const string CLUE_APOTHECARY_VIAL = "CLUE_APOTHECARY_VIAL";
    public const string CLUE_TEACUP = "CLUE_TEACUP";
    public const string CLUE_PRESCRIPTION = "CLUE_PRESCRIPTION";
    public const string CLUE_LETTER = "CLUE_LETTER";
    public const string CLUE_LOGBOOK = "CLUE_LOGBOOK";
    public const string CLUE_CHISEL = "CLUE_CHISEL";
    public const string CLUE_PENDULUM_PIN = "CLUE_PENDULUM_PIN";

    public static readonly string[] AllClues =
    {
        CLUE_POCKET_WATCH, CLUE_DESK_LAMP, CLUE_APOTHECARY_VIAL, CLUE_TEACUP, CLUE_PRESCRIPTION,
        CLUE_LETTER, CLUE_LOGBOOK, CLUE_CHISEL, CLUE_PENDULUM_PIN,
    };

    [Serializable]
    public class Question
    {
        public string Prompt;
        public string[] Options;
        public int CorrectIndex;
    }

    public struct ClueEntry
    {
        public string Id;
        public string Title;
        public string Note;
    }

    public static CaseDeductionManager Instance { get; private set; }

    [Header("Player")]
    public FirstPersonController Player;
    public PlayerInput PlayerInput;

    [Header("UI")]
    public TMP_Text TrackerText;
    public GameObject DeductionPanel;
    public TMP_Text DeductionTitleText;
    public TMP_Text DeductionBodyText;
    public TMP_Text DeductionHintText;

    [Header("Rules")]
    [Tooltip("Evidence needed before the Case Board lets the player accuse anyone.")]
    public int CluesRequiredToAccuse = 5;
    public int MaxAccusations = 3;

    [Header("Accusation")]
    public Question[] Questions =
    {
        new Question { Prompt = "Who poisoned Arthur Vance?",
            Options = new[] { "Dr. Elias Haze", "Margaret Vance", "Tobias Crane", "No one: an accident" }, CorrectIndex = 0 },
        new Question { Prompt = "When did the poison take hold?",
            Options = new[] { "2:40 AM", "3:03 AM", "3:10 AM", "3:15 AM" }, CorrectIndex = 1 },
        new Question { Prompt = "How was he killed?",
            Options = new[] { "Aconite in his tonic", "Crushed by the pendulum", "Gears sabotaged to fall", "Poisoned wax seal" }, CorrectIndex = 0 },
        new Question { Prompt = "Who stopped the great clock at 3:15?",
            Options = new[] { "Arthur Vance himself", "Dr. Elias Haze", "Tobias Crane", "Margaret Vance" }, CorrectIndex = 0 },
    };

    [TextArea(8, 24)]
    public string SolutionText =
        "Dr. Elias Haze came up the tower at 2:40 with Vance's evening tonic, and he had altered the dose from 10 drops to 40. " +
        "The bottle was <b>aconite</b>, labelled in his own hand. They shared tea, then Haze took his cup and fled at 3:10.\n\n" +
        "At <b>3:03</b> Vance felt his hands go numb and understood. A horologist to the last, he stopped his watch to mark the moment, " +
        "then turned his lamp onto a note. With minutes left he drew the pendulum's pin himself. The great clock stopped at <b>3:15</b>, " +
        "waking the whole town before Haze could return to tidy up.\n\n" +
        "Margaret's threat was only angry words: she was at the gala until 3:30. Crane's chisel marks were months old, and he had left at 11:02.";

    public event Action<string, int> ClueDiscovered;
    public event Action<CaseOutcome> CaseClosed;

    public int DiscoveredCount => discovered.Count;
    public int TotalClues => AllClues.Length;
    public IReadOnlyList<ClueEntry> Discovered => discovered;
    public int AccusationsLeft => MaxAccusations - accusationsUsed;
    public bool CanAccuse => Outcome == CaseOutcome.Open && discovered.Count >= CluesRequiredToAccuse;
    public CaseOutcome Outcome { get; private set; } = CaseOutcome.Open;
    /// <summary>Tips bought from the informant. Any tip rules out the top rank.</summary>
    public int HintsUsed { get; private set; }
    public bool VerdictOpen => verdictOpen;

    readonly List<ClueEntry> discovered = new List<ClueEntry>();
    int accusationsUsed;
    InputAction interactAction;
    InputAction cancelAction;
    InputAction restartAction;
    bool verdictOpen;
    int verdictOpenedFrame;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate {nameof(CaseDeductionManager)} on '{name}' destroyed.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (DeductionPanel != null) DeductionPanel.SetActive(false);
        RefreshTracker();
    }

    void Start()
    {
        if (PlayerInput != null)
        {
            interactAction = PlayerInput.actions.FindAction("Interact", false);
            cancelAction = PlayerInput.actions.FindAction("Cancel", false);
            restartAction = PlayerInput.actions.FindAction("Restart", false);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------------------------ clues

    /// <summary>Records a clue. Returns true if it was new.</summary>
    public bool RegisterClue(string clueId, string title = null, string note = null)
    {
        if (Array.IndexOf(AllClues, clueId) < 0)
        {
            Debug.LogWarning($"Unknown clue id '{clueId}'.", this);
            return false;
        }
        if (HasClue(clueId))
            return false;

        discovered.Add(new ClueEntry { Id = clueId, Title = title ?? clueId, Note = note ?? "" });
        RefreshTracker();
        ClueDiscovered?.Invoke(clueId, discovered.Count);
        return true;
    }

    public bool HasClue(string clueId) => discovered.Exists(c => c.Id == clueId);

    public void RegisterHint() => HintsUsed++;

    /// <summary>Called when the player puts an item back. Kept for the raycaster; hard mode has no auto-reveal.</summary>
    public void NotifyInspectionClosed() { }

    // ------------------------------------------------------------------ accusation

    /// <summary>
    /// Submits an accusation (one option index per question). Returns how many answers were right.
    /// Solves the case on a perfect answer; fails it when the last accusation is spent.
    /// </summary>
    public int Accuse(int[] answers)
    {
        if (!CanAccuse || answers == null || answers.Length != Questions.Length)
            return -1;

        int correct = 0;
        for (int i = 0; i < Questions.Length; i++)
            if (answers[i] == Questions[i].CorrectIndex)
                correct++;

        accusationsUsed++;
        if (correct == Questions.Length)
            CloseCase(CaseOutcome.Solved);
        else if (accusationsUsed >= MaxAccusations)
            CloseCase(CaseOutcome.Failed);

        RefreshTracker();
        return correct;
    }

    public string Rank()
    {
        if (Outcome != CaseOutcome.Solved) return "Case Gone Cold";
        if (accusationsUsed == 1 && discovered.Count == TotalClues && HintsUsed == 0) return "Master of the Escapement";
        if (accusationsUsed == 1) return "Chief Inspector";
        if (accusationsUsed == 2) return "Detective Sergeant";
        return "Lucky Constable";
    }

    void CloseCase(CaseOutcome outcome)
    {
        Outcome = outcome;
        string title, body;
        if (outcome == CaseOutcome.Solved)
        {
            title = "Case Solved: The 3:15 Escapement";
            body = $"<size=120%><color=#FFC774>Rank: {Rank()}</color></size>   " +
                   $"<alpha=#99>Evidence {discovered.Count}/{TotalClues}  ·  Accusations used {accusationsUsed}/{MaxAccusations}  ·  Tips bought {HintsUsed}<alpha=#FF>\n\n" +
                   SolutionText;
        }
        else
        {
            title = "The Case Goes Cold";
            body = "<color=#FF8A7A>Your last accusation falls apart. By dawn, Dr. Haze is on the 6:15 train to the coast.</color>\n\n" +
                   "<b>What really happened:</b>\n" + SolutionText;
        }
        ShowVerdict(title, body, outcome == CaseOutcome.Solved
            ? "E / Esc: keep exploring    ·    R: play again"
            : "R: try the case again");
        CaseClosed?.Invoke(outcome);
    }

    void ShowVerdict(string title, string body, string hint)
    {
        verdictOpen = true;
        verdictOpenedFrame = Time.frameCount;
        if (DeductionTitleText != null) DeductionTitleText.text = title;
        if (DeductionBodyText != null) DeductionBodyText.text = body;
        if (DeductionHintText != null) DeductionHintText.text = hint;
        if (DeductionPanel != null) DeductionPanel.SetActive(true);
        if (Player != null)
        {
            Player.SetInputActive(false);
            Player.SetCursorLocked(true);
        }
    }

    void Update()
    {
        if (!verdictOpen || Time.frameCount <= verdictOpenedFrame)
            return;

        if (restartAction != null && restartAction.WasPressedThisFrame())
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        bool close = Outcome == CaseOutcome.Solved &&
                     ((interactAction != null && interactAction.WasPressedThisFrame()) ||
                      (cancelAction != null && cancelAction.WasPressedThisFrame()));
        if (!close)
            return;

        verdictOpen = false;
        if (DeductionPanel != null) DeductionPanel.SetActive(false);
        if (Player != null) Player.SetInputActive(true);
    }

    void RefreshTracker()
    {
        if (TrackerText == null)
            return;
        string status = Outcome switch
        {
            CaseOutcome.Solved => "Case Closed",
            CaseOutcome.Failed => "Case Cold",
            _ => discovered.Count >= CluesRequiredToAccuse
                ? "<color=#FFFFFF><b>[TAB] Case Board: ready to accuse!</b></color>"
                : "[TAB] Case Board",
        };
        TrackerText.text = $"Evidence {discovered.Count}/{TotalClues}  ·  {status}";
    }
}
