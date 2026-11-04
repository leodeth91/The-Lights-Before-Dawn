using System.Collections;
using UnityEngine;

/// <summary>Conecta los eventos narrativos con las grabaciones del equipo.</summary>
[DisallowMultipleComponent]
public sealed class StoryVoiceDirector : MonoBehaviour
{
    [SerializeField] private AudioClip opening;
    [SerializeField] private AudioClip parentsMissing;
    [SerializeField] private AudioClip anomalyBegins;
    [SerializeField] private AudioClip phoneFound;
    [SerializeField] private AudioClip sunrise;
    [SerializeField] private AudioClip finalDiscovery;

    private DialogueAudioManager dialogue;
    private Coroutine openingRoutine;
    private Coroutine pendingLine;
    private GameSessionManager session;
    private StoryProgression story;
    private bool openingPlayed;

    private void Awake()
    {
        dialogue = GetComponent<DialogueAudioManager>();
        if (dialogue == null) dialogue = gameObject.AddComponent<DialogueAudioManager>();
    }

    private void Start()
    {
        session = GameSessionManager.Instance;
        story = StoryProgression.Instance;
        if (session != null) session.StateChanged += OnSessionChanged;
        if (story != null) story.ObjectiveChanged += OnObjectiveChanged;
    }

    private void OnDestroy()
    {
        if (session != null) session.StateChanged -= OnSessionChanged;
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        if (pendingLine != null) StopCoroutine(pendingLine);
    }

    private void OnSessionChanged(GameSessionState state)
    {
        if (state == GameSessionState.Playing && !openingPlayed)
        {
            openingPlayed = true;
            openingRoutine = StartCoroutine(PlayOpeningAfterSequence());
        }
    }

    private IEnumerator PlayOpeningAfterSequence()
    {
        // La sesión entra en Playing antes de que la secuencia alcance su primer frame.
        yield return null;
        while (OpeningSequenceDirector.IsRunning) yield return null;
        openingRoutine = null;
        PlayLines(1, 2);
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        switch (objective)
        {
            case StoryObjective.TryExit: PlayLines(3, 4); break;
            case StoryObjective.FindPhone: PlayLine(5); break;
            case StoryObjective.Survive: PlayLines(6, 7); break;
            case StoryObjective.Escape: PlayLine(8); break;
        }
    }

    public void PlayFinalDiscovery() => PlayLine(9);

    private void PlayLine(int id)
    {
        if (pendingLine != null) StopCoroutine(pendingLine);
        pendingLine = null;
        dialogue.PlayDialogue(id);
    }

    private void PlayLines(int first, int second)
    {
        PlayLine(first);
        pendingLine = StartCoroutine(PlayAfterCurrent(second));
    }

    private IEnumerator PlayAfterCurrent(int id)
    {
        yield return new WaitForSeconds(dialogue.DuracionActual);
        dialogue.PlayDialogue(id);
        pendingLine = null;
    }
}
