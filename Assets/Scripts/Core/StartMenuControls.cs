using UnityEngine;
using UnityEngine.UI;

/// <summary>Solo presenta los créditos; la sesión sigue siendo dueña de iniciar y cerrar el juego.</summary>
public sealed class StartMenuControls : MonoBehaviour
{
    [SerializeField] private GameObject creditsPanel;
    private Texture2D buttonTexture;
    private Sprite buttonSprite;

    private void Awake()
    {
        buttonTexture = new Texture2D(32, 64, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 32; x++)
            {
                float shade = Mathf.Lerp(.20f, .10f, y / 63f);
                if (y >= 60) shade = .31f;
                else if (y <= 2 || x <= 1 || x >= 30) shade = .27f;
                buttonTexture.SetPixel(x, y, new Color(shade, shade, shade, .96f));
            }
        buttonTexture.Apply();
        buttonSprite = Sprite.Create(buttonTexture, new Rect(0, 0, 32, 64), new Vector2(.5f, .5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(3, 4, 3, 4));
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            Image background = button.GetComponent<Image>();
            if (background == null) continue;
            background.sprite = buttonSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null) { label.fontSize = 27; label.fontStyle = FontStyle.Normal; }
            if (button.transform.parent == transform)
            {
                RectTransform rect = button.transform as RectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
                rect.pivot = new Vector2(1f, .5f);
                rect.anchoredPosition = new Vector2(-40f, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(560f, 88f);
            }
        }
    }

    private void OnDestroy()
    {
        if (buttonSprite != null) Destroy(buttonSprite);
        if (buttonTexture != null) Destroy(buttonTexture);
    }

    public void ShowCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(true);
    }

    public void HideCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }
}
