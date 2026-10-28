using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Reemplaza solamente la visual rígida de las cortinas; conserva interacción y colliders.</summary>
internal static class CurtainFabricTool
{
    private const string RoomsFolder = "Assets/Generated/ModularPrototype/Rooms";
    private const string FabricFolder = "Assets/Generated/ModularPrototype/CurtainFabric";
    private const string MaterialPath = FabricFolder + "/CurtainFabric.mat";
    private const string ShadowMaterialPath = FabricFolder + "/CurtainShadow.mat";
    private const string ShadowTexturePath = FabricFolder + "/CurtainShadowMask.asset";
    private const string VisualName = "Visual_TelaCurva";
    private const string ShadowName = "Sombra_Tela";

    private static readonly HashSet<string> CurtainNames = new HashSet<string>
    {
        "CortinaIzquierda", "CortinaDerecha", "CortinaBanera", "CortinaDucha"
    };

    [MenuItem("The Lights Before Dawn/Arte/Modelar telas de cortinas")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Salir de Play antes de modelar las cortinas.");
            return;
        }
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("Guardar la escena abierta antes de cambiar los prefabs de cortinas.");
            return;
        }

        EnsureFolder();
        Material material = GetOrCreateMaterial();
        if (material == null) return;
        Material shadowMaterial = GetOrCreateShadowMaterial();
        if (shadowMaterial == null) return;

        int curtains = 0;
        int prefabs = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { RoomsFolder });
        foreach (string guid in guids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool modified = false;
            try
            {
                foreach (Transform curtain in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!CurtainNames.Contains(curtain.name)) continue;
                    // Los prefabs ya integrados no necesitan guardarse de nuevo.
                    if (curtain.Find(VisualName) != null && curtain.Find(ShadowName) != null)
                        continue;
                    BoxCollider box = curtain.GetComponent<BoxCollider>();
                    MeshRenderer original = curtain.GetComponent<MeshRenderer>();
                    if (box == null || original == null || !box.isTrigger)
                    {
                        Debug.LogWarning("Cortina sin visual/disparador esperado: " + prefabPath + "/" + curtain.name);
                        continue;
                    }

                    bool bath = curtain.name == "CortinaBanera" || curtain.name == "CortinaDucha";
                    string meshPath = FabricFolder + "/" + Path.GetFileNameWithoutExtension(prefabPath)
                        + "_" + GetHierarchyKey(curtain, root.transform) + ".asset";
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = CreateFabricMesh(box.size, bath);
                        mesh.name = Path.GetFileNameWithoutExtension(meshPath);
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }

                    Transform visual = curtain.Find(VisualName);
                    if (visual == null)
                    {
                        visual = new GameObject(VisualName).transform;
                        visual.SetParent(curtain, false);
                    }
                    float direction = RoomFacingDirection(curtain);
                    // Solo adelanta la visual unos centímetros; el disparador queda en su sitio.
                    visual.localPosition = box.center + Vector3.forward * direction * (bath ? 0.018f : 0.035f);
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = Vector3.one;
                    MeshFilter filter = visual.GetComponent<MeshFilter>();
                    if (filter == null) filter = visual.gameObject.AddComponent<MeshFilter>();
                    MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
                    if (renderer == null) renderer = visual.gameObject.AddComponent<MeshRenderer>();
                    filter.sharedMesh = mesh;
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;

                    Transform shadow = curtain.Find(ShadowName);
                    if (shadow == null)
                    {
                        shadow = new GameObject(ShadowName).transform;
                        shadow.SetParent(curtain, false);
                    }
                    shadow.localPosition = visual.localPosition;
                    shadow.localRotation = Quaternion.identity;
                    shadow.localScale = Vector3.one;
                    MeshFilter shadowFilter = shadow.GetComponent<MeshFilter>();
                    if (shadowFilter == null) shadowFilter = shadow.gameObject.AddComponent<MeshFilter>();
                    MeshRenderer shadowRenderer = shadow.GetComponent<MeshRenderer>();
                    if (shadowRenderer == null) shadowRenderer = shadow.gameObject.AddComponent<MeshRenderer>();
                    shadowFilter.sharedMesh = mesh;
                    shadowRenderer.sharedMaterial = shadowMaterial;
                    shadowRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    shadowRenderer.receiveShadows = false;
                    original.enabled = false;
                    modified = true;
                    curtains++;
                }
                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    prefabs++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Telas curvas: {curtains} cortinas en {prefabs} prefabs. Colisiones y puntos de escondite conservados.");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(FabricFolder))
            AssetDatabase.CreateFolder("Assets/Generated/ModularPrototype", "CurtainFabric");
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("No se encontró el shader URP/Lit para las cortinas.");
            return null;
        }
        material = new Material(shader) { name = "CurtainFabric" };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_Cull", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
        material.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        material.SetColor("_BaseColor", new Color(0.84f, 0.81f, 0.76f, 0.72f));
        material.SetFloat("_Smoothness", 0.12f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static Material GetOrCreateShadowMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(ShadowTexturePath);
        if (mask == null)
        {
            // Patrón ordenado: deja pasar aproximadamente la mitad de la luz.
            int[] bayer = { 0, 48, 12, 60, 32, 16, 44, 28,
                            24, 8, 36, 20, 56, 40, 52, 4,
                            6, 54, 2, 50, 38, 22, 34, 18,
                            30, 14, 42, 26, 62, 46, 58, 10,
                            33, 17, 45, 29, 1, 49, 13, 61,
                            57, 41, 53, 5, 25, 9, 37, 21,
                            15, 63, 3, 51, 47, 31, 43, 27,
                            39, 23, 35, 19, 55, 7, 59, 11 };
            mask = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
            {
                name = "CurtainShadowMask", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color32[128 * 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
                pixels[y * 128 + x] = new Color32(255, 255, 255,
                    bayer[(y % 8) * 8 + x % 8] < 32 ? (byte)255 : (byte)0);
            mask.SetPixels32(pixels);
            mask.Apply(false, false);
            AssetDatabase.CreateAsset(mask, ShadowTexturePath);
        }
        material = new Material(shader) { name = "CurtainShadow" };
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetTexture("_BaseMap", mask);
        material.SetColor("_BaseColor", Color.white);
        material.EnableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        AssetDatabase.CreateAsset(material, ShadowMaterialPath);
        return material;
    }

    private static float RoomFacingDirection(Transform curtain)
    {
        HideSpot spot = curtain.GetComponentInParent<HideSpot>();
        if (spot != null && spot.EntryPoint != null && spot.HiddenPoint != null)
        {
            float entry = curtain.InverseTransformPoint(spot.EntryPoint.position).z;
            float hidden = curtain.InverseTransformPoint(spot.HiddenPoint.position).z;
            if (Mathf.Abs(entry - hidden) > 0.02f) return Mathf.Sign(entry - hidden);
        }
        return 1f;
    }

    private static string GetHierarchyKey(Transform item, Transform root)
    {
        var names = new List<string>();
        for (Transform current = item; current != null && current != root; current = current.parent)
            names.Add(current.name);
        names.Reverse();
        return string.Join("_", names).Replace(' ', '_');
    }

    private static Mesh CreateFabricMesh(Vector3 size, bool bath)
    {
        const int columns = 32;
        const int rows = 16;
        var vertices = new Vector3[(columns + 1) * (rows + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[columns * rows * 6];
        float width = size.x;
        float height = size.y;
        float folds = bath ? 5f : Mathf.Max(3f, Mathf.Round(width / 0.16f));
        float depth = bath ? 0.055f : 0.045f;
        for (int y = 0; y <= rows; y++)
        {
            float v = y / (float)rows;
            for (int x = 0; x <= columns; x++)
            {
                float u = x / (float)columns;
                float wave = Mathf.Sin(u * folds * Mathf.PI * 2f);
                float gather = Mathf.Lerp(0.40f, 1f, v);
                float sideDrift = Mathf.Sin(v * Mathf.PI) * Mathf.Sin(u * Mathf.PI * 2f) * 0.012f;
                float hem = v * 0.012f * Mathf.Sin(u * Mathf.PI * 5f);
                int index = y * (columns + 1) + x;
                vertices[index] = new Vector3((u - 0.5f) * width + sideDrift,
                    (0.5f - v) * height + hem, wave * depth * gather);
                uv[index] = new Vector2(u, 1f - v);
            }
        }
        int triangle = 0;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            int a = y * (columns + 1) + x;
            int b = a + 1;
            int c = a + columns + 1;
            int d = c + 1;
            triangles[triangle++] = a;
            triangles[triangle++] = c;
            triangles[triangle++] = b;
            triangles[triangle++] = b;
            triangles[triangle++] = c;
            triangles[triangle++] = d;
        }
        var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
