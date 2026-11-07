public sealed class DoriaEnemy : EnemyController
{
    protected override DeathPresentation Presentation => DeathPresentation.FluidBurst;
    public override bool CanPassDestructibleWalls => true;
}
