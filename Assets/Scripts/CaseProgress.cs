using UnityEngine;

/// <summary>
/// Saved progress per case (PlayerPrefs: the registry on desktop, IndexedDB in the browser).
/// Ranks are stored as indices into <see cref="RankNames"/>, lower is better.
/// </summary>
public static class CaseProgress
{
    public const int TopRank = 0;
    static readonly string[] RankNames = { null, "Chief Inspector", "Detective Sergeant", "Lucky Constable" };

    static string SolvedKey(string caseId) => $"case.{caseId}.solved";
    static string RankKey(string caseId) => $"case.{caseId}.bestRank";

    public static bool IsSolved(string caseId) => PlayerPrefs.GetInt(SolvedKey(caseId), 0) == 1;

    /// <summary>Best rank index achieved, or -1 if never solved.</summary>
    public static int BestRank(string caseId) => IsSolved(caseId) ? PlayerPrefs.GetInt(RankKey(caseId), RankNames.Length - 1) : -1;

    public static string RankName(int rankIndex, CaseDefinition c)
    {
        if (rankIndex == TopRank) return c != null ? c.TopRankName : "Master Detective";
        return rankIndex > 0 && rankIndex < RankNames.Length ? RankNames[rankIndex] : "";
    }

    public static void RecordSolved(string caseId, int rankIndex)
    {
        int best = BestRank(caseId);
        PlayerPrefs.SetInt(SolvedKey(caseId), 1);
        if (best < 0 || rankIndex < best)
            PlayerPrefs.SetInt(RankKey(caseId), rankIndex);
        PlayerPrefs.Save();
    }

    public static void ResetAll(CaseCatalog catalog)
    {
        if (catalog == null) return;
        foreach (var c in catalog.Cases)
        {
            if (c == null) continue;
            PlayerPrefs.DeleteKey(SolvedKey(c.CaseId));
            PlayerPrefs.DeleteKey(RankKey(c.CaseId));
        }
        PlayerPrefs.Save();
    }

    public static void UnlockAll(CaseCatalog catalog)
    {
        if (catalog == null) return;
        for (int i = 0; i + 1 < catalog.Cases.Length; i++)
            if (!IsSolved(catalog.Cases[i].CaseId))
                RecordSolved(catalog.Cases[i].CaseId, RankNames.Length - 1);
    }
}
