using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Crea una escena de ensayo independiente; jamás edita ModularPrototype ni los prefabs originales.</summary>
public static class PortalTrialSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/PortalTrial_ChildCorridor.unity";
    private const string ChildPath = "Assets/Generated/ModularPrototype/Rooms/Room_child_room.prefab";
    private const string CorridorPath = "Assets/Generated/ModularPrototype/Rooms/Room_upper_corridor.prefab";

    [MenuItem("The Lights Before Dawn/Pruebas/Crear escena de portal niño-pasillo")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Debug.Log("La escena de prueba ya existe; no se sobrescribe: " + ScenePath);
            return;
        }
        GameObject childPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChildPath);
        GameObject corridorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorridorPath);
        if (childPrefab == null || corridorPrefab == null)
        {
            Debug.LogError("Falta uno de los prefabs necesarios para el portal de prueba.");
            return;
        }

        var previousScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        RoomModule child = ((GameObject)PrefabUtility.InstantiatePrefab(childPrefab)).GetComponent<RoomModule>();
        RoomModule corridor = ((GameObject)PrefabUtility.InstantiatePrefab(corridorPrefab)).GetComponent<RoomModule>();
        child.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        corridor.transform.SetPositionAndRotation(new Vector3(0f, 0f, 35f), Quaternion.identity);

        DoorSocket childSocket = FindSocket(child, "child_east");
        DoorSocket corridorSocket = FindSocket(corridor, "corridor_north_left");
        if (childSocket == null || corridorSocket == null)
        {
            Debug.LogError("No se encontraron los marcos child_east y corridor_north_left.");
            SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
            return;
        }
        // Solo en las instancias de esta escena: los marcos quedan abiertos para observar el ensayo.
        childSocket.SetPortalAssemblyActive(false);
        corridorSocket.SetPortalAssemblyActive(false);

        GameObject player = new GameObject("Jugador de prueba (WASD)");
        player.tag = "Player";
        player.transform.position = childSocket.Position - childSocket.Outward * 1.35f + Vector3.up * .02f;
        player.transform.rotation = Quaternion.LookRotation(childSocket.Outward, Vector3.up);
        CharacterController capsule = player.AddComponent<CharacterController>();
        capsule.height = 1.15f;
        capsule.radius = .22f;
        capsule.center = Vector3.up * .575f;
        capsule.stepOffset = .2f;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, .99f, 0f);
        Camera view = cameraObject.AddComponent<Camera>();
        view.nearClipPlane = .03f;
        cameraObject.AddComponent<AudioListener>();

        PortalTrialGate entrance = childSocket.gameObject.AddComponent<PortalTrialGate>();
        PortalTrialGate exit = corridorSocket.gameObject.AddComponent<PortalTrialGate>();
        entrance.Configure(exit, view);
        exit.Configure(entrance, view);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cubo de ensayo (F para lanzar)";
        cube.transform.position = player.transform.position + childSocket.Outward * .6f + Vector3.up * .45f;
        cube.transform.localScale = Vector3.one * .22f;
        Rigidbody rigidbody = cube.AddComponent<Rigidbody>();
        rigidbody.mass = .3f;
        PortalTrialTraveller cubeTraveller = cube.AddComponent<PortalTrialTraveller>();
        cubeTraveller.Configure(entrance, exit);

        PortalTrialTraveller playerTraveller = player.AddComponent<PortalTrialTraveller>();
        playerTraveller.Configure(entrance, exit);
        PortalTrialPlayer controls = player.AddComponent<PortalTrialPlayer>();
        controls.Configure(view, rigidbody);

        GameObject lightObject = new GameObject("Luz de prueba");
        lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;

        EditorSceneManager.SaveScene(scene, ScenePath);
        SceneManager.SetActiveScene(previousScene);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        Debug.Log("Escena de portal creada: " + ScenePath + ". ModularPrototype y los prefabs no se modificaron.");
    }

    private static DoorSocket FindSocket(RoomModule room, string id)
    {
        foreach (DoorSocket socket in room.GetComponentsInChildren<DoorSocket>(true))
            if (socket.SocketId == id) return socket;
        return null;
    }
}
