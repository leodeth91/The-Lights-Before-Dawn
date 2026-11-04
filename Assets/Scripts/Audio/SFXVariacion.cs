using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SFXVariacion : MonoBehaviour
{
    [Header("Clips a alternar")]
    public AudioClip[] clips;

    [Header("Disparo al aterrizar")]
    [Tooltip("Tildar solo en el componente que debe sonar al aterrizar tras un salto")]
    public bool dispararConEspacio = false;

    [Header("Rango de Pitch (Variación)")]
    [Range(0.8f, 1.2f)] public float minPitch = 0.92f;
    [Range(0.8f, 1.2f)] public float maxPitch = 1.08f;

    [Header("Rango de Volumen")]
    [Range(0.7f, 1.0f)] public float minVolumen = 0.85f;
    [Range(0.7f, 1.0f)] public float maxVolumen = 1.0f;

    private AudioSource fuente;
    private ChildPlayerController player;
    private bool jumpInProgress;
    private int previousClip = -1;

    private void Awake()
    {
        fuente = GetComponent<AudioSource>();
        AudioMixerRouting.Route(fuente, AudioMixerRouting.Bus.Sfx);
        player = GetComponent<ChildPlayerController>();
    }

    private void LateUpdate()
    {
        if (!dispararConEspacio || player == null) return;
        if (player.IsJumping && !player.IsGrounded) jumpInProgress = true;
        if (jumpInProgress && player.IsGrounded)
        {
            jumpInProgress = false;
            Reproducir();
        }
    }

    public void Reproducir()
    {
        if (clips == null || clips.Length == 0 || fuente == null) return;

        int clipIndex = Random.Range(0, clips.Length);
        if (clips.Length > 1 && clipIndex == previousClip)
            clipIndex = (clipIndex + Random.Range(1, clips.Length)) % clips.Length;
        previousClip = clipIndex;
        AudioClip clipElegido = clips[clipIndex];
        fuente.pitch = Random.Range(minPitch, maxPitch);
        float vol = Random.Range(minVolumen, maxVolumen);

        fuente.PlayOneShot(clipElegido, vol);
    }
}
