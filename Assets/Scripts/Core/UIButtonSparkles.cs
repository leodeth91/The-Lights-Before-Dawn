using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Pequeñas partículas de Image, compatibles con el Canvas Overlay del menú.</summary>
public sealed class UIButtonSparkles : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private RectTransform emitter;
    [SerializeField] private int particleCount = 10;
    [SerializeField] private Color color = new Color(1f, .72f, .37f, .8f);

    private RectTransform[] particles;
    private Vector2[] speeds;
    private float[] remaining;
    private bool hovered;
    private float emissionTime;

    private void Awake()
    {
        if (emitter == null) return;
        particles = new RectTransform[particleCount];
        speeds = new Vector2[particleCount];
        remaining = new float[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            GameObject spark = new GameObject("Destello", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = spark.GetComponent<RectTransform>();
            rect.SetParent(emitter, false);
            rect.sizeDelta = new Vector2(5f, 5f);
            Image image = spark.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            spark.SetActive(false);
            particles[i] = rect;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Emit(4);
    }

    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnPointerClick(PointerEventData eventData) => Emit(8);

    private void Update()
    {
        if (particles == null) return;
        if (hovered)
        {
            emissionTime += Time.unscaledDeltaTime;
            if (emissionTime > .16f)
            {
                emissionTime = 0f;
                Emit(1);
            }
        }
        for (int i = 0; i < particles.Length; i++)
        {
            if (remaining[i] <= 0f) continue;
            remaining[i] -= Time.unscaledDeltaTime;
            particles[i].anchoredPosition += speeds[i] * Time.unscaledDeltaTime;
            particles[i].localScale = Vector3.one * Mathf.Clamp01(remaining[i] / .55f);
            if (remaining[i] <= 0f) particles[i].gameObject.SetActive(false);
        }
    }

    private void Emit(int count)
    {
        if (particles == null) return;
        RectTransform button = transform as RectTransform;
        for (int n = 0; n < count; n++)
        {
            int slot = System.Array.FindIndex(remaining, time => time <= 0f);
            if (slot < 0) break;
            particles[slot].gameObject.SetActive(true);
            particles[slot].anchoredPosition = new Vector2(
                Random.Range(-button.rect.width * .4f, button.rect.width * .4f),
                Random.Range(-button.rect.height * .35f, button.rect.height * .35f));
            speeds[slot] = Random.insideUnitCircle.normalized * Random.Range(45f, 95f);
            remaining[slot] = .55f;
            particles[slot].localScale = Vector3.one;
        }
    }
}
