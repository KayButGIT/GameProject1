using UnityEngine;

// Minimal power-up pickup. No power-up system existed in the project yet, so this is a stub:
// drop it on a prefab with a Collider set to "Is Trigger", place instances on the map (e.g. under
// a destroyed-block cell), and it plays the pickup SFX, awards score, and reports to
// BombermanPrototype. Fill in OnCollected() with whatever the actual effect should be
// (extra bomb, bigger blast radius, speed boost, etc.) once that's designed.
public sealed class PowerUp : MonoBehaviour
{
    [SerializeField] private int scoreValue = 50;

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsDead)
        {
            return;
        }

        SoundManager.Instance?.PlayPowerUpPickup();

        if (scoreValue != 0)
        {
            BombermanPrototype game = FindFirstObjectByType<BombermanPrototype>();
            game?.AddScore(scoreValue);
        }

        OnCollected(player);
        Destroy(gameObject);
    }

    // Hook point for the real power-up effect once the team designs it.
    private void OnCollected(PlayerController player)
    {
    }
}