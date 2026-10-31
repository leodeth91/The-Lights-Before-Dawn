using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Despertar en la cama infantil. Cinemachine solo toma la cámara durante esta secuencia.</summary>
[DisallowMultipleComponent]
public sealed class OpeningSequenceDirector : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float eyeOpeningSeconds = 3f;
    [SerializeField, Min(.1f)] private float lookAtWindowSeconds = 2f;
    [SerializeField, Min(.1f)] private float sitUpSeconds = 1.8f;
    [SerializeField, Min(.1f)] private float getUpSeconds = 2.2f;
    [SerializeField, Min(.1f)] private float shoulderBlendSeconds = 1.5f;
    [SerializeField, Range(1f, 3f)] private float openingWindowVolumeMultiplier = 2f;

    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
    private static readonly int CrouchIdle = Animator.StringToHash("Base Layer.CrouchIdle");
    private static readonly int Prone = Animator.StringToHash("Base Layer.Prone");

    public static bool IsRunning { get; private set; }

    private GameSessionManager session;
    private HouseFlowController house;
    private ChildPlayerController movement;
    private OverShoulderCamera shoulder;
    private Camera mainCamera;
    private CinemachineBrain brain;
    private CinemachineCamera eyesCamera;
    private CinemachineCamera shoulderCamera;
    private Transform visualRoot;
    private Transform eyeTarget;
    private ChildCrouchAnimation childAnimation;
    private Animator animator;
    private bool childAnimationWasEnabled;
    private Vector3 originalVisualPosition;
    private Quaternion originalVisualRotation;
    private Vector3 originalVisualScale;
    private readonly System.Collections.Generic.Dictionary<AudioSource, float> windowVolumes =
        new System.Collections.Generic.Dictionary<AudioSource, float>();
    private float eyelids = 1f;
    private Coroutine sequence;

    private void Start()
    {
        session = GetComponent<GameSessionManager>();
        house = HouseFlowController.Instance;
        mainCamera = Camera.main;
        movement = FindFirstObjectByType<ChildPlayerController>();
        shoulder = mainCamera != null ? mainCamera.GetComponent<OverShoulderCamera>() : null;
        if (session != null) session.StateChanged += OnSessionChanged;
        if (session != null && session.IsPlaying) OnSessionChanged(session.State);
    }

    private void OnSessionChanged(GameSessionState state)
    {
        if (state == GameSessionState.Playing && sequence == null)
            sequence = StartCoroutine(PlayOpening());
        else if (state != GameSessionState.Playing && sequence != null)
        {
            StopCoroutine(sequence);
            Finish();
        }
    }

    private IEnumerator PlayOpening()
    {
        if (house == null || movement == null || shoulder == null || mainCamera == null
            || !house.ModuleById.TryGetValue("child_room", out RoomModule childRoom))
        {
            Debug.LogWarning("No se pudo iniciar la secuencia de despertar; se conserva la cámara normal.", this);
            sequence = null;
            yield break;
        }
        HideSpot bed = null;
        foreach (HideSpot spot in childRoom.GetComponentsInChildren<HideSpot>(true))
            if (spot.name == "ESCONDITE_U01_CamaNino") { bed = spot; break; }
        if (bed == null)
        {
            Debug.LogWarning("Falta la cama infantil para la secuencia de despertar.", this);
            sequence = null;
            yield break;
        }

        visualRoot = movement.transform.Find("PlayerVisual");
        if (visualRoot == null)
        {
            Debug.LogWarning("Falta PlayerVisual para el despertar; se conserva la cámara normal.", this);
            sequence = null;
            yield break;
        }

        IsRunning = true;
        movement.SetMovementLocked(true);
        shoulder.SetCinematicMode(true);
        childAnimation = movement.GetComponent<ChildCrouchAnimation>();
        if (childAnimation != null)
        {
            childAnimationWasEnabled = childAnimation.enabled;
            childAnimation.enabled = false;
        }
        animator = visualRoot.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            // El asset fue ubicado al costado de la cama con un offset respecto
            // del CharacterController. Conservar su lugar visual y llevar el
            // controlador hasta él evita que cámara, colisión y cuerpo se separen.
            Vector3 authoredModelPosition = animator.transform.position;
            Vector3 controllerPosition = movement.transform.position;
            controllerPosition.x = authoredModelPosition.x;
            controllerPosition.z = authoredModelPosition.z;
            CharacterController capsule = movement.GetComponent<CharacterController>();
            bool capsuleWasEnabled = capsule != null && capsule.enabled;
            if (capsuleWasEnabled) capsule.enabled = false;
            movement.transform.position = controllerPosition;
            animator.transform.position = authoredModelPosition;
            if (capsuleWasEnabled) capsule.enabled = true;
            if (childAnimation != null) childAnimation.RefreshRestPose();
        }
        originalVisualPosition = visualRoot.localPosition;
        originalVisualRotation = visualRoot.localRotation;
        originalVisualScale = visualRoot.localScale;
        Vector3 standingVisual = visualRoot.position;
        Quaternion standingVisualRotation = visualRoot.rotation;
        if (animator != null) animator.Play(Prone, 0, 0f);
        brain = mainCamera.GetComponent<CinemachineBrain>();
        if (brain == null) brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
        brain.enabled = false;
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut, shoulderBlendSeconds);

        // El modelo permanece en el plano durante toda la toma; el controlador físico
        // espera en la posición final que el diseñador colocó junto a la cama.
        Vector3 modelOffset = animator != null
            ? visualRoot.InverseTransformPoint(animator.transform.position) : Vector3.zero;
        Quaternion modelRotationOffset = animator != null
            ? Quaternion.Inverse(visualRoot.rotation) * animator.transform.rotation : Quaternion.identity;
        Quaternion bedVisualRotation = bed.transform.rotation * Quaternion.Inverse(modelRotationOffset);
        Vector3 standingModel = animator != null ? animator.transform.position : standingVisual;
        float bedSide = Vector3.Dot(standingModel - bed.transform.position, bed.transform.right) < 0f
            ? -1f : 1f;
        Vector3 sleepingVisual = bed.transform.TransformPoint(new Vector3(0f, .84f, .3f))
            - bedVisualRotation * modelOffset;
        Vector3 sittingVisual = bed.transform.TransformPoint(new Vector3(bedSide * .45f, .73f, -.15f))
            - bedVisualRotation * modelOffset;
        visualRoot.SetPositionAndRotation(sleepingVisual, bedVisualRotation);

        Quaternion steppingVisualRotation = Quaternion.Slerp(bedVisualRotation, standingVisualRotation, .7f);
        Vector3 steppingVisual = bed.transform.TransformPoint(new Vector3(bedSide * .9f, .03f, -.15f))
            - steppingVisualRotation * modelOffset;

        // Cabecera hacia +Z del escondite. El objetivo de Cinemachine comienza
        // sobre la almohada, a la altura de los ojos, mirando el techo.
        Vector3 sleepingEye = bed.transform.TransformPoint(new Vector3(0f, 1.04f, .63f));
        Quaternion ceilingLook = Quaternion.LookRotation(Vector3.up * 1.8f - bed.transform.forward * .15f);
        Light window = null;
        foreach (Light light in childRoom.GetComponentsInChildren<Light>(true))
        {
            if (light.name != "LuzExteriorVentana") continue;
            if (window == null) window = light;
            foreach (AudioSource source in light.GetComponents<AudioSource>())
            {
                if (source.clip == null || !source.loop) continue;
                windowVolumes[source] = source.volume;
                source.volume = Mathf.Min(1f, source.volume * openingWindowVolumeMultiplier);
            }
        }
        Vector3 windowDirection = window != null ? window.transform.position - sleepingEye
            : -bed.transform.right;
        Quaternion windowLook = Quaternion.LookRotation(windowDirection.normalized, Vector3.up);
        Vector3 sittingEye = bed.transform.TransformPoint(new Vector3(bedSide * .45f, 1.4f, -.15f));
        Vector3 steppingEye = bed.transform.TransformPoint(new Vector3(bedSide * .9f, 1.15f, -.15f));
        Vector3 standingEye = standingVisual + standingVisualRotation * new Vector3(0f, 1.15f, .24f);
        Quaternion standingLook = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(windowDirection, Vector3.up).normalized + Vector3.up * .05f);

        var eyeObject = new GameObject("ObjetivoOjos_Despertar");
        eyeTarget = eyeObject.transform;
        eyeTarget.SetParent(visualRoot, false);
        eyeTarget.SetPositionAndRotation(sleepingEye, ceilingLook);
        Vector3 sleepingEyeLocal = eyeTarget.localPosition;
        Vector3 sittingEyeLocal = Quaternion.Inverse(bedVisualRotation) * (sittingEye - sittingVisual);
        Vector3 steppingEyeLocal = Quaternion.Inverse(steppingVisualRotation) * (steppingEye - steppingVisual);
        Vector3 standingEyeLocal = Quaternion.Inverse(standingVisualRotation) * (standingEye - standingVisual);

        eyesCamera = CreateCamera("CM_Despertar_PrimeraPersona", sleepingEye, ceilingLook, 20);
        eyesCamera.Follow = eyeTarget;
        eyesCamera.gameObject.AddComponent<CinemachineHardLockToTarget>();
        eyesCamera.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();
        shoulderCamera = CreateCamera("CM_Despertar_Hombro", sleepingEye, ceilingLook, 10);
        brain.enabled = true;
        eyelids = 1f;

        yield return new WaitForSeconds(.55f);
        yield return AnimateEyes();
        yield return new WaitForSeconds(.75f);
        yield return MoveEyes(ceilingLook, windowLook, lookAtWindowSeconds);
        yield return new WaitForSeconds(.45f);
        if (animator != null) animator.CrossFadeInFixedTime(CrouchIdle, .35f);
        yield return MoveActor(sleepingVisual, bedVisualRotation, sittingVisual,
            bedVisualRotation, sleepingEyeLocal, sittingEyeLocal,
            windowLook, windowLook, sitUpSeconds);
        yield return MoveActor(sittingVisual, bedVisualRotation, steppingVisual,
            steppingVisualRotation, sittingEyeLocal, steppingEyeLocal,
            windowLook, standingLook, getUpSeconds * .68f);
        if (animator != null) animator.CrossFadeInFixedTime(Walk, .25f);
        yield return MoveActor(steppingVisual, steppingVisualRotation, standingVisual,
            standingVisualRotation, steppingEyeLocal, standingEyeLocal,
            standingLook, standingLook, getUpSeconds * .32f);
        if (animator != null) animator.CrossFadeInFixedTime(Idle, .2f);

        // El Brain hace el blend a un encuadre idéntico al de la cámara jugable.
        shoulder.GetGameplayPose(out Vector3 gameplayPosition, out Quaternion gameplayRotation);
        shoulderCamera.transform.SetPositionAndRotation(gameplayPosition, gameplayRotation);
        shoulderCamera.Priority = 30;
        yield return new WaitForSeconds(shoulderBlendSeconds);
        Finish();
    }

    private CinemachineCamera CreateCamera(string name, Vector3 position, Quaternion rotation, int priority)
    {
        var root = new GameObject(name);
        root.transform.SetParent(transform, true);
        root.transform.SetPositionAndRotation(position, rotation);
        var camera = root.AddComponent<CinemachineCamera>();
        camera.Priority = priority;
        var lens = camera.Lens;
        lens.FieldOfView = mainCamera.fieldOfView;
        lens.NearClipPlane = mainCamera.nearClipPlane;
        lens.FarClipPlane = mainCamera.farClipPlane;
        camera.Lens = lens;
        return camera;
    }

    private IEnumerator AnimateEyes()
    {
        for (float time = 0f; time < eyeOpeningSeconds; time += Time.deltaTime)
        {
            eyelids = 1f - Mathf.SmoothStep(0f, 1f, time / eyeOpeningSeconds);
            yield return null;
        }
        eyelids = 0f;
    }

    private IEnumerator MoveEyes(Quaternion fromRotation, Quaternion toRotation, float seconds)
    {
        for (float time = 0f; time < seconds; time += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / seconds);
            eyeTarget.rotation = Quaternion.Slerp(fromRotation, toRotation, t);
            yield return null;
        }
        eyeTarget.rotation = toRotation;
    }

    private IEnumerator MoveActor(Vector3 from, Quaternion fromBodyRotation,
        Vector3 to, Quaternion toBodyRotation, Vector3 fromEye, Vector3 toEye,
        Quaternion fromLook, Quaternion toLook, float seconds)
    {
        for (float time = 0f; time < seconds; time += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / seconds);
            visualRoot.SetPositionAndRotation(Vector3.Lerp(from, to, t),
                Quaternion.Slerp(fromBodyRotation, toBodyRotation, t));
            eyeTarget.localPosition = Vector3.Lerp(fromEye, toEye, t);
            eyeTarget.rotation = Quaternion.Slerp(fromLook, toLook, t);
            yield return null;
        }
        visualRoot.SetPositionAndRotation(to, toBodyRotation);
        eyeTarget.localPosition = toEye;
        eyeTarget.rotation = toLook;
    }

    private void Finish()
    {
        if (brain != null) brain.enabled = false;
        if (eyesCamera != null) Destroy(eyesCamera.gameObject);
        if (shoulderCamera != null) Destroy(shoulderCamera.gameObject);
        foreach (var source in windowVolumes)
            if (source.Key != null) source.Key.volume = source.Value;
        windowVolumes.Clear();
        if (eyeTarget != null) Destroy(eyeTarget.gameObject);
        if (visualRoot != null)
        {
            visualRoot.localPosition = originalVisualPosition;
            visualRoot.localRotation = originalVisualRotation;
            visualRoot.localScale = originalVisualScale;
        }
        if (animator != null) animator.CrossFadeInFixedTime(Idle, .15f);
        if (childAnimation != null) childAnimation.enabled = childAnimationWasEnabled;
        if (shoulder != null) shoulder.SetCinematicMode(false);
        if (movement != null) movement.SetMovementLocked(false);
        eyelids = 0f;
        IsRunning = false;
        sequence = null;
    }

    private void OnGUI()
    {
        if (!IsRunning || eyelids <= 0f) return;
        float cover = Screen.height * .5f * eyelids;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, cover), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, Screen.height - cover, Screen.width, cover), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        if (session != null) session.StateChanged -= OnSessionChanged;
        if (IsRunning) Finish();
    }
}
