using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Conversation box: typewriter-revealed lines with procedural "voice" blips and up to four reply choices
/// (click, or press 1-4). Also shows short floating subtitles for ambient lines.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public struct Choice
    {
        public string Label;
        public Action OnPick;
        public Choice(string label, Action onPick) { Label = label; OnPick = onPick; }
    }

    [Header("References")]
    public FirstPersonController Player;
    public PlayerInput PlayerInput;

    [Header("UI")]
    public GameObject Panel;
    public TMP_Text SpeakerText;
    public TMP_Text LineText;
    public Button[] ChoiceButtons;
    public TMP_Text[] ChoiceLabels;
    public TMP_Text SubtitleText;

    [Header("Voice")]
    public float CharactersPerSecond = 45f;
    [Tooltip("Base pitch of the speaking blips. Lower sounds more secretive.")]
    public float VoicePitch = 0.8f;
    [Range(0f, 1f)] public float VoiceVolume = 0.25f;

    public bool IsOpen => Panel != null && Panel.activeSelf;

    Choice[] choices = Array.Empty<Choice>();
    float revealed;
    int totalChars;
    int openedFrame;
    float subtitleUntil;
    InputAction interactAction;
    InputAction cancelAction;
    Action onCancel;
    AudioSource voice;
    AudioClip blip;
    int lastBlipChar;
    bool choicesShown;

    void Awake()
    {
        if (Panel != null) Panel.SetActive(false);
        if (SubtitleText != null) SubtitleText.gameObject.SetActive(false);

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        blip = CreateBlip();

        for (int i = 0; i < ChoiceButtons.Length; i++)
        {
            int index = i;
            ChoiceButtons[i].onClick.AddListener(() => Pick(index));
        }
    }

    void Start()
    {
        interactAction = PlayerInput.actions.FindAction("Interact", true);
        cancelAction = PlayerInput.actions.FindAction("Cancel", true);
    }

    /// <summary>Opens the box (locks the player, frees the cursor). <paramref name="cancel"/> runs on Esc.</summary>
    public void Open(Action cancel)
    {
        onCancel = cancel;
        openedFrame = Time.frameCount;
        Panel.SetActive(true);
        if (SubtitleText != null) SubtitleText.gameObject.SetActive(false);
        Player.SetInputActive(false);
        Player.SetCursorLocked(false);
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        Panel.SetActive(false);
        Player.SetCursorLocked(true);
        Player.SetInputActive(true);
    }

    /// <summary>Shows a line; choices appear once the text has finished typing.</summary>
    public void Say(string speaker, string line, params Choice[] replies)
    {
        SpeakerText.text = speaker;
        LineText.text = line;
        LineText.ForceMeshUpdate();
        totalChars = LineText.textInfo.characterCount;
        revealed = 0f;
        lastBlipChar = -1;
        LineText.maxVisibleCharacters = 0;
        choices = replies ?? Array.Empty<Choice>();
        SetChoicesVisible(false);
        choicesShown = false;
    }

    /// <summary>A floating line that does not interrupt play.</summary>
    public void Subtitle(string speaker, string line, float seconds)
    {
        if (SubtitleText == null || IsOpen)
            return;
        SubtitleText.text = $"<color=#FFC774>{speaker}:</color> {line}";
        SubtitleText.gameObject.SetActive(true);
        subtitleUntil = Time.time + seconds;
        for (int i = 0; i < 6; i++)
            voice.PlayOneShot(blip, VoiceVolume * 0.6f);
    }

    void Update()
    {
        if (SubtitleText != null && SubtitleText.gameObject.activeSelf && Time.time > subtitleUntil)
            SubtitleText.gameObject.SetActive(false);

        if (!IsOpen || interactAction == null)
            return;

        bool typing = revealed < totalChars;
        if (typing)
        {
            revealed += CharactersPerSecond * Time.deltaTime;
            int shown = Mathf.Min(totalChars, Mathf.FloorToInt(revealed));
            LineText.maxVisibleCharacters = shown;
            if (shown / 3 != lastBlipChar / 3 && shown < totalChars)
            {
                lastBlipChar = shown;
                voice.pitch = VoicePitch * UnityEngine.Random.Range(0.88f, 1.12f);
                voice.PlayOneShot(blip, VoiceVolume);
            }
            if (Time.frameCount > openedFrame && interactAction.WasPressedThisFrame())
                revealed = totalChars; // skip the typing
            return;
        }

        if (!choicesShown)
        {
            // Typing finished (naturally, skipped, or an empty line): show the whole line and the replies.
            LineText.maxVisibleCharacters = totalChars;
            SetChoicesVisible(true);
            choicesShown = true;
        }

        if (Time.frameCount <= openedFrame)
            return;

        if (cancelAction.WasPressedThisFrame())
        {
            onCancel?.Invoke();
            return;
        }

        var kb = Keyboard.current;
        if (kb == null)
            return;
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        for (int i = 0; i < keys.Length && i < choices.Length; i++)
            if (kb[keys[i]].wasPressedThisFrame)
            {
                Pick(i);
                return;
            }
    }

    void Pick(int index)
    {
        if (index < 0 || index >= choices.Length || revealed < totalChars)
            return;
        choices[index].OnPick?.Invoke();
    }

    void SetChoicesVisible(bool visible)
    {
        for (int i = 0; i < ChoiceButtons.Length; i++)
        {
            bool show = visible && i < choices.Length;
            ChoiceButtons[i].gameObject.SetActive(show);
            if (show) ChoiceLabels[i].text = $"<color=#FFC774>{i + 1}.</color>  {choices[i].Label}";
        }
    }

    static AudioClip CreateBlip()
    {
        const int rate = 22050;
        int samples = rate / 22; // ~45 ms
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 55f) * Mathf.Min(1f, i / 40f);
            // Soft square-ish murmur: fundamental plus a little third harmonic.
            data[i] = env * (Mathf.Sin(2f * Mathf.PI * 190f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 570f * t)) * 0.6f;
        }
        var clip = AudioClip.Create("VoiceBlip", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
