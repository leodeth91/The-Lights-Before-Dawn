using System.Collections.Generic;
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
    private sealed class NestedView
    {
        public PortalTrialGate Gate;
        public Camera Camera;
        public RenderTexture Texture;
        public Material SurfaceMaterial;
        public Texture PreviousTexture;
        public bool TextureSwapped;
    }

    private struct TextureOverride
    {
        public Material Material;
        public Texture Previous;
    }

    private static readonly HashSet<PortalTrialGate> ActiveGates = new HashSet<PortalTrialGate>();
    private readonly List<NestedView> nestedViews = new List<NestedView>();
    private readonly HashSet<Camera> nestedCamerasRenderingThisGate = new HashSet<Camera>();
    private readonly List<TextureOverride> terminalOverrides = new List<TextureOverride>();
    private bool terminalOverrideActive;
    public PortalTrialGate Destination => destination;

    private void OnEnable() => ActiveGates.Add(this);

    private void OnDisable()
    {
        ActiveGates.Remove(this);
        RestoreNestedTextures();
        RestoreTerminalTextures();
        if (portalCamera != null) portalCamera.enabled = false;
        foreach (NestedView view in nestedViews)
            if (view.Camera != null) view.Camera.enabled = false;
    }

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
            ownDoorCollider = roomSocket.Door.LeafCollider;
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
        // Se dibujan las otras superficies de portal de la habitación contigua.
        // La superficie de retorno se oculta solo durante esta cámara.
        // La escena de prueba no usa DoorSocket ni ocultamiento del retorno;
        // allí se conserva el render simple que ya funcionaba.
        portalCamera.cullingMask = followDoorState ? viewer.cullingMask : viewer.cullingMask & ~(1 << 2);

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
        RenderPipelineManager.beginCameraRendering += OverridePortalTextures;
        RenderPipelineManager.endCameraRendering += RestorePortalTextures;
    }

    private void HideDestinationDoor(ScriptableRenderContext context, Camera camera)
    {
        if ((camera != portalCamera && !nestedCamerasRenderingThisGate.Contains(camera))
            || destination == null || destinationDoorHidden) return;
        if (destinationDoorRenderers == null)
        {
            DoorSocket otherSocket = destination.GetComponent<DoorSocket>();
            if (otherSocket == null) return;
            // El retorno exige ocultar todo el ensamblaje del marco de destino,
            // pero el suelo de apoyo debe seguir visible en la cámara del portal.
            List<Renderer> hidden = new List<Renderer>();
            foreach (Renderer candidate in otherSocket.GetComponentsInChildren<Renderer>(true))
                if (candidate != null && candidate.name != "PisoUmbral") hidden.Add(candidate);
            destinationDoorRenderers = hidden.ToArray();
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
        if (camera != portalCamera && !nestedCamerasRenderingThisGate.Contains(camera)) return;
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
        if (!visible)
        {
            foreach (NestedView view in nestedViews)
                if (view.Camera != null) view.Camera.enabled = false;
            return;
        }
        portalCamera.fieldOfView = viewer.fieldOfView;
        portalCamera.aspect = viewer.aspect;
        portalCamera.nearClipPlane = .03f;
        portalCamera.farClipPlane = viewer.farClipPlane;
        portalCamera.transform.SetPositionAndRotation(MapPoint(viewer.transform.position), MapRotation(viewer.transform.rotation));
        ApplyExitClipping(portalCamera, destination);
        UpdateNestedViews();
    }

    private static void ApplyExitClipping(Camera camera, PortalTrialGate exit)
    {
        // Reset evita acumular un recorte oblicuo nuevo sobre el del fotograma anterior.
        camera.ResetProjectionMatrix();
        Vector3 clipPosition = exit.transform.position - exit.transform.forward * .02f;
        Vector3 clipNormal = -exit.transform.forward;
        Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
        Vector3 cameraPosition = worldToCamera.MultiplyPoint(clipPosition);
        Vector3 cameraNormal = worldToCamera.MultiplyVector(clipNormal).normalized;
        Vector4 clipPlane = new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z,
            -Vector3.Dot(cameraPosition, cameraNormal));
        camera.projectionMatrix = camera.CalculateObliqueMatrix(clipPlane);
    }

    private void UpdateNestedViews()
    {
        // Las conexiones B pueden desaparecer y reaparecer mientras A sigue abierta.
        // No conservar una cámara/RT por cada antigua instancia del mismo socket.
        for (int i = nestedViews.Count - 1; i >= 0; i--)
            if (nestedViews[i].Gate == null || nestedViews[i].Camera == null)
            {
                ReleaseNestedView(nestedViews[i]);
                nestedViews.RemoveAt(i);
            }
        foreach (NestedView view in nestedViews)
            if (view.Camera != null) view.Camera.enabled = false;

        DoorSocket exitSocket = destination != null ? destination.GetComponent<DoorSocket>() : null;
        RoomModule destinationRoom = exitSocket != null ? exitSocket.Room : null;
        if (destinationRoom == null) return;

        Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(portalCamera);
        foreach (DoorSocket socket in destinationRoom.Sockets)
        {
            if (socket == null || socket == exitSocket) continue;
            PortalTrialGate next = socket.GetComponent<PortalTrialGate>();
            if (next == null || !next.isActiveAndEnabled || next.destination == null
                || next.surface == null || !next.IsVisiblyOpen()) continue;
            Renderer screen = next.surface.GetComponent<Renderer>();
            if (screen == null || !GeometryUtility.TestPlanesAABB(frustum, screen.bounds)) continue;

            NestedView nested = GetOrCreateNestedView(next);
            Camera camera = nested.Camera;
            camera.fieldOfView = viewer.fieldOfView;
            camera.aspect = viewer.aspect;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = viewer.farClipPlane;
            // La segunda perspectiva parte del observador virtual de esta puerta,
            // no de la cámara principal ni de un LateUpdate de orden indeterminado.
            camera.transform.SetPositionAndRotation(
                next.MapPoint(MapPoint(viewer.transform.position)),
                next.MapRotation(MapRotation(viewer.transform.rotation)));
            ApplyExitClipping(camera, next.destination);
            camera.enabled = true;
        }
    }

    private bool IsVisiblyOpen()
    {
        if (!followDoorState) return true;
        DoorSocket socket = roomSocket != null ? roomSocket : GetComponent<DoorSocket>();
        return socket == null || socket.Door == null || socket.Door.State != DoorState.Closed;
    }

    private NestedView GetOrCreateNestedView(PortalTrialGate next)
    {
        foreach (NestedView view in nestedViews)
            if (view.Gate == next && view.Camera != null) return view;

        var nested = new NestedView { Gate = next };
        nested.Texture = new RenderTexture(Mathf.Max(256, viewTexture.width / 2),
            Mathf.Max(144, viewTexture.height / 2), 24, RenderTextureFormat.ARGB32);
        nested.Texture.name = "Vista portal anidada - " + name + " - " + next.name;
        nested.Texture.Create();
        GameObject cameraObject = new GameObject("Camara portal anidada - " + name + " - " + next.name);
        nested.Camera = cameraObject.AddComponent<Camera>();
        nested.Camera.enabled = false;
        nested.Camera.targetTexture = nested.Texture;
        nested.Camera.depth = -20f;
        // El último nivel conserva las superficies: durante su render se vuelven
        // opacas, así un tercer portal nunca revela el cielo exterior.
        nested.Camera.cullingMask = viewer.cullingMask;
        next.nestedCamerasRenderingThisGate.Add(nested.Camera);
        nestedViews.Add(nested);
        return nested;
    }

    private void OverridePortalTextures(ScriptableRenderContext context, Camera camera)
    {
        if (camera == portalCamera)
        {
            foreach (NestedView view in nestedViews)
            {
                if (view.Camera == null || !view.Camera.enabled || view.Gate == null
                    || view.Gate.surface == null) continue;
                view.SurfaceMaterial = view.Gate.surface.GetComponent<Renderer>().material;
                view.PreviousTexture = view.SurfaceMaterial.GetTexture("_BaseMap");
                view.SurfaceMaterial.SetTexture("_BaseMap", view.Texture);
                view.TextureSwapped = true;
            }
            return;
        }

        foreach (NestedView view in nestedViews)
        {
            if (camera != view.Camera) continue;
            RestoreTerminalTextures();
            foreach (PortalTrialGate gate in ActiveGates)
            {
                if (gate == null || gate.surface == null) continue;
                Material material = gate.surface.GetComponent<Renderer>().material;
                terminalOverrides.Add(new TextureOverride
                {
                    Material = material,
                    Previous = material.GetTexture("_BaseMap")
                });
                material.SetTexture("_BaseMap", Texture2D.blackTexture);
            }
            terminalOverrideActive = true;
            return;
        }
    }

    private void RestorePortalTextures(ScriptableRenderContext context, Camera camera)
    {
        if (camera == portalCamera) RestoreNestedTextures();
        else
            foreach (NestedView view in nestedViews)
                if (camera == view.Camera) { RestoreTerminalTextures(); break; }
    }

    private void RestoreNestedTextures()
    {
        foreach (NestedView view in nestedViews)
        {
            if (!view.TextureSwapped || view.SurfaceMaterial == null) continue;
            view.SurfaceMaterial.SetTexture("_BaseMap", view.PreviousTexture);
            view.TextureSwapped = false;
        }
    }

    private void RestoreTerminalTextures()
    {
        if (!terminalOverrideActive) return;
        foreach (TextureOverride item in terminalOverrides)
            if (item.Material != null) item.Material.SetTexture("_BaseMap", item.Previous);
        terminalOverrides.Clear();
        terminalOverrideActive = false;
    }

    private void ReleaseNestedView(NestedView view)
    {
        if (view.Gate != null && view.Camera != null)
            view.Gate.nestedCamerasRenderingThisGate.Remove(view.Camera);
        if (view.Camera != null) Destroy(view.Camera.gameObject);
        if (view.Texture != null) { view.Texture.Release(); Destroy(view.Texture); }
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
        ActiveGates.Remove(this);
        RestoreNestedTextures();
        RestoreTerminalTextures();
        SetOwnDoorLeafSuppressed(false);
        RenderPipelineManager.beginCameraRendering -= HideDestinationDoor;
        RenderPipelineManager.endCameraRendering -= RestoreDestinationDoor;
        RenderPipelineManager.beginCameraRendering -= OverridePortalTextures;
        RenderPipelineManager.endCameraRendering -= RestorePortalTextures;
        RestoreDestinationDoor();
        foreach (NestedView view in nestedViews)
            ReleaseNestedView(view);
        nestedViews.Clear();
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
