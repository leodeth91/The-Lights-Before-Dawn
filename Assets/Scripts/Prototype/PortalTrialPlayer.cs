using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Controles mínimos para ensayar el portal sin cargar los sistemas narrativos de la partida.</summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PortalTrialPlayer : MonoBehaviour
{
    [SerializeField] private Camera view;
    [SerializeField] private Rigidbody testCube;
    private CharacterController controller;
    private float pitch;
    private float fallSpeed;

    public void Configure(Camera playerView, Rigidbody cube) { view = playerView; testCube = cube; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || view == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState != CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 mouse = Mouse.current.delta.ReadValue();
            transform.Rotate(0f, mouse.x * .10f, 0f);
            pitch = Mathf.Clamp(pitch - mouse.y * .10f, -75f, 75f);
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        Vector3 direction = (keyboard.dKey.isPressed ? transform.right : Vector3.zero)
            - (keyboard.aKey.isPressed ? transform.right : Vector3.zero)
            + (keyboard.wKey.isPressed ? transform.forward : Vector3.zero)
            - (keyboard.sKey.isPressed ? transform.forward : Vector3.zero);
        if (direction.sqrMagnitude > 1f) direction.Normalize();
        fallSpeed = controller.isGrounded ? -1f : fallSpeed - 18f * Time.deltaTime;
        controller.Move((direction * 2f + Vector3.up * fallSpeed) * Time.deltaTime);
        if (keyboard.fKey.wasPressedThisFrame && testCube != null)
        {
            testCube.position = view.transform.position + view.transform.forward * .6f;
            testCube.rotation = Quaternion.identity;
            testCube.linearVelocity = view.transform.forward * 5f;
            testCube.angularVelocity = Vector3.zero;
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(12, 12, 440, 68), "PRUEBA AISLADA: habitación del niño ↔ pasillo\nWASD mover · ratón mirar · F lanzar cubo · Esc liberar ratón");
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
