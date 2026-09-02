using System.Collections;
using UnityEngine;

public abstract class EnemyController : GridController
{
    protected BombermanPrototype Game { get; private set; }

    public static TEnemy Create<TEnemy>(
        string actorName,
        Vector2Int cell,
        BombermanMap map,
        Material normalMaterial,
        Material deadMaterial,
        float scale)
        where TEnemy : EnemyController
    {
        return CreateActor<TEnemy>(actorName, cell, map, normalMaterial, deadMaterial, false, scale);
    }

    public void Initialize(BombermanPrototype game)
    {
        Game = game;
        StartCoroutine(ActionLoop());
    }

    protected abstract Vector2Int ChooseDirection();

    private IEnumerator ActionLoop()
    {
        yield return new WaitForSeconds(Random.Range(0.2f, 1f));
        while (!IsDead)
        {
            if (Game != null && !Game.IsPaused && !IsMoving)
            {
                Game.TryMoveActor(this, ChooseDirection());
            }

            yield return new WaitForSeconds(Random.Range(0.25f, 0.7f));
        }
    }
}
