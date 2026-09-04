using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerController : GridController
{
    private BombermanPrototype game;
    private BombermanMap map;
    private PlayerAnimationBridge animationBridge;
    private Vector2 moveInput;

    public override bool IsPlayer => true;

    public static PlayerController Create(
        string actorName,
        Vector2Int cell,
        BombermanMap map,
        Material normalMaterial,
        Material deadMaterial,
        float scale,
        GameObject modelPrefab,
        float modelScale,
        RuntimeAnimatorController animatorController)
    {
        PlayerController player = CreateActor<PlayerController>(actorName, cell, map, normalMaterial, deadMaterial, true, scale);
        player.map = map;
        player.ConfigureFreeMovementPhysics(0.32f * scale, 1.25f * scale);
        player.ConfigureVisual(modelPrefab, modelScale, animatorController);
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
        IsDead = true;
        IsMoving = false;
        moveInput = Vector2.zero;
        animationBridge?.SetDeath();

        if (animationBridge != null && animationBridge.HasAnimator)
        {
            Destroy(gameObject, 0.85f);
            return;
        }

        base.Die();
    }

    private void ConfigureVisual(GameObject modelPrefab, float modelScale, RuntimeAnimatorController animatorController)
    {
        Transform visualRoot = transform;
        if (modelPrefab != null)
        {
            RemovePlaceholderVisual();
            GameObject model = Instantiate(modelPrefab, transform);
            model.name = "Player Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * modelScale;
            visualRoot = model.transform;
            BodyRenderer = model.GetComponentInChildren<Renderer>();
        }

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
