using UnityEngine;

/// <summary>Traslada al personaje o un cuerpo físico al cruzar un marco abierto.</summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class HousePortalTraveller : MonoBehaviour
{
    private CharacterController character;
    private Rigidbody body;
    private RoomOccupant occupant;
    private Vector3 previousPosition;
    private float nextCrossingTime;

    private void Awake()
    {
        character = GetComponent<CharacterController>();
        body = GetComponent<Rigidbody>();
        occupant = GetComponent<RoomOccupant>();
        previousPosition = transform.position;
    }

    private void OnEnable() => previousPosition = transform.position;
    private void LateUpdate() { if (body == null || body.isKinematic) CheckCrossing(); }
    private void FixedUpdate() { if (body != null && !body.isKinematic) CheckCrossing(); }

    private void CheckCrossing()
    {
        HouseFlowController house = HouseFlowController.Instance;
        Vector3 current = transform.position;
        if (house == null || !house.UsesRoomPortals || Time.time < nextCrossingTime)
        { previousPosition = current; return; }

        foreach (RoomConnection connection in house.Connections)
        {
            if (connection.SourceSocket.Door.State == DoorState.Closed) continue;
            PortalTrialGate gate = null;
            RoomModule destination = null;
            if ((occupant == null || occupant.Room == connection.SourceRoom)
                && connection.SourceGate != null
                && connection.SourceGate.WasCrossed(previousPosition, current))
            { gate = connection.SourceGate; destination = connection.TargetRoom; }
            else if ((occupant == null || occupant.Room == connection.TargetRoom)
                && connection.TargetGate != null
                && connection.TargetGate.WasCrossed(previousPosition, current))
            { gate = connection.TargetGate; destination = connection.SourceRoom; }
            if (gate == null || destination == null) continue;

            Quaternion oldRotation = transform.rotation;
            Quaternion newRotation = gate.MapRotation(oldRotation);
            // El centro del CharacterController debe quedar sobre el suelo interior,
            // no justo en el borde exterior del piso al correr a través del marco.
            float interiorDepth = character != null ? Mathf.Max(.25f, character.radius + .08f) : .15f;
            Vector3 newPosition = gate.ExitPoint(current, interiorDepth);
            bool controllerEnabled = character != null && character.enabled;
            if (controllerEnabled) character.enabled = false;
            if (body != null && !body.isKinematic)
            {
                Quaternion turn = newRotation * Quaternion.Inverse(oldRotation);
                body.position = newPosition;
                body.rotation = newRotation;
                body.linearVelocity = turn * body.linearVelocity;
                body.angularVelocity = turn * body.angularVelocity;
            }
            transform.SetPositionAndRotation(newPosition, newRotation);
            if (controllerEnabled) character.enabled = true;
            if (occupant != null) house.NotifyPortalCrossing(occupant, connection, destination);
            if (CompareTag("Player") && Camera.main != null)
                Camera.main.GetComponent<OverShoulderCamera>()?.SnapThroughPortal(gate);
            Physics.SyncTransforms();
            previousPosition = newPosition;
            nextCrossingTime = Time.time + .25f;
            return;
        }
        previousPosition = current;
    }
}
