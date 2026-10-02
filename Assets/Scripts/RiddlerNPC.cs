using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Finch", a messenger kid who hides behind the gears. Gives free hints, but only as riddles, each one pointing
/// at a piece of evidence the player has not found yet. Riddles rotate so the same one is not repeated back to back.
/// </summary>
public class RiddlerNPC : TalkingNPC
{
    const string Name = "Finch";

    // Clue id -> riddle. Order is the order riddles are offered in.
    static readonly (string clue, string riddle)[] Riddles =
    {
        (CaseDeductionManager.CLUE_POCKET_WATCH,
            "I've got hands but I can't clap, and someone told me to stop. I'm lying on the sloped desk, near the edge, waitin' to be picked up."),
        (CaseDeductionManager.CLUE_DESK_LAMP,
            "I've a neck like a swan and a hat like a bell. I'm bowing my head to show you what I saw. Look on the desk's back shelf."),
        (CaseDeductionManager.CLUE_APOTHECARY_VIAL,
            "Small and corked and nearly dry, I stand on the shelf beside the bright one. What I held put a man to sleep for good."),
        (CaseDeductionManager.CLUE_TEACUP,
            "Fallen but not broken, cold but once warm. I'm on the floor by the desk, and I've a ring on my plate that isn't mine."),
        (CaseDeductionManager.CLUE_PRESCRIPTION,
            "Six little rooms sit on the desk. Five hold rolled-up scrolls, and one holds a doctor's folded promise that somebody broke."),
        (CaseDeductionManager.CLUE_LETTER,
            "Sealed in red and full of family spite, I lie on the slope where he drew at night. Left side, detective."),
        (CaseDeductionManager.CLUE_LOGBOOK,
            "Where an old man warmed his hands by the glass, his words wait for anyone who passes. Follow the little orange glow."),
        (CaseDeductionManager.CLUE_CHISEL,
            "Where the moon spills silver on the boards, a rusty tooth lies forgotten. Look down in the light from the big clock face."),
        (CaseDeductionManager.CLUE_PENDULUM_PIN,
            "I held the heart that kept the town's time, till a shaking hand pulled me free. You can't lift me: look where the brass rod meets the big wheel."),
    };

    [Header("Candle")]
    public Light Candle;
    float candleBase;

    readonly HashSet<string> riddlesHeard = new HashSet<string>();
    string lastRiddleClue;

    protected override string DisplayName => Name;
    protected override string StrangerPrompt => "Talk to the kid";
    protected override string CallOutLine => "Oi! Detective! Behind the gears. Quick, before the coppers see me!";

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
            Dialogue.Say(Name, "You did it! I'll run the news to every street in town. First edition, no charge!",
                         new DialogueUI.Choice("Off you go, Finch.", Leave));
        else if (!Introduced)
        {
            Introduced = true;
            Dialogue.Say(Unknown, "Name's Finch. I run messages for Wren, and for anyone with a coin. But for you, riddles are " +
                                  "free! Wren charges for tips. I don't. Mine are just harder to crack.", MainChoices());
        }
        else
            Dialogue.Say(Name, "Back for another? I've got riddles comin' out me ears.", MainChoices());
    }

    DialogueUI.Choice[] MainChoices() => new[]
    {
        new DialogueUI.Choice("Give me a riddle for a clue I'm missing.  <alpha=#88>(free)<alpha=#FF>", GiveRiddle),
        new DialogueUI.Choice("What did you see tonight?", WhatDidYouSee),
        new DialogueUI.Choice("How do I solve this case?", HowToSolve),
        new DialogueUI.Choice("Goodbye.", Leave),
    };

    void GiveRiddle()
    {
        var missing = new List<(string clue, string riddle)>();
        foreach (var r in Riddles)
            if (Case == null || !Case.HasClue(r.clue))
                missing.Add(r);

        if (missing.Count == 0)
        {
            Dialogue.Say(Name, "You've found every last one! Now pin 'em up: press Tab, or use the corkboard by the desk, and work out who did it.",
                         MainChoices());
            return;
        }

        // Prefer a riddle the player has not heard yet, and never the same one twice in a row.
        var pick = missing[0];
        bool found = false;
        foreach (var r in missing)
            if (!riddlesHeard.Contains(r.clue) && r.clue != lastRiddleClue) { pick = r; found = true; break; }
        if (!found)
            foreach (var r in missing)
                if (r.clue != lastRiddleClue) { pick = r; break; }

        riddlesHeard.Add(pick.clue);
        lastRiddleClue = pick.clue;
        string left = missing.Count == 1 ? "Last one you're missin'!" : $"You're still missin' {missing.Count}.";
        Dialogue.Say(Name, $"<i>{pick.riddle}</i>\n<alpha=#AA>{left}<alpha=#FF>", MainChoices());
    }

    void WhatDidYouSee() => Dialogue.Say(Name,
        "I was hidin' on the stair. Somebody came down past me in a proper hurry. Didn't see a face, but they'd no hat, " +
        "and their bag clinked like little glass bottles. Then BONG! The whole tower shook and the clock stopped.",
        MainChoices());

    void HowToSolve() => Dialogue.Say(Name,
        "Find the clues, then open the Case Board. Press Tab, or walk up to the corkboard by the desk. Answer all four " +
        "questions, but you only get three goes! Get it wrong and they only tell you how many you got right, not which.",
        MainChoices());
}
