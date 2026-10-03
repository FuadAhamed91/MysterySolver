using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Wren", a spy who turns up wherever there's a body: free intel on the suspects, and paid tips (each one recorded
/// on the case file, which costs the top rank). All lines come from the current case's <see cref="CaseDefinition"/>.
/// </summary>
public class InformantNPC : TalkingNPC
{
    const string Name = "Wren";

    int deductionTipsGiven;

    CaseDefinition Def => Case != null ? Case.Definition : null;

    protected override string DisplayName => Name;
    protected override string StrangerPrompt => "Talk to the stranger";
    protected override string CallOutLine => Def != null ? Def.WrenCallOut : "Psst... over here, detective.";

    protected override void BeginConversation()
    {
        if (Case != null && Case.Outcome == CaseDeductionManager.CaseOutcome.Solved)
            Dialogue.Say(Name, Def.WrenSolved, new DialogueUI.Choice("Goodbye.", Leave));
        else if (!Introduced)
            Dialogue.Say(Unknown, Def.WrenIntro, MainChoices());
        else
            Dialogue.Say(Name, "Back again? The night's not getting any younger, detective.", MainChoices());
    }

    DialogueUI.Choice[] MainChoices() => new[]
    {
        new DialogueUI.Choice(Introduced ? "Remind me who you are." : "Who are you?", WhoAreYou),
        new DialogueUI.Choice("Tell me about the suspects.", Suspects),
        new DialogueUI.Choice("Sell me a tip.  <alpha=#88>(costs your top rank)<alpha=#FF>", OfferTip),
        new DialogueUI.Choice("Goodbye.", Leave),
    };

    void WhoAreYou()
    {
        Introduced = true;
        Dialogue.Say(Name, Def.WrenWhoAmI, MainChoices());
    }

    void Suspects()
    {
        Introduced = true;
        var choices = new List<DialogueUI.Choice>();
        foreach (var s in Def.Suspects)
        {
            var suspect = s;
            if (choices.Count == 3) break; // the box has four slots; keep one for "Back"
            choices.Add(new DialogueUI.Choice($"{suspect.Name}, {suspect.Role}.",
                () => Dialogue.Say(Name, suspect.Intel, BackToSuspects())));
        }
        choices.Add(new DialogueUI.Choice("Back.", () => Dialogue.Say(Name, "Anything else?", MainChoices())));
        Dialogue.Say(Name, Def.WrenSuspectsLine, choices.ToArray());
    }

    DialogueUI.Choice[] BackToSuspects() => new[]
    {
        new DialogueUI.Choice("Tell me about another suspect.", Suspects),
        new DialogueUI.Choice("Back.", () => Dialogue.Say(Name, "Anything else?", MainChoices())),
    };

    void OfferTip()
    {
        Introduced = true;
        Dialogue.Say(Name, "Tips cost, detective. Not money: pride. The Inspector will know you had help, and you'll never " +
                           "earn the top rank. Still want it?",
            new DialogueUI.Choice("Yes. Tell me.", GiveTip),
            new DialogueUI.Choice("No, I'll work it out myself.", () => Dialogue.Say(Name, "Good instinct. Anything else?", MainChoices())));
    }

    void GiveTip()
    {
        string tip = NextTip();
        if (Case != null) Case.RegisterHint();
        Dialogue.Say(Name, tip, MainChoices());
    }

    /// <summary>
    /// Points at the first missing clue (clues are listed hardest-first in the case data); once everything is found,
    /// nudges the deduction instead.
    /// </summary>
    string NextTip()
    {
        if (Case == null || Def == null) return "Look around, detective.";

        foreach (var clue in Def.Clues)
            if (!Case.HasClue(clue.Id) && !string.IsNullOrEmpty(clue.Tip))
                return clue.Tip;

        if (Def.DeductionTips.Length == 0)
            return "You've got every piece. The rest is up to you.";
        return Def.DeductionTips[Mathf.Min(deductionTipsGiven++, Def.DeductionTips.Length - 1)];
    }
}
