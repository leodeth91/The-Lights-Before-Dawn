using UnityEngine;

/// <summary>Muestra las decisiones de pausa; el estado y las transiciones pertenecen a la sesión.</summary>
[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    private void OnGUI()
    {
        GameSessionManager session = GameSessionManager.Instance;
        if (session == null || !session.IsPaused) return;

        float scale = Mathf.Max(.75f, Screen.height / 720f);
        float width = Mathf.Min(Screen.width * .72f, 430f * scale);
        float height = 330f * scale;
        Rect panel = new Rect((Screen.width - width) * .5f,
            (Screen.height - height) * .5f, width, height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, .72f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(30f * scale),
            fontStyle = FontStyle.Bold
        };
        GUIStyle button = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(18f * scale)
        };

        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x, panel.y + 18f * scale, panel.width, 52f * scale),
            "PAUSA", title);

        float buttonX = panel.x + 42f * scale;
        float buttonWidth = panel.width - 84f * scale;
        float buttonHeight = 54f * scale;
        float buttonY = panel.y + 86f * scale;
        if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight), "Continuar", button))
            session.ResumeGame();
        buttonY += 70f * scale;
        if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight),
                "Salir a la pantalla de inicio", button))
            session.ReturnToMainMenu();
        buttonY += 70f * scale;
        if (GUI.Button(new Rect(buttonX, buttonY, buttonWidth, buttonHeight),
                "Salir al escritorio", button))
            session.QuitToDesktop();
    }
}
