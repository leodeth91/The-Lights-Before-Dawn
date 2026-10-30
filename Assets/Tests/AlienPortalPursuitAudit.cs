#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Comprueba la memoria y el cruce de una puerta anómala en Play sin guardar la escena.</summary>
public sealed class AlienPortalPursuitAudit : MonoBehaviour
{
    private const string SessionKey = "TLBD.PortalPursuitAudit";

    [MenuItem("The Lights Before Dawn/Pruebas/Auditar persecución por portal")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || SceneManager.GetActiveScene().path != "Assets/Scenes/ModularPrototype.unity") return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, false);
        new GameObject("ALIEN_PORTAL_PURSUIT_AUDIT").AddComponent<AlienPortalPursuitAudit>();
    }

    private IEnumerator Start()
    {
        yield return null;
        int failures = 0;
        HouseFlowController house = HouseFlowController.Instance;
        AlienController alien = FindObjectsByType<AlienController>(FindObjectsSortMode.None).FirstOrDefault();
        PlayerVisibility player = FindFirstObjectByType<PlayerVisibility>();
        StoryProgression story = StoryProgression.Instance;
        if (house == null || alien == null || player == null || story == null)
        { Debug.LogError("PURSUIT_AUDIT: faltan componentes iniciales"); EditorApplication.ExitPlaymode(); yield break; }

        GameSessionManager.Instance.BeginGame();
        MethodInfo advance = typeof(StoryProgression).GetMethod("Advance",
            BindingFlags.Instance | BindingFlags.NonPublic);
        advance.Invoke(story, new object[] { StoryObjective.FindPhone, new string[0] });
        yield return new WaitForSeconds(3.1f);

        RoomModule origin = house.ModuleById["child_room"];
        DoorSocket entrance = origin.Sockets.First();
        alien.SetPlayerDetectionEnabled(false);
        house.RegisterOccupant(alien.gameObject, origin, true);
        if (!alien.Motor.TryPoint(entrance.ClearApproachPoint, .8f, out Vector3 alienGround)
            || !alien.Motor.Agent.Warp(alienGround))
        { Debug.LogError("PURSUIT_AUDIT: alien sin suelo en puerta inicial"); EditorApplication.ExitPlaymode(); yield break; }

        CharacterController body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = entrance.Position - entrance.Outward * .8f;
        body.enabled = true;
        Physics.SyncTransforms();
        alien.Memory.Observe(player, origin);
        if (!house.RequestOpen(entrance, player.gameObject)) failures++;
        float deadline = Time.time + 3f;
        RoomConnection connection = null;
        while (Time.time < deadline)
        {
            connection = house.ConnectionFor(entrance);
            if (connection != null && connection.SourceSocket.Door.IsOpen) break;
            yield return null;
        }
        if (connection == null || !connection.IsAnomalous)
        { Debug.LogError("PURSUIT_AUDIT: no se abrió una puerta anómala"); EditorApplication.ExitPlaymode(); yield break; }

        DoorSocket exit = connection.TargetSocket;
        house.NotifyPortalCrossing(player.GetComponent<RoomOccupant>(), connection, exit.Room);
        body.enabled = false;
        player.transform.position = exit.ClearApproachPoint;
        body.enabled = true;
        Physics.SyncTransforms();
        if (alien.Memory.LastKnownRoom != exit.Room || !house.CanFollow(connection, alien.gameObject)) failures++;
        if (!house.RequestClose(entrance, player.gameObject)) failures++;
        deadline = Time.time + 3f;
        while (connection.SourceSocket.Door.State != DoorState.Closed && Time.time < deadline)
            yield return null;
        yield return new WaitForSeconds(.5f);
        if (house.ConnectionFor(entrance) != connection) failures++;

        alien.StopAllCoroutines();
        yield return alien.CrossDoor(entrance);
        if (alien.Occupant.Room != exit.Room || !alien.Motor.Agent.isOnNavMesh) failures++;
        Debug.Log("PURSUIT_AUDIT: fallos=" + failures + ", memoria=" + alien.Memory.LastKnownRoom?.RoomId
            + ", cuarto_alien=" + alien.Occupant.Room?.RoomId
            + ", navmesh=" + alien.Motor.Agent.isOnNavMesh);
        EditorApplication.ExitPlaymode();
    }
}
#endif
