using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the supplied static Poly Haven props without changing room modules, doors, or navigation.
/// Existing stove greybox colliders remain as the collision proxy for its replacement mesh.
/// </summary>
public static class PolyHavenPropIntegration
{
    private const string ScenePath = "Assets/Scenes/ModularPrototype.unity";
    private const string PropRoot = "Assets/Props/PolyHaven";
    private const string MaterialFolder = PropRoot + "/Materials";
    private const string MarkerName = "PolyHavenProps_Integrated";
    private static readonly string[] BookSuffixes = { "02", "06", "07", "08", "13", "15", "23", "24", "29", "34", "35", "36" };

    [MenuItem("The Lights Before Dawn/Arte/Integrar libros y electrodomesticos Poly Haven")]
    public static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Salí de Play antes de integrar la utilería.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Abrí ModularPrototype antes de ejecutar esta opción.");
        if (FindInScene(scene, MarkerName) != null)
            throw new InvalidOperationException("La utilería ya fue integrada. No se crearán duplicados.");

        GameObject library = Require(scene, "Habitacion_Biblioteca");
        GameObject childRoom = Require(scene, "Habitacion_Nino");
        GameObject office = Require(scene, "Habitacion_Oficina");
        GameObject kitchen = Require(scene, "Habitacion_Cocina");
        GameObject stoveGreybox = RequireChild(kitchen, "CocinaYHorno");
        GameObject northCounter = RequireChild(kitchen, "Mesada_Norte");
        GameObject counterTop = RequireChild(northCounter, "Superior_Superficie");
        GameObject childDesktop = RequireChild(childRoom, "SuperficieEscritorio");
        GameObject officeDesktop = RequireChild(office, "SuperficieEscritorio");

        GameObject stoveAsset = RequireModel("electric_stove_2k");
        GameObject microwaveAsset = RequireModel("vintage_microwave_2k");
        GameObject laptopAsset = RequireModel("classic_laptop_2k");
        GameObject encyclopediaAsset = RequireModel("book_encyclopedia_set_01_2k");
        GameObject decorativeAsset = RequireModel("decorative_book_set_01_2k");
        List<GameObject> cases = library.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("Biblioteca_", StringComparison.Ordinal))
            .Select(t => t.gameObject).ToList();
        if (cases.Count != 5) throw new InvalidOperationException("Se esperaban cinco estanterías en la biblioteca; encontradas: " + cases.Count);
        foreach (GameObject bookcase in cases)
        {
            int surfaces = bookcase.GetComponentsInChildren<BoxCollider>(true)
                .Count(c => c.name.StartsWith("Estante_Superficie_", StringComparison.Ordinal));
            if (surfaces < 4) throw new InvalidOperationException("Faltan repisas en " + bookcase.name);
        }

        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar ModularPrototype antes del respaldo.");
        string backupDir = Path.Combine(Path.GetTempPath(), "TLBDPropIntegration", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDir);
        File.Copy(scene.path, Path.Combine(backupDir, "ModularPrototype.unity"));
        if (File.Exists(scene.path + ".meta")) File.Copy(scene.path + ".meta", Path.Combine(backupDir, "ModularPrototype.unity.meta"));

        Dictionary<string, Material> mats = BuildMaterials();
        var report = new StringBuilder();
        report.AppendLine("Respaldo: " + backupDir);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Integrar utilería Poly Haven");
        try
        {
            PlaceStove(scene, stoveGreybox, stoveAsset, mats, report);
            PlaceCounterProp(scene, counterTop, microwaveAsset, "Microondas_PolyHaven", mats, report);
            PlaceLaptop(scene, childRoom, childDesktop, laptopAsset, "Laptop_Nino_PolyHaven", mats, report);
            PlaceLaptop(scene, office, officeDesktop, laptopAsset, "Laptop_Oficina_PolyHaven", mats, report);
            foreach (GameObject bookcase in cases)
                FillBookcase(scene, bookcase, encyclopediaAsset, decorativeAsset, mats, report);

            GameObject marker = new GameObject(MarkerName);
            Undo.RegisterCreatedObjectUndo(marker, "Marcar integración");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar la escena integrada.");
            string reportPath = Path.Combine(backupDir, "placement-report.txt");
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log("Integración Poly Haven completada. Informe: " + reportPath + "\n" + report);
            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    private static void PlaceStove(Scene scene, GameObject greybox, GameObject asset, Dictionary<string, Material> mats, StringBuilder report)
    {
        Renderer[] oldRenderers = greybox.GetComponentsInChildren<Renderer>(true);
        Bounds oldBounds = WorldBounds(oldRenderers);
        GameObject model = InstantiateModel(scene, asset, greybox.transform, "Cocina_PolyHaven");
        Vector3 towardRoom = greybox.transform.parent.position - oldBounds.center;
        towardRoom.y = 0;
        if (towardRoom.sqrMagnitude < .001f) towardRoom = -greybox.transform.forward;
        // Poly Haven's stove is modelled with the door at local -Z.
        model.transform.rotation = Quaternion.LookRotation(-towardRoom.normalized, Vector3.up);
        FitToDimensions(model, Mathf.Min(.66f, oldBounds.size.x * .95f), Mathf.Min(.76f, oldBounds.size.z * .95f), Mathf.Min(.91f, oldBounds.size.y * .98f));
        Bounds placed = WorldBounds(model.GetComponentsInChildren<Renderer>(true));
        model.transform.position += new Vector3(oldBounds.center.x - placed.center.x, oldBounds.min.y - placed.min.y, oldBounds.center.z - placed.center.z);
        ApplyMaterials(model, mats);
        foreach (Renderer renderer in oldRenderers)
        {
            Undo.RecordObject(renderer, "Ocultar cocina ProBuilder");
            renderer.enabled = false; // Preserve the old BoxColliders and navigation footprint.
        }
        report.AppendLine("Cocina: " + BoundsText(WorldBounds(model.GetComponentsInChildren<Renderer>(true))) + "; colisiones greybox conservadas.");
    }

    private static void PlaceCounterProp(Scene scene, GameObject supportObject, GameObject asset, string name, Dictionary<string, Material> mats, StringBuilder report)
    {
        BoxCollider support = supportObject.GetComponent<BoxCollider>();
        if (support == null) throw new InvalidOperationException("La encimera no tiene BoxCollider: " + supportObject.name);
        GameObject model = InstantiateModel(scene, asset, supportObject.transform, name);
        model.transform.rotation = supportObject.transform.rotation;
        FitToDimensions(model, Mathf.Min(.63f, support.size.x * .38f), Mathf.Min(.43f, support.size.z * .76f), .36f, support.transform);
        PlaceOnSupport(model, support, -.28f, 0f);
        ApplyMaterials(model, mats);
        AddSimpleCollider(model);
        ValidateOnSupport(model, support, .035f);
        report.AppendLine("Microondas sobre " + supportObject.transform.parent.name + ": " + BoundsText(WorldBounds(model.GetComponentsInChildren<Renderer>(true))));
    }

    private static void PlaceLaptop(Scene scene, GameObject room, GameObject desktop, GameObject asset, string name, Dictionary<string, Material> mats, StringBuilder report)
    {
        BoxCollider support = desktop.GetComponent<BoxCollider>();
        if (support == null) throw new InvalidOperationException("El escritorio no tiene BoxCollider en " + room.name);
        GameObject model = InstantiateModel(scene, asset, room.transform, name);
        Transform chair = room.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.IndexOf("Silla", StringComparison.OrdinalIgnoreCase) >= 0);
        Vector3 towardChair = chair != null ? chair.position - desktop.transform.position : -desktop.transform.forward;
        towardChair.y = 0;
        if (towardChair.sqrMagnitude < .001f) towardChair = -desktop.transform.forward;
        model.transform.rotation = Quaternion.LookRotation(-towardChair.normalized, Vector3.up);
        FitToDimensions(model, Mathf.Min(.60f, support.size.x * .70f), Mathf.Min(.44f, support.size.z * .72f), .51f, support.transform);
        PlaceOnSupport(model, support, 0f, 0f);
        ApplyMaterials(model, mats);
        AddSimpleCollider(model);
        ValidateOnSupport(model, support, .02f);
        report.AppendLine(name + " sobre " + room.name + ": " + BoundsText(WorldBounds(model.GetComponentsInChildren<Renderer>(true))));
    }

    private static void FillBookcase(Scene scene, GameObject bookcase, GameObject encyclopedia, GameObject decorative, Dictionary<string, Material> mats, StringBuilder report)
    {
        BoxCollider[] shelves = bookcase.GetComponentsInChildren<BoxCollider>(true)
            .Where(c => c.name.StartsWith("Estante_Superficie_", StringComparison.Ordinal))
            .OrderBy(c => c.transform.position.y).ToArray();
        // The highest slab is the top cap; nothing should protrude above it.
        for (int i = 0; i < Math.Min(3, shelves.Length - 1); i++)
        {
            BoxCollider shelf = shelves[i];
            float clearance = shelves[i + 1].bounds.min.y - shelf.bounds.max.y - .03f;
            if (clearance < .18f) throw new InvalidOperationException("Sin espacio vertical suficiente en " + bookcase.name + "/" + shelf.name);
            GameObject source = i == 1 ? encyclopedia : decorative;
            string name = i == 1 ? "Enciclopedias_PolyHaven" : "LibrosDecorativos_PolyHaven";
            GameObject model = InstantiateModel(scene, source, shelf.transform, name + "_" + i);
            if (i != 1) KeepSelectedDecorativeBooks(model);
            FitToDimensions(model, shelf.size.x * .48f, shelf.size.z * .73f, Mathf.Min(.30f, clearance), shelf.transform);
            PlaceOnSupport(model, shelf, i == 0 ? -.36f : i == 2 ? .36f : 0f, 0f);
            ApplyMaterials(model, mats);
            ValidateOnSupport(model, shelf, .07f);
            if (WorldBounds(model.GetComponentsInChildren<Renderer>(true)).max.y > shelves[i + 1].bounds.min.y - .01f)
                throw new InvalidOperationException("Los libros atraviesan la repisa superior en " + bookcase.name);
            report.AppendLine(bookcase.name + "/" + shelf.name + ": " + name + " " + BoundsText(WorldBounds(model.GetComponentsInChildren<Renderer>(true))));
        }
    }

    private static void KeepSelectedDecorativeBooks(GameObject root)
    {
        var chosen = new HashSet<string>(BookSuffixes.Select(s => "book_hardcover_01_cover" + s));
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer renderer in renderers)
        {
            if (!chosen.Contains(renderer.name)) Undo.DestroyObjectImmediate(renderer.gameObject);
        }
        int remaining = root.GetComponentsInChildren<MeshRenderer>(true).Length;
        if (remaining != chosen.Count) throw new InvalidOperationException("No se encontraron todos los libros decorativos seleccionados: " + remaining);
    }

    private static Dictionary<string, Material> BuildMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(PropRoot, "Materials");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("No se encontró el shader URP/Lit.");
        var result = new Dictionary<string, Material>();
        AddMaterial(result, shader, "electric_stove", "electric_stove_2k/electric_stove_diff_2k.jpg", Color.white, .48f);
        AddMaterial(result, shader, "electric_stove_glass", null, new Color(.045f, .055f, .065f), .82f);
        AddMaterial(result, shader, "vintage_microwave", "vintage_microwave_2k/vintage_microwave_diff_2k.jpg", Color.white, .42f);
        AddMaterial(result, shader, "vintage_microwave_glass", null, new Color(.055f, .07f, .08f), .78f);
        AddMaterial(result, shader, "classic_laptop", "classic_laptop_2k/classic_laptop_diff_2k.jpg", Color.white, .55f);
        AddMaterial(result, shader, "classic_laptop_screen", null, new Color(.018f, .024f, .03f), .75f);
        // The supplied encyclopedia ZIP omits all texture images: use readable leather/paper colours for now.
        AddMaterial(result, shader, "book_encyclopedia_set_01_cover", null, new Color(.23f, .105f, .07f), .28f);
        AddMaterial(result, shader, "book_encyclopedia_set_01_paper", null, new Color(.75f, .68f, .51f), .13f);
        foreach (string suffix in BookSuffixes)
        {
            string name = "book_hardcover_01_cover" + suffix;
            AddMaterial(result, shader, name, "decorative_book_set_01_2k/" + name + ".png", Color.white, .22f);
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static void AddMaterial(Dictionary<string, Material> result, Shader shader, string name, string relativeTexture, Color color, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        if (relativeTexture != null)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PropRoot + "/" + relativeTexture);
            if (texture == null) throw new FileNotFoundException("Falta una textura importada: " + relativeTexture);
            material.SetTexture("_BaseMap", texture);
        }
        EditorUtility.SetDirty(material);
        result[name] = material;
    }

    private static void ApplyMaterials(GameObject model, Dictionary<string, Material> materials)
    {
        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] current = renderer.sharedMaterials;
            for (int i = 0; i < current.Length; i++)
            {
                string name = current[i] != null ? current[i].name.Replace(" (Instance)", "") : renderer.name;
                if (!materials.TryGetValue(name, out Material replacement))
                    throw new InvalidOperationException("Material de FBX sin correspondencia: " + name + " en " + renderer.name);
                current[i] = replacement;
            }
            Undo.RecordObject(renderer, "Asignar materiales URP");
            renderer.sharedMaterials = current;
        }
    }

    private static GameObject InstantiateModel(Scene scene, GameObject asset, Transform parent, string name)
    {
        GameObject model = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
        if (model == null) throw new InvalidOperationException("No se pudo instanciar " + asset.name);
        Undo.RegisterCreatedObjectUndo(model, "Añadir " + name);
        if (PrefabUtility.IsAnyPrefabInstanceRoot(model))
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Undo.SetTransformParent(model.transform, parent, "Ubicar " + name);
        model.name = name;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        return model;
    }

    private static void FitToDimensions(GameObject model, float maxWidth, float maxDepth, float maxHeight, Transform measurementSpace = null)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = measurementSpace != null ? BoundsInSpace(renderers, measurementSpace) : WorldBounds(renderers);
        if (bounds.size.x <= .001f || bounds.size.y <= .001f || bounds.size.z <= .001f)
            throw new InvalidOperationException("El FBX no tiene dimensiones válidas: " + model.name);
        float factor = Mathf.Min(maxWidth / bounds.size.x, maxDepth / bounds.size.z, maxHeight / bounds.size.y);
        // FBX files can import in centimetres (0.01 unit scale), so a large local
        // multiplier is normal. The final visible bounds are still limited above.
        if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0 || factor > 500)
            throw new InvalidOperationException("Escala anómala para " + model.name + ": " + factor);
        model.transform.localScale *= factor;
    }

    private static void PlaceOnSupport(GameObject model, BoxCollider support, float xFraction, float zFraction)
    {
        Bounds bounds = BoundsInSpace(model.GetComponentsInChildren<Renderer>(true), support.transform);
        Vector3 desired = new Vector3(
            support.center.x + xFraction * support.size.x * .5f,
            support.center.y + support.size.y * .5f + .003f,
            support.center.z + zFraction * support.size.z * .5f);
        Vector3 delta = new Vector3(desired.x - bounds.center.x, desired.y - bounds.min.y, desired.z - bounds.center.z);
        model.transform.position += support.transform.TransformVector(delta);
    }

    private static void ValidateOnSupport(GameObject model, BoxCollider support, float margin)
    {
        Bounds b = BoundsInSpace(model.GetComponentsInChildren<Renderer>(true), support.transform);
        Vector3 min = support.center - support.size * .5f + new Vector3(margin, 0, margin);
        Vector3 max = support.center + support.size * .5f - new Vector3(margin, 0, margin);
        if (b.min.x < min.x - .005f || b.max.x > max.x + .005f || b.min.z < min.z - .005f || b.max.z > max.z + .005f)
            throw new InvalidOperationException(model.name + " sobresale de " + support.name + ": " + BoundsText(b));
        float top = support.center.y + support.size.y * .5f;
        if (Mathf.Abs(b.min.y - top) > .025f)
            throw new InvalidOperationException(model.name + " no descansa sobre " + support.name);
    }

    private static void AddSimpleCollider(GameObject model)
    {
        Bounds b = BoundsInSpace(model.GetComponentsInChildren<Renderer>(true), model.transform);
        BoxCollider collider = Undo.AddComponent<BoxCollider>(model);
        collider.center = b.center;
        collider.size = b.size;
    }

    private static Bounds WorldBounds(Renderer[] renderers)
    {
        if (renderers.Length == 0) throw new InvalidOperationException("El modelo no tiene renderers.");
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static Bounds BoundsInSpace(Renderer[] renderers, Transform space)
    {
        bool started = false;
        Bounds result = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Bounds b = renderer.bounds;
            foreach (float x in new[] { b.min.x, b.max.x })
            foreach (float y in new[] { b.min.y, b.max.y })
            foreach (float z in new[] { b.min.z, b.max.z })
            {
                Vector3 point = space.InverseTransformPoint(new Vector3(x, y, z));
                if (!started) { result = new Bounds(point, Vector3.zero); started = true; }
                else result.Encapsulate(point);
            }
        }
        if (!started) throw new InvalidOperationException("El modelo no tiene renderers.");
        return result;
    }

    private static string BoundsText(Bounds b) => "centro=" + b.center.ToString("F2") + " tamaño=" + b.size.ToString("F2");

    private static GameObject RequireModel(string name)
    {
        string path = PropRoot + "/" + name + "/" + name + ".fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new FileNotFoundException("Unity aún no importó el FBX: " + path);
        return asset;
    }

    private static GameObject Require(Scene scene, string name) => FindInScene(scene, name) ?? throw new InvalidOperationException("Falta " + name + " en la escena.");

    private static GameObject RequireChild(GameObject parent, string name)
    {
        Transform child = parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        return child != null ? child.gameObject : throw new InvalidOperationException("Falta " + name + " bajo " + parent.name);
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            if (candidate.name == name) return candidate.gameObject;
        return null;
    }
}
