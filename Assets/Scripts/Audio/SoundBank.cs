using System;
using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>Every sound the game plays. Names match the cue ids in Assets/Audio/sound_bank.json.</summary>
    public enum Sfx
    {
        None,
        // Hero
        HeroSwingLight, HeroSwingHeavy, HitLight, HitHeavy, Parry, Clash, FlashCounter, Dash, HeroHurt, HeroDeath,
        ChargeStart, Harvest, WaveRelease,
        // Boss
        CueGlint, CueDanger, BossSwing, BossSlam, FlameWave, BurnTick, BossStagger, PhaseChange, BossDeath, BossStep,
        // Lobby / flow
        Footstep, NameCard, RelicCharge, RelicEnter, RelicSealed, UiConfirm, Victory, Defeat,
    }

    public enum MusicTrack { None, Lobby, Fight, FightPhase2, Ending }

    /// <summary>
    /// The game's sounds as data: each cue picks one of its clips at random (plus one from its layer, mixed under it),
    /// with a little pitch variation so repeats don't sound identical, and a cooldown so a burst of events doesn't
    /// stack the same sound. Built by AgentScripts/BuildAudio.cs from the loudness-measured spec
    /// (Tools/build_sound_bank.py); lives in Resources so <see cref="GameAudio"/> finds it in every scene.
    /// </summary>
    [CreateAssetMenu(menuName = "DreamLayer Jam/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Serializable]
        public class Cue
        {
            public Sfx id;
            public AudioClip[] clips;
            [Tooltip("Mixed under the main clip (e.g. the metal clank under a parry ring).")]
            public AudioClip[] layer;
            [Range(0f, 1f)] public float volume = 0.8f;
            [Range(0f, 1f)] public float layerVolume = 0.5f;
            public Vector2 pitch = new Vector2(0.95f, 1.05f);
            [Tooltip("The same cue won't restart within this many seconds.")]
            public float cooldown = 0.03f;
        }

        public Cue[] cues = new Cue[0];

        [Header("Music")]
        public AudioClip lobbyMusic;
        public AudioClip fightMusic;
        [Tooltip("Takes over when the boss enters phase 2. Empty: the fight music keeps playing.")]
        public AudioClip fightPhase2Music;
        [Tooltip("Plays under the ending after the final fight. Empty: the lobby music.")]
        public AudioClip endingMusic;
        [Range(0f, 1f)] public float musicVolume = 0.45f;

        [Header("Mix")]
        [Range(0f, 1f)] public float sfxVolume = 1f;
    }
}
