using UnityEngine;

public sealed class MinuoEnemy : EnemyController
{
    protected override Vector2Int ChooseDirection()
    {
        Vector2Int toPlayer = Game.PlayerCell - Cell;

        if (toPlayer == Vector2Int.zero)
        {
            Vector2Int[] directions = BombermanPrototype.Directions;
            return directions[Random.Range(0, directions.Length)];
        }

        if (Mathf.Abs(toPlayer.x) >= Mathf.Abs(toPlayer.y))
        {
            return toPlayer.x > 0 ? Vector2Int.right : Vector2Int.left;
        }

        return toPlayer.y > 0 ? Vector2Int.up : Vector2Int.down;
    }
}