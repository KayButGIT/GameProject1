using UnityEngine;

public sealed class PontanEnemy : EnemyController
{
    private Vector2Int lastDirection = Vector2Int.zero;

    public override bool CanPassDestructibleWalls => true;

    protected override Vector2Int ChooseDirection()
    {
        Vector2Int[] directions = BombermanPrototype.Directions;

        if (lastDirection != Vector2Int.zero && Random.value < 0.6f)
        {
            return lastDirection;
        }

        Vector2Int chosen = directions[Random.Range(0, directions.Length)];
        lastDirection = chosen;
        return chosen;
    }
}