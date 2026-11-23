using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class PauseMenu
{
    private const int PauseSortingOrder = 10;
    private const int ConfirmationSortingOrder = 30;
    private readonly Canvas canvas;
    private readonly GameObject panel;
    private readonly GameObject menuContent;
    private readonly GameObject confirmation;
    private readonly Button returnButton;
    private readonly Button cancelButton;

    private PauseMenu(Canvas canvas, GameObject panel, GameObject menuContent, GameObject confirmation,
        Button returnButton, Button cancelButton)
    {
        this.canvas = canvas;
        this.panel = panel;
        this.menuContent = menuContent;
        this.confirmation = confirmation;
        this.returnButton = returnButton;
        this.cancelButton = cancelButton;
    }

    public static PauseMenu Create(UnityAction returnToTitle, bool canReturnToTitle)
    {
        GameObject canvasObject = new("Pause Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = PauseSortingOrder;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new("Pause Panel");
        panel.transform.SetParent(canvasObject.transform, false);

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);
        StretchToParent(image.rectTransform);

        GameObject menuContent = new("Pause Menu Content", typeof(RectTransform));
        menuContent.transform.SetParent(panel.transform, false);
        StretchToParent((RectTransform)menuContent.transform);

        Text message = CreateText(menuContent.transform, "Pause Text", "PAUSED\nEsc to resume", 42);
        message.rectTransform.sizeDelta = new Vector2(420f, 120f);
        message.rectTransform.anchoredPosition = new Vector2(0f, 42f);

        Button returnButton = CreateButton(menuContent.transform, "Return to Title Button", "Return to Title",
            new Vector2(0f, -76f), 280f, canReturnToTitle);

        GameObject confirmation = new("Return to Title Confirmation", typeof(RectTransform));
        confirmation.transform.SetParent(panel.transform, false);
        Image dialog = confirmation.AddComponent<Image>();
        dialog.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        dialog.rectTransform.sizeDelta = new Vector2(660f, 300f);
        dialog.rectTransform.anchoredPosition = new Vector2(0f, 88f);

        Text heading = CreateText(confirmation.transform, "Confirmation Heading", "Return to Title?", 38);
        heading.fontStyle = FontStyle.Bold;
        heading.rectTransform.sizeDelta = new Vector2(600f, 54f);
        heading.rectTransform.anchoredPosition = new Vector2(0f, 90f);

        Text warning = CreateText(confirmation.transform, "Confirmation Warning",
            "Progress made in this stage will be lost.\nContinue will load your saved checkpoint.", 28);
        warning.rectTransform.sizeDelta = new Vector2(580f, 110f);
        warning.rectTransform.anchoredPosition = new Vector2(0f, 12f);

        Button cancelButton = CreateButton(confirmation.transform, "Cancel Return Button", "Cancel",
            new Vector2(-136f, -88f), 200f);
        Button confirmButton = CreateButton(confirmation.transform, "Confirm Return Button", "Return to Title",
            new Vector2(116f, -88f), 260f);
        cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = confirmButton };
        confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = cancelButton };

        confirmation.SetActive(false);
        panel.SetActive(false);
        PauseMenu menu = new(canvas, panel, menuContent, confirmation, returnButton, cancelButton);
        returnButton.onClick.AddListener(menu.ShowConfirmation);
        cancelButton.onClick.AddListener(() => menu.TryCancelReturnToTitle());
        confirmButton.onClick.AddListener(returnToTitle);
        return menu;
    }

    public void SetVisible(bool visible)
    {
        panel.SetActive(visible);
        if (!visible) TryCancelReturnToTitle();
    }

    public bool TryCancelReturnToTitle()
    {
        if (!confirmation.activeSelf) return false;

        confirmation.SetActive(false);
        menuContent.SetActive(true);
        canvas.sortingOrder = PauseSortingOrder;
        if (panel.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(returnButton.gameObject);
        return true;
    }

    private void ShowConfirmation()
    {
        menuContent.SetActive(false);
        canvas.sortingOrder = ConfirmationSortingOrder;
        confirmation.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(cancelButton.gameObject);
    }

    private static Button CreateButton(Transform parent, string name, string caption, Vector2 position, float width,
        bool interactable = true)
    {
        GameObject buttonObject = new(name, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.16f, 1f);
        image.rectTransform.sizeDelta = new Vector2(width, 64f);
        image.rectTransform.anchoredPosition = position;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;
        Text label = CreateText(buttonObject.transform, "Label", caption, 32);
        label.color = interactable ? Color.white : Color.gray;
        StretchToParent(label.rectTransform);
        return button;
    }

    private static Text CreateText(Transform parent, string name, string message, int fontSize)
    {
        GameObject textObject = new(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.text = message;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
