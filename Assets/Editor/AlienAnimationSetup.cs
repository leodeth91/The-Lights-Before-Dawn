using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Prepara los FBX y modifica solo el prefab Alien. Guarda una copia antes de reemplazar su apariencia.</summary>
[InitializeOnLoad]
public static class AlienAnimationSetup
{
    private const string Folder = "Assets/Animations/Alien/";
    private const string PrefabPath = "Assets/Aliens/Alien.prefab";
    private const string Request = "Temp/ApplyAlienAnimations.request";
    private const string Version = "fit-final-v2";
    private static readonly string[] Files = { "ZombieIdle", "LookingAround", "Walking", "ZombieRunning", "ZombieScream", "NervouslyLookAround" };
    private static readonly string[] States = { "Idle", "Observe", "Walk", "Run", "Alert", "Nervous" };

    static AlienAnimationSetup() => EditorApplication.update += CheckRequest;

    private static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.ReadAllText(Request).Trim() != Version) return;
        File.Delete(Request);
        try { Apply(); }
        catch (Exception error)
        {
            File.WriteAllText("Temp/AlienAnimationResult.txt", "ERROR\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("The Lights Before Dawn/Personajes/Conectar modelo y animaciones de aliens")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Salí de Play antes de integrar el modelo.");
        string backup = "Backups/AlienAnimations-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(PrefabPath, backup + "/Alien.prefab");
        File.Copy(PrefabPath + ".meta", backup + "/Alien.prefab.meta");
        var report = new StringBuilder("Backup=" + backup + "\n");
        var clips = new AnimationClip[Files.Length];
        GameObject modelAsset = null;
        for (int i = 0; i < Files.Length; i++)
        {
            string path = Folder + Files[i] + ".fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Falta " + path);
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            ModelImporterClipAnimation[] definitions = importer.defaultClipAnimations;
            if (definitions.Length == 0) throw new InvalidOperationException("El FBX no tiene clip: " + path);
            foreach (var definition in definitions)
            {
                definition.loopTime = i != 4;
                definition.loopPose = i != 4;
                definition.lockRootRotation = true;
                definition.keepOriginalOrientation = true;
                definition.lockRootHeightY = true;
                definition.heightFromFeet = true;
                // Caminar y correr extraen el avance de la animación, pero el Animator no lo aplica.
                // Así el modelo no se aleja del collider ni duplica el movimiento del NavMeshAgent.
                definition.lockRootPositionXZ = i != 2 && i != 3;
            }
            importer.clipAnimations = definitions;
            importer.SaveAndReimport();
            clips[i] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__") && clip.humanMotion);
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Animator importedAnimator = imported != null ? imported.GetComponent<Animator>() : null;
            if (clips[i] == null || importedAnimator == null || importedAnimator.avatar == null
                || !importedAnimator.avatar.isValid || !importedAnimator.avatar.isHuman)
                throw new InvalidOperationException("Avatar o clip Humanoid no válido: " + path);
            if (modelAsset == null && imported.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                modelAsset = imported;
            report.AppendLine(States[i] + ": " + clips[i].length.ToString("0.##") + "s, loop=" + clips[i].isLooping);
        }
        if (modelAsset == null)
            throw new InvalidOperationException("Los seis FBX son Without Skin: falta descargar uno With Skin para obtener el cuerpo del alien.");

        // Extrae materiales y texturas del modelo para poder editarlos desde Unity.
        string modelPath = AssetDatabase.GetAssetPath(modelAsset);
        PrepareMaterials(modelPath);
        modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        string controllerPath = Folder + "Alien.controller";
        AnimatorController animation = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (animation == null) animation = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        else File.Copy(controllerPath, backup + "/Alien.controller");
        if (!animation.parameters.Any(p => p.name == "MotionSpeed")) animation.AddParameter("MotionSpeed", AnimatorControllerParameterType.Float);
        var layers = animation.layers;
        layers[0].iKPass = true;
        animation.layers = layers;
        for (int i = 0; i < States.Length; i++)
        {
            AnimatorState state = GetState(animation.layers[0].stateMachine, States[i]);
            state.motion = clips[i];
            state.speedParameter = "MotionSpeed";
            state.speedParameterActive = i == 2 || i == 3;
            if (i == 0) animation.layers[0].stateMachine.defaultState = state;
        }
        if (animation.layers.Length < 2) animation.AddLayer("Gestures");
        string maskPath = Folder + "UpperBody.mask";
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, maskPath); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);
        layers = animation.layers;
        layers[1].avatarMask = mask;
        layers[1].defaultWeight = 0;
        layers[1].iKPass = true;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        animation.layers = layers;
        GetState(animation.layers[1].stateMachine, "Alert").motion = clips[4];
        GetState(animation.layers[1].stateMachine, "Nervous").motion = clips[5];
        EditorUtility.SetDirty(mask);
        EditorUtility.SetDirty(animation);
        AssetDatabase.SaveAssets();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            AlienController owner = root.GetComponent<AlienController>();
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            if (owner == null || capsule == null) throw new InvalidOperationException("Faltan componentes en el prefab Alien.");
            Transform old = root.transform.Find("AparienciaAlien");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var visual = new GameObject("AparienciaAlien");
            visual.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, visual.transform);
            model.name = "ModeloAlien";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            Animator animator = model.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = animation;
            Bounds bounds = Measure(model, root.transform);
            if (bounds.size.y < .01f) throw new InvalidOperationException("El modelo tiene altura inválida.");
            visual.transform.localScale = Vector3.one * (capsule.height / bounds.size.y);
            bounds = Measure(model, root.transform);
            visual.transform.localPosition += new Vector3(capsule.center.x - bounds.center.x,
                capsule.center.y - capsule.height * .5f - bounds.min.y, capsule.center.z - bounds.center.z);
            Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (bone == null) throw new InvalidOperationException("El Avatar no tiene cabeza.");
            root.GetComponent<AlienHeadScanner>().ConfigureAnimatedHead(bone, Quaternion.Inverse(bone.rotation) * root.transform.rotation);
            foreach (Collider extra in model.GetComponentsInChildren<Collider>(true)) extra.enabled = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(visual.transform)) renderer.enabled = false;
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            AlienAnimationController bridge = model.AddComponent<AlienAnimationController>();
            bridge.Configure(owner);
            FitAnimatedHeight(model, animator, clips, root.transform, visual.transform, capsule, report);
            ValidateAndPreview(model, animator, clips, root.transform, report);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/AlienAnimationResult.txt", "OK\n" + report);
        Debug.Log("Modelo y seis animaciones conectados al prefab Alien. Informe: Temp/AlienAnimationResult.txt");
    }

    private static AnimatorState GetState(AnimatorStateMachine machine, string name)
        => machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);

    private static void PrepareMaterials(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/') + "/Textures")) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Textures");
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/') + "/Materials")) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Materials");
        importer.ExtractTextures(Folder + "Textures");
        AssetDatabase.Refresh();
        foreach (Material material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
        {
            string name = string.Join("_", material.name.Split(Path.GetInvalidFileNameChars()));
            string destination = Folder + "Materials/" + name + ".mat";
            Material editable = AssetDatabase.LoadAssetAtPath<Material>(destination);
            if (editable == null)
            {
                editable = new Material(material);
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null && editable.shader.name == "Standard")
                {
                    Texture texture = editable.mainTexture;
                    Color color = editable.color;
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null) { editable.shader = shader; editable.SetTexture("_BaseMap", texture); editable.SetColor("_BaseColor", color); }
                }
                AssetDatabase.CreateAsset(editable, destination);
            }
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(material), editable);
        }
        importer.SaveAndReimport();
    }

    private static Bounds Measure(GameObject model, Transform reference)
    {
        Bounds result = default;
        bool first = true;
        foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = skin.sharedMesh;
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
                if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                else result.Encapsulate(point);
            }
        }
        return result;
    }

    private static Vector3 WeightedPoint(int index, float weight, Vector3 vertex, Transform[] bones, Matrix4x4[] poses)
        => weight <= 0f || index >= bones.Length || index >= poses.Length || bones[index] == null
            ? Vector3.zero : bones[index].TransformPoint(poses[index].MultiplyPoint3x4(vertex)) * weight;

    private static void FitAnimatedHeight(GameObject model, Animator animator, AnimationClip[] clips,
        Transform reference, Transform visual, CapsuleCollider capsule, StringBuilder report)
    {
        var transforms = model.GetComponentsInChildren<Transform>(true);
        var positions = transforms.Select(t => t.localPosition).ToArray();
        var rotations = transforms.Select(t => t.localRotation).ToArray();
        float highest = float.NegativeInfinity;
        float lowest = float.PositiveInfinity;
        foreach (AnimationClip clip in clips)
        {
            PlayableGraph graph = PlayableGraph.Create("AlienHeightCheck");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play();
                for (int sample = 0; sample < 20; sample++)
                {
                    playable.SetTime(clip.length * sample / 20f);
                    graph.Evaluate(0f);
                    Bounds bounds = Measure(model, reference);
                    highest = Mathf.Max(highest, bounds.max.y);
                    lowest = Mathf.Min(lowest, bounds.min.y);
                }
            }
            finally { graph.Destroy(); }
        }
        for (int i = 0; i < transforms.Length; i++)
        { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; }
        if (highest - lowest < .4f || highest - lowest > 3f)
            throw new InvalidOperationException("Altura animada inesperada: " + (highest - lowest));
        float factor = capsule.height / (highest - lowest);
        visual.localScale *= factor;
        Vector3 position = visual.localPosition * factor;
        position.y += capsule.center.y - capsule.height * .5f - lowest * factor;
        visual.localPosition = position;
        report.AppendLine("AnimatedHeight=" + capsule.height + ", scaleCorrection=" + factor);
    }

    private static void ValidateAndPreview(GameObject model, Animator animator, AnimationClip[] clips, Transform reference, StringBuilder report)
    {
        // Prueba las poses antes de guardar el prefab. Un clip inválido deja intacto el prefab anterior.
        var transforms = model.GetComponentsInChildren<Transform>(true);
        var positions = transforms.Select(t => t.localPosition).ToArray();
        var rotations = transforms.Select(t => t.localRotation).ToArray();
        for (int i = 0; i < clips.Length; i++)
        {
            PlayableGraph graph = PlayableGraph.Create("AlienPoseCheck");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clips[i]);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                graph.Play();
                for (int sample = 0; sample < 5; sample++)
                {
                    playable.SetTime(clips[i].length * (sample + .1f) / 5f);
                    graph.Evaluate(0f);
                    Bounds bounds = Measure(model, reference);
                    if (bounds.size.y < .4f || bounds.size.y > 2.8f || Mathf.Abs(bounds.center.x) > .8f
                        || Mathf.Abs(bounds.center.z) > .8f || bounds.min.y < -.35f)
                        throw new InvalidOperationException("Revisar pose " + States[i] + ": " + bounds);
                    report.AppendLine(States[i] + " sample " + sample + ": " + bounds);
                    if (sample == 2) RenderPose(model, "Temp/AlienPreview" + States[i] + ".png");
                }
            }
            finally { graph.Destroy(); }
        }
        for (int i = 0; i < transforms.Length; i++)
        { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; }
        animator.Rebind();
    }

    private static void RenderPose(GameObject model, string destination)
    {
        var preview = new PreviewRenderUtility();
        GameObject copy = Object.Instantiate(model);
        try
        {
            foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
            copy.GetComponent<Animator>().enabled = false;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = model.transform.lossyScale;
            preview.AddSingleGO(copy);
            Bounds bounds = Measure(copy, null);
            preview.camera.fieldOfView = 30;
            preview.camera.transform.position = bounds.center + new Vector3(.4f, .1f, 1f).normalized * bounds.extents.magnitude * 4.5f;
            preview.camera.transform.LookAt(bounds.center);
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 50f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.17f, .2f, .23f);
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
            preview.lights[1].intensity = 1f;
            preview.BeginPreview(new Rect(0, 0, 512, 512), GUIStyle.none);
            preview.Render(true);
            Texture image = preview.EndPreview();
            RenderTexture target = RenderTexture.GetTemporary(512, 512, 0);
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            try
            {
                Graphics.Blit(image, target);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(destination, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
            }
        }
        finally { preview.Cleanup(); if (copy != null) Object.DestroyImmediate(copy); }
    }
}
