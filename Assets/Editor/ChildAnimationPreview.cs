using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Prueba la animación en una copia del niño para detectar deformaciones antes de integrarla.</summary>
[InitializeOnLoad]
public static class ChildAnimationPreview
{
    private const string RequestPath = "Temp/PreviewChildAnimation.request";
    static ChildAnimationPreview() => EditorApplication.update += RunIfRequested;

    private static void RunIfRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RunIfRequested;
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        try { Preview(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/ChildAnimationPreviewResult.txt", "ERROR\n" + error);
            Debug.LogException(error);
        }
    }


    [MenuItem("The Lights Before Dawn/Personajes/Probar caminata agachada")]
    public static void Preview()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Child/kid-character-fully-rigged.glb");
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/Child/CrouchedWalking.fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (asset == null || clip == null || !clip.humanMotion)
            throw new InvalidOperationException("Falta el niño o el clip Humanoid.");
        GameObject copy = UnityEngine.Object.Instantiate(asset);
        copy.hideFlags = HideFlags.HideAndDontSave;
        Avatar avatar = null;
        PlayableGraph graph = default;
        try
        {
            Animator animator = copy.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException("El GLB no tiene Animator.");
            avatar = ChildAvatarProbe.BuildAvatar(copy);
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
                throw new InvalidOperationException("El Avatar Humanoid no es válido.");
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.enabled = true;
            Bounds before = ChildGlbPreview.Measure(copy);
            Transform[] bones = copy.GetComponentsInChildren<Transform>(true);
            Quaternion[] rotations = bones.Select(b => b.localRotation).ToArray();
            graph = PlayableGraph.Create("PruebaCaminataAgachada");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Niño", animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            playable.SetTime(clip.length * .5f);
            graph.Evaluate(0);
            Bounds after = ChildGlbPreview.Measure(copy);
            int moved = bones.Where((bone, i) => Quaternion.Angle(rotations[i], bone.localRotation) > .5f).Count();
            ChildGlbPreview.RenderModel(copy, "Temp/ChildAnimationPreview.png");
            File.WriteAllText("Temp/ChildAnimationPreviewResult.txt",
                "OK\nClip=" + clip.name + "\nChangedBones=" + moved
                + "\nBefore=" + before + "\nAfter=" + after + "\n");
            Debug.Log("Prueba de caminata terminada; resultado en Temp/ChildAnimationPreviewResult.txt");
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }
}
