using UnityEngine;
using UnityEngine.AI;

public enum AlienVocalization { Question, Response, Call, Chase }

/// <summary>Reproduce los clips asignados al alien. Sus pasos y voces no alertan al compañero por sí mismos.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AlienMotor), typeof(AudioSource))]
public sealed class AlienAmbientAudio : MonoBehaviour
{
    // Avisa a la apariencia cuándo empieza una voz. La animación no decide qué escucha la IA.
    public event System.Action<AlienVocalization, float> VocalizationStarted;
    [Header("Audio clips")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip questionClip;
    [SerializeField] private AudioClip responseClip;
    [SerializeField] private AudioClip negativeResponseClip;
    [SerializeField] private AudioClip callClip;
    [SerializeField] private AudioClip chaseClip;

    [Header("Footsteps")]
    [SerializeField, Min(0.2f)] private float stepDistance = 0.62f;
    [SerializeField, Range(0f, 1f)] private float stepVolume = 0.34f;
    [SerializeField, Min(0.1f)] private float teleportResetDistance = 1.5f;

    [Header("Vocalizations")]
    [SerializeField, Range(0f, 1f)] private float vocalVolume = 0.46f;

    [Header("3D sources")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource vocalSource;

    private AlienMotor motor;
    private Vector3 previousPosition;
    private float travelledSinceStep;
    private bool positionInitialized;
    private System.Random variationRandom;

    public AudioClip FootstepClip => footstepClip;
    public AudioClip QuestionClip => questionClip;
    public AudioClip ResponseClip => responseClip;
    public AudioClip NegativeResponseClip => negativeResponseClip;
    public AudioClip CallClip => callClip;
    public AudioClip ChaseClip => chaseClip;
    public AudioSource VocalSource => vocalSource;
    public bool IsReady => QuestionClip != null && ResponseClip != null && NegativeResponseClip != null
        && CallClip != null && ChaseClip != null
        && footstepSource != null && vocalSource != null
        && Mathf.Approximately(footstepSource.spatialBlend, 1f)
        && Mathf.Approximately(vocalSource.spatialBlend, 1f);

    private void Awake()
    {
        motor = GetComponent<AlienMotor>();
        variationRandom = new System.Random(unchecked(GetInstanceID() * 397 ^ 20260912));
        EnsureAudioSources();
        ResetTravelTracking();
    }

    private void LateUpdate()
    {
        if (!IsGameRunning())
        {
            ResetTravelTracking();
            return;
        }

        if (footstepClip != null) UpdateFootsteps();
    }

    private void UpdateFootsteps()
    {
        if (!positionInitialized)
        {
            ResetTravelTracking();
            return;
        }

        Vector3 displacement = transform.position - previousPosition;
        previousPosition = transform.position;
        displacement.y = 0f;
        float distance = displacement.magnitude;
        if (distance > teleportResetDistance)
        {
            travelledSinceStep = 0f;
            return;
        }

        NavMeshAgent agent = motor != null ? motor.Agent : null;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh
            || agent.velocity.sqrMagnitude < 0.0025f || distance < 0.0001f)
        {
            return;
        }

        travelledSinceStep += distance;
        if (travelledSinceStep < stepDistance) return;
        travelledSinceStep %= stepDistance;
        PlayFootstep();
    }

    public void PlayFootstep()
    {
        AudioClip clip = FootstepClip;
        if (footstepSource == null || clip == null) return;
        footstepSource.pitch = NextFloat(0.88f, 1.01f);
        footstepSource.PlayOneShot(clip, stepVolume);
    }

    public void PlayQuestionVocalization() => PlayVoice(AlienVocalization.Question, QuestionClip, .95f, 1.05f, 1.05f);
    public void PlayResponseVocalization() => PlayVoice(AlienVocalization.Response, ResponseClip, .94f, 1.06f, .98f);
    public void PlayNegativeResponseVocalization() => PlayVoice(AlienVocalization.Response, NegativeResponseClip, .94f, 1.06f, .98f);
    public void PlayCallVocalization() => PlayVoice(AlienVocalization.Call, CallClip, .91f, 1.04f, 1.14f);
    public void PlayChaseVocalization() => PlayVoice(AlienVocalization.Chase, ChaseClip, .93f, 1.08f, 1.18f);

    private void PlayVoice(AlienVocalization kind, AudioClip clip, float pitchMin, float pitchMax,
        float volumeMultiplier)
    {
        if (vocalSource == null || clip == null) return;
        vocalSource.Stop();
        vocalSource.pitch = NextFloat(pitchMin, pitchMax);
        vocalSource.PlayOneShot(clip, vocalVolume * volumeMultiplier);
        VocalizationStarted?.Invoke(kind, clip.length / Mathf.Max(.01f, vocalSource.pitch));
    }

    public void EnsureAudioSources()
    {
        AudioSource[] existing = GetComponents<AudioSource>();
        if (footstepSource == null)
        {
            footstepSource = existing.Length > 0
                ? existing[0] : gameObject.AddComponent<AudioSource>();
        }
        if (vocalSource == null || vocalSource == footstepSource)
        {
            vocalSource = existing.Length > 1
                ? existing[1] : gameObject.AddComponent<AudioSource>();
        }

        ConfigureSource(footstepSource, 0.9f, 14f);
        ConfigureSource(vocalSource, 1.2f, 18f);
        AudioMixerRouting.Route(footstepSource, AudioMixerRouting.Bus.Sfx);
        AudioMixerRouting.Route(vocalSource, AudioMixerRouting.Bus.Voice);
    }

    private static void ConfigureSource(AudioSource source, float minimumDistance,
        float maximumDistance)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minimumDistance;
        source.maxDistance = maximumDistance;
    }

    private bool IsGameRunning()
    {
        return GameSessionManager.Instance == null || GameSessionManager.Instance.IsPlaying;
    }

    private float NextFloat(float minimum, float maximum)
    {
        if (variationRandom == null)
            variationRandom = new System.Random(unchecked(GetInstanceID() * 397 ^ 20260912));
        return Mathf.Lerp(minimum, maximum, (float)variationRandom.NextDouble());
    }

    private void ResetTravelTracking()
    {
        previousPosition = transform.position;
        travelledSinceStep = 0f;
        positionInitialized = true;
    }

    private void OnEnable()
    {
        ResetTravelTracking();
    }

    private void OnDisable()
    {
        travelledSinceStep = 0f;
    }
}
