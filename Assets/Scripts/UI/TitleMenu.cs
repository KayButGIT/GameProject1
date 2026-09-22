using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen buttons. Wire Start, Continue, and Exit to these methods in each Button's On Click list.
public sealed class TitleMenu : MonoBehaviour
{
    [Tooltip("Scene that runs the game. It must be in Build Settings.")]
    [SerializeField] private string gameSceneName = "Prototype";
    [Tooltip("Continue button. It is disabled while no stage has been saved.")]
    [SerializeField] private Button continueButton;
    [Tooltip("Optional label on the Continue button that shows the saved stage.")]
    [SerializeField] private Text continueLabel;
    [Tooltip("Text of the Continue label. {0} is the saved stage number.")]
    [SerializeField] private string continueLabelFormat = "Continue (Stage {0})";

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
        GameProgress.Clear();
        GameProgress.RequestedStage = 1;
        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        if (!GameProgress.HasSave) return;
        GameProgress.RequestedStage = GameProgress.SavedStage;
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }
}
