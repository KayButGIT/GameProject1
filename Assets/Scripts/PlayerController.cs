using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerController : GridController
{
    private BombermanPrototype game;
    private BombermanMap map;
    private PlayerAnimationBridge animationBridge;
    private Renderer faceRenderer;
    private int bodyMaterialIndex = -1;
    private int faceMaterialIndex = -1;
    private Material bodyNormalMaterial;
    private Material bodyBurntMaterial;
    private Material faceNormalMaterial;
    private Material faceBlinkMaterial;
    private Material faceDeadMaterial;
    private Material faceDeadBurntMaterial;
    private Vector2 moveInput;
    private float blinkTimer = BlinkInterval;
    private float blinkDurationTimer;

    private const float BlinkInterval = 4f;
    private const float BlinkDuration = 0.14f;

    public override bool IsPlayer => true;

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
        Material faceNormalMaterial,
        Material faceBlinkMaterial,
        Material faceDeadMaterial,
        Material faceDeadBurntMaterial)
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
            faceNormalMaterial,
            faceBlinkMaterial,
            faceDeadMaterial,
            faceDeadBurntMaterial);
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

        moveInput = ReadMove();
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
        animationBridge?.SetMovement(CurrentFreeMoveSpeed, MoveSpeed);
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

    private void ConfigureVisual(
        GameObject modelPrefab,
        float modelScale,
        RuntimeAnimatorController animatorController,
        Material bodyNormalMaterial,
        Material bodyBurntMaterial,
        Material faceNormalMaterial,
        Material faceBlinkMaterial,
        Material faceDeadMaterial,
        Material faceDeadBurntMaterial)
    {
        this.bodyNormalMaterial = bodyNormalMaterial;
        this.bodyBurntMaterial = bodyBurntMaterial;
        this.faceNormalMaterial = faceNormalMaterial;
        this.faceBlinkMaterial = faceBlinkMaterial;
        this.faceDeadMaterial = faceDeadMaterial;
        this.faceDeadBurntMaterial = faceDeadBurntMaterial;

        Transform visualRoot = transform;
        if (modelPrefab != null)
        {
            RemovePlaceholderVisual();
            BodyRenderer = null;
            faceRenderer = null;
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
            animationBridge = gameObject.AddComponent<PlayerAnimationBridge>();
            animationBridge.Initialize(animator);
        }
    }

    private void CacheModelRenderers(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
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

    private void UpdateIdleBlink()
    {
        if (faceRenderer == null || faceMaterialIndex < 0 || faceNormalMaterial == null || faceBlinkMaterial == null)
        {
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

        Material faceMaterial = burnt ? faceDeadBurntMaterial : faceDeadMaterial;
        if (faceRenderer != null && faceMaterialIndex >= 0 && faceMaterial != null)
        {
            SetRendererMaterial(faceRenderer, faceMaterialIndex, faceMaterial);
        }
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
