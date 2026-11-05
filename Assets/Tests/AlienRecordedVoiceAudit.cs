#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Comprueba los cinco clips, las respuestas y el ruteo 3D sin guardar la escena.</summary>
public sealed class AlienRecordedVoiceAudit : MonoBehaviour
{
    private const string SessionKey = "TLBD.AlienRecordedVoiceAudit";

    [MenuItem("The Lights Before Dawn/Pruebas/Auditar voces alienígenas")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || SceneManager.GetActiveScene().path != "Assets/Scenes/ModularPrototype.unity") return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, false);
        new GameObject("ALIEN_RECORDED_VOICE_AUDIT").AddComponent<AlienRecordedVoiceAudit>();
    }

    private IEnumerator Start()
    {
        yield return null;
        int failures = 0;
        AlienController[] aliens = FindObjectsByType<AlienController>(FindObjectsSortMode.None)
            .OrderBy(alien => alien.name).ToArray();
        if (aliens.Length != 2)
        {
            Debug.LogError("ALIEN_VOICE_AUDIT: se esperaban dos aliens iniciales");
            EditorApplication.ExitPlaymode();
            yield break;
        }

        GameSessionManager.Instance.BeginGame();
        AlienController asker = aliens[0];
        AlienController respondent = aliens[1];
        AlienAmbientAudio askerVoice = asker.GetComponent<AlienAmbientAudio>();
        AlienAmbientAudio respondentVoice = respondent.GetComponent<AlienAmbientAudio>();
        if (askerVoice == null || respondentVoice == null)
        {
            Debug.LogError("ALIEN_VOICE_AUDIT: falta AlienAmbientAudio");
            EditorApplication.ExitPlaymode();
            yield break;
        }

        foreach (AlienAmbientAudio voice in new[] { askerVoice, respondentVoice })
        {
            AudioSource source = voice.VocalSource;
            if (source == null || source.transform != voice.transform
                || source.clip != null
                || !Mathf.Approximately(source.spatialBlend, 1f)
                || source.outputAudioMixerGroup == null
                || source.outputAudioMixerGroup.name != "Voz") failures++;
            if (voice.GetComponents<AudioSource>().Length != 2
                || voice.GetComponent<SFXVariacion>() != null) failures++;
            if (voice.QuestionClip == null || voice.QuestionClip.name != "Pregunta por responsabilidad"
                || voice.ResponseClip == null || voice.ResponseClip.name != "Respuesta Si"
                || voice.NegativeResponseClip == null || voice.NegativeResponseClip.name != "Respuesta No"
                || voice.CallClip == null || voice.CallClip.name != "Llamado"
                || voice.ChaseClip == null || voice.ChaseClip.name != "llamado_aliens") failures++;
        }

        int questionEvents = 0, yesEvents = 0, noEvents = 0, callEvents = 0, chaseEvents = 0;
        askerVoice.VocalizationStarted += (kind, _) =>
        {
            if (kind == AlienVocalization.Question) questionEvents++;
            if (kind == AlienVocalization.Call) callEvents++;
            if (kind == AlienVocalization.Chase) chaseEvents++;
        };
        respondentVoice.VocalizationStarted += (kind, _) =>
        {
            if (kind == AlienVocalization.Response) yesEvents++;
        };

        RoomModule room = asker.Occupant.Room;
        SoundResponsibility responsibility = respondent.GetComponent<SoundResponsibility>();
        var yesStimulus = new SoundStimulus(900001, SuspiciousSoundKind.DoorOpening,
            respondent.transform.position, respondent.gameObject, room, null);
        responsibility.Claim(yesStimulus.Id, 10f);
        askerVoice.PlayQuestionVocalization();
        SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Question,
            yesStimulus, asker, room, asker.transform.position);
        if (questionEvents != 1 || yesEvents != 1 || responsibility.IsResponsibleFor(yesStimulus.Id)) failures++;

        respondentVoice.VocalizationStarted += (kind, _) =>
        {
            if (kind == AlienVocalization.Response) noEvents++;
        };
        var noStimulus = new SoundStimulus(900002, SuspiciousSoundKind.PlayerRunningStep,
            asker.transform.position, null, room, null);
        SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Question,
            noStimulus, asker, room, asker.transform.position);
        SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Question,
            noStimulus, asker, room, asker.transform.position);
        askerVoice.PlayCallVocalization();
        askerVoice.PlayChaseVocalization();
        if (noEvents != 1 || callEvents != 1 || chaseEvents != 1) failures++;

        Debug.Log("ALIEN_VOICE_AUDIT: failures=" + failures + ", question=" + questionEvents
            + ", yes=" + yesEvents + ", no=" + noEvents + ", call=" + callEvents
            + ", chase=" + chaseEvents);
        EditorApplication.ExitPlaymode();
    }
}
#endif
