#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Comprueba en Play las colisiones y las fuentes de luz de las nuevas aberturas.</summary>
public sealed class WindowOpeningRuntimeSmokeTest : MonoBehaviour
{
    private const string SessionKey = "TLBD.WindowOpeningSmoke";

    [MenuItem("The Lights Before Dawn/Pruebas/Ventanas, muros y luz")]
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
        new GameObject("WINDOW_OPENING_SMOKE_TEST").AddComponent<WindowOpeningRuntimeSmokeTest>();
    }

    private IEnumerator Start()
    {
        yield return null;
        var failures = new List<string>();
        var details = new List<string>();
        int checkedWindows = 0;
        int checkedWalls = 0;
        RoomModule[] rooms = FindObjectsByType<RoomModule>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (RoomModule room in rooms)
        {
            bool wasActive = room.gameObject.activeSelf;
            room.gameObject.SetActive(true);
            Physics.SyncTransforms();
            foreach (Light light in room.GetComponentsInChildren<Light>(true))
                if ((light.name == "LuzHabitacion" || light.name == "LuzSuaveVentana") && light.enabled)
                    failures.Add(room.RoomId + ": luz interior activada: " + light.name);

            foreach (Transform window in room.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == "Ventana"))
            {
                string name = room.RoomId + "/" + window.parent.name;
                checkedWindows++;
                BoxCollider pane = window.Find("CristalColision")?.GetComponent<BoxCollider>();
                Light exterior = window.Find("LuzExteriorVentana")?.GetComponent<Light>();
                Renderer backing = window.GetComponent<Renderer>();
                if (pane == null || !pane.enabled || pane.isTrigger)
                    failures.Add(name + ": cristal sin colisión sólida.");
                if (backing == null || backing.enabled)
                    failures.Add(name + ": panel opaco original visible.");
                if (exterior == null || !exterior.enabled || exterior.type != LightType.Spot
                    || exterior.shadows == LightShadows.None)
                    failures.Add(name + ": falta luz exterior con sombras.");

                Renderer[] visual = window.Find("Visual_WindowOkno")?.GetComponentsInChildren<Renderer>(true);
                if (visual == null || visual.Count(renderer => renderer.sharedMaterials.Any(material =>
                    material != null && material.name == "WindowGlassNight")) != 4)
                    failures.Add(name + ": faltan cuatro paños de cristal traslúcido.");

                Vector3 origin = window.TransformPoint(new Vector3(0f, 0f, -0.4f));
                Vector3 direction = window.forward;
                RaycastHit[] throughPane = Physics.RaycastAll(origin, direction, 1f);
                if (pane != null && !throughPane.Any(hit => hit.collider == pane))
                    failures.Add(name + ": un rayo no encuentra el collider del cristal.");
                int solidSides = 0;
                var sideDiagnostics = new List<string>();
                foreach (float offset in new[] { -0.84f, 0.84f })
                {
                    Vector3 sideOrigin = window.TransformPoint(new Vector3(offset, 0f, -0.4f));
                    RaycastHit[] sideHits = Physics.RaycastAll(sideOrigin, direction, 1f);
                    sideDiagnostics.Add(offset + ":" + string.Join(",",
                        sideHits.Select(hit => hit.collider.name)));
                    if (sideHits.Any(hit => hit.collider is BoxCollider
                        && hit.collider.transform.parent != null
                        && hit.collider.transform.parent.name == "ColisionMuros")) solidSides++;
                }
                if (solidSides != 2)
                {
                    BoxCollider[] segments = room.GetComponentsInChildren<BoxCollider>(true)
                        .Where(item => item.transform.parent != null
                            && item.transform.parent.name == "ColisionMuros").ToArray();
                    Vector3 probe = window.TransformPoint(new Vector3(.84f, 0f, -.4f));
                    BoxCollider nearest = segments.OrderBy(item => Vector3.Distance(
                        item.bounds.ClosestPoint(probe), probe)).FirstOrDefault();
                    failures.Add(name + ": pared sin colisión a ambos lados (" + solidSides + "/2). "
                        + string.Join(" ", sideDiagnostics) + " scale=" + window.lossyScale.ToString("F2")
                        + " nearest=" + (nearest != null ? nearest.name + "@"
                            + Vector3.Distance(nearest.bounds.ClosestPoint(probe), probe).ToString("F2")
                            + "/dot=" + Vector3.Dot(nearest.bounds.center - probe, direction).ToString("F2")
                            + "/windowInWall=" + nearest.transform.parent.parent.parent
                                .InverseTransformPoint(window.position).ToString("F2") : "none"));
                }
                details.Add(name + ": cristal y laterales=" + solidSides + "/2");
            }
            checkedWalls += room.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name == "MuroConAberturaVentanas"
                    && item.gameObject.activeInHierarchy
                    && item.GetComponentsInChildren<BoxCollider>(true).Length > 0);
            room.gameObject.SetActive(wasActive);
        }
        if (checkedWindows != 12) failures.Add("Se esperaban 12 ventanas; vistas=" + checkedWindows);
        if (checkedWalls != 11) failures.Add("Se esperaban 11 muros recortados; vistos=" + checkedWalls);
        if (RenderSettings.ambientIntensity > 0.001f || RenderSettings.ambientLight.maxColorComponent > 0.001f)
            failures.Add("Iluminación ambiental de la escena sigue activa.");

        string report = "FAILURES=" + failures.Count + " WINDOWS=" + checkedWindows
            + " WALLS=" + checkedWalls + "\n" + string.Join("\n", failures)
            + "\n" + string.Join("\n", details);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "TLBD_WindowOpeningSmoke.txt"), report);
        Debug.Log("Prueba ventanas: " + failures.Count + " fallos, " + checkedWindows + " ventanas.");
        EditorApplication.ExitPlaymode();
    }
}
#endif
