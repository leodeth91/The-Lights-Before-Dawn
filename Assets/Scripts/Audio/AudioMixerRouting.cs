using UnityEngine;
using UnityEngine.Audio;

/// <summary>Conecta al mixer las fuentes de escena y las creadas durante la partida.</summary>
[DisallowMultipleComponent]
public sealed class AudioMixerRouting : MonoBehaviour
{
    public enum Bus { Music, Sfx, Ambience, Voice }

    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;

    private static AudioMixerRouting instance;
    private AudioMixerGroup musicGroup;
    private AudioMixerGroup voiceGroup;
    private float nextScan;

    private void Awake()
    {
        instance = this;
        if (sfxGroup != null)
        {
            musicGroup = FindGroup("Música");
            voiceGroup = FindGroup("Voz");
        }
        RouteSources();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Route(AudioSource source, Bus bus)
    {
        if (source == null) return;
        if (instance == null) instance = FindFirstObjectByType<AudioMixerRouting>();
        if (instance == null) return;
        if (instance.sfxGroup != null)
        {
            if (instance.musicGroup == null) instance.musicGroup = instance.FindGroup("Música");
            if (instance.voiceGroup == null) instance.voiceGroup = instance.FindGroup("Voz");
        }
        AudioMixerGroup group = bus switch
        {
            Bus.Music => instance.musicGroup,
            Bus.Ambience => instance.ambienceGroup,
            Bus.Voice => instance.voiceGroup,
            _ => instance.sfxGroup
        };
        if (group != null) source.outputAudioMixerGroup = group;
    }

    public static bool TransitionMusicSnapshot(string snapshotName, float seconds)
    {
        if (instance == null) instance = FindFirstObjectByType<AudioMixerRouting>();
        if (instance == null || instance.sfxGroup == null) return false;
        AudioMixerSnapshot snapshot = instance.sfxGroup.audioMixer.FindSnapshot(snapshotName);
        if (snapshot == null) return false;
        snapshot.TransitionTo(Mathf.Max(0f, seconds));
        return true;
    }

    private AudioMixerGroup FindGroup(string name)
    {
        AudioMixerGroup[] groups = sfxGroup.audioMixer.FindMatchingGroups(name);
        return groups.Length > 0 ? groups[0] : null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        RouteSources();
        nextScan = Time.unscaledTime + .25f;
    }

    private void RouteSources()
    {
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            // Conserva el ruteo ya serializado en puertas, jugador y aliens.
            if (source.outputAudioMixerGroup != null) continue;
            source.outputAudioMixerGroup = IsAmbience(source) ? ambienceGroup : sfxGroup;
        }
    }

    private static bool IsAmbience(AudioSource source)
    {
        string name = source.name;
        string clipName = source.clip != null ? source.clip.name : "";
        return name == "LuzExteriorVentana" || name == "LuzVigilanciaTecho"
            || name == "tv__small" || name == "small_radio" || name == "grandfather_clock"
            || name == "Alien Ambience" || name == "FinalAlienAmbience"
            || clipName == "Ambiente de Noche" || clipName == "Ambiente Alien"
            || clipName == "Sonido de Luz" || clipName == "UFO Sonido"
            || clipName == "TV" || clipName == "Reloj Sonando";
    }
}
