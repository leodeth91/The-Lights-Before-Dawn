using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AlienOriginalVisualSetup
{
    private const string ModelPath = "Assets/Animations/Alien/AlienOriginal.fbx";
    private const string MaterialPath = "Assets/Animations/Alien/Materials/MAT_AlienSkin.mat";
    private const string PrefabPath = "Assets/Aliens/Alien.prefab";
    private const string ControllerPath = "Assets/Animations/Alien/Alien.controller";

    [MenuItem("The Lights Before Dawn/Personajes/Aplicar modelo original del alien")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Animator animator = model != null ? model.GetComponent<Animator>() : null;
        Avatar avatar = animator != null ? animator.avatar : null;
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogError("AlienOriginal: el Avatar humanoide no es válido; se conserva el prefab actual.");
            return;
        }

        ApplyVisual(model);
    }

    private static void ApplyVisual(GameObject source)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visual = root.transform.Find("AparienciaAlien");
            if (visual == null) return;
            Transform currentModel = visual.Find("ModeloAlienOriginal");
            if (currentModel != null)
            {
                if (Quaternion.Angle(currentModel.localRotation, Quaternion.Euler(0f, 180f, 0f)) < 0.1f)
                    return;

                currentModel.localRotation = Quaternion.Euler(0f, 180f, 0f);
                SkinnedMeshRenderer skin = currentModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Animator currentAnimator = currentModel.GetComponent<Animator>();
                AlienHeadScanner currentScanner = root.GetComponent<AlienHeadScanner>();
                CapsuleCollider currentCapsule = root.GetComponent<CapsuleCollider>();
                if (skin == null || currentAnimator == null || currentScanner == null || currentCapsule == null)
                    throw new InvalidOperationException("Faltan componentes en la apariencia alien.");

                float height = visual.localPosition.y;
                visual.localPosition = Vector3.zero;
                Vector3 currentCenter = root.transform.InverseTransformPoint(skin.bounds.center);
                visual.localPosition = new Vector3(currentCapsule.center.x - currentCenter.x,
                    height, currentCapsule.center.z - currentCenter.z);
                Transform currentHead = currentAnimator.GetBoneTransform(HumanBodyBones.Head);
                if (currentHead == null) throw new InvalidOperationException("Falta la cabeza del Avatar alien.");
                currentScanner.ConfigureAnimatedHead(currentHead,
                    Quaternion.Inverse(currentHead.rotation) * root.transform.rotation);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("AlienOriginal: modelo girado hacia el avance del alien.");
                return;
            }

            AlienController owner = root.GetComponent<AlienController>();
            AlienHeadScanner scanner = root.GetComponent<AlienHeadScanner>();
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (owner == null || scanner == null || capsule == null || controller == null || material == null)
                throw new InvalidOperationException("Faltan referencias necesarias para cambiar la apariencia alien.");

            Transform oldModel = visual.Find("ModeloAlien");
            if (oldModel == null) throw new InvalidOperationException("No se encontró el modelo anterior.");

            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            GameObject newModel = (GameObject)PrefabUtility.InstantiatePrefab(source, visual);
            newModel.name = "ModeloAlienOriginal";
            newModel.transform.localPosition = Vector3.zero;
            newModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Animator newAnimator = newModel.GetComponent<Animator>();
            SkinnedMeshRenderer[] skins = newModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (newAnimator == null || skins.Length == 0 || newAnimator.avatar == null || !newAnimator.avatar.isHuman)
                throw new InvalidOperationException("El modelo original no importó su malla o su Avatar humanoide.");

            foreach (SkinnedMeshRenderer skin in skins)
            {
                skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMaterials.Length).ToArray();
                skin.updateWhenOffscreen = true;
            }
            foreach (MeshRenderer extra in newModel.GetComponentsInChildren<MeshRenderer>(true))
                extra.enabled = false;
            foreach (Collider extra in newModel.GetComponentsInChildren<Collider>(true))
                extra.enabled = false;

            newAnimator.runtimeAnimatorController = controller;
            newAnimator.applyRootMotion = false;
            newAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Transform head = newAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null) throw new InvalidOperationException("El Avatar original no tiene hueso de cabeza.");

            Bounds bounds = skins[0].bounds;
            for (int i = 1; i < skins.Length; i++) bounds.Encapsulate(skins[i].bounds);
            if (bounds.size.y < 0.01f) throw new InvalidOperationException("La malla original tiene altura inválida.");
            visual.localScale = Vector3.one * (capsule.height / bounds.size.y);

            bounds = skins[0].bounds;
            for (int i = 1; i < skins.Length; i++) bounds.Encapsulate(skins[i].bounds);
            Vector3 center = root.transform.InverseTransformPoint(bounds.center);
            Vector3 bottom = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            visual.localPosition = new Vector3(capsule.center.x - center.x,
                capsule.center.y - capsule.height * 0.5f - bottom.y, capsule.center.z - center.z);

            scanner.ConfigureAnimatedHead(head, Quaternion.Inverse(head.rotation) * root.transform.rotation);
            newModel.AddComponent<AlienAnimationController>().Configure(owner);
            UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("AlienOriginal: visual con el atlas correcto integrada en Alien.prefab.");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
