using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerController : GridController
{
    private BombermanPrototype game;
    private BombermanMap map;
    private PlayerAnimationBridge animationBridge;
    private Renderer faceRenderer;
    private Renderer headRenderer;
    private int bodyMaterialIndex = -1;
    private int faceMaterialIndex = -1;
    private Material bodyNormalMaterial;
    private Material bodyBurntMaterial;
    private Material bodyInvincibleMaterial;
    private Material faceNormalMaterial;
    private Material faceBlinkMaterial;
    private Material faceInvincibleMaterial;
    private Material faceDeadMaterial;
    private Material faceDeadBurntMaterial;
    private Material bodyFireDissolveMaterial;
    private Material faceFireDissolveMaterial;
    private float burnDissolveStart;
    private float burnDissolveEnd = 1f;
    private Vector2 moveInput;
    private float blinkTimer = BlinkInterval;
    private float blinkDurationTimer;
    private bool invincibleVisualActive;
    private Renderer[] burnRenderers;
    private Material[] burnMaterials;
    private Material[][] originalMaterials;

    private const float BlinkInterval = 3f;
    private const float BlinkDuration = 0.14f;
    private const float InputAcceleration = 12f;
    private const float InputDeceleration = 18f;
    public override bool IsPlayer => true;

    // Wall Pass power-up: walk through breakable blocks. BombermanPrototype turns it on and off.
    public bool WallPass { get; set; }
    public override bool CanPassDestructibleWalls => WallPass;

    public static PlayerController Create(
        string actorName,
        Vector2Int cell,
        BombermanMap map,
        Material normalMaterial,
        Material deadMaterial,
        float scale,
        float colliderRadius,
        float colliderHeight,
        GameObject modelPrefab,
        float modelScale,
        RuntimeAnimatorController animatorController,
        Material bodyNormalMaterial,
        Material bodyBurntMaterial,
        Material bodyInvincibleMaterial,
        Material faceNormalMaterial,
        Material faceBlinkMaterial,
        Material faceInvincibleMaterial,
        Material faceDeadMaterial,
        Material faceDeadBurntMaterial,
        Material bodyFireDissolveMaterial,
        Material faceFireDissolveMaterial,
        float burnDissolveStart,
        float burnDissolveEnd)
    {
        PlayerController player = CreateActor<PlayerController>(actorName, cell, map, normalMaterial, deadMaterial, true, scale);
        player.map = map;
        player.ConfigureFreeMovementPhysics(colliderRadius, colliderHeight);
        player.ConfigureVisual(
            modelPrefab,
            modelScale,
            animatorController,
            bodyNormalMaterial,
            bodyBurntMaterial,
            bodyInvincibleMaterial,
            faceNormalMaterial,
            faceBlinkMaterial,
            faceInvincibleMaterial,
            faceDeadMaterial,
            faceDeadBurntMaterial,
            bodyFireDissolveMaterial,
            faceFireDissolveMaterial,
            burnDissolveStart,
            burnDissolveEnd);
        return player;
    }

    public void Initialize(BombermanPrototype game)
    {
        this.game = game;
    }

    protected override void Update()
    {
        if (game == null || game.IsPaused || IsDead)
        {
            moveInput = Vector2.zero;
            return;
        }

        Vector2 targetMoveInput = ReadMove();
        float inputChangeSpeed = targetMoveInput.sqrMagnitude > 0.001f ? InputAcceleration : InputDeceleration;
        moveInput = Vector2.MoveTowards(moveInput, targetMoveInput, inputChangeSpeed * Time.deltaTime);
        UpdateIdleBlink();

        if (WasPressed(Key.Space))
        {
            game.TryDropBomb();
        }
    }

    private void FixedUpdate()
    {
        if (game == null || game.IsPaused || IsDead || map == null)
        {
            return;
        }

        MoveFreely(moveInput, map, game.IsBombBlockingCell);
        animationBridge?.SetMovement(CurrentFreeMoveSpeed, MoveSpeed, game.PlayerSpeedMultiplier);
    }

    public void PlayFootstepFromAnimation()
    {
        if (game == null || game.IsPaused || IsDead || CurrentFreeMoveSpeed <= 0.18f)
            return;

        SoundManager.Instance?.PlayFootstep(game.CurrentStagePhase);
    }

    public void SetInvincibleMaterials(bool active)
    {
        if (IsDead) return;
        if (invincibleVisualActive == active) return;
        invincibleVisualActive = active;
        if (active)
        {
            if (BodyRenderer != null && bodyMaterialIndex >= 0 && bodyInvincibleMaterial != null)
                SetRendererMaterial(BodyRenderer, bodyMaterialIndex, bodyInvincibleMaterial);
            if (headRenderer != null && bodyInvincibleMaterial != null)
                SetNonFaceHeadMaterials(bodyInvincibleMaterial);
            if (faceRenderer != null && faceMaterialIndex >= 0 && faceInvincibleMaterial != null)
                SetRendererMaterial(faceRenderer, faceMaterialIndex, faceInvincibleMaterial);
        }
        else
        {
            ApplyNormalMaterials();
        }
    }

    public override void Die()
    {
        BeginDeathAnimation(false);
    }

    public void BeginDeathAnimation(bool burnt)
    {
        IsDead = true;
        IsMoving = false;
        moveInput = Vector2.zero;
        SetDeathMaterials(burnt);
        animationBridge?.SetDeath();
    }

    public void BeginBurnDissolve()
    {
        if (burnMaterials != null || BodyRenderer == null) return;
        Transform modelRoot = transform;
        burnRenderers = modelRoot.GetComponentsInChildren<Renderer>();
        System.Collections.Generic.List<Material> created = new();
        originalMaterials = new Material[burnRenderers.Length][];
        for (int rendererIndex = 0; rendererIndex < burnRenderers.Length; rendererIndex++)
        {
            Renderer renderer = burnRenderers[rendererIndex];
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
            originalMaterials[rendererIndex] = renderer.sharedMaterials;
            if (!renderer.enabled) continue;
            Material[] replacement = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < replacement.Length; i++)
            {
                Material source = renderer.sharedMaterials[i];
                bool isFace = renderer == faceRenderer && i == faceMaterialIndex;
                Material burnTemplate = isFace ? faceFireDissolveMaterial : bodyFireDissolveMaterial;
                if (source == null) continue;

                Material burn;
                if (burnTemplate != null)
                {
                    burn = new(burnTemplate) { name = burnTemplate.name + " Runtime Burn" };
                    if (burn.HasProperty("_Clipping")) burn.SetFloat("_Clipping", burnDissolveStart);
                }
                else
                {
                    Shader shader = Shader.Find("Bomberman/Player Burn");
                    if (shader == null) continue;
                    burn = new(shader) { name = source.name + " Burn" };
                    burn.SetFloat("_NoiseScale", 7f);
                    burn.SetFloat("_EdgeWidth", 0.18f);
                    burn.SetColor("_EdgeColor", new Color(5f, 0.65f, 0.035f, 1f));
                    burn.SetColor("_CoreColor", new Color(8f, 3.2f, 0.35f, 1f));
                    Texture baseMap = source.mainTexture;
                    if (source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null) baseMap = source.GetTexture("_BaseMap");
                    if (source.HasProperty("_BaseColorMap") && source.GetTexture("_BaseColorMap") != null) baseMap = source.GetTexture("_BaseColorMap");
                    if (source.HasProperty("_ColorMap") && source.GetTexture("_ColorMap") != null) baseMap = source.GetTexture("_ColorMap");
                    Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                    if (baseMap != null) burn.SetTexture("_BaseMap", baseMap);
                    burn.SetColor("_BaseColor", baseColor);
                }
                replacement[i] = burn;
                created.Add(burn);
            }
            if (replacement.Length > 0 && replacement[0] != null) renderer.materials = replacement;
        }
        burnMaterials = created.ToArray();
        if (burnMaterials.Length == 0) { burnMaterials = null; return; }
        SetBurnDissolve(0f);
    }

    public void SetBurnDissolve(float progress)
    {
        if (burnMaterials == null) return;
        progress = Mathf.Clamp01(progress);
        foreach (Material material in burnMaterials)
        {
            float clipping = Mathf.Lerp(burnDissolveStart, burnDissolveEnd, progress);
            if (material.HasProperty("_Clipping")) material.SetFloat("_Clipping", clipping);
            else material.SetFloat("_BurnProgress", clipping);
        }
    }

    public void ClearBurnDissolve()
    {
        if (burnRenderers != null && originalMaterials != null)
            for (int i = 0; i < burnRenderers.Length; i++)
                if (burnRenderers[i] != null && originalMaterials[i] != null) burnRenderers[i].sharedMaterials = originalMaterials[i];
        if (burnMaterials != null)
            foreach (Material material in burnMaterials) if (material != null) Destroy(material);
        burnMaterials = null;
        burnRenderers = null;
        originalMaterials = null;
    }

    private void OnDestroy()
    {
        ClearBurnDissolve();
    }

    private void ConfigureVisual(
        GameObject modelPrefab,
        float modelScale,
        RuntimeAnimatorController animatorController,
        Material bodyNormalMaterial,
        Material bodyBurntMaterial,
        Material bodyInvincibleMaterial,
        Material faceNormalMaterial,
        Material faceBlinkMaterial,
        Material faceInvincibleMaterial,
        Material faceDeadMaterial,
        Material faceDeadBurntMaterial,
        Material bodyFireDissolveMaterial,
        Material faceFireDissolveMaterial,
        float burnDissolveStart,
        float burnDissolveEnd)
    {
        this.bodyNormalMaterial = bodyNormalMaterial;
        this.bodyBurntMaterial = bodyBurntMaterial;
        this.bodyInvincibleMaterial = bodyInvincibleMaterial;
        this.faceNormalMaterial = faceNormalMaterial;
        this.faceBlinkMaterial = faceBlinkMaterial;
        this.faceInvincibleMaterial = faceInvincibleMaterial;
        this.faceDeadMaterial = faceDeadMaterial;
        this.faceDeadBurntMaterial = faceDeadBurntMaterial;
        this.bodyFireDissolveMaterial = bodyFireDissolveMaterial;
        this.faceFireDissolveMaterial = faceFireDissolveMaterial;
        this.burnDissolveStart = Mathf.Clamp01(burnDissolveStart);
        this.burnDissolveEnd = Mathf.Clamp01(burnDissolveEnd);

        Transform visualRoot = transform;
        if (modelPrefab != null)
        {
            RemovePlaceholderVisual();
            BodyRenderer = null;
            faceRenderer = null;
            headRenderer = null;
            bodyMaterialIndex = -1;
            faceMaterialIndex = -1;
            GameObject model = Instantiate(modelPrefab, transform);
            model.name = "Player Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * modelScale;
            visualRoot = model.transform;
            CacheModelRenderers(model);
        }
        else
        {
            faceRenderer = transform.Find("Face Normal Blink Wink Dead Dead Burnt")?.GetComponent<Renderer>();
            bodyMaterialIndex = BodyRenderer != null ? 0 : -1;
            faceMaterialIndex = faceRenderer != null ? 0 : -1;
        }

        ApplyNormalMaterials();

        Animator animator = visualRoot.GetComponentInChildren<Animator>();
        if (animator == null && animatorController != null)
        {
            animator = visualRoot.gameObject.AddComponent<Animator>();
        }

        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            animationBridge = animator.GetComponent<PlayerAnimationBridge>();
            if (animationBridge == null)
                animationBridge = animator.gameObject.AddComponent<PlayerAnimationBridge>();
            animationBridge.Initialize(animator, this);
        }
    }

    private void CacheModelRenderers(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        BodyRenderer = FindRendererByName(renderers, "body");
        headRenderer = FindRendererByName(renderers, "head");

        if (BodyRenderer != null)
        {
            bodyMaterialIndex = FindMaterialIndex(BodyRenderer, "body", 0);
        }

        if (headRenderer != null)
        {
            faceRenderer = headRenderer;
            faceMaterialIndex = FindMaterialIndex(headRenderer, "face", headRenderer.sharedMaterials.Length > 1 ? 1 : 0);
        }

        foreach (Renderer renderer in renderers)
        {
            string rendererName = renderer.name.ToLowerInvariant();
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string materialName = materials[i] != null ? materials[i].name.ToLowerInvariant() : string.Empty;
                if (BodyRenderer == null && (rendererName.Contains("body") || materialName.Contains("body")))
                {
                    BodyRenderer = renderer;
                    bodyMaterialIndex = i;
                }

                if (faceRenderer == null && (rendererName.Contains("face") || materialName.Contains("face")))
                {
                    faceRenderer = renderer;
                    faceMaterialIndex = i;
                }
            }
        }

        if (BodyRenderer == null && renderers.Length > 0)
        {
            BodyRenderer = renderers[0];
            bodyMaterialIndex = 0;
        }

        if (faceRenderer == null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == BodyRenderer)
                {
                    continue;
                }

                faceRenderer = renderer;
                faceMaterialIndex = 0;
                return;
            }

            if (BodyRenderer != null && BodyRenderer.sharedMaterials.Length > 1)
            {
                faceRenderer = BodyRenderer;
                faceMaterialIndex = 1;
            }
        }
    }

    private static Renderer FindRendererByName(Renderer[] renderers, string namePart)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer.name.ToLowerInvariant().Contains(namePart))
            {
                return renderer;
            }
        }

        return null;
    }

    private static int FindMaterialIndex(Renderer renderer, string materialNamePart, int fallbackIndex)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].name.ToLowerInvariant().Contains(materialNamePart))
            {
                return i;
            }
        }

        return fallbackIndex >= 0 && fallbackIndex < materials.Length ? fallbackIndex : -1;
    }

    private void UpdateIdleBlink()
    {
        if (faceRenderer == null || faceMaterialIndex < 0 || faceNormalMaterial == null || faceBlinkMaterial == null)
        {
            return;
        }

        if (invincibleVisualActive)
        {
            blinkTimer = BlinkInterval;
            blinkDurationTimer = 0f;
            if (faceInvincibleMaterial != null)
                SetRendererMaterial(faceRenderer, faceMaterialIndex, faceInvincibleMaterial);
            return;
        }

        if (moveInput.sqrMagnitude > 0.001f)
        {
            blinkTimer = BlinkInterval;
            blinkDurationTimer = 0f;
            SetRendererMaterial(faceRenderer, faceMaterialIndex, faceNormalMaterial);
            return;
        }

        if (blinkDurationTimer > 0f)
        {
            blinkDurationTimer -= Time.deltaTime;
            if (blinkDurationTimer <= 0f)
            {
                SetRendererMaterial(faceRenderer, faceMaterialIndex, faceNormalMaterial);
                blinkTimer = BlinkInterval;
            }
            return;
        }

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            SetRendererMaterial(faceRenderer, faceMaterialIndex, faceBlinkMaterial);
            blinkDurationTimer = BlinkDuration;
        }
    }

    private void ApplyNormalMaterials()
    {
        if (BodyRenderer != null && bodyMaterialIndex >= 0 && bodyNormalMaterial != null)
        {
            SetRendererMaterial(BodyRenderer, bodyMaterialIndex, bodyNormalMaterial);
        }

        if (headRenderer != null && bodyNormalMaterial != null)
        {
            SetNonFaceHeadMaterials(bodyNormalMaterial);
        }

        if (faceRenderer != null && faceMaterialIndex >= 0 && faceNormalMaterial != null)
        {
            SetRendererMaterial(faceRenderer, faceMaterialIndex, faceNormalMaterial);
        }
    }

    private void SetDeathMaterials(bool burnt)
    {
        if (BodyRenderer != null && bodyMaterialIndex >= 0 && burnt && bodyBurntMaterial != null)
        {
            SetRendererMaterial(BodyRenderer, bodyMaterialIndex, bodyBurntMaterial);
        }

        if (headRenderer != null && burnt && bodyBurntMaterial != null)
        {
            SetNonFaceHeadMaterials(bodyBurntMaterial);
        }

        Material faceMaterial = burnt ? faceDeadBurntMaterial : faceDeadMaterial;
        if (faceRenderer != null && faceMaterialIndex >= 0 && faceMaterial != null)
        {
            SetRendererMaterial(faceRenderer, faceMaterialIndex, faceMaterial);
        }
    }

    private void SetNonFaceHeadMaterials(Material material)
    {
        Material[] materials = headRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (i == faceMaterialIndex)
            {
                continue;
            }

            materials[i] = material;
        }

        headRenderer.materials = materials;
    }

    private static void SetRendererMaterial(Renderer renderer, int materialIndex, Material material)
    {
        Material[] materials = renderer.materials;
        if (materialIndex < 0 || materialIndex >= materials.Length)
        {
            return;
        }

        materials[materialIndex] = material;
        renderer.materials = materials;
    }

    private void RemovePlaceholderVisual()
    {
        Transform body = transform.Find("Body");
        if (body != null)
        {
            Destroy(body.gameObject);
        }

        Transform face = transform.Find("Face Normal Blink Wink Dead Dead Burnt");
        if (face != null)
        {
            Destroy(face.gameObject);
        }
    }

    private static Vector2 ReadMove()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 move = Vector2.zero;
        if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
        {
            move.y += 1f;
        }

        if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
        {
            move.y -= 1f;
        }

        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
        {
            move.x -= 1f;
        }

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
        {
            move.x += 1f;
        }

        return move.sqrMagnitude > 1f ? move.normalized : move;
    }

    private static bool WasPressed(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }
}
