using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerController : GridController
{
    private BombermanPrototype game;

    public override bool IsPlayer => true;

    public static PlayerController Create(
        string actorName,
        Vector2Int cell,
        BombermanMap map,
        Material normalMaterial,
        Material deadMaterial,
        float scale)
    {
        return CreateActor<PlayerController>(actorName, cell, map, normalMaterial, deadMaterial, true, scale);
    }

    public void Initialize(BombermanPrototype game)
    {
        this.game = game;
    }

    protected override void Update()
    {
        base.Update();

        if (game == null || game.IsPaused || IsDead)
        {
            return;
        }

        if (!IsMoving)
        {
            Vector2Int move = ReadMove();
            if (move != Vector2Int.zero)
            {
                game.TryMoveActor(this, move);
            }
        }

        if (WasPressed(Key.Space))
        {
            game.TryDropBomb();
        }
    }

    private static Vector2Int ReadMove()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2Int.zero;
        }

        if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
        {
            return Vector2Int.up;
        }

        if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
        {
            return Vector2Int.down;
        }

        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
        {
            return Vector2Int.left;
        }

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
        {
            return Vector2Int.right;
        }

        return Vector2Int.zero;
    }

    private static bool WasPressed(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }
}
