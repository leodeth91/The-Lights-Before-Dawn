using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Une dos marcos: dibuja la habitación de destino desde la perspectiva del observador
/// y transforma posiciones, orientaciones y direcciones al atravesar el umbral.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class PortalTrialGate : MonoBehaviour
{
    [SerializeField] private PortalTrialGate destination;
    [SerializeField] private Camera viewer;
    [SerializeField] private float openingWidth = 1.08f;
    [SerializeField] private float openingHeight = 1.94f;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);
    private Camera portalCamera;
    private RenderTexture viewTexture;
    private GameObject surface;
    private DoorSocket roomSocket;
    private bool followDoorState;
    private bool suppressOwnDoorLeafWhenOpen;
    private Renderer ownDoorLeaf;
    private Collider ownDoorCollider;
    private bool ownDoorRendererWasEnabled;
    private bool ownDoorColliderWasEnabled;
    private bool ownDoorLeafSuppressed;
    private Renderer[] destinationDoorRenderers;
    private bool[] previousRendererVisibility;
    private bool destinationDoorHidden;
    public PortalTrialGate Destination => destination;

    public void Configure(PortalTrialGate linkedGate, Camera mainViewer, bool hideWhenClosed = false,
        float width = 1.08f, float height = 1.94f, bool hideOwnDoorLeafWhenOpen = false)
    {
        destination = linkedGate;
        viewer = mainViewer;
        followDoorState = hideWhenClosed;
        openingWidth = width;
        openingHeight = height;
        suppressOwnDoorLeafWhenOpen = hideOwnDoorLeafWhenOpen;
    }

    private void Start()
    {
        if (destination == null || viewer == null) { enabled = false; return; }
        roomSocket = GetComponent<DoorSocket>();
        if (suppressOwnDoorLeafWhenOpen && roomSocket != null && roomSocket.Door != null)
        {
            ownDoorLeaf = roomSocket.Door.LeafRenderer;
            ownDoorCollider = ownDoorLeaf != null ? ownDoorLeaf.GetComponent<Collider>() : null;
        }

        int width = followDoorState ? Mathf.Max(320, viewer.pixelWidth / 2)
            : Mathf.Max(640, viewer.pixelWidth);
        int height = followDoorState ? Mathf.Max(180, viewer.pixelHeight / 2)
            : Mathf.Max(360, viewer.pixelHeight);
        viewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        viewTexture.name = "Vista portal - " + name;
        viewTexture.Create();

        GameObject cameraObject = new GameObject("Camara portal - " + name);
        portalCamera = cameraObject.AddComponent<Camera>();
        portalCamera.targetTexture = viewTexture;
        portalCamera.depth = -10f;
        // La cámara de este portal no debe dibujar la superficie del portal de salida:
        // de hacerlo, ve su propia textura en vez de la habitación de destino.
        // Las superficies usan Ignore Raycast (capa 2) en ambas escenas.
        portalCamera.cullingMask = viewer.cullingMask & ~(1 << 2);

        surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "Vista al otro cuarto - " + name;
        surface.layer = 2;
        Destroy(surface.GetComponent<Collider>());
        surface.transform.SetParent(transform, false);
        surface.transform.localPosition = new Vector3(0f, openingHeight * .5f, -.025f);
        surface.transform.localRotation = HalfTurn;
        surface.transform.localScale = new Vector3(openingWidth, openingHeight, 1f);

        Shader shader = Shader.Find("Hidden/PortalTrialScreenSpace");
        if (shader == null) { Debug.LogError("No se encontró un shader para la vista del portal."); enabled = false; return; }
        Material material = new Material(shader) { name = "Vista portal - " + name };
        material.SetTexture("_BaseMap", viewTexture);
        surface.GetComponent<Renderer>().material = material;
        if (followDoorState)
        {
            RenderPipelineManager.beginCameraRendering += HideDestinationDoor;
            RenderPipelineManager.endCameraRendering += RestoreDestinationDoor;
        }
    }

    private void HideDestinationDoor(ScriptableRenderContext context, Camera camera)
    {
        if (camera != portalCamera || destination == null || destinationDoorHidden) return;
        if (destinationDoorRenderers == null)
        {
            DoorSocket otherSocket = destination.GetComponent<DoorSocket>();
            if (otherSocket == null) return;
            destinationDoorRenderers = otherSocket.GetComponentsInChildren<Renderer>(true);
            previousRendererVisibility = new bool[destinationDoorRenderers.Length];
        }
        for (int i = 0; i < destinationDoorRenderers.Length; i++)
        {
            Renderer renderer = destinationDoorRenderers[i];
            if (renderer == null) continue;
            previousRendererVisibility[i] = renderer.forceRenderingOff;
            renderer.forceRenderingOff = true;
        }
        destinationDoorHidden = true;
    }

    private void RestoreDestinationDoor(ScriptableRenderContext context, Camera camera)
    {
        if (camera != portalCamera) return;
        RestoreDestinationDoor();
    }

    private void RestoreDestinationDoor()
    {
        if (!destinationDoorHidden) return;
        for (int i = 0; i < destinationDoorRenderers.Length; i++)
            if (destinationDoorRenderers[i] != null)
                destinationDoorRenderers[i].forceRenderingOff = previousRendererVisibility[i];
        destinationDoorHidden = false;
    }

    private void LateUpdate()
    {
        if (portalCamera == null || destination == null || viewer == null) return;
        bool visible = !followDoorState || roomSocket == null || roomSocket.Door == null
            || roomSocket.Door.State != DoorState.Closed;
        SetOwnDoorLeafSuppressed(suppressOwnDoorLeafWhenOpen && visible);
        if (surface != null) surface.SetActive(visible);
        portalCamera.enabled = visible;
        if (!visible) return;
        portalCamera.fieldOfView = viewer.fieldOfView;
        portalCamera.aspect = viewer.aspect;
        portalCamera.nearClipPlane = .03f;
        portalCamera.farClipPlane = viewer.farClipPlane;
        portalCamera.transform.SetPositionAndRotation(MapPoint(viewer.transform.position), MapRotation(viewer.transform.rotation));

        // Recorta la imagen en el plano de salida: no muestra el espacio vacío que rodea a la habitación.
        Vector3 clipPosition = destination.transform.position - destination.transform.forward * .02f;
        Vector3 clipNormal = -destination.transform.forward;
        Matrix4x4 worldToCamera = portalCamera.worldToCameraMatrix;
        Vector3 cameraPosition = worldToCamera.MultiplyPoint(clipPosition);
        Vector3 cameraNormal = worldToCamera.MultiplyVector(clipNormal).normalized;
        Vector4 clipPlane = new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
            -Vector3.Dot(cameraPosition, cameraNormal));
        portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(clipPlane);
    }

    public Vector3 MapPoint(Vector3 worldPoint)
        => destination.transform.TransformPoint(HalfTurn * transform.InverseTransformPoint(worldPoint));

    // Una dirección se rota entre marcos; no se traslada ni se escala como un punto.
    public Vector3 MapDirection(Vector3 worldDirection)
        => destination.transform.TransformDirection(HalfTurn * transform.InverseTransformDirection(worldDirection));

    public Quaternion MapRotation(Quaternion worldRotation)
        => destination.transform.rotation * HalfTurn * Quaternion.Inverse(transform.rotation) * worldRotation;

    public bool WasCrossed(Vector3 previous, Vector3 current)
    {
        Vector3 before = transform.InverseTransformPoint(previous);
        Vector3 after = transform.InverseTransformPoint(current);
        if (before.z > .015f || after.z <= .015f) return false;
        float fraction = Mathf.Clamp01(-before.z / (after.z - before.z));
        Vector3 crossing = Vector3.Lerp(before, after, fraction);
        return Mathf.Abs(crossing.x) < openingWidth * .5f - .02f
            && crossing.y >= -.08f && crossing.y <= openingHeight;
    }

    public bool IsInOpening(Vector3 worldPoint, float margin = 0f)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        return Mathf.Abs(local.x) <= openingWidth * .5f - margin
            && local.y >= margin && local.y <= openingHeight - margin;
    }

    public Vector3 ExitPoint(Vector3 current, float minimumInteriorDepth = .03f)
    {
        Vector3 local = destination.transform.InverseTransformPoint(MapPoint(current));
        local.z = Mathf.Min(local.z, -minimumInteriorDepth);
        return destination.transform.TransformPoint(local);
    }

    private void SetOwnDoorLeafSuppressed(bool suppress)
    {
        if (ownDoorLeaf == null || suppress == ownDoorLeafSuppressed) return;
        if (suppress)
        {
            ownDoorRendererWasEnabled = ownDoorLeaf.enabled;
            ownDoorColliderWasEnabled = ownDoorCollider != null && ownDoorCollider.enabled;
            ownDoorLeaf.enabled = false;
            if (ownDoorCollider != null) ownDoorCollider.enabled = false;
        }
        else
        {
            ownDoorLeaf.enabled = ownDoorRendererWasEnabled;
            if (ownDoorCollider != null) ownDoorCollider.enabled = ownDoorColliderWasEnabled;
        }
        ownDoorLeafSuppressed = suppress;
    }

    private void OnDestroy()
    {
        SetOwnDoorLeafSuppressed(false);
        RenderPipelineManager.beginCameraRendering -= HideDestinationDoor;
        RenderPipelineManager.endCameraRendering -= RestoreDestinationDoor;
        RestoreDestinationDoor();
        if (portalCamera != null) Destroy(portalCamera.gameObject);
        if (surface != null)
        {
            Renderer renderer = surface.GetComponent<Renderer>();
            if (renderer != null) Destroy(renderer.material);
            Destroy(surface);
        }
        if (viewTexture != null) { viewTexture.Release(); Destroy(viewTexture); }
    }
}
