using UnityEngine;
using UnityEngine.UI;

public sealed class ScreenTransitionOverlay
{
    private readonly GameObject canvasObject;
    private readonly CanvasGroup canvasGroup;
    private readonly Image backgroundImage;
    private readonly Image irisImage;
    private readonly Text messageText;
    private readonly Text scoreLabelsText;
    private readonly Text scoreValuesText;
    private readonly Material irisMaterial;

    private ScreenTransitionOverlay(
        GameObject canvasObject,
        CanvasGroup canvasGroup,
        Image backgroundImage,
        Image irisImage,
        Text messageText,
        Text scoreLabelsText,
        Text scoreValuesText,
        Material irisMaterial)
    {
        this.canvasObject = canvasObject;
        this.canvasGroup = canvasGroup;
        this.backgroundImage = backgroundImage;
        this.irisImage = irisImage;
        this.messageText = messageText;
        this.scoreLabelsText = scoreLabelsText;
        this.scoreValuesText = scoreValuesText;
        this.irisMaterial = irisMaterial;
    }

    public static ScreenTransitionOverlay Create()
    {
        return Create(null);
    }

    public static ScreenTransitionOverlay Create(Material irisTransitionMaterial)
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

        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = Color.black;
        StretchToParent(backgroundImage.rectTransform);

        Image irisImage = null;
        Material irisMaterial = null;

        if (irisTransitionMaterial != null)
        {
            GameObject irisObject = new("Iris Transition");
            irisObject.transform.SetParent(canvasObject.transform, false);

            irisImage = irisObject.AddComponent<Image>();
            irisImage.raycastTarget = false;
            StretchToParent(irisImage.rectTransform);

            irisMaterial = new Material(irisTransitionMaterial);
            irisImage.material = irisMaterial;
            irisImage.enabled = false;
        }

        GameObject textObject = new("Transition Text");
        textObject.transform.SetParent(canvasObject.transform, false);

        Text messageText = textObject.AddComponent<Text>();
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = 64;
        messageText.fontStyle = FontStyle.Bold;
        messageText.color = Color.white;
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Overflow;
        StretchToParent(messageText.rectTransform);

        Text scoreLabelsText = CreateScoreColumn(
            canvasObject.transform,
            messageText.font,
            TextAnchor.MiddleRight,
            new Vector2(0.5f, 0.5f),
            new Vector2(-120f, -100f));
        Text scoreValuesText = CreateScoreColumn(
            canvasObject.transform,
            messageText.font,
            TextAnchor.MiddleLeft,
            new Vector2(0.5f, 0.5f),
            new Vector2(120f, -100f));

        canvasObject.SetActive(false);

        return new ScreenTransitionOverlay(
            canvasObject,
            canvasGroup,
            backgroundImage,
            irisImage,
            messageText,
            scoreLabelsText,
            scoreValuesText,
            irisMaterial);
    }

    public void Show(string message, float alpha, string scoreSummary = null)
    {
        messageText.text = message;
        SetScoreSummary(scoreSummary);
        SetMessageAlpha(1f);
        backgroundImage.enabled = true;

        if (irisImage != null)
        {
            irisImage.enabled = false;
        }

        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasObject.SetActive(true);
    }

    public void ShowIris(Vector2 center, float radius)
    {
        if (irisImage == null || irisMaterial == null)
        {
            return;
        }

        backgroundImage.enabled = false;
        irisImage.enabled = true;
        SetScoreSummary(null);
        canvasGroup.alpha = 1f;
        canvasObject.SetActive(true);

        SetIris(center, radius);
    }

    public void SetIris(Vector2 center, float radius)
    {
        if (irisMaterial == null)
        {
            return;
        }

        irisMaterial.SetVector(
            "_Center",
            new Vector4(center.x, center.y, 0f, 0f));

        irisMaterial.SetFloat("_Radius", radius);
    }

    public void SetAlpha(float alpha)
    {
        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void SetMessageAlpha(float alpha)
    {
        Color color = messageText.color;
        color.a = Mathf.Clamp01(alpha);
        messageText.color = color;
        scoreLabelsText.color = color;
        scoreValuesText.color = color;
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

    private void SetScoreSummary(string scoreSummary)
    {
        if (string.IsNullOrEmpty(scoreSummary))
        {
            scoreLabelsText.text = string.Empty;
            scoreValuesText.text = string.Empty;
            return;
        }

        string[] lines = scoreSummary.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        System.Text.StringBuilder labels = new();
        System.Text.StringBuilder values = new();
        foreach (string line in lines)
        {
            int separator = line.LastIndexOf(' ');
            if (separator <= 0)
            {
                labels.AppendLine(line);
                values.AppendLine();
                continue;
            }

            labels.AppendLine(line.Substring(0, separator));
            values.AppendLine(line.Substring(separator + 1));
        }

        scoreLabelsText.text = labels.ToString();
        scoreValuesText.text = values.ToString();
    }

    private static Text CreateScoreColumn(
        Transform parent,
        Font font,
        TextAnchor alignment,
        Vector2 anchor,
        Vector2 position)
    {
        GameObject scoreObject = new("Ending Score Column");
        scoreObject.transform.SetParent(parent, false);
        Text text = scoreObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 34;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(220f, 88f);
        rect.anchoredPosition = position;
        return text;
    }
}
