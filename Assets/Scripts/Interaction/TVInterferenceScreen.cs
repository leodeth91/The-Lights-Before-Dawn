using System;
using UnityEngine;

/// <summary>Interferencia procedural en la malla de pantalla del televisor importado.</summary>
[DisallowMultipleComponent]
public sealed class TVInterferenceScreen : MonoBehaviour
{
    [SerializeField, Range(2f, 30f)] private float framesPerSecond = 14f;
    [SerializeField, Range(0.1f, 2f)] private float brightness = 0.85f;
    [SerializeField, Range(0f, 1f)] private float contrast = 0.65f;
    [SerializeField] private bool activeOnStart = true;

    private const int Width = 128;
    private const int Height = 96;
    private Renderer screen;
    private Material originalMaterial;
    private Material interferenceMaterial;
    private Texture2D noiseTexture;
    private Color32[] pixels;
    private System.Random random;
    private float nextFrameTime;
    private int frame;

    private void Awake()
    {
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
        {
            if (candidate.name.Contains("TV_Small_Screen_Off"))
            {
                screen = candidate;
                break;
            }
        }
        if (screen == null) { Debug.LogWarning("No se encontró la malla de pantalla del televisor.", this); return; }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) { Debug.LogWarning("No se encontró el shader URP/Unlit.", this); return; }
        originalMaterial = screen.sharedMaterial;
        interferenceMaterial = new Material(shader) { name = "TV Interferencia (instancia)" };
        noiseTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
        {
            name = "TV Interferencia",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        pixels = new Color32[Width * Height];
        random = new System.Random(271828);
        interferenceMaterial.SetTexture("_BaseMap", noiseTexture);
        interferenceMaterial.SetColor("_BaseColor", Color.white);
        if (activeOnStart) screen.sharedMaterial = interferenceMaterial;
        DrawFrame();
    }

    private void Update()
    {
        if (screen == null || noiseTexture == null || !activeOnStart || Time.unscaledTime < nextFrameTime) return;
        nextFrameTime = Time.unscaledTime + 1f / framesPerSecond;
        DrawFrame();
    }

    private void DrawFrame()
    {
        int brightLine = random.Next(Height);
        int drift = frame++ * 3;
        for (int y = 0; y < Height; y++)
        {
            float scan = ((y + drift) % 5 == 0) ? 0.72f : 1f;
            float streak = Math.Abs(y - brightLine) < 2 ? 1.3f : 1f;
            for (int x = 0; x < Width; x++)
            {
                float grain = (float)random.NextDouble();
                float value = Mathf.Clamp01((0.5f + (grain - 0.5f) * contrast) * scan * streak * brightness);
                byte shade = (byte)(value * 255f);
                pixels[y * Width + x] = new Color32(shade, shade, shade, 255);
            }
        }
        noiseTexture.SetPixels32(pixels);
        noiseTexture.Apply(false, false);
    }

    private void OnDestroy()
    {
        if (screen != null && screen.sharedMaterial == interferenceMaterial) screen.sharedMaterial = originalMaterial;
        if (interferenceMaterial != null) Destroy(interferenceMaterial);
        if (noiseTexture != null) Destroy(noiseTexture);
    }
}
