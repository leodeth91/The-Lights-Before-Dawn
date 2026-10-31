using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(AudioSource))]
public class SFXVariacion : MonoBehaviour
{
    [Header("Clips a alternar")]
    public AudioClip[] clips;

    [Header("Disparo por Teclado")]
    [Tooltip("Tildar solo en el componente que debe sonar con la barra espaciadora")]
    public bool dispararConEspacio = false;

    [Header("Rango de Pitch (Variación)")]
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

    private void Update()
    {
        if (!dispararConEspacio) return;

        bool presionoEspacio = false;

#if ENABLE_INPUT_SYSTEM
        
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            presionoEspacio = true;
        }
#else
        // Respaldo para el Input Manager clásico
        if (Input.GetKeyDown(KeyCode.Space))
        {
            presionoEspacio = true;
        }
#endif

        if (presionoEspacio)
        {
            Reproducir();
        }
    }

    public void Reproducir()
    {
        if (clips == null || clips.Length == 0 || fuente == null) return;

        AudioClip clipElegido = clips[Random.Range(0, clips.Length)];
        fuente.pitch = Random.Range(minPitch, maxPitch);
        float vol = Random.Range(minVolumen, maxVolumen);

        fuente.PlayOneShot(clipElegido, vol);
    }
}