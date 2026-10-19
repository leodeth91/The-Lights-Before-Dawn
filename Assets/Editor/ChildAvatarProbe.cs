using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Prueba un Avatar Humanoid en una copia temporal del niño, sin cambiar la escena.</summary>
[InitializeOnLoad]
public static class ChildAvatarProbe
{
    private const string RequestPath = "Temp/ProbeChildAvatar.request";
    private const string ResultPath = "Temp/ChildAvatarProbeResult.txt";
    static ChildAvatarProbe() => EditorApplication.update += RunIfRequested;

    private static void RunIfRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RunIfRequested;
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        try { Probe(); }
        catch (Exception error) { File.WriteAllText(ResultPath, "ERROR\n" + error); Debug.LogException(error); }
    }

    [MenuItem("The Lights Before Dawn/Personajes/Probar Avatar Humanoid del niño")]
    public static void Probe()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Child/kid-character-fully-rigged.glb");
        if (asset == null) throw new InvalidOperationException("Falta el modelo GLB.");
        GameObject copy = UnityEngine.Object.Instantiate(asset);
        copy.hideFlags = HideFlags.HideAndDontSave;
        Avatar avatar = null;
        try
        {
            Transform[] transforms = copy.GetComponentsInChildren<Transform>(true);
            avatar = BuildAvatar(copy);
            string result = "Avatar=" + (avatar != null ? avatar.name : "ninguno")
                + "\nValid=" + (avatar != null && avatar.isValid)
                + "\nHuman=" + (avatar != null && avatar.isHuman)
                + "\nTransforms=" + transforms.Length;
            File.WriteAllText(ResultPath, result + "\n");
            Debug.Log("Avatar de prueba: " + result.Replace('\n', ' '));
        }
        finally
        {
            if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }

    public static Avatar BuildAvatar(GameObject copy)
    {
            Transform[] transforms = copy.GetComponentsInChildren<Transform>(true);
            var mappings = new (HumanBodyBones bone, string name)[]
            {
                (HumanBodyBones.Hips, "Boy_Hips_04"), (HumanBodyBones.Spine, "Boy_Spine_019"),
                (HumanBodyBones.Chest, "Boy_Spine2_021"), (HumanBodyBones.UpperChest, "Boy_Spine3_022"),
                (HumanBodyBones.Neck, "Boy_Neck1_056"), (HumanBodyBones.Head, "Boy_Head_057"),
                (HumanBodyBones.LeftShoulder, "Boy_LeftShoulder_0159"), (HumanBodyBones.LeftUpperArm, "Boy_LeftArm_0160"),
                (HumanBodyBones.LeftLowerArm, "Boy_LeftForeArm_0163"), (HumanBodyBones.LeftHand, "Boy_LeftHand_00"),
                (HumanBodyBones.RightShoulder, "Boy_RightShoulder_023"), (HumanBodyBones.RightUpperArm, "Boy_RightArm_024"),
                (HumanBodyBones.RightLowerArm, "Boy_RightForeArm_027"), (HumanBodyBones.RightHand, "Boy_RightHand_030"),
                (HumanBodyBones.LeftUpperLeg, "Boy_LeftUpLeg_0187"), (HumanBodyBones.LeftLowerLeg, "Boy_LeftLeg_0190"),
                (HumanBodyBones.LeftFoot, "Boy_LeftFoot_0191"), (HumanBodyBones.LeftToes, "Boy_LeftToeBase_0192"),
                (HumanBodyBones.RightUpperLeg, "Boy_RightUpLeg_05"), (HumanBodyBones.RightLowerLeg, "Boy_RightLeg_08"),
                (HumanBodyBones.RightFoot, "Boy_RightFoot_09"), (HumanBodyBones.RightToes, "Boy_RightToeBase_010")
            };
            foreach (var mapping in mappings)
                if (transforms.Count(t => t.name == mapping.name) != 1)
                    throw new InvalidOperationException("El hueso " + mapping.name + " no es único o falta.");
            HumanDescription description = new HumanDescription
            {
                skeleton = transforms.Select(t => new SkeletonBone
                {
                    name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
                }).ToArray(),
                human = mappings.Select(m => new HumanBone
                {
                    boneName = m.name, humanName = HumanTrait.BoneName[(int)m.bone], limit = new HumanLimit { useDefaultValues = true }
                }).ToArray(),
                armStretch = .05f, legStretch = .05f,
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f
            };
            return AvatarBuilder.BuildHumanAvatar(copy, description);
    }
}
