using UnityEngine;

public sealed class BombermanMaterials
{
    public Material Floor { get; private set; }
    public Material Solid { get; private set; }
    public Material Destructible { get; private set; }
    public Material Player { get; private set; }
    public Material PlayerDead { get; private set; }
    public Material Oneal { get; private set; }
    public Material Dahl { get; private set; }
    public Material EnemyDead { get; private set; }
    public Material Bomb { get; private set; }
    public Material Explosion { get; private set; }
    public Material Border { get; private set; }

    public static BombermanMaterials Create()
    {
        return new BombermanMaterials
        {
            Floor = Make("Ice Floor", new Color(0.15f, 0.63f, 0.95f)),
            Solid = Make("Snow Block", new Color(0.92f, 0.96f, 1f)),
            Destructible = Make("Wood Crate", new Color(0.86f, 0.57f, 0.23f)),
            Player = Make("Bomberman Normal", new Color(0.08f, 0.82f, 0.68f)),
            PlayerDead = Make("Bomberman Dead Burnt", new Color(0.07f, 0.07f, 0.07f)),
            Oneal = Make("O'neal Normal", new Color(0.98f, 0.95f, 0.86f)),
            Dahl = Make("Dahl Normal", new Color(0.75f, 0.12f, 0.12f)),
            EnemyDead = Make("Enemy Dead", new Color(0.18f, 0.16f, 0.2f)),
            Bomb = Make("Bomb", new Color(0.02f, 0.02f, 0.025f)),
            Explosion = Make("Explosion", new Color(1f, 0.55f, 0.02f)),
            Border = Make("Blue Border", new Color(0.04f, 0.28f, 0.58f))
        };
    }

    public static Material Make(string materialName, Color color)
    {
        Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
        if (material.shader == null)
        {
            material.shader = Shader.Find("Standard");
        }

        material.name = materialName;
        material.color = color;
        return material;
    }
}
