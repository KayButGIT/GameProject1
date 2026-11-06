using UnityEngine;
using UnityEngine.UI;

public sealed class ScreenTransitionOverlay
{
    private readonly GameObject canvasObject;
    private readonly CanvasGroup canvasGroup;
    private readonly Text messageText;

    private ScreenTransitionOverlay(GameObject canvasObject, CanvasGroup canvasGroup, Text messageText)
    {
        this.canvasObject = canvasObject;
        this.canvasGroup = canvasGroup;
        this.messageText = messageText;
    }

    public static ScreenTransitionOverlay Create()
    {
        GameObject canvasObject = new("Transition Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        canvasObject.AddComponent<CanvasScaler>();
        CanvasGroup canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        GameObject backgroundObject = new("Transition Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = Color.black;
        StretchToParent(background.rectTransform);

        GameObject textObject = new("Transition Text");
        textObject.transform.SetParent(canvasObject.transform, false);
        Text text = textObject.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        StretchToParent(text.rectTransform);

        canvasObject.SetActive(false);
        return new ScreenTransitionOverlay(canvasObject, canvasGroup, text);
    }

    public void Show(string message, float alpha)
    {
        messageText.text = message;
        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasObject.SetActive(true);
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void Hide()
    {
        canvasObject.SetActive(false);
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
