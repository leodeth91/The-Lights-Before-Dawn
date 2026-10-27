using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Inspecciona el GLB y las aberturas existentes antes de integrar la visual.</summary>
internal static class WindowAssetPlacement
{
    private const string AssetPath = "Assets/Props/WindowOkno/Window_окно.glb";
    private const string RoomPath = "Assets/Generated/ModularPrototype/Rooms";
    private const string GlassPath = "Assets/Materials/Objects/WindowGlassNight.mat";
    private const string VisualName = "Visual_WindowOkno";

    [MenuItem("The Lights Before Dawn/Arte/Probar ventana en cuarto infantil")]
    private static void Prototype() => Install(false);

    [MenuItem("The Lights Before Dawn/Arte/Integrar ventanas en habitaciones")]
    private static void InstallAll() => Install(true);

    private static void Install(bool allRooms)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Salir de Play antes de modificar prefabs.");
            return;
        }
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
        if (source == null) { Debug.LogError("No se pudo importar " + AssetPath); return; }
        Material glass = GetOrCreateGlass();
        if (glass == null) return;

        var report = new StringBuilder();
        int placed = 0;
        foreach (string file in Directory.GetFiles(RoomPath, "*.prefab"))
        {
            string path = file.Replace('\\', '/');
            if (!allRooms && !path.EndsWith("Room_child_room.prefab")) continue;
            GameObject room = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                foreach (Transform window in room.GetComponentsInChildren<Transform>(true))
                {
                    if (window.name != "Ventana" || window.Find(VisualName) != null) continue;
                    if (!AddVisual(room, window, source, glass, report)) continue;
                    changed = true;
                    placed++;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(room, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }
        report.Insert(0, "Instancias agregadas=" + placed + "\n");
        File.WriteAllText("Temp/WindowAssetPlacementResult.txt", report.ToString());
        Debug.Log("Ventanas integradas=" + placed + ". Ver Temp/WindowAssetPlacementResult.txt.");
    }

    private static bool AddVisual(GameObject room, Transform window, GameObject source,
        Material glass, StringBuilder report)
    {
        Renderer backing = window.GetComponent<Renderer>();
        if (backing == null) { report.AppendLine("Sin respaldo: " + room.name + "/" + window.parent.name); return false; }
        Bounds target = GetBounds(window, new[] { backing });
        Bounds sourceBounds = GetBounds(source.transform,
            source.GetComponentsInChildren<Renderer>(true));
        if (target.size.x < 0.1f || target.size.y < 0.1f
            || sourceBounds.size.y < 0.1f || sourceBounds.size.z < 0.1f)
        {
            report.AppendLine("Límites inválidos: " + room.name + "/" + window.parent.name);
            return false;
        }

        GameObject visual = new GameObject(VisualName);
        EditorSceneManager.MoveGameObjectToScene(visual, room.scene);
        visual.transform.SetParent(window, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, room.scene);
        if (instance == null) { Object.DestroyImmediate(visual); return false; }
        instance.transform.SetParent(visual.transform, false);
        // En el GLB: Y es ancho, Z es altura y X es grosor.
        instance.transform.localRotation = Quaternion.AngleAxis(-120f, Vector3.one.normalized);
        float scale = Mathf.Min(target.size.x / sourceBounds.size.y,
            target.size.y / sourceBounds.size.z);
        instance.transform.localScale = Vector3.one * scale;
        instance.transform.localPosition = Vector3.zero;
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        Bounds current = GetBounds(window, renderers);
        // El modelo queda justo delante del panel nocturno existente y detrás de la cortina.
        instance.transform.localPosition = target.center - current.center
            + new Vector3(0f, 0f, -0.07f);

        int glassRenderers = 0;
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool isGlass = false;
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] == null || !materials[index].name.StartsWith("IN_WINDOW")) continue;
                materials[index] = glass;
                isGlass = true;
            }
            if (!isGlass) continue;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            glassRenderers++;
        }
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        Bounds placed = GetBounds(window, renderers);
        report.AppendLine(room.name + "/" + window.parent.name
            + " target=" + Describe(target) + " asset=" + Describe(placed)
            + " cristales=" + glassRenderers);
        return true;
    }

    private static Material GetOrCreateGlass()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { Debug.LogError("No se encontró URP/Lit."); return null; }
        var glass = new Material(shader) { name = "WindowGlassNight" };
        glass.SetColor("_BaseColor", new Color(0.18f, 0.29f, 0.42f, 0.38f));
        glass.SetColor("_Color", new Color(0.18f, 0.29f, 0.42f, 0.38f));
        glass.SetFloat("_Metallic", 0f);
        glass.SetFloat("_Smoothness", 0.82f);
        glass.SetFloat("_Surface", 1f);
        glass.SetFloat("_Blend", 0f);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0f);
        glass.SetFloat("_Cull", 0f);
        glass.SetOverrideTag("RenderType", "Transparent");
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(glass, GlassPath);
        return glass;
    }

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar ventanas importadas")]
    private static void Inspect()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
        if (source == null) { Debug.LogError("No se pudo importar " + AssetPath); return; }
        var report = new StringBuilder();
        report.AppendLine("GLB: " + Describe(GetBounds(source.transform,
            source.GetComponentsInChildren<Renderer>(true))));
        foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            report.AppendLine("  " + renderer.name + " material="
                + (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null")
                + " shader=" + (renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.shader.name : "null")
                + " bounds=" + Describe(GetBounds(source.transform, new[] { renderer })));

        int count = 0;
        foreach (string path in Directory.GetFiles(RoomPath, "*.prefab"))
        {
            GameObject room = PrefabUtility.LoadPrefabContents(path.Replace('\\', '/'));
            if (room == null) continue;
            try
            {
                foreach (Transform item in room.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name != "Ventana") continue;
                    Renderer renderer = item.GetComponent<Renderer>();
                    report.AppendLine(room.name + "/" + item.parent.name + "/" + item.name
                        + " local=" + item.localPosition.ToString("F3")
                        + " bounds=" + (renderer != null
                            ? Describe(GetBounds(item, new[] { renderer })) : "sin visual")
                        + " light=" + (item.Find("LuzSuaveVentana") != null));
                    count++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }
        report.Insert(0, "Ventanas=" + count + "\n");
        File.WriteAllText("Temp/WindowAssetAnalysis.txt", report.ToString());
        Debug.Log("Ventanas inspeccionadas=" + count + ". Ver Temp/WindowAssetAnalysis.txt.");
    }

    private static Bounds GetBounds(Transform reference, Renderer[] renderers)
    {
        Bounds combined = default;
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            Bounds local = renderer.localBounds;
            Matrix4x4 toReference = reference.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = toReference.MultiplyPoint3x4(local.center
                    + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                if (!initialized) { combined = new Bounds(point, Vector3.zero); initialized = true; }
                else combined.Encapsulate(point);
            }
        }
        return combined;
    }

    private static string Describe(Bounds bounds)
        => "min=" + bounds.min.ToString("F3") + " max=" + bounds.max.ToString("F3")
            + " size=" + bounds.size.ToString("F3");
}
