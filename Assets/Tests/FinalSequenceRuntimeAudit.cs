#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Comprueba en Play el amanecer, los efectos y el retorno de la secuencia final al menú.</summary>
public sealed class FinalSequenceRuntimeAudit : MonoBehaviour
{
    private const string SessionKey = "TLBD.FinalSequenceRuntimeAudit";
    private const string MainSceneName = "ModularPrototype";
    private int failures;

    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, false);
        Application.runInBackground = true;
        GameObject root = new GameObject("FINAL_SEQUENCE_RUNTIME_AUDIT");
        DontDestroyOnLoad(root);
        root.AddComponent<FinalSequenceRuntimeAudit>();
    }

    private IEnumerator Start()
    {
        yield return null;
        try
        {
            SceneManager.LoadScene(MainSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Check(false, "No se pudo cargar ModularPrototype: " + exception.Message);
            Finish();
            yield break;
        }

        yield return null;
        yield return null;
        Check(SceneManager.GetActiveScene().name == MainSceneName, "La escena principal cargó en Play.");

        GameSessionManager session = GameSessionManager.Instance;
        StoryProgression story = StoryProgression.Instance;
        HouseFlowController house = HouseFlowController.Instance;
        PlayerInteractor playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        Check(session != null && story != null && house != null && playerInteractor != null,
            "La escena inicializó sesión, historia, habitaciones y jugador.");
        if (session == null || story == null || house == null || playerInteractor == null)
        { Finish(); yield break; }

        OpeningSequenceDirector opening = session.GetComponent<OpeningSequenceDirector>();
        if (opening != null) Destroy(opening);
        yield return null;
        session.BeginGame();
        Check(session.IsPlaying, "La partida inició sin la secuencia de despertar.");
        if (!session.IsPlaying) { Finish(); yield break; }

        MethodInfo advance = typeof(StoryProgression).GetMethod("Advance",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (advance == null)
        {
            Check(false, "StoryProgression.Advance está disponible para el auditor.");
            Finish(); yield break;
        }

        advance.Invoke(story, new object[] { StoryObjective.Survive, new string[0] });
        session.StartSurvival(.3f);
        Check(story.Objective == StoryObjective.Survive && session.HasSurvivalTimer,
            "La misión de supervivencia y su temporizador iniciaron.");

        float dawnDeadline = Time.realtimeSinceStartup + 4f;
        while (story.Objective != StoryObjective.Escape && Time.realtimeSinceStartup < dawnDeadline)
            yield return null;
        Check(story.HasDawnArrived, "El temporizador avanzó la historia a Escape.");
        Check(session.IsPlaying, "El fin del temporizador no terminó la partida.");
        if (!story.HasDawnArrived || !session.IsPlaying) { Finish(); yield break; }

        yield return new WaitForSecondsRealtime(8.25f);
        Volume anomalyVolume = FindVolume("POST_Anomaly");
        Volume nightVolume = FindVolume("POST_GlobalNight");
        Check(anomalyVolume != null && anomalyVolume.weight <= .01f,
            "POST_Anomaly llegó a peso cero tras el fundido.");
        Check(nightVolume != null && nightVolume.weight <= .01f,
            "POST_GlobalNight se apagó gradualmente al amanecer.");
        Check(TelevisionIsOff(), "La TV restauró su material y detuvo la interferencia.");
        Check(SurveillanceIsStopped(), "La vigilancia ya no tiene una rutina activa.");
        AlienController[] aliens = FindObjectsByType<AlienController>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Check(aliens.Length > 0 && System.Array.TrueForAll(aliens,
                alien => !alien.gameObject.activeSelf),
            "Los aliens desaparecieron al terminar el contador.");

        DoorSocket exit = GetPrivateField<DoorSocket>(story, "entrance");
        ChildPlayerController player = playerInteractor.GetComponent<ChildPlayerController>();
        FinalSequenceDirector director = session.GetComponent<FinalSequenceDirector>();
        Check(exit != null && exit.Room != null && exit.Door != null && player != null && director != null,
            "La puerta final, el controlador del jugador y el director existen.");
        if (exit == null || exit.Room == null || exit.Door == null || player == null || director == null)
        { Finish(); yield break; }

        exit.Room.gameObject.SetActive(true);
        MethodInfo setCurrentRoom = typeof(HouseFlowController).GetMethod("SetCurrentRoom",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (setCurrentRoom == null)
        {
            Check(false, "HouseFlowController.SetCurrentRoom está disponible para el auditor.");
            Finish(); yield break;
        }
        house.RegisterOccupant(player.gameObject, exit.Room, false);
        setCurrentRoom.Invoke(house, new object[] { exit.Room });
        TeleportPlayer(player, exit.Position - exit.Outward * .3f);

        bool requestAccepted = house.RequestOpen(exit, player.gameObject);
        Check(requestAccepted, "La solicitud de apertura preparó el exterior final.");
        if (!requestAccepted) { Finish(); yield break; }

        float readyDeadline = Time.realtimeSinceStartup + 25f;
        while (!director.IsReady && Time.realtimeSinceStartup < readyDeadline) yield return null;
        Check(director.IsReady, "La escena alienígena y el par de portales quedaron preparados.");
        if (!director.IsReady) { Finish(); yield break; }

        float doorDeadline = Time.realtimeSinceStartup + 5f;
        while (!exit.Door.IsOpen && Time.realtimeSinceStartup < doorDeadline) yield return null;
        Check(exit.Door.IsOpen, "La puerta de salida se abrió.");
        if (!exit.Door.IsOpen) { Finish(); yield break; }

        TeleportPlayer(player, exit.Position + exit.Outward * .15f);
        yield return null;
        yield return null;
        Check(session.State == GameSessionState.Ending, "Cruzar el umbral inició la secuencia final.");
        Check(player.IsMovementLocked, "El control del jugador quedó bloqueado para la cinemática.");

        yield return new WaitForSecondsRealtime(3f);
        Transform visualRoot = player.transform.Find("PlayerVisual");
        GameObject eye = GameObject.Find("FinalSequenceEyeTarget");
        Vector3 expectedEye = visualRoot != null
            ? visualRoot.position + visualRoot.rotation * new Vector3(0f, 1.15f, .24f)
            : Vector3.zero;
        Check(eye != null && visualRoot != null
            && Vector3.Distance(eye.transform.position, expectedEye) < .05f,
            "La cámara final sigue los ojos adelantados como en el despertar.");
        bool visualHidden = visualRoot != null;
        if (visualRoot != null)
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
                visualHidden &= renderer.forceRenderingOff;
        Check(visualHidden, "La visual del niño no tapa la primera persona al mirar atrás.");

        float menuDeadline = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < menuDeadline)
        {
            GameSessionManager currentSession = GameSessionManager.Instance;
            if (SceneManager.GetActiveScene().name == MainSceneName
                && currentSession != null && currentSession != session
                && currentSession.State == GameSessionState.Menu) break;
            yield return null;
        }

        GameSessionManager menuSession = GameSessionManager.Instance;
        Check(SceneManager.GetActiveScene().name == MainSceneName
            && menuSession != null && menuSession != session
            && menuSession.State == GameSessionState.Menu,
            "La secuencia cargó ModularPrototype y regresó al menú en menos de 45 segundos.");
        Finish();
    }

    private static Volume FindVolume(string objectName)
    {
        foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
            if (volume.name == objectName) return volume;
        return null;
    }

    private static bool TelevisionIsOff()
    {
        TVInterferenceScreen[] screens = FindObjectsByType<TVInterferenceScreen>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (screens.Length == 0) return false;

        FieldInfo poweredField = typeof(TVInterferenceScreen).GetField("activeOnStart",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo rendererField = typeof(TVInterferenceScreen).GetField("screen",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo materialField = typeof(TVInterferenceScreen).GetField("originalMaterial",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (poweredField == null || rendererField == null || materialField == null) return false;

        foreach (TVInterferenceScreen screen in screens)
        {
            if ((bool)poweredField.GetValue(screen)) return false;
            Renderer renderer = rendererField.GetValue(screen) as Renderer;
            Material original = materialField.GetValue(screen) as Material;
            if (renderer != null && renderer.sharedMaterial != original) return false;
        }
        return true;
    }

    private static bool SurveillanceIsStopped()
    {
        RoomSurveillanceDirector[] directors = FindObjectsByType<RoomSurveillanceDirector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (directors.Length == 0) return false;
        FieldInfo routineField = typeof(RoomSurveillanceDirector).GetField("routine",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (routineField == null) return false;
        foreach (RoomSurveillanceDirector director in directors)
            if (routineField.GetValue(director) != null) return false;
        return true;
    }

    private static T GetPrivateField<T>(object instance, string fieldName) where T : class
    {
        FieldInfo field = instance.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? field.GetValue(instance) as T : null;
    }

    private static void TeleportPlayer(ChildPlayerController player, Vector3 position)
    {
        CharacterController capsule = player.GetComponent<CharacterController>();
        bool wasEnabled = capsule != null && capsule.enabled;
        if (wasEnabled) capsule.enabled = false;
        player.transform.position = position;
        if (wasEnabled) capsule.enabled = true;
        Physics.SyncTransforms();
    }

    private void Check(bool passed, string description)
    {
        if (passed) Debug.Log("FINAL_AUDIT PASS: " + description);
        else
        {
            failures++;
            Debug.LogError("FINAL_AUDIT FAIL: " + description);
        }
    }

    private void Finish()
    {
        if (failures == 0) Debug.Log("FINAL_AUDIT PASS: secuencia completa; fallos=0");
        else Debug.LogError("FINAL_AUDIT FAIL: fallos=" + failures);
        EditorApplication.ExitPlaymode();
    }
}
#endif
