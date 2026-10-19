using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Aplica colores simples a la casa sin mover muebles ni alterar colliders o rutas.</summary>
[InitializeOnLoad]
public static class HouseColorSetup
{
    private const string ScenePath = "Assets/Scenes/ModularPrototype.unity";
    private const string RoomFolder = "Assets/Generated/ModularPrototype/Rooms/";
    private const string MaterialFolder = "Assets/Generated/ModularPrototype/Materials";
    private const string Request = "Temp/ApplyHouseColors.request";
    private const string Result = "Temp/HouseColorResult.txt";
    private static readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();
    private static int painted, lights;

    static HouseColorSetup() => EditorApplication.update += RunIfRequested;
    private static void RunIfRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Apply(); }
        catch (Exception error) { File.WriteAllText(Result, "ERROR\n" + error); Debug.LogException(error); }
    }

    [MenuItem("The Lights Before Dawn/Casa/Colorear habitaciones desde los planos")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Salí de Play antes de aplicar los colores.");
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Abrí ModularPrototype antes de aplicar los colores.");
        // Si el usuario movió algo en el Editor, primero guardamos esa ubicación y hacemos una copia.
        if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            throw new IOException("No se pudo guardar la distribución actual de la escena.");
        string backup = "Backups/HouseColors-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/ModularPrototype.unity");
        File.Copy(ScenePath + ".meta", backup + "/ModularPrototype.unity.meta");
        foreach (string path in Directory.GetFiles(RoomFolder, "*.prefab"))
        {
            File.Copy(path, backup + "/" + Path.GetFileName(path));
            File.Copy(path + ".meta", backup + "/" + Path.GetFileName(path) + ".meta");
        }
        painted = lights = 0;
        BuildPalette();
        foreach (string path in Directory.GetFiles(RoomFolder, "*.prefab"))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string roomId = Path.GetFileNameWithoutExtension(path).Substring("Room_".Length);
                PaintRoom(root, roomId, true);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // Los muebles añadidos a mano pertenecen a instancias de habitación y viven en la escena.
        foreach (RoomModule room in Object.FindObjectsByType<RoomModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(renderer) != null) continue;
                Paint(renderer, room.RoomId, true);
            }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar la escena coloreada.");
        File.WriteAllText(Result, "OK\nBackup=" + backup + "\nColoredRenderers=" + painted
            + "\nWindowLights=" + lights + "\nRooms=11\n");
        Debug.Log("Colores de la casa aplicados. Informe: " + Result);
    }

    private static void BuildPalette()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Generated/ModularPrototype", "Materials");
        // Colores aproximados de las vistas 3D. Materiales compartidos facilitan cambios posteriores.
        Add("FloorOak", "#BDA473"); Add("FloorWalnut", "#685647"); Add("FloorGrey", "#A69D9A");
        Add("WallWarmBrown", "#725449"); Add("WallBrown", "#76584D");
        Add("WallUpperTan", "#9E7D69"); Add("WallCharcoal", "#3A3E48");
        Add("WallChildBlue", "#252F46"); Add("WallBathroomGrey", "#737D80");
        Add("WallBathroomBlueGrey", "#5E7076"); Add("CeilingBrown", "#625047");
        Add("DoorWhite", "#E4E1D9"); Add("WindowFrame", "#D8DDE0");
        Add("CurtainIvory", "#D8D1CA"); Add("MetalDark", "#494B50");
        Add("WoodMedium", "#80634C"); Add("WoodDark", "#574538");
        Add("WoodPale", "#B8966B"); Add("FabricCream", "#D8D0C4");
        Add("FabricTan", "#A58163"); Add("FabricBlue", "#41618A");
        Add("ChildBlue", "#357DAF"); Add("UpholsteryBrown", "#79604E");
        Add("CeramicWhite", "#E8E9E5"); Add("ApplianceWhite", "#D4D8D9");
        Add("Countertop", "#A18768"); Add("BathroomTeal", "#628A8C");
        Add("MonitorBlack", "#25282C"); Add("PlantsGreen", "#567452");
        Add("Laundry", "#A69B8C");
        Add("WindowGlow", "#BDD1D6", true);
    }

    private static void Add(string key, string html, bool emission = false)
    {
        string path = MaterialFolder + "/" + key + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Falta el shader URP/Lit.");
            material = new Material(shader) { name = key };
            AssetDatabase.CreateAsset(material, path);
        }
        if (!ColorUtility.TryParseHtmlString(html, out Color color))
            throw new InvalidOperationException("Color no válido: " + html);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", emission ? .65f : .15f);
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(.28f, .38f, .43f));
        }
        EditorUtility.SetDirty(material);
        palette[key] = material;
    }

    private static void PaintRoom(GameObject root, string roomId, bool addWindowGlow)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            Paint(renderer, roomId, addWindowGlow);
    }

    private static void Paint(Renderer renderer, string roomId, bool addWindowGlow)
    {
        Transform transform = renderer.transform;
        if (transform.GetComponentInParent<AlienController>() != null
            || transform.GetComponentInParent<ChildPlayerController>() != null) return;
        if (transform.name == "Pared_DescansoSuperior_Seguridad") return; // Material manual del usuario.
        string key = MaterialFor(transform, roomId);
        if (key == null) return;
        Material material = palette[key];
        Material[] previous = renderer.sharedMaterials;
        int count = Mathf.Max(1, previous.Length);
        if (previous.Length == count && previous.All(m => m == material)) return;
        renderer.sharedMaterials = Enumerable.Repeat(material, count).ToArray();
        ProBuilderMesh mesh = renderer.GetComponent<ProBuilderMesh>();
        if (mesh != null)
        {
#pragma warning disable 618
            foreach (Face face in mesh.faces) { face.submeshIndex = 0; face.material = material; }
#pragma warning restore 618
            EditorUtility.SetDirty(mesh);
        }
        EditorUtility.SetDirty(renderer);
        painted++;
        if (addWindowGlow && key == "WindowGlow") AddWindowLight(transform);
    }

    private static string MaterialFor(Transform item, string roomId)
    {
        string n = item.name;
        string parent = item.parent != null ? item.parent.name : "";
        string whole = (n + " " + parent).ToLowerInvariant();
        if (n == "Piso") return roomId == "child_room" ? "FloorWalnut"
            : roomId == "foyer" || roomId == "stair_hall" || roomId == "kitchen" || roomId == "lower_bathroom" ? "FloorGrey" : "FloorOak";
        if (n == "Techo") return "CeilingBrown";
        if (n.StartsWith("Pared_") || n == "SeparadorSeguridadEscalera")
        {
            if (roomId == "child_room") return "WallChildBlue";
            if (roomId == "library" || roomId == "office" || roomId == "parents_bedroom") return "WallCharcoal";
            if (roomId == "upper_bathroom") return "WallBathroomBlueGrey";
            if (roomId == "lower_bathroom") return "WallBathroomGrey";
            if (roomId == "upper_corridor") return "WallUpperTan";
            return roomId == "kitchen" ? "WallBrown" : "WallWarmBrown";
        }
        // La ventana suele vivir dentro del escondite Cortina; miramos su nombre, no el del padre.
        if (n.ToLowerInvariant().Contains("ventana")) return "WindowGlow";
        if (whole.Contains("barral")) return "MetalDark";
        if (whole.Contains("cortina")) return "CurtainIvory";
        if (n.StartsWith("Marco_") || n == "HojaPuerta") return "DoorWhite";
        if (whole.Contains("escalera") || whole.Contains("descanso")) return "WoodPale";
        if (whole.Contains("bisagra") || whole.Contains("manija") || whole.Contains("canilla")
            || whole.Contains("ganchos") || whole.Contains("soporte")) return "MetalDark";
        if (whole.Contains("colchon") || whole.Contains("ropa")) return "FabricCream";
        if (whole.Contains("ducha") || whole.Contains("banera") || whole.Contains("bañera")) return "CeramicWhite";
        if (whole.Contains("canasto")) return "Laundry";
        if (whole.Contains("armario")) return roomId == "child_room" ? "ChildBlue" : "WoodMedium";
        if (whole.Contains("cama") || whole.Contains("cabecera")) return "WoodDark";
        if (whole.Contains("escritorio") || whole.Contains("biblioteca")) return "WoodMedium";
        if (whole.Contains("baul") || whole.Contains("baúl") || whole.Contains("mueble")) return "WoodDark";
        if (whole.Contains("mesa")) return "WoodMedium";
        if (whole.Contains("silla")) return roomId == "child_room" ? "FabricBlue" : "FabricCream";
        if (whole.Contains("sofa") || whole.Contains("sillon") || whole.Contains("sillón")) return "UpholsteryBrown";
        if (whole.Contains("heladera") || whole.Contains("lavarropas") || whole.Contains("alacena")) return "ApplianceWhite";
        if (whole.Contains("cocina") || whole.Contains("horno") || whole.Contains("hornalla")) return "MetalDark";
        if (whole.Contains("mesada")) return n.Contains("Superior") ? "Countertop" : "ApplianceWhite";
        if (whole.Contains("inodoro") || whole.Contains("lavamanos") || whole.Contains("vanitory")
            || whole.Contains("pileta")) return "CeramicWhite";
        if (whole.Contains("monitor") || whole.Contains("televisor") || whole.Contains("celular")) return "MonitorBlack";
        if (whole.Contains("planta")) return "PlantsGreen";
        if (whole.Contains("perchero") || whole.Contains("consola")) return "WoodMedium";
        if (n == "Pata" || n == "Lateral" || n == "Base" || n.StartsWith("Estante_")
            || n.StartsWith("Superior_")) return "WoodMedium";
        return null;
    }

    private static void AddWindowLight(Transform window)
    {
        Transform existing = window.Find("LuzSuaveVentana");
        if (existing != null) return;
        var lamp = new GameObject("LuzSuaveVentana");
        lamp.layer = 2; // Ignore Raycast: ni el Player ni los aliens la detectan como obstáculo.
        lamp.transform.SetParent(window, false);
        lamp.transform.localPosition = Vector3.zero;
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(.70f, .81f, .88f);
        light.intensity = .3f;
        light.range = 1.65f;
        light.shadows = LightShadows.None;
        lights++;
    }
}
