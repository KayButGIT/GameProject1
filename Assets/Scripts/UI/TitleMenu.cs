using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen buttons. Wire Start, Continue, and Exit to these methods in each Button's On Click list.
public sealed class TitleMenu : MonoBehaviour
{
    private const float SelectionAnimationSeconds = 1.2f;
    private static readonly int SelectTrigger = Animator.StringToHash("Select");

    [Tooltip("Scene that runs the game. It must be in Build Settings.")]
    [SerializeField] private string gameSceneName = "Prototype";
    [Tooltip("Continue button. It is disabled while no stage has been saved.")]
    [SerializeField] private Button continueButton;
    [Tooltip("Optional label on the Continue button that shows the saved stage.")]
    [SerializeField] private Text continueLabel;
    [Tooltip("Text of the Continue label. {0} is the saved stage number.")]
    [SerializeField] private string continueLabelFormat = "Continue (Stage {0})";
    [Tooltip("Seconds for the short fade-to-black before loading the game scene.")]
    [SerializeField, Min(0f)] private float preLoadFadeSeconds = 0.5f;

    private Animator titleAnimator;
    private bool actionPending;
    private ScreenTransitionOverlay transitionOverlay;

    private void Awake()
    {
        titleAnimator = FindFirstObjectByType<Animator>();
        // Create or reuse the transition overlay; start hidden and transparent.
        transitionOverlay = ScreenTransitionOverlay.Create();
        transitionOverlay.SetAlpha(0f);
        transitionOverlay.Hide();
    }

    private void Start()
    {
        bool hasSave = GameProgress.HasSave;
        if (continueButton != null)
        {
            continueButton.interactable = hasSave;
        }

        if (continueLabel != null && hasSave)
        {
            continueLabel.text = string.Format(continueLabelFormat, GameProgress.SavedStage);
        }
    }

    public void StartNewGame()
    {
        if (!BeginButtonAction()) return;
        StartCoroutine(StartNewGameAfterSelection());
    }

    private IEnumerator StartNewGameAfterSelection()
    {
        yield return new WaitForSecondsRealtime(SelectionAnimationSeconds);
        // short fade to black, then load scene so the stage transition in the game can take over
        transitionOverlay.Show("", 0f);
        yield return FadeToBlackRealtime(preLoadFadeSeconds);

        GameProgress.Clear();
        GameProgress.RequestedStage = 1;
        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        if (!GameProgress.HasSave) return;
        if (!BeginButtonAction()) return;
        StartCoroutine(ContinueGameAfterSelection());
    }

    private IEnumerator ContinueGameAfterSelection()
    {
        yield return new WaitForSecondsRealtime(SelectionAnimationSeconds);
        transitionOverlay.Show("", 0f);
        yield return FadeToBlackRealtime(preLoadFadeSeconds);

        GameProgress.RequestedStage = GameProgress.SavedStage;
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        if (!BeginButtonAction()) return;
        StartCoroutine(QuitAfterSelection());
    }

    private IEnumerator QuitAfterSelection()
    {
        yield return new WaitForSecondsRealtime(SelectionAnimationSeconds);
        transitionOverlay.Show("", 0f);
        yield return FadeToBlackRealtime(preLoadFadeSeconds);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }

    private bool BeginButtonAction()
    {
        if (actionPending) return false;
        actionPending = true;
        if (titleAnimator != null) titleAnimator.SetTrigger(SelectTrigger);
        return true;
    }

    // Short unscaled fade to black.
    private IEnumerator FadeToBlackRealtime(float seconds)
    {
        float elapsed = 0f;
        transitionOverlay.SetAlpha(0f);
        transitionOverlay.Show("", 0f);
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, seconds));
            transitionOverlay.SetAlpha(Mathf.Lerp(0f, 1f, t));
            yield return null;
        }
        transitionOverlay.SetAlpha(1f);
    }
}
