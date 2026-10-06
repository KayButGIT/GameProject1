public sealed class PontanEnemy : EnemyController
{
    public override bool CanPassDestructibleWalls => true;
    protected override bool UseShortestPathChase => true;
}
