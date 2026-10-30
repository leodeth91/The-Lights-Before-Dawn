#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Audita en Play las rutas reales de cada habitación sin guardar cambios en la escena.</summary>
public sealed class AlienNavigationAudit : MonoBehaviour
{
    [MenuItem("The Lights Before Dawn/Pruebas/Auditar navegación de habitaciones")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || SceneManager.GetActiveScene().path != "Assets/Scenes/ModularPrototype.unity") return;
        SessionState.SetBool("TLBD.NavigationAudit", true);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool("TLBD.NavigationAudit", false)) return;
        SessionState.SetBool("TLBD.NavigationAudit", false);
        new GameObject("ALIEN_NAVIGATION_AUDIT").AddComponent<AlienNavigationAudit>();
    }

    private IEnumerator Start()
    {
        yield return null; // AlienPopulation ya creó el tipo de agente y preparó cada habitación.
        HouseFlowController house = HouseFlowController.Instance;
        if (house == null) { Debug.LogError("NAV_AUDIT: HouseFlowController ausente"); EditorApplication.ExitPlaymode(); yield break; }
        var filter = new NavMeshQueryFilter { agentTypeID = RoomNavigation.AgentType, areaMask = NavMesh.AllAreas };
        var failures = new List<string>();
        int checkedRooms = 0, checkedPaths = 0;
        foreach (RoomModule room in house.ModuleById.Values)
        {
            room.gameObject.SetActive(true);
            yield return null;
            checkedRooms++;
            var points = new List<(string name, Vector3 position, float searchRadius)>();
            foreach (DoorSocket door in room.Sockets)
                points.Add(("puerta " + door.SocketId, door.ClearApproachPoint, 0f));
            foreach (AlienSearchPoint point in room.GetComponentsInChildren<AlienSearchPoint>(true))
                if (point.Kind != AlienSearchPointKind.Door)
                    points.Add(("búsqueda " + point.name, point.SearchPosition,
                        point.AssociatedHidingSpot != null ? 1f : 2.5f));

            if (points.Count == 0) continue;
            if (!NavMesh.SamplePosition(points[0].position, out NavMeshHit entry, .8f, filter))
            { failures.Add(room.RoomId + ": primera puerta sin NavMesh"); continue; }
            foreach (var point in points)
            {
                if (point.name == points[0].name) continue;
                bool reachable = false;
                int rings = Mathf.CeilToInt(point.searchRadius / .5f);
                for (int ring = 0; ring <= rings && !reachable; ring++)
                {
                    int directions = ring == 0 ? 1 : 12;
                    for (int direction = 0; direction < directions; direction++)
                    {
                        float angle = direction * Mathf.PI / 6f;
                        Vector3 candidate = point.position + new Vector3(Mathf.Cos(angle), 0f,
                            Mathf.Sin(angle)) * Mathf.Min(ring * .5f, point.searchRadius);
                        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                            point.searchRadius == 0f ? .8f : .3f, filter)
                            || Mathf.Abs(hit.position.y - point.position.y) > .3f
                            || !room.ContainsPoint(hit.position, .15f)) continue;
                        var path = new NavMeshPath();
                        checkedPaths++;
                        reachable = NavMesh.CalculatePath(entry.position, hit.position, filter, path)
                            && path.status == NavMeshPathStatus.PathComplete;
                        if (reachable) break;
                    }
                }
                if (!reachable) failures.Add(room.RoomId + ": sin ruta desde " + points[0].name
                    + " hasta " + point.name + " " + point.position.ToString("F2"));
            }
            if (!house.IsOccupied(room)) room.gameObject.SetActive(false);
        }
        Debug.Log("NAV_AUDIT: habitaciones=" + checkedRooms + ", rutas=" + checkedPaths
            + ", fallos=" + failures.Count + (failures.Count > 0 ? "\n" + string.Join("\n", failures) : ""));
        EditorApplication.ExitPlaymode();
    }
}
#endif
