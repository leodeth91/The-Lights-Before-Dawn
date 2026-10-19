#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class AlienCoordinationChecks
{
    internal static IEnumerator RunDoorPriorityChecks(HouseFlowController house,
        AlienController[] aliens, GameObject player, Action<bool, string> check)
    {
        if (aliens.Length < 2) { check(false, "Prioridad: faltan aliens para la prueba"); yield break; }
        RoomModule foyer = house.ModuleById["foyer"];
        RoomModule child = house.ModuleById["child_room"];
        RoomModule hall = house.ModuleById["stair_hall"];
        DoorSocket exit = foyer.Sockets.Single(s => s.SocketId == "foyer_east");
        DoorSocket previous = foyer.Sockets.Single(s => s.SocketId == "foyer_north");
        var configuration = new SerializedObject(house);
        configuration.FindProperty("randomizeEveryEligibleDoor").boolValue = false;
        configuration.FindProperty("anomalyChance").floatValue = 0f;
        configuration.ApplyModifiedPropertiesWithoutUndo();

        // Esta preparación solo ocurre en Play: no guarda posiciones en la escena ni en prefabs.
        foreach (AlienController alien in aliens)
        {
            alien.StopAllCoroutines(); alien.Motor.Stop();
            house.CancelDoorRequest(alien.gameObject); house.ForgetPursuit(alien.gameObject);
            PlaceDoorTestActor(house, alien.gameObject, child, child.transform.position, true);
        }
        house.CancelDoorRequest(player);
        PlaceDoorTestActor(house, player, foyer, exit.ClearApproachPoint, false);
        foreach (RoomConnection old in house.Connections.ToArray())
        { old.PassageUsers.Clear(); old.Pursuers.Clear(); old.SourceSocket.Door.SetClosedInstant(); }
        yield return new WaitForSeconds(.6f);
        foreach (RoomModule room in house.ModuleById.Values)
            if (!house.IsOccupied(room) && !house.Connections.Any(c => c.SourceRoom == room || c.TargetRoom == room))
                room.gameObject.SetActive(false);

        house.RequestOpen(previous, player);
        yield return new WaitForSeconds(.5f);
        RoomConnection oldPassage = house.ConnectionFor(previous);
        check(oldPassage != null, "Prioridad: no pudo preparar el paso anterior");
        if (oldPassage == null) yield break;
        foreach (AlienController alien in aliens.Take(2))
            PlaceDoorTestActor(house, alien.gameObject, oldPassage.TargetRoom,
                oldPassage.TargetSocket.ClearApproachPoint, true);
        house.RememberPursuit(aliens[0].gameObject, foyer);
        check(house.ReservePassage(oldPassage, aliens[0].gameObject), "Prioridad: no pudo reservar el cruce inicial");
        house.RequestOpen(exit, player);
        yield return new WaitForSeconds(.15f);
        check(oldPassage.SourceSocket.Door.IsOpen && house.ConnectionFor(exit) == null,
            "Prioridad: cerró un paso que todavía tenía un cruce reservado");
        check(!house.ReservePassage(oldPassage, aliens[1].gameObject),
            "Prioridad: un cruce nuevo pudo prolongar la espera del jugador");
        house.ReleasePassage(oldPassage, aliens[0].gameObject);
        float started = Time.time;
        while (Time.time - started < 1.5f && house.ConnectionFor(exit)?.SourceSocket.Door.IsOpen != true)
        {
            // Reproduce al perseguidor que insiste en reabrir mientras se libera la otra salida.
            if (house.ConnectionFor(previous) == oldPassage)
                house.RequestOpen(oldPassage.TargetSocket, aliens[0].gameObject);
            yield return null;
        }
        check(house.ConnectionFor(exit)?.SourceSocket.Door.IsOpen == true,
            "Prioridad: la salida tardó más de 1,5 s tras liberar el cruce");
        Debug.Log("Puertas: cambio de salida tras cruce = " + (Time.time - started).ToString("F3") + " s");
        foreach (AlienController alien in aliens) house.CancelDoorRequest(alien.gameObject);

        foreach (RoomConnection old in house.Connections.ToArray())
        { old.PassageUsers.Clear(); old.Pursuers.Clear(); old.SourceSocket.Door.SetClosedInstant(); }
        foreach (AlienController alien in aliens)
            PlaceDoorTestActor(house, alien.gameObject, child, child.transform.position, true);
        yield return new WaitForSeconds(.6f);
        foreach (RoomModule room in house.ModuleById.Values)
            if (!house.IsOccupied(room) && !house.Connections.Any(c => c.SourceRoom == room || c.TargetRoom == room))
                room.gameObject.SetActive(false);

        // El destino está apartado y conserva una conexión cerrada por memoria de persecución.
        hall.transform.position += Vector3.right * 120f;
        DoorSocket busyDoor = hall.Sockets.Single(s => s.SocketId == "hall_lower_north");
        PlaceDoorTestActor(house, aliens[0].gameObject, hall, busyDoor.ClearApproachPoint, true);
        house.RequestOpen(busyDoor, aliens[0].gameObject);
        yield return new WaitForSeconds(.5f);
        RoomConnection retained = house.ConnectionFor(busyDoor);
        check(retained != null, "Prioridad: no pudo preparar el destino ocupado");
        if (retained == null) yield break;
        house.RememberPursuit(aliens[0].gameObject, retained.TargetRoom);
        house.RequestClose(busyDoor, aliens[0].gameObject);
        yield return new WaitForSeconds(.5f);
        check(house.ConnectionFor(busyDoor) == retained && busyDoor.Door.State == DoorState.Closed,
            "Prioridad: la prueba requiere una conexión cerrada retenida por persecución");
        started = Time.time;
        house.RequestOpen(exit, player);
        while (Time.time - started < 1.5f && house.ConnectionFor(exit)?.SourceSocket.Door.IsOpen != true)
            yield return null;
        check(house.ConnectionFor(exit)?.SourceSocket.Door.IsOpen == true,
            "Prioridad: esperó la reserva de persecución del destino en vez de liberar su conexión cerrada");
        check(aliens[0].Motor.Agent.isOnNavMesh, "Prioridad: recolocar el destino rompió el NavMesh del alien");
        Debug.Log("Puertas: recuperar destino retenido = " + (Time.time - started).ToString("F3") + " s");
    }

    private static void PlaceDoorTestActor(HouseFlowController house, GameObject actor,
        RoomModule room, Vector3 position, bool normalDoors)
    {
        house.RegisterOccupant(actor, room, normalDoors);
        CharacterController character = actor.GetComponent<CharacterController>();
        if (character != null) character.enabled = false;
        AlienMotor motor = actor.GetComponent<AlienMotor>();
        if (motor != null) motor.Agent.Warp(position + Vector3.up * .04f);
        else actor.transform.position = position + Vector3.up * .04f;
        if (character != null) character.enabled = true;
        Physics.SyncTransforms();
    }

    public static IEnumerator Run(Action<bool, string> check)
    {
        HouseFlowController house = HouseFlowController.Instance;
        AlienController[] aliens = UnityEngine.Object.FindObjectsByType<AlienController>(FindObjectsSortMode.None);
        if (aliens.Length != 2) { check(false, "Coordination requires two aliens"); yield break; }
        foreach (AlienController alien in aliens)
        {
            alien.SetHearingEnabled(false);
            alien.StopAllCoroutines(); alien.FinishInspection(); alien.Motor.Stop();
            house.ReleasePassage(alien.Passage, alien.gameObject); alien.Passage = null;
        }
        RoomModule foyer = house.ModuleById["foyer"];
        for (int i = 0; i < aliens.Length; i++)
        {
            house.RegisterOccupant(aliens[i].gameObject, foyer, true);
            Vector3 point = foyer.transform.TransformPoint(new Vector3(i == 0 ? -.75f : .75f, .05f, 0));
            if (aliens[i].Motor.TryPoint(point, 1, out Vector3 spawn)) aliens[i].Motor.Agent.Warp(spawn);
        }
        yield return new WaitForFixedUpdate();
        foreach (RoomConnection connection in house.Connections.ToArray()) house.CloseAfterPassage(connection);
        yield return new WaitForSeconds(1);
        // Solo esta prueba mueve habitaciones vacías para que los resultados no dependan de partidas anteriores.
        foreach (RoomModule room in house.ModuleById.Values)
            if (!house.IsOccupied(room) && !house.Connections.Any(c => c.SourceRoom == room || c.TargetRoom == room)) room.gameObject.SetActive(false);

        var configuration = new SerializedObject(house);
        configuration.FindProperty("randomizeEveryEligibleDoor").boolValue = false;
        configuration.FindProperty("anomalyChance").floatValue = 0;
        configuration.FindProperty("automaticCloseDelay").vector2Value = new Vector2(1000, 1000);
        configuration.ApplyModifiedPropertiesWithoutUndo();
        GameObject player = GameObject.FindWithTag("Player");
        DoorSocket playerDoor = house.CurrentRoom.Sockets.First();
        DoorSocket alienDoor = foyer.Sockets.First(s => s.SocketId == "foyer_north");
        check(house.RequestOpen(playerDoor, player), "Player could not open normal door concurrently");
        check(house.RequestOpen(alienDoor, aliens[0].gameObject), "Alien could not open normal door concurrently");
        RoomConnection playerConnection = house.ConnectionFor(playerDoor);
        RoomConnection alienConnection = house.ConnectionFor(alienDoor);
        check(playerConnection != null && alienConnection != null && house.Connections.Count == 2, "Connections are not independent");
        if (alienConnection == null) yield break;
        Collider leaf = alienConnection.SourceSocket.Door.GetComponentInChildren<Collider>();
        check(leaf != null && Physics.GetIgnoreCollision(leaf, player.GetComponent<CharacterController>()),
            "A door opened by an alien can push the player during its animation");
        yield return new WaitForSeconds(.5f);
        house.ReservePassage(alienConnection, aliens[0].gameObject);
        house.ReservePassage(alienConnection, aliens[1].gameObject);
        check(house.RequestClose(alienDoor) && alienConnection.SourceSocket.Door.State == DoorState.Open,
            "A reserved passage did not defer its accepted close request");
        house.ReleasePassage(alienConnection, aliens[0].gameObject);
        check(house.RequestClose(alienDoor) && alienConnection.SourceSocket.Door.State == DoorState.Open,
            "Releasing one user discarded the other user's reservation");
        house.ReleasePassage(alienConnection, aliens[1].gameObject);
        check(house.RequestClose(alienDoor), "Unreserved passage would not close");
        yield return new WaitForSeconds(.5f);
        check(foyer.gameObject.activeSelf && !alienConnection.TargetRoom.gameObject.activeSelf, "Pool ignored room occupancy");
        check(house.ConnectionFor(playerDoor) == playerConnection, "Alien close removed player's connection");

        check(house.RequestOpen(alienDoor, aliens[0].gameObject), "Could not reopen recycled destination");
        yield return new WaitForSeconds(.5f);
        alienConnection = house.ConnectionFor(alienDoor);
        if (alienConnection == null) yield break;
        house.ReservePassage(alienConnection, aliens[1].gameObject);
        DoorSocket target = alienConnection.TargetSocket;
        yield return aliens[1].Motor.Go(target.Position - target.Outward * 1.3f, 15);
        check(aliens[1].Motor.Arrived, "Alien failed physical doorway traversal");
        if (aliens[1].Motor.Arrived) aliens[1].Occupant.Enter(target.Room);
        house.ReleasePassage(alienConnection, aliens[1].gameObject);
        house.CloseAfterPassage(alienConnection);
        yield return new WaitForSeconds(.6f);
        check(foyer.gameObject.activeSelf && target.Room.gameObject.activeSelf, "Closing separated actors pooled an occupied room");
        Vector3 otherAlienPosition = aliens[1].transform.position;
        check(house.RequestOpen(alienDoor, aliens[0].gameObject), "Unable to reconnect to an aligned occupied room");
        check(Vector3.Distance(otherAlienPosition, aliens[1].transform.position) < .001f, "Reconnecting displaced the other alien");

        RoomModule hall = house.ModuleById["stair_hall"];
        house.RegisterOccupant(aliens[0].gameObject, hall, true);
        StairInterest bottom = hall.GetComponentsInChildren<StairInterest>().First(s => (s.name.EndsWith("Bottom") || s.name.EndsWith("Inferior")));
        check(aliens[0].Motor.TryPoint(bottom.ApproachPoint, .5f, out Vector3 foot), "No staircase spawn");
        aliens[0].Motor.Agent.Warp(foot);
        yield return aliens[0].Motor.Go(bottom.Destination, 25);
        check(aliens[0].Motor.Arrived && aliens[0].transform.position.y > hall.transform.position.y + 2.8f, "Alien could not walk upstairs");
        yield return aliens[0].Motor.Go(bottom.ApproachPoint, 25);
        check(aliens[0].Motor.Arrived && aliens[0].transform.position.y < hall.transform.position.y + .3f, "Alien could not walk downstairs");

        // Reproduce el encuentro entre el jugador y un alien que estaba en una habitación aislada del pool.
        house.CloseAfterPassage(playerConnection);
        yield return new WaitForSeconds(.6f);
        RoomModule corridor = house.ModuleById["upper_corridor"];
        check(!corridor.gameObject.activeSelf, "Encounter fixture corridor was not returned to pool");
        corridor.transform.position += Vector3.right * 80;
        house.RegisterOccupant(aliens[0].gameObject, corridor, true);
        if (aliens[0].Motor.TryPoint(corridor.transform.position + Vector3.up * .03f, .5f, out Vector3 corridorPoint))
            aliens[0].Motor.Agent.Warp(corridorPoint);
        Vector3 localAlien = corridor.transform.InverseTransformPoint(aliens[0].transform.position);
        Vector3 stationaryPlayer = player.transform.position;
        yield return new WaitForFixedUpdate();
        check(aliens[0].Motor.SetDestination(corridor.transform.TransformPoint(new Vector3(1.2f, .03f, 0))),
            "Could not give the remote alien a navigation destination");
        Vector3 intendedLocalDestination = corridor.transform.InverseTransformPoint(aliens[0].Motor.Agent.destination);
        localAlien = corridor.transform.InverseTransformPoint(aliens[0].transform.position);
        check(house.RequestOpen(playerDoor, player), "Player could not connect to an alien room in another pool sector");
        RoomConnection encounter = house.ConnectionFor(playerDoor);
        check(encounter != null && encounter.TargetRoom == corridor, "Encounter used a different room instance");
        check(Vector3.Distance(stationaryPlayer, player.transform.position) < .001f, "Encounter moved the player");
        check(Vector3.Distance(localAlien, corridor.transform.InverseTransformPoint(aliens[0].transform.position)) < .02f,
            "Room placement failed to carry its alien at the same local position");
        check(aliens[0].Motor.Agent.isOnNavMesh, "Encounter invalidated alien navigation");
        check(aliens[0].Motor.Agent.hasPath && Vector3.Distance(intendedLocalDestination,
            corridor.transform.InverseTransformPoint(aliens[0].Motor.Agent.destination)) < .08f,
            "Encounter did not restore the alien's intended destination");
        yield return RunDoorRecoveryChecks(house, aliens, player, check);
    }

    internal static IEnumerator RunDoorRecoveryChecks(HouseFlowController house,
        AlienController[] aliens, GameObject player, Action<bool, string> check)
    {
        foreach (AlienController alien in aliens)
        {
            alien.StopAllCoroutines();
            alien.Motor.Stop();
            house.CancelDoorRequest(alien.gameObject);
            house.ForgetPursuit(alien.gameObject);
        }
        // Prepara esta prueba sin depender de las rutas aleatorias anteriores.
        foreach (RoomConnection old in house.Connections.ToArray())
        {
            old.PassageUsers.Clear();
            old.Pursuers.Clear();
            old.SourceSocket.Door.SetClosedInstant();
        }
        RoomModule child = house.ModuleById["child_room"];
        RoomModule foyer = house.ModuleById["foyer"];
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = child.transform.position + Vector3.up * .05f;
        cc.enabled = true;
        house.RegisterOccupant(player, child, false);
        foreach (AlienController alien in aliens)
        {
            house.RegisterOccupant(alien.gameObject, foyer, true);
            alien.Motor.Agent.Warp(foyer.transform.position + Vector3.up * .05f);
        }
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.5f);
        foreach (RoomModule room in house.ModuleById.Values)
            if (!house.IsOccupied(room) && !house.Connections.Any(c => c.SourceRoom == room || c.TargetRoom == room))
                room.gameObject.SetActive(false);
        var config = new SerializedObject(house);
        config.FindProperty("randomizeEveryEligibleDoor").boolValue = true;
        config.FindProperty("anomalyChance").floatValue = 1f;
        config.ApplyModifiedPropertiesWithoutUndo();
        DoorSocket door = child.Sockets.First();
        check(house.RequestOpen(door, player), "Recovery: player opening rejected");
        yield return new WaitForSeconds(.6f);
        RoomConnection connection = house.ConnectionFor(door);
        check(connection != null && connection.IsAnomalous, "Recovery: anomalous fixture missing");
        if (connection == null) yield break;
        AlienController hunter = aliens[0];
        DoorSocket farDoor = connection.TargetSocket;
        house.RegisterOccupant(hunter.gameObject, farDoor.Room, true);
        check(hunter.Motor.TryPoint(farDoor.ClearApproachPoint, 1f, out Vector3 spawn),
            "Recovery: no clear approach point");
        hunter.Motor.Agent.Warp(spawn);
        hunter.Motor.Stop();
        house.RememberPursuit(hunter.gameObject, child);
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        check(house.RequestClose(door, player), "Recovery: player cannot shut door before hunter crosses");
        yield return new WaitForSeconds(.5f);
        check(house.ConnectionFor(door) == connection && door.Door.State == DoorState.Closed,
            "Recovery: pursuit did not retain a physically closed anomalous connection");
        AlienPopulation population = UnityEngine.Object.FindFirstObjectByType<AlienPopulation>();
        check(population != null && population.Links.TryGetValue(connection, out var bridge)
            && !bridge.enabled, "Recovery: closed retained door still has a live navigation bridge");
        // La puerta debe poder volver a abrirse desde ambos lados mientras conserva la conexión.
        check(house.RequestOpen(door, player), "Recovery: player cannot reopen retained door");
        yield return new WaitForSeconds(.5f);
        check(door.Door.IsOpen, "Recovery: accepted player reopen never opened");
        check(house.RequestClose(door, player), "Recovery: second close rejected");
        yield return new WaitForSeconds(.5f);
        yield return hunter.CrossDoor(farDoor);
        check(hunter.Occupant.Room == child, "Recovery: hunter failed to reopen and cross anomalous door");
        // Aleja al jugador del recorrido de la puerta para que pueda terminar de cerrarse.
        cc.enabled = false;
        player.transform.position = child.transform.position + Vector3.up * .05f;
        cc.enabled = true;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.7f);
        check(door.Door.State == DoorState.Closed, "Recovery: hunter did not close the door behind itself");
        check(house.PendingRequestCount == 0, "Recovery: finished crossing left pending requests");

        check(house.RequestOpen(door, player), "Recovery: cannot reopen after hunter crossed");
        yield return new WaitForSeconds(.5f);
        connection = house.ConnectionFor(door);
        if (connection == null) { check(false, "Recovery: lease fixture missing"); yield break; }
        house.ReservePassage(connection, hunter.gameObject);
        connection.PassageUsers[hunter.gameObject] = Time.time + .2f;
        check(house.RequestClose(door, player) && door.Door.State == DoorState.Open,
            "Recovery: active reservation did not defer closing");
        yield return new WaitForSeconds(.3f);
        check(house.RequestClose(door, player), "Recovery: expired reservation still blocks door");
        // Reabrir invierte el cierre de inmediato; repetir la orden no crea solicitudes pendientes.
        for (int i = 0; i < 20; i++) house.RequestOpen(door, player);
        check(house.PendingRequestCount == 0 && door.Door.State == DoorState.Opening,
            "Recovery: repeated inputs duplicated requests or reopening was delayed");
        yield return new WaitForSeconds(1f);
        connection = house.ConnectionFor(door);
        check(connection != null && door.Door.IsOpen && house.PendingRequestCount == 0,
            "Recovery: queued reopen during closing was lost");
        if (connection != null)
        {
            connection.AutoCloseAt = Time.time;
            yield return new WaitForSeconds(.7f);
            check(door.Door.State == DoorState.Closed, "Recovery: automatic closing did not complete");
        }
    }
}
#endif
