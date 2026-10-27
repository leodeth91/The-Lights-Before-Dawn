using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Crea los aliens desde su prefab y conserva las habitaciones y su distribución manual.</summary>
public sealed class AlienPopulation : MonoBehaviour
{
    [Header("Template")]
    [Tooltip("Prefab que se usa para crear cada alien. Editalo para cambiar su apariencia o sus componentes.")]
    [SerializeField] private AlienController alienPrefab;

    private HouseFlowController house;
    private StoryProgression story;
    private bool thirdAlienSpawned;
    private readonly Dictionary<RoomConnection, NavMeshLink> links = new Dictionary<RoomConnection, NavMeshLink>();
    public IReadOnlyDictionary<RoomConnection, NavMeshLink> Links => links;

    private void Start()
    {
        house = HouseFlowController.Instance;
        if (house == null) return;
        if (alienPrefab == null)
        {
            Debug.LogError("AlienPopulation requires an Alien prefab reference.", this);
            enabled = false;
            return;
        }
        RoomNavigation.CreateAgentSettings();
        foreach (RoomModule room in house.ModuleById.Values)
        {
            foreach (HideSpot spot in room.GetComponentsInChildren<HideSpot>(true))
                if (spot.GetComponent<HideSpotInterest>() == null) spot.gameObject.AddComponent<HideSpotInterest>();
            foreach (DoorSocket socket in room.Sockets)
                if (socket.GetComponent<DoorInterest>() == null) socket.gameObject.AddComponent<DoorInterest>();
            (room.GetComponent<RoomNavigation>() ?? room.gameObject.AddComponent<RoomNavigation>()).Prepare();
        }
        house.ConnectionClosed += RemoveLink;
        if (!house.ModuleById.TryGetValue("foyer", out RoomModule foyer)) return;
        foyer.gameObject.SetActive(true);
        Spawn(foyer, new Vector3(-.75f, .05f, 0), "Alien 1");
        Spawn(foyer, new Vector3(.75f, .05f, 0), "Alien 2");
        story = StoryProgression.Instance;
        if (story != null)
        {
            story.ObjectiveChanged += OnObjectiveChanged;
            if (story.Objective == StoryObjective.Survive) OnObjectiveChanged(story.Objective);
        }
    }
    private void OnObjectiveChanged(StoryObjective objective)
    {
        if (objective != StoryObjective.Survive || thirdAlienSpawned || house == null
            || !house.ModuleById.TryGetValue("foyer", out RoomModule foyer)) return;
        foyer.gameObject.SetActive(true);
        // Evita colocarlo sobre un alien que haya regresado al recibidor.
        Vector3[] positions = { new Vector3(0, .05f, 0), new Vector3(0, .05f, -1f),
            new Vector3(0, .05f, 1f), new Vector3(-1f, .05f, 0), new Vector3(1f, .05f, 0) };
        foreach (Vector3 position in positions)
            if (Spawn(foyer, position, "Alien 3", true))
            {
                thirdAlienSpawned = true;
                return;
            }
        Debug.LogError("No clear walkable spawn for Alien 3 in the foyer.", foyer);
    }
    private bool Spawn(RoomModule room, Vector3 local, string label, bool avoidOccupants = false)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = RoomNavigation.AgentType, areaMask = NavMesh.AllAreas };
        Vector3 requestedPosition = room.transform.TransformPoint(local);
        if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, 2f, filter))
        {
            if (!avoidOccupants) Debug.LogError("No walkable spawn for " + label, room);
            return false;
        }
        if (avoidOccupants)
            foreach (RoomOccupant otherOccupant in house.Occupants)
                if (otherOccupant != null && otherOccupant.Room == room && otherOccupant.gameObject.activeInHierarchy
                    && (otherOccupant.transform.position - hit.position).sqrMagnitude < .8f * .8f)
                    return false;

        Quaternion spawnRotation = room.transform.rotation * alienPrefab.transform.localRotation;
        GameObject root = Instantiate(alienPrefab.gameObject, hit.position, spawnRotation, transform);
        root.name = label;
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        CapsuleCollider collider = root.GetComponent<CapsuleCollider>();
        Rigidbody body = root.GetComponent<Rigidbody>();
        RoomOccupant occupant = root.GetComponent<RoomOccupant>();
        if (agent == null || collider == null || body == null || occupant == null)
        {
            Debug.LogError("The Alien prefab is missing NavMeshAgent, CapsuleCollider, Rigidbody or RoomOccupant.", alienPrefab);
            Destroy(root);
            return false;
        }
        // El agente del prefab permanece desactivado al instanciarse: su tipo de NavMesh
        // se crea para esta sesión y solo puede activarse después de asignarlo aquí.
        agent.enabled = false; agent.agentTypeID = RoomNavigation.AgentType;
        agent.radius = .22f; agent.height = 1.8f; agent.speed = Random.Range(.7f, .9f);
        agent.acceleration = 2; agent.angularSpeed = 110; agent.stoppingDistance = .12f;
        agent.avoidancePriority = label == "Alien 1" ? 40 : label == "Alien 2" ? 55 : 70;
        collider.radius = .22f;
        collider.height = 1.8f; collider.center = Vector3.up * .9f;
        body.isKinematic = true; body.useGravity = false;
        house.RegisterOccupant(root, room, true);
        agent.enabled = true;
        return true;
    }
    private void Update()
    {
        if (house == null) return;
        // Los módulos remotos no deben unirse con un NavMeshLink físico: la IA cruza
        // por el marco y se recoloca en el NavMesh de destino en AlienController.
        if (house.UsesRoomPortals) return;
        foreach (RoomConnection connection in house.Connections)
        {
            if (!connection.SourceSocket.Door.IsOpen)
            {
                if (links.TryGetValue(connection, out NavMeshLink closedLink))
                    closedLink.enabled = false;
                continue;
            }
            if (links.TryGetValue(connection, out NavMeshLink openLink))
            { openLink.enabled = true; continue; }
            if (links.ContainsKey(connection) || !connection.SourceSocket.Door.IsOpen) continue;
            GameObject bridge = new GameObject("EnlacePuerta_" + connection.SourceSocket.SocketId);
            bridge.transform.SetParent(transform, false);
            NavMeshLink link = bridge.AddComponent<NavMeshLink>(); link.agentTypeID = RoomNavigation.AgentType;
            link.startPoint = connection.SourceSocket.Position - connection.SourceSocket.Outward * .65f;
            link.endPoint = connection.TargetSocket.Position - connection.TargetSocket.Outward * .65f;
            link.width = .45f; link.bidirectional = true; link.UpdateLink();
            links.Add(connection, link);
        }
    }
    private void RemoveLink(RoomConnection connection)
    {
        if (!links.TryGetValue(connection, out NavMeshLink link)) return;
        if (link != null) Destroy(link.gameObject);
        links.Remove(connection);
    }
    private void OnDestroy()
    {
        if (story != null) story.ObjectiveChanged -= OnObjectiveChanged;
        if (house != null) house.ConnectionClosed -= RemoveLink;
        if (RoomNavigation.AgentType != 0) NavMesh.RemoveSettings(RoomNavigation.AgentType);
    }
}
