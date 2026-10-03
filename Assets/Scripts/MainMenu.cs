using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Case-select screen. One card per case in the catalog: locked until the previous case is solved, showing the best
/// rank once solved. Progress can be reset with a two-click confirmation.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [System.Serializable]
    public class Card
    {
        public GameObject Root;
        public Image Background;
        public TMP_Text NumberText;
        public TMP_Text TitleText;
        public TMP_Text LocationText;
        public TMP_Text BlurbText;
        public TMP_Text StatusText;
        public Button PlayButton;
        public TMP_Text PlayLabel;
    }

    public CaseCatalog Catalog;
    public Card[] Cards;
    public Button ResetButton;
    public TMP_Text ResetLabel;

    static readonly Color Unlocked = new Color(0.16f, 0.12f, 0.09f, 0.96f);
    static readonly Color Solved = new Color(0.12f, 0.16f, 0.1f, 0.96f);
    static readonly Color Locked = new Color(0.08f, 0.08f, 0.09f, 0.9f);

    bool resetArmed;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        for (int i = 0; i < Cards.Length; i++)
        {
            int index = i;
            Cards[i].PlayButton.onClick.AddListener(() => Play(index));
        }
        if (ResetButton != null)
            ResetButton.onClick.AddListener(OnReset);
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < Cards.Length; i++)
        {
            var card = Cards[i];
            bool exists = Catalog != null && i < Catalog.Cases.Length && Catalog.Cases[i] != null;
            card.Root.SetActive(exists);
            if (!exists) continue;

            var c = Catalog.Cases[i];
            bool unlocked = Catalog.IsUnlocked(i);
            bool solved = CaseProgress.IsSolved(c.CaseId);

            card.NumberText.text = $"CASE {c.Number}";
            card.TitleText.text = unlocked ? c.Title : "? ? ?";
            card.LocationText.text = unlocked ? c.Location : "";
            card.BlurbText.text = unlocked ? c.MenuBlurb : $"Solve case {c.Number - 1} to unlock.";
            card.StatusText.text = solved
                ? $"<color=#9FE08A>SOLVED</color>  ·  Best rank: {CaseProgress.RankName(CaseProgress.BestRank(c.CaseId), c)}"
                : unlocked ? "<color=#FFC774>NEW CASE</color>" : "<color=#888888>LOCKED</color>";
            card.Background.color = solved ? Solved : unlocked ? Unlocked : Locked;
            card.PlayButton.interactable = unlocked;
            card.PlayLabel.text = !unlocked ? "LOCKED" : solved ? "PLAY AGAIN" : "INVESTIGATE";
        }
        if (ResetLabel != null)
            ResetLabel.text = resetArmed ? "Click again to erase all progress" : "Reset progress";
    }

    void Play(int index)
    {
        if (Catalog == null || !Catalog.IsUnlocked(index))
            return;
        SceneManager.LoadScene(Catalog.Cases[index].SceneName);
    }

    void OnReset()
    {
        if (!resetArmed)
        {
            resetArmed = true;
            Refresh();
            return;
        }
        resetArmed = false;
        CaseProgress.ResetAll(Catalog);
        Refresh();
    }
}
