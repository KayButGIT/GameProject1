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
    [SerializeField, Min(0f)] private float preLoadFadeSeconds = 0.8f;
    [SerializeField] private Transform titlePlayer;
    [SerializeField] private Material burntBodyMaterial;
    [SerializeField] private Material burntFaceMaterial;
    [SerializeField] private Material irisTransitionMaterial;

    [Header("Iris Transition")]
    [SerializeField, Min(0.5f)] private float irisStartRadius = 1.5f;
    [SerializeField, Min(0.05f)] private float irisPauseRadius = 0.25f;
    [SerializeField, Min(0.05f)] private float irisCloseToPauseSeconds = 0.35f;
    [SerializeField, Min(0f)] private float irisHoldSeconds = 1f;
    [SerializeField, Min(0.05f)] private float irisFinalCloseSeconds = 0.75f;
    [SerializeField] private float irisCenterHeight = 1.2f;
    [SerializeField] private float irisExitCenterHeight = 0.6f;

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
        transitionOverlay = ScreenTransitionOverlay.Create(irisTransitionMaterial);
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
            continueLabel.text = string.Format(
                continueLabelFormat,
                GameProgress.SavedStage);

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
        yield return FadeToBlackRealtime(preLoadFadeSeconds, irisCenterHeight);

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
        yield return FadeToBlackRealtime(
    preLoadFadeSeconds,
    irisCenterHeight);

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
        yield return FadeToBlackRealtime(
    preLoadFadeSeconds,
    irisExitCenterHeight);
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
    private IEnumerator FadeToBlackRealtime(
        float seconds,
        float centerHeight)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null || titlePlayer == null)
        {
            transitionOverlay.Show("", 1f);
            yield break;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(
            titlePlayer.position + Vector3.up * centerHeight);

        Vector2 center = new(
            screenPosition.x / Screen.width,
            screenPosition.y / Screen.height);

        // Start large enough that the screen is completely visible.
        transitionOverlay.ShowIris(center, irisStartRadius);

        // Slowly close inward to the pause size.
        float elapsed = 0f;

        while (elapsed < irisCloseToPauseSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / irisCloseToPauseSeconds);

            float radius = Mathf.Lerp(
                irisStartRadius,
                irisPauseRadius,
                progress);

            transitionOverlay.SetIris(center, radius);

            yield return null;
        }

        // Pause around the player.
        transitionOverlay.SetIris(center, irisPauseRadius);
        yield return new WaitForSecondsRealtime(irisHoldSeconds);

        // Slowly continue closing to black.
        elapsed = 0f;

        while (elapsed < irisFinalCloseSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / irisFinalCloseSeconds);

            float radius = Mathf.Lerp(
                irisPauseRadius,
                0f,
                progress);

            transitionOverlay.SetIris(center, radius);

            yield return null;
        }

        transitionOverlay.SetIris(center, 0f);
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
