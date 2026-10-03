using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Multi-case scene building: the shared case rig (player, HUD, case file, Case Board, dialogue, intro card), the
/// lighthouse and greenhouse scenes, the main menu, and "Build Everything".
/// Positions are Unity coordinates; the Blender generators' (x, y, z) land at Unity (-x, z, -y).
/// </summary>
public static partial class ClocktowerSceneBuilder
{
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string LighthouseScenePath = "Assets/Scenes/Lighthouse.unity";
    const string GreenhouseScenePath = "Assets/Scenes/Greenhouse.unity";

    /// <summary>References to everything the shared case rig creates.</summary>
    class CaseRig
    {
        public FirstPersonController Player;
        public PlayerInput Input;
        public Camera Camera;
        public InspectionRaycaster Raycaster;
        public GameObject Ui;
        public CaseDeductionManager Manager;
        public CaseBoard Board;
        public DialogueUI Dialogue;
    }

    // ==================================================================== build everything

    [MenuItem("Tools/The Spooky Crime Scene/2. Build Everything (menu + all cases)")]
    static void BuildAllMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Debug.Log(BuildAll());
    }

    public static string BuildAll()
    {
        var log = new List<string> { "cases: " + CaseLibrary.EnsureAssets().Cases.Length, ConfigureImports() };
        log.Add(BuildScene(discardUnsavedChanges: true));
        log.Add(BuildLighthouseScene());
        log.Add(BuildGreenhouseScene());
        log.Add(BuildMainMenuScene()); // last, so it is the scene left open
        EnsureBuildScenes();
        return string.Join("\n", log);
    }

    /// <summary>Build Settings: the menu first, then the cases in order. Other scenes are left out of the build.</summary>
    static void EnsureBuildScenes()
    {
        var list = new List<EditorBuildSettingsScene>();
        foreach (var path in new[] { MenuScenePath, ScenePath, LighthouseScenePath, GreenhouseScenePath })
            if (System.IO.File.Exists(path))
                list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>UI input bound to the project's own actions asset (its "UI" map), which persists in the saved scene.</summary>
    static void AddUIInput(GameObject eventSystem)
    {
        var module = eventSystem.AddComponent<InputSystemUIInputModule>();
        module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
    }

    // ==================================================================== shared case rig

    static CaseRig BuildCaseRig(CaseDefinition def, CaseCatalog catalog, Vector3 spawn, float yaw, float farClip, Color background)
    {
        var rig = new CaseRig();

        var player = new GameObject("Player");
        player.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, yaw, 0f));
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, -0.1f, 0f); // capsule bottom rests on the floor at y = 0 when spawned at y = 1
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.04f;
        rig.Input = player.AddComponent<PlayerInput>();
        rig.Input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        rig.Input.defaultActionMap = "Player";
        rig.Input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        rig.Player = player.AddComponent<FirstPersonController>();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.6f, 0f); // world eye height 1.6 m
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = farClip;
        cam.fieldOfView = 70f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        camGo.AddComponent<AudioListener>();
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
        camData.dithering = true;
        rig.Player.CameraPivot = camGo.transform;
        rig.Camera = cam;

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

        rig.Raycaster = camGo.AddComponent<InspectionRaycaster>();
        rig.Raycaster.InspectionAnchor = anchor;
        rig.Raycaster.Player = rig.Player;
        rig.Raycaster.PlayerInput = rig.Input;
        rig.Raycaster.InspectionLight = inspectLight;
        rig.Raycaster.InteractRange = 3f;

        rig.Ui = BuildUI(out var crosshair, out var prompt, out var inspectPanel, out var inspectTitle,
                         out var inspectDesc, out var tracker, out var deductionPanel, out var deductionTitle, out var deductionBody,
                         out var deductionHint);
        rig.Raycaster.Crosshair = crosshair;
        rig.Raycaster.PromptText = prompt;
        rig.Raycaster.InspectionPanel = inspectPanel;
        rig.Raycaster.InspectTitleText = inspectTitle;
        rig.Raycaster.InspectDescriptionText = inspectDesc;

        rig.Manager = new GameObject("CaseDeductionManager").AddComponent<CaseDeductionManager>();
        rig.Manager.Definition = def;
        rig.Manager.Catalog = catalog;
        rig.Manager.Player = rig.Player;
        rig.Manager.PlayerInput = rig.Input;
        rig.Manager.TrackerText = tracker;
        rig.Manager.DeductionPanel = deductionPanel;
        rig.Manager.DeductionTitleText = deductionTitle;
        rig.Manager.DeductionBodyText = deductionBody;
        rig.Manager.DeductionHintText = deductionHint;

        rig.Board = BuildCaseBoard(rig.Ui.transform);
        rig.Board.Case = rig.Manager;
        rig.Board.Player = rig.Player;
        rig.Board.PlayerInput = rig.Input;
        rig.Board.Raycaster = rig.Raycaster;

        rig.Dialogue = BuildDialogueUI(rig.Ui.transform);
        rig.Dialogue.Player = rig.Player;
        rig.Dialogue.PlayerInput = rig.Input;

        var intro = BuildIntroCard(rig.Ui.transform);
        intro.Case = rig.Manager;
        intro.Player = rig.Player;
        intro.PlayerInput = rig.Input;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        AddUIInput(eventSystem);

        var volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = CreateVolumeProfile();
        return rig;
    }

    static CaseIntro BuildIntroCard(Transform root)
    {
        var intro = root.gameObject.AddComponent<CaseIntro>(); // on the always-active canvas
        var panel = Panel("IntroCard", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.01f, 0.02f, 0.93f));
        intro.Group = panel.AddComponent<CanvasGroup>();
        var card = panel.transform;
        intro.NumberText = Text("CaseNumber", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f),
                                new Vector2(1200f, 40f), "CASE 1", 26f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Center);
        intro.NumberText.characterSpacing = 12f;
        intro.TitleText = Text("CaseTitle", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f),
                               new Vector2(1500f, 90f), "Title", 64f, Gold, TextAlignmentOptions.Center);
        intro.TitleText.fontStyle = FontStyles.Bold;
        intro.LocationText = Text("CaseLocation", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 115f),
                                  new Vector2(1200f, 40f), "Location", 26f, new Color(0.75f, 0.8f, 0.9f, 0.85f), TextAlignmentOptions.Center);
        intro.LocationText.fontStyle = FontStyles.Italic;
        Panel("Divider", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(700f, 2f),
              new Color(1f, 0.78f, 0.45f, 0.5f));
        intro.BodyText = Text("CaseIntroBody", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f),
                              new Vector2(1150f, 300f), "", 27f, Parchment, TextAlignmentOptions.Top);
        intro.BodyText.lineSpacing = 8f;
        intro.PromptText = Text("BeginPrompt", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f),
                                new Vector2(900f, 40f), "", 24f, Color.white, TextAlignmentOptions.Center);
        panel.SetActive(false);
        return intro;
    }

    static ScarySoundscape BuildSoundscape(ScarySoundscape.SoundTheme theme, CaseRig rig, Vector3 crimeScene,
                                           AudioReverbPreset reverbPreset, Vector3 ambientCentre, Vector3 ambientArea, float ambientHeight)
    {
        var sound = new GameObject("ScarySoundscape");
        sound.transform.position = ambientCentre;
        var soundscape = sound.AddComponent<ScarySoundscape>();
        soundscape.Theme = theme;
        soundscape.Listener = rig != null ? rig.Camera.transform : null;
        soundscape.AmbientArea = ambientArea;
        soundscape.AmbientHeight = ambientHeight;
        var focus = new GameObject("CrimeScene_HeartbeatFocus").transform;
        focus.SetParent(sound.transform, false);
        focus.position = crimeScene;
        soundscape.CrimeScene = focus;
        var reverb = sound.AddComponent<AudioReverbZone>();
        reverb.reverbPreset = reverbPreset;
        reverb.minDistance = Mathf.Max(ambientArea.x, ambientArea.z) * 0.6f;
        reverb.maxDistance = Mathf.Max(ambientArea.x, ambientArea.z) * 1.4f;
        return soundscape;
    }

    /// <summary>Glass, light beams and glowing parts must not block the moonlight.</summary>
    static void DisableShadowsOnSeeThrough(GameObject unused)
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (m == null) continue;
            bool seeThrough = m.renderQueue >= (int)RenderQueue.Transparent - 500 && m.renderQueue > 2000
                              || m.name.StartsWith("M_LampGlow") || m.name.StartsWith("M_Moonflower") || m.name.StartsWith("M_Beam");
            if (seeThrough)
                r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// <summary>UBX_ meshes become hidden mesh colliders.</summary>
    static void UseCollisionProxies(GameObject modelRoot)
    {
        foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.name.StartsWith("UBX_")) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            mf.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    static GameObject ExaminePoint(Transform parent, string name, Vector3 pos, float size, string title, string description,
                                   string clueId, string note)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().size = Vector3.one * size;
        var item = go.AddComponent<InspectableItem>();
        item.CanPickUp = false;
        item.ItemName = title;
        item.ItemDescription = description;
        item.ClueID = clueId;
        item.NotebookNote = note;
        return go;
    }

    static void ApplyAtmosphere(Color ambient, Color fogColor, float fogDensity, FogMode fogMode = FogMode.Exponential)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.fog = true;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    static Light DirectionalLight(Transform parent, string name, Vector3 euler, string hex, float intensity)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.rotation = Quaternion.Euler(euler);
        light.type = LightType.Directional;
        light.color = Hex(hex);
        light.intensity = intensity;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;
        return light;
    }

    static void ReflectionProbeAt(Transform parent, Vector3 centre, Vector3 size)
    {
        var probe = new GameObject("RoomReflectionProbe").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(parent, false);
        probe.transform.position = centre;
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = size;
        probe.boxProjection = true;
    }

    static Material ParticleMaterial(string name, Color color, bool additive)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Rain(Transform parent, Vector3 centre)
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(centre, Quaternion.Euler(90f, 0f, 0f)); // circle shape lies flat
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.prewarm = true;
        main.startLifetime = 1.6f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.025f);
        main.startColor = new Color(0.78f, 0.84f, 0.95f, 0.35f);
        main.maxParticles = 1800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 900f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 14f;
        shape.radiusThickness = 0.72f; // only the outer ring: no rain inside the lamp room
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-19f, -15f);
        vel.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.035f;
        r.lengthScale = 1f;
        r.sharedMaterial = ParticleMaterial("M_Rain", new Color(0.8f, 0.86f, 1f, 0.45f), false);
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void Fireflies(Transform parent, Vector3 centre, Vector3 size)
    {
        var go = new GameObject("Fireflies");
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
        main.startColor = new Color(0.85f, 1f, 0.45f, 1f);
        main.maxParticles = 70;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 7f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = size;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.2f, 0.5f),
                             new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMaterial("M_Firefly", new Color(1.6f, 2.0f, 0.7f, 1f), true);
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ==================================================================== Case 2: the lighthouse

    [MenuItem("Tools/The Spooky Crime Scene/2c. Build Lighthouse Scene Only")]
    static void BuildLighthouseMenu() => Debug.Log(BuildLighthouseScene());

    public static string BuildLighthouseScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var catalog = CaseLibrary.EnsureAssets();
        var def = catalog.Cases[1];

        var env = new GameObject("Environment");
        var room = InstantiateModel("Lighthouse_Room", env.transform, Vector3.zero, Quaternion.identity);
        UseCollisionProxies(room);
        foreach (var part in new[] { "LH_KeeperDesk", "LH_OilBarrels" })
        {
            var t = room.transform.Find(part);
            t.gameObject.AddComponent<MeshCollider>().sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
        }
        var lamp = InstantiateModel("Lighthouse_Lamp", env.transform, Vector3.zero, Quaternion.identity);
        SetStaticRecursive(room);
        // The optic turns: prisms, brass frame and beams spin about the lamp axis (their pivots are on it).
        const float lensSpeed = 18f;
        foreach (var part in new[] { "Lens_Prisms", "Lens_Frame", "Lens_Beams" })
            lamp.transform.Find(part).gameObject.AddComponent<Rotator>().DegreesPerSecond = lensSpeed;

        // ---------------------------------------------------------------- evidence
        var clues = new GameObject("Evidence").transform;
        var deskCentre = new Vector3(2.913f, 0.94f, 1.06f);

        ExaminePoint(clues, "FuelValve_Examine", new Vector3(0f, 0.5f, 0.79f), 0.32f,
            "The Lamp's Fuel Valve",
            "The valve feeding the burner has been screwed shut, hard, and the brass handwheel is smeared with black harbour " +
            "tar. A lamp doesn't run dry with its valve closed. Someone shut it on purpose, then opened it again later: the " +
            "lamp is burning now.",
            CaseLibrary.LH_FUEL_VALVE, "Valve shut on purpose. Harbour tar on the wheel.");

        var button = CreateClue("Prop_Button", clues, new Vector3(-1.547f, 0.975f, 0.238f), Quaternion.Euler(0f, 0f, 0f),
            "Torn Brass Button",
            "Caught on the stair railing: a brass button with an anchor and crown, the badge of the <b>Harbour Board</b>. It was " +
            "torn off, thread and all, by a man grabbing at the rail, or by a man being shoved against it.",
            CaseLibrary.LH_BUTTON, "Harbour Board button, torn off at the stair rail.",
            Vector3.zero, Vector3.zero, canPickUp: false, minSize: 0.26f);

        var spyPos = new Vector3(1.778f, 1.35f, -2.539f);
        CreateClue("Prop_Spyglass", clues, spyPos, Quaternion.Euler(0f, 145f, 0f),
            "Spyglass",
            "Left on its stand by the glass, still trained on the Teeth, where the Merrow struck. A keeper sweeps the whole " +
            "horizon. Whoever stood here tonight was watching for one ship.",
            CaseLibrary.LH_SPYGLASS, "Aimed at the Teeth. Someone was waiting for the Merrow.",
            new Vector3(0f, 0.1f, 0.05f), new Vector3(0f, 90f, 0f));
        Tripod(clues, spyPos + Vector3.down * 0.03f, AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/M_Wood.mat"));

        CreateClue("Prop_Matchbook", clues, new Vector3(-0.35f, 0.002f, 0.62f), Quaternion.Euler(0f, 25f, 0f),
            "Tavern Matchbook",
            "A matchbook from the <i>Anchor & Lamp</i> tavern on the harbour, and three spent matches by the lamp. Silas lit his " +
            "lamp with the flint striker on his belt. He never needed matches. Somebody else lit this lamp tonight.",
            CaseLibrary.LH_MATCHES, "Someone relit the lamp with tavern matches.",
            new Vector3(0f, 0.1f, -0.22f), new Vector3(70f, 180f, 0f));

        CreateClue("Prop_PocketWatch1149", clues, new Vector3(-1.35f, 0.0f, 0.45f), Quaternion.Euler(0f, 220f, 0f),
            "Silas Marsh's Pocket Watch",
            "Found by the stair hatch, its glass cracked. The hands stopped at <b>11:49</b>, when he fell. That's three minutes " +
            "<i>before</i> the light went out at 11:52. Silas was already dead when the lamp went dark.",
            CaseLibrary.LH_WATCH, "Stopped at 11:49 in the fall: before the light went out.",
            new Vector3(0f, 0.1f, -0.25f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_KeeperLog", clues, new Vector3(2.951f, 0.94f, 0.808f), Quaternion.Euler(0f, -20f, 0f),
            "Keeper's Logbook",
            "Silas's last entries: <i>11:30, Wick trimmed, light burning bright. Storm rising. Merrow due past the Teeth near " +
            "midnight. 11:44, Reed on the stair AGAIN. Told him no. The light stays lit while I</i>... The entry stops mid-sentence.",
            CaseLibrary.LH_LOGBOOK, "11:44: Reed on the stair again. Silas refused him.",
            new Vector3(0f, 0.12f, 0.02f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_Manifest", clues, new Vector3(2.783f, 0.941f, 1.066f), Quaternion.Euler(0f, -35f, 0f),
            "The Merrow's Cargo Manifest",
            "A shipping manifest with the harbour board's red seal: <i>Schooner Merrow. Forty bars of silver.</i> Pencilled in " +
            "the margin, in a different hand: <i>salvage, one third to the board</i>.",
            CaseLibrary.LH_MANIFEST, "The Merrow carried silver. Salvage goes to the harbour board.",
            new Vector3(0f, 0.12f, -0.1f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_ClayPipe", clues, new Vector3(2.81f, 0.94f, 1.342f), Quaternion.Euler(0f, 60f, 0f),
            "Clay Pipe",
            "Edwin Pike's clay pipe, left on the shelf. The tobacco in the bowl is unburnt and stone cold. It hasn't been " +
            "smoked since his day shift ended.",
            CaseLibrary.LH_PIPE, "Edwin's pipe: cold, untouched since the day shift.",
            new Vector3(0f, 0.1f, -0.1f), new Vector3(0f, 90f, 0f));

        // ---------------------------------------------------------------- player, HUD, case file
        var rig = BuildCaseRig(def, catalog, new Vector3(-0.4f, 1f, 2.6f), 171f, 230f, Hex("0A0F16"));

        // ---------------------------------------------------------------- lighting: storm, lamp, beams
        var lighting = new GameObject("Lighting");
        var sky = DirectionalLight(lighting.transform, "StormSky_Directional", new Vector3(40f, 30f, 0f), "6F7F99", 0.35f);
        var lampLight = PointLight(lighting.transform, "Lamp_Light", new Vector3(0f, 1.775f, 0f), "FFE2A0", 2.2f, 7f);
        lampLight.shadows = LightShadows.Soft; // the turning brass frame throws moving shadows round the room
        PointLight(lighting.transform, "Desk_Lantern", deskCentre + new Vector3(-0.35f, 0.75f, 0f), "FFB35C", 0.8f, 2.6f);
        var beamPivot = new GameObject("BeamLights");
        beamPivot.transform.SetParent(lighting.transform, false);
        beamPivot.transform.position = new Vector3(0f, 1.775f, 0f);
        beamPivot.AddComponent<Rotator>().DegreesPerSecond = lensSpeed;
        foreach (float dir in new[] { 1f, -1f })
        {
            var spot = new GameObject(dir > 0 ? "Beam_A" : "Beam_B").AddComponent<Light>();
            spot.transform.SetParent(beamPivot.transform, false);
            spot.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, dir));
            spot.type = LightType.Spot;
            spot.color = Hex("FFF0C8");
            spot.intensity = 6f;
            spot.range = 70f;
            spot.spotAngle = 14f;
            spot.innerSpotAngle = 6f;
            spot.shadows = LightShadows.None;
        }
        ReflectionProbeAt(lighting.transform, new Vector3(0f, 1.8f, 0f), new Vector3(8f, 4f, 8f));
        ApplyAtmosphere(Hex("1A222E"), Hex("10161E"), 0.02f);

        // ---------------------------------------------------------------- informants
        var wren = BuildInformant(rig.Camera.transform, new Vector3(2.561f, 0f, -0.451f));
        wren.Dialogue = rig.Dialogue;
        var finch = BuildFinch(rig.Camera.transform, new Vector3(-1.549f, 0f, -2.212f));
        finch.Dialogue = rig.Dialogue;

        // ---------------------------------------------------------------- storm
        var weather = new GameObject("Weather");
        Rain(weather.transform, new Vector3(0f, 9f, 0f));
        var soundscape = BuildSoundscape(ScarySoundscape.SoundTheme.Lighthouse, rig, new Vector3(-2.2f, 0.3f, 0f), // the stair hatch
                                         AudioReverbPreset.Room, Vector3.zero, new Vector3(7f, 1.5f, 7f), 3.6f);
        var storm = weather.AddComponent<LightningStorm>();
        storm.Sky = sky;
        storm.Soundscape = soundscape;
        storm.Camera = rig.Camera;

        DisableShadowsOnSeeThrough(env);
        EditorSceneManager.SaveScene(scene, LighthouseScenePath);
        EnsureBuildScenes();
        return $"Built {LighthouseScenePath}: evidence={clues.GetComponentsInChildren<InspectableItem>().Length}";
    }

    static void Tripod(Transform parent, Vector3 top, Material mat)
    {
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f + 0.4f;
            Vector3 foot = new Vector3(top.x + Mathf.Cos(a) * 0.3f, 0f, top.z + Mathf.Sin(a) * 0.3f);
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leg.name = "Tripod_Leg";
            leg.transform.SetParent(parent, false);
            Vector3 d = top - foot;
            leg.transform.SetPositionAndRotation((top + foot) / 2f, Quaternion.FromToRotation(Vector3.up, d.normalized));
            leg.transform.localScale = new Vector3(0.025f, d.magnitude / 2f, 0.025f);
            leg.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(leg.GetComponent<Collider>());
        }
    }

    // ==================================================================== Case 3: the greenhouse

    [MenuItem("Tools/The Spooky Crime Scene/2d. Build Greenhouse Scene Only")]
    static void BuildGreenhouseMenu() => Debug.Log(BuildGreenhouseScene());

    public static string BuildGreenhouseScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var catalog = CaseLibrary.EnsureAssets();
        var def = catalog.Cases[2];

        var env = new GameObject("Environment");
        var room = InstantiateModel("Greenhouse_Room", env.transform, Vector3.zero, Quaternion.identity);
        UseCollisionProxies(room);
        InstantiateModel("Greenhouse_Plants", env.transform, Vector3.zero, Quaternion.identity);
        SetStaticRecursive(env);

        // ---------------------------------------------------------------- evidence
        var clues = new GameObject("Evidence").transform;

        ExaminePoint(clues, "VentCrank_Examine", new Vector3(-4.2f, 1.45f, -3.88f), 0.36f,
            "Roof Vent Crank",
            "The vent crank was wound open last night, and the vent was pulled shut again from outside: there are fresh hook " +
            "scratches on the frame up top. Smeared on the handle is bright <b>violet pollen</b>. The Moonflower only sheds pollen " +
            "when it blooms, at midnight.",
            CaseLibrary.GH_VENT_CRANK, "The vent was the way out. Violet midnight pollen on the crank.");

        CreateClue("Prop_Footprints", clues, Vector3.zero, Quaternion.identity,
            "Muddy Footprints",
            "Small boot prints, a woman's size, in wet mud from Agatha's chair to the foot of the ladder. Tom wears enormous " +
            "hobnailed boots, and Percy never sets foot in the mud. Whoever made these stood by her chair, then climbed out.",
            CaseLibrary.GH_FOOTPRINTS, "A woman's small boots, from the chair to the ladder.",
            Vector3.zero, Vector3.zero, canPickUp: false);

        CreateClue("Prop_PoisonBottle", clues, new Vector3(2.6f, 0.27f, 3.0f), Quaternion.Euler(0f, 160f, 0f),
            "Nicotine Wash",
            "Hidden on the low shelf under a potting bench: <i>Nicotine Wash. POISON. For greenfly.</i> It soaks straight through " +
            "the skin; the heart races, then stops, just like heart failure. Half empty. The label reads <i>Vale & Daughters, " +
            "Nurserymen</i>.",
            CaseLibrary.GH_POISON, "Nicotine poison from Vale & Daughters, half empty.",
            new Vector3(0f, 0.1f, -0.15f), new Vector3(10f, 0f, 0f));

        CreateClue("Prop_Gloves", clues, new Vector3(-1.6f, 0.826f, -0.15f), Quaternion.Euler(0f, 20f, 0f),
            "Lady Agatha's Gloves",
            "Her monogrammed gardening gloves, left beside the Moonflower. The leather is stained yellow-brown inside and reeks " +
            "of tobacco. Somebody soaked them in nicotine wash, and she pulled them on to tend her orchid at midnight.",
            CaseLibrary.GH_GLOVES, "Her gloves were soaked in nicotine: the poison.",
            new Vector3(0f, 0.12f, -0.05f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_OrchidTag", clues, new Vector3(-1.43f, 0.99f, -0.42f), Quaternion.Euler(0f, 30f, 0f),
            "Orchid Name Tag",
            "The tag in the Moonflower's pot once read <i>Selenicereus thornei, discovered by A. Thorne</i>. The name has been " +
            "scraped off and written over in fresh ink: <i>discovered by I. Vale</i>.",
            CaseLibrary.GH_TAG, "Moonflower tag rewritten: 'discovered by I. Vale'.",
            new Vector3(0f, 0.04f, -0.2f), new Vector3(0f, 180f, 0f));

        CreateClue("Prop_VisitorBook", clues, new Vector3(6.0f, 1.08f, -1.4f), Quaternion.Euler(0f, 0f, -15f),
            "Visitor Book",
            "The last signature: <i>I. Vale, 9:40 PM, to see the Moonflower bloom.</i> No time of leaving is written beside it. " +
            "Ivy says she left at a quarter past ten, before the bloom.",
            CaseLibrary.GH_VISITORS, "Ivy Vale signed in at 9:40. Never signed out.",
            new Vector3(0f, 0.12f, 0.05f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_GardenDiary", clues, new Vector3(-6.2f, 1.125f, 1.35f), Quaternion.Euler(0f, 70f, 0f),
            "Gardener's Diary",
            "Tom Briar's work diary, left on the boiler ledge: <i>Lady A locked herself in at 10 to watch her bloom. Stoked the " +
            "boiler till 2, then to bed.</i> In the margin he has drawn his own enormous hobnailed boots, a joke about the mud.",
            CaseLibrary.GH_DIARY, "Tom stoked the boiler till 2. Huge hobnailed boots.",
            new Vector3(0f, 0.1f, -0.1f), new Vector3(90f, 180f, 0f));

        CreateClue("Prop_BettingSlip", clues, new Vector3(5.3f, 0.0f, 1.2f), Quaternion.Euler(0f, 10f, 0f),
            "Betting Slip",
            "Percy Thorne's slip from the Royal Card Club, stamped <i>12:40 AM</i> and again at <i>2:15 AM</i>. He lost, again. He " +
            "was at the tables in town when the Moonflower bloomed.",
            CaseLibrary.GH_SLIP, "Percy was at the card club past 2 AM.",
            new Vector3(0f, 0.1f, -0.22f), new Vector3(90f, 180f, 0f));

        // ---------------------------------------------------------------- player, HUD, case file
        var rig = BuildCaseRig(def, catalog, new Vector3(6.0f, 1f, 0.4f), -90f, 90f, Hex("070B0A"));

        // ---------------------------------------------------------------- lighting: moon through the glass roof
        var lighting = new GameObject("Lighting");
        DirectionalLight(lighting.transform, "Moonlight_Directional", new Vector3(52f, -35f, 0f), "9DB4D9", 1.7f);
        var lantern = PointLight(lighting.transform, "Agatha_Lantern", new Vector3(-1.1f, 1.02f, -0.25f), "FFB060", 1.4f, 4.5f);
        lantern.shadows = LightShadows.Soft;
        PointLight(lighting.transform, "Moonflower_Glow", new Vector3(-1.52f, 1.6f, -0.47f), "B57BFF", 0.9f, 2.4f);
        PointLight(lighting.transform, "Boiler_Fire", new Vector3(-5.75f, 0.5f, 2.2f), "FF6A2A", 0.8f, 2.6f);
        ReflectionProbeAt(lighting.transform, new Vector3(0f, 2f, 0f), new Vector3(15f, 5f, 9f));
        ApplyAtmosphere(Hex("1E2A26"), Hex("0C1412"), 0.03f);

        // ---------------------------------------------------------------- informants + corkboard
        var wren = BuildInformant(rig.Camera.transform, new Vector3(6.2f, 0f, -2.4f));
        wren.Dialogue = rig.Dialogue;
        var finch = BuildFinch(rig.Camera.transform, new Vector3(-5.0f, 0f, 1.0f));
        finch.Dialogue = rig.Dialogue;

        var cork = InstantiateModel("Prop_CaseBoard", env.transform, new Vector3(6.2f, 0f, 1.6f), Quaternion.LookRotation(Vector3.left));
        var corkBox = cork.AddComponent<BoxCollider>();
        corkBox.center = new Vector3(0f, 1.25f, 0f);
        corkBox.size = new Vector3(1.25f, 0.9f, 0.35f);
        cork.AddComponent<CaseBoardStation>().Board = rig.Board;
        PointLight(env.transform, "CaseBoard_Light", new Vector3(5.6f, 2.1f, 1.6f), "FFD9A0", 1.0f, 2.4f);

        // ---------------------------------------------------------------- night life
        Fireflies(env.transform, new Vector3(0f, 1.3f, 0f), new Vector3(12f, 1.6f, 6f));
        BuildSoundscape(ScarySoundscape.SoundTheme.Greenhouse, rig, new Vector3(-0.55f, 0.6f, 0.15f), // Agatha's chair
                        AudioReverbPreset.Room, Vector3.zero, new Vector3(13f, 1f, 7f), 2.8f);

        DisableShadowsOnSeeThrough(env);
        EditorSceneManager.SaveScene(scene, GreenhouseScenePath);
        EnsureBuildScenes();
        return $"Built {GreenhouseScenePath}: evidence={clues.GetComponentsInChildren<InspectableItem>().Length}";
    }

    // ==================================================================== main menu

    [MenuItem("Tools/The Spooky Crime Scene/2a. Build Main Menu Only")]
    static void BuildMenuMenu() => Debug.Log(BuildMainMenuScene());

    public static string BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var catalog = CaseLibrary.EnsureAssets();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("06080C");
        camGo.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = CreateVolumeProfile();
        RenderSettings.fog = false;

        var canvasGo = new GameObject("Menu Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // never clip in 4:3 or narrow windows
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = canvasGo.transform;

        Panel("Backdrop", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.035f, 0.04f, 0.055f, 1f));
        var title = Text("Title", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1600f, 120f),
                         "THE SPOOKY CRIME SCENE", 92f, Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 10f;
        Text("Subtitle", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(1400f, 44f),
             "Three cases. One detective. Three accusations each, and no second chances.", 26f, new Color(0.8f, 0.82f, 0.88f, 0.75f),
             TextAlignmentOptions.Center).fontStyle = FontStyles.Italic;

        var menu = canvasGo.AddComponent<MainMenu>();
        menu.Catalog = catalog;
        int n = catalog.Cases.Length;
        menu.Cards = new MainMenu.Card[n];
        const float cardW = 520f, cardH = 540f, gap = 40f;
        float startX = -(n - 1) * (cardW + gap) / 2f;
        for (int i = 0; i < n; i++)
        {
            var card = new MainMenu.Card();
            var bg = Panel($"Card{i + 1}", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startX + i * (cardW + gap), -95f),
                           new Vector2(cardW, cardH), new Color(0.16f, 0.12f, 0.09f, 0.96f));
            card.Root = bg;
            card.Background = bg.GetComponent<Image>();
            var t = bg.transform;
            card.NumberText = Text("Number", t, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(-60f, 32f),
                                   "CASE", 22f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Top);
            card.NumberText.characterSpacing = 10f;
            card.TitleText = Text("Title", t, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -100f), new Vector2(-60f, 96f),
                                  "", 36f, Gold, TextAlignmentOptions.Top);
            card.TitleText.fontStyle = FontStyles.Bold;
            card.LocationText = Text("Location", t, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -178f), new Vector2(-60f, 56f),
                                     "", 20f, new Color(0.75f, 0.8f, 0.9f, 0.85f), TextAlignmentOptions.Top);
            card.LocationText.fontStyle = FontStyles.Italic;
            Panel("Divider", t, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(360f, 2f),
                  new Color(1f, 0.78f, 0.45f, 0.4f));
            card.BlurbText = Text("Blurb", t, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -320f), new Vector2(-70f, 150f),
                                  "", 22f, Parchment, TextAlignmentOptions.Top);
            card.StatusText = Text("Status", t, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 120f), new Vector2(-60f, 30f),
                                   "", 20f, Color.white, TextAlignmentOptions.Center);
            card.PlayButton = MakeButton("PlayButton", t, new Vector2(cardW / 2f - 150f, -cardH + 92f), new Vector2(300f, 58f),
                                         "INVESTIGATE", 26f, out var label, new Color(0.55f, 0.16f, 0.1f, 1f));
            card.PlayLabel = label;
            menu.Cards[i] = card;
        }
        menu.ResetButton = MakeButton("ResetProgress", root, new Vector2(0f, 0f), new Vector2(340f, 40f), "Reset progress", 18f,
                                      out var resetLabel, new Color(0.15f, 0.13f, 0.12f, 1f));
        var resetRect = (RectTransform)menu.ResetButton.transform;
        resetRect.anchorMin = resetRect.anchorMax = new Vector2(1f, 0f);
        resetRect.pivot = new Vector2(1f, 0f);
        resetRect.anchoredPosition = new Vector2(-30f, 26f);
        menu.ResetLabel = resetLabel;
        Text("Controls", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1300f, 30f),
             "WASD Move  ·  Mouse Look  ·  E Inspect / Talk  ·  Tab Case Board  ·  Progress saves automatically",
             18f, new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Center);

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        AddUIInput(eventSystem);

        // Low wind, the odd creak and a distant bell behind the menu.
        var sound = BuildSoundscape(ScarySoundscape.SoundTheme.Clocktower, null, new Vector3(0f, -100f, 0f),
                                    AudioReverbPreset.Hallway, Vector3.zero, new Vector3(10f, 2f, 10f), 4f);
        sound.Listener = cam.transform;
        sound.WindVolume = 0.22f;

        EditorSceneManager.SaveScene(scene, MenuScenePath);
        EnsureBuildScenes();
        return $"Built {MenuScenePath}: cards={n}";
    }
}
