using UnityEngine;

/// <summary>The ordered list of cases. A case unlocks once the one before it has been solved.</summary>
[CreateAssetMenu(menuName = "Spooky Crime Scene/Case Catalog", fileName = "CaseCatalog")]
public class CaseCatalog : ScriptableObject
{
    public string MenuSceneName = "MainMenu";
    public CaseDefinition[] Cases = new CaseDefinition[0];

    public int IndexOf(CaseDefinition c) => System.Array.IndexOf(Cases, c);

    public CaseDefinition Next(CaseDefinition c)
    {
        int i = IndexOf(c);
        return i >= 0 && i + 1 < Cases.Length ? Cases[i + 1] : null;
    }

    public bool IsUnlocked(int index) =>
        index == 0 || (index > 0 && index < Cases.Length && CaseProgress.IsSolved(Cases[index - 1].CaseId));
}
