using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tooling for "The Spooky Crime Scene": configures the Blender FBX imports and URP materials.
/// Re-runnable from Tools > The Spooky Crime Scene.
/// </summary>
public static partial class ClocktowerSceneBuilder
{
    const string ModelRoot = "Assets/Models";
    static readonly string[] ModelDirs = { "Assets/Models/Clocktower", "Assets/Models/Lighthouse", "Assets/Models/Greenhouse" };
    const string MaterialDir = "Assets/Materials/Clocktower";

    enum MatKind { Lit, Transparent, Unlit, Additive, Emissive }

    struct MatSpec
    {
        public string Hex; public float Metallic; public float Smoothness; public float Alpha;
        public MatKind Kind; public float Glow;
        public MatSpec(string hex, float metallic, float smoothness, float alpha = 1f, MatKind kind = MatKind.Lit, float glow = 0f)
        {
            Hex = hex; Metallic = metallic; Smoothness = smoothness; Alpha = alpha; Glow = glow;
            Kind = alpha < 1f && kind == MatKind.Lit ? MatKind.Transparent : kind;
        }
    }

    // Material names match the Blender material slots so FBX imports can be remapped by name.
    // Metals stay below full metallic: the night-time rooms give reflections little to show, so albedo must carry the colour.
    static readonly Dictionary<string, MatSpec> Materials = new Dictionary<string, MatSpec>
    {
        // Case 1: clocktower (and shared)
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
        // Case 2: lighthouse
        { "M_WhitePaint",  new MatSpec("D9D4C8", 0f,    0.35f) },
        { "M_DeckPlate",   new MatSpec("3A3D40", 0.6f,  0.40f) },
        { "M_StormGlass",  new MatSpec("A9C0D2", 0f,    0.95f, 0.10f) },
        { "M_Copper",      new MatSpec("4E7F70", 0.5f,  0.40f) },
        { "M_LensGlass",   new MatSpec("D6F2FF", 0f,    1.00f, 0.30f) },
        { "M_Beam",        new MatSpec("FFF1C8", 0f,    0f,    0.05f, MatKind.Additive) },
        { "M_LampGlow",    new MatSpec("FFE6A8", 0f,    0f,    1f,    MatKind.Unlit, 2.2f) },
        { "M_Sea",         new MatSpec("0B1A22", 0f,    0.85f) },
        { "M_Rock",        new MatSpec("26282B", 0f,    0.15f) },
        { "M_RedPaint",    new MatSpec("8E2620", 0f,    0.35f) },
        { "M_Oilskin",     new MatSpec("C9A227", 0f,    0.65f) },
        { "M_ClayPipe",    new MatSpec("E9E2D0", 0f,    0.25f) },
        { "M_BookBlue",    new MatSpec("1E2F5A", 0f,    0.40f) },
        // Case 3: greenhouse
        { "M_Brick",       new MatSpec("7A3B2A", 0f,    0.15f) },
        { "M_GlassPane",   new MatSpec("C9E6D8", 0f,    0.95f, 0.10f) },
        { "M_FrameGreen",  new MatSpec("2F4A3A", 0.4f,  0.45f) },
        { "M_Gravel",      new MatSpec("6E6A62", 0f,    0.10f) },
        { "M_Soil",        new MatSpec("2E2016", 0f,    0.05f) },
        { "M_Terracotta",  new MatSpec("A8573A", 0f,    0.20f) },
        { "M_Leaf",        new MatSpec("3E6B34", 0f,    0.40f) },
        { "M_LeafDark",    new MatSpec("244A26", 0f,    0.35f) },
        { "M_PlantStem",   new MatSpec("4A6A2E", 0f,    0.30f) },
        { "M_Moonflower",  new MatSpec("B57BFF", 0f,    0.50f, 1f,    MatKind.Emissive, 1.6f) },
        { "M_Glove",       new MatSpec("8A6A44", 0f,    0.30f) },
        { "M_BrownGlass",  new MatSpec("4A2A12", 0f,    0.90f, 0.85f) },
        { "M_Mud",         new MatSpec("2A1D12", 0f,    0.55f) },
        { "M_BookGreen",   new MatSpec("1F4A2E", 0f,    0.40f) },
    };

    [MenuItem("Tools/The Spooky Crime Scene/1. Configure Materials + Model Imports")]
    public static string ConfigureImports()
    {
        EnsureFolder(MaterialDir);
        var mats = new Dictionary<string, Material>();
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

        foreach (var kv in Materials)
        {
            string path = $"{MaterialDir}/{kv.Key}.mat";
            var spec = kv.Value;
            bool isUnlit = spec.Kind == MatKind.Unlit || spec.Kind == MatKind.Additive;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(isUnlit ? unlit : lit) { name = kv.Key };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = isUnlit ? unlit : lit;
            ColorUtility.TryParseHtmlString("#" + spec.Hex, out Color c);
            c.a = spec.Alpha;
            if (spec.Kind == MatKind.Unlit && spec.Glow > 0f)
                c = new Color(c.r * spec.Glow, c.g * spec.Glow, c.b * spec.Glow, 1f); // HDR colour so it blooms
            m.SetColor("_BaseColor", c);
            if (!isUnlit)
            {
                m.SetFloat("_Metallic", spec.Metallic);
                m.SetFloat("_Smoothness", spec.Smoothness);
            }

            switch (spec.Kind)
            {
                case MatKind.Transparent: MakeTransparent(m, doubleSided: true); break;
                case MatKind.Additive: MakeAdditive(m); break;
                default: MakeOpaque(m); break;
            }
            if (spec.Kind == MatKind.Emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * spec.Glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            mats[kv.Key] = m;
        }
        AssetDatabase.SaveAssets();

        int configured = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                continue;
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
        m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = -1;
    }

    static void MakeTransparent(Material m, bool doubleSided)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", (float)(doubleSided ? UnityEngine.Rendering.CullMode.Off : UnityEngine.Rendering.CullMode.Back));
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    /// <summary>Additive, double-sided, no depth write: fake volumetric light beams.</summary>
    static void MakeAdditive(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f); // Additive
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    /// <summary>Finds an FBX by name in any of the model folders.</summary>
    static string ModelPath(string model)
    {
        foreach (var dir in ModelDirs)
        {
            string p = $"{dir}/{model}.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                return p;
        }
        throw new FileNotFoundException($"Model '{model}' not found in {string.Join(", ", ModelDirs)}");
    }
}
