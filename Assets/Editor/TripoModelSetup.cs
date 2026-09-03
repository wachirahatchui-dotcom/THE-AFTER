using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Turns a raw Tripo export into a usable Unity asset, in one menu click.
//
// Tripo hands over an FBX plus a folder of JPEGs named <model>_basecolor,
// _normal, _metallic, _roughness, _rm. Getting that to render correctly in URP
// takes the same five fiddly steps every time:
//
//   1. tag the normal map as a normal map, or lighting goes flat and blue
//   2. pack metallic + smoothness into one texture - URP Lit reads metallic
//      from R and smoothness from A, has no roughness input at all, and JPEG
//      carries no alpha channel to put smoothness in
//   3. build a URP/Lit material from the maps
//   4. point the FBX at that material instead of its own embedded one
//   5. save a prefab so the model can be dropped into a scene
//
// Doing that by hand once is fine. Doing it for every model is how a project
// ends up with half its assets set up differently from the other half.
//
//   THE AFTER > Models > Set Up Selected Tripo Model
//   THE AFTER > Models > Export Selected Folder as .unitypackage
//
// Select the model's folder in the Project window first.
public static class TripoModelSetup
{
    // ------------------------------------------------------------------ menu
    [MenuItem("THE AFTER/Models/Set Up Selected Tripo Model")]
    static void SetUpSelected()
    {
        string folder = SelectedFolder();
        if (folder == null) return;

        string report = Setup(folder);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Set Up Tripo Model", report, "OK");
    }

    [MenuItem("THE AFTER/Models/Export Selected Folder as .unitypackage")]
    static void ExportSelected()
    {
        string folder = SelectedFolder();
        if (folder == null) return;

        string name = Path.GetFileName(folder);
        string output = name + ".unitypackage";

        AssetDatabase.ExportPackage(folder, output,
            ExportPackageOptions.Recurse | ExportPackageOptions.IncludeLibraryAssets);

        var info = new FileInfo(Path.GetFullPath(output));
        string message = "Exported " + folder + "\n\n" + Path.GetFullPath(output)
                       + "\n" + (info.Length / 1048576f).ToString("0.0") + " MB";

        Debug.Log(message);
        EditorUtility.DisplayDialog("Export Package", message, "OK");
    }

    static string SelectedFolder()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
        {
            EditorUtility.DisplayDialog("Set Up Tripo Model",
                "Select the model's folder in the Project window first.", "OK");
            return null;
        }
        return path;
    }

    // ================================================================== setup
    public static string Setup(string folder)
    {
        var log = new StringBuilder();

        string name = Path.GetFileName(folder);
        string texDir = folder + "/Textures";
        string matDir = folder + "/Materials";

        // ---- locate the FBX
        string fbx = AssetDatabase.FindAssets("t:Model", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase));

        if (fbx == null) return "No FBX found in " + folder;
        log.AppendLine("model: " + Path.GetFileName(fbx));

        Directory.CreateDirectory(Path.GetFullPath(texDir));
        Directory.CreateDirectory(Path.GetFullPath(matDir));
        AssetDatabase.Refresh();

        // ---- locate the maps by suffix
        string baseColor = FindTexture(texDir, "basecolor");
        string normal = FindTexture(texDir, "normal");
        string metallic = FindTexture(texDir, "metallic");
        string roughness = FindTexture(texDir, "roughness");
        string rm = FindTexture(texDir, "_rm");

        log.AppendLine("maps: base=" + Has(baseColor) + " normal=" + Has(normal)
                     + " metallic=" + Has(metallic) + " roughness=" + Has(roughness) + " rm=" + Has(rm));

        // ---- 1. normal map type
        if (normal != null)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(normal);
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                log.AppendLine("normal map tagged");
            }
        }

        // ---- 2. packed metallic (R) + smoothness (A)
        string packed = PackMetallicSmoothness(texDir, name, metallic, roughness, rm, log);

        // ---- 3. material
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        string matPath = matDir + "/" + name + ".mat";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            log.AppendLine("material created: " + matPath);
        }

        mat.shader = shader;
        if (baseColor != null) mat.SetTexture("_BaseMap", Load(baseColor));

        if (normal != null)
        {
            mat.SetTexture("_BumpMap", Load(normal));
            mat.EnableKeyword("_NORMALMAP");
        }

        if (packed != null)
        {
            mat.SetTexture("_MetallicGlossMap", Load(packed));
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);   // 0 = metallic alpha
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        EditorUtility.SetDirty(mat);

        // ---- 4. remap the FBX onto it
        var model = (ModelImporter)AssetImporter.GetAtPath(fbx);
        int remapped = 0;

        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbx))
        {
            if (!(sub is Material)) continue;
            model.AddRemap(new AssetImporter.SourceAssetIdentifier(sub), mat);
            remapped++;
        }

        model.SaveAndReimport();
        log.AppendLine("FBX material slots remapped: " + remapped);

        // ---- 5. prefab
        string prefabPath = folder + "/" + name + ".prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(source);
        temp.name = name;

        PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
        log.AppendLine("prefab: " + prefabPath);

        // ---- report what it costs
        int tris = 0, verts = 0;
        foreach (var mf in temp.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            verts += mf.sharedMesh.vertexCount;
            tris += mf.sharedMesh.triangles.Length / 3;
        }

        var renderers = temp.GetComponentsInChildren<Renderer>();
        var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        Object.DestroyImmediate(temp);
        AssetDatabase.SaveAssets();

        log.AppendLine();
        log.AppendLine("triangles: " + tris.ToString("N0") + "   vertices: " + verts.ToString("N0"));
        log.AppendLine("size: " + bounds.size.ToString("0.00") + " m");

        if (tris > 200000)
            log.AppendLine("WARNING: very dense for a prop - consider decimating before it ships.");

        return log.ToString();
    }

    // ---------------------------------------------------------------- packing
    // URP Lit wants metallic in R and smoothness in A. Tripo supplies metallic
    // and roughness as separate JPEGs - and JPEG has no alpha - so the combined
    // map has to be written out as a new PNG. Falls back to the packed "rm"
    // map (glTF ORM layout: G roughness, B metallic) when the separate ones are
    // missing.
    static string PackMetallicSmoothness(string texDir, string name, string metallic,
                                         string roughness, string rm, StringBuilder log)
    {
        string output = texDir + "/" + name + "_MetallicSmoothness.png";

        Texture2D mTex = null, rTex = null;
        bool useOrm = false;

        if (metallic != null && roughness != null)
        {
            mTex = LoadReadable(metallic);
            rTex = LoadReadable(roughness);
        }
        else if (rm != null)
        {
            mTex = rTex = LoadReadable(rm);
            useOrm = true;
        }
        else
        {
            log.AppendLine("no metallic/roughness maps - smoothness left at the material default");
            return null;
        }

        var mPix = mTex.GetPixels32();
        var rPix = rTex.GetPixels32();
        int count = Mathf.Min(mPix.Length, rPix.Length);

        var packed = new Color32[count];
        for (int i = 0; i < count; i++)
        {
            byte metal = useOrm ? mPix[i].b : mPix[i].r;
            byte rough = useOrm ? rPix[i].g : rPix[i].r;
            packed[i] = new Color32(metal, 0, 0, (byte)(255 - rough));
        }

        var tex = new Texture2D(mTex.width, mTex.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(packed);
        tex.Apply();
        File.WriteAllBytes(Path.GetFullPath(output), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(output);
        imp.sRGBTexture = false;                 // data, not colour
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = false;
        imp.SaveAndReimport();

        // Read/Write was only needed for the pack; leaving it on keeps a second
        // uncompressed copy of the texture in memory for the whole session.
        SetReadable(metallic, false);
        SetReadable(roughness, false);
        SetReadable(rm, false);

        log.AppendLine("packed metallic+smoothness: " + Path.GetFileName(output)
                     + (useOrm ? "  (from the rm map)" : ""));
        return output;
    }

    // ------------------------------------------------------------------ utils
    static string FindTexture(string dir, string suffix)
    {
        if (!AssetDatabase.IsValidFolder(dir)) return null;

        return AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p)
                .ToLowerInvariant().EndsWith(suffix.ToLowerInvariant()));
    }

    static Texture2D Load(string path) { return AssetDatabase.LoadAssetAtPath<Texture2D>(path); }
    static string Has(string path) { return path == null ? "-" : "yes"; }

    static Texture2D LoadReadable(string path)
    {
        SetReadable(path, true);
        return Load(path);
    }

    static void SetReadable(string path, bool readable)
    {
        if (path == null) return;

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null || imp.isReadable == readable) return;

        imp.isReadable = readable;
        imp.SaveAndReimport();
    }
}
