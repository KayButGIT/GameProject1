using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Runtime demo controls, shown from an optional HUD button.</summary>
public sealed class CheatMenu
{
    private readonly BombermanPrototype game;
    private readonly GameObject panel;
    private readonly Button nextStageButton;
    private readonly Button godModeButton;
    private readonly InputField stageNumberInput;
    private readonly Text stageFeedback;

    private CheatMenu(BombermanPrototype game, GameObject panel, Button nextStageButton, Button godModeButton,
        InputField stageNumberInput, Text stageFeedback)
    {
        this.game = game;
        this.panel = panel;
        this.nextStageButton = nextStageButton;
        this.godModeButton = godModeButton;
        this.stageNumberInput = stageNumberInput;
        this.stageFeedback = stageFeedback;
    }

    public static CheatMenu Create(BombermanPrototype game, bool showButton)
    {
        EnsureEventSystem();

        GameObject canvasObject = new("Cheat Menu Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        if (showButton)
        {
            Button openButton = CreateButton(canvasObject.transform, "Open Cheat Menu", "CHEATS", new Vector2(1f, 0f),
                new Vector2(-90f, 38f), new Vector2(170f, 58f), 23);
            openButton.onClick.AddListener(game.OpenCheatMenu);
        }

        GameObject panelObject = new("Cheat Panel", typeof(RectTransform), typeof(CanvasRenderer));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(700f, 700f);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.045f, 0.075f, 0.96f);

        CreateText(panelObject.transform, "Cheat Menu Title", "DEMO CHEATS", new Vector2(0.5f, 1f),
            new Vector2(0f, -42f), new Vector2(620f, 60f), 42, FontStyle.Bold, TextAnchor.MiddleCenter);
        Button closeButton = CreateButton(panelObject.transform, "Close Cheat Menu", "X", new Vector2(1f, 1f),
            new Vector2(-38f, -34f), new Vector2(52f, 52f), 28);
        closeButton.onClick.AddListener(game.CloseCheatMenu);

        Button nextStage = CreateButton(panelObject.transform, "Next Stage Cheat", "NEXT STAGE", new Vector2(0.5f, 1f),
            new Vector2(-158f, -112f), new Vector2(300f, 58f), 25);
        nextStage.onClick.AddListener(game.CheatSkipToNextStage);

        Button allPowerUps = CreateButton(panelObject.transform, "All Power Ups Cheat", "ALL POWER-UPS", new Vector2(0.5f, 1f),
            new Vector2(158f, -112f), new Vector2(300f, 58f), 25);
        allPowerUps.onClick.AddListener(game.CheatGrantAllPowerUps);

        InputField stageNumber = CreateStageNumberInput(panelObject.transform);
        Button goToStage = CreateButton(panelObject.transform, "Go To Stage Cheat", "GO", new Vector2(0.5f, 1f),
            new Vector2(192f, -184f), new Vector2(150f, 52f), 24);
        Text stageFeedback = CreateText(panelObject.transform, "Stage Number Feedback", string.Empty, new Vector2(0.5f, 1f),
            new Vector2(0f, -222f), new Vector2(570f, 26f), 16, FontStyle.Normal, TextAnchor.MiddleCenter);
        stageFeedback.color = new Color(1f, 0.62f, 0.62f);
        goToStage.onClick.AddListener(() =>
        {
            if (!int.TryParse(stageNumber.text, out int targetStage))
            {
                stageFeedback.text = "ENTER A VALID STAGE NUMBER";
                return;
            }

            if (!game.CheatGoToStage(targetStage, out string error))
            {
                stageFeedback.text = error;
            }
        });

        Button godMode = CreateButton(panelObject.transform, "God Mode Cheat", "GOD MODE: OFF", new Vector2(0.5f, 1f),
            new Vector2(0f, -264f), new Vector2(620f, 54f), 24);
        godMode.onClick.AddListener(() =>
        {
            game.ToggleGodMode();
            godMode.GetComponentInChildren<Text>().text = game.GodModeEnabled ? "GOD MODE: ON" : "GOD MODE: OFF";
        });

        PowerUpType[] types = (PowerUpType[])Enum.GetValues(typeof(PowerUpType));
        for (int i = 0; i < types.Length; i++)
        {
            int column = i % 2;
            int row = i / 2;
            string label = LabelFor(types[i]);
            Button powerUpButton = CreateButton(panelObject.transform, $"Cheat {label}", label,
                new Vector2(0.5f, 1f), new Vector2(column == 0 ? -158f : 158f, -338f - row * 68f),
                new Vector2(300f, 54f), 23);
            PowerUpType type = types[i];
            powerUpButton.onClick.AddListener(() => game.CheatGrantPowerUp(type));
        }

        panelObject.SetActive(false);
        return new CheatMenu(game, panelObject, nextStage, godMode, stageNumber, stageFeedback);
    }

    public void SetVisible(bool visible)
    {
        panel.SetActive(visible);
        if (!visible) return;

        nextStageButton.interactable = !game.IsLastStage;
        godModeButton.GetComponentInChildren<Text>().text = game.GodModeEnabled ? "GOD MODE: ON" : "GOD MODE: OFF";
        stageNumberInput.text = game.CurrentStageNumber.ToString();
        stageFeedback.text = string.Empty;
    }

    private static InputField CreateStageNumberInput(Transform parent)
    {
        GameObject inputObject = new("Stage Number Input", typeof(RectTransform), typeof(CanvasRenderer));
        inputObject.transform.SetParent(parent, false);
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-80f, -184f);
        rect.sizeDelta = new Vector2(360f, 52f);

        Image background = inputObject.AddComponent<Image>();
        background.color = new Color(0.09f, 0.12f, 0.18f, 1f);
        InputField input = inputObject.AddComponent<InputField>();
        Text valueText = CreateText(inputObject.transform, "Stage Number Text", string.Empty, Vector2.zero,
            Vector2.zero, Vector2.zero, 24, FontStyle.Bold, TextAnchor.MiddleCenter, stretch: true);
        Text placeholder = CreateText(inputObject.transform, "Stage Number Placeholder", "STAGE NUMBER", Vector2.zero,
            Vector2.zero, Vector2.zero, 20, FontStyle.Normal, TextAnchor.MiddleCenter, stretch: true);
        placeholder.color = new Color(0.68f, 0.72f, 0.79f, 0.8f);
        input.textComponent = valueText;
        input.placeholder = placeholder;
        input.contentType = InputField.ContentType.IntegerNumber;
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 8;
        input.targetGraphic = background;
        return input;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject eventSystemObject = new("Event System");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchor,
        Vector2 anchoredPosition, Vector2 size, int fontSize)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.27f, 0.43f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        CreateText(buttonObject.transform, objectName + " Label", label, Vector2.zero, Vector2.zero,
            Vector2.zero, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, stretch: true);
        return button;
    }

    private static Text CreateText(Transform parent, string objectName, string value, Vector2 anchor,
        Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyle style, TextAnchor alignment, bool stretch = false)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 3f);
            rect.offsetMax = new Vector2(-6f, -3f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        return text;
    }

    private static string LabelFor(PowerUpType type)
    {
        return type switch
        {
            PowerUpType.BombUp => "BOMB UP",
            PowerUpType.FireUp => "FIRE UP",
            PowerUpType.SpeedUp => "SPEED UP",
            PowerUpType.Detonator => "DETONATOR",
            PowerUpType.BombPass => "BOMB PASS",
            PowerUpType.WallPass => "WALL PASS",
            PowerUpType.FlamePass => "FLAME PASS",
            PowerUpType.Mystery => "MYSTERY",
            _ => type.ToString().ToUpperInvariant()
        };
    }
}
