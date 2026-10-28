using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

internal static class WindowOpeningTool
{
    private const string RoomPath = "Assets/Generated/ModularPrototype/Rooms";
    private const string MeshPath = "Assets/Generated/ModularPrototype/WindowOpenings";
    private const string CutWallName = "MuroConAberturaVentanas";
    private const string CollisionRootName = "ColisionMuros";
    private const string GlassColliderName = "CristalColision";
    private const string OutsideLightName = "LuzExteriorVentana";

    [MenuItem("The Lights Before Dawn/Arte/Alinear UV de ventana del baño superior")]
    private static void AlignBathroomWindowWallUvs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Hay que salir de Play antes de editar las mallas de los muros.");
            return;
        }
        RoomModule room = UnityEngine.Object.FindObjectsByType<RoomModule>(FindObjectsSortMode.None)
            .FirstOrDefault(item => item.RoomId == "upper_bathroom");
        Transform wall = room?.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "Pared_Sur_0_0" && item.Find(CutWallName) != null);
        if (wall == null) throw new InvalidOperationException("No se encontró el muro de la ventana del baño superior.");
        Mesh original = wall.GetComponent<MeshFilter>()?.sharedMesh;
        MeshFilter cutFilter = wall.Find(CutWallName).GetComponent<MeshFilter>();
        Mesh prefabMesh = cutFilter.sharedMesh;
        if (original == null || prefabMesh == null)
            throw new InvalidOperationException("Falta la malla original o la malla con abertura.");
        const string sceneMeshPath = MeshPath + "/upper_bathroom_scene_Pared_Sur_0_0.asset";
        Mesh sceneMesh = AssetDatabase.LoadAssetAtPath<Mesh>(sceneMeshPath);
        if (sceneMesh == null)
        {
            sceneMesh = UnityEngine.Object.Instantiate(prefabMesh);
            sceneMesh.name = "upper_bathroom_scene_Pared_Sur_0_0";
            AssetDatabase.CreateAsset(sceneMesh, sceneMeshPath);
        }
        Bounds slab = wall.GetComponent<Renderer>().localBounds;
        ApplyOriginalFaceUvs(sceneMesh, original, slab, slab.size.x < slab.size.z);
        EditorUtility.SetDirty(sceneMesh);
        Undo.RecordObject(cutFilter, "Alinear azulejos de ventana del baño");
        cutFilter.sharedMesh = sceneMesh;
        PrefabUtility.RecordPrefabInstancePropertyModifications(cutFilter);
        EditorSceneManager.MarkSceneDirty(wall.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(wall.gameObject.scene);
        Selection.activeGameObject = cutFilter.gameObject;
        SceneView.lastActiveSceneView?.LookAt(wall.TransformPoint(slab.center),
            Quaternion.LookRotation(-wall.forward, wall.up), 2f);
        Debug.Log("Azulejos del muro del baño alineados con el muro original.");
    }

    [MenuItem("The Lights Before Dawn/Arte/Probar abertura en cuarto infantil")]
    private static void InstallPrototype() => Install(false);

    [MenuItem("The Lights Before Dawn/Arte/Abrir huecos para todas las ventanas")]
    private static void InstallAll() => Install(true);

    private static void Install(bool allRooms)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Hay que salir de Play antes de editar muros y prefabs.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(MeshPath))
            AssetDatabase.CreateFolder("Assets/Generated/ModularPrototype", "WindowOpenings");

        var report = new StringBuilder();
        int openings = 0;
        int wallsCut = 0;
        foreach (string file in Directory.GetFiles(RoomPath, "*.prefab"))
        {
            string path = file.Replace('\\', '/');
            if (!allRooms && !path.EndsWith("Room_child_room.prefab", StringComparison.Ordinal)) continue;
            GameObject room = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                Transform[] walls = room.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith("Pared_", StringComparison.Ordinal)
                        && item.GetComponent<BoxCollider>() != null).ToArray();
                Transform[] windows = room.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name == "Ventana").ToArray();
                var grouped = new Dictionary<Transform, List<Transform>>();
                foreach (Transform window in windows)
                {
                    Transform visual = window.Find("Visual_WindowOkno");
                    if (visual == null || visual.GetComponentsInChildren<Renderer>(true).Length == 0)
                        throw new InvalidOperationException("Falta el asset visual de " + room.name + "/" + window.parent.name);
                    Transform wall = FindWall(window, walls);
                    if (wall == null)
                        throw new InvalidOperationException("No se encontró muro para " + room.name + "/" + window.parent.name);
                    if (!grouped.TryGetValue(wall, out List<Transform> wallWindows))
                        grouped[wall] = wallWindows = new List<Transform>();
                    wallWindows.Add(window);
                }

                foreach (var group in grouped)
                {
                    CutWall(room.name, group.Key, group.Value, report);
                    wallsCut++;
                    openings += group.Value.Count;
                    changed = true;
                }
                foreach (Light light in room.GetComponentsInChildren<Light>(true))
                    if (light.name == "LuzHabitacion" || light.name == "LuzSuaveVentana")
                        light.enabled = false;
                if (changed || allRooms) PrefabUtility.SaveAsPrefabAsset(room, path);
            }
            catch (Exception error)
            {
                report.AppendLine("ERROR " + room.name + ": " + error.Message);
                Debug.LogException(error);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }
        if (allRooms && openings == 12)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.4f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.35f;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        report.Insert(0, "Aberturas=" + openings + " muros=" + wallsCut + "\n");
        string reportPath = Path.Combine(Path.GetTempPath(), "TLBD_WindowOpeningResult.txt");
        File.WriteAllText(reportPath, report.ToString());
        AssetDatabase.SaveAssets();
        Debug.Log("Aberturas=" + openings + " muros=" + wallsCut + ". Informe: " + reportPath);
    }

    private static Transform FindWall(Transform window, IEnumerable<Transform> walls)
    {
        Renderer backing = window.GetComponent<Renderer>();
        Bounds opening = InLocalSpace(window, backing);
        return walls.Select(wall => new { wall, renderer = wall.GetComponent<Renderer>() })
            .Where(item => item.renderer != null)
            .Select(item => new { item.wall, bounds = InLocalSpace(window, item.renderer) })
            .Where(item => Mathf.Abs(item.bounds.center.z) < 0.25f
                && item.bounds.min.x < opening.min.x && item.bounds.max.x > opening.max.x
                && item.bounds.min.y < opening.min.y && item.bounds.max.y > opening.max.y)
            .OrderBy(item => Mathf.Abs(item.bounds.center.z)).Select(item => item.wall).FirstOrDefault();
    }

    private static void CutWall(string roomName, Transform wall, List<Transform> windows, StringBuilder report)
    {
        Renderer original = wall.GetComponent<Renderer>();
        BoxCollider originalCollider = wall.GetComponent<BoxCollider>();
        MeshFilter originalFilter = wall.GetComponent<MeshFilter>();
        if (original == null || originalCollider == null || originalFilter == null)
            throw new InvalidOperationException("Muro incompleto: " + wall.name);

        Bounds slab = original.localBounds;
        bool depthAlongX = slab.size.x < slab.size.z;
        float slabUmin = depthAlongX ? slab.min.z : slab.min.x;
        float slabUmax = depthAlongX ? slab.max.z : slab.max.x;
        var holes = new List<Rect>();
        foreach (Transform window in windows)
        {
            Renderer[] visualRenderers = window.Find("Visual_WindowOkno")
                .GetComponentsInChildren<Renderer>(true);
            Bounds visual = CombinedBounds(wall, visualRenderers);
            float clearance = 0.001f;
            Rect hole = Rect.MinMaxRect((depthAlongX ? visual.min.z : visual.min.x) - clearance,
                visual.min.y - clearance,
                (depthAlongX ? visual.max.z : visual.max.x) + clearance,
                visual.max.y + clearance);
            if (hole.xMin <= slabUmin + 0.02f || hole.xMax >= slabUmax - 0.02f
                || hole.yMin <= slab.min.y + 0.02f || hole.yMax >= slab.max.y - 0.02f)
                throw new InvalidOperationException("El marco se sale del muro: " + wall.name
                    + " slab=" + Describe(slab) + " cut=" + hole.ToString("F3"));
            holes.Add(hole);
        }
        for (int i = 0; i < holes.Count; i++)
        for (int j = i + 1; j < holes.Count; j++)
            if (holes[i].Overlaps(holes[j]))
                throw new InvalidOperationException("Huecos superpuestos en " + wall.name);

        report.AppendLine("  wall=" + wall.name + " slab=" + Describe(slab) + " cuts="
            + string.Join(", ", holes.Select(hole => hole.ToString("F3"))));

        Mesh generated = BuildPerforatedSlab(slab, holes, depthAlongX, out List<Bounds> solidCells);
        ApplyOriginalFaceUvs(generated, originalFilter.sharedMesh, slab, depthAlongX);
        string meshAssetPath = MeshPath + "/" + roomName + "_" + wall.name + ".asset";
        generated.name = Path.GetFileNameWithoutExtension(meshAssetPath);
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
        if (saved == null) { AssetDatabase.CreateAsset(generated, meshAssetPath); saved = generated; }
        else { EditorUtility.CopySerialized(generated, saved); saved.name = generated.name; UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(saved); }

        Transform previous = wall.Find(CutWallName);
        GameObject openingWall = previous != null ? previous.gameObject
            : new GameObject(CutWallName, typeof(MeshFilter), typeof(MeshRenderer));
        if (previous == null) EditorSceneManager.MoveGameObjectToScene(openingWall, wall.gameObject.scene);
        openingWall.transform.SetParent(wall, false);
        openingWall.transform.localPosition = Vector3.zero;
        openingWall.transform.localRotation = Quaternion.identity;
        openingWall.transform.localScale = Vector3.one;
        openingWall.layer = wall.gameObject.layer;
        MeshFilter filter = openingWall.GetComponent<MeshFilter>() ?? openingWall.AddComponent<MeshFilter>();
        MeshRenderer renderer = openingWall.GetComponent<MeshRenderer>() ?? openingWall.AddComponent<MeshRenderer>();
        filter.sharedMesh = saved;
        renderer.sharedMaterials = original.sharedMaterials;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        MeshCollider previousCollision = openingWall.GetComponent<MeshCollider>();
        if (previousCollision != null) UnityEngine.Object.DestroyImmediate(previousCollision);
        Transform oldCollisionRoot = openingWall.transform.Find(CollisionRootName);
        if (oldCollisionRoot != null) UnityEngine.Object.DestroyImmediate(oldCollisionRoot.gameObject);
        GameObject collisionRoot = new GameObject(CollisionRootName);
        EditorSceneManager.MoveGameObjectToScene(collisionRoot, wall.gameObject.scene);
        collisionRoot.transform.SetParent(openingWall.transform, false);
        for (int index = 0; index < solidCells.Count; index++)
        {
            GameObject segment = new GameObject("Seccion_" + index.ToString("D2"), typeof(BoxCollider));
            EditorSceneManager.MoveGameObjectToScene(segment, wall.gameObject.scene);
            segment.transform.SetParent(collisionRoot.transform, false);
            segment.layer = wall.gameObject.layer;
            BoxCollider box = segment.GetComponent<BoxCollider>();
            box.center = solidCells[index].center;
            box.size = solidCells[index].size;
            box.sharedMaterial = originalCollider.sharedMaterial;
        }
        bool sceneInstance = PrefabUtility.IsPartOfPrefabInstance(wall.gameObject);
        if (sceneInstance)
        {
            Undo.RecordObject(original, "Abrir hueco de ventana");
            Undo.RecordObject(originalCollider, "Abrir hueco de ventana");
        }
        original.enabled = false;
        originalCollider.enabled = false;
        if (sceneInstance)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(original);
            PrefabUtility.RecordPrefabInstancePropertyModifications(originalCollider);
        }

        foreach (Transform window in windows)
        {
            Renderer backing = window.GetComponent<Renderer>();
            if (sceneInstance) Undo.RecordObject(backing, "Mostrar ventana transparente");
            backing.enabled = false;
            if (sceneInstance) PrefabUtility.RecordPrefabInstancePropertyModifications(backing);
            Bounds visual = CombinedBounds(window, window.Find("Visual_WindowOkno")
                .GetComponentsInChildren<Renderer>(true));
            Transform existingGlassCollider = window.Find(GlassColliderName);
            GameObject glassObject = existingGlassCollider != null ? existingGlassCollider.gameObject
                : new GameObject(GlassColliderName, typeof(BoxCollider));
            if (existingGlassCollider == null)
            {
                EditorSceneManager.MoveGameObjectToScene(glassObject, window.gameObject.scene);
                glassObject.transform.SetParent(window, false);
            }
            glassObject.transform.localPosition = Vector3.zero;
            glassObject.transform.localRotation = Quaternion.identity;
            glassObject.transform.localScale = Vector3.one;
            BoxCollider glassCollider = glassObject.GetComponent<BoxCollider>();
            glassCollider.center = new Vector3(visual.center.x, visual.center.y, visual.center.z);
            glassCollider.size = new Vector3(visual.size.x, visual.size.y, 0.04f);
            glassCollider.isTrigger = false;
            glassCollider.enabled = true;
            Transform lamp = window.Find("LuzSuaveVentana");
            if (lamp != null && lamp.GetComponent<Light>() != null) lamp.GetComponent<Light>().enabled = false;
            AddExteriorLight(window);
            report.AppendLine(roomName + "/" + window.parent.name + " wall=" + wall.name
                + " opening=" + Describe(visual) + " glassCollider=" + glassCollider.size.ToString("F3"));
        }
    }

    private static void AddExteriorLight(Transform window)
    {
        Transform current = window.Find(OutsideLightName);
        GameObject lightObject = current != null ? current.gameObject
            : new GameObject(OutsideLightName, typeof(Light));
        if (current == null) EditorSceneManager.MoveGameObjectToScene(lightObject, window.gameObject.scene);
        lightObject.transform.SetParent(window, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, 0.85f);
        lightObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        lightObject.layer = 2;
        Light light = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(0.58f, 0.7f, 0.9f);
        light.intensity = 0.38f;
        light.range = 5f;
        light.spotAngle = 76f;
        light.innerSpotAngle = 48f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 1f;
        light.shadowBias = 0.02f;
        light.shadowNormalBias = 0.2f;
        light.enabled = true;
    }

    private static Mesh BuildPerforatedSlab(Bounds slab, List<Rect> holes, bool depthAlongX,
        out List<Bounds> solidCells)
    {
        List<float> xs = depthAlongX
            ? new List<float> { slab.min.z, slab.max.z }
            : new List<float> { slab.min.x, slab.max.x };
        List<float> ys = new List<float> { slab.min.y, slab.max.y };
        foreach (Rect hole in holes)
        {
            xs.Add(hole.xMin); xs.Add(hole.xMax);
            ys.Add(hole.yMin); ys.Add(hole.yMax);
        }
        xs.Sort(); ys.Sort();
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        solidCells = new List<Bounds>();
        for (int x = 0; x < xs.Count - 1; x++)
        for (int y = 0; y < ys.Count - 1; y++)
        {
            float x0 = xs[x], x1 = xs[x + 1], y0 = ys[y], y1 = ys[y + 1];
            if (x1 - x0 < 0.0001f || y1 - y0 < 0.0001f) continue;
            Vector2 mid = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            if (holes.Any(hole => hole.Contains(mid))) continue;
            if (depthAlongX)
            {
                AddBox(vertices, uvs, triangles, slab.min.x, slab.max.x, y0, y1, x0, x1);
                solidCells.Add(new Bounds(new Vector3(slab.center.x, (y0+y1)*.5f, (x0+x1)*.5f),
                    new Vector3(slab.size.x, y1-y0, x1-x0)));
            }
            else
            {
                AddBox(vertices, uvs, triangles, x0, x1, y0, y1, slab.min.z, slab.max.z);
                solidCells.Add(new Bounds(new Vector3((x0+x1)*.5f, (y0+y1)*.5f, slab.center.z),
                    new Vector3(x1-x0, y1-y0, slab.size.z)));
            }
        }
        var mesh = new Mesh { name = "Muro con aberturas" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddBox(List<Vector3> v, List<Vector2> uv, List<int> t,
        float x0, float x1, float y0, float y1, float z0, float z1)
    {
        AddFace(v, uv, t, new Vector3(x0,y0,z0), new Vector3(x0,y1,z0),
            new Vector3(x1,y1,z0), new Vector3(x1,y0,z0), 0);
        AddFace(v, uv, t, new Vector3(x1,y0,z1), new Vector3(x1,y1,z1),
            new Vector3(x0,y1,z1), new Vector3(x0,y0,z1), 0);
        AddFace(v, uv, t, new Vector3(x0,y0,z1), new Vector3(x0,y1,z1),
            new Vector3(x0,y1,z0), new Vector3(x0,y0,z0), 1);
        AddFace(v, uv, t, new Vector3(x1,y0,z0), new Vector3(x1,y1,z0),
            new Vector3(x1,y1,z1), new Vector3(x1,y0,z1), 1);
        AddFace(v, uv, t, new Vector3(x0,y0,z0), new Vector3(x1,y0,z0),
            new Vector3(x1,y0,z1), new Vector3(x0,y0,z1), 2);
        AddFace(v, uv, t, new Vector3(x0,y1,z1), new Vector3(x1,y1,z1),
            new Vector3(x1,y1,z0), new Vector3(x0,y1,z0), 2);
    }

    private static void AddFace(List<Vector3> v, List<Vector2> uv, List<int> t,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, int axis)
    {
        int start = v.Count;
        foreach (Vector3 point in new[] { a, b, c, d })
        {
            v.Add(point);
            uv.Add(axis == 0 ? new Vector2(point.x + .5f, point.y + .5f)
                : axis == 1 ? new Vector2(point.z + .5f, point.y + .5f)
                : new Vector2(point.x + .5f, point.z + .5f));
        }
        t.Add(start); t.Add(start + 1); t.Add(start + 2);
        t.Add(start); t.Add(start + 2); t.Add(start + 3);
    }

    // Las caras interiores de ProBuilder pueden tener una rotación UV distinta del
    // material. Copiar su proyección evita que la textura cambie al abrir el muro.
    private static void ApplyOriginalFaceUvs(Mesh generated, Mesh original, Bounds slab, bool depthAlongX)
    {
        if (original == null || generated.vertexCount % 24 != 0)
            throw new InvalidOperationException("No se puede alinear la UV del muro generado.");
        float min = depthAlongX ? slab.min.x : slab.min.z;
        float max = depthAlongX ? slab.max.x : slab.max.z;
        FaceUvMap minMap = FindFaceUvMap(original, depthAlongX, min);
        FaceUvMap maxMap = FindFaceUvMap(original, depthAlongX, max);
        Vector3[] vertices = generated.vertices;
        Vector2[] uv = generated.uv;
        int minOffset = depthAlongX ? 8 : 0;
        int maxOffset = depthAlongX ? 12 : 4;
        for (int box = 0; box < vertices.Length; box += 24)
        for (int corner = 0; corner < 4; corner++)
        {
            int minIndex = box + minOffset + corner;
            int maxIndex = box + maxOffset + corner;
            uv[minIndex] = minMap.Evaluate(SurfacePoint(vertices[minIndex], depthAlongX));
            uv[maxIndex] = maxMap.Evaluate(SurfacePoint(vertices[maxIndex], depthAlongX));
        }
        generated.uv = uv;
        generated.RecalculateTangents();
    }

    private static FaceUvMap FindFaceUvMap(Mesh source, bool depthAlongX, float plane)
    {
        Vector3[] vertices = source.vertices;
        Vector2[] uv = source.uv;
        int[] triangles = source.triangles;
        float largestArea = 0f;
        FaceUvMap best = default;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if (a >= uv.Length || b >= uv.Length || c >= uv.Length) continue;
            if (Mathf.Abs((depthAlongX ? vertices[a].x : vertices[a].z) - plane) > .002f
                || Mathf.Abs((depthAlongX ? vertices[b].x : vertices[b].z) - plane) > .002f
                || Mathf.Abs((depthAlongX ? vertices[c].x : vertices[c].z) - plane) > .002f)
                continue;
            Vector2 p0 = SurfacePoint(vertices[a], depthAlongX);
            Vector2 p1 = SurfacePoint(vertices[b], depthAlongX);
            Vector2 p2 = SurfacePoint(vertices[c], depthAlongX);
            float area = Mathf.Abs((p1.x - p0.x) * (p2.y - p0.y)
                - (p1.y - p0.y) * (p2.x - p0.x));
            if (area <= largestArea) continue;
            largestArea = area;
            best = new FaceUvMap(p0, p1, p2, uv[a], uv[b], uv[c]);
        }
        if (largestArea < .0001f)
            throw new InvalidOperationException("Falta la cara original del muro para copiar sus UV: " + source.name);
        return best;
    }

    private static Vector2 SurfacePoint(Vector3 point, bool depthAlongX) => depthAlongX
        ? new Vector2(point.z, point.y) : new Vector2(point.x, point.y);

    private readonly struct FaceUvMap
    {
        private readonly Vector2 p0, p1, p2, uv0, uv1, uv2;

        public FaceUvMap(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            this.p0 = p0; this.p1 = p1; this.p2 = p2;
            this.uv0 = uv0; this.uv1 = uv1; this.uv2 = uv2;
        }

        public Vector2 Evaluate(Vector2 point)
        {
            Vector2 a = p1 - p0, b = p2 - p0, offset = point - p0;
            float determinant = a.x * b.y - a.y * b.x;
            float u = (offset.x * b.y - offset.y * b.x) / determinant;
            float v = (a.x * offset.y - a.y * offset.x) / determinant;
            return uv0 + u * (uv1 - uv0) + v * (uv2 - uv0);
        }
    }

    private static Bounds CombinedBounds(Transform reference, IEnumerable<Renderer> renderers)
    {
        Bounds combined = default;
        bool first = true;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            Bounds current = InLocalSpace(reference, renderer);
            if (first) { combined = current; first = false; }
            else combined.Encapsulate(current);
        }
        if (first) throw new InvalidOperationException("El modelo de ventana no tiene mallas visibles.");
        return combined;
    }

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar muros de ventanas")]
    private static void Inspect()
    {
        var report = new StringBuilder();
        foreach (string file in Directory.GetFiles(RoomPath, "*.prefab"))
        {
            GameObject room = PrefabUtility.LoadPrefabContents(file.Replace('\\', '/'));
            try
            {
                foreach (Light light in room.GetComponentsInChildren<Light>(true))
                    report.AppendLine("LIGHT " + room.name + "/" + light.name + " enabled="
                        + light.enabled + " type=" + light.type + " intensity=" + light.intensity
                        + " shadows=" + light.shadows);
                Transform[] walls = room.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith("Pared_", StringComparison.Ordinal)).ToArray();
                foreach (Transform window in room.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name == "Ventana"))
                {
                    Renderer glass = window.GetComponent<Renderer>();
                    if (glass == null) continue;
                    Bounds aperture = InLocalSpace(window, glass);
                    report.AppendLine(room.name + "/" + window.parent.name + " window=" + Describe(aperture));
                    foreach (var candidate in walls.Select(wall => new
                    {
                        wall,
                        bounds = wall.GetComponent<Renderer>() != null
                            ? InLocalSpace(window, wall.GetComponent<Renderer>()) : default
                    }).Where(item => item.bounds.size.x > 0
                        && Overlap(item.bounds.min.x, item.bounds.max.x, aperture.min.x, aperture.max.x)
                        && Overlap(item.bounds.min.y, item.bounds.max.y, aperture.min.y, aperture.max.y))
                        .OrderBy(item => Mathf.Abs(item.bounds.center.z)).Take(5))
                    {
                        report.AppendLine("  " + candidate.wall.name + " " + Describe(candidate.bounds)
                            + " collider=" + (candidate.wall.GetComponent<Collider>() != null)
                            + " material=" + candidate.wall.GetComponent<Renderer>().sharedMaterial.name);
                        Mesh mesh = candidate.wall.GetComponent<MeshFilter>()?.sharedMesh;
                        if (mesh != null && mesh.uv.Length > 0)
                        {
                            Vector2 lo = mesh.uv[0], hi = mesh.uv[0];
                            foreach (Vector2 uv in mesh.uv)
                            {
                                lo = Vector2.Min(lo, uv);
                                hi = Vector2.Max(hi, uv);
                            }
                            report.AppendLine("    uv=" + lo.ToString("F3") + ".." + hi.ToString("F3"));
                        }
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }
        string path = Path.Combine(Path.GetTempPath(), "TLBD_WindowOpeningInspection.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log("Informe de muros: " + path);
    }

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar aberturas en escena")]
    private static void InspectScene()
    {
        var report = new StringBuilder();
        foreach (RoomModule room in UnityEngine.Object.FindObjectsByType<RoomModule>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform[] walls = room.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("Pared_", StringComparison.Ordinal)
                    && item.GetComponent<BoxCollider>() != null).ToArray();
            foreach (Transform window in room.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "Ventana"))
            {
                Transform wall = FindWall(window, walls);
                report.AppendLine(room.RoomId + "/" + window.parent.name + " actual="
                    + (wall != null ? wall.name : "NONE") + " window=" + window.position.ToString("F3")
                    + " cut=" + string.Join(",", walls.Where(item => item.Find(CutWallName) != null)
                        .Select(item => item.name)));
            }
        }
        string path = Path.Combine(Path.GetTempPath(), "TLBD_WindowSceneInspection.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log("Informe de aberturas en escena: " + path);
    }

    [MenuItem("The Lights Before Dawn/Arte/Ajustar aberturas desplazadas en escena")]
    private static void FixSceneOverrides()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
                != "Assets/Scenes/ModularPrototype.unity") return;
        var report = new StringBuilder();
        int corrected = 0;
        foreach (RoomModule room in UnityEngine.Object.FindObjectsByType<RoomModule>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform[] walls = room.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("Pared_", StringComparison.Ordinal)
                    && item.GetComponent<BoxCollider>() != null).ToArray();
            Transform[] windows = room.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "Ventana").ToArray();
            var actual = windows.ToDictionary(window => window, window => FindWall(window, walls));
            if (actual.Values.Any(wall => wall == null))
                throw new InvalidOperationException("Falta un muro de ventana en " + room.RoomId);
            HashSet<Transform> needed = new HashSet<Transform>(actual.Values);
            foreach (Transform wall in walls.Where(item => item.Find(CutWallName) != null
                && !needed.Contains(item)))
            {
                Renderer renderer = wall.GetComponent<Renderer>();
                BoxCollider collider = wall.GetComponent<BoxCollider>();
                Transform cut = wall.Find(CutWallName);
                Undo.RecordObject(renderer, "Cerrar hueco desplazado");
                Undo.RecordObject(collider, "Cerrar hueco desplazado");
                Undo.RecordObject(cut.gameObject, "Cerrar hueco desplazado");
                renderer.enabled = true;
                collider.enabled = true;
                cut.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cut.gameObject);
                report.AppendLine(room.RoomId + ": muro original restaurado " + wall.name);
            }
            foreach (var pair in actual)
            {
                if (pair.Value.Find(CutWallName) != null) continue;
                CutWall(room.RoomId, pair.Value, new List<Transform> { pair.Key }, report);
                corrected++;
            }
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        string path = Path.Combine(Path.GetTempPath(), "TLBD_WindowSceneFix.txt");
        File.WriteAllText(path, "Correcciones=" + corrected + "\n" + report);
        Debug.Log("Ventanas de escena corregidas=" + corrected + ". Informe: " + path);
    }

    private static bool Overlap(float a0, float a1, float b0, float b1) => a0 < b1 && a1 > b0;

    private static Bounds InLocalSpace(Transform reference, Renderer renderer)
    {
        Matrix4x4 matrix = reference.worldToLocalMatrix * renderer.localToWorldMatrix;
        Bounds source = renderer.localBounds;
        Bounds result = default;
        bool first = true;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 point = matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                new Vector3(x, y, z)));
            if (first) { result = new Bounds(point, Vector3.zero); first = false; }
            else result.Encapsulate(point);
        }
        return result;
    }

    private static string Describe(Bounds bounds) => "min=" + bounds.min.ToString("F3")
        + " max=" + bounds.max.ToString("F3") + " size=" + bounds.size.ToString("F3");
}
