using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Conecta una sola animación probada; respalda la escena y conserva los otros objetos.</summary>
[InitializeOnLoad]
public static class ChildCrouchAnimationSetup
{
    private const string RequestPath = "Temp/ApplyChildCrouchAnimation.request";
    private const string ResultPath = "Temp/ChildCrouchAnimationSetupResult.txt";
    private const string AvatarPath = "Assets/Animations/Child/ChildHumanoidAvatar.asset";
    private const string ControllerPath = "Assets/Animations/Child/ChildCrouch.controller";
    private const string ClipPath = "Assets/Animations/Child/CrouchedWalking.fbx";

    static ChildCrouchAnimationSetup() => EditorApplication.update += RunIfRequested;

    private static void RunIfRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RunIfRequested;
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        try { Apply(); }
        catch (Exception error)
        {
            File.WriteAllText(ResultPath, "ERROR\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("The Lights Before Dawn/Personajes/Aplicar caminata agachada")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/ModularPrototype.unity")
            throw new InvalidOperationException("Abrí ModularPrototype fuera de Play.");
        ChildPlayerController[] players = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ChildPlayerController>(true)).ToArray();
        if (players.Length != 1) throw new InvalidOperationException("Se esperaba un solo Player.");
        ChildPlayerController player = players[0];
        Transform modelRoot = player.transform.Find("PlayerVisual/AparienciaNiño/ModeloNiño");
        if (modelRoot == null) throw new InvalidOperationException("No se encontró el modelo actual del niño.");
        Animator animator = modelRoot.GetComponent<Animator>();
        if (animator == null) throw new InvalidOperationException("Falta el Animator del GLB.");
        if (player.GetComponent<ChildCrouchAnimation>() != null)
            throw new InvalidOperationException("La caminata agachada ya fue conectada.");
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.humanMotion);
        if (clip == null) throw new InvalidOperationException("Falta el clip Humanoid importado.");
        if (File.Exists(AvatarPath) || File.Exists(ControllerPath))
            throw new InvalidOperationException("Ya existen los assets de destino; se evitará sobrescribirlos.");

        // Guardamos primero cualquier movimiento manual hecho en Unity y luego su copia fuera de Assets.
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar la escena actual.");
        string backupFolder = "Backups/ChildCrouchAnimation-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backupFolder);
        string backupScene = backupFolder + "/ModularPrototype.unity";
        File.Copy(scene.path, backupScene);
        if (File.Exists(scene.path + ".meta")) File.Copy(scene.path + ".meta", backupScene + ".meta");

        GameObject avatarSource = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Characters/Child/kid-character-fully-rigged.glb");
        GameObject temporary = UnityEngine.Object.Instantiate(avatarSource);
        temporary.hideFlags = HideFlags.HideAndDontSave;
        Avatar avatar;
        try { avatar = ChildAvatarProbe.BuildAvatar(temporary); }
        finally { UnityEngine.Object.DestroyImmediate(temporary); }
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("El Avatar del niño no resultó válido.");
        avatar.name = "AvatarNiño";
        AssetDatabase.CreateAsset(avatar, AvatarPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorState state = controller.layers[0].stateMachine.AddState("CaminataAgachada");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Añadir caminata agachada al niño");
        try
        {
            Undo.RecordObject(animator, "Configurar animación del niño");
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            ChildCrouchAnimation driver = Undo.AddComponent<ChildCrouchAnimation>(player.gameObject);
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("modelRoot").objectReferenceValue = modelRoot;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("No se pudo guardar la caminata en la escena.");
            Undo.CollapseUndoOperations(group);
            File.WriteAllText(ResultPath, "OK\nBackup=" + backupScene + "\nClip=" + clip.name
                + "\nAvatar=" + AvatarPath + "\nController=" + ControllerPath + "\n");
            Debug.Log("Caminata agachada integrada. Respaldo: " + backupScene);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }
}
