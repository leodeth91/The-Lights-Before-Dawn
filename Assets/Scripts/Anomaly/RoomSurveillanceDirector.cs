using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Vigila una habitación a la vez. Solo transmite su identidad al primer alien.</summary>
[DisallowMultipleComponent]
public sealed class RoomSurveillanceDirector : MonoBehaviour
{
    private const float ScanSeconds = 4f;
    private const float BetweenRooms = 1.2f;
    private const float Boost = 1.15f;

    private sealed class ScanLight
    {
        public Light Light;
        public AudioSource Audio;
        public float BaseIntensity;
        public Quaternion BaseLocalRotation;
        public bool BaseEnabled;
    }

    private sealed class ScanRoom
    {
        public RoomModule Room;
        public readonly List<ScanLight> Lights = new List<ScanLight>();
    }

    private readonly List<ScanRoom> rooms = new List<ScanRoom>();
    private HouseFlowController house;
    private AlienController receiver;
    private StoryProgression story;
    private GameSessionManager session;
    private AudioClip vibration;
    private bool ownsVibration;
    private Coroutine routine;

    public void Initialize(HouseFlowController flow, AlienController alien, AudioClip vibrationClip)
    {
        house = flow;
        receiver = alien;
        story = StoryProgression.Instance;
        session = GameSessionManager.Instance;
        ownsVibration = vibrationClip == null;
        vibration = ownsVibration ? CreateVibration() : vibrationClip;
        foreach (RoomModule room in house.ModuleById.Values)
        {
            if (room == null) continue;
            var scan = new ScanRoom { Room = room };
            foreach (Light light in room.GetComponentsInChildren<Light>(true))
                if (light.name == "LuzExteriorVentana") scan.Lights.Add(Prepare(light));
            if (scan.Lights.Count == 0)
            {
                Light ceiling = null;
                foreach (Light light in room.GetComponentsInChildren<Light>(true))
                    if (light.name == "LuzHabitacion") { ceiling = light; break; }
                if (ceiling != null) scan.Lights.Add(PrepareCeiling(room, ceiling));
            }
            if (scan.Lights.Count > 0) rooms.Add(scan);
        }
        rooms.Sort((a, b) => string.CompareOrdinal(a.Room.RoomId, b.Room.RoomId));
        if (story != null) story.ObjectiveChanged += OnObjectiveChanged;
        if (session != null) session.StateChanged += OnSessionChanged;
        TryStart();
    }

    private ScanLight Prepare(Light light)
    {
        var audio = light.gameObject.AddComponent<AudioSource>();
        audio.clip = vibration;
        audio.loop = true;
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = .8f;
        audio.maxDistance = 9f;
        audio.dopplerLevel = 0f;
        return new ScanLight { Light = light, Audio = audio, BaseIntensity = light.intensity,
            BaseLocalRotation = light.transform.localRotation, BaseEnabled = light.enabled };
    }

    private ScanLight PrepareCeiling(RoomModule room, Light fixture)
    {
        Vector3 origin = room.transform.TransformPoint(room.LocalVolumeCenter
            + Vector3.up * (room.LocalVolumeSize.y * .5f - .15f));
        origin.x = fixture.transform.position.x;
        origin.z = fixture.transform.position.z;
        foreach (Transform child in room.GetComponentsInChildren<Transform>(true))
        {
            if (!child.name.ToLowerInvariant().Contains("ceiling_light")) continue;
            Renderer visual = child.GetComponentInChildren<Renderer>(true);
            origin = visual != null ? visual.bounds.center : child.position;
            break;
        }
        var root = new GameObject("LuzVigilanciaTecho");
        root.transform.SetParent(room.transform, true);
        root.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(Vector3.down));
        Light light = root.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(.65f, .8f, 1f);
        light.range = 6f;
        light.spotAngle = 72f;
        light.innerSpotAngle = 45f;
        light.shadows = LightShadows.Soft;
        light.intensity = 0f;
        light.enabled = false;
        return Prepare(light);
    }

    private void OnObjectiveChanged(StoryObjective _) => TryStart();
    private void OnSessionChanged(GameSessionState state)
    {
        if (state == GameSessionState.Playing) TryStart();
        else StopScan();
    }

    private void TryStart()
    {
        if (routine == null && story != null && story.AnomalyEnabled
            && session != null && session.IsPlaying && rooms.Count > 0)
            routine = StartCoroutine(ScanLoop());
    }

    private IEnumerator ScanLoop()
    {
        while (session != null && session.IsPlaying && story != null && story.AnomalyEnabled)
        {
            foreach (ScanRoom room in rooms)
            {
                float start = Time.time;
                bool checkedOccupancy = false;
                foreach (ScanLight light in room.Lights)
                {
                    light.Light.enabled = true;
                    light.Audio.volume = .8f / Mathf.Sqrt(room.Lights.Count);
                    if (room.Room.gameObject.activeInHierarchy) light.Audio.Play();
                }
                while (Time.time - start < ScanSeconds && session.IsPlaying)
                {
                    float elapsed = Time.time - start;
                    float strength = Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / ScanSeconds));
                    foreach (ScanLight light in room.Lights)
                    {
                        float increase = Mathf.Max(Boost, Mathf.Min(4f, light.BaseIntensity * .75f));
                        light.Light.intensity = light.BaseIntensity + increase * strength;
                        float drift = (Mathf.PerlinNoise(elapsed * 2.4f, light.Light.GetInstanceID() * .001f) - .5f) * 8f;
                        light.Light.transform.localRotation = light.BaseLocalRotation
                            * Quaternion.Euler(drift, drift * .65f, 0f);
                    }
                    if (!checkedOccupancy && elapsed >= ScanSeconds * .5f)
                    {
                        checkedOccupancy = true;
                        if (house.CurrentRoom == room.Room) receiver?.ReceiveSurveillanceClue(room.Room);
                    }
                    yield return null;
                }
                Restore(room);
                if (!session.IsPlaying) break;
                yield return new WaitForSeconds(BetweenRooms);
            }
        }
        routine = null;
    }

    private static void Restore(ScanRoom room)
    {
        foreach (ScanLight light in room.Lights)
        {
            if (light.Light == null) continue;
            light.Light.intensity = light.BaseIntensity;
            light.Light.transform.localRotation = light.BaseLocalRotation;
            light.Light.enabled = light.BaseEnabled;
            if (light.Audio != null) light.Audio.Stop();
        }
    }

    private void StopScan()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        foreach (ScanRoom room in rooms) Restore(room);
    }

    private static AudioClip CreateVibration()
    {
        const int rate = 22050;
        float[] samples = new float[rate * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float pulse = .55f + .45f * Mathf.Sin(t * Mathf.PI * 14f);
            samples[i] = .42f * pulse * (Mathf.Sin(t * Mathf.PI * 130f)
                + .3f * Mathf.Sin(t * Mathf.PI * 238f)
                + .16f * Mathf.Sin(t * Mathf.PI * 490f));
        }
        AudioClip clip = AudioClip.Create("VibracionVigilancia", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        StopScan();
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
        if (session != null) session.StateChanged -= OnSessionChanged;
        if (ownsVibration && vibration != null) Destroy(vibration);
    }
}
