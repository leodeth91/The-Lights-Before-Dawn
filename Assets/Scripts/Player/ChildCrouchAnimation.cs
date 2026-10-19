using UnityEngine;

/// <summary>Elige la animación visible del niño. El CharacterController sigue siendo el único que mueve y colisiona.</summary>
public sealed class ChildCrouchAnimation : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
    private static readonly int Run = Animator.StringToHash("Base Layer.Run");
    private static readonly int CrouchIdle = Animator.StringToHash("Base Layer.CrouchIdle");
    private static readonly int CrouchWalk = Animator.StringToHash("Base Layer.CrouchWalk");
    private static readonly int Prone = Animator.StringToHash("Base Layer.Prone");
    private static readonly int Jump = Animator.StringToHash("Base Layer.Jump");

    [SerializeField] private ChildPlayerController player;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform modelRoot;
    private PlayerHidingController hiding;
    private Quaternion restModelRotation;
    private Vector3 restModelPosition;
    private Vector3 restModelScale;
    private Vector3 previousPosition;
    private int currentState;

    // Las alturas se midieron sobre el modelo animado en Unity. La cápsula física
    // conserva su tamaño; solo se corrige la posición visible de cada pose.
    private const float WalkGroundLift = 0.03f;
    private const float RunGroundLift = 0.065f;
    private const float CrouchIdleGroundLift = 0.10f;
    private const float CrouchWalkGroundLift = 0.04f;
    private const float JumpGroundLift = 0.13f;
    private const float ProneGroundLift = 0.11f;
    private const float RunVisualScale = 0.98f;
    private const float JumpPlaybackSpeed = 1.6f;

    private void Awake()
    {
        previousPosition = transform.position;
        hiding = GetComponent<PlayerHidingController>();
        if (modelRoot == null || animator == null) return;
        restModelRotation = modelRoot.localRotation;
        restModelPosition = modelRoot.localPosition;
        restModelScale = modelRoot.localScale;
        animator.applyRootMotion = false;
        animator.enabled = true;
        animator.Play(Idle, 0, 0f);
        currentState = Idle;
    }

    private void LateUpdate()
    {
        if (player == null || animator == null || modelRoot == null) return;
        Vector3 movement = transform.position - previousPosition;
        movement.y = 0f;
        bool moving = movement.sqrMagnitude > 0.000004f;
        int nextState = ChooseState(moving);
        if (nextState != currentState)
        {
            // Un fundido corto evita un salto brusco entre las poses.
            animator.CrossFadeInFixedTime(nextState, nextState == Jump ? 0.06f : 0.12f);
            currentState = nextState;
        }
        animator.speed = nextState == Jump ? JumpPlaybackSpeed : 1f;
        if (nextState == Prone)
        {
            // Sleeping Idle ya está acostado. Cancelamos el giro que usaba el
            // escondite para acostar al antiguo cuerpo geométrico.
            Quaternion hideRotation = Quaternion.Euler(hiding.CurrentSpot.HiddenVisualEulerAngles);
            Quaternion correction = Quaternion.Inverse(hideRotation);
            modelRoot.localRotation = correction * restModelRotation;
            modelRoot.localPosition = restModelPosition
                + correction * (Vector3.up * ProneGroundLift);
        }
        else
        {
            modelRoot.localRotation = restModelRotation;
            modelRoot.localPosition = restModelPosition + Vector3.up * GroundLift(nextState);
        }
        // Sneak Walk flexiona las piernas por sí mismo: no se reduce el tamaño
        // del niño para simular que está agachado.
        float poseScale = nextState == Run ? RunVisualScale : 1f;
        modelRoot.localScale = restModelScale * poseScale;
        previousPosition = transform.position;
    }

    private static float GroundLift(int state)
    {
        if (state == Walk) return WalkGroundLift;
        if (state == Run) return RunGroundLift;
        if (state == CrouchIdle) return CrouchIdleGroundLift;
        if (state == CrouchWalk) return CrouchWalkGroundLift;
        if (state == Jump) return JumpGroundLift;
        return 0f;
    }

    private int ChooseState(bool moving)
    {
        if (hiding != null && hiding.State == PlayerHideState.Hidden && hiding.CurrentSpot != null)
        {
            HideTransitionType type = hiding.CurrentSpot.TransitionType;
            if (type == HideTransitionType.ProneSlide) return Prone;
            if (type == HideTransitionType.CrouchSlide || type == HideTransitionType.ClimbInside)
                return CrouchIdle;
            return Idle;
        }
        if (hiding != null && hiding.State != PlayerHideState.Outside) return Idle;
        if (player.IsJumping) return Jump;
        if (player.IsMovementLocked || !player.IsGrounded) return Idle;
        if (player.IsCrouched) return moving ? CrouchWalk : CrouchIdle;
        if (!moving) return Idle;
        return player.IsSprinting ? Run : Walk;
    }
}
