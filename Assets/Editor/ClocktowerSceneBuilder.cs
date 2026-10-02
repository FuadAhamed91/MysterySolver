using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tooling for "The 3:15 Escapement": configures the Blender FBX imports and URP materials.
/// Re-runnable from Tools > The 3:15 Escapement.
/// </summary>
public static partial class ClocktowerSceneBuilder
{
    const string ModelDir = "Assets/Models/Clocktower";
    const string MaterialDir = "Assets/Materials/Clocktower";

    static readonly string[] ModelNames =
    {
        "Clocktower_Room", "Gears_Pendulum", "Prop_DraftingDesk",
        "Prop_PocketWatch", "Prop_DeskLamp", "Prop_ApothecaryVial",
        "Prop_Teacup", "Prop_Logbook", "Prop_Letter", "Prop_Prescription", "Prop_Chisel",
        "NPC_Informant", "NPC_Finch", "Prop_CaseBoard",
    };

    struct MatSpec
    {
        public string Hex; public float Metallic; public float Smoothness; public float Alpha;
        public MatSpec(string hex, float metallic, float smoothness, float alpha = 1f)
        { Hex = hex; Metallic = metallic; Smoothness = smoothness; Alpha = alpha; }
    }

    // Material names match the Blender material slots so FBX imports can be remapped by name.
    // Metals stay below full metallic: the night-time room gives reflections little to show, so albedo must carry the colour.
    static readonly Dictionary<string, MatSpec> Materials = new Dictionary<string, MatSpec>
    {
        { "M_Stone",       new MatSpec("8E8780", 0f,    0.15f) },
        { "M_StoneTrim",   new MatSpec("6E6964", 0f,    0.20f) },
        { "M_FloorBoards", new MatSpec("6B4A30", 0f,    0.30f) },
        { "M_Timber",      new MatSpec("5A3E28", 0f,    0.25f) },
        { "M_Iron",        new MatSpec("4A4A4F", 0.5f,  0.45f) },
        { "M_Brass",       new MatSpec("E0BC7A", 0.6f,  0.65f) },
        { "M_DarkBrass",   new MatSpec("A88250", 0.6f,  0.55f) },
        { "M_Wood",        new MatSpec("8E5A33", 0f,    0.50f) },
        { "M_Paper",       new MatSpec("E8DDC2", 0f,    0.10f) },
        { "M_Ink",         new MatSpec("141418", 0f,    0.80f) },
        { "M_Enamel",      new MatSpec("F2EEE4", 0f,    0.75f) },
        { "M_Glass",       new MatSpec("9CC4AE", 0f,    0.95f, 0.35f) },
        { "M_Cork",        new MatSpec("B08A5E", 0f,    0.10f) },
        { "M_Proxy",       new MatSpec("FF00FF", 0f,    0f) },
        { "M_Leather",     new MatSpec("6B231C", 0f,    0.45f) },
        { "M_Wax",         new MatSpec("B3191A", 0f,    0.55f) },
        { "M_Rust",        new MatSpec("8A4A22", 0.3f,  0.15f) },
        { "M_Tea",         new MatSpec("3B1E0C", 0f,    0.92f) },
        { "M_Coat",        new MatSpec("6E6048", 0f,    0.25f) },
        { "M_Felt",        new MatSpec("26262A", 0f,    0.20f) },
        { "M_Skin",        new MatSpec("C8987A", 0f,    0.35f) },
        { "M_Scarf",       new MatSpec("7A1420", 0f,    0.15f) },
        { "M_Shirt",       new MatSpec("C9C1B0", 0f,    0.10f) },
        { "M_Vest",        new MatSpec("6B4A2C", 0f,    0.30f) },
        { "M_CapGreen",    new MatSpec("2C4A33", 0f,    0.10f) },
        { "M_Candle",      new MatSpec("EFE3C2", 0f,    0.35f) },
    };

    [MenuItem("Tools/The 3:15 Escapement/1. Configure Materials + Model Imports")]
    public static string ConfigureImports()
    {
        EnsureFolder(MaterialDir);
        var mats = new Dictionary<string, Material>();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        foreach (var kv in Materials)
        {
            string path = $"{MaterialDir}/{kv.Key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit) { name = kv.Key };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;
            ColorUtility.TryParseHtmlString("#" + kv.Value.Hex, out Color c);
            c.a = kv.Value.Alpha;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", kv.Value.Metallic);
            m.SetFloat("_Smoothness", kv.Value.Smoothness);
            if (kv.Value.Alpha < 1f) MakeTransparent(m); else MakeOpaque(m);
            EditorUtility.SetDirty(m);
            mats[kv.Key] = m;
        }
        AssetDatabase.SaveAssets();

        int configured = 0;
        foreach (string model in ModelNames)
        {
            string path = $"{ModelDir}/{model}.fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogError($"[Clocktower] Missing model {path}");
                continue;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            importer.SaveAndReimport();
            configured++;
        }
        return $"materials={mats.Count} models={configured}";
    }

    static void MakeOpaque(Material m)
    {
        m.SetFloat("_Surface", 0f);
        m.SetOverrideTag("RenderType", "Opaque");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = -1;
    }

    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
