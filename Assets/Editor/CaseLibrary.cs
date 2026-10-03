using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Source of truth for the three mysteries. Writes (or refreshes) the CaseDefinition assets and the CaseCatalog in
/// Assets/Cases. Clues are listed hardest-first: Wren's paid tips point at the first one still missing.
/// </summary>
public static class CaseLibrary
{
    public const string Folder = "Assets/Cases";
    public const string CatalogPath = Folder + "/CaseCatalog.asset";

    // Case 2: The Lighthouse at Gull Point
    public const string LH_FUEL_VALVE = "LH_FUEL_VALVE";
    public const string LH_BUTTON = "LH_BUTTON";
    public const string LH_SPYGLASS = "LH_SPYGLASS";
    public const string LH_MATCHES = "LH_MATCHES";
    public const string LH_WATCH = "LH_WATCH";
    public const string LH_LOGBOOK = "LH_LOGBOOK";
    public const string LH_MANIFEST = "LH_MANIFEST";
    public const string LH_PIPE = "LH_PIPE";

    // Case 3: The Nightshade Conservatory
    public const string GH_VENT_CRANK = "GH_VENT_CRANK";
    public const string GH_FOOTPRINTS = "GH_FOOTPRINTS";
    public const string GH_POISON = "GH_POISON";
    public const string GH_GLOVES = "GH_GLOVES";
    public const string GH_TAG = "GH_TAG";
    public const string GH_VISITORS = "GH_VISITORS";
    public const string GH_DIARY = "GH_DIARY";
    public const string GH_SLIP = "GH_SLIP";

    [MenuItem("Tools/The Spooky Crime Scene/0. Write Case Assets")]
    static void Menu() => Debug.Log(EnsureAssets());

    [MenuItem("Tools/The Spooky Crime Scene/Unlock All Cases (testing)")]
    static void UnlockAll() => CaseProgress.UnlockAll(AssetDatabase.LoadAssetAtPath<CaseCatalog>(CatalogPath));

    [MenuItem("Tools/The Spooky Crime Scene/Reset Case Progress")]
    static void ResetProgress() => CaseProgress.ResetAll(AssetDatabase.LoadAssetAtPath<CaseCatalog>(CatalogPath));

    public static CaseCatalog EnsureAssets()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Cases");

        var c1 = Load("Case1_Clocktower");
        FillClocktower(c1);
        var c2 = Load("Case2_Lighthouse");
        FillLighthouse(c2);
        var c3 = Load("Case3_Greenhouse");
        FillGreenhouse(c3);

        var catalog = AssetDatabase.LoadAssetAtPath<CaseCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<CaseCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.MenuSceneName = "MainMenu";
        catalog.Cases = new[] { c1, c2, c3 };
        foreach (var o in new Object[] { c1, c2, c3, catalog })
            EditorUtility.SetDirty(o);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    static CaseDefinition Load(string name)
    {
        string path = $"{Folder}/{name}.asset";
        var def = AssetDatabase.LoadAssetAtPath<CaseDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<CaseDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        return def;
    }

    static CaseDefinition.Clue Clue(string id, string riddle, string tip) =>
        new CaseDefinition.Clue { Id = id, Riddle = riddle, Tip = tip };

    static CaseDefinition.Suspect Suspect(string name, string role, string intel) =>
        new CaseDefinition.Suspect { Name = name, Role = role, Intel = intel };

    static CaseDefinition.Question Q(string prompt, int correct, params string[] options) =>
        new CaseDefinition.Question { Prompt = prompt, Options = options, CorrectIndex = correct };

    // ==================================================================== Case 1

    static void FillClocktower(CaseDefinition d)
    {
        d.CaseId = "clocktower";
        d.Number = 1;
        d.Title = "The 3:15 Escapement";
        d.Location = "The Clocktower Gear Room  ·  3:15 AM";
        d.SceneName = "Clocktower";
        d.MenuBlurb = "Inventor Arthur Vance lies beneath the fallen pendulum of the town clock. The great clock stopped at 3:15. Was it an accident?";
        d.Intro =
            "The great clock of Ashgrove stopped at 3:15 this morning, and half the town woke to the silence. Inventor Arthur Vance " +
            "was found in the gear room beneath the fallen pendulum.\n\nThe constable calls it an accident. You don't. Search the tower, " +
            "question anyone lurking in the shadows, and name the killer before the trail goes cold.";

        d.Clues = new[]
        {
            Clue(CaseDeductionManager.CLUE_PENDULUM_PIN,
                "I held the heart that kept the town's time, till a shaking hand pulled me free. You can't lift me: look where the brass rod meets the big wheel.",
                "Not everything worth seeing can be carried. Go to the fallen pendulum and look where the rod meets the great wheel."),
            Clue(CaseDeductionManager.CLUE_CHISEL,
                "Where the moon spills silver on the boards, a rusty tooth lies forgotten. Look down in the light from the big clock face.",
                "Moonlight shows more than it hides. Check the floor inside the glow from the clock face, and look down."),
            Clue(CaseDeductionManager.CLUE_LOGBOOK,
                "Where an old man warmed his hands by the glass, his words wait for anyone who passes. Follow the little orange glow.",
                "The night watchman sat by a window to keep warm. Follow the little orange glow."),
            Clue(CaseDeductionManager.CLUE_PRESCRIPTION,
                "Six little rooms sit on the desk. Five hold rolled-up scrolls, and one holds a doctor's folded promise that somebody broke.",
                "Vance kept his papers in pigeonholes. Not every one of them is empty."),
            Clue(CaseDeductionManager.CLUE_TEACUP,
                "Fallen but not broken, cold but once warm. I'm on the floor by the desk, and I've a ring on my plate that isn't mine.",
                "Someone knocked something off that desk tonight. Look at the floor beside it."),
            Clue(CaseDeductionManager.CLUE_LETTER,
                "Sealed in red and full of family spite, I lie on the slope where he drew at night. Left side, detective.",
                "There's a letter on the desk with a red seal. Family business, and nasty business."),
            Clue(CaseDeductionManager.CLUE_POCKET_WATCH,
                "I've got hands but I can't clap, and someone told me to stop. I'm lying on the sloped desk, near the edge, waitin' to be picked up.",
                "His pocket watch is on the sloping desk, near the front edge. A watchmaker doesn't stop a watch for nothing."),
            Clue(CaseDeductionManager.CLUE_DESK_LAMP,
                "I've a neck like a swan and a hat like a bell. I'm bowing my head to show you what I saw. Look on the desk's back shelf.",
                "Look at the brass lamp on the back shelf of the desk, and at what it's pointing at."),
            Clue(CaseDeductionManager.CLUE_APOTHECARY_VIAL,
                "Small and corked and nearly dry, I stand on the shelf beside the bright one. What I held put a man to sleep for good.",
                "A small corked bottle stands on the desk's back shelf beside the lamp. Read the label."),
        };
        d.CluesRequiredToAccuse = 5;
        d.MaxAccusations = 3;
        d.Questions = new[]
        {
            Q("Who poisoned Arthur Vance?", 0, "Dr. Elias Haze", "Margaret Vance", "Tobias Crane", "No one: an accident"),
            Q("When did the poison take hold?", 1, "2:40 AM", "3:03 AM", "3:10 AM", "3:15 AM"),
            Q("How was he killed?", 0, "Aconite in his tonic", "Crushed by the pendulum", "Gears sabotaged to fall", "Poisoned wax seal"),
            Q("Who stopped the great clock at 3:15?", 0, "Arthur Vance himself", "Dr. Elias Haze", "Tobias Crane", "Margaret Vance"),
        };
        d.SolutionText =
            "Dr. Elias Haze came up the tower at 2:40 with Vance's evening tonic, and he had altered the dose from 10 drops to 40. " +
            "The bottle was <b>aconite</b>, labelled in his own hand. They shared tea, then Haze took his cup and fled at 3:10.\n\n" +
            "At <b>3:03</b> Vance felt his hands go numb and understood. A horologist to the last, he stopped his watch to mark the moment, " +
            "then turned his lamp onto a note. With minutes left he drew the pendulum's pin himself. The great clock stopped at <b>3:15</b>, " +
            "waking the whole town before Haze could return to tidy up.\n\n" +
            "Margaret's threat was only angry words: she was at the gala until 3:30. Crane's chisel marks were months old, and he had left at 11:02.";
        d.FailLine = "Your last accusation falls apart. By dawn, Dr. Haze is on the 6:15 train to the coast.";
        d.TopRankName = "Master of the Escapement";

        d.WrenCallOut = "Psst... over here, detective. By the window.";
        d.WrenIntro = "Easy. I'm not here to hurt you. I watch this tower for people who pay well, and tonight nobody paid me to look away. So I looked.";
        d.WrenWhoAmI = "Call me Wren. Names are expensive, so that one's a gift. I keep an eye on Vance's work for a client who wanted his designs. " +
                       "Dead men don't sell blueprints, so I want his killer caught as much as you do.";
        d.WrenSuspectsLine = "Three people had reason to want Vance gone. Pick one.";
        d.Suspects = new[]
        {
            Suspect("Dr. Elias Haze", "the physician",
                "Town doctor. Treats half the council, and owes the bank twice what he earns. Vance found out Haze was watering down patients' " +
                "medicine and pocketing the difference. He was going to the constable on Monday."),
            Suspect("Margaret Vance", "the niece",
                "Hot temper, empty purse. She inherits if the will stands. But she spent tonight dancing at the Founders' Gala. Half the town saw " +
                "her there, and the watchman writes everything down."),
            Suspect("Tobias Crane", "the rival",
                "Vance's old apprentice. Swears Vance stole his escapement design. Loud, petty, and sloppy with his tools: he leaves them " +
                "everywhere and never cleans them."),
        };
        d.DeductionTips = new[]
        {
            "You've got every piece. Now check alibis: two of your suspects were nowhere near this room when it mattered. The watchman's book says who.",
            "The poison took hold before the clock stopped. A watchmaker would mark that moment on the thing he trusts most.",
            "Ask who was in this room after Haze ran down the stairs at 3:10. Only one man. And he was dying, but not finished.",
        };
        d.WrenSolved = "Nicely done, detective. Haze will run, or hang. Either way my client will be pleased. You never saw me.";

        d.FinchCallOut = "Oi! Detective! Behind the gears. Quick, before the coppers see me!";
        d.FinchIntro = "Name's Finch. I run messages for Wren, and for anyone with a coin. But for you, riddles are free! Wren charges for tips. " +
                       "I don't. Mine are just harder to crack.";
        d.FinchWitness = "I was hidin' on the stair. Somebody came down past me in a proper hurry. Didn't see a face, but they'd no hat, and their " +
                         "bag clinked like little glass bottles. Then BONG! The whole tower shook and the clock stopped.";
        d.FinchSolved = "You did it! I'll run the news to every street in town. First edition, no charge!";
    }

    // ==================================================================== Case 2

    static void FillLighthouse(CaseDefinition d)
    {
        d.CaseId = "lighthouse";
        d.Number = 2;
        d.Title = "The Lighthouse at Gull Point";
        d.Location = "The Lamp Room  ·  A Storm at Midnight";
        d.SceneName = "Lighthouse";
        d.MenuBlurb = "The light went dark in a storm, a ship full of silver struck the rocks, and the keeper lay dead at the foot of the stairs.";
        d.Intro =
            "At 11:52 last night the Gull Point light went dark. Eighteen minutes later the schooner Merrow struck the rocks called the Teeth, " +
            "her cargo of silver spilling into the surf.\n\nKeeper Silas Marsh was found at the foot of the lamp-room stairs at half past " +
            "midnight. The harbour board says he fell, rushing to relight the lamp. The storm is still raging. Find out what really happened up here.";

        d.Clues = new[]
        {
            Clue(LH_FUEL_VALVE,
                "My wheel is small but I hold the light's breath. Turn me shut and the whole sea goes dark. Look low on the lamp's iron leg.",
                "The lamp didn't run out of oil. Look at the fuel valve on the lamp's pedestal: the little brass wheel facing the room."),
            Clue(LH_BUTTON,
                "Anchor and crown, I hang where a man went down. Look on the iron ring that guards the stairs.",
                "Check the railing around the stair hatch. Something from somebody's coat got caught there tonight."),
            Clue(LH_SPYGLASS,
                "My eye is long and my tube is brass. I stare at the rocks from the ledge by the glass.",
                "There's a spyglass resting on the window ledge, pointed straight at the rocks. See where it's aimed."),
            Clue(LH_MATCHES,
                "Three little sticks with their heads burned black, dropped by the lamp by someone who came back.",
                "Look on the floor at the base of the great lamp, near the fuel tank. Someone dropped some matches."),
            Clue(LH_WATCH,
                "My face is cracked and my hands stood still when a body fell down the stairs. Look near the hole in the floor.",
                "There's a pocket watch on the floor near the stair hatch. Read the time it stopped."),
            Clue(LH_LOGBOOK,
                "I keep the hours of the lamp in blue cloth. The keeper's last words sit on his writing shelf.",
                "Silas's logbook is on his writing shelf by the window. Read the last entry."),
            Clue(LH_MANIFEST,
                "Sealed in red, I list what the sea now keeps: forty silver bars beneath the deeps. I lie on the keeper's shelf.",
                "On the writing shelf there's a folded paper with a red seal. It explains why someone wanted that ship on the rocks."),
            Clue(LH_PIPE,
                "White and thin with a cold little bowl, I wait on the shelf for my owner's return. Was he even here?",
                "The clay pipe on the writing shelf belongs to the assistant keeper. Check whether it was smoked tonight."),
        };
        d.CluesRequiredToAccuse = 5;
        d.MaxAccusations = 3;
        d.Questions = new[]
        {
            Q("Who killed Silas Marsh?", 0, "Jonas Reed, the harbourmaster", "Edwin Pike, the assistant", "Nell Marsh, his daughter", "No one: he fell"),
            Q("Why did the light go out at 11:52?", 0, "The fuel valve was shut on purpose", "The wick burned out", "The storm broke the glass", "Silas forgot to fill the tank"),
            Q("Why was it done?", 0, "To wreck the Merrow for her silver", "To inherit the keeper's cottage", "To become head keeper", "Revenge for an old debt"),
            Q("When did Silas die?", 0, "11:49, before the light went out", "12:10, when the ship struck", "12:30, when he was found", "11:30, after trimming the wick"),
        };
        d.SolutionText =
            "Harbourmaster <b>Jonas Reed</b> wanted the Merrow on the rocks. A wreck in his waters meant a third of her silver for the harbour " +
            "board, and a fat cut for himself.\n\nAt 11:44 he climbed the tower again and told Silas to let the light fail. Silas refused. They " +
            "struggled at the stair rail, Reed's button tore away, and Silas went down the stairs. His watch stopped at <b>11:49</b>.\n\n" +
            "At 11:52 Reed screwed the <b>fuel valve</b> shut and watched through the spyglass until the Merrow struck the Teeth. Then he relit " +
            "the lamp with matches from the Anchor & Lamp, so everyone would say the light never failed and the keeper simply fell.\n\n" +
            "Edwin was ringing the fog bell all night, and Nell was on the mainland.";
        d.FailLine = "Your accusation sinks like the Merrow. Jonas Reed signs the salvage papers at dawn, and the harbour board shares out the silver.";
        d.TopRankName = "Keeper of the Light";

        d.WrenCallOut = "Psst... detective. Over here, out of the wind.";
        d.WrenIntro = "Don't look so surprised, detective. Wherever there's money and a body, you'll find me. Tonight it's forty bars of silver at " +
                      "the bottom of the bay.";
        d.WrenWhoAmI = "Wren. I watch things for people who pay. Tonight I was paid to see the Merrow come in safe. She didn't, so somebody owes " +
                       "my client a great deal of silver. Help me find out who.";
        d.WrenSuspectsLine = "Three people had business with Silas tonight. Pick one.";
        d.Suspects = new[]
        {
            Suspect("Jonas Reed", "the harbourmaster",
                "Runs the harbour, and the harbour board's salvage rights. Anything that washes off a wreck in his waters, the board takes a third, " +
                "and Reed takes his cut. He's been up this tower twice this month, arguing with Silas about the light."),
            Suspect("Edwin Pike", "the assistant keeper",
                "Silas's assistant. Ambitious, and Silas wouldn't recommend him for a light of his own. But tonight was his watch on the fog bell at " +
                "the foot of the tower. You could hear him ringing it across the bay, every half minute, all night."),
            Suspect("Nell Marsh", "the keeper's daughter",
                "Silas's daughter. They quarrelled because she wants to leave Gull Point for good, and the cottage comes to her now. But she took " +
                "the evening ferry to the mainland. It left at nine, before the storm closed the harbour."),
        };
        d.DeductionTips = new[]
        {
            "You've got everything. Start with time: the watch, the logbook, and when the light went out. Who died first, the keeper or the light?",
            "Silas lit his lamp with flint, never matches. So who relit it after the wreck, and why would they want it burning when help arrived?",
            "Follow the button and the silver. Only one name on that list profits from a wreck in his own harbour.",
        };
        d.WrenSolved = "The Merrow's owners will want a word with Jonas Reed. So will I. Nice work, detective. Mind the stairs on your way down.";

        d.FinchCallOut = "Oi! Detective! Over here by the oil barrels, I'm soaked!";
        d.FinchIntro = "Finch! Remember me? I rowed out with the post boat and got stuck when the storm came in. Riddles are still free, " +
                       "detective. Wren charges, I don't.";
        d.FinchWitness = "I was shelterin' in the oil store at the bottom. Around a quarter to twelve a big man in a harbour coat went up the " +
                         "stairs, swearin'. Later I heard a crash out on the rocks, then somebody came down in a hurry, stinkin' of matches.";
        d.FinchSolved = "Reed?! I'll have it in every paper on the coast by breakfast. Mind you don't slip on the way down!";
    }

    // ==================================================================== Case 3

    static void FillGreenhouse(CaseDefinition d)
    {
        d.CaseId = "greenhouse";
        d.Number = 3;
        d.Title = "The Nightshade Conservatory";
        d.Location = "Thornfield Glasshouse  ·  The Midnight Bloom";
        d.SceneName = "Greenhouse";
        d.MenuBlurb = "A famous botanist lies dead in her locked glasshouse, beside a rare orchid that blooms only at midnight.";
        d.Intro =
            "Lady Agatha Thorne locked herself in her glasshouse last night to watch her Moonflower bloom: an orchid that opens only at " +
            "midnight and has never before been seen in England.\n\nAt dawn the gardener broke the lock and found her dead in her chair. The " +
            "doctor says her heart simply stopped. But the door was locked from the inside, the key was in her pocket, and somebody walked through the mud.";

        d.Clues = new[]
        {
            Clue(GH_VENT_CRANK,
                "Turn my wheel and the roof breathes in. Someone turned me after midnight with flower dust on their skin. Find me by the ladder.",
                "Look at the crank wheel on the wall by the ladder. That's how a locked glasshouse gets unlocked from the inside. Check the handle."),
            Clue(GH_FOOTPRINTS,
                "We walk without feet from the chair to the climb, small and muddy, left after midnight. Look down on the path.",
                "Look at the gravel between Agatha's chair and the ladder. Someone left footprints. Check their size."),
            Clue(GH_POISON,
                "Brown and corked with a warning in black, I hide on the low shelf where the spiders sleep. Under the bench on your right as you come in.",
                "Get down low and look under the potting bench on your right as you enter. Something poisonous is hidden on the bottom shelf."),
            Clue(GH_GLOVES,
                "Two empty hands lie by the purple light. They held the poison while she held her flower that night.",
                "Agatha's gloves are on the table by the Moonflower. Smell the leather."),
            Clue(GH_TAG,
                "I wear the flower's name on a little stick, but someone scratched me and wrote a new name quick.",
                "Look at the little name tag stuck in the Moonflower's pot. Whose name is on it now?"),
            Clue(GH_VISITORS,
                "Every guest must sign my pages by the door. The last name in me never signed out.",
                "The visitor book on the lectern by the door shows who came last night. Look for who never signed out."),
            Clue(GH_DIARY,
                "I'm warm and I'm worn and I keep the gardener's hours. Find me by the fire that keeps the flowers.",
                "The gardener's diary is on the ledge beside the boiler. It tells you where Tom was, and what boots he wears."),
            Clue(GH_SLIP,
                "Dropped by the door, I'm proof that a young man was losing at cards in town at midnight.",
                "There's a folded betting slip on the floor near the door. Check the times stamped on it."),
        };
        d.CluesRequiredToAccuse = 5;
        d.MaxAccusations = 3;
        d.Questions = new[]
        {
            Q("Who killed Lady Agatha?", 0, "Ivy Vale, the rival botanist", "Percy Thorne, the nephew", "Tom Briar, the gardener", "No one: her heart failed"),
            Q("How was she poisoned?", 0, "Nicotine soaked into her gloves", "Foxglove in her tea", "A thorn from the orchid", "Fumes from the boiler"),
            Q("Why was she killed?", 0, "To steal credit for the Moonflower", "To inherit Thornfield", "Over being pensioned off", "To pay gambling debts"),
            Q("How did the killer leave the locked glasshouse?", 0, "Climbed out through the roof vent", "With Tom's spare key", "Through the boiler tunnel", "Hid inside until morning"),
        };
        d.SolutionText =
            "<b>Ivy Vale</b> could not bear Agatha's name on the greatest discovery of the age. She signed in at 9:40, and while Agatha fussed " +
            "over her orchid, Ivy soaked Agatha's gardening gloves in <b>nicotine wash</b> from her own family's nursery.\n\nShe never left. She hid " +
            "among the palms until midnight, when the Moonflower bloomed and Agatha pulled on her gloves to tend it. The poison went through her " +
            "skin, and her heart stopped in her chair.\n\nIvy rewrote the orchid's tag in her own name. Then, with the door locked and the key in " +
            "Agatha's pocket, she climbed the ladder, wound the <b>roof vent</b> open and slipped out across the roof, leaving violet pollen on the " +
            "crank and small muddy prints on the path.\n\nPercy was at the card tables. Tom's boots would have left prints twice that size.";
        d.FailLine = "The coroner writes 'heart failure'. Next spring the Royal Society honours Miss Ivy Vale for discovering the Moonflower.";
        d.TopRankName = "Master of the Midnight Bloom";

        d.WrenCallOut = "Psst. Behind the palms, detective.";
        d.WrenIntro = "Wren. Again. Don't make that face. A rare orchid is worth more than silver to the right buyer, and my client wanted this one. " +
                      "Now it's sitting in a crime scene.";
        d.WrenWhoAmI = "Wren. I find valuable things for people who can afford them. My client wanted to buy Lady Agatha's Moonflower, and a dead " +
                       "seller makes for a messy sale. Find me the killer and I'll owe you one.";
        d.WrenSuspectsLine = "Three people wanted something from Lady Agatha. Pick one.";
        d.Suspects = new[]
        {
            Suspect("Ivy Vale", "the rival botanist",
                "Runs Vale & Daughters, the nursery across the river. She and Agatha went to war over who discovered the Moonflower. If Agatha's " +
                "name came off that discovery, Ivy's would go on. She visited last evening and says she left at a quarter past ten."),
            Suspect("Percy Thorne", "the nephew",
                "Agatha's nephew and heir, up to his neck in gambling debts. He inherits Thornfield. But he swears he spent the whole night at the " +
                "card tables in town, and the club stamps every slip."),
            Suspect("Tom Briar", "the head gardener",
                "Thirty years in Agatha's service, and she meant to pension him off next spring. He broke the lock this morning and found her. Says " +
                "he was stoking the boiler half the night, then went to bed."),
        };
        d.DeductionTips = new[]
        {
            "You have it all. Start with alibis: one suspect was at the card tables, and one wears boots far too big for those prints. Who's left?",
            "Nicotine goes through skin. She wore gloves to touch her orchid at midnight. Who had the poison, and the chance?",
            "A locked door, the key in her pocket. The footprints end at the ladder, and the midnight pollen is on the vent crank.",
        };
        d.WrenSolved = "Ivy Vale. Of course. My client will be delighted the Moonflower keeps its true name. Goodnight, detective. Don't touch the gloves.";

        d.FinchCallOut = "Oi! Detective! By the boiler, it's the only warm spot in here!";
        d.FinchIntro = "Finch again! I deliver the papers to Thornfield, and I sneak in here to get warm by the boiler. Riddles are free, as always. " +
                       "Wren's tips cost, mine just make you think.";
        d.FinchWitness = "I was dozin' by the boiler. Midnight-ish there was a glow from that flower, purple like a ghost. Then someone went creakin' " +
                         "up the ladder by the north wall, and a pane went squeak, squeak up in the roof. Didn't see who. Small, quick, smelled of " +
                         "flowers and tobacco.";
        d.FinchSolved = "The nursery lady?! That's front-page news! 'Murder Among the Orchids'! I'm off to the printer!";
    }
}
