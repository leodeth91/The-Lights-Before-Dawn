using UnityEngine;
using UnityEngine.AI;

/// <summary>Traduce las acciones de la IA en poses. El NavMeshAgent sigue moviendo el collider.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class AlienAnimationController : MonoBehaviour
{
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
    private static readonly int Run = Animator.StringToHash("Base Layer.Run");
    private static readonly int Observe = Animator.StringToHash("Base Layer.Observe");
    private static readonly int Alert = Animator.StringToHash("Base Layer.Alert");
    private static readonly int Nervous = Animator.StringToHash("Base Layer.Nervous");
    private static readonly int UpperAlert = Animator.StringToHash("Gestures.Alert");
    private static readonly int UpperNervous = Animator.StringToHash("Gestures.Nervous");
    private static readonly int MotionSpeed = Animator.StringToHash("MotionSpeed");

    [SerializeField] private AlienController controller;
    [SerializeField, Min(.05f), Tooltip("Segundos del fundido entre poses.")]
    private float transitionDuration = .18f;
    [SerializeField, Min(.1f)] private float walkReferenceSpeed = .82f;
    [SerializeField, Min(.1f)] private float runReferenceSpeed = 2.35f;
    private Animator animator;
    private AlienAmbientAudio voice;
    private int currentState;
    private int currentGesture;
    private int voiceGesture;
    private float gestureUntil;
    private float lookWeight;
    private bool moving;

    public void Configure(AlienController owner) => controller = owner;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (controller == null) controller = GetComponentInParent<AlienController>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void OnEnable()
    {
        if (controller == null) controller = GetComponentInParent<AlienController>();
        voice = controller != null ? controller.GetComponent<AlienAmbientAudio>() : null;
        if (voice != null) voice.VocalizationStarted += HandleVoice;
        currentState = currentGesture = voiceGesture = 0;
        gestureUntil = 0f;
        moving = false;
    }

    private void OnDisable()
    {
        if (voice != null) voice.VocalizationStarted -= HandleVoice;
    }

    private void HandleVoice(AlienVocalization kind, float duration)
    {
        if (kind == AlienVocalization.Question || kind == AlienVocalization.Response)
            voiceGesture = Nervous;
        else if (kind == AlienVocalization.Call) voiceGesture = Alert;
        else if (kind == AlienVocalization.Chase) { voiceGesture = 0; return; }
        else return;
        gestureUntil = Time.time + Mathf.Clamp(duration, .65f, 2f);
    }

    private void Update()
    {
        if (controller == null || animator == null || !animator.isInitialized || Time.timeScale <= 0f) return;
        NavMeshAgent agent = controller.Motor != null ? controller.Motor.Agent : null;
        float speed = agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped
            ? Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude : 0f;
        if (controller.Motor != null) speed = Mathf.Max(speed, controller.Motor.ManualMotionSpeed);
        // Dos umbrales evitan alternar entre quieto y caminar por pequeños temblores del agente.
        moving = speed > (moving ? .035f : .08f);
        bool alerting = controller.CurrentState is AlienAlertState;
        int gesture = alerting ? Alert : Time.time < gestureUntil ? voiceGesture : 0;
        if (controller.CurrentState is AlienChaseState || controller.CatchRequested) gesture = 0;
        int next = moving ? (speed > 1.55f ? Run : Walk)
            : gesture != 0 ? gesture
            : controller.Scanning || controller.CurrentState is AlienScanState ? Observe : Idle;
        if (currentState != next)
        {
            // El grito tiene una preparación larga. Entramos cerca del gesto para que quepa en la alerta breve.
            animator.CrossFadeInFixedTime(next, transitionDuration, 0, next == Alert ? .65f : 0f);
            currentState = next;
        }
        float rate = next == Walk ? Mathf.Clamp(speed / walkReferenceSpeed, .5f, 1.8f)
            : next == Run ? Mathf.Clamp(speed / runReferenceSpeed, .65f, 1.4f) : 1f;
        animator.SetFloat(MotionSpeed, rate, .12f, Time.deltaTime);

        // Si responde mientras camina, solo gesticula con el torso: las piernas siguen caminando.
        bool upperBody = moving && gesture != 0;
        int upper = gesture == Alert ? UpperAlert : UpperNervous;
        if (upperBody && currentGesture != upper)
        {
            animator.CrossFadeInFixedTime(upper, transitionDuration, 1, gesture == Alert ? .65f : 0f);
            currentGesture = upper;
        }
        if (!upperBody) currentGesture = 0;
        animator.SetLayerWeight(1, Mathf.MoveTowards(animator.GetLayerWeight(1),
            upperBody ? 1f : 0f, Time.deltaTime / transitionDuration));
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (controller == null || animator == null || !animator.isHuman) return;
        Vector3? point = controller.FocusPoint;
        if (!point.HasValue && controller.Target != null) point = controller.Target.LookPoint;
        if (layerIndex == 0) lookWeight = Mathf.MoveTowards(lookWeight, point.HasValue ? .85f : 0f, Time.deltaTime * 5f);
        animator.SetLookAtWeight(lookWeight, .1f, .85f, 0f, .65f);
        if (point.HasValue) animator.SetLookAtPosition(point.Value);
    }
}
