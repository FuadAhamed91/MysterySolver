using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Finch", a messenger kid who always seems to be hiding nearby. Gives free hints, but only as riddles, each one
/// pointing at a piece of evidence the player has not found yet. Riddles rotate so the same one is not repeated
/// back to back. All lines come from the current case's <see cref="CaseDefinition"/>.
/// </summary>
public class RiddlerNPC : TalkingNPC
{
    const string Name = "Finch";

    [Header("Candle")]
    public Light Candle;
    float candleBase;

    readonly HashSet<string> riddlesHeard = new HashSet<string>();
    string lastRiddleClue;

    CaseDefinition Def => Case != null ? Case.Definition : null;

    protected override string DisplayName => Name;
    protected override string StrangerPrompt => "Talk to the kid";
    protected override string CallOutLine => Def != null ? Def.FinchCallOut : "Oi! Detective!";

    protected override void Start()
    {
        base.Start();
        if (Candle != null) candleBase = Candle.intensity;
    }

    protected override void Update()
    {
        base.Update();
        if (Candle != null) // flickering candle flame
            Candle.intensity = candleBase * (0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 7f, 0.3f));
    }

    protected override void BeginConversation()
    {
        if (Case != null && Case.Outcome == CaseDeductionManager.CaseOutcome.Solved)
            Dialogue.Say(Name, Def.FinchSolved, new DialogueUI.Choice("Off you go, Finch.", Leave));
        else if (!Introduced)
        {
            Introduced = true;
            Dialogue.Say(Unknown, Def.FinchIntro, MainChoices());
        }
        else
            Dialogue.Say(Name, "Back for another? I've got riddles comin' out me ears.", MainChoices());
    }

    DialogueUI.Choice[] MainChoices() => new[]
    {
        new DialogueUI.Choice("Give me a riddle for a clue I'm missing.  <alpha=#88>(free)<alpha=#FF>", GiveRiddle),
        new DialogueUI.Choice("What did you see tonight?", () => Dialogue.Say(Name, Def.FinchWitness, MainChoices())),
        new DialogueUI.Choice("How do I solve this case?", HowToSolve),
        new DialogueUI.Choice("Goodbye.", Leave),
    };

    void GiveRiddle()
    {
        var missing = new List<CaseDefinition.Clue>();
        foreach (var clue in Def.Clues)
            if (Case == null || !Case.HasClue(clue.Id))
                missing.Add(clue);

        if (missing.Count == 0)
        {
            Dialogue.Say(Name, "You've found every last one! Now pin 'em up: press Tab and work out who did it.", MainChoices());
            return;
        }

        // Prefer a riddle the player has not heard yet, and never the same one twice in a row.
        var pick = missing[0];
        bool found = false;
        foreach (var c in missing)
            if (!riddlesHeard.Contains(c.Id) && c.Id != lastRiddleClue) { pick = c; found = true; break; }
        if (!found)
            foreach (var c in missing)
                if (c.Id != lastRiddleClue) { pick = c; break; }

        riddlesHeard.Add(pick.Id);
        lastRiddleClue = pick.Id;
        string left = missing.Count == 1 ? "Last one you're missin'!" : $"You're still missin' {missing.Count}.";
        Dialogue.Say(Name, $"<i>{pick.Riddle}</i>\n<alpha=#AA>{left}<alpha=#FF>", MainChoices());
    }

    void HowToSolve() => Dialogue.Say(Name,
        $"Find the clues, then open the Case Board with Tab. Answer all {(Def != null ? Def.Questions.Length : 4)} " +
        $"questions, but you only get {(Def != null ? Def.MaxAccusations : 3)} goes! Get it wrong and they only tell you " +
        "how many you got right, not which.",
        MainChoices());
}
