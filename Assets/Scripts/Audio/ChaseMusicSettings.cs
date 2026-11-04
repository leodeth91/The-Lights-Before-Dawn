using UnityEngine;

/// <summary>Referencia estable para la música de persecución sin depender de la escena.</summary>
public sealed class ChaseMusicSettings : ScriptableObject
{
    public const string ResourcePath = "Audio/ChaseMusicSettings";

    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = .42f;
    [SerializeField, Min(.1f)] private float fadeInSeconds = .35f;
    [SerializeField, Min(.1f)] private float fadeOutSeconds = 3f;

    public AudioClip Clip => clip;
    public float Volume => volume;
    public float FadeInSeconds => fadeInSeconds;
    public float FadeOutSeconds => fadeOutSeconds;
}
