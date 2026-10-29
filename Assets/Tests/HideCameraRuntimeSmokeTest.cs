#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Comprueba la vista del escondite y su restauración en la escena real.</summary>
public sealed class HideCameraRuntimeSmokeTest : MonoBehaviour
{
    private readonly List<string> failures = new List<string>();
    private readonly List<string> observations = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SessionState.GetBool("TLBD.HideCameraSmoke", false)) return;
        SessionState.SetBool("TLBD.HideCameraSmoke", false);
        new GameObject("HIDE_CAMERA_SMOKE_TEST").AddComponent<HideCameraRuntimeSmokeTest>();
    }

    private IEnumerator Start()
    {
        yield return null;
        HouseFlowController house = HouseFlowController.Instance;
        PlayerHidingController hiding = FindFirstObjectByType<PlayerHidingController>();
        OverShoulderCamera cameraController = FindFirstObjectByType<OverShoulderCamera>();
        if (house == null || hiding == null || cameraController == null
            || !house.ModuleById.ContainsKey("child_room"))
        {
            failures.Add("Faltan la habitación infantil, el jugador o la cámara.");
            Finish();
            yield break;
        }

        GameSessionManager.Instance.BeginGame();
        foreach (AlienController alien in FindObjectsByType<AlienController>(FindObjectsSortMode.None))
            alien.SetPlayerDetectionEnabled(false);
        string[] roomIds = { "child_room", "parents_bedroom", "child_room",
            "child_room", "living_dining_room", "child_room", "lower_bathroom" };
        HideTransitionType[] types = { HideTransitionType.ProneSlide,
            HideTransitionType.ProneSlide, HideTransitionType.CrouchSlide, HideTransitionType.StepInside,
            HideTransitionType.ClimbInside, HideTransitionType.BehindCurtain,
            HideTransitionType.StepOverAndHide };
        CharacterController character = hiding.GetComponent<CharacterController>();
        Renderer[] visuals = hiding.transform.Find("PlayerVisual")?.GetComponentsInChildren<Renderer>(true);
        if (character == null || visuals == null || visuals.Length == 0)
        {
            failures.Add("Faltan el collider o las mallas del jugador.");
            Finish();
            yield break;
        }

        for (int index = 0; index < types.Length; index++)
        {
            HideTransitionType type = types[index];
            string label = roomIds[index] + "/" + type;
            if (!house.ModuleById.TryGetValue(roomIds[index], out RoomModule room))
            {
                failures.Add("Falta la habitación " + roomIds[index] + ".");
                continue;
            }
            room.gameObject.SetActive(true);
            HideSpot[] spots = room.GetComponentsInChildren<HideSpot>(true);
            HideSpot spot = spots.FirstOrDefault(candidate => candidate.TransitionType == type);
            if (spot == null || spot.EntryPoint == null || spot.HiddenPoint == null)
            {
                failures.Add("Falta el escondite " + label + " o sus puntos.");
                continue;
            }

            bool[] originalVisibility = visuals.Select(renderer => renderer.forceRenderingOff).ToArray();
            house.RegisterOccupant(hiding.gameObject, room, false);
            character.enabled = false;
            hiding.transform.SetPositionAndRotation(spot.EntryPoint.position, spot.EntryPoint.rotation);
            character.enabled = true;
            Physics.SyncTransforms();
            hiding.Toggle(spot);
            Transform chestDoorJoint = null;
            Quaternion closedChestDoor = Quaternion.identity;
            if (type == HideTransitionType.ClimbInside)
            {
                chestDoorJoint = spot.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(child => child.name == "Joint_1_2_03"
                        && child.parent != null && child.parent.name == "_rootJoint");
                if (chestDoorJoint == null)
                    failures.Add(label + ": falta la articulación de la tapa importada.");
                else
                {
                    closedChestDoor = chestDoorJoint.localRotation;
                    yield return new WaitForSecondsRealtime(.5f);
                    if (Quaternion.Angle(closedChestDoor, chestDoorJoint.localRotation) < 5f)
                        failures.Add(label + ": la tapa importada no se abrió al entrar.");
                }
            }
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!hiding.IsHidden && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(.35f);
            if (chestDoorJoint != null
                && Quaternion.Angle(closedChestDoor, chestDoorJoint.localRotation) > 3f)
                failures.Add(label + ": la tapa importada no se cerró al quedar oculto.");

            float hiddenDistance = Vector3.Distance(cameraController.transform.position,
                hiding.transform.position);
            observations.Add(label + ": distancia al esconderse=" + hiddenDistance.ToString("F2"));
            if (type == HideTransitionType.ProneSlide)
            {
                float eyeHeight = cameraController.transform.position.y - hiding.transform.position.y;
                observations.Add(label + ": altura de cámara sobre el punto oculto="
                    + eyeHeight.ToString("F2"));
                if (eyeHeight < 0.1f || eyeHeight > 0.2f)
                    failures.Add(label + ": la cámara no quedó al ras del suelo.");
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Temp/HideCamera_" + roomIds[index] + "_" + type + ".png");

            if (!hiding.IsHidden || !cameraController.IsFirstPersonHiding)
                failures.Add(label + ": no se activó la primera persona.");
            if (hiddenDistance > 1.15f)
                failures.Add(label + ": la cámara quedó fuera del escondite.");
            if (visuals.Any(renderer => renderer != null && !renderer.forceRenderingOff))
                failures.Add(label + ": el cuerpo tapa la vista del niño.");

            if (hiding.IsHidden) hiding.Toggle(spot);
            deadline = Time.realtimeSinceStartup + 3f;
            while (hiding.State != PlayerHideState.Outside && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(.35f);
            if (hiding.State != PlayerHideState.Outside || cameraController.IsFirstPersonHiding)
                failures.Add(label + ": la cámara no volvió a tercera persona.");
            observations.Add(label + ": distancia al salir="
                + Vector3.Distance(cameraController.transform.position,
                    hiding.transform.position).ToString("F2"));
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i] != null && visuals[i].forceRenderingOff != originalVisibility[i])
                    failures.Add(label + ": no se restauró la visibilidad del modelo.");
        }

        Finish();
    }

    private void Finish()
    {
        File.WriteAllText("Temp/HideCameraTestResult.txt",
            "FAILURES=" + failures.Count + "\n" + string.Join("\n", failures)
            + "\n" + string.Join("\n", observations));
        EditorApplication.ExitPlaymode();
    }
}
#endif
