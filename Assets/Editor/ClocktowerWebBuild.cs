using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds "The Spooky Crime Scene" for the web (WebGL, Brotli) into Builds/WebGL and drops in the Vercel
/// config (Deploy/vercel.json) that serves the compressed files with the right headers.
/// </summary>
public static class ClocktowerWebBuild
{
    const string OutputDir = "Builds/WebGL";
    const string VercelConfig = "Deploy/vercel.json";

    [MenuItem("Tools/The Spooky Crime Scene/3. Build WebGL (for Vercel)")]
    static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        string applied = ApplyWebSettings();
        if (applied.StartsWith("FAILED"))
            return applied;

        var options = new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path),
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
            return $"FAILED: {summary.result}, {summary.totalErrors} error(s). See the Console.";

        CopyVercelConfig();
        return $"OK: {OutputDir} ({summary.totalSize / (1024f * 1024f):F1} MB) in {summary.totalTime.TotalMinutes:F1} min";
    }

    /// <summary>Player settings for the web build: Brotli files (served by vercel.json), 1280x720, desktop quality.</summary>
    public static string ApplyWebSettings()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            return "FAILED: Web Build Support module is not installed for this Editor.";

        PlayerSettings.productName = "The Spooky Crime Scene";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = false; // Vercel serves Content-Encoding: br (see vercel.json)
        PlayerSettings.WebGL.dataCaching = true;
        // Hash-named files change URL on every build, so a browser can never mix a cached old .wasm/.data with a
        // new build, and vercel.json can cache them forever.
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        // With this off, the Input System discards every mouse/keyboard event whenever the page is not reported
        // as focused, so menu buttons can ignore clicks entirely. Keep the game listening.
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.template = "PROJECT:SpookyCrimeScene"; // full-window page with a themed loader
        UseDesktopQualityOnWeb();
        AssetDatabase.SaveAssets();
        return "settings applied";
    }

    /// <summary>Puts the Vercel header config next to the built index.html.</summary>
    public static string CopyVercelConfig()
    {
        File.Copy(VercelConfig, Path.Combine(OutputDir, "vercel.json"), true);
        return "vercel.json copied";
    }

    /// <summary>
    /// The URP template defaults WebGL to the "Mobile" quality level. Point it at "PC" so the web build matches
    /// the lighting, shadows and SSAO tuned in the Editor.
    /// </summary>
    static void UseDesktopQualityOnWeb()
    {
        int pc = System.Array.IndexOf(QualitySettings.names, "PC");
        if (pc < 0)
            return;
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
        if (asset.Length == 0)
            return;
        var so = new SerializedObject(asset[0]);
        var map = so.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; map != null && i < map.arraySize; i++)
        {
            var entry = map.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == "WebGL")
                entry.FindPropertyRelative("second").intValue = pc;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
