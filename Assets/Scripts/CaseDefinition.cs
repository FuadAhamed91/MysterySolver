using System;
using UnityEngine;

/// <summary>
/// Everything that makes one mystery unique: its evidence (with Finch's riddle and Wren's tip for each clue),
/// suspects, accusation questions, solution, and the informants' lines. The scene supplies the 3D evidence;
/// clue ids on each InspectableItem must match the ids listed here.
/// </summary>
[CreateAssetMenu(menuName = "Spooky Crime Scene/Case Definition", fileName = "Case")]
public class CaseDefinition : ScriptableObject
{
    [Serializable]
    public class Clue
    {
        public string Id;
        [TextArea(2, 4)] public string Riddle; // Finch: free, cryptic
        [TextArea(2, 4)] public string Tip;    // Wren: paid, direct
    }

    [Serializable]
    public class Suspect
    {
        public string Name;
        public string Role;
        [TextArea(3, 6)] public string Intel;
    }

    [Serializable]
    public class Question
    {
        public string Prompt;
        public string[] Options;
        public int CorrectIndex;
    }

    [Header("Identity")]
    [Tooltip("Stable key used for saved progress. Never change it once players have saves.")]
    public string CaseId = "case";
    public int Number = 1;
    public string Title = "Untitled Case";
    public string Location = "";
    [Tooltip("Scene loaded from the case menu (must be in Build Settings).")]
    public string SceneName = "";
    [TextArea(2, 4)] public string MenuBlurb = "";
    [TextArea(4, 10)] public string Intro = "";

    [Header("Evidence")]
    public Clue[] Clues = Array.Empty<Clue>();
    public int CluesRequiredToAccuse = 5;
    public int MaxAccusations = 3;

    [Header("Accusation")]
    public Question[] Questions = Array.Empty<Question>();
    [TextArea(8, 24)] public string SolutionText = "";
    [Tooltip("Shown when the last accusation fails, before the solution.")]
    [TextArea(2, 4)] public string FailLine = "";
    [Tooltip("Rank for a flawless solve: every clue, first accusation, no tips bought.")]
    public string TopRankName = "Master Detective";

    [Header("Wren (paid tips, suspect intel)")]
    public string WrenCallOut = "Psst... over here, detective.";
    [TextArea(2, 4)] public string WrenIntro = "";
    [TextArea(2, 4)] public string WrenWhoAmI = "";
    public string WrenSuspectsLine = "Three people had reason to want them gone. Pick one.";
    public Suspect[] Suspects = Array.Empty<Suspect>();
    [Tooltip("Nudges toward the solution once every clue is found, given in order.")]
    [TextArea(2, 4)] public string[] DeductionTips = Array.Empty<string>();
    [TextArea(2, 4)] public string WrenSolved = "";

    [Header("Finch (free riddles)")]
    public string FinchCallOut = "Oi! Detective!";
    [TextArea(2, 4)] public string FinchIntro = "";
    [TextArea(2, 4)] public string FinchWitness = "";
    [TextArea(2, 4)] public string FinchSolved = "";

    public Clue FindClue(string id) => Array.Find(Clues, c => c.Id == id);
}
