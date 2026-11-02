using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FinalAlienGrassPainter
{
    private const string Source = "Assets/Textures/simple-grass-chunks/source/rostlinka_07c_ske/rostlinka_07c_ske.FBX";
    private const string Textures = "Assets/Textures/simple-grass-chunks/textures";
    private const string Folder = "Assets/Props/AlienGrass";
    private const string MeshPath = Folder + "/GrassClump_TerrainMesh.asset";
    private const string TexturePath = Folder + "/GrassClump_AlbedoAlpha.png";
    private const string MaterialPath = Folder + "/MAT_GrassClump.mat";
    private const string PrefabPath = Folder + "/GrassClump_TerrainDetail.prefab";
    private const float MeshGroundOffset = .5f;

    [MenuItem("The Lights Before Dawn/Arte/Corregir apoyo del pasto")]
    private static void CorrectGrounding()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Terrain terrain = GameObject.Find("AlienTerrain")?.GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null
            || terrain.gameObject.scene.name != "FinalAlienLandscape")
        {
            Debug.LogError("Abrí FinalAlienLandscape con AlienTerrain activo antes de corregir el pasto.");
            return;
        }
        TerrainData data = terrain.terrainData;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (prefab == null || mesh == null || source == null) return;

        EnsureGroundedMesh(mesh, source);
        float visibleBase = FindVisibleBase();
        Undo.RegisterCompleteObjectUndo(data, "Corregir apoyo del pasto");
        TreeInstance[] trees = data.treeInstances;
        int corrected = 0;
        for (int i = 0; i < trees.Length; i++)
        {
            TreeInstance tree = trees[i];
            if (data.treePrototypes[tree.prototypeIndex].prefab != prefab) continue;
            tree.position = GroundedPosition(data, mesh, tree, visibleBase);
            trees[i] = tree;
            corrected++;
        }
        // La propiedad treeInstances vuelve a pegar cada árbol al heightmap.
        // Esta sobrecarga conserva el desplazamiento vertical calculado.
        data.SetTreeInstances(trees, false);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        terrain.Flush();
        Debug.Log($"Apoyo del pasto corregido: {corrected} matas; posiciones X/Z, escala, giro y otros prototipos conservados.");
        InspectGrass();
    }

    private static Vector3 GroundedPosition(TerrainData data, Mesh mesh, TreeInstance tree,
        float visibleBase)
    {
        Vector3 position = tree.position;
        float height = data.GetInterpolatedHeight(position.x, position.z);
        // La textura es transparente bajo la mata. Apoyar su primer píxel opaco,
        // no el borde geométrico invisible de la tarjeta.
        float slope = data.GetSteepness(position.x, position.z) * Mathf.Deg2Rad;
        float radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.z) * tree.widthScale;
        float burial = .10f * tree.heightScale + Mathf.Tan(slope) * radius;
        position.y = (height - visibleBase * tree.heightScale - burial) / data.size.y;
        return position;
    }

    [MenuItem("The Lights Before Dawn/Arte/Auditar altura del pasto")]
    private static void AuditHeight()
    {
        Terrain terrain = GameObject.Find("AlienTerrain")?.GetComponent<Terrain>();
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;
        float smallest = float.MaxValue;
        float largest = float.MinValue;
        float smallestBase = float.MaxValue;
        float largestBase = float.MinValue;
        float smallestVisible = float.MaxValue;
        float largestVisible = float.MinValue;
        int floating = 0;
        int count = 0;
        float lowestGround = float.MaxValue;
        float highestGround = float.MinValue;
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        float visibleBase = FindVisibleBase();
        foreach (TreeInstance tree in data.treeInstances)
        {
            if (data.treePrototypes[tree.prototypeIndex].prefab?.name != "GrassClump_TerrainDetail") continue;
            float ground = data.GetInterpolatedHeight(tree.position.x, tree.position.z);
            float gap = tree.position.y * data.size.y - ground;
            float baseGap = gap + (mesh != null ? mesh.bounds.min.y * tree.heightScale : 0f);
            float visibleGap = gap + visibleBase * tree.heightScale;
            smallestBase = Mathf.Min(smallestBase, baseGap);
            largestBase = Mathf.Max(largestBase, baseGap);
            smallestVisible = Mathf.Min(smallestVisible, visibleGap);
            largestVisible = Mathf.Max(largestVisible, visibleGap);
            if (visibleGap > .02f) floating++;
            lowestGround = Mathf.Min(lowestGround, ground);
            highestGround = Mathf.Max(highestGround, ground);
            smallest = Mathf.Min(smallest, gap);
            largest = Mathf.Max(largest, gap);
            count++;
        }
        Debug.Log($"Altura del pasto: {count} matas, pivote/heightmap {smallest:F3}..{largest:F3} m; base geométrica {smallestBase:F3}..{largestBase:F3} m; base opaca {smallestVisible:F3}..{largestVisible:F3} m; posibles flotantes {floating}; suelo {lowestGround:F3}..{highestGround:F3} m; primer píxel opaco {visibleBase:F3} m.");
    }

    private static float FindVisibleBase()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        Mesh assetMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (source == null || assetMesh == null || !File.Exists(TexturePath)) return 0f;
        Mesh sourceMesh = CreateMesh(source);
        Texture2D texture = new Texture2D(2, 2);
        try
        {
            if (sourceMesh == null || !texture.LoadImage(File.ReadAllBytes(TexturePath))) return 0f;
            Vector3[] vertices = sourceMesh.vertices;
            Vector2[] uvs = sourceMesh.uv;
            int[] triangles = sourceMesh.triangles;
            float minimum = float.MaxValue;
            const int samples = 16;
            for (int i = 0; i < triangles.Length; i += 3)
                for (int a = 0; a <= samples; a++)
                    for (int b = 0; b <= samples - a; b++)
                    {
                        float x = a / (float)samples;
                        float y = b / (float)samples;
                        float z = 1f - x - y;
                        Vector2 uv = uvs[triangles[i]] * x + uvs[triangles[i + 1]] * y
                            + uvs[triangles[i + 2]] * z;
                        if (texture.GetPixelBilinear(uv.x, uv.y).a < .38f) continue;
                        float height = vertices[triangles[i]].y * x + vertices[triangles[i + 1]].y * y
                            + vertices[triangles[i + 2]].y * z;
                        minimum = Mathf.Min(minimum, height);
                    }
            return minimum == float.MaxValue ? assetMesh.bounds.min.y
                : minimum + assetMesh.bounds.min.y;
        }
        finally
        {
            if (sourceMesh != null) UnityEngine.Object.DestroyImmediate(sourceMesh);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar pasto de cerca")]
    private static void InspectGrass()
    {
        Terrain terrain = GameObject.Find("AlienTerrain")?.GetComponent<Terrain>();
        SceneView view = SceneView.lastActiveSceneView;
        if (terrain == null || view == null)
            return;

        Vector3 center = new Vector3(-25f, 0f, 14f);
        center.y = terrain.SampleHeight(center) + terrain.transform.position.y;
        TerrainData data = terrain.terrainData;
        int nearby = 0;
        foreach (TreeInstance tree in data.treeInstances)
        {
            if (data.treePrototypes[tree.prototypeIndex].prefab?.name != "GrassClump_TerrainDetail")
                continue;
            Vector3 position = terrain.transform.position + Vector3.Scale(tree.position, data.size);
            if ((position - center).sqrMagnitude < 36f)
                nearby++;
        }
        Debug.Log($"Pasto cercano: {nearby} matas a 6 m de {center}; terreno activo {terrain.drawTreesAndFoliage}.");
        view.LookAt(center, Quaternion.Euler(35f, 0f, 0f), 6f);
        view.Repaint();
    }

    [MenuItem("The Lights Before Dawn/Arte/Pintar pasto del paisaje final")]
    private static void Paint()
    {
        Terrain terrain = GameObject.Find("AlienTerrain")?.GetComponent<Terrain>();
        if (EditorApplication.isPlayingOrWillChangePlaymode || terrain == null
            || terrain.gameObject.scene.name != "FinalAlienLandscape")
        {
            Debug.LogError("Abrí FinalAlienLandscape y salí de Play antes de pintar el pasto.");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (terrain == null || terrain.terrainData == null || source == null)
        {
            Debug.LogError("Falta AlienTerrain o el FBX del pasto.");
            return;
        }

        TerrainData data = terrain.terrainData;
        int existingIndex = Array.FindIndex(data.detailPrototypes,
            existing => existing.prototype != null && existing.prototype.name == "GrassClump_TerrainDetail");

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Props", "AlienGrass");

        Mesh mesh = PrepareMesh(source);
        if (mesh != null) EnsureGroundedMesh(mesh, source);
        float visibleBase = mesh != null ? FindVisibleBase() : 0f;
        Texture2D texture = PrepareTexture();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (mesh == null || texture == null || shader == null)
        {
            Debug.LogError("No se pudo preparar la mata, la textura con alfa o el shader URP.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MAT_GrassClump", enableInstancing = true };
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.38f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_ALPHATEST_ON");
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetTexture("_MainTex", texture);
        EditorUtility.SetDirty(material);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            GameObject root = new GameObject("GrassClump_TerrainDetail");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        Undo.RegisterCompleteObjectUndo(data, "Pintar pasto del paisaje final");
        if (existingIndex >= 0)
        {
            List<DetailPrototype> details = new List<DetailPrototype>(data.detailPrototypes);
            details.RemoveAt(existingIndex);
            data.detailPrototypes = details.ToArray();
        }
        List<TreePrototype> prototypes = new List<TreePrototype>(data.treePrototypes);
        int treeIndex = prototypes.FindIndex(existing => existing.prefab == prefab);
        if (treeIndex < 0)
        {
            treeIndex = prototypes.Count;
            prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0f });
            data.treePrototypes = prototypes.ToArray();
        }
        List<TreeInstance> trees = new List<TreeInstance>();
        foreach (TreeInstance tree in data.treeInstances)
            if (tree.prototypeIndex != treeIndex)
                trees.Add(tree);
        const int resolution = 256;
        Vector2[] patches =
        {
            new Vector2(-25, 14), new Vector2(21, 24), new Vector2(-39, 43),
            new Vector2(38, 54), new Vector2(-13, 59), new Vector2(8, 72),
            new Vector2(-44, -20), new Vector2(31, -28), new Vector2(-58, 6)
        };
        float[] strengths = { 0.80f, 0.64f, 0.56f, 0.70f, 0.42f, 0.48f, 0.62f, 0.46f, 0.35f };
        System.Random random = new System.Random(1701);
        float baseHeight = terrain.SampleHeight(Vector3.zero);
        int placed = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float nx = (x + 0.15f + (float)random.NextDouble() * 0.7f) / resolution;
                float nz = (z + 0.15f + (float)random.NextDouble() * 0.7f) / resolution;
                float worldX = terrain.transform.position.x + nx * data.size.x;
                float worldZ = terrain.transform.position.z + nz * data.size.z;
                float height = data.GetInterpolatedHeight(nx, nz);
                if (height > baseHeight + 4.5f || data.GetSteepness(nx, nz) > 18f)
                    continue;
                if (worldX * worldX + (worldZ + 3f) * (worldZ + 3f) < 20f)
                    continue;

                float density = 0f;
                for (int i = 0; i < patches.Length; i++)
                {
                    float dx = worldX - patches[i].x;
                    float dz = worldZ - patches[i].y;
                    float radius = i % 3 == 0 ? 18f : 13f;
                    density += strengths[i] * Mathf.Exp(-(dx * dx + dz * dz) / (2f * radius * radius));
                }
                float variation = Mathf.PerlinNoise(worldX * 0.11f + 8f, worldZ * 0.11f + 19f);
                density = Mathf.Clamp01(density * Mathf.Lerp(0.2f, 1.25f, variation) * 0.7f);
                if (random.NextDouble() >= density)
                    continue;

                float scale = Mathf.Lerp(0.6f, 1.8f, (float)random.NextDouble());
                trees.Add(new TreeInstance
                {
                    position = new Vector3(nx, height / data.size.y, nz),
                    prototypeIndex = treeIndex,
                    widthScale = scale * Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble()),
                    heightScale = scale,
                    rotation = (float)random.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
                placed++;
            }
        }

        for (int i = 0; i < trees.Count; i++)
        {
            TreeInstance tree = trees[i];
            if (tree.prototypeIndex != treeIndex) continue;
            tree.position = GroundedPosition(data, mesh, tree, visibleBase);
            trees[i] = tree;
        }
        data.SetTreeInstances(trees.ToArray(), false);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        terrain.Flush();
        Debug.Log($"Pasto pintado como árboles del Terrain: {placed} matas, malla {mesh.bounds.size}. Alturas mayores de {baseHeight + 4.5f:F1} m y pendientes fuertes excluidas.");
    }

    private static Mesh PrepareMesh(GameObject source)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing != null)
            return existing;

        Mesh mesh = CreateMesh(source);
        if (mesh != null) AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }

    private static void EnsureGroundedMesh(Mesh mesh, GameObject source)
    {
        if (Mathf.Abs(mesh.bounds.min.y + MeshGroundOffset) < .001f) return;
        Mesh replacement = CreateMesh(source);
        if (replacement == null) throw new InvalidOperationException("No se pudo reconstruir la malla del pasto.");
        try
        {
            Vector3[] vertices = replacement.vertices;
            float shift = -MeshGroundOffset - replacement.bounds.min.y;
            for (int i = 0; i < vertices.Length; i++) vertices[i].y += shift;
            replacement.vertices = vertices;
            replacement.RecalculateBounds();
            Undo.RegisterCompleteObjectUndo(mesh, "Corregir pivote del pasto");
            EditorUtility.CopySerialized(replacement, mesh);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(replacement);
        }
    }

    private static Mesh CreateMesh(GameObject source)
    {

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            MeshFilter selected = null;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == "rostlinka_07c")
                    selected = filter;
            if (selected == null || selected.sharedMesh == null)
                return null;

            Mesh mesh = UnityEngine.Object.Instantiate(selected.sharedMesh);
            mesh.name = "GrassClump_TerrainMesh";
            Matrix4x4 matrix = selected.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++)
                bounds.Encapsulate(vertices[i]);
            Vector3 offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = (vertices[i] - offset) / bounds.size.y;
            mesh.vertices = vertices;

            Vector3[] normals = mesh.normals;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static Texture2D PrepareTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (existing != null)
            return existing;

        Texture2D diffuse = new Texture2D(2, 2);
        Texture2D mask = new Texture2D(2, 2);
        try
        {
            if (!diffuse.LoadImage(File.ReadAllBytes(Textures + "/rostlinka_07c_diffuse.jpeg")) ||
                !mask.LoadImage(File.ReadAllBytes(Textures + "/rostlinka_07c_alfa.jpeg")))
                return null;

            const int size = 1024;
            Texture2D combined = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    Color color = diffuse.GetPixelBilinear(u, v);
                    color.a = mask.GetPixelBilinear(u, v).grayscale;
                    pixels[y * size + x] = color;
                }
            combined.SetPixels(pixels);
            combined.Apply();
            File.WriteAllBytes(TexturePath, combined.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(combined);
            AssetDatabase.ImportAsset(TexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(diffuse);
            UnityEngine.Object.DestroyImmediate(mask);
        }
    }
}
