using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Comprueba el modelo nuevo sin cambiar el Player ni guardar cambios en la escena.</summary>
[InitializeOnLoad]
public static class ChildGlbPreview
{
    private const string ModelPath = "Assets/Characters/Child/kid-character-fully-rigged.glb";
    static ChildGlbPreview() => EditorApplication.update += CheckRequest;

    private static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= CheckRequest;
        const string request = "Temp/PreviewChildGlb.request";
        if (!File.Exists(request)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Preview(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/ChildGlbPreviewResult.txt", "ERROR\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("The Lights Before Dawn/Personajes/Comprobar modelo GLB del niño")]
    public static void Preview()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (asset == null) throw new InvalidOperationException("El GLB todavía no fue importado. Revisá el Package Manager y la consola.");
        RenderModel(asset, "Temp/ChildGlbPreview.png");
    }

    public static void RenderModel(GameObject asset, string imagePath)
    {
        var preview = new PreviewRenderUtility();
        GameObject model = UnityEngine.Object.Instantiate(asset);
        model.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            Bounds bounds = Measure(model);
            int rendererCount = model.GetComponentsInChildren<Renderer>(true).Length;
            preview.AddSingleGO(model);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
            preview.camera.fieldOfView = 30;
            preview.camera.transform.position = bounds.center + new Vector3(0.35f, 0.12f, 1).normalized * radius * 4.5f;
            preview.camera.transform.LookAt(bounds.center);
            preview.camera.nearClipPlane = radius * 0.01f;
            preview.camera.farClipPlane = radius * 20;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.2f, 0.23f, 0.26f);
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
            preview.lights[1].intensity = 1;
            preview.BeginPreview(new Rect(0, 0, 640, 640), GUIStyle.none);
            preview.Render(true);
            Texture image = preview.EndPreview();
            RenderTexture target = RenderTexture.GetTemporary(640, 640, 0);
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            try
            {
                Graphics.Blit(image, target);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(imagePath, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
            File.WriteAllText("Temp/ChildGlbPreviewResult.txt", $"OK\nRenderers={rendererCount}\nBounds={bounds}\nScale={model.transform.localScale}\n");
            Debug.Log("Vista previa GLB terminada. El Player y la escena no se modificaron.");
        }
        finally
        {
            preview.Cleanup();
            if (model != null) UnityEngine.Object.DestroyImmediate(model);
        }
    }

    public static Bounds Measure(GameObject model, Transform reference = null)
    {
        bool first = true;
        Bounds bounds = default;
        foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            // Usamos los huesos para medir el cuerpo real. La escala interna del GLB se aplica una sola vez.
            Mesh mesh = skin.sharedMesh;
            if (mesh == null) continue;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var poses = mesh.bindposes;
            var bones = skin.bones;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = skin.transform.TransformPoint(vertices[i]);
                if (weights.Length == vertices.Length && poses.Length > 0)
                {
                    BoneWeight weight = weights[i];
                    point = WeightedPoint(weight.boneIndex0, weight.weight0, vertices[i], bones, poses)
                        + WeightedPoint(weight.boneIndex1, weight.weight1, vertices[i], bones, poses)
                        + WeightedPoint(weight.boneIndex2, weight.weight2, vertices[i], bones, poses)
                        + WeightedPoint(weight.boneIndex3, weight.weight3, vertices[i], bones, poses);
                }
                if (reference != null) point = reference.InverseTransformPoint(point);
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        if (first) throw new InvalidOperationException("El modelo no contiene un cuerpo con esqueleto.");
        return bounds;
    }

    private static Vector3 WeightedPoint(int index, float weight, Vector3 vertex, Transform[] bones, Matrix4x4[] poses)
    {
        if (weight <= 0 || index >= bones.Length || index >= poses.Length || bones[index] == null) return Vector3.zero;
        return bones[index].TransformPoint(poses[index].MultiplyPoint3x4(vertex)) * weight;
    }
}
