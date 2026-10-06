using UnityEngine;

// This is your real StageHud.cs (confirmed byte-for-byte against the file you sent) with Score and
// Stage support added. Everything else — the "Stage HUD Canvas" name, Time Text created before
// Lives Text, SetVisible/SetTime/SetLives — is untouched, so every existing smoke test
// (StageEnemiesPlaySmoke's GetComponentInChildren<Text>() lookup included) keeps passing.
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

        // Score, under Lives. Created after Time/Lives so child order for the existing smoke
        // tests (which only look at the first Text) never changes.
        GameObject scoreObject = new("Score Text");
        scoreObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text score = scoreObject.AddComponent<UnityEngine.UI.Text>();
        score.alignment = TextAnchor.UpperLeft;
        score.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        score.fontSize = 36;
        score.color = Color.white;

        RectTransform scoreTransform = scoreObject.GetComponent<RectTransform>();
        scoreTransform.anchorMin = new Vector2(0f, 1f);
        scoreTransform.anchorMax = new Vector2(0f, 1f);
        scoreTransform.pivot = new Vector2(0f, 1f);
        scoreTransform.anchoredPosition = new Vector2(24f, -64f);
        scoreTransform.sizeDelta = new Vector2(260f, 48f);

        // Stage, under Score.
        GameObject stageObject = new("Stage Text");
        stageObject.transform.SetParent(canvasObject.transform);

        UnityEngine.UI.Text stage = stageObject.AddComponent<UnityEngine.UI.Text>();
        stage.alignment = TextAnchor.UpperLeft;
        stage.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        stage.fontSize = 36;
        stage.color = Color.white;

        RectTransform stageTransform = stageObject.GetComponent<RectTransform>();
        stageTransform.anchorMin = new Vector2(0f, 1f);
        stageTransform.anchorMax = new Vector2(0f, 1f);
        stageTransform.pivot = new Vector2(0f, 1f);
        stageTransform.anchoredPosition = new Vector2(24f, -116f);
        stageTransform.sizeDelta = new Vector2(200f, 48f);

        return new StageHud(canvasObject, text, lives, score, stage);
    }

    public void SetLives(int lives)
    {
        if (lives == shownLives) return;
        shownLives = lives;
        livesText.text = $"LEFT {Mathf.Max(0, lives)}";
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
        stageText.text = $"STAGE {stage}";
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

    // Hides only the Time row, so Score/Stage/Left still show on stages with no time limit.
    public void SetTimeVisible(bool visible)
    {
        timeText.gameObject.SetActive(visible);
    }

    public void SetVisible(bool visible)
    {
        canvasObject.SetActive(visible);
    }
}