#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Pruebas voluntarias desde el menú de Unity. Solo se ejecutan al elegirlas, nunca durante una partida normal.</summary>
public static class ProjectValidation
{
    private static void RunStory() => Run("TLBD.StorySmoke");

    private static void RunDetection() => Run("TLBD.DetectionSmoke");

    private static void RunAliens() => Run("TLBD.AlienSmoke");

    private static void RunHideCamera()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || SceneManager.GetActiveScene().path != "Assets/Scenes/ModularPrototype.unity") return;
        // Esta prueba solo altera objetos en Play; respeta los cambios sin guardar de la escena.
        SessionState.SetBool("TLBD.HideCameraSmoke", true);
        EditorApplication.EnterPlaymode();
    }

    private static void Run(string key)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || SceneManager.GetActiveScene().path != "Assets/Scenes/ModularPrototype.unity") return;
        // Guardar es necesario: las pruebas terminan saliendo de Play y no deben perder la distribución editada.
        if (!EditorSceneManager.SaveOpenScenes()) return;
        SessionState.SetBool(key, true);
        EditorApplication.EnterPlaymode();
    }
}
#endif
