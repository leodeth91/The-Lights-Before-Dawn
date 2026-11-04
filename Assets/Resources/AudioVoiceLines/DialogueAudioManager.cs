
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DialogueAudioManager : MonoBehaviour
{
    [Header("Audios de diálogos")]
    [SerializeField] private AudioClip vienenOtraVez;
    [SerializeField] private AudioClip estaVezNoMeAtraparan;
    [SerializeField] private AudioClip noEstan;
    [SerializeField] private AudioClip movermeEnSilencio;
    [SerializeField] private AudioClip buscarAyuda;
    [SerializeField] private AudioClip noHaySenal;
    [SerializeField] private AudioClip noMeAtraparan;
    [SerializeField] private AudioClip yaAmanecio;
    [SerializeField] private AudioClip dondeEstoy;

    [Header("Configuración")]
    [SerializeField] private float tiempoExtraTexto = 1f;

    private AudioSource audioSource;
    public float DuracionActual { get; private set; }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        AudioMixerRouting.Route(audioSource, AudioMixerRouting.Bus.Voice);
    }

    public void PlayDialogue(int id)
    {
        AudioClip clip = ObtenerAudio(id);

        if (clip == null) return;

        audioSource.Stop();
        audioSource.clip = clip;
        AudioMixerRouting.Route(audioSource, AudioMixerRouting.Bus.Voice);
        audioSource.Play();

        DuracionActual = clip.length + tiempoExtraTexto;
    }

    private AudioClip ObtenerAudio(int id)
    {
        AudioClip assigned = id switch
        {
            1 => vienenOtraVez,
            2 => estaVezNoMeAtraparan,
            3 => noEstan,
            4 => movermeEnSilencio,
            5 => buscarAyuda,
            6 => noHaySenal,
            7 => noMeAtraparan,
            8 => yaAmanecio,
            9 => dondeEstoy,
            _ => null
        };
        if (assigned != null) return assigned;

        string[] names = {
            "Voz-001 - Vienen otra vez", "Voz-002 - Esta vez no", "Voz-003 - No estan",
            "Voz-004 - Debo moverme", "Voz-005 - Que esta pasando", "Voz-006 - No hay señal",
            "Voz-007 - No me atraparan", "Voz-008 - Ya amanecio", "Voz-009 - Donde estoy"
        };
        return id >= 1 && id <= names.Length
            ? Resources.Load<AudioClip>("AudioVoiceLines/" + names[id - 1]) : null;
    }

    public void StopDialogue()
    {
        audioSource.Stop();
        DuracionActual = 0f;
    }

    private void OnDisable()
    {
        StopDialogue();
    }
}
