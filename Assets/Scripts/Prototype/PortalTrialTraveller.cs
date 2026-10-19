using UnityEngine;

/// <summary>Traslada jugador u objeto al cruzar completamente el plano del marco de esta prueba.</summary>
public sealed class PortalTrialTraveller : MonoBehaviour
{
    [SerializeField] private PortalTrialGate first;
    [SerializeField] private PortalTrialGate second;
    private CharacterController character;
    private Rigidbody body;
    private Vector3 previousPosition;
    private float nextCrossingTime;

    public void Configure(PortalTrialGate a, PortalTrialGate b) { first = a; second = b; }

    private void Awake()
    {
        character = GetComponent<CharacterController>();
        body = GetComponent<Rigidbody>();
        previousPosition = transform.position;
    }

    private void OnEnable() => previousPosition = transform.position;
    private void LateUpdate() { if (body == null) CheckCrossing(); }
    private void FixedUpdate() { if (body != null) CheckCrossing(); }

    private void CheckCrossing()
    {
        Vector3 current = transform.position;
        if (Time.time >= nextCrossingTime)
        {
            PortalTrialGate crossed = first != null && first.WasCrossed(previousPosition, current) ? first
                : second != null && second.WasCrossed(previousPosition, current) ? second : null;
            if (crossed != null)
            {
                Quaternion oldRotation = transform.rotation;
                Quaternion newRotation = crossed.MapRotation(oldRotation);
                Vector3 newPosition = crossed.ExitPoint(current);
                if (character != null) character.enabled = false;
                if (body != null)
                {
                    Quaternion delta = newRotation * Quaternion.Inverse(oldRotation);
                    body.position = newPosition;
                    body.rotation = newRotation;
                    body.linearVelocity = delta * body.linearVelocity;
                    body.angularVelocity = delta * body.angularVelocity;
                }
                transform.SetPositionAndRotation(newPosition, newRotation);
                if (character != null) character.enabled = true;
                Physics.SyncTransforms();
                nextCrossingTime = Time.time + .2f;
                current = newPosition;
            }
        }
        previousPosition = current;
    }
}
