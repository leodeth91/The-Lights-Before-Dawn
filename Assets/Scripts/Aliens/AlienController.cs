using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AlienMotor), typeof(AlienPerception), typeof(AlienHeadScanner))]
[RequireComponent(typeof(AlienMemory))]
public sealed class AlienController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(.1f)] private float patrolSpeed = .82f;
    [SerializeField, Min(.1f)] private float searchSpeed = 1.05f;
    [SerializeField, Min(.1f)] private float chaseSpeed = 2.35f;
    [Header("Player response")]
    [SerializeField] private bool enablePlayerDetection = true;
    [SerializeField, Min(.02f)] private float perceptionInterval = .10f;
    [SerializeField, Min(.1f)] private float alertDurationMin = .90f;
    [SerializeField, Min(.1f)] private float alertDurationMax = 1.15f;
    [SerializeField, Min(.1f)] private float catchDistance = .55f;
    [SerializeField, Min(2f)] private float searchDuration = 45f;
    [SerializeField, Min(.05f)] private float chaseDestinationRefresh = .15f;
    [SerializeField, Min(0f)] private float lostSightGrace = .35f;
    [Header("Inspection")]
    [SerializeField, Min(.1f)] private float inspectionDurationMin = 3f;
    [SerializeField, Min(.1f)] private float inspectionDurationMax = 6f;
    [Header("Sound investigation")]
    [SerializeField, Min(.1f)] private float turnTowardSoundDuration = .65f;
    [SerializeField] private Vector2 responseWaitRange = new Vector2(.9f, 1.25f);
    [SerializeField] private Vector2 soundSearchDuration = new Vector2(4f, 8f);
    [SerializeField, Min(.1f)] private float assistanceSpeed = 1.45f;
    [SerializeField, Min(.5f)] private float soundSearchRadius = 2.2f;
    [Header("Events and debug")]
    [SerializeField] private UnityEvent<string> onStateChanged = new UnityEvent<string>();
    [SerializeField] private UnityEvent onAlertStarted = new UnityEvent();
    [SerializeField] private bool verboseLogging;

    private readonly Dictionary<AlienInterest, float> cooldowns = new Dictionary<AlienInterest, float>();
    private readonly AlienState wander = new AlienWanderState(), scan = new AlienScanState(),
        approach = new AlienApproachState(), inspect = new AlienInspectState(), traverse = new AlienTraverseState(),
        alert = new AlienAlertState(), chase = new AlienChaseState(), search = new AlienSearchState(),
        investigateKnownHide = new AlienInvestigateKnownHideSpotState(), catchPlayer = new AlienCatchPlayerState();
    private Coroutine behaviourRoutine;
    private PlayerVisibility player;
    private bool playerSubscribed;
    private float nextPerceptionTime;
    private bool sensesEnabled;
    private bool hearingEnabled = true;
    private bool traversingDoor;
    private RoomModule pendingSurveillanceRoom;
    private float surveillanceClueExpires;
    private bool soundReactionActive;
    private bool playerCallReactionActive;
    private SoundStimulus activeSound;
    private SoundStimulus latestRunningSound;

    public bool RefreshRunningSound(SoundStimulus stimulus)
    {
        if (!enabled || !hearingEnabled || !soundReactionActive || PlayerVisible
            || stimulus.Kind != SuspiciousSoundKind.PlayerRunningStep) return false;
        // Actualiza la pista sin reiniciar la pregunta con cada paso ni crear otra investigación.
        latestRunningSound = stimulus;
        FocusPoint = stimulus.PositionFor(Occupant.Room);
        return true;
    }
    private int friendlyResponseSoundId;
    private readonly List<AlienInterest> expiredInterests = new List<AlienInterest>();
    private AlienActivityDirector activityDirector;
    private AlienActivityTuning activity = new AlienActivityTuning(
        AlienActivityPhase.Cautious, new Vector2(2f, 4f), new Vector2(3.5f, 4.5f),
        1f, 1f, 1f, 1f, 1f, 12f, .1f);
    private int routineChoicesWithoutHideSpot;
    private int routineChoicesWithoutPassage;

    public readonly List<AlienInterest> Visible = new List<AlienInterest>();
    public AlienMotor Motor { get; private set; }
    public AlienPerception Perception { get; private set; }
    public AlienHeadScanner Head { get; private set; }
    public AlienMemory Memory { get; private set; }
    public AlienAmbientAudio Voice { get; private set; }
    public RoomOccupant Occupant { get; private set; }
    public AlienInterest Target { get; set; }
    public IInspectable Inspection { get; set; }
    public RoomConnection Passage { get; set; }
    public bool Scanning { get; set; }
    public bool PlayerVisible { get; private set; }
    public bool HasRecentVisualContact => PlayerVisible
        || (Memory != null && Memory.TimeSinceLastSeen <= lostSightGrace);
    public bool CatchRequested { get; private set; }
    public Vector3? FocusPoint { get; set; }
    public AlienState CurrentState { get; private set; }
    public string StateName { get; private set; } = "Espera";
    public PlayerVisibility Player => player;
    public float PatrolSpeed => patrolSpeed * activity.PatrolSpeedMultiplier;
    public float SearchSpeed => searchSpeed * activity.SearchSpeedMultiplier;
    public float ChaseSpeed => chaseSpeed;
    public float CatchDistance => catchDistance;
    public float SearchDuration => searchDuration;
    public float ChaseDestinationRefresh => chaseDestinationRefresh;
    public Vector2 WanderDuration => activity.WanderDuration;
    public Vector2 ScanDuration => activity.ScanDuration;
    public float HideSpotInterestWeight => activity.HideSpotWeight
        * (1f + Mathf.Min(5, routineChoicesWithoutHideSpot) * activity.AlternationPressure);
    public float PassageInterestWeight => activity.PassageWeight
        * (1f + Mathf.Min(5, routineChoicesWithoutPassage) * activity.AlternationPressure);
    public float InspectionDuration => Random.Range(inspectionDurationMin,
        Mathf.Max(inspectionDurationMin, inspectionDurationMax))
        * activity.InspectionDurationMultiplier;
    public float AlertDuration => Random.Range(alertDurationMin,
        Mathf.Max(alertDurationMin, alertDurationMax));
    public UnityEvent<string> OnStateChanged => onStateChanged;
    public UnityEvent OnAlertStarted => onAlertStarted;
    public bool CanReactToSound => enabled && gameObject.activeInHierarchy && sensesEnabled
        && hearingEnabled
        && !traversingDoor && !soundReactionActive && !PlayerVisible
        && !(CurrentState is AlienAlertState) && !(CurrentState is AlienChaseState)
        && !(CurrentState is AlienSearchState) && !(CurrentState is AlienCatchPlayerState)
        && !(CurrentState is AlienInvestigateKnownHideSpotState)
        && !(CurrentState is AlienTraverseState);
    public bool CanReactToSoundStimulus(SoundStimulus stimulus)
    {
        if (stimulus == null || !enabled || !gameObject.activeInHierarchy
            || !sensesEnabled || !hearingEnabled || traversingDoor
            || soundReactionActive || PlayerVisible
            || CurrentState is AlienAlertState || CurrentState is AlienChaseState
            || CurrentState is AlienCatchPlayerState
            || CurrentState is AlienInvestigateKnownHideSpotState
            || CurrentState is AlienTraverseState) return false;

        // Escuchar al jugador correr aporta una pista más reciente que un punto de búsqueda.
        return !(CurrentState is AlienSearchState)
            || stimulus.Kind == SuspiciousSoundKind.PlayerRunningStep;
    }
    public bool CanReactToPlayerCall => enabled && gameObject.activeInHierarchy
        && sensesEnabled && hearingEnabled && !traversingDoor && !playerCallReactionActive
        && !PlayerVisible && !(CurrentState is AlienAlertState)
        && !(CurrentState is AlienChaseState) && !(CurrentState is AlienCatchPlayerState)
        && !(CurrentState is AlienInvestigateKnownHideSpotState)
        && !(CurrentState is AlienTraverseState);
    public AlienController LastPlayerCallSender { get; private set; }
    public bool AllowsRoamingVocalization => sensesEnabled && !soundReactionActive
        && !playerCallReactionActive
        && !PlayerVisible && !(CurrentState is AlienAlertState)
        && !(CurrentState is AlienChaseState) && !(CurrentState is AlienCatchPlayerState);

    private void Awake()
    {
        Motor = GetComponent<AlienMotor>();
        Perception = GetComponent<AlienPerception>();
        Head = GetComponent<AlienHeadScanner>();
        Memory = GetComponent<AlienMemory>();
        Occupant = GetComponent<RoomOccupant>();
        Voice = GetComponent<AlienAmbientAudio>();
        if (GetComponent<SoundResponsibility>() == null)
            gameObject.AddComponent<SoundResponsibility>();
        if (GetComponent<AlienHearing>() == null)
            gameObject.AddComponent<AlienHearing>();
    }

    private HouseFlowController observedHouse;

    private void OnEnable() => SubscribeToPortalCrossings();

    private void SubscribeToPortalCrossings()
    {
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null || house == observedHouse) return;
        if (observedHouse != null) observedHouse.PortalCrossed -= HandlePortalCrossed;
        observedHouse = house;
        observedHouse.PortalCrossed += HandlePortalCrossed;
    }

    private void HandlePortalCrossed(RoomConnection connection, GameObject actor, RoomModule destination)
    {
        if (!sensesEnabled || player == null || actor != player.gameObject
            || Occupant == null || Memory == null || connection == null || destination == null) return;
        RoomModule origin = connection.SourceRoom == destination
            ? connection.TargetRoom : connection.SourceRoom;
        if (Occupant.Room != origin) return;
        DoorSocket entrance = origin == connection.SourceRoom
            ? connection.SourceSocket : connection.TargetSocket;
        DoorSocket exit = destination == connection.SourceRoom
            ? connection.SourceSocket : connection.TargetSocket;
        Vector3 last = Memory.LastKnownPosition - entrance.Position;
        last.y = 0f;
        bool recentlySawDoor = Memory.LastKnownRoom == origin
            && Memory.TimeSinceLastSeen <= 1.5f && last.sqrMagnitude <= 3f * 3f;
        // Durante una persecución, oír al jugador cruzar un marco próximo da una
        // pista de la habitación de destino; no revela dónde se oculta dentro de ella.
        bool chasingNearby = (CurrentState is AlienChaseState || CurrentState is AlienAlertState)
            && Memory.LastKnownRoom == origin && Memory.TimeSinceLastSeen <= 6f
            && Vector3.Distance(transform.position, entrance.Position) <= 6f;
        bool witnessed = PlayerVisible || recentlySawDoor || chasingNearby;
        if (!witnessed) return;

        Memory.InferPortalCrossing(destination, exit.Position - exit.Outward * .7f);
        observedHouse.RememberPursuit(gameObject, destination);
        PlayerVisible = false; // La observación anterior no es una visión actual en el cuarto remoto.
        if (!traversingDoor && !(CurrentState is AlienChaseState)
            && !(CurrentState is AlienAlertState) && !(CurrentState is AlienCatchPlayerState))
            StartBehaviour(PlayerResponseLoop(true));
    }

    private IEnumerator Start()
    {
        SubscribeToPortalCrossings();
        BindActivityDirector();
        ResolvePlayer();
        while (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying)
            yield return null;
        // Los dos iniciales conservan su espera; el que aparece tras hallar el celular se activa enseguida.
        if (StoryProgression.Instance == null || StoryProgression.Instance.Objective != StoryObjective.Survive)
            yield return new WaitForSeconds(3f);
        sensesEnabled = true;
        StartBehaviour(OrdinaryLoop());
    }

    private void ResolvePlayer()
    {
        if (player == null) player = FindFirstObjectByType<PlayerVisibility>();
        if (!playerSubscribed && player != null && player.HidingController != null)
        {
            player.HidingController.StateChanged -= HandlePlayerHideStateChanged;
            player.HidingController.StateChanged += HandlePlayerHideStateChanged;
            playerSubscribed = true;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (pendingSurveillanceRoom != null && Memory != null && Memory.KnownHidingSpot != null)
            pendingSurveillanceRoom = null;
        if (pendingSurveillanceRoom != null && !traversingDoor && !PlayerVisible
            && !(CurrentState is AlienAlertState) && !(CurrentState is AlienChaseState)
            && !(CurrentState is AlienCatchPlayerState)
            && !(CurrentState is AlienInvestigateKnownHideSpotState))
        {
            RoomModule target = pendingSurveillanceRoom;
            pendingSurveillanceRoom = null;
            if (Time.time < surveillanceClueExpires)
                StartBehaviour(SurveillanceInvestigationLoop(target));
        }
        if (activityDirector == null) BindActivityDirector();
        ResolvePlayer();
        if (enablePlayerDetection && sensesEnabled && Time.time >= nextPerceptionTime) SensePlayer();
        Vector3? lookPoint = FocusPoint;
        if (!lookPoint.HasValue && Target != null) lookPoint = Target.LookPoint;
        if (Head != null) Head.Look(Scanning, lookPoint);
    }

    private void SensePlayer()
    {
        nextPerceptionTime = Time.time + perceptionInterval;
        HouseFlowController house = HouseFlowController.Instance;
        bool visibleNow = player != null && Perception.CanSeePlayer(player, Head.Head,
            Occupant != null ? Occupant.Room : null, house);
        PlayerVisible = visibleNow;
        if (!visibleNow) return;
        RoomOccupant playerOccupant = player.GetComponent<RoomOccupant>();
        Memory.Observe(player, playerOccupant != null ? playerOccupant.Room : null);
        if (playerOccupant != null) house.RememberPursuit(gameObject, playerOccupant.Room);
        if (traversingDoor) return; // Termina de cruzar el marco antes de cambiar de acción.
        if (CurrentState is AlienAlertState || CurrentState is AlienChaseState
            || CurrentState is AlienCatchPlayerState) return;
        // Si ya estaba buscando al jugador, reconocerlo no necesita otra pausa de sorpresa.
        StartBehaviour(PlayerResponseLoop(CurrentState is AlienSearchState
            || CurrentState is AlienInvestigateKnownHideSpotState));
    }

    private void HandlePlayerHideStateChanged(PlayerHideState state, HideSpot spot)
    {
        if (!enablePlayerDetection || !sensesEnabled || state != PlayerHideState.Entering || spot == null
            || player == null) return;
        HouseFlowController house = HouseFlowController.Instance;
        if (!Perception.CanSeePlayer(player, Head.Head, Occupant.Room, house)) return;
        RoomOccupant playerOccupant = player.GetComponent<RoomOccupant>();
        Memory.Observe(player, playerOccupant != null ? playerOccupant.Room : null);
        Memory.RememberHidingSpot(spot);
        StartBehaviour(KnownHidingSpotLoop());
    }

    private void StartBehaviour(IEnumerator routine)
    {
        if (behaviourRoutine != null) StopCoroutine(behaviourRoutine);
        CleanupInterruptedActivity();
        behaviourRoutine = StartCoroutine(routine);
    }

    // State separa las acciones: caminar, observar, acercarse y revisar. Al detectar al Player se interrumpe este ciclo para darle prioridad.
    private IEnumerator OrdinaryLoop()
    {
        while (GameSessionManager.Instance == null || GameSessionManager.Instance.IsPlaying)
        {
            HouseFlowController.Instance?.ForgetPursuit(gameObject);
            expiredInterests.Clear();
            foreach (var entry in cooldowns)
                if (entry.Key == null || entry.Value <= Time.time) expiredInterests.Add(entry.Key);
            foreach (AlienInterest interest in expiredInterests) cooldowns.Remove(interest);
            Target = null;
            // Al terminar una búsqueda no debe seguir mirando su última pared o punto recordado.
            FocusPoint = null;
            Scanning = false;
            yield return Run(wander);
            yield return Run(scan);
            if (Target == null) continue;
            yield return Run(approach);
            if (Motor.Arrived && Target != null && Target.Available)
                yield return Run(Target.Kind == AlienInterestKind.Passage ? traverse : inspect);
            if (CatchRequested)
            {
                yield return Run(catchPlayer);
                yield break;
            }
            if (Target != null)
            {
                RegisterRoutineChoice(Target.Kind);
                cooldowns[Target] = Time.time + activity.InterestCooldown;
            }
            Target = null;
        }
    }

    private IEnumerator PlayerResponseLoop(bool resumeChase = false)
    {
        CatchRequested = false;
        if (!resumeChase) yield return Run(alert);
        if (Memory.KnownHidingSpot != null)
            yield return Run(investigateKnownHide);
        else if (Memory.HasLastKnownPosition)
            yield return Run(chase);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying) yield break;
        if (Memory.KnownHidingSpot != null)
            yield return Run(investigateKnownHide);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying) yield break;
        yield return Run(search);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        Memory.ClearSearch();
        yield return OrdinaryLoop();
    }

    private IEnumerator KnownHidingSpotLoop()
    {
        CatchRequested = false;
        yield return Run(investigateKnownHide);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsPlaying) yield break;
        yield return Run(search);
        if (CatchRequested)
        {
            yield return Run(catchPlayer);
            yield break;
        }
        Memory.ClearSearch();
        yield return OrdinaryLoop();
    }

    public void BeginSoundInvestigation(SoundStimulus stimulus)
    {
        if (!CanReactToSoundStimulus(stimulus)) return;
        StartBehaviour(SoundInvestigationLoop(stimulus));
    }

    public void BeginSoundAssistance(AlienCommunicationSignal signal)
    {
        if (signal == null || signal.Stimulus == null || !CanReactToSound) return;
        StartBehaviour(SoundAssistanceLoop(signal));
    }

    public void ConfirmSoundWasFriendly(int soundId)
    {
        if (soundReactionActive && activeSound != null && activeSound.Id == soundId)
            friendlyResponseSoundId = soundId;
    }

    public void BeginPlayerCallAssistance(PlayerSpottedSignal signal)
    {
        if (signal == null || !CanReactToPlayerCall) return;
        StartBehaviour(PlayerCallAssistanceLoop(signal));
    }

    // La vigilancia comunica una habitación, nunca una posición ni un escondite.
    public void ReceiveSurveillanceClue(RoomModule room)
    {
        if (room == null || !enabled || !gameObject.activeInHierarchy || !sensesEnabled
            || (Memory != null && Memory.KnownHidingSpot != null)) return;
        pendingSurveillanceRoom = room;
        surveillanceClueExpires = Time.time + 30f;
    }

    private IEnumerator SurveillanceInvestigationLoop(RoomModule target)
    {
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null || Occupant == null) yield break;
        SetActivityState("Acude a la vigilancia");
        if (Occupant.Room != target)
        {
            float deadline = Time.time + 20f;
            while (Time.time < deadline && Occupant.Room != target && IsSessionRunning())
            {
                RoomModule origin = Occupant.Room;
                if (origin == null) break;
                foreach (DoorSocket socket in origin.Sockets)
                {
                    if (Occupant.Room == target || Time.time >= deadline) break;
                    bool compatible = false;
                    foreach (DoorSocket arrival in target.Sockets)
                        if (socket.IsAnomalyCompatibleWith(arrival)
                            || (house.UsesRoomPortals && socket.Kind == DoorSocketKind.HallAccess
                                && arrival.Kind == DoorSocketKind.Interior))
                        { compatible = true; break; }
                    if (!compatible) continue;
                    RoomConnection current = house.ConnectionFor(socket);
                    if (current != null && current.SourceRoom != target && current.TargetRoom != target)
                        continue;
                    yield return CrossDoor(socket, target);
                }
                if (Occupant.Room != target) yield return new WaitForSeconds(.5f);
            }
            // Si el destino no liberó ninguna puerta compatible, conserva una ruta normal válida.
            if (Occupant.Room != target && IsSessionRunning()) yield return TravelToRoom(target);
        }
        if (Occupant.Room == target && IsSessionRunning())
        {
            Memory.InferPortalCrossing(target, transform.position);
            yield return Run(search);
            Memory.ClearSearch();
        }
        if (IsSessionRunning()) yield return OrdinaryLoop();
    }

    private IEnumerator PlayerCallAssistanceLoop(PlayerSpottedSignal signal)
    {
        playerCallReactionActive = true;
        LastPlayerCallSender = signal.Sender;
        SetActivityState("Acude al jugador detectado");
        FocusPoint = signal.WorldPosition;
        Motor.SetSpeed(ChaseSpeed);
        if (signal.Room != null && Occupant.Room != signal.Room)
            yield return TravelToRoom(signal.Room);
        if (signal.Room != null && Occupant.Room == signal.Room)
            yield return SearchSoundArea(signal.Room, signal.WorldPosition, ChaseSpeed);
        playerCallReactionActive = false;
        if (IsSessionRunning()) yield return OrdinaryLoop();
    }

    private IEnumerator SoundInvestigationLoop(SoundStimulus stimulus)
    {
        soundReactionActive = true;
        activeSound = stimulus;
        latestRunningSound = stimulus.Kind == SuspiciousSoundKind.PlayerRunningStep ? stimulus : null;
        friendlyResponseSoundId = 0;
        SetActivityState("Escucha un ruido");
        Motor.Stop();
        RoomModule room = Occupant != null ? Occupant.Room : null;
        Vector3 target = stimulus.PositionFor(room);
        FocusPoint = target;

        float turnDuration = stimulus.Kind == SuspiciousSoundKind.PlayerRunningStep
            ? Mathf.Min(turnTowardSoundDuration, .30f) : turnTowardSoundDuration;
        float turnUntil = Time.time + turnDuration;
        while (Time.time < turnUntil && IsSessionRunning())
        {
            TurnTowardSound();
            yield return null;
        }

        if (!soundReactionActive || activeSound != stimulus) yield break;
        SetActivityState("Pregunta por el ruido");
        Voice?.PlayQuestionVocalization();
        SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Question,
            stimulus, this, room, target);

        float waitMinimum = Mathf.Max(.1f, responseWaitRange.x);
        float waitMaximum = Mathf.Max(waitMinimum, responseWaitRange.y);
        float responseDeadline = Time.time + Random.Range(waitMinimum, waitMaximum);
        while (Time.time < responseDeadline && friendlyResponseSoundId != stimulus.Id
            && IsSessionRunning())
        {
            TurnTowardSound();
            yield return null;
        }

        if (friendlyResponseSoundId == stimulus.Id)
        {
            EndSoundReaction();
            yield return OrdinaryLoop();
            yield break;
        }

        SetActivityState("Llama a su compañero");
        Voice?.PlayCallVocalization();
        room = Occupant != null ? Occupant.Room : room;
        target = (latestRunningSound ?? stimulus).PositionFor(room);
        SoundSignalSystem.EmitCommunication(AlienCommunicationKind.Call,
            stimulus, this, room, target);
        yield return SearchSoundArea(room, target, SearchSpeed);
        EndSoundReaction();
        if (IsSessionRunning()) yield return OrdinaryLoop();
    }

    private IEnumerator SoundAssistanceLoop(AlienCommunicationSignal signal)
    {
        soundReactionActive = true;
        activeSound = signal.Stimulus;
        friendlyResponseSoundId = 0;
        SetActivityState("Acude a la llamada");
        RoomModule destinationRoom = signal.SearchRoom;
        FocusPoint = signal.SearchWorldPosition;
        Motor.SetSpeed(assistanceSpeed);
        if (destinationRoom != null && Occupant.Room != destinationRoom)
            yield return TravelToRoom(destinationRoom);
        if (destinationRoom == null || Occupant.Room == destinationRoom)
            yield return SearchSoundArea(destinationRoom, signal.SearchWorldPosition,
                assistanceSpeed);
        EndSoundReaction();
        if (IsSessionRunning()) yield return OrdinaryLoop();
    }

    private IEnumerator SearchSoundArea(RoomModule room, Vector3 center, float speed)
    {
        if (room == null || Occupant == null) yield break;
        if (Occupant.Room != room) yield return TravelToRoom(room);
        if (Occupant.Room != room) yield break;

        SetActivityState("Investiga el sonido");
        Motor.SetSpeed(speed);
        center = room == Occupant.Room ? center : transform.position;
        FocusPoint = center;
        yield return Motor.Go(center, 8f);

        float minimum = Mathf.Max(.5f, soundSearchDuration.x);
        float maximum = Mathf.Max(minimum, soundSearchDuration.y);
        float end = Time.time + Random.Range(minimum, maximum);
        while (Time.time < end && IsSessionRunning())
        {
            Scanning = true;
            FocusPoint = null;
            Vector2 offset = Random.insideUnitCircle * soundSearchRadius;
            Vector3 point = center + new Vector3(offset.x, 0f, offset.y);
            Motor.SetDestination(point, 1f, room);
            float moveUntil = Mathf.Min(end, Time.time + Random.Range(.75f, 1.35f));
            while (Time.time < moveUntil && IsSessionRunning()) yield return null;
            Motor.Stop();
            Scanning = false;
            FocusPoint = center;
            float observeUntil = Mathf.Min(end, Time.time + Random.Range(.35f, .75f));
            while (Time.time < observeUntil && IsSessionRunning()) yield return null;
        }
        Motor.Stop();
        Scanning = false;
    }

    private bool IsSessionRunning()
        => GameSessionManager.Instance == null || GameSessionManager.Instance.IsPlaying;

    private void SetActivityState(string activity)
    {
        CurrentState = null;
        StateName = activity;
        onStateChanged.Invoke(StateName);
        if (verboseLogging) Debug.Log(name + " -> " + StateName, this);
    }

    private void TurnTowardSound()
    {
        if (!FocusPoint.HasValue) return;
        Vector3 direction = FocusPoint.Value - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < .001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.LookRotation(direction), 180f * Time.deltaTime);
    }

    private void EndSoundReaction()
    {
        if (activeSound != null) SoundSignalSystem.ReleaseLead(activeSound, this);
        soundReactionActive = false;
        activeSound = null;
        latestRunningSound = null;
        friendlyResponseSoundId = 0;
    }

    private IEnumerator Run(AlienState state)
    {
        CurrentState = state;
        StateName = state.Name;
        onStateChanged.Invoke(StateName);
        if (verboseLogging) Debug.Log(name + " -> " + StateName, this);
        yield return state.Execute(this);
        if (CurrentState == state) CurrentState = null;
    }

    public void RequestCatch() => CatchRequested = true;

    public IEnumerator TravelToRoom(RoomModule destinationRoom)
    {
        if (destinationRoom == null || Occupant == null || Occupant.Room == destinationRoom)
            yield break;
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null) yield break;
        // Calcula el camino con las puertas conocidas. El destino sale de la memoria del alien, sin consultar la posición actual del jugador.
        var attempted = new HashSet<DoorSocket>();
        for (int hop = 0; hop < house.ModuleById.Count && Occupant.Room != destinationRoom; hop++)
        {
            DoorSocket socket = NextRouteDoor(destinationRoom, attempted);
            if (socket == null) yield break;
            attempted.Add(socket);
            yield return CrossDoor(socket);
        }
    }

    private DoorSocket NextRouteDoor(RoomModule destination, HashSet<DoorSocket> attempted)
    {
        HouseFlowController house = HouseFlowController.Instance;
        // Durante el segundo de gracia de una puerta cerrada, conserva exactamente
        // el paso que acaba de usar el jugador para que la persecución continúe.
        foreach (DoorSocket socket in Occupant.Room.Sockets)
        {
            if (attempted.Contains(socket)) continue;
            RoomConnection direct = house.ConnectionFor(socket);
            if (direct == null || (direct.IsAnomalous && !house.CanFollow(direct, gameObject))) continue;
            RoomModule next = direct.SourceRoom == Occupant.Room ? direct.TargetRoom : direct.SourceRoom;
            if (next == destination) return socket;
        }
        var frontier = new Queue<RoomModule>();
        var firstDoor = new Dictionary<RoomModule, DoorSocket>();
        frontier.Enqueue(Occupant.Room);
        firstDoor[Occupant.Room] = null;
        while (frontier.Count > 0)
        {
            RoomModule room = frontier.Dequeue();
            foreach (DoorSocket socket in room.Sockets)
            {
                if (attempted.Contains(socket) || IsCoolingDown(socket.GetComponent<DoorInterest>())) continue;
                RoomConnection connection = house.ConnectionFor(socket);
                RoomModule next;
                if (connection != null)
                {
                    if (connection.IsAnomalous && !house.CanFollow(connection, gameObject)) continue;
                    next = connection.SourceRoom == room ? connection.TargetRoom : connection.SourceRoom;
                }
                else next = house.NormalDestination(socket)?.Room;
                if (next == null || firstDoor.ContainsKey(next)) continue;
                firstDoor[next] = firstDoor[room] != null ? firstDoor[room] : socket;
                if (next == destination) return firstDoor[next];
                frontier.Enqueue(next);
            }
        }
        return null;
    }

    public IEnumerator CrossDoor(DoorSocket socket, RoomModule surveillanceDestination = null)
    {
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null || socket == null || Occupant.Room != socket.Room) yield break;
        RoomModule origin = Occupant.Room;
        traversingDoor = true;
        try
        {
            for (int attempt = 0; attempt < 3 && Occupant.Room == origin; attempt++)
            {
                Vector3 approach = socket.ClearApproachPoint
                    + socket.transform.right * (attempt == 0 ? 0f : attempt == 1 ? .4f : -.4f);
                yield return Motor.Go(approach, 4f);
                if (!Motor.Arrived) continue;
                if (surveillanceDestination != null)
                    house.RequestSurveillanceOpen(socket, this, surveillanceDestination);
                else house.RequestOpen(socket, gameObject);
                float deadline = Time.time + 3.5f;
                RoomConnection connection = null;
                while (Time.time < deadline)
                {
                    connection = house.ConnectionFor(socket);
                    if (connection != null && connection.SourceSocket.Door.IsOpen
                        && (surveillanceDestination == null || connection.SourceRoom == surveillanceDestination
                            || connection.TargetRoom == surveillanceDestination)) break;
                    yield return null;
                }
                house.CancelDoorRequest(gameObject);
                if (connection == null || !connection.SourceSocket.Door.IsOpen
                    || (surveillanceDestination != null && connection.SourceRoom != surveillanceDestination
                        && connection.TargetRoom != surveillanceDestination)
                    || (connection.IsAnomalous && !house.CanFollow(connection, gameObject))
                    || !house.ReservePassage(connection, gameObject)) continue;
                Passage = connection;
                yield return null;
                DoorSocket exit = connection.SourceSocket == socket
                    ? connection.TargetSocket : connection.SourceSocket;
                if (house.UsesRoomPortals)
                {
                    Vector3 threshold = socket.Position - socket.Outward * .24f;
                    yield return Motor.Go(threshold, 3f);
                    if (Motor.Arrived)
                    {
                        PortalTrialGate gate = connection.SourceSocket == socket
                            ? connection.SourceGate : connection.TargetGate;
                        Vector3 arrival = exit.Position - exit.Outward * .65f;
                        if (gate != null && Motor.TryPoint(arrival, .8f, out Vector3 ground))
                        {
                            yield return WalkThroughPortal(house, socket, gate, ground, connection, exit.Room);
                        }
                    }
                }
                else
                {
                    yield return Motor.Go(exit.ClearApproachPoint, 6f);
                    if (Motor.Arrived) Occupant.Enter(exit.Room);
                }
                if (!Motor.Arrived)
                {
                    DoorSocket safeSide = Occupant.Room == exit.Room ? exit : socket;
                    yield return Motor.Go(safeSide.ClearApproachPoint, 3f);
                }
                house.ReleasePassage(connection, gameObject);
                Passage = null;
                if (Occupant.Room != origin) connection.Pursuers.Remove(gameObject);
                // Pide cerrar inmediatamente después de liberar su reserva.
                // Si otro personaje sigue en el marco, la casa conserva la orden hasta que salga.
                house.RequestClose(connection.SourceSocket, gameObject);
                deadline = Time.time + 1.5f;
                while (house.ConnectionFor(socket) == connection
                    && connection.SourceSocket.Door.State != DoorState.Closed && Time.time < deadline)
                    yield return null;
                if (Occupant.Room != origin) yield break;
                yield return new WaitForSeconds(.25f + attempt * .2f);
            }
        }
        finally
        {
            house.CancelDoorRequest(gameObject);
            if (Passage != null)
            {
                house.ReleasePassage(Passage, gameObject);
                house.CloseAfterPassage(Passage, gameObject);
                Passage = null;
            }
            traversingDoor = false;
            if (Occupant.Room == origin && socket != null)
            {
                DoorInterest interest = socket.GetComponent<DoorInterest>();
                if (interest != null) cooldowns[interest] = Time.time + 8f;
            }
        }
    }

    private IEnumerator WalkThroughPortal(HouseFlowController house, DoorSocket entrance, PortalTrialGate gate,
        Vector3 destinationGround, RoomConnection connection, RoomModule destinationRoom)
    {
        var agent = Motor.Agent;
        Vector3 sourceGround = transform.position;
        Quaternion sourceFacing = transform.rotation;
        float speed = Mathf.Max(.65f, agent.speed);
        bool crossed = false;
        Motor.Stop();
        agent.enabled = false;
        Motor.SetManualMotionSpeed(speed);
        try
        {
            // El agente llega por el NavMesh; el último tramo hasta el plano se
            // recorre visualmente para no saltar desde antes del marco.
            Vector3 sourcePlane = entrance.Position - entrance.Outward * .015f;
            yield return WalkStraight(sourcePlane, speed);
            transform.SetPositionAndRotation(gate.ExitPoint(transform.position),
                gate.MapRotation(transform.rotation));
            Physics.SyncTransforms();
            crossed = true;
            house.NotifyPortalCrossing(Occupant, connection, destinationRoom);
            // Tras aparecer exactamente en el plano de salida, termina los pasos
            // hasta el NavMesh del cuarto nuevo antes de devolver el control al agente.
            yield return WalkStraight(destinationGround, speed);
        }
        finally
        {
            Motor.SetManualMotionSpeed(0f);
            if (crossed) transform.position = destinationGround;
            else transform.SetPositionAndRotation(sourceGround, sourceFacing);
            agent.enabled = true;
            agent.Warp(transform.position);
            Physics.SyncTransforms();
        }
    }

    private IEnumerator WalkStraight(Vector3 destination, float speed)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
        Quaternion facing = horizontal.sqrMagnitude > .001f
            ? Quaternion.LookRotation(horizontal.normalized, Vector3.up) : transform.rotation;
        while ((transform.position - destination).sqrMagnitude > .0004f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);
            yield return null;
        }
        transform.position = destination;
    }

    public void SetPlayerDetectionEnabled(bool enabled)
    {
        enablePlayerDetection = enabled;
        if (!enabled) PlayerVisible = false;
    }

    public void SetHearingEnabled(bool enabled)
    {
        hearingEnabled = enabled;
        if (!enabled && soundReactionActive)
        {
            StartBehaviour(OrdinaryLoop());
        }
    }

    public bool IsCoolingDown(AlienInterest interest)
        => interest != null && cooldowns.TryGetValue(interest, out float until) && Time.time < until;

    private void BindActivityDirector()
    {
        AlienActivityDirector director = AlienActivityDirector.Instance;
        if (activityDirector == director) return;
        if (activityDirector != null) activityDirector.ActivityChanged -= HandleActivityChanged;
        activityDirector = director;
        if (activityDirector == null) return;
        activityDirector.ActivityChanged += HandleActivityChanged;
        HandleActivityChanged(activityDirector.Current);
    }

    private void HandleActivityChanged(AlienActivityTuning tuning)
    {
        // El evento cambia los valores que usarán los estados en sus próximos pasos.
        // No corta una persecución, una inspección ni un cruce que estén en marcha.
        activity = tuning;
    }

    private void RegisterRoutineChoice(AlienInterestKind kind)
    {
        // Si repitió mucho una clase de decisión, la otra gana peso en el próximo
        // ciclo. Esto genera recorridos variados sin revelar dónde está el jugador.
        routineChoicesWithoutHideSpot = kind == AlienInterestKind.HideSpot
            ? 0 : routineChoicesWithoutHideSpot + 1;
        routineChoicesWithoutPassage = kind == AlienInterestKind.Passage
            ? 0 : routineChoicesWithoutPassage + 1;
    }

    public void NotifyAlertStarted()
    {
        Voice?.PlayCallVocalization();
        RoomModule room = Occupant != null ? Occupant.Room : null;
        Vector3 lastSeen = Memory != null && Memory.HasLastKnownPosition
            ? Memory.LastKnownPosition : transform.position;
        SoundSignalSystem.EmitPlayerSpotted(this, room, lastSeen);
        onAlertStarted.Invoke();
    }


    public void NotifyChaseStarted() => Voice?.PlayChaseVocalization();

    /// <summary>Todos los estados revisan el escondite de la misma manera: comprueban si está ocupado, sin importar por qué llegaron hasta él.</summary>
    public bool TryBeginInspection(IInspectable inspectable)
    {
        if (inspectable == null || !inspectable.TryBeginInspection(gameObject)) return false;
        Inspection = inspectable;
        if (inspectable is HideSpot spot && spot.IsOccupied) RequestCatch();
        return true;
    }

    public void FinishInspection()
    {
        IInspectable inspection = Inspection;
        Inspection = null;
        if (inspection is Object unityObject && unityObject == null) return;
        inspection?.EndInspection(gameObject);
    }

    private void CleanupInterruptedActivity()
    {
        EndSoundReaction();
        playerCallReactionActive = false;
        FinishInspection();
        Scanning = false;
        FocusPoint = null;
        Motor?.Stop();
        traversingDoor = false;
        HouseFlowController.Instance?.CancelDoorRequest(gameObject);
        if (Passage != null && HouseFlowController.Instance != null)
        {
            HouseFlowController.Instance.ReleasePassage(Passage, gameObject);
            HouseFlowController.Instance.CloseAfterPassage(Passage, gameObject);
        }
        Passage = null;
    }

    private void OnDisable()
    {
        if (observedHouse != null) observedHouse.PortalCrossed -= HandlePortalCrossed;
        observedHouse = null;
        if (activityDirector != null) activityDirector.ActivityChanged -= HandleActivityChanged;
        activityDirector = null;
        if (player != null && player.HidingController != null)
            player.HidingController.StateChanged -= HandlePlayerHideStateChanged;
        playerSubscribed = false;
        CleanupInterruptedActivity();
        HouseFlowController.Instance?.ForgetPursuit(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        AlienPerception perception = Perception != null ? Perception : GetComponent<AlienPerception>();
        AlienHeadScanner scanner = Head != null ? Head : GetComponent<AlienHeadScanner>();
        if (perception == null || scanner == null || scanner.Head == null) return;
        Transform eyes = scanner.Head;
        float half = perception.FieldOfView * .5f;
        Gizmos.color = PlayerVisible ? Color.red : new Color(1f, .7f, .1f, .75f);
        Gizmos.DrawRay(eyes.position, Quaternion.AngleAxis(-half, Vector3.up) * eyes.forward * perception.Range);
        Gizmos.DrawRay(eyes.position, Quaternion.AngleAxis(half, Vector3.up) * eyes.forward * perception.Range);
        if (FocusPoint.HasValue) Gizmos.DrawLine(eyes.position, FocusPoint.Value);
    }
}
