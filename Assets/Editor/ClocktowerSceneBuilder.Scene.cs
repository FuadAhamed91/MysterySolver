using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Assembles the playable clocktower scene. Unity layout (FBX import maps Blender (x,y,z) -> (-x,z,-y)):
/// the clock-face wall is north at -Z, the study / drafting desk is against the south wall at +Z.
/// </summary>
public static partial class ClocktowerSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Clocktower.unity";
    const string VolumeProfilePath = "Assets/Settings/ClocktowerVolumeProfile.asset";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    static readonly Vector3 DeskPosition = new Vector3(0f, 0f, 4.9f);

    [MenuItem("Tools/The 3:15 Escapement/2. Build Scene")]
    static void BuildSceneMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Debug.Log(BuildScene(discardUnsavedChanges: true));
    }

    /// <summary>Builds and saves the scene. Refuses to replace a modified scene unless told to discard it.</summary>
    public static string BuildScene(bool discardUnsavedChanges = false)
    {
        var active = SceneManager.GetActiveScene();
        if (!discardUnsavedChanges && active.isDirty && active.path != ScenePath)
            return $"ABORTED: '{active.path}' has unsaved changes. Save or discard them first.";

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---------------------------------------------------------------- environment
        var env = new GameObject("Environment");
        var room = InstantiateModel("Clocktower_Room", env.transform, Vector3.zero, Quaternion.identity);
        foreach (var mf in room.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.name.StartsWith("UBX_"))
            {
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                mf.GetComponent<MeshRenderer>().enabled = false;
            }
        }
        var mechanism = InstantiateModel("Gears_Pendulum", env.transform, Vector3.zero, Quaternion.identity);
        foreach (var mf in mechanism.GetComponentsInChildren<MeshFilter>())
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        SetStaticRecursive(env);

        // ---------------------------------------------------------------- study
        var study = new GameObject("Study");
        var desk = InstantiateModel("Prop_DraftingDesk", study.transform, DeskPosition, Quaternion.Euler(0f, 180f, 0f));
        var deskWood = desk.transform.Find("DraftingDesk_Wood");
        deskWood.gameObject.AddComponent<MeshCollider>().sharedMesh = deskWood.GetComponent<MeshFilter>().sharedMesh;
        SetStaticRecursive(desk);

        // Desk-local layout helper: the desk is turned 180 degrees, so Blender desk coords (bx, by, bz)
        // land at world (bx, bz, deskZ + by).
        Vector3 OnDesk(float bx, float by, float bz) => DeskPosition + new Vector3(bx, bz, by);

        // ---- The nine pieces of evidence. Three suspects; two of them are red herrings.
        var clues = new GameObject("Evidence").transform;

        CreateClue("Prop_PocketWatch", clues, OnDesk(-0.30f, -0.33f, 0.834f), Quaternion.Euler(15f, 192f, 0f),
            "Arthur Vance's Pocket Watch",
            "A brass hunter-case watch. The crown has been pulled out to halt the movement at <b>3:03</b>, twelve minutes " +
            "before the tower clock stopped. A master horologist doesn't stop a watch by accident. He was marking a moment.",
            CaseDeductionManager.CLUE_POCKET_WATCH, "Stopped by hand at 3:03.",
            new Vector3(0f, 0.1f, -0.25f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_DeskLamp", clues, OnDesk(0.38f, 0.32f, 0.985f), Quaternion.Euler(0f, 190f, 0f),
            "Cast-Brass Gooseneck Lamp",
            "The cowl has been wrenched down onto the drafting sheet to spotlight a shaky pencil note: " +
            "<i>\"The tonic... so bitter. Hands numb. 3:03.\"</i> No name. Whoever brought that tonic, Vance wanted it seen.",
            CaseDeductionManager.CLUE_DESK_LAMP, "Lamp aimed at a note: bitter tonic, numb hands, 3:03.",
            new Vector3(0f, 0.14f, 0.2f), new Vector3(0f, -120f, 0f));

        CreateClue("Prop_ApothecaryVial", clues, OnDesk(0.15f, 0.20f, 0.985f), Quaternion.Euler(0f, 35f, 0f),
            "Apothecary Vial",
            "<i>Tinct. Aconiti</i>: monkshood. A nerve poison that numbs the hands and feet before it stops the heart. " +
            "The label is hand-inked: <i>\"Dispensed by E.H.\"</i> Almost empty.",
            CaseDeductionManager.CLUE_APOTHECARY_VIAL, "Aconite poison, dispensed by \"E.H.\"",
            new Vector3(0f, 0.1f, -0.2f), new Vector3(10f, 0f, 0f));

        CreateClue("Prop_Teacup", clues, DeskPosition + new Vector3(-1.0f, 0f, -1.0f), Quaternion.Euler(0f, 40f, 0f),
            "Cold Teacup",
            "Knocked off the desk, but it didn't break. The tea has gone cold and leaves a bitter, gritty film. On the saucer is " +
            "a second ring from another cup that stood beside it. Someone drank with him, then took their cup away.",
            CaseDeductionManager.CLUE_TEACUP, "Bitter tea. A visitor's cup was taken away.",
            new Vector3(0f, 0.1f, -0.12f), new Vector3(-35f, 0f, 0f));

        CreateClue("Prop_Prescription", clues, OnDesk(-0.08f, 0.31f, 0.987f), Quaternion.Euler(0f, 186f, 0f),
            "Folded Prescription",
            "Tucked into a pigeonhole: <i>\"For A. Vance. Evening tonic, 10 drops. Dr. E. Haze.\"</i> Someone has inked " +
            "the 10 over to read <b>40</b>, in the same brown ink as the vial's label.",
            CaseDeductionManager.CLUE_PRESCRIPTION, "Dr. E. Haze's tonic. Dose changed from 10 to 40 drops.",
            new Vector3(0f, 0.1f, -0.25f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_Letter", clues, OnDesk(-0.47f, -0.17f, 0.877f), Quaternion.Euler(15f, 172f, 0f),
            "Sealed Letter",
            "Red wax with the Vance crest. <i>\"Uncle, if you will not change the will by Friday, I swear I will find a way. " +
            "Margaret.\"</i> Dated yesterday. It's furious, but is it a confession?",
            CaseDeductionManager.CLUE_LETTER, "Margaret threatened him over the will.",
            new Vector3(0f, 0.13f, 0.02f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_Logbook", clues, new Vector3(5.6f, 0.905f, 0.05f), Quaternion.Euler(0f, 8f, 0f),
            "Night Watchman's Logbook",
            "Left on the window ledge. <i>11:02, Mr. Crane leaves, cursing about stolen designs. 2:40, Dr. Haze comes up; " +
            "says Mr. V sent for his tonic. 3:10, Dr. Haze down in a hurry, no hat. 3:15, great clock STOPS. " +
            "3:30, Miss Vance back from the gala, raises the alarm.</i>",
            CaseDeductionManager.CLUE_LOGBOOK, "Crane left 11:02. Haze 2:40 to 3:10. Clock stopped 3:15. Margaret back 3:30.",
            new Vector3(0f, 0.12f, 0.02f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_Chisel", clues, new Vector3(0.85f, 0f, -1.25f), Quaternion.Euler(0f, 35f, 0f),
            "Engraved Chisel",
            "Stamped <b>T. CRANE</b>. Its edge matches gouges in the gear frame, but the gouges and the blade are both thick " +
            "with old rust. Those marks were made months ago, not tonight.",
            CaseDeductionManager.CLUE_CHISEL, "Crane's chisel. The tampering marks are months old.",
            new Vector3(0f, 0.1f, -0.15f), new Vector3(0f, 90f, 0f));

        // The pendulum's suspension pin can only be examined in place.
        var pin = new GameObject("PendulumPin_Examine");
        pin.transform.SetParent(clues, false);
        pin.transform.position = new Vector3(0f, 1.85f, -2.3f);
        pin.AddComponent<BoxCollider>().size = new Vector3(0.4f, 0.4f, 0.4f);
        var pinItem = pin.AddComponent<InspectableItem>();
        pinItem.CanPickUp = false;
        pinItem.ItemName = "Pendulum Suspension Pin";
        pinItem.ItemDescription =
            "The pendulum didn't snap. Its suspension pin was <b>drawn out by hand</b>, and the fresh oil is smeared by fingers " +
            "that were shaking. A trail of spilled tea runs straight here from the desk. Whoever pulled it came from Vance's chair.";
        pinItem.ClueID = CaseDeductionManager.CLUE_PENDULUM_PIN;
        pinItem.NotebookNote = "Pin pulled by a shaking hand. A tea trail leads here from the desk.";

        // A dim lantern glow so the logbook on the west ledge can be found.
        var lantern = new GameObject("WatchmanLantern_Glow").AddComponent<Light>();
        lantern.transform.SetParent(clues, false);
        lantern.transform.position = new Vector3(5.3f, 1.35f, 0.4f);
        lantern.type = LightType.Point;
        lantern.color = Hex("FFB866");
        lantern.intensity = 0.7f;
        lantern.range = 2.2f;
        lantern.shadows = LightShadows.None;

        // Vance's last note on the drafting sheet, lit by the lamp's lowered cowl (board slopes 15 degrees).
        var note = new GameObject("VanceNote_Text").AddComponent<TextMeshPro>();
        note.transform.SetParent(desk.transform.parent, false);
        note.transform.SetPositionAndRotation(OnDesk(0.17f, -0.17f, 0.8995f), Quaternion.Euler(75f, -4f, 0f));
        note.rectTransform.sizeDelta = new Vector2(0.48f, 0.32f);
        note.text = "The tonic... so bitter.\nHands numb.\n<size=150%>3:03</size>";
        note.fontSize = 0.36f;
        note.fontStyle = FontStyles.Italic;
        note.color = new Color(0.08f, 0.08f, 0.16f, 0.9f);
        note.alignment = TextAlignmentOptions.Center;
        note.textWrappingMode = TextWrappingModes.NoWrap;

        AddCrimeSceneTape(env.transform);

        // ---------------------------------------------------------------- player rig
        var player = new GameObject("Player");
        player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 180f, 0f));
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, -0.1f, 0f); // capsule bottom rests on the floor at y = 0
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.04f;
        var playerInput = player.AddComponent<PlayerInput>();
        playerInput.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        playerInput.defaultActionMap = "Player";
        playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        var fpc = player.AddComponent<FirstPersonController>();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.6f, 0f); // world eye height 1.6 m
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 60f;
        cam.fieldOfView = 70f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("070A10");
        camGo.AddComponent<AudioListener>();
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
        camData.dithering = true; // removes banding in the dark light falloff
        fpc.CameraPivot = camGo.transform;

        var anchor = new GameObject("InspectionAnchor").transform;
        anchor.SetParent(camGo.transform, false);
        anchor.localPosition = new Vector3(0f, -0.08f, 0.45f);

        var inspectLight = new GameObject("InspectionFillLight").AddComponent<Light>();
        inspectLight.transform.SetParent(camGo.transform, false);
        inspectLight.transform.localPosition = new Vector3(0.1f, 0.15f, 0.15f);
        inspectLight.type = LightType.Point;
        inspectLight.color = Hex("FFE2B8");
        inspectLight.intensity = 0.3f;
        inspectLight.range = 1.4f;
        inspectLight.shadows = LightShadows.None;

        var raycaster = camGo.AddComponent<InspectionRaycaster>();
        raycaster.InspectionAnchor = anchor;
        raycaster.Player = fpc;
        raycaster.PlayerInput = playerInput;
        raycaster.InspectionLight = inspectLight;
        raycaster.InteractRange = 3f;

        // ---------------------------------------------------------------- lighting
        var lighting = new GameObject("Lighting");
        var moon = new GameObject("Moonlight_Directional").AddComponent<Light>();
        moon.transform.SetParent(lighting.transform, false);
        // Enters through the north clock opening (-Z) and falls south and down across the gear train.
        moon.transform.rotation = Quaternion.Euler(24f, 8f, 0f);
        moon.type = LightType.Directional;
        moon.color = Hex("78909C");
        moon.intensity = 2.4f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 1f;
        RenderSettings.sun = moon;

        var tungsten = new GameObject("DeskLight_Tungsten2700K").AddComponent<Light>();
        tungsten.transform.SetParent(lighting.transform, false);
        tungsten.transform.position = OnDesk(0.1f, -0.15f, 1.75f);
        tungsten.type = LightType.Point;
        tungsten.color = Hex("FFA726"); // ~2700K tungsten, expressed as the requested colour
        tungsten.intensity = 1.5f;
        tungsten.range = 5.5f;
        tungsten.shadows = LightShadows.Soft;

        var probe = new GameObject("RoomReflectionProbe").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(lighting.transform, false);
        probe.transform.position = new Vector3(0f, 2.2f, 0f);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(12f, 8.5f, 12f);
        probe.boxProjection = true;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Hex("3B4762");
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Hex("0A0E15");
        RenderSettings.fogDensity = 0.035f;

        var volumeGo = new GameObject("Global Volume");
        volumeGo.transform.SetParent(lighting.transform, false);
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = CreateVolumeProfile();

        // ---------------------------------------------------------------- UI + case manager
        var ui = BuildUI(out var crosshair, out var prompt, out var inspectPanel, out var inspectTitle,
                         out var inspectDesc, out var tracker, out var deductionPanel, out var deductionTitle, out var deductionBody,
                         out var deductionHint);
        raycaster.Crosshair = crosshair;
        raycaster.PromptText = prompt;
        raycaster.InspectionPanel = inspectPanel;
        raycaster.InspectTitleText = inspectTitle;
        raycaster.InspectDescriptionText = inspectDesc;

        var manager = new GameObject("CaseDeductionManager").AddComponent<CaseDeductionManager>();
        manager.Player = fpc;
        manager.PlayerInput = playerInput;
        manager.TrackerText = tracker;
        manager.DeductionPanel = deductionPanel;
        manager.DeductionTitleText = deductionTitle;
        manager.DeductionBodyText = deductionBody;
        manager.DeductionHintText = deductionHint;

        var board = BuildCaseBoard(ui.transform);
        board.Case = manager;
        board.Player = fpc;
        board.PlayerInput = playerInput;
        board.Raycaster = raycaster;

        // ---------------------------------------------------------------- the informant
        var dialogue = BuildDialogueUI(ui.transform);
        dialogue.Player = fpc;
        dialogue.PlayerInput = playerInput;
        var wren = BuildInformant(cam.transform);
        wren.Dialogue = dialogue;
        var finch = BuildFinch(cam.transform);
        finch.Dialogue = dialogue;

        // ---------------------------------------------------------------- corkboard in the study
        var cork = InstantiateModel("Prop_CaseBoard", study.transform, new Vector3(1.7f, 0f, 3.9f),
                                    Quaternion.LookRotation(new Vector3(-1.7f, 0f, -3.9f)));
        var corkBox = cork.AddComponent<BoxCollider>();
        corkBox.center = new Vector3(0f, 1.25f, 0f);
        corkBox.size = new Vector3(1.25f, 0.9f, 0.35f);
        cork.AddComponent<CaseBoardStation>().Board = board;
        PointLight(study.transform, "CaseBoard_Light", new Vector3(1.45f, 2.1f, 3.4f), "FFD9A0", 1.1f, 2.4f);

        // ---------------------------------------------------------------- scary soundscape
        var sound = new GameObject("ScarySoundscape");
        var soundscape = sound.AddComponent<ScarySoundscape>();
        soundscape.Listener = cam.transform;
        var crimeScene = new GameObject("CrimeScene_HeartbeatFocus").transform;
        crimeScene.SetParent(sound.transform, false);
        crimeScene.position = new Vector3(-1.3f, 0.6f, -0.05f); // the pendulum bob
        soundscape.CrimeScene = crimeScene;
        var reverb = sound.AddComponent<AudioReverbZone>(); // stone-tower echo
        reverb.reverbPreset = AudioReverbPreset.StoneCorridor;
        reverb.minDistance = 7f;
        reverb.maxDistance = 14f;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        // ---------------------------------------------------------------- save
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildFirst(ScenePath);
        AssetDatabase.SaveAssets();
        return $"Built {ScenePath}: evidence={clues.GetComponentsInChildren<InspectableItem>().Length} ui={ui.name}";
    }

    // ==================================================================== helpers

    // ==================================================================== informant NPC

    /// <summary>
    /// Places a character model facing the room centre, gives its head parts a neck pivot so they can turn,
    /// and adds a capsule collider for talking/blocking. Returns the unpacked root.
    /// </summary>
    static T BuildNPC<T>(string model, string objectName, Vector3 pos, float neckHeight, float height,
                         string[] headParts, string bodyPart, Transform playerCamera) where T : TalkingNPC
    {
        var go = InstantiateModel(model, null, pos, Quaternion.LookRotation(new Vector3(-pos.x, 0f, -pos.z)));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = objectName;

        var headPivot = new GameObject("HeadPivot").transform;
        headPivot.SetParent(go.transform, false);
        headPivot.localPosition = new Vector3(0f, neckHeight, 0f);
        foreach (var part in headParts)
            go.transform.Find(part).SetParent(headPivot, true);

        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, height / 2f, 0f);
        capsule.height = height;
        capsule.radius = 0.3f;

        var npc = go.AddComponent<T>();
        npc.HeadPivot = headPivot;
        npc.Body = go.transform.Find(bodyPart);
        npc.PlayerCamera = playerCamera;
        return npc;
    }

    static Light PointLight(Transform parent, string name, Vector3 worldPos, string hex, float intensity, float range)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.position = worldPos;
        light.type = LightType.Point;
        light.color = Hex(hex);
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    static InformantNPC BuildInformant(Transform playerCamera)
    {
        // Lurking beside the east (-X) lancet window. Cold rim light so Wren reads as a silhouette first.
        var npc = BuildNPC<InformantNPC>("NPC_Informant", "Informant_Wren", new Vector3(-4.6f, 0f, 1.3f), 1.46f, 1.8f,
            new[] { "Wren_Head", "Wren_Scarf", "Wren_Fedora", "Wren_HatBand" }, "Wren_Coat", playerCamera);
        PointLight(npc.transform, "Wren_RimLight", new Vector3(-5.2f, 2.1f, 0.9f), "9FB8FF", 1.4f, 2.6f);
        return npc;
    }

    static RiddlerNPC BuildFinch(Transform playerCamera)
    {
        // Hiding behind the gear frame on the west (+X) side, lit by the candle in Finch's hand.
        var npc = BuildNPC<RiddlerNPC>("NPC_Finch", "Messenger_Finch", new Vector3(3.6f, 0f, -3.35f), 1.24f, 1.5f,
            new[] { "Finch_Head", "Finch_Cap" }, "Finch_Shirt", playerCamera);

        // Candle flame: a small unlit glowing teardrop on the candle, plus the light it casts.
        var candle = npc.transform.Find("Finch_Candle");
        var flameTop = candle.GetComponent<Renderer>().bounds.center + Vector3.up * 0.06f;
        var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flame.name = "Finch_Flame";
        flame.transform.SetParent(npc.transform, false);
        flame.transform.position = flameTop;
        flame.transform.localScale = new Vector3(0.018f, 0.034f, 0.018f);
        Object.DestroyImmediate(flame.GetComponent<Collider>());
        var flameMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Flame.mat");
        if (flameMat == null)
        {
            flameMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(flameMat, MaterialDir + "/M_Flame.mat");
        }
        flameMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.38f) * 2.2f);
        EditorUtility.SetDirty(flameMat);
        var flameRenderer = flame.GetComponent<Renderer>();
        flameRenderer.sharedMaterial = flameMat;
        flameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        npc.Candle = PointLight(npc.transform, "Finch_CandleLight", flameTop + Vector3.up * 0.05f, "FFB060", 0.9f, 2.4f);
        return npc;
    }

    static DialogueUI BuildDialogueUI(Transform root)
    {
        var dialogue = root.gameObject.AddComponent<DialogueUI>(); // on the always-active canvas
        var panel = Panel("DialoguePanel", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f),
                          new Vector2(1300f, 410f), new Color(0.04f, 0.035f, 0.03f, 0.9f));
        dialogue.Panel = panel;
        dialogue.SpeakerText = Text("Speaker", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -22f),
                                    new Vector2(600f, 44f), "???", 32f, Gold, TextAlignmentOptions.TopLeft);
        dialogue.SpeakerText.fontStyle = FontStyles.Bold;
        dialogue.LineText = Text("Line", panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -72f),
                                 new Vector2(1220f, 120f), "", 24f, Parchment, TextAlignmentOptions.TopLeft);
        dialogue.ChoiceButtons = new Button[4];
        dialogue.ChoiceLabels = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            dialogue.ChoiceButtons[i] = MakeButton($"Choice{i + 1}", panel.transform, new Vector2(40f, -200f - i * 50f),
                                                   new Vector2(1220f, 44f), "", 22f, out var label, new Color(0.13f, 0.11f, 0.09f, 1f));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontStyle = FontStyles.Normal;
            label.color = Parchment;
            label.margin = new Vector4(18f, 0f, 18f, 0f);
            dialogue.ChoiceLabels[i] = label;
        }
        Text("DialogueHint", panel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 8f), new Vector2(700f, 24f),
             "Click or press 1-4   ·   E: skip text   ·   Esc: leave", 16f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.BottomRight);
        panel.SetActive(false);

        dialogue.SubtitleText = Text("Subtitle", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 380f),
                                     new Vector2(1400f, 44f), "", 26f, Parchment, TextAlignmentOptions.Center);
        dialogue.SubtitleText.gameObject.SetActive(false);
        return dialogue;
    }

    // ==================================================================== crime-scene tape

    const string TapeMaterialPath = MaterialDir + "/M_DangerTape.mat";

    /// <summary>
    /// Decorative "DANGER - DO NOT CROSS" tape: a cordon around the fallen pendulum on iron stanchions, a line across
    /// the gear frame and crossed tape over both lancet windows. Tape has no colliders so it never blocks the player or clues.
    /// </summary>
    static void AddCrimeSceneTape(Transform parent)
    {
        var root = new GameObject("CrimeSceneTape").transform;
        root.SetParent(parent, false);

        var tapeMat = AssetDatabase.LoadAssetAtPath<Material>(TapeMaterialPath);
        if (tapeMat == null)
        {
            tapeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(tapeMat, TapeMaterialPath);
        }
        tapeMat.SetColor("_BaseColor", Hex("F5C400"));
        tapeMat.SetFloat("_Smoothness", 0.45f);
        tapeMat.SetFloat("_Metallic", 0f);
        tapeMat.EnableKeyword("_EMISSION");
        tapeMat.SetColor("_EmissionColor", Hex("F5C400") * 0.12f); // stays readable in the dark corners
        tapeMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(tapeMat);
        var ironMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Iron.mat");

        // Stanchions fencing the fallen pendulum (open toward the player's start so nobody is boxed in).
        Vector3[] posts =
        {
            new Vector3(-2.25f, 0f, -1.6f), new Vector3(-2.25f, 0f, 0.95f), new Vector3(-0.75f, 0f, 1.05f),
        };
        const float postTop = 1.0f;
        for (int i = 0; i < posts.Length; i++)
            Stanchion(root, $"Stanchion_{i}", posts[i], postTop, ironMat);

        float h = postTop - 0.06f;
        TapeLine(root, posts[0] + Vector3.up * h, posts[1] + Vector3.up * h, 0.06f, tapeMat);
        TapeLine(root, posts[1] + Vector3.up * h, posts[2] + Vector3.up * h, 0.05f, tapeMat);
        // Tied off to the left mechanism-frame post.
        TapeLine(root, posts[0] + Vector3.up * h, new Vector3(-1.55f, 1.05f, -2.82f), 0.04f, tapeMat);
        // Across the front of the gear frame, below the pendulum pin so it can still be examined.
        TapeLine(root, new Vector3(-1.55f, 1.15f, -2.82f), new Vector3(3.05f, 1.15f, -2.82f), 0.08f, tapeMat);

        // Crossed tape over the east (-X) and west (+X) lancet windows, just inside the wall face.
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * 5.47f;
            TapeLine(root, new Vector3(x, 3.6f, -0.62f), new Vector3(x, 1.2f, 0.62f), 0f, tapeMat);
            TapeLine(root, new Vector3(x, 3.6f, 0.62f), new Vector3(x, 1.2f, -0.62f), 0f, tapeMat);
        }
    }

    static void Stanchion(Transform parent, string name, Vector3 basePos, float height, Material mat)
    {
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = name;
        post.transform.SetParent(parent, false);
        post.transform.position = basePos + Vector3.up * (height / 2f);
        post.transform.localScale = new Vector3(0.05f, height / 2f, 0.05f);
        post.GetComponent<Renderer>().sharedMaterial = mat;

        var foot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        foot.name = name + "_Base";
        foot.transform.SetParent(post.transform.parent, false);
        foot.transform.position = basePos + Vector3.up * 0.015f;
        foot.transform.localScale = new Vector3(0.26f, 0.015f, 0.26f);
        foot.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(foot.GetComponent<Collider>());
    }

    /// <summary>A tape run from a to b, sagging by <paramref name="sag"/> metres at the middle (two straight pieces).</summary>
    static void TapeLine(Transform parent, Vector3 a, Vector3 b, float sag, Material mat)
    {
        Vector3 mid = (a + b) / 2f + Vector3.down * sag;
        TapeSegment(parent, a, mid, mat);
        TapeSegment(parent, mid, b, mat);
    }

    static void TapeSegment(Transform parent, Vector3 a, Vector3 b, Material mat)
    {
        const float width = 0.075f;
        Vector3 dir = b - a;
        float length = dir.magnitude;
        // Keep the tape face vertical (perpendicular to the horizontal), like real cordon tape.
        Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
        if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
        Vector3 faceNormal = side.normalized;
        Quaternion rot = Quaternion.LookRotation(faceNormal, Vector3.Cross(dir.normalized, faceNormal) * -1f);

        var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        band.name = "Tape";
        band.transform.SetParent(parent, false);
        band.transform.SetPositionAndRotation((a + b) / 2f, rot);
        // Local X runs along the tape, Y across it, Z through it.
        band.transform.localScale = new Vector3(length + 0.01f, width, 0.003f);
        band.GetComponent<Renderer>().sharedMaterial = mat;
        band.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.DestroyImmediate(band.GetComponent<Collider>());

        // Printed text on both faces.
        foreach (float face in new[] { -1f, 1f })
        {
            var t = new GameObject("TapeText").AddComponent<TextMeshPro>();
            t.transform.SetParent(band.transform.parent, false);
            t.transform.SetPositionAndRotation((a + b) / 2f + rot * Vector3.forward * (0.0025f * face),
                                               rot * Quaternion.Euler(0f, face > 0 ? 180f : 0f, 0f));
            t.rectTransform.sizeDelta = new Vector2(length, width);
            var sb = new System.Text.StringBuilder();
            int repeats = Mathf.Max(1, Mathf.CeilToInt(length / 0.55f));
            for (int i = 0; i < repeats; i++) sb.Append("DANGER • DO NOT CROSS • ");
            t.text = sb.ToString();
            t.fontSize = 0.42f;
            t.fontStyle = FontStyles.Bold;
            t.color = new Color(0.05f, 0.05f, 0.05f, 1f);
            t.alignment = TextAlignmentOptions.Midline;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Truncate;
        }
    }

    static GameObject InstantiateModel(string model, Transform parent, Vector3 pos, Quaternion rot)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{model}.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        return go;
    }

    static GameObject CreateClue(string model, Transform parent, Vector3 pos, Quaternion rot,
                                 string title, string description, string clueId, string notebookNote,
                                 Vector3 displayOffset, Vector3 displayEuler)
    {
        var go = InstantiateModel(model, parent, pos, rot);

        // Display offsets keep held items above the bottom inspection panel.
        // Box collider around all child meshes in root-local space, padded so tiny props are easy to target.
        var renderers = go.GetComponentsInChildren<MeshRenderer>();
        var b = new Bounds();
        bool first = true;
        foreach (var r in renderers)
        {
            var mb = r.GetComponent<MeshFilter>().sharedMesh.bounds;
            var m = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            foreach (var corner in Corners(mb))
            {
                var p = m.MultiplyPoint3x4(corner);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }
        const float minSize = 0.12f;
        var box = go.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = Vector3.Max(b.size, new Vector3(minSize, minSize * 0.5f, minSize));

        var item = go.AddComponent<InspectableItem>();
        item.ItemName = title;
        item.ItemDescription = description;
        item.ClueID = clueId;
        item.NotebookNote = notebookNote;
        item.DisplayOffset = displayOffset;
        item.DisplayEulerRotation = displayEuler;
        return go;
    }

    static Vector3[] Corners(Bounds b)
    {
        var c = new Vector3[8];
        for (int i = 0; i < 8; i++)
            c[i] = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        return c;
    }

    static void SetStaticRecursive(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    static VolumeProfile CreateVolumeProfile()
    {
        AssetDatabase.DeleteAsset(VolumeProfilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumeProfilePath);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.45f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(Hex("FFE6C4"));

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(Color.black);

        var tonemap = profile.Add<Tonemapping>(true);
        tonemap.mode.Override(TonemappingMode.Neutral);

        foreach (var component in profile.components)
            AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static void AddSceneToBuildFirst(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == path);
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }

    // ==================================================================== UI

    static readonly Color Gold = new Color(1f, 0.78f, 0.45f);
    static readonly Color Parchment = new Color(0.91f, 0.87f, 0.8f);

    static GameObject BuildUI(out GameObject crosshair, out TMP_Text prompt, out GameObject inspectPanel,
                              out TMP_Text inspectTitle, out TMP_Text inspectDesc, out TMP_Text tracker,
                              out GameObject deductionPanel, out TMP_Text deductionTitle, out TMP_Text deductionBody,
                              out TMP_Text deductionHint)
    {
        var canvasGo = new GameObject("HUD Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); // required for mouse clicks on the Case Board / dialogue buttons
        var root = canvasGo.transform;

        // Centre crosshair + interaction prompt
        crosshair = Panel("Crosshair", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f),
                          new Color(1f, 1f, 1f, 0.85f));
        prompt = Text("InteractionPrompt", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -52f),
                      new Vector2(900f, 48f), "[E] Inspect", 26f, Parchment, TextAlignmentOptions.Center);

        // Top deduction tracker
        var trackerBg = Panel("ClueTracker", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f),
                              new Vector2(860f, 62f), new Color(0.03f, 0.03f, 0.04f, 0.7f));
        tracker = Text("TrackerText", trackerBg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                       "Evidence 0/9  ·  [TAB] Case Board", 30f, Gold, TextAlignmentOptions.Center);

        var titleCard = Text("GameTitle", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -28f),
                             new Vector2(600f, 40f), "THE 3:15 ESCAPEMENT", 22f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.TopLeft);
        titleCard.characterSpacing = 8f;
        Text("ControlsHint", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 24f), new Vector2(900f, 32f),
             "WASD Move   ·   Mouse Look   ·   Shift Sprint   ·   E Inspect   ·   Tab Case Board", 18f, new Color(1f, 1f, 1f, 0.4f),
             TextAlignmentOptions.BottomLeft);

        // Bottom inspection panel
        inspectPanel = Panel("InspectionPanel", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f),
                             new Vector2(1240f, 300f), new Color(0.04f, 0.035f, 0.03f, 0.85f));
        inspectTitle = Text("ItemTitle", inspectPanel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -46f),
                            new Vector2(-80f, 46f), "Item", 34f, Gold, TextAlignmentOptions.TopLeft);
        inspectTitle.fontStyle = FontStyles.Bold;
        inspectDesc = Text("ItemDescription", inspectPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -8f),
                           new Vector2(-80f, -120f), "Description", 22f, Parchment, TextAlignmentOptions.TopLeft);
        Text("InspectHint", inspectPanel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 16f),
             new Vector2(700f, 28f), "Hold LMB: Rotate   ·   RMB / Esc / E: Done", 18f, new Color(1f, 1f, 1f, 0.5f),
             TextAlignmentOptions.BottomRight);
        inspectPanel.SetActive(false);

        // Verdict modal (solved or failed)
        deductionPanel = Panel("DeductionModal", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.78f));
        var card = Panel("Card", deductionPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                         new Vector2(1280f, 860f), new Color(0.075f, 0.062f, 0.05f, 0.97f));
        deductionTitle = Text("DeductionTitle", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -44f),
                              new Vector2(-120f, 60f), "Case Solved", 44f, Gold, TextAlignmentOptions.Top);
        deductionTitle.fontStyle = FontStyles.Bold;
        Panel("Divider", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 2f),
              new Color(1f, 0.78f, 0.45f, 0.5f));
        deductionBody = Text("DeductionBody", card.transform, Vector2.zero, Vector2.one, new Vector2(0f, -24f),
                             new Vector2(-140f, -260f), "", 23f, Parchment, TextAlignmentOptions.TopLeft);
        deductionBody.lineSpacing = 6f;
        deductionHint = Text("CloseHint", card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(900f, 30f),
             "", 20f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Bottom);
        deductionPanel.SetActive(false);

        return canvasGo;
    }

    static CaseBoard BuildCaseBoard(Transform root)
    {
        var panel = Panel("CaseBoard", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.82f));
        // The controller lives on the always-active canvas: a script on the hidden panel would never get Update().
        var board = root.gameObject.AddComponent<CaseBoard>();
        board.Panel = panel;
        var card = Panel("Card", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                         new Vector2(1560f, 900f), new Color(0.2f, 0.15f, 0.1f, 0.98f));
        var title = Text("Title", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -38f),
                         new Vector2(-80f, 60f), "CASE BOARD: WHO KILLED ARTHUR VANCE?", 42f, Gold, TextAlignmentOptions.Top);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4f;
        Panel("Divider", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1440f, 2f),
              new Color(1f, 0.78f, 0.45f, 0.45f));

        // Left: evidence notebook
        var notebook = Panel("Notebook", card.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(330f, -30f),
                             new Vector2(600f, -200f), new Color(0.93f, 0.88f, 0.77f, 1f)); // parchment notebook
        board.EvidenceText = Text("EvidenceText", notebook.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-44f, -36f),
                                  "", 22f, new Color(0.16f, 0.11f, 0.07f, 1f), TextAlignmentOptions.TopLeft);
        board.EvidenceText.enableAutoSizing = true;
        board.EvidenceText.fontSizeMin = 15f;
        board.EvidenceText.fontSizeMax = 22f;
        board.EvidenceText.overflowMode = TextOverflowModes.Ellipsis;

        // Right: the four questions
        int n = 4;
        board.QuestionTexts = new TMP_Text[n];
        board.AnswerTexts = new TMP_Text[n];
        board.PrevButtons = new Button[n];
        board.NextButtons = new Button[n];
        for (int i = 0; i < n; i++)
        {
            float y = -150f - i * 125f;
            board.QuestionTexts[i] = Text($"Q{i + 1}", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                          new Vector2(690f, y), new Vector2(820f, 40f), "", 28f, Gold, TextAlignmentOptions.Left);
            board.QuestionTexts[i].rectTransform.pivot = new Vector2(0f, 1f);
            board.PrevButtons[i] = MakeButton($"Q{i + 1}_Prev", card.transform, new Vector2(690f, y - 46f), new Vector2(60f, 54f), "<", 30f, out _);
            var answerBg = Panel($"Q{i + 1}_Answer", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(760f, y - 46f),
                                 new Vector2(600f, 54f), new Color(0.32f, 0.26f, 0.19f, 1f));
            ((RectTransform)answerBg.transform).pivot = new Vector2(0f, 1f);
            board.AnswerTexts[i] = Text("Text", answerBg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                                        "", 28f, Color.white, TextAlignmentOptions.Center);
            board.NextButtons[i] = MakeButton($"Q{i + 1}_Next", card.transform, new Vector2(1370f, y - 46f), new Vector2(60f, 54f), ">", 30f, out _);
        }

        board.AccuseButton = MakeButton("AccuseButton", card.transform, new Vector2(690f, -660f), new Vector2(740f, 70f), "ACCUSE", 30f,
                                        out var accuseLabel, new Color(0.62f, 0.14f, 0.09f, 1f));
        board.AccuseButtonText = accuseLabel;
        board.StatusText = Text("Status", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(690f, -742f),
                                new Vector2(820f, 34f), "", 21f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopLeft);
        board.StatusText.rectTransform.pivot = new Vector2(0f, 1f);
        board.FeedbackText = Text("Feedback", card.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(690f, -780f),
                                  new Vector2(820f, 60f), "", 23f, Parchment, TextAlignmentOptions.TopLeft);
        board.FeedbackText.rectTransform.pivot = new Vector2(0f, 1f);
        Text("CloseHint", card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(900f, 28f),
             "Tab / Esc: close board", 18f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Bottom);

        panel.SetActive(false);
        return board;
    }

    static Button MakeButton(string name, Transform parent, Vector2 topLeft, Vector2 size, string label, float fontSize,
                             out TMP_Text labelText, Color? color = null)
    {
        var go = Panel(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size, color ?? new Color(0.36f, 0.27f, 0.16f, 1f));
        ((RectTransform)go.transform).pivot = new Vector2(0f, 1f);
        var img = go.GetComponent<Image>();
        img.raycastTarget = true;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.35f, 1.25f, 1.1f, 1f);
        colors.pressedColor = new Color(0.8f, 0.75f, 0.7f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;
        labelText = Text("Label", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, label, fontSize, Gold,
                         TextAlignmentOptions.Center);
        labelText.fontStyle = FontStyles.Bold;
        return button;
    }

    static GameObject Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Layout(rt, anchorMin, anchorMax, pos, size);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
                                string text, float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Layout(rt, anchorMin, anchorMax, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Layout(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
