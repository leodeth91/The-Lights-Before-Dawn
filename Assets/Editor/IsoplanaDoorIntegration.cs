using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Replaces door art in the scene while keeping the functional hinge, leaf collider and sockets.</summary>
public static class IsoplanaDoorIntegration
{
    private const string ScenePath = "Assets/Scenes/ModularPrototype.unity";
    private const string InteriorPath = "Assets/Props/PuertasIsoplanas/isoplana_hoja_01.glb";
    private const string ExitPath = "Assets/Props/PuertaDeMadera/puerta_de_madera_hoja.glb";
    private const string VisualName = "NewDoorVisual";

    [MenuItem("The Lights Before Dawn/Arte/Integrar puertas isoplanas y salida de madera")]
    private static void Integrate()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
        {
            Debug.LogError("Salí de Play y abrí ModularPrototype guardada antes de integrar las puertas.");
            return;
        }

        GameObject interior = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorPath);
        GameObject exterior = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPath);
        if (interior == null || exterior == null)
        {
            Debug.LogError("Falta importar una de las dos visuales de puerta.");
            return;
        }

        DoorSocket[] sockets = UnityEngine.Object.FindObjectsByType<DoorSocket>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(s => s.gameObject.scene == scene && s.Door != null).ToArray();
        DoorInteractable[] doors = sockets.Select(s => s.Door).Distinct().ToArray();
        if (doors.Length != 27 || sockets.Count(s => s.SocketId == "foyer_south") != 1)
        {
            Debug.LogError($"Se esperaban 27 puertas y una salida foyer_south; encontradas {doors.Length}.");
            return;
        }

        foreach (DoorInteractable door in doors)
            if (door.LeafCollider == null || door.LeafCollider.GetComponent<MeshRenderer>() == null
                || door.LeafCollider.transform.Find(VisualName) != null)
            {
                Debug.LogError($"La hoja {door.name} no tiene collider/renderizador válido o ya fue integrada.");
                return;
            }

        int exitCount = 0;
        foreach (DoorInteractable door in doors)
        {
            bool isExit = sockets.Any(s => s.Door == door && s.SocketId == "foyer_south");
            if (isExit) exitCount++;
            Transform leaf = door.LeafCollider.transform;
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(isExit ? exterior : interior, leaf);
            model.name = VisualName;
            Undo.RegisterCreatedObjectUndo(model, "Reemplazar visual de puerta");

            Transform visual = model.transform;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            Bounds source = MeasureLocalBounds(leaf, model);
            BoxCollider collider = (BoxCollider)door.LeafCollider;
            float width = collider.size.x / source.size.x;
            float height = collider.size.y / source.size.y;
            // Both chosen models put the handle toward their negative X edge.
            // Existing hinges sit at negative X, so mirror the art, never the collider or hinge.
            visual.localScale = new Vector3(-width, height, Mathf.Min(width, height));
            visual.localPosition = collider.center - new Vector3(
                -source.center.x * width, source.center.y * height,
                source.center.z * Mathf.Min(width, height));

            foreach (Transform child in leaf)
                if (child != visual && child.name == "PSX_Door_Wood_Visual")
                {
                    Undo.RecordObject(child.gameObject, "Ocultar visual anterior");
                    child.gameObject.SetActive(false);
                }
            MeshRenderer oldRenderer = leaf.GetComponent<MeshRenderer>();
            Undo.RecordObject(oldRenderer, "Ocultar hoja ProBuilder");
            oldRenderer.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldRenderer);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"DOOR_ART PASS: {doors.Length - exitCount} puertas isoplanas, {exitCount} salida de madera; " +
            "bisagras, colliders y sockets conservados.");
    }

    [MenuItem("The Lights Before Dawn/Arte/Corregir visual de salida de madera")]
    private static void FixExit()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
        {
            Debug.LogError("Abrí ModularPrototype fuera de Play.");
            return;
        }
        DoorSocket socket = UnityEngine.Object.FindObjectsByType<DoorSocket>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(s => s.gameObject.scene == scene && s.SocketId == "foyer_south");
        Transform leaf = socket.Door.LeafCollider.transform;
        Transform old = leaf.Find(VisualName);
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPath);
        if (old == null || source == null)
        {
            Debug.LogError("Falta la visual anterior o la hoja de madera preparada.");
            return;
        }

        Undo.RecordObject(old.gameObject, "Ocultar madera con origen incorrecto");
        old.name = "OldWoodDoorVisual";
        old.gameObject.SetActive(false);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, leaf);
        Undo.RegisterCreatedObjectUndo(model, "Corregir visual de salida");
        model.name = VisualName;
        Transform visual = model.transform;
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        Bounds bounds = MeasureLocalBounds(leaf, model);
        BoxCollider collider = (BoxCollider)socket.Door.LeafCollider;
        float width = collider.size.x / bounds.size.x;
        float height = collider.size.y / bounds.size.y;
        visual.localScale = new Vector3(-width, height, Mathf.Min(width, height));
        visual.localPosition = collider.center - new Vector3(
            -bounds.center.x * width, bounds.center.y * height,
            bounds.center.z * Mathf.Min(width, height));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("DOOR_EXIT PASS: visual de madera normalizada y alineada con la bisagra existente.");
    }

    [MenuItem("The Lights Before Dawn/Arte/Auditar puertas nuevas")]
    private static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;

        DoorSocket[] sockets = UnityEngine.Object.FindObjectsByType<DoorSocket>(
            FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(s => s.gameObject.scene == scene && s.Door != null).ToArray();
        DoorInteractable[] doors = sockets.Select(s => s.Door).Distinct().ToArray();
        int failures = 0;
        foreach (DoorInteractable door in doors)
        {
            BoxCollider collider = door.LeafCollider as BoxCollider;
            Transform visual = collider != null ? collider.transform.Find(VisualName) : null;
            if (visual == null || !visual.gameObject.activeSelf)
            {
                failures++;
                continue;
            }

            Bounds bounds = MeasureLocalBounds(collider.transform, visual.gameObject);
            if (Vector3.Distance(bounds.center, collider.center) > 0.02f
                || Mathf.Abs(bounds.size.x - collider.size.x) > 0.02f
                || Mathf.Abs(bounds.size.y - collider.size.y) > 0.02f)
            {
                Debug.LogError($"DOOR_AUDIT: desalineación en {door.name}: {bounds} frente a {collider.bounds}");
                failures++;
            }
        }
        Debug.Log($"DOOR_AUDIT: {doors.Length} hojas, {failures} desalineadas o ausentes.");
    }

    private static Bounds MeasureLocalBounds(Transform leaf, GameObject model)
    {
        bool hasBounds = false;
        Bounds result = new Bounds();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 center = meshBounds.center;
            Vector3 extent = meshBounds.extents;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extent, new Vector3(x, y, z));
                        Vector3 point = leaf.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        if (!hasBounds) { result = new Bounds(point, Vector3.zero); hasBounds = true; }
                        else result.Encapsulate(point);
                    }
        }
        if (!hasBounds || result.size.x <= 0f || result.size.y <= 0f)
            throw new InvalidOperationException("La visual importada no tiene geometría válida.");
        return result;
    }
}
