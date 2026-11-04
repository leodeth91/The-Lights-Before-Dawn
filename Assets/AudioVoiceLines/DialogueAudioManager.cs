
using System.Collections;
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
    private Coroutine reproduccionActual;

    public float DuracionActual { get; private set; }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    public void PlayDialogue(int id)
    {
        AudioClip clip = ObtenerAudio(id);

        if (clip == null)
        {
            Debug.LogWarning("No hay un audio asignado para el diálogo " + id);
            StopDialogue();
            return;
        }

        if (reproduccionActual != null)
            StopCoroutine(reproduccionActual);

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();

        DuracionActual = clip.length + tiempoExtraTexto;
        reproduccionActual = StartCoroutine(EsperarFinalAudio());
    }

    private AudioClip ObtenerAudio(int id)
    {
        switch (id)
        {
            case 1: return vienenOtraVez;
            case 2: return estaVezNoMeAtraparan;
            case 3: return noEstan;
            case 4: return movermeEnSilencio;
            case 5: return buscarAyuda;
            case 6: return noHaySenal;
            case 7: return noMeAtraparan;
            case 8: return yaAmanecio;
            case 9: return dondeEstoy;
            default: return null;
        }
    }

    private IEnumerator EsperarFinalAudio()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        reproduccionActual = null;
    }

    public void StopDialogue()
    {
        if (reproduccionActual != null)
        {
            StopCoroutine(reproduccionActual);
            reproduccionActual = null;
        }

        audioSource.Stop();
        DuracionActual = 0f;
    }

    private void OnDisable()
    {
        StopDialogue();
    }
}
