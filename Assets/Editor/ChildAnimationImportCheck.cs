using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Comprueba la caminata de Mixamo y el esqueleto del niño sin modificar la escena.</summary>
[InitializeOnLoad]
public static class ChildAnimationImportCheck
{
    private const string AnimationPath = "Assets/Animations/Child/CrouchedWalking.fbx";
    private const string ModelPath = "Assets/Characters/Child/kid-character-fully-rigged.glb";
    private const string RequestPath = "Temp/InspectChildAnimation.request";

    static ChildAnimationImportCheck() => EditorApplication.update += RunIfRequested;

    private static void RunIfRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RunIfRequested;
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        try { Inspect(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/ChildAnimationImportResult.txt", "ERROR\n" + error);
            Debug.LogException(error);
        }
    }


    [MenuItem("The Lights Before Dawn/Personajes/Revisar animación del niño")]
    public static void Inspect()
    {
        var importer = AssetImporter.GetAtPath(AnimationPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("El FBX de Mixamo todavía no fue importado.");
        // El clip debe describir un cuerpo humano para poder adaptarlo a otro esqueleto.
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(AnimationPath)
            .OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__")).ToArray();
        Avatar clipAvatar = AssetDatabase.LoadAllAssetsAtPath(AnimationPath).OfType<Avatar>().FirstOrDefault();
        GameObject childAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (childAsset == null) throw new InvalidOperationException("No se encontró el GLB del niño.");
        string report = "FBX=" + AnimationPath + "\nImportType=" + importer.animationType
            + "\nClipAvatar=" + (clipAvatar != null ? clipAvatar.name + ", human=" + clipAvatar.isHuman + ", valid=" + clipAvatar.isValid : "ninguno")
            + "\nClipCount=" + clips.Length;
        foreach (AnimationClip clip in clips)
            report += "\nClip=" + clip.name + ", length=" + clip.length + ", humanMotion=" + clip.humanMotion + ", legacy=" + clip.legacy;
        Animator[] childAnimators = childAsset.GetComponentsInChildren<Animator>(true);
        report += "\nChildAnimators=" + childAnimators.Length;
        foreach (Animator animator in childAnimators)
            report += "\nChildAnimator=" + animator.name + ", avatar="
                + (animator.avatar != null ? animator.avatar.name + ", human=" + animator.avatar.isHuman + ", valid=" + animator.avatar.isValid : "ninguno");
        File.WriteAllText("Temp/ChildAnimationImportResult.txt", report + "\n");
        Debug.Log("Revisión de animación terminada; resultado en Temp/ChildAnimationImportResult.txt");
    }
}
