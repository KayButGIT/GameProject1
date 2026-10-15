using System;

// What the player has collected so far. Saved with the run, so Continue brings it back.
// Mystery is not stored: it is a timed effect that ends with the stage.
[Serializable]
public sealed class PowerUpState
{
    public int bombUps;
    public int fireUps;
    public int speedUps;
    public bool detonator;
    public bool bombPass;
    public bool wallPass;
    public bool flamePass;

    public PowerUpState Clone()
    {
        return (PowerUpState)MemberwiseClone();
    }
}