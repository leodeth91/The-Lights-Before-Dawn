using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Reproduce el video del menú detrás de los textos de la pantalla de inicio.
/// La imagen conserva su proporción y recorta solo los bordes necesarios para llenar el panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class StartMenuVideoBackground : MonoBehaviour
{
    [SerializeField] private VideoClip videoClip;

    private RawImage backgroundImage;
    private VideoPlayer videoPlayer;
    private RectTransform panelRect;
    private Vector2 lastPanelSize;

    private void Awake()
    {
        panelRect = transform as RectTransform;
        CreateVideoLayer();
    }

    private void OnEnable()
    {
        if (videoPlayer == null || videoClip == null) return;

        videoPlayer.clip = videoClip;
        videoPlayer.Prepare();
    }

    private void Update()
    {
        if (videoPlayer == null || backgroundImage == null) return;

        if (videoPlayer.texture != null && backgroundImage.texture != videoPlayer.texture)
            backgroundImage.texture = videoPlayer.texture;

        Vector2 panelSize = panelRect != null ? panelRect.rect.size : Vector2.zero;
        if (panelSize != lastPanelSize)
        {
            lastPanelSize = panelSize;
            UpdateCrop();
        }
    }

    private void OnDisable()
    {
        if (videoPlayer != null) videoPlayer.Stop();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.prepareCompleted -= HandlePrepared;
    }

    private void CreateVideoLayer()
    {
        GameObject layer = new GameObject(
            "VideoFondoInicio",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(VideoPlayer));

        RectTransform layerRect = layer.GetComponent<RectTransform>();
        layerRect.SetParent(transform, false);
        layerRect.SetAsFirstSibling();
        layerRect.anchorMin = Vector2.zero;
        layerRect.anchorMax = Vector2.one;
        layerRect.anchoredPosition = Vector2.zero;
        layerRect.sizeDelta = Vector2.zero;

        backgroundImage = layer.GetComponent<RawImage>();
        backgroundImage.raycastTarget = false;
        backgroundImage.color = Color.white;

        videoPlayer = layer.GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.prepareCompleted += HandlePrepared;
    }

    private void HandlePrepared(VideoPlayer preparedPlayer)
    {
        if (!isActiveAndEnabled) return;

        backgroundImage.texture = preparedPlayer.texture;
        UpdateCrop();
        preparedPlayer.Play();
    }

    private void UpdateCrop()
    {
        if (backgroundImage == null || panelRect == null || videoClip == null) return;

        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;
        float videoWidth = videoPlayer != null && videoPlayer.width > 0 ? videoPlayer.width : videoClip.width;
        float videoHeight = videoPlayer != null && videoPlayer.height > 0 ? videoPlayer.height : videoClip.height;
        if (panelWidth <= 0f || panelHeight <= 0f || videoWidth <= 0f || videoHeight <= 0f) return;

        float panelAspect = panelWidth / panelHeight;
        float videoAspect = videoWidth / videoHeight;

        if (videoAspect > panelAspect)
        {
            float visibleWidth = panelAspect / videoAspect;
            backgroundImage.uvRect = new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
        }
        else
        {
            float visibleHeight = videoAspect / panelAspect;
            backgroundImage.uvRect = new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight);
        }
    }
}
