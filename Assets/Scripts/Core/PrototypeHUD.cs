using UnityEngine;
using UnityEngine.UI;

/// <summary>Actualiza los textos y paneles al recibir avisos del juego. Muestra la habitación, la interacción y el tiempo; no decide objetivos ni capturas.</summary>
[DisallowMultipleComponent]
public sealed class PrototypeHUD : MonoBehaviour
{
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultText;
    [SerializeField] private Text roomText;
    [SerializeField] private Text connectionText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text interactionText;
    [SerializeField] private Text objectiveText;
    [SerializeField] private GameObject gameplayPanel;

    private HouseFlowController house;
    private GameSessionManager session;
    private PlayerInteractor playerInteractor;
    private StoryProgression story;

    public void Configure(GameObject menu, GameObject result, Text resultLabel,
        Text roomLabel, Text connectionLabel, Text timerLabel, Text interactionLabel)
    {
        startPanel = menu;
        resultPanel = result;
        resultText = resultLabel;
        roomText = roomLabel;
        connectionText = connectionLabel;
        timerText = timerLabel;
        interactionText = interactionLabel;
    }

    private void Start()
    {
        Transform controls = transform.Find("GameplayHUD/Controles");
        if (controls != null) controls.gameObject.SetActive(false);
        house = HouseFlowController.Instance;
        session = GameSessionManager.Instance;
        playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        story = StoryProgression.Instance;
        if (story != null)
        {
            story.ObjectiveChanged += HandleObjectiveChanged;
            HandleObjectiveChanged(story.Objective);
        }

        if (house != null)
        {
            house.CurrentRoomChanged += HandleRoomChanged;
            house.ConnectionOpened += HandleConnectionOpened;
            house.ConnectionClosed += HandleConnectionClosed;
            HandleRoomChanged(null, house.CurrentRoom);
        }

        if (session != null)
        {
            session.StateChanged += HandleStateChanged;
            HandleStateChanged(session.State);
        }

        if (playerInteractor != null)
        {
            playerInteractor.PromptChanged += HandlePromptChanged;
            HandlePromptChanged(playerInteractor.CurrentPrompt);
        }
    }

    private void OnDestroy()
    {
        if (house != null)
        {
            house.CurrentRoomChanged -= HandleRoomChanged;
            house.ConnectionOpened -= HandleConnectionOpened;
            house.ConnectionClosed -= HandleConnectionClosed;
        }

        if (session != null)
        {
            session.StateChanged -= HandleStateChanged;
        }

        if (playerInteractor != null)
        {
            playerInteractor.PromptChanged -= HandlePromptChanged;
        }
        if (story != null) story.ObjectiveChanged -= HandleObjectiveChanged;
    }

    private void HandlePromptChanged(string prompt)
    {
        if (interactionText != null)
        {
            interactionText.text = prompt;
        }
    }

    private void Update()
    {
        bool showGameplay = !OpeningSequenceDirector.IsRunning
            && session != null && session.IsPlaying;
        if (gameplayPanel != null && gameplayPanel.activeSelf != showGameplay)
            gameplayPanel.SetActive(showGameplay);
        if (roomText != null) roomText.enabled = showGameplay;
        if (connectionText != null) connectionText.enabled = false;
        if (interactionText != null) interactionText.enabled = showGameplay;
        if (timerText != null && session != null)
        {
            int remainingSeconds = Mathf.CeilToInt(session.RemainingTime);
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            timerText.text = session.IsPlaying && session.HasSurvivalTimer
                ? $"Amanecer en: {minutes:00}:{seconds:00}" : string.Empty;
        }
    }

    private void HandleObjectiveChanged(StoryObjective objective)
    {
        if (objectiveText == null) return;
        switch (objective)
        {
            case StoryObjective.FindParents: objectiveText.text = "Buscar a mamá y papá"; break;
            case StoryObjective.TryExit: objectiveText.text = "Intentar salir de la casa"; break;
            case StoryObjective.FindPhone: objectiveText.text = "Encontrar el celular"; break;
            case StoryObjective.Survive: objectiveText.text = "Sobrevivir hasta el amanecer"; break;
            case StoryObjective.Escape: objectiveText.text = "Salir de la casa"; break;
        }
    }

    private void HandleStateChanged(GameSessionState state)
    {
        if (startPanel != null)
        {
            startPanel.SetActive(state == GameSessionState.Menu);
        }

        bool finished = state == GameSessionState.Victory || state == GameSessionState.Defeat;
        if (resultPanel != null)
        {
            resultPanel.SetActive(finished);
        }

        if (resultText != null && finished)
        {
            resultText.text = state == GameSessionState.Victory
                ? "Llegó el amanecer, sobreviviste\n\nR: reiniciar"
                : "nunca nadie lo volvió a ver\n\nR: reiniciar";
        }
    }

    private void HandleRoomChanged(RoomModule previous, RoomModule current)
    {
        if (roomText != null)
        {
            roomText.text = current != null ? "Habitacion: " + current.DisplayName : string.Empty;
        }
        RefreshPlayerConnection();
    }

    private void HandleConnectionOpened(RoomConnection connection)
    {
        RefreshPlayerConnection();
    }

    private void HandleConnectionClosed(RoomConnection connection)
    {
        RefreshPlayerConnection();
    }

    private void RefreshPlayerConnection()
    {
        if (connectionText == null || house == null) return;
        RoomConnection connection = house.ActiveConnection;
        if (connection == null) { connectionText.text = "Conexion cerrada"; return; }
        RoomModule destination = connection.SourceRoom == house.CurrentRoom ? connection.TargetRoom : connection.SourceRoom;
        connectionText.text = (connection.IsAnomalous ? "Anomalia: " : "Conexion normal: ") + destination.DisplayName;
    }
}
