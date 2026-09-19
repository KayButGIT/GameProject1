using UnityEngine;

public sealed class StageHud
{
    private readonly GameObject canvasObject;
    private readonly UnityEngine.UI.Text timeText;
    private int shownSeconds = -1;

    private StageHud(GameObject canvasObject, UnityEngine.UI.Text timeText)
    {
        this.canvasObject = canvasObject;
        this.timeText = timeText;
    }

    public static StageHud Create()
    {
        GameObject canvasObject = new("Stage HUD Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();

        GameObject textObject = new("Time Text");
        textObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text text = textObject.AddComponent<UnityEngine.UI.Text>();
        text.alignment = TextAnchor.UpperCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.color = Color.white;

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = new Vector2(0f, 1f);
        textTransform.anchorMax = Vector2.one;
        textTransform.pivot = new Vector2(0.5f, 1f);
        textTransform.offsetMin = new Vector2(0f, -60f);
        textTransform.offsetMax = new Vector2(0f, -12f);

        return new StageHud(canvasObject, text);
    }

    public void SetTime(float seconds)
    {
        int wholeSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        if (wholeSeconds == shownSeconds)
        {
            return;
        }

        shownSeconds = wholeSeconds;
        timeText.text = $"TIME {wholeSeconds}";
    }

    public void SetVisible(bool visible)
    {
        canvasObject.SetActive(visible);
    }
}
