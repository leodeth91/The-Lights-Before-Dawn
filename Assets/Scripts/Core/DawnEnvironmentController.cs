using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;

/// <summary>Convierte la iluminación de la casa al amanecer sin modificar la escena.</summary>
[DisallowMultipleComponent]
public sealed class DawnEnvironmentController : MonoBehaviour
{
    private const float TransitionSeconds = 6f;
    private const float WindowLightFadeSeconds = 1.5f;
    private static readonly Color WarmDaylight = new Color(1f, .84f, .68f);
    private static readonly Color WarmAmbient = new Color(.48f, .40f, .32f);

    private StoryProgression story;
    private readonly List<Light> windowLights = new List<Light>();
    private Light daylight;
    private float daylightStartIntensity;
    private Color daylightStartColor;
    private float ambientStartIntensity;
    private Color ambientStartColor;
    private bool dawnStarted;
    private Coroutine transition;

    private void Start()
    {
        story = StoryProgression.Instance;
        if (story != null) story.ObjectiveChanged += OnObjectiveChanged;
        if (story != null && story.HasDawnArrived) BeginDawn();
    }

    private void OnDestroy()
    {
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        if (objective == StoryObjective.Escape) BeginDawn();
    }

    private void BeginDawn()
    {
        if (dawnStarted) return;
        dawnStarted = true;
        CacheLights();
        StopAlienThreats();

        RenderSettings.ambientMode = AmbientMode.Flat;
        ambientStartColor = RenderSettings.ambientLight;
        ambientStartIntensity = RenderSettings.ambientIntensity;
        if (daylight != null)
        {
            daylightStartIntensity = daylight.enabled ? daylight.intensity : 0f;
            daylightStartColor = daylight.color;
            daylight.shadows = LightShadows.Soft;
            daylight.enabled = true;
        }

        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(FadeToDaylight());
    }

    private void CacheLights()
    {
        foreach (Light lightSource in FindObjectsByType<Light>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (lightSource == null || lightSource.gameObject.scene != gameObject.scene) continue;
            if (lightSource.name == "LuzExteriorVentana") windowLights.Add(lightSource);
            else if (daylight == null && lightSource.name == "Directional Light") daylight = lightSource;
        }

        if (daylight == null)
        {
            GameObject lightObject = new GameObject("LuzNaturalAmanecer");
            lightObject.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            daylight = lightObject.AddComponent<Light>();
            daylight.type = LightType.Directional;
            daylight.intensity = 0f;
            daylight.shadows = LightShadows.Soft;
        }
    }

    private void StopAlienThreats()
    {
        HouseFlowController house = HouseFlowController.Instance;
        foreach (AlienPopulation population in FindObjectsByType<AlienPopulation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (population != null && population.gameObject.scene == gameObject.scene)
                population.enabled = false;

        foreach (AlienController alien in FindObjectsByType<AlienController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (alien == null || alien.gameObject.scene != gameObject.scene) continue;

            GameObject alienObject = alien.gameObject;
            house?.CancelDoorRequest(alienObject);
            house?.ForgetPursuit(alienObject);
            if (alien.Passage != null) house?.ReleasePassage(alien.Passage, alienObject);

            // Detiene su acción antes de apagar el NavMeshAgent. Así el alien queda quieto
            // y no continúa un cruce de puerta que había quedado a medias.
            Vector3 stoppedPosition = alien.transform.position;
            Quaternion stoppedRotation = alien.transform.rotation;
            alien.StopAllCoroutines();
            alien.transform.SetPositionAndRotation(stoppedPosition, stoppedRotation);
            if (alien.Motor != null)
            {
                alien.Motor.SetManualMotionSpeed(0f);
                alien.Motor.Stop();
                NavMeshAgent agent = alien.Motor.Agent;
                if (agent != null && agent.enabled)
                {
                    if (agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                        agent.ResetPath();
                    }
                    agent.enabled = false;
                }
                alien.Motor.enabled = false;
            }

            HousePortalTraveller traveller = alien.GetComponent<HousePortalTraveller>();
            if (traveller != null) traveller.enabled = false;
            AlienAmbientAudio alienAudio = alien.GetComponent<AlienAmbientAudio>();
            if (alienAudio != null) alienAudio.enabled = false;
            foreach (AudioSource source in alienObject.GetComponents<AudioSource>()) source.Stop();
            Collider alienCollider = alien.GetComponent<Collider>();
            if (alienCollider != null) alienCollider.enabled = false;
            alien.enabled = false;
            alienObject.SetActive(false);
        }
        Physics.SyncTransforms();
    }

    private IEnumerator FadeToDaylight()
    {
        for (float elapsed = 0f; elapsed < TransitionSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float amount = Mathf.SmoothStep(0f, 1f, elapsed / TransitionSeconds);
            daylight.intensity = Mathf.Lerp(daylightStartIntensity, .8f, amount);
            daylight.color = Color.Lerp(daylightStartColor, WarmDaylight, amount);
            RenderSettings.ambientLight = Color.Lerp(ambientStartColor, WarmAmbient, amount);
            RenderSettings.ambientIntensity = Mathf.Lerp(ambientStartIntensity, 1f, amount);

            float windowAmount = Mathf.Clamp01(elapsed / WindowLightFadeSeconds);
            foreach (Light windowLight in windowLights)
            {
                if (windowLight == null) continue;
                windowLight.intensity = Mathf.Lerp(windowLight.intensity, 0f, windowAmount);
            }
            yield return null;
        }

        daylight.intensity = .8f;
        daylight.color = WarmDaylight;
        RenderSettings.ambientLight = WarmAmbient;
        RenderSettings.ambientIntensity = 1f;
        foreach (Light windowLight in windowLights)
        {
            if (windowLight == null) continue;
            windowLight.intensity = 0f;
            windowLight.enabled = false;
        }
        transition = null;
    }
}
