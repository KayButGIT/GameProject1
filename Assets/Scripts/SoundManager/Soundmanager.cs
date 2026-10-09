using UnityEngine;

public sealed class SoundManager : MonoBehaviour
{
    private const string SettingsResourceName = "BombermanSoundSettings";
    private static SoundManager instance;
    public static SoundManager Instance => instance;
    public SoundManagerSettings Settings => settings;

    // Runtime access for a future options menu (0..1).
    public float MasterVolume
    {
        get => sfxVolume;
        set => sfxVolume = Mathf.Clamp01(value);
    }

    private SoundManagerSettings settings;
    private float sfxVolume = 1f;
    private AudioSource sfxSource;
    private AudioSource footstepSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        SoundManagerSettings loadedSettings = Resources.Load<SoundManagerSettings>(SettingsResourceName);
        if (loadedSettings == null)
        {
            Debug.LogWarning($"SoundManager: Resources/{SettingsResourceName}.asset is missing. Open Tools > Bomberman > Sound Manager to create and configure it; SFX will remain silent.");
        }

        GameObject managerObject = new("Sound Manager");
        SoundManager manager = managerObject.AddComponent<SoundManager>();
        manager.Configure(loadedSettings);
    }

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

        DontDestroyOnLoad(gameObject);

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 0f;
        footstepSource.loop = false;
    }

    private void Configure(SoundManagerSettings loadedSettings)
    {
        settings = loadedSettings;
        if (settings != null)
        {
            sfxVolume = Mathf.Clamp01(settings.sfxVolume);
        }
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

    public void PlayPowerUpPickup() => PlayOneShot(settings != null ? settings.powerUpPickup : null, settings != null ? settings.powerUpPickupVolume : 0f);
    public void PlayPlaceBomb() => PlayOneShot(settings != null ? settings.placeBomb : null, settings != null ? settings.placeBombVolume : 0f);
    public void PlayBombExplode() => PlayOneShot(settings != null ? settings.bombExplode : null, settings != null ? settings.bombExplodeVolume : 0f);
    public void PlayExitOpen() => PlayOneShot(settings != null ? settings.exitOpen : null, settings != null ? settings.exitOpenVolume : 0f);
    public void PlayPlayerDeath() => PlayOneShot(settings != null ? settings.playerDeath : null, settings != null ? settings.playerDeathVolume : 0f);
    // Method names kept so TitleButtonSfx.cs needs no change: "Choose" = hover, "Click" = selected.
    public void PlayTitleChoose() => PlayOneShot(settings != null ? settings.titleHover : null, settings != null ? settings.titleHoverVolume : 0f);
    public void PlayTitleClick() => PlayOneShot(settings != null ? settings.titleSelected : null, settings != null ? settings.titleSelectedVolume : 0f);

    // enemyTypeName is the C# type name of the EnemyController subclass (GetType().Name),
    // e.g. "DahlEnemy", "PontanEnemy". Unknown types or empty slots play nothing.
    public void PlayEnemyDeath(string enemyTypeName)
    {
        if (settings == null) return;

        AudioClip[] pool;
        float volume;
        switch (enemyTypeName)
        {
            case "OnealEnemy": pool = settings.onealDeath; volume = settings.onealDeathVolume; break;
            case "DahlEnemy": pool = settings.dahlDeath; volume = settings.dahlDeathVolume; break;
            case "PontanEnemy": pool = settings.pontanDeath; volume = settings.pontanDeathVolume; break;
            case "PassEnemy": pool = settings.passDeath; volume = settings.passDeathVolume; break;
            case "ValcomEnemy": pool = settings.valcomDeath; volume = settings.valcomDeathVolume; break;
            case "OvapeEnemy": pool = settings.ovapeDeath; volume = settings.ovapeDeathVolume; break;
            case "DoriaEnemy": pool = settings.doriaDeath; volume = settings.doriaDeathVolume; break;
            case "MinuoEnemy": pool = settings.minuoDeath; volume = settings.minuoDeathVolume; break;
            default: pool = null; volume = 1f; break;
        }

        PlayRandomOneShot(pool, volume);
    }

    // Sequence phases are 1-based. Every stage assigned to a phase uses that phase's footstep pool.
    public void PlayFootstep(int stagePhase)
    {
        if (settings == null || settings.footstepPhases == null || settings.footstepPhases.Count == 0) return;

        int phaseIndex = Mathf.Max(0, stagePhase - 1);
        SoundManagerSettings.FootstepPhase phase = phaseIndex < settings.footstepPhases.Count
            ? settings.footstepPhases[phaseIndex]
            : null;
        SoundManagerSettings.FootstepPhase firstPhase = settings.footstepPhases[0];
        float volume = phase != null ? phase.volume : firstPhase != null ? firstPhase.volume : 0f;
        AudioClip[] pool = phase?.footsteps;
        if (pool == null || pool.Length == 0)
            pool = firstPhase?.footsteps;

        if (pool == null || pool.Length == 0 || footstepSource == null)
        {
            return;
        }

        AudioClip clip = pool[Random.Range(0, pool.Length)];
        if (clip == null)
        {
            return;
        }

        footstepSource.pitch = Random.Range(settings.footstepPitchRange.x, settings.footstepPitchRange.y);
        footstepSource.PlayOneShot(clip, sfxVolume * volume);
    }
}
