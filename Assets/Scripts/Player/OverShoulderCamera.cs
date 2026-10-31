using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Cámara sobre el hombro fuera de los escondites y en primera persona dentro de ellos.</summary>
public sealed class OverShoulderCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public InputActionAsset inputActions;
    public Vector3 targetOffset = new Vector3(0f, 0.86f, 0f);

    [Header("Shoulder framing")]
    public float distance = 2.80f;
    public float shoulderOffset = 0.46f;
    public float cameraHeight = 0.25f;
    public float positionSmoothing = 14f;
    public float rotationSmoothing = 18f;

    [Header("Look")]
    public float mouseSensitivity = 0.08f;
    public float minimumPitch = -12f;
    public float maximumPitch = 32f;
    public bool lockCursorOnPlay = true;

    [Header("Collision")]
    public float collisionRadius = 0.16f;
    public float collisionPadding = 0.08f;
    public LayerMask collisionMask = ~(1 << 2);

    [Header("Hiding view")]
    public Vector3 hidingTargetOffset = new Vector3(0f, 0.32f, 0f);
    public float hidingDistance = 1.55f;
    public float hidingShoulderOffset = 0.22f;
    [Tooltip("Cuánto se adelanta la cámara desde el punto oculto hacia la entrada al espiar.")]
    public float peekForwardOffset = 0.24f;
    [Tooltip("Altura de los ojos como fracción de la altura del collider oculto.")]
    public float peekEyeHeightRatio = 0.82f;
    [Tooltip("Altura de los ojos sobre el suelo al esconderse bajo una cama.")]
    [SerializeField, Min(0.1f)] private float underBedEyeHeight = 0.14f;
    public float peekMinimumPitch = -45f;
    public float peekMaximumPitch = 65f;

    private InputAction lookAction;
    private float yaw;
    private float pitch = 10f;
    private bool cursorLocked;
    private float hidingBlend;
    private HideSpot firstPersonSpot;
    private Vector3 firstPersonOutward;
    private bool snapAfterPortal;
    private Transform portalBoundary;
    private bool cinematicMode;

    public void SetCinematicMode(bool active)
    {
        cinematicMode = active;
        if (active) SetCursorState(false);
        else
        {
            SetCursorState(lockCursorOnPlay);
            // El primer fotograma del control normal parte del encuadre final de Cinemachine.
            snapAfterPortal = false;
        }
    }

    public void GetGameplayPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 pivot = target.position + targetOffset;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        position = KeepCameraInsidePortal(pivot,
            pivot + orbit * new Vector3(shoulderOffset, cameraHeight, -distance));
        Vector3 direction = position - pivot;
        if (direction.sqrMagnitude > .0001f && Physics.SphereCast(pivot, collisionRadius,
                direction.normalized, out RaycastHit hit, direction.magnitude, collisionMask,
                QueryTriggerInteraction.Ignore))
            position = pivot + direction.normalized * Mathf.Max(.05f, hit.distance - collisionPadding);
        rotation = Quaternion.LookRotation(pivot + orbit * Vector3.forward * 10f - position, Vector3.up);
    }

    public void GetPlanarMovementAxes(out Vector3 forward, out Vector3 right)
    {
        // La rotación final de la cámara converge desde el hombro hacia el centro
        // y por eso no representa exactamente el eje de avance que ve el jugador.
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        forward = heading * Vector3.forward;
        right = heading * Vector3.right;
    }

    public void SnapThroughPortal(PortalTrialGate gate)
    {
        if (gate == null) return;
        Quaternion mappedRotation = gate.MapRotation(transform.rotation);
        portalBoundary = gate.Destination != null ? gate.Destination.transform : null;
        yaw = mappedRotation.eulerAngles.y;
        // La cámara está detrás del niño: mapear su posición anterior la dejaría
        // fuera del cuarto de llegada. Reencuadramos desde el jugador ya trasladado.
        Vector3 pivot = target.position + Vector3.Lerp(targetOffset, hidingTargetOffset, hidingBlend);
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        float framingDistance = Mathf.Lerp(distance, hidingDistance, hidingBlend);
        float framingShoulder = Mathf.Lerp(shoulderOffset, hidingShoulderOffset, hidingBlend);
        Vector3 desired = pivot + orbit * new Vector3(framingShoulder, cameraHeight, -framingDistance);
        desired = KeepCameraInsidePortal(pivot, desired);
        Vector3 direction = desired - pivot;
        if (direction.sqrMagnitude > .0001f && Physics.SphereCast(pivot, collisionRadius,
                direction.normalized, out RaycastHit hit, direction.magnitude, collisionMask,
                QueryTriggerInteraction.Ignore))
            desired = pivot + direction.normalized * Mathf.Max(.05f, hit.distance - collisionPadding);
        Vector3 lookPoint = pivot + orbit * Vector3.forward * 10f;
        transform.SetPositionAndRotation(desired,
            Quaternion.LookRotation(lookPoint - desired, Vector3.up));
        snapAfterPortal = true;
    }

    private Vector3 KeepCameraInsidePortal(Vector3 pivot, Vector3 desired)
    {
        if (portalBoundary == null) return desired;
        Vector3 outward = portalBoundary.forward;
        float pivotSide = Vector3.Dot(pivot - portalBoundary.position, outward);
        float desiredSide = Vector3.Dot(desired - portalBoundary.position, outward);
        const float insideMargin = -.06f;
        if (desiredSide <= insideMargin) return desired;
        if (pivotSide >= insideMargin) return pivot - outward * .08f;
        float fraction = (insideMargin - pivotSide) / (desiredSide - pivotSide);
        return Vector3.Lerp(pivot, desired, Mathf.Clamp01(fraction));
    }

    public bool IsFirstPersonHiding => firstPersonSpot != null;

    public void SetHidingView(bool hiding, HideSpot spot = null, Vector3? entryPosition = null)
    {
        hidingBlend = hiding ? 1f : 0f;
        firstPersonSpot = hiding ? spot : null;
        if (firstPersonSpot != null && firstPersonSpot.HiddenPoint != null)
        {
            // Mirar hacia el lado de entrada evita empezar frente a la pared del mueble.
            Vector3 entrance = entryPosition ?? (firstPersonSpot.EntryPoint != null
                ? firstPersonSpot.EntryPoint.position : firstPersonSpot.HiddenPoint.position);
            Vector3 outward = Vector3.ProjectOnPlane(
                entrance - firstPersonSpot.HiddenPoint.position, Vector3.up);
            if (outward.sqrMagnitude > 0.001f)
            {
                firstPersonOutward = outward.normalized;
                yaw = Quaternion.LookRotation(outward, Vector3.up).eulerAngles.y;
            }
            else firstPersonOutward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            pitch = 5f;
        }
    }

    private void Awake()
    {
        CacheLookAction();
        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null && Mathf.Approximately(yaw, 0f))
        {
            yaw = target.eulerAngles.y;
        }

        SetCursorState(lockCursorOnPlay);
    }

    private void OnEnable()
    {
        CacheLookAction();
        InputActionMap map = lookAction != null ? lookAction.actionMap : null;
        if (map != null)
        {
            map.Enable();
        }
    }

    private void OnDisable()
    {
        InputActionMap map = lookAction != null ? lookAction.actionMap : null;
        if (map != null)
        {
            map.Disable();
        }

        SetCursorState(false);
    }

    private void Update()
    {
        if (cinematicMode) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorState(false);
        }

        if (!cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SetCursorState(true);
        }

        if (!cursorLocked)
        {
            return;
        }

        Vector2 look = ReadLookInput();
        yaw += look.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity,
            firstPersonSpot != null ? peekMinimumPitch : minimumPitch,
            firstPersonSpot != null ? peekMaximumPitch : maximumPitch);
    }

    private void LateUpdate()
    {
        if (cinematicMode) return;
        if (target == null)
        {
            return;
        }
        if (snapAfterPortal) { snapAfterPortal = false; return; }

        Vector3 effectiveTargetOffset = Vector3.Lerp(targetOffset, hidingTargetOffset,
            hidingBlend);
        float effectiveDistance = Mathf.Lerp(distance, hidingDistance, hidingBlend);
        float effectiveShoulder = Mathf.Lerp(shoulderOffset, hidingShoulderOffset,
            hidingBlend);
        Vector3 pivot = target.position + effectiveTargetOffset;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        if (firstPersonSpot != null)
        {
            // Bajo la cama, la cámara debe quedar por debajo del faldón del mueble.
            // El margen de 14 cm la mantiene sobre el piso y su plano de recorte.
            float eyeHeight = firstPersonSpot.TransitionType == HideTransitionType.ProneSlide
                ? Mathf.Max(0.1f, underBedEyeHeight)
                : Mathf.Clamp(firstPersonSpot.HiddenControllerHeight * peekEyeHeightRatio,
                    0.24f, 1.02f);
            // Solo los escondites con cobertura visual permiten adelantar los ojos.
            // Dentro de un armario o baúl la cámara permanece detrás de su puerta.
            float forwardOffset = firstPersonSpot.Concealment == HideConcealment.VisualCover
                ? peekForwardOffset : 0f;
            Vector3 eye = target.position + Vector3.up * eyeHeight
                + firstPersonOutward * forwardOffset;
            float peekBlend = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, eye, peekBlend);
            float lookBlend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, orbit, lookBlend);
            return;
        }
        Vector3 desired = pivot + orbit * new Vector3(effectiveShoulder, cameraHeight,
            -effectiveDistance);
        desired = KeepCameraInsidePortal(pivot, desired);
        Vector3 direction = desired - pivot;
        float desiredDistance = direction.magnitude;

        if (desiredDistance > 0.001f && Physics.SphereCast(pivot, collisionRadius,
                direction.normalized, out RaycastHit hit, desiredDistance, collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            desired = pivot + direction.normalized * Mathf.Max(0.05f, hit.distance - collisionPadding);
        }

        float positionBlend = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, positionBlend);

        Vector3 lookPoint = pivot + orbit * Vector3.forward * 10f;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        float rotationBlend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
    }

    private void CacheLookAction()
    {
        lookAction = null;
        if (inputActions == null)
        {
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", false);
        if (playerMap != null)
        {
            lookAction = playerMap.FindAction("Look", false);
        }
    }

    private Vector2 ReadLookInput()
    {
        if (lookAction != null)
        {
            return lookAction.ReadValue<Vector2>();
        }

        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
    }

    private void SetCursorState(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
