using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Replaces only the visible ProBuilder door leaves in ModularPrototype.
/// Hinges, leaf colliders, sockets and portal components stay on their existing objects.
/// Scene instances receive prefab overrides; generated room prefabs are not edited.
/// </summary>
public static class PSXDoorArtIntegration
{
    private const string ScenePath = "Assets/Scenes/ModularPrototype.unity";
    private const string ModelPath = "Assets/Props/PSXDoors/PSX Doors Pack.fbx";
    private const string TexturePath = "Assets/Props/PSXDoors/PSX Doors Texture.jpg";
    private const string MaterialPath = "Assets/Props/PSXDoors/PSXDoorWood.mat";
    private const string VisualName = "PSX_Door_Wood_Visual";
    private const string ChosenDoorName = "Door#5";

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar pack de puertas PSX")]
    private static void Inspect()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogError("No se importó el FBX de puertas PSX.");
            return;
        }

        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Debug.Log($"PSX {filter.name}: mesh={filter.sharedMesh.name}, bounds={filter.sharedMesh.bounds}, " +
                $"localPosition={filter.transform.localPosition}, localRotation={filter.transform.localRotation}");
        }

        Debug.Log($"Hojas activas en escena: {FindDoorLeaves().Length}");
    }

    [MenuItem("The Lights Before Dawn/Arte/Integrar puertas PSX en ModularPrototype")]
    private static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Salí de Play antes de integrar las puertas.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty)
        {
            Debug.LogError("Abrí ModularPrototype y guardá la escena antes de integrar las puertas PSX.");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        MeshFilter source = model != null
            ? model.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == ChosenDoorName)
            : null;
        if (source == null || source.sharedMesh == null || texture == null)
        {
            Debug.LogError("Falta el modelo Door#5 o su textura. Revisá Assets/Props/PSXDoors.");
            return;
        }

        MeshRenderer[] leaves = FindDoorLeaves();
        if (leaves.Length == 0 || leaves.Any(r => r.GetComponent<BoxCollider>() == null))
        {
            Debug.LogError("No se encontraron todas las hojas con BoxCollider; no se modificó la escena.");
            return;
        }

        string recoveryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SceneRecovery");
        Directory.CreateDirectory(recoveryDirectory);
        string recoveryPath = Path.Combine(recoveryDirectory, "ModularPrototype_BeforePSXDoors.unity");
        if (!File.Exists(recoveryPath))
            File.Copy(Path.GetFullPath(ScenePath), recoveryPath);

        Material material = GetOrCreateMaterial(texture);
        Bounds sourceBounds = source.sharedMesh.bounds;
        Quaternion sourceRotation = source.transform.rotation;
        if (sourceBounds.size.x <= 0f || sourceBounds.size.y <= 0f || sourceBounds.size.z <= 0f)
            throw new InvalidOperationException("El mesh elegido no tiene dimensiones válidas.");
        int[] targetAxes = GetTargetAxes(sourceRotation);
        int changed = 0;
        foreach (MeshRenderer oldRenderer in leaves)
        {
            Transform leaf = oldRenderer.transform;
            Transform existing = leaf.Find(VisualName);
            BoxCollider collider = leaf.GetComponent<BoxCollider>();
            Vector3 targetSize = collider.size;
            GameObject visual;
            if (existing == null)
            {
                visual = new GameObject(VisualName);
                Undo.RegisterCreatedObjectUndo(visual, "Integrar puerta PSX");
                visual.transform.SetParent(leaf, false);
            }
            else
            {
                visual = existing.gameObject;
                Undo.RecordObject(visual.transform, "Alinear puerta PSX");
            }

            Vector3 fitScale = Vector3.one;
            for (int sourceAxis = 0; sourceAxis < 3; sourceAxis++)
                fitScale[sourceAxis] = targetSize[targetAxes[sourceAxis]] / sourceBounds.size[sourceAxis];
            visual.transform.localRotation = sourceRotation;
            visual.transform.localScale = fitScale;
            visual.transform.localPosition = collider.center -
                sourceRotation * Vector3.Scale(sourceBounds.center, fitScale);

            MeshFilter filter = visual.GetComponent<MeshFilter>();
            if (filter == null) filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = source.sharedMesh;
            MeshRenderer newRenderer = visual.GetComponent<MeshRenderer>();
            if (newRenderer == null) newRenderer = visual.AddComponent<MeshRenderer>();
            newRenderer.sharedMaterial = material;
            newRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            Undo.RecordObject(oldRenderer, "Ocultar hoja ProBuilder");
            oldRenderer.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldRenderer);
            changed++;
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"Puertas PSX: {changed} hojas visuales alineadas de {leaves.Length}. " +
            $"Colliders, bisagras y conexiones conservados. Copia previa: {recoveryPath}");
    }

    private static int[] GetTargetAxes(Quaternion rotation)
    {
        int[] result = new int[3];
        bool[] used = new bool[3];
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        for (int sourceAxis = 0; sourceAxis < 3; sourceAxis++)
        {
            Vector3 direction = rotation * axes[sourceAxis];
            int targetAxis = 0;
            for (int axis = 1; axis < 3; axis++)
                if (Mathf.Abs(direction[axis]) > Mathf.Abs(direction[targetAxis])) targetAxis = axis;
            if (Mathf.Abs(direction[targetAxis]) < 0.999f || used[targetAxis])
                throw new InvalidOperationException("La orientación del FBX no está alineada a ejes.");
            result[sourceAxis] = targetAxis;
            used[targetAxis] = true;
        }
        return result;
    }

    private static MeshRenderer[] FindDoorLeaves()
    {
        return UnityEngine.Object.FindObjectsByType<DoorInteractable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(d => d.gameObject.scene == SceneManager.GetActiveScene())
            .Select(d => d.LeafCollider != null
                ? d.LeafCollider.GetComponent<MeshRenderer>() : null)
            .Where(r => r != null && r.name == "HojaPuerta")
            .Distinct()
            .ToArray();
    }

    private static Material GetOrCreateMaterial(Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("No se encontró el shader URP/Lit.");
        material = new Material(shader) { name = "PSXDoorWood" };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }
}
