using System.Collections.Generic;
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
    [SerializeField] private AudioClip chase;
    [Header("Transiciones elegidas para cada evento")]
    [SerializeField, Min(.1f)] private float menuFadeIn = 1.25f;
    [SerializeField, Min(.1f)] private float menuFadeOut = 1.25f;
    [SerializeField, Min(.1f)] private float explorationFade = 2f;
    [SerializeField, Min(.1f)] private float anomalyFade = 4f;
    [SerializeField, Min(.1f)] private float dawnFade = 5f;
    [SerializeField, Min(.1f)] private float endingFade = 2f;
    [SerializeField, Min(.1f)] private float chaseFadeIn = .35f;
    [SerializeField, Min(.1f)] private float chaseFadeOut = 3f;
    [SerializeField, Range(0f, 1f)] private float chaseVolume = .42f;

    private readonly AudioSource[] layers = new AudioSource[2];
    private readonly AudioLowPassFilter[] filters = new AudioLowPassFilter[2];
    private readonly float[] startVolumes = new float[2];
    private readonly float[] startCutoffs = new float[2];
    private readonly HashSet<AlienController> activeChasers = new HashSet<AlienController>();
    private GameSessionManager session;
    private StoryProgression story;
    private int activeLayer;
    private float targetVolume;
    private float targetCutoff = 22000f;
    private float menuVolume = 1f;
    private float fadeSeconds = 2f;
    private float transitionElapsed;
    private float nextRoutingCheck;
    private bool finalMusicLocked;

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
        ChaseMusicSettings chaseSettings = Resources.Load<ChaseMusicSettings>(ChaseMusicSettings.ResourcePath);
        if (chaseSettings != null)
        {
            if (chase == null) chase = chaseSettings.Clip;
            chaseVolume = chaseSettings.Volume;
            chaseFadeIn = chaseSettings.FadeInSeconds;
            chaseFadeOut = chaseSettings.FadeOutSeconds;
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
        AlienController.ChaseStateChanged += OnAlienChaseChanged;
        if (session != null) OnSessionChanged(session.State);
    }

    private void OnDestroy()
    {
        if (session != null) session.StateChanged -= OnSessionChanged;
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
        AlienController.ChaseStateChanged -= OnAlienChaseChanged;
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
        if (state == GameSessionState.Menu)
        {
            finalMusicLocked = false;
            activeChasers.Clear();
            Select(menu, menuVolume, 22000f, menuFadeIn, "Menu");
        }
        else if (state == GameSessionState.Ending)
        {
            activeChasers.Clear();
            if (!finalMusicLocked) Select(ending, .38f, 18000f, endingFade, "Amanecer");
        }
        else if (state == GameSessionState.Playing)
        {
            if (finalMusicLocked) return;
            if (activeChasers.Count > 0) Select(chase, chaseVolume, 22000f, chaseFadeIn, "Persecucion");
            else SelectBaseMusic(menuFadeOut);
        }
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        if (finalMusicLocked)
        {
            if (objective == StoryObjective.Escape)
                AudioMixerRouting.TransitionMusicSnapshot("Amanecer", dawnFade);
            return;
        }
        if (session == null || session.State == GameSessionState.Ending) return;
        if (activeChasers.Count > 0) return;
        SelectBaseMusic(objective, -1f);
    }

    private void OnAlienChaseChanged(AlienController alien, bool active)
    {
        if (alien == null) return;
        int previousCount = activeChasers.Count;
        if (active) activeChasers.Add(alien);
        else activeChasers.Remove(alien);

        if (session == null || !session.IsPlaying || finalMusicLocked) return;
        if (previousCount == 0 && activeChasers.Count > 0)
            Select(chase, chaseVolume, 22000f, chaseFadeIn, "Persecucion");
        else if (previousCount > 0 && activeChasers.Count == 0)
            SelectBaseMusic(chaseFadeOut);
    }

    public void BeginFinalMusic()
    {
        if (finalMusicLocked) return;
        finalMusicLocked = true;
        activeChasers.Clear();
        AudioMixerRouting.TransitionMusicSnapshot("Distancias", 1.25f);
        foreach (AudioSource layer in layers)
        {
            if (layer == null) continue;
            layer.Stop();
            layer.volume = 0f;
        }
        targetVolume = 0f;
    }

    private void SelectBaseMusic(float fadeOverride)
    {
        SelectBaseMusic(story != null ? story.Objective : StoryObjective.FindParents, fadeOverride);
    }

    private void SelectBaseMusic(StoryObjective objective, float fadeOverride)
    {
        if (objective == StoryObjective.FindPhone || objective == StoryObjective.Survive)
            Select(anomaly, .32f, 8000f, fadeOverride > 0f ? fadeOverride : anomalyFade, "Anomalia");
        else if (objective == StoryObjective.Escape)
            Select(dawn, .4f, 22000f, fadeOverride > 0f ? fadeOverride : dawnFade, "Amanecer");
        else
            Select(exploration, .4f, 22000f, fadeOverride > 0f ? fadeOverride : explorationFade, "Exploracion");
    }

    private void Select(AudioClip clip, float volume, float cutoff, float seconds, string snapshotName)
    {
        fadeSeconds = Mathf.Max(.1f, seconds);
        AudioMixerRouting.TransitionMusicSnapshot(snapshotName, fadeSeconds);
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
