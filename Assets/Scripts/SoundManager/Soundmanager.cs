using UnityEngine;
using UnityEngine.Serialization;

// Central SFX hub with an adjustable volume for every sound.
//
// Setup: put one "Sound Manager" GameObject with this component in the Title scene (on its own empty
// object, no parent, no other components) and assign the clips in the Inspector. Awake() marks it
// DontDestroyOnLoad so it survives into the Prototype scene. If you press Play starting from the
// Prototype scene directly (skipping Title), drop a second copy there too; the Awake() de-dupe guard
// keeps only one alive.
//
// Volume = Master Volume x that sound's own volume slider (both 0..1). Sliders can be dragged while
// the game is running and take effect on the next sound played.
//
// Called from: BombermanPrototype (place bomb, bomb explode, exit open, player death),
// EnemyController (per-type enemy death), PlayerController (footstep), TitleButtonSfx (title
// hover/selected), PowerUp (pickup).
public sealed class SoundManager : MonoBehaviour
{
    private static SoundManager instance;
    public static SoundManager Instance => instance;

    [Header("Master")]
    [Tooltip("Scales every sound below. 0 = mute all SFX.")]
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;

    [Header("Power Up Pickup")]
    [Tooltip("SFX/Pickup Powerup.wav")]
    [SerializeField] private AudioClip powerUpPickup;
    [Range(0f, 1f)][SerializeField] private float powerUpPickupVolume = 1f;

    [Header("Bomb Lay")]
    [Tooltip("SFX/Bomb_Lay.mp3")]
    [SerializeField] private AudioClip placeBomb;
    [Range(0f, 1f)][SerializeField] private float placeBombVolume = 1f;

    [Header("Bomb Explode")]
    [Tooltip("SFX/Bomb_Explode.mp3")]
    [SerializeField] private AudioClip bombExplode;
    [Range(0f, 1f)][SerializeField] private float bombExplodeVolume = 1f;

    [Header("Bomberman Die")]
    [Tooltip("SFX/Player_DeadImpact.mp3, played when the player dies.")]
    [SerializeField] private AudioClip playerDeath;
    [Range(0f, 1f)][SerializeField] private float playerDeathVolume = 1f;

    [Header("Exit Open")]
    [Tooltip("SFX/ExitOpen.mp3, played once when the exit door unlocks.")]
    [SerializeField] private AudioClip exitOpen;
    [Range(0f, 1f)][SerializeField] private float exitOpenVolume = 1f;

    [Header("Walk - Stage 1")]
    [Tooltip("SFX/WalkMap1A.mp3, WalkMap1B.mp3, WalkMap1C.mp3 - one is picked at random per step.")]
    [SerializeField] private AudioClip[] stage1Footsteps;
    [Range(0f, 1f)][SerializeField] private float footstepVolume = 0.85f;
    [Tooltip("Random pitch range applied to each footstep so they don't sound robotic.")]
    [SerializeField] private Vector2 footstepPitchRange = new(0.96f, 1.04f);
    [Header("Walk - Stage 2 / 3 (optional - empty falls back to the Stage 1 clips)")]
    [SerializeField] private AudioClip[] stage2Footsteps;
    [SerializeField] private AudioClip[] stage3Footsteps;
    [Tooltip("World units the player must travel between footstep sounds.")]
    [SerializeField, Min(0.05f)] private float footstepStride = 0.9f;

    [Header("Enemy Death - one clip list + volume per enemy (several clips = random pick)")]
    [Tooltip("SFX/Enemy Death/O'neal Dead.mp3")]
    [SerializeField] private AudioClip[] onealDeath;
    [Range(0f, 1f)][SerializeField] private float onealDeathVolume = 1f;
    [Tooltip("SFX/Enemy Death/Dahl Dead.mp3")]
    [SerializeField] private AudioClip[] dahlDeath;
    [Range(0f, 1f)][SerializeField] private float dahlDeathVolume = 1f;
    [Tooltip("SFX/Enemy Death/Pontan Dead.mp3 and Pontan Dead ALT.mp3 - one is picked at random.")]
    [SerializeField] private AudioClip[] pontanDeath;
    [Range(0f, 1f)][SerializeField] private float pontanDeathVolume = 1f;
    [Tooltip("SFX/Enemy Death/Pass Dead.mp3")]
    [SerializeField] private AudioClip[] passDeath;
    [Range(0f, 1f)][SerializeField] private float passDeathVolume = 1f;
    [Tooltip("SFX/Enemy Death/Valcom Dead.mp3")]
    [SerializeField] private AudioClip[] valcomDeath;
    [Range(0f, 1f)][SerializeField] private float valcomDeathVolume = 1f;
    [Tooltip("SFX/Enemy Death/Ovape Dead.m4a")]
    [SerializeField] private AudioClip[] ovapeDeath;
    [Range(0f, 1f)][SerializeField] private float ovapeDeathVolume = 1f;
    [Tooltip("SFX/Enemy Death/Doria Dead.mp3")]
    [SerializeField] private AudioClip[] doriaDeath;
    [Range(0f, 1f)][SerializeField] private float doriaDeathVolume = 1f;
    [Tooltip("SFX/Enemy Death/Minuo Dead.mp3")]
    [SerializeField] private AudioClip[] minuoDeath;
    [Range(0f, 1f)][SerializeField] private float minuoDeathVolume = 1f;

    [Header("Title Screen Button Hover")]
    [Tooltip("Played on mouse hover and on keyboard/gamepad navigation between buttons.")]
    [FormerlySerializedAs("titleChoose")]
    [SerializeField] private AudioClip titleHover;
    [Range(0f, 1f)][SerializeField] private float titleHoverVolume = 1f;

    [Header("Title Screen Button Selected")]
    [Tooltip("Played when a title button is confirmed (click, Space or Enter).")]
    [FormerlySerializedAs("titleClick")]
    [SerializeField] private AudioClip titleSelected;
    [Range(0f, 1f)][SerializeField] private float titleSelectedVolume = 1f;

    public float FootstepStride => footstepStride;

    // Runtime access for a future options menu (0..1).
    public float MasterVolume
    {
        get => sfxVolume;
        set => sfxVolume = Mathf.Clamp01(value);
    }

    private AudioSource sfxSource;
    private AudioSource footstepSource;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Remove only this duplicate component. Destroying the whole GameObject would also
            // delete anything else living on it (e.g. BombermanPrototype), leaving the scene dead.
            Destroy(this);
            return;
        }

        instance = this;

        // DontDestroyOnLoad keeps the entire GameObject alive across scenes. If this component sits on
        // an object that also holds scene logic (TitleMenu, Canvas, BombermanPrototype...), that logic
        // would leak into the next scene and look like a frozen game. Keep it only on a dedicated object.
        bool dedicated = transform.parent == null && GetComponents<Component>().Length <= 2;
        if (dedicated)
        {
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("SoundManager: put this component on its own empty GameObject (no other components, no parent) so it can persist between scenes.", this);
        }

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 0f;
        footstepSource.loop = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void PlayOneShot(AudioClip clip, float soundVolume)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(clip, sfxVolume * soundVolume);
    }

    private void PlayRandomOneShot(AudioClip[] pool, float soundVolume)
    {
        if (pool == null || pool.Length == 0)
        {
            return;
        }

        PlayOneShot(pool[Random.Range(0, pool.Length)], soundVolume);
    }

    public void PlayPowerUpPickup() => PlayOneShot(powerUpPickup, powerUpPickupVolume);
    public void PlayPlaceBomb() => PlayOneShot(placeBomb, placeBombVolume);
    public void PlayBombExplode() => PlayOneShot(bombExplode, bombExplodeVolume);
    public void PlayExitOpen() => PlayOneShot(exitOpen, exitOpenVolume);
    public void PlayPlayerDeath() => PlayOneShot(playerDeath, playerDeathVolume);
    // Method names kept so TitleButtonSfx.cs needs no change: "Choose" = hover, "Click" = selected.
    public void PlayTitleChoose() => PlayOneShot(titleHover, titleHoverVolume);
    public void PlayTitleClick() => PlayOneShot(titleSelected, titleSelectedVolume);

    // enemyTypeName is the C# type name of the EnemyController subclass (GetType().Name),
    // e.g. "DahlEnemy", "PontanEnemy". Unknown types or empty slots play nothing.
    public void PlayEnemyDeath(string enemyTypeName)
    {
        AudioClip[] pool;
        float volume;
        switch (enemyTypeName)
        {
            case "OnealEnemy": pool = onealDeath; volume = onealDeathVolume; break;
            case "DahlEnemy": pool = dahlDeath; volume = dahlDeathVolume; break;
            case "PontanEnemy": pool = pontanDeath; volume = pontanDeathVolume; break;
            case "PassEnemy": pool = passDeath; volume = passDeathVolume; break;
            case "ValcomEnemy": pool = valcomDeath; volume = valcomDeathVolume; break;
            case "OvapeEnemy": pool = ovapeDeath; volume = ovapeDeathVolume; break;
            case "DoriaEnemy": pool = doriaDeath; volume = doriaDeathVolume; break;
            case "MinuoEnemy": pool = minuoDeath; volume = minuoDeathVolume; break;
            default: pool = null; volume = 1f; break;
        }

        PlayRandomOneShot(pool, volume);
    }

    // "Stage" here is the map/theme phase (BombermanPrototype.CurrentStagePhase, 1-based), so all
    // stages on map 2 use the Stage 2 sound. Stage 2/3 fall back to the Stage 1 pool while their
    // own clips haven't been assigned yet.
    public void PlayFootstep(int stagePhase)
    {
        AudioClip[] pool = stagePhase switch
        {
            2 => stage2Footsteps is { Length: > 0 } ? stage2Footsteps : stage1Footsteps,
            3 => stage3Footsteps is { Length: > 0 } ? stage3Footsteps : stage1Footsteps,
            _ => stage1Footsteps
        };

        if (pool == null || pool.Length == 0 || footstepSource == null)
        {
            return;
        }

        AudioClip clip = pool[Random.Range(0, pool.Length)];
        if (clip == null)
        {
            return;
        }

        footstepSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
        footstepSource.PlayOneShot(clip, sfxVolume * footstepVolume);
    }
}