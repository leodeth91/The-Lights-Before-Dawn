using System;
using System.Collections.Generic;
using System.Text;
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
        PrepareCredits();
    }

    private void PrepareCredits()
    {
        if (creditsPanel == null) return;
        Transform bodyTransform = creditsPanel.transform.Find("ContenidoCreditos");
        Transform backTransform = creditsPanel.transform.Find("Volver");
        Text body = bodyTransform != null ? bodyTransform.GetComponent<Text>() : null;
        Text backLabel = backTransform != null ? backTransform.GetComponentInChildren<Text>(true) : null;
        if (backLabel != null) backLabel.text = "ATRÁS";
        TextAsset content = Resources.Load<TextAsset>("CreditsAssets");
        if (body == null || content == null) return;

        body.fontSize = 23;
        body.alignment = TextAnchor.UpperLeft;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        body.raycastTarget = false;

        RectTransform scrollArea = new GameObject("DesplazamientoCreditos", typeof(RectTransform),
            typeof(ScrollRect)).GetComponent<RectTransform>();
        scrollArea.SetParent(creditsPanel.transform, false);
        scrollArea.anchorMin = scrollArea.anchorMax = new Vector2(.5f, .5f);
        scrollArea.sizeDelta = new Vector2(1500f, 560f);
        scrollArea.anchoredPosition = new Vector2(0f, -10f);

        RectTransform viewport = new GameObject("VentanaCreditos", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(scrollArea, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.sizeDelta = Vector2.zero;
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = Color.clear;

        RectTransform contentRoot = new GameObject("ListaCreditos", typeof(RectTransform))
            .GetComponent<RectTransform>();
        contentRoot.SetParent(viewport, false);
        contentRoot.anchorMin = contentRoot.anchorMax = new Vector2(.5f, 1f);
        contentRoot.pivot = new Vector2(.5f, 1f);
        contentRoot.sizeDelta = new Vector2(1400f, 0f);
        contentRoot.anchoredPosition = Vector2.zero;

        float height = 0f;
        List<string> sections = SplitCredits(content.text);
        for (int i = 0; i < sections.Count; i++)
        {
            Text section = i == 0 ? body : new GameObject("Creditos_" + i,
                typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            section.transform.SetParent(contentRoot, false);
            section.font = body.font;
            section.color = body.color;
            section.fontSize = body.fontSize;
            section.alignment = TextAnchor.UpperLeft;
            section.horizontalOverflow = HorizontalWrapMode.Wrap;
            section.verticalOverflow = VerticalWrapMode.Overflow;
            section.raycastTarget = false;
            section.text = sections[i];
            RectTransform sectionRect = section.rectTransform;
            sectionRect.anchorMin = sectionRect.anchorMax = new Vector2(.5f, 1f);
            sectionRect.pivot = new Vector2(.5f, 1f);
            sectionRect.sizeDelta = new Vector2(1400f, 0f);
            sectionRect.anchoredPosition = new Vector2(0f, -height);
            float sectionHeight = section.preferredHeight + 16f;
            sectionRect.sizeDelta = new Vector2(1400f, sectionHeight);
            height += sectionHeight;
        }
        contentRoot.sizeDelta = new Vector2(1400f, Mathf.Max(560f, height));

        ScrollRect scroll = scrollArea.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = contentRoot;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 100f;
        scroll.verticalNormalizedPosition = 1f;
    }

    private static List<string> SplitCredits(string text)
    {
        List<string> sections = new List<string>();
        StringBuilder section = new StringBuilder();
        foreach (string paragraph in text.Replace("\r\n", "\n").Split(new[] { "\n\n" },
                     StringSplitOptions.None))
        {
            if (section.Length > 0 && section.Length + paragraph.Length > 2200)
            {
                sections.Add(section.ToString());
                section.Clear();
            }
            if (section.Length > 0) section.Append("\n\n");
            section.Append(paragraph);
        }
        if (section.Length > 0) sections.Add(section.ToString());
        return sections;
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
