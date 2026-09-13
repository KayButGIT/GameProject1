using UnityEngine;

public sealed class PassEnemy : EnemyController
{
    protected override Vector2Int ChooseDirection()
    {
        Vector2Int[] directions = BombermanPrototype.Directions;

        if (Random.value < 0.4f)
        {
            Vector2Int toPlayer = Game.PlayerCell - Cell;
            if (Mathf.Abs(toPlayer.x) >= Mathf.Abs(toPlayer.y) && toPlayer.x != 0)
            {
                return toPlayer.x > 0 ? Vector2Int.right : Vector2Int.left;
            }
            if (toPlayer.y != 0)
            {
                return toPlayer.y > 0 ? Vector2Int.up : Vector2Int.down;
            }
        }

        return directions[Random.Range(0, directions.Length)];
    }
}