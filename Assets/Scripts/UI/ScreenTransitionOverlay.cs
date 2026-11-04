using UnityEngine;
using UnityEngine.UI;

public sealed class ScreenTransitionOverlay
{
    private readonly GameObject canvasObject;
    private readonly CanvasGroup canvasGroup;
    private readonly Image backgroundImage;
    private readonly Image irisImage;
    private readonly Text messageText;
    private readonly Material irisMaterial;

    private ScreenTransitionOverlay(
        GameObject canvasObject,
        CanvasGroup canvasGroup,
        Image backgroundImage,
        Image irisImage,
        Text messageText,
        Material irisMaterial)
    {
        this.canvasObject = canvasObject;
        this.canvasGroup = canvasGroup;
        this.backgroundImage = backgroundImage;
        this.irisImage = irisImage;
        this.messageText = messageText;
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

        canvasObject.SetActive(false);

        return new ScreenTransitionOverlay(
            canvasObject,
            canvasGroup,
            backgroundImage,
            irisImage,
            messageText,
            irisMaterial);
    }

    public void Show(string message, float alpha)
    {
        messageText.text = message;
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
