using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen buttons. Wire Start, Continue, and Exit to these methods in each Button's On Click list.
public sealed class TitleMenu : MonoBehaviour
{
    private const float SelectionAnimationSeconds = 1.2f;
    private static readonly int SelectTrigger = Animator.StringToHash("Select");
    private static readonly int DeadTrigger = Animator.StringToHash("Dead");

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
    [SerializeField] private Transform titlePlayer;
    [SerializeField] private Material burntBodyMaterial;
    [SerializeField] private Material burntFaceMaterial;

    private Animator titleAnimator;
    private bool actionPending;
    private ScreenTransitionOverlay transitionOverlay;
    private Renderer titleBodyRenderer;
    private Renderer titleHeadRenderer;
    private int titleBodyMaterialIndex = -1;
    private int titleFaceMaterialIndex = -1;
    private bool keepBurntMaterialsApplied;

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
        SoundManager.Instance?.PlayTitleScreenBgm();
        bool hasSave = GameProgress.HasSave;
        if (continueButton != null)
        {
            continueButton.interactable = hasSave;
        }

        if (continueLabel != null && hasSave)
        {
            continueLabel.text = string.Format(continueLabelFormat, GameProgress.SavedStage);
            continueLabel.fontSize = 30;
        }

        CacheTitlePlayerRenderers();
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
        if (!BeginButtonAction(true))
        {
            return;
        }

        PlayExitExplosion();

        keepBurntMaterialsApplied = true;
        ApplyBurntDeathMaterials();

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

    private bool BeginButtonAction(bool useDeadAnimation = false)
    {
        if (actionPending) return false;

        actionPending = true;

        if (titleAnimator != null)
        {
            titleAnimator.ResetTrigger(SelectTrigger);
            titleAnimator.ResetTrigger(DeadTrigger);
            titleAnimator.SetTrigger(useDeadAnimation ? DeadTrigger : SelectTrigger);
        }

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

    private void PlayExitExplosion()
    {
        if (titlePlayer == null)
        {
            Debug.LogWarning("Title Player is not assigned on TitleMenu.", this);
            return;
        }

        Vector3 explosionPosition = titlePlayer.position;

        BlastEffects.CreateFire(
            "Title Exit Explosion",
            explosionPosition,
            0);

        ExplosionFlash.Create(
            explosionPosition + Vector3.up,
            new Color(1f, 0.42f, 0.1f),
            3.5f,
            2.5f,
            transform);
    }

    private void CacheTitlePlayerRenderers()
    {
        if (titlePlayer == null)
        {
            Debug.LogWarning("Title Player is not assigned.", this);
            return;
        }

        Transform body = FindChild(titlePlayer, "Body");
        Transform head = FindChild(titlePlayer, "Head");

        titleBodyRenderer = body != null
            ? body.GetComponentInChildren<Renderer>(true)
            : null;

        titleHeadRenderer = head != null
            ? head.GetComponentInChildren<Renderer>(true)
            : null;

        titleBodyMaterialIndex = titleBodyRenderer != null ? 0 : -1;
        titleFaceMaterialIndex = titleHeadRenderer != null ? 1 : -1;

        Debug.Log(
            $"Title materials cached. Body renderer: {titleBodyRenderer}, Head renderer: {titleHeadRenderer}, Face slot: {titleFaceMaterialIndex}",
            this);
    }

    private IEnumerator ApplyBurntMaterialsDuringDeath()
    {
        float elapsed = 0f;

        while (elapsed < SelectionAnimationSeconds)
        {
            yield return new WaitForEndOfFrame();

            ApplyBurntDeathMaterials();
            elapsed += Time.unscaledDeltaTime;
        }
    }

    private void ApplyBurntDeathMaterials()
    {
        if (titleBodyRenderer != null && burntBodyMaterial != null)
        {
            Material[] bodyMaterials = titleBodyRenderer.materials;

            if (bodyMaterials.Length > 0)
            {
                bodyMaterials[0] = burntBodyMaterial;
                titleBodyRenderer.materials = bodyMaterials;
            }
        }

        if (titleHeadRenderer == null)
        {
            return;
        }

        Material[] headMaterials = titleHeadRenderer.materials;

        if (headMaterials.Length > 0 && burntBodyMaterial != null)
        {
            // Head Element 0 is the head/body surface.
            headMaterials[0] = burntBodyMaterial;
        }

        if (headMaterials.Length > 1 && burntFaceMaterial != null)
        {
            // Head Element 1 is the visible face.
            headMaterials[1] = burntFaceMaterial;
        }

        titleHeadRenderer.materials = headMaterials;
    }

    private static void SetRendererMaterial(
        Renderer renderer,
        int materialIndex,
        Material material)
    {
        Material[] materials = renderer.materials;

        if (materialIndex < 0 || materialIndex >= materials.Length)
        {
            return;
        }

        materials[materialIndex] = material;
        renderer.materials = materials;
    }

    private static int FindMaterialIndex(
        Renderer renderer,
        string materialName,
        int fallbackIndex)
    {
        Material[] materials = renderer.sharedMaterials;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null &&
                materials[i].name.ToLowerInvariant().Contains(materialName))
            {
                return i;
            }
        }

        return fallbackIndex < materials.Length ? fallbackIndex : -1;
    }

    private static Transform FindChild(
        Transform root,
        string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Equals(
                childName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private void LateUpdate()
    {
        if (keepBurntMaterialsApplied)
        {
            ApplyBurntDeathMaterials();
        }
    }
}
