using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class ChestVisualPlacement
{
    private const string SourcePath = "Assets/Props/chest__box__wooden_26_mb/chest__box__wooden_26_mb.glb";
    private const string RoomPath = "Assets/Generated/ModularPrototype/Rooms/Room_living_dining_room.prefab";
    private const string SpotName = "ESCONDITE_L05_BaulSala";

    [MenuItem("The Lights Before Dawn/Arte/Inspeccionar baul importado")]
    private static void Inspect()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (source == null) { Debug.LogError("No se importo el baul: " + SourcePath); return; }
        GameObject room = PrefabUtility.LoadPrefabContents(RoomPath);
        try
        {
            Transform spot = Find(room.transform, SpotName);
            if (spot == null) { Debug.LogError("No se encontro " + SpotName); return; }
            GameObject model = UnityEngine.Object.Instantiate(source);
            try
            {
                AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
                    .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                var report = new StringBuilder();
                report.AppendLine("Visual actual: " + Describe(GetBounds(spot, spot.GetComponentsInChildren<Renderer>(true))));
                report.AppendLine("Asset importado: " + Describe(GetBounds(model.transform, model.GetComponentsInChildren<Renderer>(true))));
                report.AppendLine("Animators: " + model.GetComponentsInChildren<Animator>(true).Length);
                foreach (AnimationClip clip in clips)
                {
                    report.AppendLine("Clip " + clip.name + " longitud " + clip.length.ToString("F3"));
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                        report.AppendLine("  " + binding.path + " / " + binding.propertyName);
                }
                Debug.Log(report.ToString());
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
        finally { PrefabUtility.UnloadPrefabContents(room); }
    }

    [MenuItem("The Lights Before Dawn/Arte/Integrar baul animado en sala")]
    private static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Salir de Play antes de editar el prefab."); return; }
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (source == null || clip == null) { Debug.LogError("Falta el modelo o su animacion importada."); return; }
        GameObject room = PrefabUtility.LoadPrefabContents(RoomPath);
        bool changed = false;
        try
        {
            Transform spot = Find(room.transform, SpotName);
            if (spot == null) { Debug.LogError("No se encontro " + SpotName); return; }
            if (spot.Find("Visual_BaulMadera") != null) { Debug.LogWarning("El baul importado ya esta colocado."); return; }
            Renderer[] oldRenderers = spot.GetComponentsInChildren<Renderer>(true);
            Bounds target = GetBounds(spot, oldRenderers);
            if (target.size.sqrMagnitude < 0.01f) { Debug.LogError("No hay visual ProBuilder para medir."); return; }

            var visual = new GameObject("Visual_BaulMadera");
            visual.transform.SetParent(spot, false);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, room.scene);
            model.name = "chest__box__wooden_26_mb";
            model.transform.SetParent(visual.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Animation animation in model.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
            clip.SampleAnimation(model, 2.2f);

            Bounds imported = GetBounds(visual.transform, model.GetComponentsInChildren<Renderer>(true));
            if (imported.size.x < 0.001f || imported.size.y < 0.001f || imported.size.z < 0.001f)
            { Debug.LogError("El asset importado no tiene limites validos."); return; }
            visual.transform.localScale = new Vector3(target.size.x / imported.size.x,
                target.size.y / imported.size.y, target.size.z / imported.size.z);
            Bounds fitted = GetBounds(spot, model.GetComponentsInChildren<Renderer>(true));
            visual.transform.localPosition = target.center - fitted.center;

            ChestVisualAnimation chestAnimation = visual.AddComponent<ChestVisualAnimation>();
            chestAnimation.Configure(model, clip, 2.2f, clip.length);
            HingedHideFurniture hinge = spot.GetComponent<HingedHideFurniture>();
            if (hinge == null) { Debug.LogError("Falta HingedHideFurniture."); return; }
            chestAnimation.SetHinge(hinge);
            foreach (Renderer renderer in oldRenderers) renderer.enabled = false;
            changed = true;
            PrefabUtility.SaveAsPrefabAsset(room, RoomPath);
            Debug.Log("Baul importado integrado. ProBuilder queda como collider; visual y animacion siguen a HingedHideFurniture. Escala: "
                + visual.transform.localScale + ", posicion: " + visual.transform.localPosition);
        }
        finally { PrefabUtility.UnloadPrefabContents(room); }
        if (changed) AssetDatabase.SaveAssets();
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static Bounds GetBounds(Transform root, Renderer[] renderers)
    {
        Bounds result = new Bounds();
        bool set = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = root.InverseTransformPoint(world.center + Vector3.Scale(world.extents, new Vector3(x, y, z)));
                if (!set) { result = new Bounds(point, Vector3.zero); set = true; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }

    private static string Describe(Bounds bounds) => "centro=" + bounds.center + " tamano=" + bounds.size;
}
