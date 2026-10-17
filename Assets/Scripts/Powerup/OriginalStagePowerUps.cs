/// <summary>Fixed power-up for each stage in the original 50-stage Bomberman campaign.</summary>
public static class OriginalStagePowerUps
{
    private static readonly PowerUpType[] TypesByStage =
    {
        PowerUpType.FireUp, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.SpeedUp, PowerUpType.BombUp,
        PowerUpType.BombUp, PowerUpType.FireUp, PowerUpType.Detonator, PowerUpType.BombPass, PowerUpType.WallPass,
        PowerUpType.BombUp, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.BombPass, PowerUpType.FireUp,
        PowerUpType.WallPass, PowerUpType.BombUp, PowerUpType.BombPass, PowerUpType.BombUp, PowerUpType.Detonator,
        PowerUpType.BombPass, PowerUpType.Detonator, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.BombPass,
        PowerUpType.Mystery, PowerUpType.FireUp, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.FlamePass,
        PowerUpType.WallPass, PowerUpType.BombUp, PowerUpType.Detonator, PowerUpType.Mystery, PowerUpType.BombPass,
        PowerUpType.FlamePass, PowerUpType.Detonator, PowerUpType.FireUp, PowerUpType.WallPass, PowerUpType.Mystery,
        PowerUpType.Detonator, PowerUpType.WallPass, PowerUpType.BombPass, PowerUpType.Detonator, PowerUpType.Mystery,
        PowerUpType.WallPass, PowerUpType.BombPass, PowerUpType.Detonator, PowerUpType.FlamePass, PowerUpType.Mystery
    };

    public static PowerUpType GetForStage(int stage, int campaignLength = 0)
    {
        int referenceStage = OriginalStageEnemies.ResolveReferenceStage(stage, campaignLength);
        return TypesByStage[referenceStage - 1];
    }
}
