using UnityEngine;

/// <summary>Reproduce y mezcla las piezas musicales según el estado de la sesión.</summary>
[DisallowMultipleComponent]
public sealed class AdaptiveMusicDirector : MonoBehaviour
{
    [Header("Clips pendientes de la música original")]
    [SerializeField] private AudioClip menu;
    [SerializeField] private AudioClip exploration;
    [SerializeField] private AudioClip anomaly;
    [SerializeField] private AudioClip dawn;
    [SerializeField] private AudioClip ending;
    [Header("Transiciones elegidas para cada evento")]
    [SerializeField, Min(.1f)] private float menuFadeIn = 1.25f;
    [SerializeField, Min(.1f)] private float menuFadeOut = 1.25f;
    [SerializeField, Min(.1f)] private float explorationFade = 2f;
    [SerializeField, Min(.1f)] private float anomalyFade = 4f;
    [SerializeField, Min(.1f)] private float dawnFade = 5f;
    [SerializeField, Min(.1f)] private float endingFade = 2f;

    private readonly AudioSource[] layers = new AudioSource[2];
    private readonly AudioLowPassFilter[] filters = new AudioLowPassFilter[2];
    private readonly float[] startVolumes = new float[2];
    private readonly float[] startCutoffs = new float[2];
    private GameSessionManager session;
    private StoryProgression story;
    private int activeLayer;
    private float targetVolume;
    private float targetCutoff = 22000f;
    private float menuVolume = 1f;
    private float fadeSeconds = 2f;
    private float transitionElapsed;
    private float nextRoutingCheck;

    private void Awake()
    {
        MenuMusicSettings menuSettings = Resources.Load<MenuMusicSettings>(MenuMusicSettings.ResourcePath);
        if (menuSettings != null)
        {
            if (menu == null) menu = menuSettings.Clip;
            menuVolume = menuSettings.Volume;
            menuFadeIn = menuSettings.FadeInSeconds;
            menuFadeOut = menuSettings.FadeOutSeconds;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            GameObject layer = new GameObject("MusicLayer_" + (i + 1));
            layer.transform.SetParent(transform, false);
            layers[i] = layer.AddComponent<AudioSource>();
            layers[i].playOnAwake = false;
            layers[i].loop = true;
            layers[i].spatialBlend = 0f;
            layers[i].volume = 0f;
            filters[i] = layer.AddComponent<AudioLowPassFilter>();
            filters[i].cutoffFrequency = 22000f;
            AudioMixerRouting.Route(layers[i], AudioMixerRouting.Bus.Music);
        }
    }

    private void Start()
    {
        session = GameSessionManager.Instance;
        story = StoryProgression.Instance;
        if (session != null) session.StateChanged += OnSessionChanged;
        if (story != null) story.ObjectiveChanged += OnObjectiveChanged;
        if (session != null) OnSessionChanged(session.State);
    }

    private void OnDestroy()
    {
        if (session != null) session.StateChanged -= OnSessionChanged;
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
    }

    private void Update()
    {
        // El volumen y el filtro reaccionan juntos al objetivo, con tiempos distintos
        // para el inicio de la anomalía y el amanecer.
        transitionElapsed = Mathf.Min(fadeSeconds, transitionElapsed + Time.unscaledDeltaTime);
        float progress = transitionElapsed / fadeSeconds;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            float desired = i == activeLayer ? targetVolume : 0f;
            layers[i].volume = Mathf.Lerp(startVolumes[i], desired, progress);
            filters[i].cutoffFrequency = Mathf.Lerp(startCutoffs[i], targetCutoff, progress);
            if (i != activeLayer && layers[i].isPlaying && layers[i].volume <= 0f)
                layers[i].Stop();
        }
        if (Time.unscaledTime < nextRoutingCheck) return;
        nextRoutingCheck = Time.unscaledTime + 1f;
        foreach (AudioSource layer in layers)
            if (layer != null && layer.outputAudioMixerGroup == null)
                AudioMixerRouting.Route(layer, AudioMixerRouting.Bus.Music);
    }

    private void OnSessionChanged(GameSessionState state)
    {
        if (state == GameSessionState.Menu) Select(menu, menuVolume, 22000f, menuFadeIn);
        else if (state == GameSessionState.Ending) Select(ending, .38f, 18000f, endingFade);
        else if (state == GameSessionState.Playing)
            Select(null, 0f, 22000f, menuFadeOut);
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        if (session == null || session.State == GameSessionState.Ending) return;
        if (objective == StoryObjective.FindPhone || objective == StoryObjective.Survive)
            Select(anomaly, .32f, 8000f, anomalyFade);
        else if (objective == StoryObjective.Escape)
            Select(dawn, .4f, 22000f, dawnFade);
        else
            Select(exploration, .4f, 22000f, explorationFade);
    }

    private void Select(AudioClip clip, float volume, float cutoff, float seconds)
    {
        fadeSeconds = Mathf.Max(.1f, seconds);
        transitionElapsed = 0f;
        for (int i = 0; i < layers.Length; i++)
        {
            startVolumes[i] = layers[i].volume;
            startCutoffs[i] = filters[i].cutoffFrequency;
        }
        targetCutoff = cutoff;
        if (clip == layers[activeLayer].clip)
        {
            targetVolume = clip == null ? 0f : volume;
            return;
        }
        activeLayer = 1 - activeLayer;
        layers[activeLayer].Stop();
        layers[activeLayer].clip = clip;
        layers[activeLayer].volume = 0f;
        startVolumes[activeLayer] = 0f;
        AudioMixerRouting.Route(layers[activeLayer], AudioMixerRouting.Bus.Music);
        if (clip != null) layers[activeLayer].Play();
        targetVolume = clip == null ? 0f : volume;
    }
}
