using UnityEngine;

/// <summary>
/// "Wren", a spy who watched the tower tonight: free intel on the suspects, and paid tips
/// (each one recorded on the case file, which costs the top rank).
/// </summary>
public class InformantNPC : TalkingNPC
{
    const string Name = "Wren";

    int deductionTipsGiven;

    protected override string DisplayName => Name;
    protected override string StrangerPrompt => "Talk to the stranger";
    protected override string CallOutLine => "Psst... over here, detective. By the window.";

    protected override void BeginConversation()
    {
        if (Case != null && Case.Outcome == CaseDeductionManager.CaseOutcome.Solved)
            Dialogue.Say(Name, "Nicely done, detective. Haze will run, or hang. Either way my client will be pleased. " +
                               "You never saw me.", new DialogueUI.Choice("Goodbye.", Leave));
        else if (!Introduced)
            Dialogue.Say(Unknown, "Easy. I'm not here to hurt you. I watch this tower for people who pay well, and tonight " +
                                  "nobody paid me to look away. So I looked.", MainChoices());
        else
            Dialogue.Say(Name, "Back again. The clock's not getting any younger, detective.", MainChoices());
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
        Dialogue.Say(Name, "Call me Wren. Names are expensive, so that one's a gift. I keep an eye on Vance's work for " +
                           "a client who wanted his designs. Dead men don't sell blueprints, so I want his killer caught as much as you do.",
                     MainChoices());
    }

    void Suspects()
    {
        Introduced = true;
        Dialogue.Say(Name, "Three people had reason to want Vance gone. Pick one.",
            new DialogueUI.Choice("Dr. Elias Haze, the physician.", () => Dialogue.Say(Name,
                "Town doctor. Treats half the council, and owes the bank twice what he earns. Vance found out Haze was " +
                "watering down patients' medicine and pocketing the difference. He was going to the constable on Monday.",
                BackToSuspects())),
            new DialogueUI.Choice("Margaret Vance, the niece.", () => Dialogue.Say(Name,
                "Hot temper, empty purse. She inherits if the will stands. But she spent tonight dancing at the Founders' Gala. " +
                "Half the town saw her there, and the watchman writes everything down.", BackToSuspects())),
            new DialogueUI.Choice("Tobias Crane, the rival.", () => Dialogue.Say(Name,
                "Vance's old apprentice. Swears Vance stole his escapement design. Loud, petty, and sloppy with his tools: " +
                "he leaves them everywhere and never cleans them.", BackToSuspects())),
            new DialogueUI.Choice("Back.", () => Dialogue.Say(Name, "Anything else?", MainChoices())));
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

    /// <summary>Points at the hardest evidence still missing; once everything is found, nudges the deduction.</summary>
    string NextTip()
    {
        var c = Case;
        if (c == null) return "Look around, detective.";

        if (!c.HasClue(CaseDeductionManager.CLUE_PENDULUM_PIN))
            return "Not everything worth seeing can be carried. Go to the fallen pendulum and look where the rod meets the great wheel.";
        if (!c.HasClue(CaseDeductionManager.CLUE_CHISEL))
            return "Moonlight shows more than it hides. Check the floor inside the glow from the clock face, and look down.";
        if (!c.HasClue(CaseDeductionManager.CLUE_LOGBOOK))
            return "The night watchman sat by a window to keep warm. Follow the little orange glow.";
        if (!c.HasClue(CaseDeductionManager.CLUE_PRESCRIPTION))
            return "Vance kept his papers in pigeonholes. Not every one of them is empty.";
        if (!c.HasClue(CaseDeductionManager.CLUE_TEACUP))
            return "Someone knocked something off that desk tonight. Look at the floor beside it.";
        if (!c.HasClue(CaseDeductionManager.CLUE_LETTER))
            return "There's a letter on the desk with a red seal. Family business, and nasty business.";
        if (!c.HasClue(CaseDeductionManager.CLUE_POCKET_WATCH) || !c.HasClue(CaseDeductionManager.CLUE_DESK_LAMP)
            || !c.HasClue(CaseDeductionManager.CLUE_APOTHECARY_VIAL))
            return "Start with his desk. A watch, a lamp, a bottle. Everything a dying horologist would reach for.";

        switch (deductionTipsGiven++)
        {
            case 0: return "You've got every piece. Now check alibis: two of your suspects were nowhere near this room when it mattered. The watchman's book says who.";
            case 1: return "The poison took hold before the clock stopped. A watchmaker would mark that moment on the thing he trusts most.";
            default: return "Ask who was in this room after Haze ran down the stairs at 3:10. Only one man. And he was dying, but not finished.";
        }
    }

}
