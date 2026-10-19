using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Integra el aspecto del niño sin sustituir sus controles. Guarda antes una copia de la escena actual.</summary>
[InitializeOnLoad]
public static class ChildModelSetup
{
    private const string ModelPath = "Assets/Characters/Child/kid-character-fully-rigged.glb";
    private const string RequestPath = "Temp/IntegrateChildModel.request";

    static ChildModelSetup() => EditorApplication.update += RunRequestedIntegration;

    private static void RunRequestedIntegration()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RunRequestedIntegration;
        if (!File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        try { Integrate(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/ChildModelIntegrationResult.txt", "ERROR\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("The Lights Before Dawn/Personajes/Integrar modelo del niño")]
    public static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Salí de Play antes de integrar el modelo del niño.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/ModularPrototype.unity")
            throw new InvalidOperationException("Abrí la escena ModularPrototype, fuera del modo de edición de prefabs.");
        ChildPlayerController[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ChildPlayerController>(true)).ToArray();
        if (players.Length != 1) throw new InvalidOperationException("Se esperaba un único Player en la escena.");
        ChildPlayerController player = players[0];
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null) throw new InvalidOperationException("No se encontró el GLB importado con glTFast.");
        var hiding = new SerializedObject(player.GetComponent<PlayerHidingController>());
        Transform visualRoot = hiding.FindProperty("visualRoot").objectReferenceValue as Transform;
        if (visualRoot == null) throw new InvalidOperationException("Falta la raíz visual del escondite.");
        Transform previousModel = visualRoot.Find("AparienciaNiño");

        // Guarda los cambios manuales antes de copiar. El respaldo queda fuera de Assets
        // para que Unity no lo trate como otra escena del juego.
        string backup = "Backups/ChildModel-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        string backupScene = backup + "/ModularPrototype.unity";
        if (!EditorSceneManager.SaveScene(scene))
            throw new IOException("No se pudo guardar la copia de seguridad. No se modificó el Player.");
        File.Copy(scene.path, backupScene);
        if (File.Exists(scene.path + ".meta")) File.Copy(scene.path + ".meta", backupScene + ".meta");
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Integrar apariencia del niño");
        try
        {
            GameObject container = new GameObject("AparienciaNiño");
            Undo.RegisterCreatedObjectUndo(container, "Crear contenedor visual");
            container.transform.SetParent(visualRoot, false);
            GameObject model = PrefabUtility.InstantiatePrefab(modelAsset, scene) as GameObject;
            if (model == null) throw new InvalidOperationException("No se pudo instanciar el modelo.");
            Undo.RegisterCreatedObjectUndo(model, "Añadir modelo del niño");
            model.transform.SetParent(container.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.name = "ModeloNiño";
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = player.gameObject.layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.enabled = false; // Las animaciones se configurarán en una etapa posterior.
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            foreach (Animation animation in model.GetComponentsInChildren<Animation>(true))
            {
                animation.playAutomatically = false;
                animation.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(animation);
            }
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("El modelo no tiene superficies visibles.");
            Bounds bounds = ChildGlbPreview.Measure(model, container.transform);
            float height = bounds.size.y;
            if (height < .001f || float.IsNaN(height)) throw new InvalidOperationException("La altura del modelo no es válida.");
            float scale = player.standingHeight / height;
            model.transform.localScale *= scale;
            // Ajustamos la altura al collider y dejamos los pies en el origen del Player.
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            // El GLB ya trae sus materiales y texturas. Se conservan las referencias del importador.
            Bounds fitted = ChildGlbPreview.Measure(model, container.transform);
            if (Mathf.Abs(fitted.size.y - player.standingHeight) > .005f || Mathf.Abs(fitted.min.y) > .005f)
                throw new InvalidOperationException("El ajuste de altura o de pies no superó la comprobación.");
            if (model.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("El modelo visual contiene colliders que podrían interferir con el Player.");
            ChildGlbPreview.RenderModel(container, "Temp/ChildModelIntegrated.png");
            // Retiramos solo el aspecto anterior después de comprobar el nuevo. Undo y el respaldo permiten recuperarlo.
            if (previousModel != null) Undo.DestroyObjectImmediate(previousModel.gameObject);

            // Conserva las figuras, referencias y materiales anteriores; solo oculta sus renderers.
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(container.transform))
                {
                    Undo.RecordObject(renderer, "Ocultar figura del grey box");
                    renderer.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            var movement = new SerializedObject(player);
            movement.FindProperty("visualModel").objectReferenceValue = container.transform;
            movement.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar la escena integrada.");
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = container;
            File.WriteAllText("Temp/ChildModelIntegrationResult.txt", "OK\nBackup=" + backupScene
                + "\nModel=" + ModelPath + "\nRenderers=" + renderers.Length + "\nScale=" + scale
                + "\nHeight=" + fitted.size.y + "\nFeet=" + fitted.min.y);
            Debug.Log("Modelo del niño integrado. Copia de la distribución anterior: " + backupScene);
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

}
