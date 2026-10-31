using UnityEngine;
using UnityEngine.Audio;

/// <summary>Conecta al mixer las fuentes de escena y las creadas durante la partida.</summary>
[DisallowMultipleComponent]
public sealed class AudioMixerRouting : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;

    private AudioMixerGroup voiceGroup;
    private float nextScan;

    private void Awake()
    {
        if (sfxGroup != null)
        {
            AudioMixerGroup[] groups = sfxGroup.audioMixer.FindMatchingGroups("Voz");
            if (groups.Length > 0) voiceGroup = groups[0];
        }
        RouteSources();
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
            AlienAmbientAudio alienAudio = source.GetComponent<AlienAmbientAudio>();
            if (voiceGroup != null && alienAudio != null && source == alienAudio.VocalSource)
            {
                source.outputAudioMixerGroup = voiceGroup;
                continue;
            }
            // Conserva el ruteo SFX que ya está serializado en puertas, jugador y aliens.
            if (source.outputAudioMixerGroup != null) continue;
            source.outputAudioMixerGroup = IsAmbience(source.transform) ? ambienceGroup : sfxGroup;
        }
    }

    private static bool IsAmbience(Transform source)
    {
        string name = source.name;
        return name == "LuzExteriorVentana" || name == "LuzVigilanciaTecho"
            || name == "tv__small" || name == "small_radio" || name == "grandfather_clock";
    }
}
