using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene singleton that owns the case file for the current mystery (a <see cref="CaseDefinition"/>): which clues
/// have been found, the accusation, how many accusations remain, the verdict screen, and saving the result.
/// </summary>
public class CaseDeductionManager : MonoBehaviour
{
    public enum CaseOutcome { Open, Solved, Failed }

    // Clue ids for case 1, "The 3:15 Escapement" (used by the scene builder).
    public const string CLUE_POCKET_WATCH = "CLUE_POCKET_WATCH";
    public const string CLUE_DESK_LAMP = "CLUE_DESK_LAMP";
    public const string CLUE_APOTHECARY_VIAL = "CLUE_APOTHECARY_VIAL";
    public const string CLUE_TEACUP = "CLUE_TEACUP";
    public const string CLUE_PRESCRIPTION = "CLUE_PRESCRIPTION";
    public const string CLUE_LETTER = "CLUE_LETTER";
    public const string CLUE_LOGBOOK = "CLUE_LOGBOOK";
    public const string CLUE_CHISEL = "CLUE_CHISEL";
    public const string CLUE_PENDULUM_PIN = "CLUE_PENDULUM_PIN";

    public struct ClueEntry
    {
        public string Id;
        public string Title;
        public string Note;
    }

    public static CaseDeductionManager Instance { get; private set; }

    [Header("Case")]
    public CaseDefinition Definition;
    public CaseCatalog Catalog;

    [Header("Player")]
    public FirstPersonController Player;
    public PlayerInput PlayerInput;

    [Header("UI")]
    public TMP_Text TrackerText;
    public GameObject DeductionPanel;
    public TMP_Text DeductionTitleText;
    public TMP_Text DeductionBodyText;
    public TMP_Text DeductionHintText;

    public event Action<string, int> ClueDiscovered;
    public event Action<CaseOutcome> CaseClosed;

    public CaseDefinition.Question[] Questions => Definition.Questions;
    public int CluesRequiredToAccuse => Definition.CluesRequiredToAccuse;
    public int MaxAccusations => Definition.MaxAccusations;
    public int DiscoveredCount => discovered.Count;
    public int TotalClues => Definition.Clues.Length;
    public IReadOnlyList<ClueEntry> Discovered => discovered;
    public int AccusationsLeft => MaxAccusations - accusationsUsed;
    public bool CanAccuse => Outcome == CaseOutcome.Open && discovered.Count >= CluesRequiredToAccuse;
    public CaseOutcome Outcome { get; private set; } = CaseOutcome.Open;
    /// <summary>Tips bought from the informant. Any tip rules out the top rank.</summary>
    public int HintsUsed { get; private set; }
    public bool VerdictOpen => verdictOpen;
    public CaseDefinition NextCase => Catalog != null ? Catalog.Next(Definition) : null;

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
        if (Definition.FindClue(clueId) == null)
        {
            Debug.LogWarning($"Clue id '{clueId}' is not part of case '{Definition.CaseId}'.", this);
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

    /// <summary>0 = flawless (top rank) ... 3 = scraped through on the last accusation.</summary>
    public int RankIndex()
    {
        if (accusationsUsed == 1 && discovered.Count == TotalClues && HintsUsed == 0) return CaseProgress.TopRank;
        return Mathf.Clamp(accusationsUsed, 1, 3);
    }

    public string Rank() => Outcome == CaseOutcome.Solved ? CaseProgress.RankName(RankIndex(), Definition) : "Case Gone Cold";

    void CloseCase(CaseOutcome outcome)
    {
        Outcome = outcome;
        string title, body;
        if (outcome == CaseOutcome.Solved)
        {
            CaseProgress.RecordSolved(Definition.CaseId, RankIndex());
            title = $"Case Solved: {Definition.Title}";
            body = $"<size=120%><color=#FFC774>Rank: {Rank()}</color></size>   " +
                   $"<alpha=#99>Evidence {discovered.Count}/{TotalClues}  ·  Accusations used {accusationsUsed}/{MaxAccusations}  ·  Tips bought {HintsUsed}<alpha=#FF>\n\n" +
                   Definition.SolutionText;
        }
        else
        {
            title = "The Case Goes Cold";
            body = $"<color=#FF8A7A>{Definition.FailLine}</color>\n\n<b>What really happened:</b>\n{Definition.SolutionText}";
        }

        string hint;
        if (outcome == CaseOutcome.Solved)
            hint = NextCase != null
                ? $"N: next case  ·  M: case menu  ·  E / Esc: keep exploring  ·  R: play again"
                : "M: case menu  ·  E / Esc: keep exploring  ·  R: play again";
        else
            hint = "R: try the case again  ·  M: case menu";
        ShowVerdict(title, body, hint);
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

        var kb = Keyboard.current;
        if (restartAction != null && restartAction.WasPressedThisFrame())
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }
        if (kb != null && kb.mKey.wasPressedThisFrame)
        {
            GoToMenu();
            return;
        }
        if (Outcome == CaseOutcome.Solved && NextCase != null && kb != null && kb.nKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(NextCase.SceneName);
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

    public void GoToMenu()
    {
        if (Catalog != null && !string.IsNullOrEmpty(Catalog.MenuSceneName))
            SceneManager.LoadScene(Catalog.MenuSceneName);
    }

    void RefreshTracker()
    {
        if (TrackerText == null || Definition == null)
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
