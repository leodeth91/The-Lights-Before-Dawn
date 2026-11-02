using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>Conecta los sonidos ambientales a los objetos de las habitaciones modulares.</summary>
[DisallowMultipleComponent]
public sealed class SpatialWorldAudio : MonoBehaviour
{
    private const float ClockVolume = .28f;

    [SerializeField] private HouseFlowController house;
    [SerializeField] private StoryProgression story;
    [SerializeField] private GameSessionManager session;
    [SerializeField] private RoomModule officeRoom;
    [SerializeField] private StoryPhone phone;
    [Header("Clips")]
    [SerializeField] private AudioClip phoneVibration;
    [SerializeField] private AudioClip windowAmbience;
    [SerializeField] private AudioClip televisionStatic;
    [SerializeField] private AudioClip kitchenRadio;
    [SerializeField] private AudioClip clockTicking;

    private readonly List<AudioSource> loops = new List<AudioSource>();
    private readonly List<AudioSource> anomalyLoops = new List<AudioSource>();
    private AudioSource clockSource;
    private AudioSource phoneSource;
    private bool vibrationPlayed;
    private bool dawnReached;
    private Coroutine anomalyFade;

    private void Start()
    {
        if (house == null) house = HouseFlowController.Instance;
        if (story == null) story = StoryProgression.Instance;
        if (session == null) session = GameSessionManager.Instance;
        if (house == null || story == null || session == null) { Debug.LogWarning("Faltan referencias para el audio espacial.", this); return; }

        foreach (RoomModule room in house.ModuleById.Values)
        {
            if (room == null) continue;
            foreach (Transform child in room.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "LuzExteriorVentana") TrackAnomalyLoop(AddLoop(child, windowAmbience, 0.30f, 0.8f, 5f));
                else if (child.name == "tv__small") TrackAnomalyLoop(AddLoop(child, televisionStatic, 0.23f, 0.7f, 9f));
                else if (child.name == "small_radio") AddLoop(child, kitchenRadio, 0.60f, 1.5f, 8f);
                else if (child.name == "grandfather_clock") clockSource = AddLoop(child, clockTicking, ClockVolume, 1.2f, 9f);
            }
        }
        if (phone != null && phoneVibration != null)
        {
            phoneSource = phone.gameObject.AddComponent<AudioSource>();
            ConfigureSpatial(phoneSource, 1f, 0.7f, 7f);
            phoneSource.playOnAwake = false;
        }
        house.CurrentRoomChanged += OnRoomChanged;
        story.ObjectiveChanged += OnObjectiveChanged;
        session.StateChanged += OnSessionChanged;
        OnObjectiveChanged(story.Objective);
    }

    private AudioSource AddLoop(Transform target, AudioClip clip, float volume, float minDistance, float maxDistance)
    {
        if (clip == null) return null;
        AudioSource source = target.gameObject.AddComponent<AudioSource>();
        ConfigureSpatial(source, volume, minDistance, maxDistance);
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        loops.Add(source);
        return source;
    }

    private static void ConfigureSpatial(AudioSource source, float volume, float minDistance, float maxDistance)
    {
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.dopplerLevel = 0f;
    }

    private void OnRoomChanged(RoomModule previous, RoomModule current)
    {
        if (vibrationPlayed || !session.IsPlaying || story.Objective != StoryObjective.FindPhone || current != officeRoom || phoneSource == null) return;
        vibrationPlayed = true;
        phoneSource.PlayOneShot(phoneVibration);
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        if (objective != StoryObjective.FindPhone && phoneSource != null)
            phoneSource.Stop();
        if (clockSource != null)
            clockSource.volume = objective == StoryObjective.FindPhone || objective == StoryObjective.Survive
                ? ClockVolume * 1.3f : ClockVolume;
        if (objective == StoryObjective.Escape) BeginDawnTransition();
    }

    private void TrackAnomalyLoop(AudioSource source)
    {
        if (source != null) anomalyLoops.Add(source);
    }

    private void BeginDawnTransition()
    {
        if (dawnReached) return;
        dawnReached = true;
        foreach (TVInterferenceScreen screen in FindObjectsByType<TVInterferenceScreen>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            screen.SetPowered(false);
        if (anomalyFade != null) StopCoroutine(anomalyFade);
        anomalyFade = StartCoroutine(FadeAnomalyAudio());
    }

    private IEnumerator FadeAnomalyAudio()
    {
        const float fadeSeconds = 3f;
        List<float> startingVolumes = new List<float>(anomalyLoops.Count);
        foreach (AudioSource source in anomalyLoops)
            startingVolumes.Add(source != null ? source.volume : 0f);

        for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float amount = Mathf.Clamp01(elapsed / fadeSeconds);
            for (int i = 0; i < anomalyLoops.Count; i++)
                if (anomalyLoops[i] != null)
                    anomalyLoops[i].volume = Mathf.Lerp(startingVolumes[i], 0f, amount);
            yield return null;
        }

        foreach (AudioSource source in anomalyLoops)
            if (source != null) { source.volume = 0f; source.Stop(); }
        anomalyFade = null;
    }

    private void OnSessionChanged(GameSessionState state)
    {
        if (state == GameSessionState.Playing) return;
        foreach (AudioSource source in loops) if (source != null) source.Stop();
    }

    private void Update()
    {
        if (session == null || !session.IsPlaying) return;
        foreach (AudioSource source in loops)
            if (source != null && source.isActiveAndEnabled && !source.isPlaying
                && !(dawnReached && anomalyLoops.Contains(source))) source.Play();
    }

    private void OnDestroy()
    {
        if (anomalyFade != null) StopCoroutine(anomalyFade);
        if (house != null) house.CurrentRoomChanged -= OnRoomChanged;
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
        if (session != null) session.StateChanged -= OnSessionChanged;
    }
}
