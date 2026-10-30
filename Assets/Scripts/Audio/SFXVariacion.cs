using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SFXVariacion : MonoBehaviour
{
    [Header("Clips a alternar")]
    public AudioClip[] clips; // Debe ser "public" para verse en el Inspector

    [Header("Rango de Pitch")]
    [Range(0.8f, 1.2f)] public float minPitch = 0.92f;
    [Range(0.8f, 1.2f)] public float maxPitch = 1.08f;

    [Header("Rango de Volumen")]
    [Range(0.7f, 1.0f)] public float minVolumen = 0.85f;
    [Range(0.7f, 1.0f)] public float maxVolumen = 1.0f;

    private AudioSource fuente;

    private void Awake()
    {
        fuente = GetComponent<AudioSource>();
    }

    public void Reproducir()
    {
        if (clips == null || clips.Length == 0 || fuente == null) return;

        AudioClip clipElegido = clips[Random.Range(0, clips.Length)];
        fuente.pitch = Random.Range(minPitch, maxPitch);
        float volumen = Random.Range(minVolumen, maxVolumen);

        fuente.PlayOneShot(clipElegido, volumen);
    }
}