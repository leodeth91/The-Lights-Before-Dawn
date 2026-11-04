using UnityEngine;

/// <summary>Reproduce las capas de Distancias con un único reloj musical.</summary>
[DisallowMultipleComponent]
public sealed class FinalLayeredMusicDirector : MonoBehaviour
{
    private const float StartBeforeDawn = 45f;
    private const float DrumsBeforeDawn = 30f;
    private const float FadeSeconds = .35f;
    private const int Bass = 0, Drums = 1, Piano = 2, Choir = 3, Guitar = 4;

    private readonly AudioSource[] layers = new AudioSource[5];
    private readonly float[] levels = { .5f, .45f, .4f, .4f, .5f };
    private readonly bool[] audible = new bool[5];
    private GameSessionManager session;
    private StoryProgression story;
    private bool started;
    private bool stopped;

    private void Awake()
    {
        FinalLayeredMusicSettings settings = Resources.Load<FinalLayeredMusicSettings>(
            FinalLayeredMusicSettings.ResourcePath);
        if (settings == null || !settings.IsComplete) return;
        for (int i = 0; i < layers.Length; i++)
        {
            AudioSource source = new GameObject("Distancias_" + i).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = settings.ClipAt(i);
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            levels[i] = settings.VolumeAt(i);
            AudioMixerRouting.Route(source, AudioMixerRouting.Bus.Music);
            layers[i] = source;
        }
    }

    private void Start()
    {
        session = GameSessionManager.Instance;
        story = StoryProgression.Instance;
        if (story != null) story.ObjectiveChanged += OnObjectiveChanged;
    }

    private void Update()
    {
        if (session == null || !session.IsPlaying || stopped) return;
        if (!started && session.HasSurvivalTimer && session.RemainingTime <= StartBeforeDawn)
            StartLayers();
        if (!started) return;
        if (session.RemainingTime <= DrumsBeforeDawn) BringIn(Drums);
        for (int i = 0; i < layers.Length; i++)
        {
            AudioSource source = layers[i];
            if (source == null) continue;
            source.volume = Mathf.MoveTowards(source.volume, audible[i] ? levels[i] : 0f,
                Time.deltaTime / FadeSeconds);
        }
    }

    private void StartLayers()
    {
        if (layers[Bass] == null) return;
        started = true;
        GetComponent<AdaptiveMusicDirector>()?.BeginFinalMusic();
        double startTime = AudioSettings.dspTime + .15;
        foreach (AudioSource source in layers) source.PlayScheduled(startTime);
        BringIn(Bass);
    }

    private void BringIn(int index)
    {
        if (started && !stopped && layers[index] != null) audible[index] = true;
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        if (objective != StoryObjective.Escape || !started) return;
        BringIn(Piano);
        BringIn(Choir);
    }

    public void BringInGuitar() => BringIn(Guitar);

    public void StopFinalMusic()
    {
        stopped = true;
        foreach (AudioSource source in layers)
            if (source != null) source.Stop();
    }

    private void OnDestroy()
    {
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
    }
}
