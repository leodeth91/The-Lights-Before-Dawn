using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Prepara el paisaje exterior y dirige la secuencia cinematográfica final.</summary>
[DisallowMultipleComponent]
public sealed class FinalSequenceDirector : MonoBehaviour
{
    private const string LandscapeSceneName = "FinalAlienLandscape";
    private const string MenuSceneName = "ModularPrototype";
    private const float WalkDistance = 5.5f;
    private const float DawnAmbienceVolume = 0.16f;
    private const float AmbienceRiseSeconds = 3f;
    private static readonly Vector3 StandingEyeOffset = new Vector3(0f, 1.15f, .24f);

    [SerializeField, Min(0.1f)] private float walkSeconds = 4f;
    [SerializeField, Min(0.1f)] private float cameraBlendSeconds = 2f;
    [SerializeField, Min(0.1f)] private float lookSeconds = 1.8f;
    [SerializeField, Min(0.1f)] private float textSeconds = 2f;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 2f;
    [SerializeField] private AudioClip alienAmbienceClip;
    [SerializeField, Range(0f, 1f)] private float alienAmbienceVolume = 0.65f;

    private Scene landscapeScene;
    private Transform doorStage;
    private DoorInteractable stageDoor;
    private DoorSocket destinationSocket;
    private PortalTrialGate sourceGate;
    private PortalTrialGate destinationGate;
    private Camera playerCamera;
    private HousePortalTraveller playerPortalTraveller;
    private DoorSocket preparedExit;
    private AudioSource finalAmbience;
    private StoryProgression story;
    private Coroutine ambienceFade;
    private bool isReady;
    private bool isLoading;
    private bool isRunning;
    private bool showMessage;
    private float fadeAlpha;
    private string loadError;

    public bool IsReady => isReady;

    private void Start()
    {
        story = StoryProgression.Instance;
        if (story == null) return;

        story.ObjectiveChanged += OnObjectiveChanged;
        if (story.HasDawnArrived) StartDawnAmbience();
    }

    private void OnObjectiveChanged(StoryObjective objective)
    {
        if (objective == StoryObjective.Escape) StartDawnAmbience();
    }

    /// <summary> carga el escenario exterior al abrir la salida para que ya se vea tras el umbral. </summary>
    public void PrepareExit(DoorSocket exit)
    {
        if (exit == null || isReady || isLoading || loadError != null) return;
        preparedExit = exit;
        if (playerCamera == null) playerCamera = Camera.main;

        Scene existing = SceneManager.GetSceneByName(LandscapeSceneName);
        if (existing.IsValid() && existing.isLoaded)
        {
            ConfigureLandscape(existing, exit);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        isLoading = true;
        if (SceneManager.LoadSceneAsync(LandscapeSceneName, LoadSceneMode.Additive) == null)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            isLoading = false;
            loadError = "No se pudo iniciar la carga de FinalAlienLandscape.";
        }
    }

    /// <summary>Inicia la secuencia cuando el jugador cruzó hacia afuera.</summary>
    public void Begin(ChildPlayerController player, DoorSocket exit)
    {
        if (isRunning || player == null || exit == null) return;
        GameSessionManager session = GameSessionManager.Instance;
        if (session == null || !session.IsPlaying) return;

        if (!isReady) PrepareExit(exit);
        isRunning = true;
        player.SetMovementLocked(true);
        playerPortalTraveller = player.GetComponent<HousePortalTraveller>();
        if (playerPortalTraveller != null) playerPortalTraveller.enabled = false;
        StartCoroutine(WaitForLandscapeAndPlay(player, exit, session));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != LandscapeSceneName) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        isLoading = false;
        ConfigureLandscape(scene, preparedExit);
    }

    private void ConfigureLandscape(Scene scene, DoorSocket exit)
    {
        if (exit == null)
        {
            loadError = "No se encontró la puerta de salida para configurar el portal final.";
            Debug.LogWarning(loadError, this);
            return;
        }

        landscapeScene = scene;
        GameObject[] roots = scene.GetRootGameObjects();
        // La casa conserva módulos activos cerca del origen. Separar el exterior
        // por traslación evita verlos o chocar con ellos sin rotar el Terrain.
        foreach (GameObject root in roots)
            root.transform.position += new Vector3(2000f, 0f, 2000f);
        doorStage = FindTransform(roots, "DoorStage");
        destinationSocket = doorStage != null
            ? doorStage.GetComponentInChildren<DoorSocket>(true) : null;
        if (doorStage == null)
        {
            loadError = "La escena FinalAlienLandscape necesita un objeto DoorStage.";
            Debug.LogWarning(loadError, this);
            return;
        }
        if (destinationSocket == null)
        {
            loadError = "DoorStage necesita un DoorSocket para enlazar el portal final.";
            Debug.LogWarning(loadError, this);
            return;
        }

        stageDoor = doorStage.GetComponentInChildren<DoorInteractable>(true);
        DisableSceneCameras(roots);
        if (!ConfigurePortalPair(exit))
        {
            loadError = "No se pudo configurar la cámara o el portal del paisaje final.";
            Debug.LogWarning(loadError, this);
            return;
        }
        OpenStageDoor();
        isReady = true;
    }

    private static Transform FindTransform(GameObject[] roots, string objectName)
    {
        foreach (GameObject root in roots)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
                if (candidate.name == objectName) return candidate;
        }
        return null;
    }

    private bool ConfigurePortalPair(DoorSocket exit)
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return false;
        sourceGate = exit.gameObject.AddComponent<PortalTrialGate>();
        destinationGate = destinationSocket.GetComponent<PortalTrialGate>();
        if (destinationGate == null)
            destinationGate = destinationSocket.gameObject.AddComponent<PortalTrialGate>();
        sourceGate.Configure(destinationGate, playerCamera, true, .86f, 2.08f);
        Scene previousActive = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(landscapeScene);
        sourceGate.SetSkybox(RenderSettings.skybox);
        SceneManager.SetActiveScene(previousActive);
        destinationGate.Configure(sourceGate, playerCamera, true, .86f, 2.08f, true);
        return true;
    }

    private static void DisableSceneCameras(GameObject[] roots)
    {
        foreach (GameObject root in roots)
        {
            foreach (Camera sceneCamera in root.GetComponentsInChildren<Camera>(true))
                sceneCamera.gameObject.SetActive(false);
        }
    }

    private IEnumerator PlaySequence(ChildPlayerController player, DoorSocket exit)
    {
        GameSessionManager session = GameSessionManager.Instance;
        if (session == null || !session.IsPlaying)
        {
            player.SetMovementLocked(false);
            isRunning = false;
            yield break;
        }
        MapPlayerThroughExit(player);
        if (landscapeScene.IsValid() && landscapeScene.isLoaded)
            SceneManager.SetActiveScene(landscapeScene);
        session.BeginEnding();
        RaiseAlienAmbience();
        CharacterController capsule = player.GetComponent<CharacterController>();
        bool capsuleWasEnabled = capsule != null && capsule.enabled;
        if (capsuleWasEnabled) capsule.enabled = true;

        OverShoulderCamera shoulder = FindShoulderCamera(player);
        Camera mainCamera = playerCamera != null ? playerCamera : Camera.main;
        if (shoulder != null)
        {
            shoulder.SetCinematicMode(true);
            shoulder.SnapThroughPortal(sourceGate);
        }
        else if (mainCamera != null && sourceGate != null)
        {
            mainCamera.transform.SetPositionAndRotation(
                sourceGate.MapPoint(mainCamera.transform.position),
                sourceGate.MapRotation(mainCamera.transform.rotation));
        }

        ChildCrouchAnimation childAnimation = player.GetComponent<ChildCrouchAnimation>();
        if (childAnimation != null) childAnimation.enabled = false;
        Animator animator = FindAnimator(player.transform);
        Transform visualRoot = player.transform.Find("PlayerVisual");
        Transform eyeTarget = new GameObject("FinalSequenceEyeTarget").transform;
        Vector3 forward = GetExteriorDirection();
        UpdateEyeTarget(eyeTarget, visualRoot != null ? visualRoot : player.transform, forward);
        CinemachineBrain brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
        if (mainCamera != null && brain == null) brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
        if (brain != null)
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, cameraBlendSeconds);
        }

        CinemachineCamera shoulderBridge = CreateStaticCamera(mainCamera, 30);
        CinemachineCamera firstPersonCamera = CreateFirstPersonCamera(mainCamera, eyeTarget);
        if (firstPersonCamera != null)
        {
            firstPersonCamera.Priority = 10;
            if (brain != null) brain.enabled = true;
            yield return null;
            firstPersonCamera.Priority = 50;
            if (visualRoot != null) StartCoroutine(HideVisualAfterBlend(visualRoot));
        }

        player.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        UpdateEyeTarget(eyeTarget, visualRoot != null ? visualRoot : player.transform, forward);
        Vector3 destination = destinationSocket.Position + forward * WalkDistance;
        destination.y = GetTerrainHeight(destination, player.transform.position.y);
        if (animator != null) animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer.Walk"), 0.2f);
        yield return WalkTo(player, visualRoot, eyeTarget, destination, forward, walkSeconds);
        if (shoulderBridge != null) Destroy(shoulderBridge.gameObject);
        if (animator != null) animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer.Idle"), 0.25f);

        yield return LookDirection(eyeTarget, Quaternion.Euler(0f, -40f, 0f) * forward, lookSeconds);
        yield return LookDirection(eyeTarget, Quaternion.Euler(0f, 40f, 0f) * forward, lookSeconds);
        yield return LookAtSky(eyeTarget, lookSeconds);
        yield return LookDirection(eyeTarget, -forward, lookSeconds);

        CloseStageDoor();
        yield return new WaitForSeconds(1.2f);

        showMessage = true;
        yield return new WaitForSeconds(textSeconds);
        showMessage = false;
        yield return FadeToBlack();

        SceneManager.LoadScene(MenuSceneName, LoadSceneMode.Single);
    }

    private OverShoulderCamera FindShoulderCamera(ChildPlayerController player)
    {
        Camera camera = playerCamera;
        if (camera != null)
        {
            OverShoulderCamera shoulder = camera.GetComponent<OverShoulderCamera>();
            if (shoulder != null) return shoulder;
        }
        return player.GetComponentInChildren<OverShoulderCamera>(true);
    }

    private static Animator FindAnimator(Transform player)
    {
        Transform visual = player.Find("PlayerVisual");
        return visual != null ? visual.GetComponentInChildren<Animator>(true)
            : player.GetComponentInChildren<Animator>(true);
    }

    private static void UpdateEyeTarget(Transform target, Transform visualRoot, Vector3 forward)
    {
        Vector3 planar = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (planar.sqrMagnitude < 0.001f) planar = visualRoot.forward;
        target.SetPositionAndRotation(visualRoot.position + visualRoot.rotation * StandingEyeOffset,
            Quaternion.LookRotation(planar, Vector3.up));
    }

    private IEnumerator HideVisualAfterBlend(Transform visualRoot)
    {
        yield return new WaitForSeconds(cameraBlendSeconds * .9f);
        // Al mirar hacia atrás la cámara ve el interior de la cabeza aunque
        // los ojos estén delante del rostro. Ocultar solo la visual en esta toma.
        foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            renderer.forceRenderingOff = true;
    }

    private void MapPlayerThroughExit(ChildPlayerController player)
    {
        if (sourceGate == null) return;
        CharacterController capsule = player.GetComponent<CharacterController>();
        bool wasEnabled = capsule != null && capsule.enabled;
        if (wasEnabled) capsule.enabled = false;
        player.transform.SetPositionAndRotation(
            sourceGate.ExitPoint(player.transform.position, 0.35f),
            sourceGate.MapRotation(player.transform.rotation));
        if (wasEnabled) capsule.enabled = true;
        Physics.SyncTransforms();

        if (playerCamera != null)
        {
            playerCamera.clearFlags = CameraClearFlags.Skybox;
            playerCamera.farClipPlane = Mathf.Max(playerCamera.farClipPlane, 500f);
        }
    }

    private Vector3 GetExteriorDirection()
    {
        if (destinationSocket == null) return Vector3.forward;
        Vector3 exterior = Vector3.ProjectOnPlane(-destinationSocket.Outward, Vector3.up).normalized;
        return exterior.sqrMagnitude > 0.001f ? exterior : Vector3.forward;
    }

    private static CinemachineCamera CreateFirstPersonCamera(Camera mainCamera, Transform eyeTarget)
    {
        if (mainCamera == null) return null;
        GameObject cameraObject = new GameObject("CM_Final_FirstPerson");
        CinemachineCamera camera = cameraObject.AddComponent<CinemachineCamera>();
        camera.Follow = eyeTarget;
        camera.Priority = 10;
        var lens = camera.Lens;
        lens.FieldOfView = mainCamera.fieldOfView;
        lens.NearClipPlane = mainCamera.nearClipPlane;
        lens.FarClipPlane = mainCamera.farClipPlane;
        camera.Lens = lens;
        cameraObject.AddComponent<CinemachineHardLockToTarget>();
        cameraObject.AddComponent<CinemachineRotateWithFollowTarget>();
        return camera;
    }

    private static CinemachineCamera CreateStaticCamera(Camera mainCamera, int priority)
    {
        if (mainCamera == null) return null;
        GameObject cameraObject = new GameObject("CM_Final_ShoulderBridge");
        cameraObject.transform.SetPositionAndRotation(mainCamera.transform.position,
            mainCamera.transform.rotation);
        CinemachineCamera camera = cameraObject.AddComponent<CinemachineCamera>();
        camera.Priority = priority;
        var lens = camera.Lens;
        lens.FieldOfView = mainCamera.fieldOfView;
        lens.NearClipPlane = mainCamera.nearClipPlane;
        lens.FarClipPlane = mainCamera.farClipPlane;
        camera.Lens = lens;
        return camera;
    }

    private IEnumerator WalkTo(ChildPlayerController player, Transform visualRoot, Transform eyeTarget,
        Vector3 destination, Vector3 forward, float duration)
    {
        Vector3 start = player.transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 desired = Vector3.Lerp(start, destination, progress);
            Vector3 movement = desired - player.transform.position;
            CharacterController capsule = player.GetComponent<CharacterController>();
            if (capsule != null && capsule.enabled) capsule.Move(movement);
            else player.transform.position = desired;
            player.transform.rotation = Quaternion.Slerp(player.transform.rotation,
                Quaternion.LookRotation(forward, Vector3.up), Time.deltaTime * 4f);
            UpdateEyeTarget(eyeTarget, visualRoot != null ? visualRoot : player.transform, forward);
            yield return null;
        }

        if (player.GetComponent<CharacterController>() == null
            || !player.GetComponent<CharacterController>().enabled)
            player.transform.position = destination;
        UpdateEyeTarget(eyeTarget, visualRoot != null ? visualRoot : player.transform, forward);
    }

    private IEnumerator WaitForLandscapeAndPlay(ChildPlayerController player, DoorSocket exit,
        GameSessionManager session)
    {
        while (!isReady && loadError == null) yield return null;
        if (!isReady)
        {
            Debug.LogWarning("No se pudo preparar el paisaje final; se devuelve el control al jugador. " + loadError, this);
            player.SetMovementLocked(false);
            if (playerPortalTraveller != null) playerPortalTraveller.enabled = true;
            isRunning = false;
            yield break;
        }
        if (session == null || !session.IsPlaying)
        {
            player.SetMovementLocked(false);
            if (playerPortalTraveller != null) playerPortalTraveller.enabled = true;
            isRunning = false;
            yield break;
        }
        yield return PlaySequence(player, exit);
    }

    private IEnumerator LookDirection(Transform target, Vector3 direction, float duration)
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        Quaternion initial = target.rotation;
        Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
        yield return RotateEyeTarget(target, initial, desired, duration);
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator LookAtSky(Transform target, float duration)
    {
        Quaternion initial = target.rotation;
        Vector3 direction = target.forward + Vector3.up * 1.35f;
        Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
        yield return RotateEyeTarget(target, initial, desired, duration);
        yield return new WaitForSeconds(0.5f);
    }

    private static IEnumerator RotateEyeTarget(Transform target, Quaternion from, Quaternion to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.rotation = Quaternion.Slerp(from, to,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        target.rotation = to;
    }

    private float GetTerrainHeight(Vector3 position, float fallback)
    {
        foreach (Terrain terrain in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (terrain.terrainData == null) continue;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (position.x < terrainPosition.x || position.x > terrainPosition.x + size.x
                || position.z < terrainPosition.z || position.z > terrainPosition.z + size.z) continue;
            return terrain.SampleHeight(position) + terrainPosition.y;
        }
        return fallback;
    }

    private IEnumerator FadeToBlack()
    {
        StopAmbienceFade();
        fadeAlpha = 0f;
        float ambienceVolume = finalAmbience != null ? finalAmbience.volume : 0f;
        for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.deltaTime)
        {
            fadeAlpha = Mathf.Clamp01(elapsed / fadeSeconds);
            if (finalAmbience != null)
                finalAmbience.volume = Mathf.Lerp(ambienceVolume, 0f, fadeAlpha);
            yield return null;
        }
        fadeAlpha = 1f;
        if (finalAmbience != null) finalAmbience.Stop();
    }

    private void StartDawnAmbience()
    {
        if (!EnsureAlienAmbience()) return;

        StopAmbienceFade();
        finalAmbience.volume = Mathf.Min(DawnAmbienceVolume, alienAmbienceVolume);
        if (!finalAmbience.isPlaying) finalAmbience.Play();
    }

    private void RaiseAlienAmbience()
    {
        if (!EnsureAlienAmbience()) return;

        if (!finalAmbience.isPlaying)
        {
            finalAmbience.volume = Mathf.Min(DawnAmbienceVolume, alienAmbienceVolume);
            finalAmbience.Play();
        }

        StopAmbienceFade();
        ambienceFade = StartCoroutine(FadeAmbienceTo(alienAmbienceVolume, AmbienceRiseSeconds));
    }

    private bool EnsureAlienAmbience()
    {
        if (finalAmbience != null) return true;
        if (alienAmbienceClip == null)
        {
            Debug.LogWarning("Falta asignar el clip de ambiente alienígena a la secuencia final.", this);
            return false;
        }

        GameObject ambienceObject = new GameObject("FinalAlienAmbience");
        ambienceObject.transform.SetParent(transform, false);
        finalAmbience = ambienceObject.AddComponent<AudioSource>();
        AudioMixerRouting.Route(finalAmbience, AudioMixerRouting.Bus.Ambience);
        finalAmbience.playOnAwake = false;
        finalAmbience.clip = alienAmbienceClip;
        finalAmbience.spatialBlend = 0f;
        finalAmbience.dopplerLevel = 0f;
        finalAmbience.loop = true;
        finalAmbience.volume = 0f;
        return true;
    }

    private IEnumerator FadeAmbienceTo(float targetVolume, float duration)
    {
        float initialVolume = finalAmbience != null ? finalAmbience.volume : 0f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (finalAmbience == null) yield break;
            finalAmbience.volume = Mathf.Lerp(initialVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        if (finalAmbience != null) finalAmbience.volume = targetVolume;
        ambienceFade = null;
    }

    private void StopAmbienceFade()
    {
        if (ambienceFade == null) return;
        StopCoroutine(ambienceFade);
        ambienceFade = null;
    }

    private void OpenStageDoor()
    {
        if (stageDoor == null || stageDoor.IsOpen) return;
        stageDoor.Configure(100f, 1f);
        stageDoor.BeginOpen(null);
    }

    private void CloseStageDoor()
    {
        if (stageDoor == null || !stageDoor.IsOpen) return;
        if (destinationGate != null)
            destinationGate.Configure(sourceGate, playerCamera, true, 1.12f, 2f, false);
        stageDoor.Configure(100f, 1.2f);
        stageDoor.BeginClose();
    }

    private void OnGUI()
    {
        if (!showMessage && fadeAlpha <= 0f) return;
        Color previous = GUI.color;
        if (showMessage)
        {
            GUI.color = Color.white;
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(24, Screen.height / 22),
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(0f, Screen.height * 0.68f, Screen.width, 80f), "¿Dónde estoy?", style);
        }
        if (fadeAlpha > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = previous;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
    }
}
