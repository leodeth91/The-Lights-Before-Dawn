using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Funde el ambiente nocturno hacia el perfil visual de la anomalía.</summary>
[DisallowMultipleComponent]
public sealed class AnomalyPostProcessingController : MonoBehaviour
{
    [SerializeField] private string globalNightVolumeName = "POST_GlobalNight";
    [SerializeField] private string anomalyVolumeName = "POST_Anomaly";
    [SerializeField, Min(0.01f)] private float transitionSeconds = 8f;

    private StoryProgression story;
    private Volume globalNightVolume;
    private Volume anomalyVolume;
    private Coroutine transition;

    private void Start()
    {
        story = GetComponent<StoryProgression>();
        ResolveVolumes();
        if (story != null) story.ObjectiveChanged += OnObjectiveChanged;
        ApplyImmediate(story != null && story.AnomalyEnabled ? 1f : 0f);
    }

    private void OnDestroy()
    {
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
    }

    private void ResolveVolumes()
    {
        foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (volume.name == globalNightVolumeName) globalNightVolume = volume;
            else if (volume.name == anomalyVolumeName) anomalyVolume = volume;
        }

        if (globalNightVolume == null || anomalyVolume == null)
            Debug.LogWarning("Falta POST_GlobalNight o POST_Anomaly para la transición visual.", this);
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        float target = objective >= StoryObjective.FindPhone ? 1f : 0f;
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(BlendTo(target));
    }

    private IEnumerator BlendTo(float anomalyTarget)
    {
        if (globalNightVolume == null || anomalyVolume == null) yield break;
        float initialAnomaly = anomalyVolume.weight;
        float initialNight = globalNightVolume.weight;
        float nightTarget = 1f - anomalyTarget;

        for (float time = 0f; time < transitionSeconds; time += Time.deltaTime)
        {
            float amount = Mathf.SmoothStep(0f, 1f, time / transitionSeconds);
            anomalyVolume.weight = Mathf.Lerp(initialAnomaly, anomalyTarget, amount);
            globalNightVolume.weight = Mathf.Lerp(initialNight, nightTarget, amount);
            yield return null;
        }

        ApplyImmediate(anomalyTarget);
        transition = null;
    }

    private void ApplyImmediate(float anomalyWeight)
    {
        if (anomalyVolume != null) anomalyVolume.weight = anomalyWeight;
        if (globalNightVolume != null) globalNightVolume.weight = 1f - anomalyWeight;
    }
}
