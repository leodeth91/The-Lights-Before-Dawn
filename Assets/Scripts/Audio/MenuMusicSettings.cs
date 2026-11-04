using UnityEngine;

/// <summary>Referencia estable para la música del menú sin modificar la escena principal.</summary>
public sealed class MenuMusicSettings : ScriptableObject
{
    public const string ResourcePath = "Audio/MenuMusicSettings";

    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Min(.1f)] private float fadeInSeconds = 1.25f;
    [SerializeField, Min(.1f)] private float fadeOutSeconds = 1.25f;

    public AudioClip Clip => clip;
    public float Volume => volume;
    public float FadeInSeconds => fadeInSeconds;
    public float FadeOutSeconds => fadeOutSeconds;
}
