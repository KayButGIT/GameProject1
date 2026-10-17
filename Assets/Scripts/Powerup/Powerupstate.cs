using System;

// What the player has collected so far. Saved with the run, so Continue brings it back.
// Mystery is not stored: it is a timed effect that ends with the stage.
[Serializable]
public sealed class PowerUpState
{
    public int bombUps;
    public int fireUps;
    public int speedUps;
    // Permanent levels granted through demo cheats are removed on death; normal pickups persist.
    public int cheatBombUps;
    public int cheatFireUps;
    public int cheatSpeedUps;
    public bool detonator;
    public bool bombPass;
    public bool wallPass;
    public bool flamePass;

    public PowerUpState Clone()
    {
        return (PowerUpState)MemberwiseClone();
    }
}
