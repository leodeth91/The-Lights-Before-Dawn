using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Completa la interfaz existente sin reemplazar sus referencias de historia y video.</summary>
public static class MenuHudLayoutTool
{
    private const string ScenePath = "Assets/Scenes/ModularPrototype.unity";
    private static readonly Color Ink = new Color(.94f, .91f, .83f);
    private static readonly Color Accent = new Color(1f, .63f, .34f);

    [MenuItem("The Lights Before Dawn/Arte/Preparar menú y HUD")]
    private static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
        {
            Debug.LogError("Abrí ModularPrototype guardada y salí de Play antes de preparar la interfaz.");
            return;
        }

        PrototypeHUD hud = UnityEngine.Object.FindFirstObjectByType<PrototypeHUD>(FindObjectsInactive.Include);
        GameSessionManager session = UnityEngine.Object.FindFirstObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        if (hud == null || session == null)
        {
            Debug.LogError("Falta PrototypeHUD o GameSessionManager.");
            return;
        }
        RectTransform canvas = hud.GetComponent<RectTransform>();
        RectTransform menu = canvas.Find("PanelInicio") as RectTransform;
        if (menu == null || menu.Find("ComenzarPartida") != null)
        {
            Debug.LogError("Falta PanelInicio o la interfaz nueva ya existe.");
            return;
        }

        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        Text oldHelp = menu.Find("AyudaInicio")?.GetComponent<Text>();
        Font font = oldHelp != null ? oldHelp.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (oldHelp != null) oldHelp.gameObject.SetActive(false);

        Text title = menu.Find("Titulo")?.GetComponent<Text>();
        if (title != null)
        {
            RectTransform titleRect = title.rectTransform;
            Place(titleRect, new Vector2(.5f, 1f), new Vector2(0f, -160f), new Vector2(1450f, 170f));
            title.fontSize = 84;
            title.color = Ink;
            title.alignment = TextAnchor.MiddleCenter;
            title.raycastTarget = false;
        }

        Text instructions = MakeText("Instrucciones", menu, font,
            "INSTRUCCIONES\n\nMoverse     WASD\nMirar           MOUSE\nInteractuar   E\nAgacharse   C\nCorrer         SHIFT\nPausa           ESC",
            30, TextAnchor.UpperLeft, Ink);
        Place(instructions.rectTransform, new Vector2(0f, .5f), new Vector2(175f, -40f), new Vector2(540f, 540f));
        instructions.rectTransform.pivot = new Vector2(0f, .5f);

        StartMenuControls controls = menu.gameObject.AddComponent<StartMenuControls>();
        Button start = MakeButton("ComenzarPartida", "COMENZAR PARTIDA", menu, font, new Vector2(-190f, 75f));
        Button credits = MakeButton("Creditos", "CRÉDITOS", menu, font, new Vector2(-190f, -45f));
        Button quit = MakeButton("Salir", "SALIR", menu, font, new Vector2(-190f, -165f));
        UnityEventTools.AddPersistentListener(start.onClick, session.BeginGame);
        UnityEventTools.AddPersistentListener(credits.onClick, controls.ShowCredits);
        UnityEventTools.AddPersistentListener(quit.onClick, session.QuitToDesktop);

        RectTransform creditsPanel = MakePanel("PanelCreditos", menu, new Color(.015f, .02f, .035f, .97f));
        Stretch(creditsPanel);
        Text creditsTitle = MakeText("TituloCreditos", creditsPanel, font, "CRÉDITOS", 65,
            TextAnchor.MiddleCenter, Ink);
        Place(creditsTitle.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -200f), new Vector2(1000f, 110f));
        Text creditBody = MakeText("ContenidoCreditos", creditsPanel, font,
            "THE LIGHTS BEFORE DAWN\n\nJuego y dirección: equipo del proyecto\n\nCréditos completos de modelos, audio y demás recursos\nen la documentación del proyecto.",
            30, TextAnchor.MiddleCenter, Ink);
        Place(creditBody.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1100f, 390f));
        Button back = MakeButton("Volver", "VOLVER", creditsPanel, font, new Vector2(0f, -360f), false);
        UnityEventTools.AddPersistentListener(back.onClick, controls.HideCredits);
        SerializedObject controlsSerialized = new SerializedObject(controls);
        controlsSerialized.FindProperty("creditsPanel").objectReferenceValue = creditsPanel.gameObject;
        controlsSerialized.ApplyModifiedPropertiesWithoutUndo();
        creditsPanel.gameObject.SetActive(false);

        RectTransform gameplay = new GameObject("GameplayHUD", typeof(RectTransform)).GetComponent<RectTransform>();
        gameplay.SetParent(canvas, false);
        Stretch(gameplay);
        Text objective = MakeText("Objetivo", gameplay, font, "Buscar a mamá y papá", 34,
            TextAnchor.MiddleCenter, Ink);
        Place(objective.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -85f), new Vector2(900f, 75f));
        Text gameplayInstructions = MakeText("Controles", gameplay, font,
            "WASD  Moverse\nMOUSE  Mirar\nE  Interactuar\nC  Agacharse\nSHIFT  Correr\nESC  Pausa",
            23, TextAnchor.UpperLeft, new Color(.84f, .86f, .88f, .8f));
        Place(gameplayInstructions.rectTransform, new Vector2(0f, 1f), new Vector2(42f, -205f),
            new Vector2(310f, 255f));
        gameplayInstructions.rectTransform.pivot = new Vector2(0f, 1f);

        PlaceExisting(canvas, "TextoTiempo", new Vector2(1f, 1f), new Vector2(-40f, -82f),
            new Vector2(425f, 70f), TextAnchor.MiddleRight, 31);
        PlaceExisting(canvas, "TextoHabitacion", new Vector2(0f, 1f), new Vector2(45f, -55f),
            new Vector2(450f, 65f), TextAnchor.MiddleLeft, 26);
        PlaceExisting(canvas, "TextoConexion", new Vector2(0f, 1f), new Vector2(45f, -100f),
            new Vector2(490f, 55f), TextAnchor.MiddleLeft, 21);
        PlaceExisting(canvas, "TextoInteraccion", new Vector2(.5f, 0f), new Vector2(0f, 82f),
            new Vector2(850f, 70f), TextAnchor.MiddleCenter, 28);

        SerializedObject hudSerialized = new SerializedObject(hud);
        hudSerialized.FindProperty("objectiveText").objectReferenceValue = objective;
        hudSerialized.FindProperty("gameplayPanel").objectReferenceValue = gameplay.gameObject;
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(events, scene);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MENU_HUD PASS: botones, créditos, destellos UI, objetivo y HUD creados; video original conservado.");
    }

    [MenuItem("The Lights Before Dawn/Arte/Ajustar márgenes del HUD")]
    private static void CorrectMargins()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
        {
            Debug.LogError("Abrí ModularPrototype guardada y salí de Play antes de ajustar el HUD.");
            return;
        }
        PrototypeHUD hud = UnityEngine.Object.FindFirstObjectByType<PrototypeHUD>(FindObjectsInactive.Include);
        RectTransform canvas = hud != null ? hud.GetComponent<RectTransform>() : null;
        if (canvas == null || canvas.Find("GameplayHUD") == null) return;
        PlaceExisting(canvas, "TextoTiempo", new Vector2(1f, 1f), new Vector2(-40f, -82f),
            new Vector2(425f, 70f), TextAnchor.MiddleRight, 31);
        PlaceExisting(canvas, "TextoHabitacion", new Vector2(0f, 1f), new Vector2(45f, -55f),
            new Vector2(450f, 65f), TextAnchor.MiddleLeft, 26);
        PlaceExisting(canvas, "TextoConexion", new Vector2(0f, 1f), new Vector2(45f, -100f),
            new Vector2(490f, 55f), TextAnchor.MiddleLeft, 21);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MENU_HUD MARGINS PASS: habitación, conexión y contador dentro de pantalla.");
    }

    private static RectTransform MakePanel(string name, Transform parent, Color tint)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        go.GetComponent<Image>().color = tint;
        return rect;
    }

    private static Text MakeText(string name, Transform parent, Font font, string value,
        int size, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(string name, string caption, Transform parent, Font font,
        Vector2 position, bool rightSide = true)
    {
        RectTransform rect = MakePanel(name, parent, new Color(.035f, .045f, .065f, .85f));
        Place(rect, rightSide ? new Vector2(1f, .5f) : new Vector2(.5f, .5f),
            position, new Vector2(450f, 88f));
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .75f, .5f);
        colors.pressedColor = Accent;
        colors.selectedColor = new Color(1f, .75f, .5f);
        button.colors = colors;
        Text label = MakeText("Texto", rect, font, caption, 32, TextAnchor.MiddleCenter, Ink);
        Stretch(label.rectTransform);
        RectTransform emitter = new GameObject("ParticulasDestello", typeof(RectTransform)).GetComponent<RectTransform>();
        emitter.SetParent(rect, false);
        Stretch(emitter);
        SerializedObject sparkle = new SerializedObject(rect.gameObject.AddComponent<UIButtonSparkles>());
        sparkle.FindProperty("emitter").objectReferenceValue = emitter;
        sparkle.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }

    private static void PlaceExisting(RectTransform root, string name, Vector2 anchor, Vector2 position,
        Vector2 size, TextAnchor alignment, int fontSize)
    {
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        foreach (Text label in texts)
        {
            if (label.name != name) continue;
            Place(label.rectTransform, anchor, position, size);
            label.rectTransform.pivot = anchor;
            label.alignment = alignment;
            label.fontSize = fontSize;
            label.raycastTarget = false;
            return;
        }
        throw new InvalidOperationException("Falta texto de HUD: " + name);
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
