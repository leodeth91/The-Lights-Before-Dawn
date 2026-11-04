using UnityEngine;

/// <summary>Seis espacios para voces grabadas por el equipo, disparadas por eventos del juego.</summary>
[DisallowMultipleComponent]
public sealed class StoryVoiceDirector : MonoBehaviour
{
    [SerializeField] private AudioClip opening;
    [SerializeField] private AudioClip parentsMissing;
    [SerializeField] private AudioClip anomalyBegins;
    [SerializeField] private AudioClip phoneFound;
    [SerializeField] private AudioClip sunrise;
    [SerializeField] private AudioClip finalDiscovery;

    private AudioSource source;
    private GameSessionManager session;
    private StoryProgression story;
    private bool openingPlayed;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = .8f;
        AudioMixerRouting.Route(source, AudioMixerRouting.Bus.Voice);
    }

    private void Start()
    {
        session = GameSessionManager.Instance;
        story = StoryProgression.Instance;
        if (session != null) session.StateChanged += OnSessionChanged;
        if (story != null) story.ObjectiveChanged += OnObjectiveChanged;
        AudioMixerRouting.Route(source, AudioMixerRouting.Bus.Voice);
    }

    private void OnDestroy()
    {
        if (session != null) session.StateChanged -= OnSessionChanged;
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
    }

    private void OnSessionChanged(GameSessionState state)
    {
        if (state == GameSessionState.Playing && !openingPlayed)
        {
            openingPlayed = true;
            Play(opening);
        }
        else if (state == GameSessionState.Ending) Play(finalDiscovery);
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        switch (objective)
        {
            case StoryObjective.TryExit: Play(parentsMissing); break;
            case StoryObjective.FindPhone: Play(anomalyBegins); break;
            case StoryObjective.Survive: Play(phoneFound); break;
            case StoryObjective.Escape: Play(sunrise); break;
        }
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        AudioMixerRouting.Route(source, AudioMixerRouting.Bus.Voice);
        source.Stop();
        source.clip = clip;
        source.Play();
    }
}
