using UnityEngine;

public sealed class StageHud
{
    private readonly GameObject canvasObject;
    private readonly UnityEngine.UI.Text timeText;
    private readonly UnityEngine.UI.Text livesText;
    private int shownSeconds = -1;
    private int shownLives = -1;

    private StageHud(GameObject canvasObject, UnityEngine.UI.Text timeText, UnityEngine.UI.Text livesText)
    {
        this.canvasObject = canvasObject;
        this.timeText = timeText;
        this.livesText = livesText;
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

        // Created after the timer, so GetComponentInChildren<Text> still finds the timer first.
        GameObject livesObject = new("Lives Text");
        livesObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text lives = livesObject.AddComponent<UnityEngine.UI.Text>();
        lives.alignment = TextAnchor.UpperLeft;
        lives.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lives.fontSize = 36;
        lives.color = Color.white;

        RectTransform livesTransform = livesObject.GetComponent<RectTransform>();
        livesTransform.anchorMin = new Vector2(0f, 1f);
        livesTransform.anchorMax = new Vector2(0f, 1f);
        livesTransform.pivot = new Vector2(0f, 1f);
        livesTransform.anchoredPosition = new Vector2(24f, -12f);
        livesTransform.sizeDelta = new Vector2(200f, 48f);

        return new StageHud(canvasObject, text, lives);
    }

    public void SetLives(int lives)
    {
        if (lives == shownLives) return;
        shownLives = lives;
        livesText.text = $"LEFT {Mathf.Max(0, lives)}";
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
