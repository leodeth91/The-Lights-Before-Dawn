using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OrangeQuartzPrefabPreparation
{
    private const string Folder = "Assets/Props/OrangeQuartzMineral";

    [MenuItem("The Lights Before Dawn/Arte/Dispersar cuarzo en el paisaje final")]
    private static void ScatterRocks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "FinalAlienLandscape")
        {
            Debug.LogError("Abrí FinalAlienLandscape y salí de Play antes de dispersar el cuarzo.");
            return;
        }

        Terrain terrain = GameObject.Find("AlienTerrain")?.GetComponent<Terrain>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/OrangeQuartzRock_PaintTrees.prefab");
        if (terrain == null || prefab == null)
        {
            Debug.LogError("Falta AlienTerrain o el prefab de cuarzo.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int rockIndex = System.Array.FindIndex(data.treePrototypes, prototype => prototype.prefab == prefab);
        if (rockIndex < 0)
        {
            Debug.LogError("Primero agregá el prefab de cuarzo a Paint Trees.");
            return;
        }

        foreach (TreeInstance instance in data.treeInstances)
        {
            if (instance.prototypeIndex == rockIndex)
            {
                Debug.LogWarning("Ya hay cuarzo en este Terrain. Se conserva tal como está.");
                return;
            }
        }

        // Coordenadas de mundo alrededor de la puerta; el centro queda libre para caminar.
        Vector3[] positions =
        {
            new Vector3(-7, 0, 3), new Vector3(9, 0, 5),
            new Vector3(-13, 0, 10), new Vector3(15, 0, 13),
            new Vector3(-5, 0, 17), new Vector3(8, 0, 21),
            new Vector3(-20, 0, 20), new Vector3(23, 0, 24),
            new Vector3(-11, 0, 31), new Vector3(16, 0, 34),
            new Vector3(-27, 0, 38), new Vector3(28, 0, 42),
            new Vector3(-4, 0, 47), new Vector3(9, 0, 51),
            new Vector3(-24, 0, -12), new Vector3(21, 0, -17)
        };
        float[] scales = { 1.3f, 2.4f, 0.9f, 1.7f, 2.8f, 1.1f, 2.1f, 1.5f,
            2.6f, 0.8f, 1.9f, 2.3f, 1.2f, 2.0f, 1.4f, 1.8f };
        List<TreeInstance> instances = new List<TreeInstance>(data.treeInstances);
        int added = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 local = positions[i] - terrain.transform.position;
            float x = local.x / data.size.x;
            float z = local.z / data.size.z;
            if (x <= 0 || x >= 1 || z <= 0 || z >= 1 || data.GetSteepness(x, z) > 32f)
                continue;

            bool nearTree = false;
            foreach (TreeInstance existing in instances)
            {
                float dx = (existing.position.x - x) * data.size.x;
                float dz = (existing.position.z - z) * data.size.z;
                if (dx * dx + dz * dz < 10f)
                {
                    nearTree = true;
                    break;
                }
            }
            if (nearTree)
                continue;

            instances.Add(new TreeInstance
            {
                position = new Vector3(x, data.GetInterpolatedHeight(x, z) / data.size.y, z),
                prototypeIndex = rockIndex,
                widthScale = scales[i],
                heightScale = scales[i],
                rotation = (i * 137.5f % 360f) * Mathf.Deg2Rad,
                color = Color.white,
                lightmapColor = Color.white
            });
            added++;
        }

        Undo.RegisterCompleteObjectUndo(data, "Dispersar cuarzo en paisaje final");
        data.treeInstances = instances.ToArray();
        terrain.Flush();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cuarzo en Paint Trees: {added} rocas de distintos tamaños. Árboles previos conservados.");
    }

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar árboles del paisaje final")]
    private static void InspectTerrain()
    {
        if (SceneManager.GetActiveScene().name != "FinalAlienLandscape")
            return;

        Terrain terrain = GameObject.Find("AlienTerrain")?.GetComponent<Terrain>();
        if (terrain == null)
            return;

        TerrainData data = terrain.terrainData;
        string report = $"Terrain {terrain.transform.position}, tamaño {data.size}, instancias {data.treeInstanceCount}. ";
        for (int i = 0; i < data.treePrototypes.Length; i++)
        {
            int count = 0;
            foreach (TreeInstance instance in data.treeInstances)
                if (instance.prototypeIndex == i)
                    count++;
            report += $"[{i}] {data.treePrototypes[i].prefab?.name}: {count}. ";
        }
        Camera camera = Camera.main;
        report += $"Cámara principal: {camera?.transform.position}, dirección {camera?.transform.forward}. ";
        GameObject door = GameObject.Find("DoorStage");
        report += $"Puerta: {door?.transform.position}.";
        Debug.Log(report);
    }

    [MenuItem("The Lights Before Dawn/Arte/Preparar cuarzo para Paint Trees")]
    private static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Salí de Play antes de preparar el cuarzo.");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/OrangeQuartzRock_Low.fbx");
        Texture2D firstTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/yellow-stone.jpg");
        Texture2D secondTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/yellow-stone1.jpg");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (model == null || firstTexture == null || secondTexture == null || shader == null)
        {
            Debug.LogError("Falta el FBX simplificado, alguna textura o el shader URP Lit.");
            return;
        }

        Material firstMaterial = CreateMaterial(Folder + "/MAT_OrangeQuartz_A.mat", shader, firstTexture);
        Material secondMaterial = CreateMaterial(Folder + "/MAT_OrangeQuartz_B.mat", shader, secondTexture);
        string meshPath = Folder + "/OrangeQuartzRock_TerrainMesh.asset";
        string prefabPath = Folder + "/OrangeQuartzRock_PaintTrees.prefab";
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            MeshFilter sourceFilter = visual.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer sourceRenderer = sourceFilter != null ? sourceFilter.GetComponent<MeshRenderer>() : null;
            if (sourceRenderer == null || sourceFilter.sharedMesh == null)
            {
                Debug.LogError("El FBX no contiene MeshRenderer válidos para Paint Trees.");
                return;
            }

            Mesh terrainMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (terrainMesh == null)
            {
                terrainMesh = Object.Instantiate(sourceFilter.sharedMesh);
                terrainMesh.name = "OrangeQuartzRock_TerrainMesh";
                Matrix4x4 modelToRoot = sourceFilter.transform.localToWorldMatrix;
                Vector3[] vertices = terrainMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = modelToRoot.MultiplyPoint3x4(vertices[i]);

                Bounds bounds = new Bounds(vertices[0], Vector3.zero);
                for (int i = 1; i < vertices.Length; i++)
                    bounds.Encapsulate(vertices[i]);
                Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] -= offset;
                terrainMesh.vertices = vertices;

                Vector3[] normals = terrainMesh.normals;
                Matrix4x4 normalMatrix = modelToRoot.inverse.transpose;
                for (int i = 0; i < normals.Length; i++)
                    normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                terrainMesh.normals = normals;
                terrainMesh.RecalculateBounds();
                AssetDatabase.CreateAsset(terrainMesh, meshPath);
            }

            bool existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            GameObject root = existingPrefab
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject("OrangeQuartzRock_PaintTrees");
            try
            {
                while (root.transform.childCount > 0)
                    Object.DestroyImmediate(root.transform.GetChild(0).gameObject);

                MeshFilter filter = root.GetComponent<MeshFilter>();
                if (filter == null)
                    filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = terrainMesh;

                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                if (renderer == null)
                    renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { firstMaterial, secondMaterial };

                BoxCollider collider = root.GetComponent<BoxCollider>();
                if (collider == null)
                    collider = root.AddComponent<BoxCollider>();
                collider.center = terrainMesh.bounds.center;
                collider.size = terrainMesh.bounds.size * 0.8f;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"Cuarzo preparado para Paint Trees: {prefabPath}. Malla en la raíz, tamaño: {terrainMesh.bounds.size}.");
            }
            finally
            {
                if (existingPrefab)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }
        }
        finally
        {
            Object.DestroyImmediate(visual);
        }
    }

    private static Material CreateMaterial(string path, Shader shader, Texture2D texture)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;

        Material material = new Material(shader);
        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Smoothness", 0.4f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
