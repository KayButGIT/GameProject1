using UnityEngine;

public sealed class OnealEnemy : EnemyController
{
    protected override Vector2Int ChooseDirection()
    {
        Vector2Int[] directions = BombermanPrototype.Directions;
        return directions[Random.Range(0, directions.Length)];
    }
}
