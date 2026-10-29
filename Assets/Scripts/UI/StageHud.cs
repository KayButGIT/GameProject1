using UnityEngine;

// Builds the screen-space status bar used during gameplay: Time, Score, Stage, and Life.
public sealed class StageHud
{
    private readonly GameObject canvasObject;
    private readonly UnityEngine.UI.Text timeText;
    private readonly UnityEngine.UI.Text livesText;
    private readonly UnityEngine.UI.Text scoreText;
    private readonly UnityEngine.UI.Text stageText;
    private int shownSeconds = -1;
    private int shownLives = -1;
    private int shownScore = -1;
    private int shownStage = -1;

    private StageHud(GameObject canvasObject, UnityEngine.UI.Text timeText, UnityEngine.UI.Text livesText,
        UnityEngine.UI.Text scoreText, UnityEngine.UI.Text stageText)
    {
        this.canvasObject = canvasObject;
        this.timeText = timeText;
        this.livesText = livesText;
        this.scoreText = scoreText;
        this.stageText = stageText;
    }

    public static StageHud Create()
    {
        GameObject canvasObject = new("Stage HUD Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new("Time Text");
        textObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text text = textObject.AddComponent<UnityEngine.UI.Text>();
        text.alignment = TextAnchor.UpperLeft;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 44;
        text.color = Color.white;
        AddBlackOutline(text);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = new Vector2(0f, 1f);
        textTransform.anchorMax = new Vector2(0.3f, 1f);
        textTransform.pivot = new Vector2(0f, 1f);
        textTransform.anchoredPosition = new Vector2(28f, -14f);
        textTransform.offsetMin = new Vector2(28f, -86f);
        textTransform.offsetMax = new Vector2(-8f, -14f);

        // Created after the timer, so GetComponentInChildren<Text> still finds the timer first.
        GameObject livesObject = new("Lives Text");
        livesObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text lives = livesObject.AddComponent<UnityEngine.UI.Text>();
        lives.alignment = TextAnchor.UpperRight;
        lives.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lives.fontSize = 44;
        lives.color = Color.white;
        AddBlackOutline(lives);

        RectTransform livesTransform = livesObject.GetComponent<RectTransform>();
        livesTransform.anchorMin = new Vector2(0.7f, 1f);
        livesTransform.anchorMax = new Vector2(1f, 1f);
        livesTransform.pivot = new Vector2(1f, 1f);
        livesTransform.anchoredPosition = new Vector2(-28f, -14f);
        livesTransform.offsetMin = new Vector2(8f, -86f);
        livesTransform.offsetMax = new Vector2(-28f, -14f);

        // Score is centered in the status bar between time and lives.
        GameObject scoreObject = new("Score Text");
        scoreObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text score = scoreObject.AddComponent<UnityEngine.UI.Text>();
        score.alignment = TextAnchor.UpperCenter;
        score.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        score.fontSize = 44;
        score.color = Color.white;
        AddBlackOutline(score);

        RectTransform scoreTransform = scoreObject.GetComponent<RectTransform>();
        scoreTransform.anchorMin = new Vector2(0.3f, 1f);
        scoreTransform.anchorMax = new Vector2(0.7f, 1f);
        scoreTransform.pivot = new Vector2(0.5f, 1f);
        scoreTransform.anchoredPosition = new Vector2(0f, -14f);
        scoreTransform.offsetMin = new Vector2(8f, -86f);
        scoreTransform.offsetMax = new Vector2(-8f, -14f);

        // Stage sits centered under the score, so the top row keeps Time, Score, and Life.
        GameObject stageObject = new("Stage Text");
        stageObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text stage = stageObject.AddComponent<UnityEngine.UI.Text>();
        stage.alignment = TextAnchor.UpperCenter;
        stage.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stage.fontSize = 36;
        stage.color = Color.white;
        AddBlackOutline(stage);

        RectTransform stageTransform = stageObject.GetComponent<RectTransform>();
        stageTransform.anchorMin = new Vector2(0.3f, 1f);
        stageTransform.anchorMax = new Vector2(0.7f, 1f);
        stageTransform.pivot = new Vector2(0.5f, 1f);
        stageTransform.anchoredPosition = new Vector2(0f, -64f);
        stageTransform.offsetMin = new Vector2(8f, -120f);
        stageTransform.offsetMax = new Vector2(-8f, -64f);

        return new StageHud(canvasObject, text, lives, score, stage);
    }

    public void SetLives(int lives)
    {
        if (lives == shownLives) return;
        shownLives = lives;
        livesText.text = $"LIFE {Mathf.Max(0, lives)}";
    }

    public void SetScore(int score)
    {
        if (score == shownScore) return;
        shownScore = score;
        scoreText.text = $"SCORE {Mathf.Max(0, score)}";
    }

    public void SetStage(int stage)
    {
        if (stage == shownStage) return;
        shownStage = stage;
        stageText.text = $"STAGE {Mathf.Max(1, stage)}";
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

    // Hides only the Time row, so Score/Stage/Life still show on stages with no time limit.
    public void SetTimeVisible(bool visible)
    {
        timeText.gameObject.SetActive(visible);
    }

    public void SetVisible(bool visible)
    {
        canvasObject.SetActive(visible);
    }

    private static void AddBlackOutline(UnityEngine.UI.Text text)
    {
        UnityEngine.UI.Outline outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
    }
}