using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Coloca la visual importada dentro del vanity del baño superior sin tocar su física.</summary>
internal static class WashBasinStandPlacement
{
    private const string AssetPath = "Assets/Props/wash_basin_stand/wash_basin_stand.glb";
    private const string AnalysisPath = "Temp/WashBasinStandAnalysis.txt";
    private const string VisualName = "Visual_WashBasinStand";

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar lavabo importado")]
    private static void Inspect()
    {
        if (!TryGetObjects(out Transform vanity, out GameObject asset)) return;
        Bounds oldBounds = GetBounds(vanity, vanity.GetComponentsInChildren<Renderer>(true));
        Bounds importedBounds = GetBounds(asset.transform,
            asset.GetComponentsInChildren<Renderer>(true));
        var report = new StringBuilder();
        report.AppendLine("VanitoryDoble local: " + Describe(oldBounds));
        report.AppendLine("wash_basin_stand local: " + Describe(importedBounds));
        foreach (Renderer renderer in asset.GetComponentsInChildren<Renderer>(true))
            report.AppendLine("  " + renderer.name + ": "
                + Describe(GetBounds(asset.transform, new[] { renderer })));
        File.WriteAllText(AnalysisPath, report.ToString());
        Debug.Log(report.ToString());
    }

    [MenuItem("The Lights Before Dawn/Arte/Integrar lavabo doble en baño superior")]
    private static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Salir de Play antes de cambiar la escena.");
            return;
        }
        if (!TryGetObjects(out Transform vanity, out GameObject asset)) return;
        if (vanity.Find(VisualName) != null)
        {
            Debug.LogWarning("La visual importada ya está dentro de VanitoryDoble.");
            return;
        }

        Renderer[] oldRenderers = vanity.GetComponentsInChildren<Renderer>(true);
        Bounds oldBounds = GetBounds(vanity, oldRenderers);
        Bounds sourceBounds = GetBounds(asset.transform,
            asset.GetComponentsInChildren<Renderer>(true));
        if (oldBounds.size.x <= 0f || sourceBounds.size.x <= 0f
            || sourceBounds.size.y <= 0f || sourceBounds.size.z <= 0f)
        {
            Debug.LogError("Las mallas del lavabo no tienen límites válidos.");
            return;
        }

        GameObject visual = new GameObject(VisualName);
        Undo.RegisterCreatedObjectUndo(visual, "Integrar lavabo doble");
        visual.transform.SetParent(vanity, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        // El GLB está en centímetros y trae el origen a miles de unidades de la malla.
        // Su eje vertical es Z; al girarlo -90° en X, Z pasa a ser la altura en Unity.
        float halfWidth = oldBounds.size.x * 0.5f;
        float height = oldBounds.size.y - 0.04f;
        Vector3 sourceScale = new Vector3(
            halfWidth / sourceBounds.size.x,
            oldBounds.size.z / sourceBounds.size.y,
            height / sourceBounds.size.z);
        for (int index = 0; index < 2; index++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset,
                vanity.gameObject.scene);
            if (instance == null)
            {
                Debug.LogError("No se pudo instanciar " + AssetPath);
                return;
            }
            Undo.RegisterCreatedObjectUndo(instance, "Integrar lavabo doble");
            instance.name = index == 0 ? "Lavabo_Izquierdo" : "Lavabo_Derecho";
            instance.transform.SetParent(visual.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            instance.transform.localScale = sourceScale;

            Bounds currentBounds = GetBounds(visual.transform,
                instance.GetComponentsInChildren<Renderer>(true));
            Vector3 desiredCenter = new Vector3(
                oldBounds.center.x + (index == 0 ? -0.25f : 0.25f) * oldBounds.size.x,
                oldBounds.min.y + height * 0.5f, oldBounds.center.z);
            instance.transform.localPosition += desiredCenter - currentBounds.center;
        }

        Undo.RecordObjects(oldRenderers, "Ocultar visual gris de VanitoryDoble");
        foreach (Renderer renderer in oldRenderers)
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
        EditorSceneManager.MarkSceneDirty(vanity.gameObject.scene);
        Selection.activeGameObject = visual;
        Bounds result = GetBounds(vanity, visual.GetComponentsInChildren<Renderer>(true));
        string report = "Original: " + Describe(oldBounds) + "\nNueva visual: "
            + Describe(result) + "\nColliders originales conservados="
            + vanity.GetComponentsInChildren<Collider>(true).Length;
        File.WriteAllText("Temp/WashBasinStandPlacementResult.txt", report);
        Debug.Log(report, visual);
    }

    private static bool TryGetObjects(out Transform vanity, out GameObject asset)
    {
        vanity = Selection.activeTransform;
        if (vanity == null || vanity.name != "VanitoryDoble"
            || !vanity.gameObject.scene.IsValid()
            || vanity.GetComponentInParent<RoomModule>()?.RoomId != "upper_bathroom")
        {
            Debug.LogError("Selecciona VanitoryDoble dentro de Habitacion_BanoSuperior.");
            asset = null;
            return false;
        }
        asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
        if (asset != null) return true;
        Debug.LogError("No se pudo cargar " + AssetPath);
        return false;
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
            + " center=" + bounds.center.ToString("F3")
            + " size=" + bounds.size.ToString("F3");
}
