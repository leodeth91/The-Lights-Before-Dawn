using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>Conecta los clips de Mixamo y ajusta el tamaño agachado del niño.</summary>
[InitializeOnLoad]
public static class ChildAnimationSetSetup
{
    private const string Folder = "Assets/Animations/Child/";
    private const string ControllerPath = Folder + "ChildCrouch.controller";
    private const string RequestPath = "Temp/ApplyChildAnimationSet.request";
    private const string ResultPath = "Temp/ChildAnimationSetResult.txt";
    private static readonly (string file, string state, bool repeats, bool grounded)[] Clips =
    {
        ("WalkingNew.fbx", "Walk", true, true),
        ("UnarmedIdleLookingV2.fbx", "Idle", true, true),
        ("RunningNew.fbx", "Run", true, true),
        ("CrouchingIdleNew.fbx", "CrouchIdle", true, true),
        ("SneakWalkNew.fbx", "CrouchWalk", true, true),
        ("SleepingIdle.fbx", "Prone", true, false),
        ("JumpNew.fbx", "Jump", false, false)
    };

    static ChildAnimationSetSetup() => EditorApplication.update += RunIfRequested;

    private static void RunIfRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!File.Exists(RequestPath))
        {
            EditorApplication.update -= RunIfRequested;
            return;
        }
        // Una petición recibida durante Play espera hasta volver al modo de edición.
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= RunIfRequested;
        File.Delete(RequestPath);
        try { Apply(); }
        catch (Exception error)
        {
            File.WriteAllText(ResultPath, "ERROR\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("The Lights Before Dawn/Personajes/Conectar animaciones del niño")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Salí de Play antes de importar las animaciones.");
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Falta el Animator Controller actual del niño.");

        AnimationClip[] imported = new AnimationClip[Clips.Length];
        for (int i = 0; i < Clips.Length; i++)
        {
            string path = Folder + Clips[i].file;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No se importó " + path);
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ModelImporterClipAnimation[] definitions = importer.clipAnimations;
            if (definitions == null || definitions.Length == 0) definitions = importer.defaultClipAnimations;
            if (definitions == null || definitions.Length == 0)
                throw new InvalidOperationException("El FBX no contiene animación: " + path);
            foreach (ModelImporterClipAnimation definition in definitions)
            {
                definition.loopTime = Clips[i].repeats;
                definition.loopPose = Clips[i].repeats;
                definition.keepOriginalPositionXZ = false;
                if (Clips[i].grounded)
                {
                    // La altura del cuerpo se hornea tomando los pies como referencia.
                    definition.keepOriginalPositionY = false;
                    definition.heightFromFeet = true;
                }
            }
            importer.clipAnimations = definitions;
            importer.SaveAndReimport();
            imported[i] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__") && clip.humanMotion);
            if (imported[i] == null)
                throw new InvalidOperationException("Unity no reconoció como Humanoid: " + path);
            Avatar sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (sourceAvatar == null || !sourceAvatar.isHuman || !sourceAvatar.isValid)
                throw new InvalidOperationException("El esqueleto Humanoid no es válido: " + path);
        }

        // Se guarda una copia justo antes de tocar el controlador existente.
        string backupFolder = "Backups/ChildAnimationSet-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backupFolder);
        File.Copy(ControllerPath, backupFolder + "/ChildCrouch.controller");
        File.Copy(ControllerPath + ".meta", backupFolder + "/ChildCrouch.controller.meta");

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState existingWalk = machine.states.Select(item => item.state)
            .FirstOrDefault(state => state != null && state.motion == imported[0]);
        if (existingWalk != null) existingWalk.name = "Walk";
        AnimatorState idle = null;
        for (int i = 0; i < Clips.Length; i++)
        {
            string stateName = Clips[i].state;
            AnimatorState state = machine.states.Select(item => item.state)
                .FirstOrDefault(item => item != null && item.name == stateName);
            if (state == null) state = machine.AddState(stateName);
            state.motion = imported[i];
            state.speed = 1f;
            state.iKOnFeet = stateName == "Walk" || stateName == "Run" || stateName == "CrouchWalk";
            EditorUtility.SetDirty(state);
            if (stateName == "Idle") idle = state;
        }
        machine.defaultState = idle;
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // Sneak Walk mide 1,024 unidades de alto en el modelo original. Con la
        // escala 0,8 de la escena ocupa unos 0,82 m; 0,74 * 1,15 = 0,85 m deja
        // un pequeño margen. No se achica el modelo: la postura hace el trabajo.
        // Cada escondite sigue aplicando su cápsula especial por separado.
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/ModularPrototype.unity")
            throw new InvalidOperationException("Abrí ModularPrototype para ajustar la postura agachada.");
        ChildPlayerController player = UnityEngine.Object.FindFirstObjectByType<ChildPlayerController>();
        if (player == null || player.gameObject.scene != scene)
            throw new InvalidOperationException("No se encontró el Player en la escena abierta.");
        var serializedPlayer = new SerializedObject(player);
        SerializedProperty heightRatio = serializedPlayer.FindProperty("crouchedTotalHeightRatio");
        SerializedProperty visualScale = serializedPlayer.FindProperty("animatedCrouchVisualScale");
        if (Mathf.Approximately(heightRatio.floatValue, 0.62f)
            && Mathf.Approximately(visualScale.floatValue, 0.85f))
        {
            File.Copy(scene.path, backupFolder + "/ModularPrototype.unity");
            heightRatio.floatValue = 0.74f;
            visualScale.floatValue = 1f;
            serializedPlayer.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        string report = "OK\nBackup=" + backupFolder + "\nController=" + ControllerPath;
        report += "\nCrouchHeightRatio=" + heightRatio.floatValue
            + "\nCrouchVisualScale=" + visualScale.floatValue;
        for (int i = 0; i < Clips.Length; i++)
            report += "\n" + Clips[i].state + "=" + Clips[i].file
                + ", seconds=" + imported[i].length.ToString("0.###")
                + ", humanoid=" + imported[i].humanMotion
                + ", loop=" + imported[i].isLooping;
        try { report += MeasureChildPoses(imported); }
        catch (Exception error) { report += "\nPoseCheckError=" + error.Message; }
        File.WriteAllText(ResultPath, report + "\n");
        Debug.Log("Animaciones del niño conectadas. Resultado en " + ResultPath);
    }

    private static string MeasureChildPoses(AnimationClip[] clips)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Characters/Child/kid-character-fully-rigged.glb");
        Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(Folder + "ChildHumanoidAvatar.asset");
        if (asset == null || avatar == null || !avatar.isValid)
            throw new InvalidOperationException("No se pudo medir el modelo o su Avatar.");

        string result = "";
        for (int i = 0; i < clips.Length; i++)
        {
            GameObject copy = UnityEngine.Object.Instantiate(asset);
            copy.hideFlags = HideFlags.HideAndDontSave;
            PlayableGraph graph = default;
            try
            {
                Animator animator = copy.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("Falta el Animator del niño.");
                animator.avatar = avatar;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.enabled = true;
                graph = PlayableGraph.Create("Revisar pose " + Clips[i].state);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clips[i]);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Niño", animator);
                output.SetSourcePlayable(playable);
                graph.Play();

                float lowest = float.PositiveInfinity;
                float highest = float.NegativeInfinity;
                for (int sample = 0; sample < 20; sample++)
                {
                    playable.SetTime(clips[i].length * sample / 20f);
                    graph.Evaluate(0f);
                    Bounds bounds = ChildGlbPreview.Measure(copy, copy.transform);
                    lowest = Mathf.Min(lowest, bounds.min.y);
                    highest = Mathf.Max(highest, bounds.max.y);
                }
                result += "\nPose " + Clips[i].state + ": minY=" + lowest.ToString("0.###")
                    + ", maxY=" + highest.ToString("0.###");
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }
        return result;
    }
}
