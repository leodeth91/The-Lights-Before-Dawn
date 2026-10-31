using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Coordina las habitaciones compartidas. Sus ocupantes y puertas conectadas determinan cuándo pueden volver al pool.</summary>
[DisallowMultipleComponent]
public sealed class HouseFlowController : MonoBehaviour
{
    public static HouseFlowController Instance { get; private set; }
    [SerializeField] private List<RoomModule> roomModules = new List<RoomModule>();
    [SerializeField] private Transform modulePoolRoot;
    [SerializeField] private RoomModule initialRoom;
    [SerializeField] private Transform player;
    [Header("Anomaly randomness")]
    [Tooltip("Genera una semilla nueva al cargar cada partida. Desactivar permite usar una semilla fija para pruebas.")]
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int randomSeed = 20260901;
    [Tooltip("Cada conexión elegible sortea un destino seguro. Desactivar permite probar la probabilidad configurada abajo.")]
    [SerializeField] private bool randomizeEveryEligibleDoor = true;
    [SerializeField, Range(0f, 1f)] private float anomalyChance = .4f;
    [SerializeField] private List<NormalRoomConnection> normalConnections = new List<NormalRoomConnection>();
    [SerializeField] private bool verboseLogging;
    [Header("Room portals")]
    [SerializeField] private bool useRoomPortals = true;
    [SerializeField, Min(0f)] private float closedDoorGrace = .25f;
    [Header("Door coordination")]
    [SerializeField, Min(1f)] private float requestLifetime = 10f;
    [SerializeField, Min(1f)] private float passageLifetime = 8f;
    [SerializeField, Min(1f)] private float pursuitLifetime = 12f;
    [SerializeField] private Vector2 automaticCloseDelay = new Vector2(9f, 16f);
    private readonly Dictionary<string, RoomModule> moduleById = new Dictionary<string, RoomModule>();
    private readonly Dictionary<string, DoorSocket> socketById = new Dictionary<string, DoorSocket>();
    private readonly Dictionary<string, string> normalDestinationBySocketId = new Dictionary<string, string>();
    private readonly List<RoomConnection> connections = new List<RoomConnection>();
    private readonly List<RoomOccupant> occupants = new List<RoomOccupant>();
    private readonly Dictionary<RoomModule, Pose> parking = new Dictionary<RoomModule, Pose>();
    private readonly HashSet<RoomConnection> closeWhenClear = new HashSet<RoomConnection>();
    private readonly HashSet<RoomConnection> immediatePlayerCloses = new HashSet<RoomConnection>();
    private readonly Dictionary<RoomConnection, GameObject> closeRequestedBy
        = new Dictionary<RoomConnection, GameObject>();
    private sealed class DoorRequest
    {
        public DoorSocket Socket;
        public GameObject Actor;
        public RoomModule DirectedRoom;
        public float Expires, NextAttempt;
        public bool IsPlayer;
    }
    // Guarda una solicitud por personaje. Dentro de cada prioridad atiende primero al que pidió antes, sin duplicar intentos.
    // La lista conserva el orden de llegada y permite cancelar una petición. Un stack atendería primero la última y podría dejar esperando a las anteriores.
    private readonly List<DoorRequest> requests = new List<DoorRequest>();
    private readonly List<GameObject> expiredActors = new List<GameObject>();
    private readonly List<RoomConnection> connectionSnapshot = new List<RoomConnection>();
    public int PendingRequestCount => requests.Count;
    public int ActiveSeed { get; private set; }
    private AnomalyDirector anomalyDirector;
    private System.Random connectionRandom;
    public RoomModule CurrentRoom { get; private set; }
    public RoomConnection ActiveConnection => connections.Find(c => c.SourceRoom == CurrentRoom || c.TargetRoom == CurrentRoom);
    public IReadOnlyList<RoomConnection> Connections => connections;
    public IReadOnlyList<RoomOccupant> Occupants => occupants;
    public IReadOnlyDictionary<string, RoomModule> ModuleById => moduleById;
    public int RegisteredSocketCount => socketById.Count;
    public bool LastConnectionWasAnomalous { get; private set; }
    public bool UsesRoomPortals => useRoomPortals;
    public event Action<RoomConnection> ConnectionOpened;
    public event Action<RoomConnection> ConnectionClosed;
    public event Action<RoomModule, RoomModule> CurrentRoomChanged;
    public event Action<RoomConnection, GameObject, RoomModule> PortalCrossed;

    public void Configure(List<RoomModule> modules, RoomModule startingRoom, Transform poolRoot,
        Transform playerTransform, int seed, List<NormalRoomConnection> topology, float chance)
    {
        roomModules = modules; initialRoom = startingRoom; modulePoolRoot = poolRoot;
        player = playerTransform; randomSeed = seed; normalConnections = topology; anomalyChance = chance;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Una semilla por partida produce una secuencia nueva sin volver a crear el azar en cada puerta.
        // Se conserva para poder consultarla; los intentos de apertura usan la misma secuencia.
        ActiveSeed = randomizeSeed ? Guid.NewGuid().GetHashCode() : randomSeed;
        anomalyDirector = new AnomalyDirector(new RandomRoomSelectionStrategy(ActiveSeed));
        connectionRandom = new System.Random(ActiveSeed ^ 0x5F3759DF);
        if (verboseLogging) Debug.Log("Anomaly seed: " + ActiveSeed);
        if (modulePoolRoot != null) roomModules = new List<RoomModule>(modulePoolRoot.GetComponentsInChildren<RoomModule>(true));
        if (player == null) player = GameObject.FindWithTag("Player")?.transform;
        foreach (RoomModule room in roomModules)
        {
            if (room == null) continue;
            if (!moduleById.TryAdd(room.RoomId, room)) { Debug.LogError("Duplicate room: " + room.RoomId); continue; }
            parking[room] = new Pose(room.transform.position, room.transform.rotation);
            room.RefreshSockets(); room.ResetDoorsInstant(); room.SetCurrent(false);
            foreach (DoorSocket socket in room.Sockets)
            {
                if (!socketById.TryAdd(socket.SocketId, socket)) Debug.LogError("Duplicate socket: " + socket.SocketId);
                if (useRoomPortals) socket.EnsureThresholdFloor();
                if (socket.Door != null) socket.Door.Closed += HandleDoorClosed;
            }
        }
        foreach (NormalRoomConnection pair in normalConnections)
        {
            if (!socketById.ContainsKey(pair.SocketA) || !socketById.ContainsKey(pair.SocketB))
            { Debug.LogError("Unknown normal connection: " + pair.SocketA + " / " + pair.SocketB); continue; }
            normalDestinationBySocketId[pair.SocketA] = pair.SocketB;
            normalDestinationBySocketId[pair.SocketB] = pair.SocketA;
        }
        if (initialRoom == null || !roomModules.Contains(initialRoom)) moduleById.TryGetValue("child_room", out initialRoom);
        foreach (RoomModule room in roomModules) if (room != null) room.gameObject.SetActive(room == initialRoom);
        if (initialRoom != null)
        {
            Vector3 translation = -initialRoom.transform.position;
            translation.y = 0;
            initialRoom.transform.position += translation;
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                bool enabledBefore = cc != null && cc.enabled;
                if (enabledBefore) cc.enabled = false;
                player.position += translation;
                if (enabledBefore) cc.enabled = true;
                RegisterOccupant(player.gameObject, initialRoom, false);
                if (useRoomPortals && player.GetComponent<HousePortalTraveller>() == null)
                    player.gameObject.AddComponent<HousePortalTraveller>();
            }
            SetCurrentRoom(initialRoom);
        }
        Physics.SyncTransforms();
        foreach (RoomModule room in roomModules)
        {
            if (room != null && room.gameObject.activeInHierarchy)
            {
                ReconcileThresholdSensors(room);
            }
        }
    }
    public RoomOccupant RegisterOccupant(GameObject actor, RoomModule room, bool normalDoors)
    {
        RoomOccupant occupant = actor.GetComponent<RoomOccupant>() ?? actor.AddComponent<RoomOccupant>();
        occupant.Configure(room, normalDoors);
        if (!occupants.Contains(occupant)) occupants.Add(occupant);
        if (room != null) room.gameObject.SetActive(true);
        return occupant;
    }
    private void Start()
    {
        if (!useRoomPortals) return;
        foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (body != null && !body.isKinematic && body.GetComponent<HousePortalTraveller>() == null)
                body.gameObject.AddComponent<HousePortalTraveller>();
    }
    public void NotifyPortalCrossing(RoomOccupant occupant, RoomConnection connection, RoomModule destination)
    {
        if (occupant == null || connection == null || destination == null || !connections.Contains(connection)) return;
        occupant.Enter(destination);
        if (occupant.UsesNormalDoors)
        {
            connection.Pursuers.Remove(occupant.gameObject);
            CloseAfterPassage(connection, occupant.gameObject);
        }
        if (player != null && occupant.transform == player) SetCurrentRoom(destination);
        PortalCrossed?.Invoke(connection, occupant.gameObject, destination);
    }
    public bool IsOccupied(RoomModule room) => occupants.Exists(o => o != null && o.gameObject.activeInHierarchy && o.Room == room);
    public RoomConnection ConnectionFor(DoorSocket socket) => connections.Find(c => c.SourceSocket == socket || c.TargetSocket == socket);
    public DoorSocket NormalDestination(DoorSocket socket)
    {
        return socket != null && normalDestinationBySocketId.TryGetValue(socket.SocketId, out string id)
            && socketById.TryGetValue(id, out DoorSocket target) ? target : null;
    }
    public bool RequestOpen(DoorSocket socket, GameObject actor)
        => RequestOpenInternal(socket, actor, null);

    // Solo una orden de vigilancia puede pedir este destino; la conexión sigue pasando por CanPlace.
    public bool RequestSurveillanceOpen(DoorSocket socket, AlienController alien, RoomModule destination)
    {
        if (alien == null || destination == null || StoryProgression.Instance == null
            || !StoryProgression.Instance.AnomalyEnabled) return false;
        return RequestOpenInternal(socket, alien.gameObject, destination);
    }

    private bool RequestOpenInternal(DoorSocket socket, GameObject actor, RoomModule directedRoom)
    {
        if (actor == null) actor = player != null ? player.gameObject : null;
        RoomOccupant occupant = actor != null ? actor.GetComponent<RoomOccupant>() : null;
        if (socket == null || occupant == null || !actor.activeInHierarchy) return false;
        RoomConnection existing = ConnectionFor(socket);
        if (existing != null && existing.TargetRoom == occupant.Room) socket = existing.TargetSocket;
        if (socket.Room != occupant.Room) return false;
        if (directedRoom != null && (directedRoom == occupant.Room ||
            (existing != null && existing.SourceRoom != directedRoom && existing.TargetRoom != directedRoom))) return false;
        if (occupant.UsesNormalDoors && existing != null && existing.IsAnomalous
            && directedRoom == null && !CanFollow(existing, actor)) return false;
        if (TryOpenNow(socket, actor, !occupant.UsesNormalDoors, directedRoom)) { CancelDoorRequest(actor); return true; }
        DoorRequest request = requests.Find(r => r.Actor == actor);
        if (request != null && request.Socket == socket && request.DirectedRoom == directedRoom) return true;
        CancelDoorRequest(actor);
        requests.Add(new DoorRequest { Socket = socket, Actor = actor, DirectedRoom = directedRoom,
            IsPlayer = !occupant.UsesNormalDoors, Expires = Time.time + requestLifetime,
            NextAttempt = Time.time + .05f });
        return true; // Solicitud aceptada; la puerta puede seguir esperando para abrir.
    }
    public void CancelDoorRequest(GameObject actor) => requests.RemoveAll(r => r.Actor == actor);
    public bool CanFollow(RoomConnection connection, GameObject actor)
        => connection != null && actor != null && connection.Pursuers.TryGetValue(actor, out float until)
            && until > Time.time;
    public void RememberPursuit(GameObject actor, RoomModule destination)
    {
        RoomModule origin = actor.GetComponent<RoomOccupant>()?.Room;
        foreach (RoomConnection connection in connections)
            if ((connection.SourceRoom == origin && connection.TargetRoom == destination)
                || (connection.TargetRoom == origin && connection.SourceRoom == destination))
                connection.Pursuers[actor] = Time.time + pursuitLifetime;
    }
    public void ForgetPursuit(GameObject actor)
    {
        foreach (RoomConnection connection in connections) connection.Pursuers.Remove(actor);
    }
    // Una salida solicitada por el jugador tiene prioridad sobre nuevos cruces de aliens.
    // Los que ya reservaron el paso terminan de cruzar antes de cerrar o retirar el suelo.
    private bool PlayerNeedsRoom(RoomModule room, RoomConnection connection = null)
    {
        if (room == null) return false;
        foreach (DoorRequest request in requests)
        {
            if (!request.IsPlayer || !IsRequestValid(request)) continue;
            if (connection != null && (request.Socket == connection.SourceSocket
                || request.Socket == connection.TargetSocket)) continue;
            if (request.Socket.Room == room) return true;
            StoryProgression story = StoryProgression.Instance;
            RoomModule destination = story != null && story.ShouldLoopExit(request.Socket)
                ? story.ChildRoom : NormalDestination(request.Socket)?.Room;
            if (destination == room) return true;
        }
        return false;
    }
    private bool YieldsToPlayer(RoomConnection connection)
        => PlayerNeedsRoom(connection.SourceRoom, connection)
            || PlayerNeedsRoom(connection.TargetRoom, connection);

    private bool IsRequestValid(DoorRequest request)
        => request.Actor != null && request.Actor.activeInHierarchy && request.Socket != null
            && Time.time < request.Expires
            && request.Actor.GetComponent<RoomOccupant>()?.Room == request.Socket.Room
            && Vector3.Distance(request.Actor.transform.position, request.Socket.Position) <= 4f;

    private bool TryOpenNow(DoorSocket socket, GameObject actor, bool allowAlternative = false,
        RoomModule directedRoom = null)
    {
        RoomOccupant occupant = actor != null ? actor.GetComponent<RoomOccupant>() : null;
        RoomModule actorRoom = occupant != null ? occupant.Room : CurrentRoom;
        bool normalOnly = occupant != null && occupant.UsesNormalDoors && directedRoom == null;
        if (socket == null || socket.Door == null || actorRoom == null) return false;
        if (directedRoom != null && (PlayerNeedsRoom(actorRoom) || PlayerNeedsRoom(directedRoom))) return false;
        RoomConnection existing = ConnectionFor(socket);
        if (directedRoom != null && existing != null
            && existing.SourceRoom != directedRoom && existing.TargetRoom != directedRoom) return false;
        if (normalOnly && (existing != null ? YieldsToPlayer(existing)
            : PlayerNeedsRoom(actorRoom) || PlayerNeedsRoom(NormalDestination(socket)?.Room))) return false;
        if (existing != null)
        {
            if (actorRoom != existing.SourceRoom && actorRoom != existing.TargetRoom) return false;
            if (normalOnly && existing.IsAnomalous && !CanFollow(existing, actor)) return false;
            if (directedRoom != null) existing.Pursuers[actor] = Time.time + pursuitLifetime;
            if (existing.SourceSocket.Door.State == DoorState.Closed
                || existing.SourceSocket.Door.State == DoorState.Closing)
            {
                immediatePlayerCloses.Remove(existing);
                closeWhenClear.Remove(existing);
                closeRequestedBy.Remove(existing);
                existing.AutoCloseAt = NextAutoClose();
                existing.OpenProtectedUntil = Time.time + 1.2f;
                existing.RerollAt = float.PositiveInfinity;
                bool opened = existing.SourceSocket.Door.BeginOpen(actor);
                if (opened && useRoomPortals && existing.TargetSocket.Door != existing.SourceSocket.Door)
                    existing.TargetSocket.Door.BeginOpen(actor);
                return opened;
            }
            return true;
        }
        if (socket.Room != actorRoom) return false;
        RoomConnection other = useRoomPortals ? null : connections.Find(c => c.SourceRoom == actorRoom || c.TargetRoom == actorRoom);
        if (other != null)
        {
            if (normalOnly && Time.time < other.OpenProtectedUntil) return false;
            // Una persecución detenida por una puerta cerrada no debe bloquear las otras salidas.
            if (other.SourceSocket.Door.State == DoorState.Closed && ThresholdClear(other))
            {
                FinalizeConnection(other);
                if (connections.Contains(other)) return false;
            }
            else { CloseAfterPassage(other, actor); TryClose(other, actor); return false; }
        }
        return OpenConnection(socket, actor, normalOnly, allowAlternative, directedRoom);
    }
    public bool RequestClose(DoorSocket socket, GameObject actor = null)
    {
        RoomConnection connection = ConnectionFor(socket);
        if (connection == null) return false;
        // La orden manual del jugador no espera a que su cápsula abandone el marco.
        // El apoyo físico y un pequeño desplazamiento interior evitan dejarlo en el vacío.
        if (actor != null && actor.CompareTag("Player")) immediatePlayerCloses.Add(connection);
        CloseAfterPassage(connection, actor);
        TryClose(connection, actor);
        return true;
    }
    // Reserva el paso durante el cruce. El plazo evita que una acción interrumpida deje una puerta bloqueada para siempre.
    public bool ReservePassage(RoomConnection connection, GameObject actor)
    {
        if (connection == null || actor == null || !connections.Contains(connection)
            || connection.SourceSocket.Door.State == DoorState.Closing
            || connection.SourceSocket.Door.State == DoorState.Closed) return false;
        if (!connection.PassageUsers.ContainsKey(actor) && YieldsToPlayer(connection)) return false;
        connection.PassageUsers[actor] = Time.time + passageLifetime;
        return true;
    }
    public void ReleasePassage(RoomConnection connection, GameObject actor)
    {
        if (connection != null && actor != null) connection.PassageUsers.Remove(actor);
    }
    public void CloseAfterPassage(RoomConnection connection, GameObject actor = null)
    {
        if (connection == null || !connections.Contains(connection)) return;
        closeWhenClear.Add(connection);
        if (actor != null) closeRequestedBy[connection] = actor;
    }
    private bool TryClose(RoomConnection connection, GameObject actor)
    {
        if (connection == null || !connections.Contains(connection)) return false;
        PruneLeases(connection.PassageUsers);
        if (connection.PassageUsers.Count > 0) return false;
        bool immediate = immediatePlayerCloses.Contains(connection);
        if (!immediate && !ThresholdClear(connection)) return false;
        DoorInteractable door = connection.SourceSocket.Door;
        if (door.State == DoorState.Open)
        {
            if (immediate) NudgePlayerInside(connection);
            bool closing = door.BeginClose(actor);
            if (closing && useRoomPortals && connection.TargetSocket.Door != door
                && connection.TargetSocket.Door.State == DoorState.Open)
                connection.TargetSocket.Door.BeginClose(actor);
            return closing;
        }
        return door.State == DoorState.Closing || door.State == DoorState.Closed;
    }
    private bool ThresholdClear(RoomConnection connection)
    {
        // La consulta física cubre entradas por teletransporte o movimiento entre
        // FixedUpdates que todavía no hayan generado OnTriggerEnter.
        connection.SourceSocket.RefreshThresholdOccupancy();
        connection.TargetSocket.RefreshThresholdOccupancy();
        return !connection.SourceSocket.IsThresholdOccupied
            && !connection.TargetSocket.IsThresholdOccupied;
    }

    private void NudgePlayerInside(RoomConnection connection)
    {
        if (player == null) return;
        connection.SourceSocket.RefreshThresholdOccupancy();
        connection.TargetSocket.RefreshThresholdOccupancy();
        GameObject actor = player.gameObject;
        DoorSocket socket = null;
        RoomOccupant occupant = player.GetComponent<RoomOccupant>();
        if (occupant != null && occupant.Room == connection.SourceRoom
            && connection.SourceSocket.IsActorInThreshold(actor)) socket = connection.SourceSocket;
        else if (occupant != null && occupant.Room == connection.TargetRoom
            && connection.TargetSocket.IsActorInThreshold(actor)) socket = connection.TargetSocket;
        if (socket == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();
        float radius = controller != null ? controller.radius : .25f;
        float outward = Vector3.Dot(player.position - socket.Position, socket.Outward);
        float displacement = Mathf.Clamp(outward + radius + .10f, 0f, .55f);
        if (displacement <= 0f) return;
        Vector3 inward = -socket.Outward * displacement;
        if (controller != null && controller.enabled) controller.Move(inward);
        else player.position += inward;
        Physics.SyncTransforms();
    }

    private void ReopenForThresholdOccupant(RoomConnection connection)
    {
        GameObject actor = connection.SourceSocket.ThresholdActor ?? connection.TargetSocket.ThresholdActor;
        connection.SourceSocket.Door.BeginOpen(actor);
        if (connection.TargetSocket.Door != connection.SourceSocket.Door)
            connection.TargetSocket.Door.BeginOpen(actor);
        CloseAfterPassage(connection, actor);
    }
    private float NextAutoClose() => Time.time + UnityEngine.Random.Range(
        Mathf.Max(1f, automaticCloseDelay.x), Mathf.Max(automaticCloseDelay.x, automaticCloseDelay.y));
    private void PruneLeases(Dictionary<GameObject, float> leases)
    {
        expiredActors.Clear();
        foreach (var lease in leases)
            if (lease.Key == null || !lease.Key.activeInHierarchy || lease.Value <= Time.time
                || (lease.Key.TryGetComponent(out AlienController alien) && !alien.enabled))
                expiredActors.Add(lease.Key);
        foreach (GameObject actor in expiredActors) leases.Remove(actor);
    }
    private bool OpenConnection(DoorSocket source, GameObject actor, bool normalOnly, bool allowAlternative,
        RoomModule directedRoom = null)
    {
        StoryProgression story = StoryProgression.Instance;
        bool storyExit = !normalOnly && story != null && story.ShouldLoopExit(source);
        DoorSocket normal = NormalDestination(source);
        bool protectedHall = !useRoomPortals && (source.Room.IsAnchorRoom || source.Kind == DoorSocketKind.HallAccess || (normal != null && normal.Room.IsAnchorRoom));
        bool canUseAnomaly = !normalOnly && !protectedHall && (story == null || story.AnomalyEnabled);
        // Si el destino habitual sigue ocupado, una solicitud esperando puede probar otro destino válido.
        bool alternative = allowAlternative && normal != null && !CanPlace(source, normal);
        bool anomaly = canUseAnomaly && (randomizeEveryEligibleDoor || alternative || normal == null || connectionRandom.NextDouble() < anomalyChance);
        RoomConnectionCandidate destination = default;
        // El primer intento de salir vuelve al cuarto del niño; después se sortean destinos de ambas plantas.
        // El destino narrativo también debe superar las comprobaciones de colocación segura.
        if (directedRoom != null)
        {
            foreach (DoorSocket target in directedRoom.Sockets)
                if ((source.IsAnomalyCompatibleWith(target)
                    || (useRoomPortals && source.Kind == DoorSocketKind.HallAccess
                        && target.Kind == DoorSocketKind.Interior)) && CanPlace(source, target))
                { destination = new RoomConnectionCandidate(directedRoom, target); break; }
            if (destination.Room == null) return false;
        }
        else if (storyExit)
        {
            foreach (DoorSocket target in story.ChildRoom.Sockets)
                if (CanPlace(source, target)) { destination = new RoomConnectionCandidate(target.Room, target); break; }
            if (destination.Room == null)
            {
                if (useRoomPortals)
                    foreach (DoorSocket target in story.ChildRoom.Sockets)
                    {
                        RoomConnection blocker = ConnectionFor(target);
                        if (blocker == null) continue;
                        CloseAfterPassage(blocker, actor);
                        TryClose(blocker, actor);
                    }
                return false;
            }
        }
        else if (anomaly)
        {
            var candidates = new List<RoomConnectionCandidate>();
            foreach (RoomModule room in roomModules)
            {
                // Una habitación activa puede estar disponible si está aislada y solo contiene aliens. CanPlace comprueba ocupantes, conexiones y superposiciones antes de moverla.
                // En el modo normal de juego también participa el destino habitual: cualquiera puede salir sorteado.
                if (room == source.Room || room.IsAnchorRoom
                    || (!randomizeEveryEligibleDoor && normal != null && room == normal.Room)) continue;
                foreach (DoorSocket socket in room.Sockets)
                    if ((source.IsAnomalyCompatibleWith(socket) || (useRoomPortals && source.Kind == DoorSocketKind.HallAccess && socket.Kind == DoorSocketKind.Interior))
                        && CanPlace(source, socket)) candidates.Add(new RoomConnectionCandidate(room, socket));
            }
            destination = anomalyDirector.SelectDestination(candidates);
        }
        bool isAnomaly = destination.Room != null;
        if (destination.Room == null && normal != null && CanPlace(source, normal)) destination = new RoomConnectionCandidate(normal.Room, normal);
        if (destination.Room == null && useRoomPortals && normal != null)
            foreach (DoorSocket alternativeSocket in normal.Room.Sockets)
                if (CanPlace(source, alternativeSocket)
                    && (source.IsAnomalyCompatibleWith(alternativeSocket) || source.Kind == DoorSocketKind.HallAccess))
                { destination = new RoomConnectionCandidate(normal.Room, alternativeSocket); break; }
        if (destination.Room == null)
        {
            if (useRoomPortals && normal != null)
            {
                RoomConnection blocker = ConnectionFor(normal);
                if (blocker != null) { CloseAfterPassage(blocker, actor); TryClose(blocker, actor); }
            }
            return false;
        }
        RoomModule relocatedRoom = null;
        Pose placement = default;
        if (!useRoomPortals && !TryPlanPlacement(source, destination.Socket, out relocatedRoom, out placement)) return false;
        if (!destination.Room.gameObject.activeSelf)
        {
            destination.Room.ResetDoorsInstant();
            if (!useRoomPortals) destination.Room.transform.SetPositionAndRotation(placement.position, placement.rotation);
            destination.Room.gameObject.SetActive(true);
        }
        else if (relocatedRoom != null) RelocateIsolatedRoom(relocatedRoom, placement);
        if (!useRoomPortals) destination.Socket.SetPortalAssemblyActive(false);
        source.ConnectTo(destination.Socket); destination.Socket.ConnectTo(source);
        var connection = new RoomConnection(source.Room, source, destination.Room, destination.Socket, isAnomaly);
        if (directedRoom != null) connection.Pursuers[actor] = Time.time + pursuitLifetime;
        connection.AutoCloseAt = NextAutoClose();
        connection.OpenProtectedUntil = Time.time + 1.2f;
        connections.Add(connection);
        if (!source.Door.BeginOpen(actor)) { FinalizeConnection(connection); return false; }
        if (useRoomPortals)
        {
            Camera viewer = Camera.main;
            if (viewer == null)
            {
                Debug.LogError("Room portals require a MainCamera.", this);
                source.Door.SetClosedInstant();
                FinalizeConnection(connection);
                return false;
            }
            connection.SourceGate = source.gameObject.AddComponent<PortalTrialGate>();
            connection.TargetGate = destination.Socket.gameObject.AddComponent<PortalTrialGate>();
            connection.SourceGate.Configure(connection.TargetGate, viewer, true, 1.12f, 2f);
            connection.TargetGate.Configure(connection.SourceGate, viewer, true, 1.12f, 2f, true);
            if (destination.Socket.Door != source.Door) destination.Socket.Door.BeginOpen(actor);
        }
        LastConnectionWasAnomalous = isAnomaly;
        if (storyExit) story.CompleteExit();
        ConnectionOpened?.Invoke(connection);
        if (verboseLogging) Debug.Log((isAnomaly ? "ANOMALY " : "NORMAL ") + source.SocketId + " -> " + destination.Socket.SocketId);
        return true;
    }
    private static Pose TargetPose(DoorSocket source, DoorSocket target)
    {
        Transform root = target.Room.transform;
        Quaternion localRotation = Quaternion.Inverse(root.rotation) * target.transform.rotation;
        Quaternion rotation = Quaternion.LookRotation(-source.Outward, Vector3.up) * Quaternion.Inverse(localRotation);
        Vector3 localPosition = root.InverseTransformPoint(target.Position);
        return new Pose(source.Position - rotation * Vector3.Scale(localPosition, root.lossyScale), rotation);
    }
    private bool CanPlace(DoorSocket source, DoorSocket target)
        => useRoomPortals ? source != null && target != null && source.Room != target.Room
            && ConnectionFor(source) == null && ConnectionFor(target) == null
            : TryPlanPlacement(source, target, out _, out _);

    // Calcula la ubicación antes de mover nada: si no hay espacio o la habitación está en uso, se prueba otro destino seguro.
    private bool TryPlanPlacement(DoorSocket source, DoorSocket target, out RoomModule movedRoom, out Pose pose)
    {
        movedRoom = null; pose = default;
        if (target.Room == source.Room || ConnectionFor(target) != null) return false;
        pose = TargetPose(source, target);
        if (target.Room.gameObject.activeSelf)
        {
            if (Vector3.Distance(target.Room.transform.position, pose.position) < .03f
                && Quaternion.Angle(target.Room.transform.rotation, pose.rotation) < .5f) return true;
            // Puede mover habitaciones aisladas y cerradas junto con sus aliens. La habitación del jugador y las conectadas por puertas abiertas o en cierre permanecen en su lugar.
            if (CanRelocate(target.Room) && FitsAt(target.Room, pose)) { movedRoom = target.Room; return true; }
            pose = TargetPose(target, source);
            if (CanRelocate(source.Room) && FitsAt(source.Room, pose)) { movedRoom = source.Room; return true; }
            return false;
        }
        movedRoom = target.Room;
        return FitsAt(target.Room, pose);
    }
    private bool CanRelocate(RoomModule room)
    {
        if (room == CurrentRoom || connections.Exists(c => c.SourceRoom == room || c.TargetRoom == room)) return false;
        foreach (DoorSocket socket in room.Sockets) if (socket.IsThresholdOccupied) return false;
        foreach (RoomOccupant occupant in occupants)
            if (occupant != null && occupant.Room == room && occupant.GetComponent<AlienMotor>() == null) return false;
        return true;
    }
    public bool TryParkIsolatedRoom(RoomModule room)
    {
        if (useRoomPortals) { ReturnUnused(room); return true; }
        // Tras el primer bucle narrativo, despeja la salida sin separar los aliens de su habitación.
        if (room == null || !CanRelocate(room) || !parking.TryGetValue(room, out Pose pose)) return false;
        // Su posición de edición puede coincidir con el hueco que necesitamos liberar.
        // Para este traslado narrativo usamos un espacio apartado, sin cambiar el parking registrado.
        pose.position += Vector3.right * 80f;
        if (!FitsAt(room, pose)) return false;
        RelocateIsolatedRoom(room, pose);
        ReturnUnused(room);
        return true;
    }
    private bool FitsAt(RoomModule movedRoom, Pose pose)
    {
        Vector3 size = movedRoom.LocalVolumeSize;
        Vector3 rotated = pose.rotation * new Vector3(size.x, 0, size.z);
        Bounds desired = new Bounds(pose.position + Vector3.up * size.y * .5f,
            new Vector3(Mathf.Abs(rotated.x) - .08f, size.y - .08f, Mathf.Abs(rotated.z) - .08f));
        foreach (RoomModule room in roomModules)
        {
            if (room == movedRoom || !room.gameObject.activeSelf) continue;
            Vector3 other = room.transform.rotation * new Vector3(room.LocalVolumeSize.x, 0, room.LocalVolumeSize.z);
            Bounds occupied = new Bounds(room.transform.position + Vector3.up * room.LocalVolumeSize.y * .5f,
                new Vector3(Mathf.Abs(other.x) - .08f, room.LocalVolumeSize.y - .08f, Mathf.Abs(other.z) - .08f));
            if (desired.Intersects(occupied)) return false;
        }
        return true;
    }
    // Guarda a los aliens en coordenadas locales y los vuelve a colocar con la habitación. También recoloca su NavMesh.
    private void RelocateIsolatedRoom(RoomModule room, Pose pose)
    {
        var placements = new Dictionary<AlienMotor, AlienMotor.RoomPlacement>();
        foreach (RoomOccupant occupant in occupants)
        {
            if (occupant == null || occupant.Room != room) continue;
            AlienMotor motor = occupant.GetComponent<AlienMotor>();
            if (motor != null) placements.Add(motor, motor.SuspendForPlacement(room.transform));
        }
        room.transform.SetPositionAndRotation(pose.position, pose.rotation);
        room.GetComponent<RoomNavigation>()?.RefreshPlacement();
        foreach (var pair in placements) pair.Key.RestorePlacement(room.transform, pair.Value);
        Physics.SyncTransforms();
        ReconcileThresholdSensors(room);
    }

    private static void ReconcileThresholdSensors(RoomModule room)
    {
        if (room == null || !room.gameObject.activeInHierarchy) return;
        foreach (DoorThresholdSensor sensor in room.GetComponentsInChildren<DoorThresholdSensor>(true))
            if (sensor != null && sensor.isActiveAndEnabled) sensor.ReconcileOccupants();
    }
    private void HandleDoorClosed(DoorInteractable door)
    {
        RoomConnection connection = connections.Find(c => c.SourceSocket.Door == door);
        if (connection == null) return;
        if (immediatePlayerCloses.Remove(connection))
            NudgePlayerInside(connection);
        else if (!ThresholdClear(connection))
        {
            ReopenForThresholdOccupant(connection);
            return;
        }
        if (useRoomPortals)
        {
            connection.RerollAt = Time.time + closedDoorGrace;
            if (connection.TargetSocket.Door.State == DoorState.Open)
                connection.TargetSocket.Door.BeginClose();
            return;
        }
        PruneLeases(connection.Pursuers);
        if ((connection.Pursuers.Count == 0 || YieldsToPlayer(connection))
            && ThresholdClear(connection)) FinalizeConnection(connection);
    }
    private void FinalizeConnection(RoomConnection connection)
    {
        // También protege si alguien entró mientras se animaba el cierre.
        // Una puerta cerrada puede esperar antes de liberar sus habitaciones y el enlace de navegación.
        PruneLeases(connection.PassageUsers);
        if (connection.PassageUsers.Count > 0 || !ThresholdClear(connection)) return;
        TrackOccupants();
        connections.Remove(connection);
        connection.PassageUsers.Clear();
        connection.Pursuers.Clear();
        closeWhenClear.Remove(connection);
        immediatePlayerCloses.Remove(connection);
        closeRequestedBy.Remove(connection);
        connection.SourceSocket.Disconnect(); connection.TargetSocket.Disconnect();
        if (connection.SourceGate != null) Destroy(connection.SourceGate);
        if (connection.TargetGate != null) Destroy(connection.TargetGate);
        if (!useRoomPortals) connection.TargetSocket.SetPortalAssemblyActive(true);
        connection.TargetSocket.Door.SetClosedInstant();
        ConnectionClosed?.Invoke(connection);
        ReturnUnused(connection.SourceRoom); ReturnUnused(connection.TargetRoom);
    }
    private void ReturnUnused(RoomModule room)
    {
        if (IsOccupied(room) || connections.Exists(c => c.SourceRoom == room || c.TargetRoom == room)) return;
        foreach (DoorSocket socket in room.Sockets) if (socket.IsThresholdOccupied) return;
        room.ResetDoorsInstant(); room.gameObject.SetActive(false);
        if (!useRoomPortals && parking.TryGetValue(room, out Pose pose)) room.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }
    private void Update()
    {
        TrackOccupants();
        requests.RemoveAll(r => !IsRequestValid(r));
        connectionSnapshot.Clear();
        connectionSnapshot.AddRange(connections);
        foreach (RoomConnection connection in connectionSnapshot)
        {
            PruneLeases(connection.PassageUsers);
            PruneLeases(connection.Pursuers);
            if ((connection.SourceSocket.Door.State == DoorState.Closing
                || connection.TargetSocket.Door.State == DoorState.Closing)
                && !immediatePlayerCloses.Contains(connection) && !ThresholdClear(connection))
                ReopenForThresholdOccupant(connection);
            if (immediatePlayerCloses.Contains(connection)
                && connection.SourceSocket.Door.State == DoorState.Closing)
                NudgePlayerInside(connection);
            if (connection.SourceSocket.Door.State == DoorState.Closed
                && connection.PassageUsers.Count == 0
                && (useRoomPortals ? Time.time >= connection.RerollAt
                    && (connection.Pursuers.Count == 0 || YieldsToPlayer(connection))
                    : connection.Pursuers.Count == 0 || YieldsToPlayer(connection)) && ThresholdClear(connection))
            { FinalizeConnection(connection); continue; }
            if (closeWhenClear.Contains(connection) || Time.time >= connection.AutoCloseAt)
            {
                closeRequestedBy.TryGetValue(connection, out GameObject closingActor);
                if (TryClose(connection, closingActor))
                {
                    closeWhenClear.Remove(connection);
                    closeRequestedBy.Remove(connection);
                }
            }
        }
        // Atiende por orden de llegada dentro de cada prioridad. Aumenta la prioridad de quienes llevan esperando para evitar esperas indefinidas.
        for (int priority = 0; priority < 2; priority++)
            for (int i = 0; i < requests.Count; i++)
            {
                DoorRequest request = requests[i];
                bool urgent = request.IsPlayer || request.Expires - Time.time < requestLifetime * .5f;
                if ((priority == 0) != urgent || Time.time < request.NextAttempt) continue;
                request.NextAttempt = Time.time + (request.IsPlayer ? .05f : .15f);
                // Un alien esperando otra puerta no debe deshacer el cierre que libera la salida del jugador.
                if (!request.IsPlayer && (PlayerNeedsRoom(request.Socket.Room)
                    || PlayerNeedsRoom(NormalDestination(request.Socket)?.Room))) continue;
                bool waiting = request.IsPlayer || Time.time + requestLifetime - request.Expires >= .65f;
                if (TryOpenNow(request.Socket, request.Actor, waiting, request.DirectedRoom))
                { requests.RemoveAt(i--); continue; }
                // Cierra en paralelo los pasos que bloquean la solicitud, incluso en el destino contiguo.
                // La memoria de persecución no retiene un paso ya cerrado si hace falta para la huida.
                if (useRoomPortals) continue;
                foreach (RoomConnection idle in connectionSnapshot)
                {
                    if (!connections.Contains(idle)) continue;
                    if (request.IsPlayer && YieldsToPlayer(idle))
                    {
                        CloseAfterPassage(idle, request.Actor);
                        TryClose(idle, request.Actor);
                    }
                    else if (idle.Pursuers.Count == 0
                        && idle.SourceRoom != CurrentRoom && idle.TargetRoom != CurrentRoom)
                        CloseAfterPassage(idle);
                }
                if (request.IsPlayer || request.Expires - Time.time < requestLifetime - 1f)
                    RecoverIsolatedSpace(request.Socket.Room);
            }
    }
    private void RecoverIsolatedSpace(RoomModule source)
    {
        foreach (RoomModule room in roomModules)
        {
            if (room == source || !room.gameObject.activeSelf || !CanRelocate(room)
                || !parking.TryGetValue(room, out Pose pose)
                || Vector3.Distance(room.transform.position, pose.position) < .1f
                || !FitsAt(room, pose)) continue;
            RelocateIsolatedRoom(room, pose);
            ReturnUnused(room);
            return;
        }
    }
    private void TrackOccupants()
    {
        occupants.RemoveAll(o => o == null);
        foreach (RoomOccupant occupant in occupants)
        {
            if (useRoomPortals)
            {
                if (player != null && occupant.transform == player) SetCurrentRoom(occupant.Room);
                continue;
            }
            foreach (RoomConnection connection in connections)
            {
                RoomModule other = occupant.Room == connection.SourceRoom ? connection.TargetRoom
                    : occupant.Room == connection.TargetRoom ? connection.SourceRoom : null;
                if (other != null && other.ContainsPoint(occupant.transform.position, .12f))
                {
                    occupant.Enter(other);
                    if (occupant.UsesNormalDoors)
                    {
                        connection.Pursuers.Remove(occupant.gameObject);
                        CloseAfterPassage(connection, occupant.gameObject);
                    }
                    break;
                }
            }
            if (player != null && occupant.transform == player) SetCurrentRoom(occupant.Room);
        }
    }
    private void SetCurrentRoom(RoomModule room)
    {
        if (room == null || room == CurrentRoom) return;
        RoomModule old = CurrentRoom;
        if (old != null) old.SetCurrent(false);
        CurrentRoom = room; room.SetCurrent(true);
        // Después del inicio, las habitaciones ocupadas conservan su posición y sus zonas de navegación.
        CurrentRoomChanged?.Invoke(old, room);
    }
    private void OnDestroy()
    {
        foreach (DoorSocket socket in socketById.Values) if (socket != null && socket.Door != null) socket.Door.Closed -= HandleDoorClosed;
        if (Instance == this) Instance = null;
    }
}
