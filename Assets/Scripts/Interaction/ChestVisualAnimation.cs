using System.Collections;
using UnityEngine;

/// <summary>Reproduce la apertura del GLB siguiendo al baul funcional, tambien cuando lo inspecciona un alien.</summary>
[DisallowMultipleComponent]
public sealed class ChestVisualAnimation : MonoBehaviour
{
    [SerializeField] private GameObject model;
    [SerializeField] private AnimationClip sourceClip;
    [SerializeField, Min(0f)] private float closedClipTime = 2.2f;
    [SerializeField, Min(0.05f)] private float openClipTime = 4f;
    [SerializeField, Min(0.05f)] private float openingDuration = 0.7f;
    [SerializeField, Min(0.05f)] private float closingDuration = 0.28f;
    [SerializeField] private HingedHideFurniture hinge;
    private Coroutine routine;
    private float currentTime;

    public void Configure(GameObject visualModel, AnimationClip clip, float closedTime, float openTime)
    {
        model = visualModel;
        sourceClip = clip;
        closedClipTime = Mathf.Clamp(closedTime, 0f, clip != null ? clip.length : closedTime);
        openClipTime = Mathf.Clamp(openTime, closedClipTime + 0.05f, clip != null ? clip.length : openTime);
        currentTime = closedClipTime;
        Sample();
    }

    public void SetHinge(HingedHideFurniture furniture)
    {
        if (isActiveAndEnabled && hinge != null) hinge.OpenStateChanged -= HandleOpenStateChanged;
        hinge = furniture;
        if (isActiveAndEnabled && hinge != null) hinge.OpenStateChanged += HandleOpenStateChanged;
    }

    private void OnEnable()
    {
        if (hinge != null) hinge.OpenStateChanged += HandleOpenStateChanged;
        currentTime = hinge != null && hinge.IsOpen ? openClipTime : closedClipTime;
        Sample();
    }

    private void OnDisable()
    {
        if (hinge != null) hinge.OpenStateChanged -= HandleOpenStateChanged;
        if (routine != null) { StopCoroutine(routine); routine = null; }
    }

    private void HandleOpenStateChanged(bool open)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(open ? openClipTime : closedClipTime));
    }

    private IEnumerator Animate(float destination)
    {
        float start = currentTime;
        bool opening = destination > start;
        float duration = Mathf.Max(0.05f, Mathf.Abs(destination - start)
            / Mathf.Max(0.05f, openClipTime - closedClipTime)
            * (opening ? openingDuration : closingDuration));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float amount = Mathf.Clamp01(elapsed / duration);
            amount = amount * amount * (3f - 2f * amount);
            currentTime = Mathf.Lerp(start, destination, amount);
            Sample();
            yield return null;
        }
        currentTime = destination;
        Sample();
        routine = null;
    }

    private void Sample()
    {
        if (model != null && sourceClip != null)
            sourceClip.SampleAnimation(model, Mathf.Clamp(currentTime, 0f, sourceClip.length));
    }
}
