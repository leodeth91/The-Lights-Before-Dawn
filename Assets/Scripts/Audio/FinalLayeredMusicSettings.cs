using UnityEngine;

/// <summary>Clips y niveles de las cinco capas exportadas de Distancias.</summary>
public sealed class FinalLayeredMusicSettings : ScriptableObject
{
    public const string ResourcePath = "Audio/FinalLayeredMusicSettings";
    [SerializeField] private AudioClip bass, drums, piano, choir, guitar;
    [SerializeField, Range(0f, 1f)] private float bassVolume = .5f, drumsVolume = .45f,
        pianoVolume = .4f, choirVolume = .4f, guitarVolume = .5f;

    public bool IsComplete => bass != null && drums != null && piano != null
        && choir != null && guitar != null;
    public AudioClip ClipAt(int index) => index switch
    {
        0 => bass, 1 => drums, 2 => piano, 3 => choir, 4 => guitar, _ => null
    };
    public float VolumeAt(int index) => index switch
    {
        0 => bassVolume, 1 => drumsVolume, 2 => pianoVolume,
        3 => choirVolume, 4 => guitarVolume, _ => 0f
    };
}
