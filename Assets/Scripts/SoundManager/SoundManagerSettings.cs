using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BombermanSoundSettings", menuName = "Bomberman/Sound Manager Settings")]
public sealed class SoundManagerSettings : ScriptableObject
{
    [Serializable]
    public sealed class MapMusicPhase
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Serializable]
    public sealed class FootstepPhase
    {
        public AudioClip[] footsteps = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 0.85f;
    }

    [Header("Master")]
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Background Music")]
    public AudioClip titleScreenBgm;
    [Range(0f, 1f)] public float titleScreenBgmVolume = 1f;
    public AudioClip stageStartIntroBgm;
    [Range(0f, 1f)] public float stageStartIntroBgmVolume = 1f;
    public List<MapMusicPhase> mapBgmPhases = new() { new MapMusicPhase() };
    public AudioClip deadBgm;
    [Range(0f, 1f)] public float deadBgmVolume = 1f;
    public AudioClip gameOverBgm;
    [Range(0f, 1f)] public float gameOverBgmVolume = 1f;
    public AudioClip stageWinBgm;
    [Range(0f, 1f)] public float stageWinBgmVolume = 1f;
    public AudioClip gameClearedBgm;
    [Range(0f, 1f)] public float gameClearedBgmVolume = 1f;

    [Header("Power Up Pickup")]
    public AudioClip powerUpPickup;
    [Range(0f, 1f)] public float powerUpPickupVolume = 1f;

    [Header("Bomb Lay")]
    public AudioClip placeBomb;
    [Range(0f, 1f)] public float placeBombVolume = 1f;

    [Header("Bomb Explode")]
    public AudioClip bombExplode;
    [Range(0f, 1f)] public float bombExplodeVolume = 1f;

    [Header("Bomberman Die")]
    public AudioClip playerDeath;
    [Range(0f, 1f)] public float playerDeathVolume = 1f;

    [Header("Exit Open")]
    public AudioClip exitOpen;
    [Range(0f, 1f)] public float exitOpenVolume = 1f;

    [Header("Walk by Sequence Phase")]
    public List<FootstepPhase> footstepPhases = new() { new FootstepPhase() };
    [Tooltip("Random pitch range applied to each footstep.")]
    public Vector2 footstepPitchRange = new(0.96f, 1.04f);

    public void EnsureFootstepPhaseCount(int phaseCount)
    {
        footstepPhases ??= new List<FootstepPhase>();
        while (footstepPhases.Count < Mathf.Max(1, phaseCount))
            footstepPhases.Add(new FootstepPhase());
    }

    public void EnsureMapBgmPhaseCount(int phaseCount)
    {
        mapBgmPhases ??= new List<MapMusicPhase>();
        while (mapBgmPhases.Count < Mathf.Max(1, phaseCount))
            mapBgmPhases.Add(new MapMusicPhase());
        while (mapBgmPhases.Count > Mathf.Max(1, phaseCount))
            mapBgmPhases.RemoveAt(mapBgmPhases.Count - 1);
    }

    [Header("Enemy Death")]
    public AudioClip[] onealDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float onealDeathVolume = 1f;
    public AudioClip[] dahlDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float dahlDeathVolume = 1f;
    public AudioClip[] pontanDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float pontanDeathVolume = 1f;
    public AudioClip[] passDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float passDeathVolume = 1f;
    public AudioClip[] valcomDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float valcomDeathVolume = 1f;
    public AudioClip[] ovapeDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float ovapeDeathVolume = 1f;
    public AudioClip[] doriaDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float doriaDeathVolume = 1f;
    public AudioClip[] minuoDeath = System.Array.Empty<AudioClip>();
    [Range(0f, 1f)] public float minuoDeathVolume = 1f;

    [Header("Title Screen Button Hover")]
    public AudioClip titleHover;
    [Range(0f, 1f)] public float titleHoverVolume = 1f;

    [Header("Title Screen Button Selected")]
    public AudioClip titleSelected;
    [Range(0f, 1f)] public float titleSelectedVolume = 1f;
}
