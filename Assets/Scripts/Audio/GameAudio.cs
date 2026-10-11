using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamLayerJam
{
    /// <summary>
    /// Plays the game's sounds: <c>GameAudio.Play(Sfx.Parry)</c> from anywhere. Created on first use from
    /// Resources/SoundBank and kept across scenes, so the music carries from the lobby into a fight.
    /// Sound effects are 2D (a side-view arena fits on one screen) and ignore time scale, so hit-stop freezes the
    /// picture but not the impact. Music cross-fades between two sources.
    /// It also carries the game's one AudioListener: scene cameras' listeners are switched off, so there's no gap
    /// with no listener while a scene loads and the music keeps playing (every sound is 2D, so its position doesn't
    /// matter).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        const int PoolSize = 16;
        const float MusicFadeSeconds = 1.2f;

        static GameAudio instance;

        SoundBank bank;
        readonly Dictionary<Sfx, SoundBank.Cue> cues = new Dictionary<Sfx, SoundBank.Cue>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        AudioSource[] pool;
        int next;
        AudioSource musicA, musicB;
        AudioListener listener;
        MusicTrack currentTrack;
        float musicFade = 1f;

        // Enter Play Mode without a domain reload keeps statics: forget the last session's (destroyed) instance.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        static GameAudio Instance
        {
            get
            {
                if (instance != null) return instance; // Unity's null check: false once the object is destroyed
                var bank = Resources.Load<SoundBank>("SoundBank");
                if (bank == null) return null;
                var go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameAudio>();
                instance.Init(bank);
                return instance;
            }
        }

        /// <summary>Plays a cue. <paramref name="volume"/> scales it (e.g. for lighter variants of an event).</summary>
        public static void Play(Sfx id, float volume = 1f)
        {
            var a = Instance;
            if (a != null) a.PlayCue(id, volume);
        }

        /// <summary>Cross-fades to a track (no-op if it's already playing, or the bank has no clip for it).</summary>
        public static void PlayMusic(MusicTrack track)
        {
            var a = Instance;
            if (a != null) a.SwitchMusic(track);
        }

        void Init(SoundBank b)
        {
            bank = b;
            foreach (var c in bank.cues)
                if (c != null && c.clips != null && c.clips.Length > 0) cues[c.id] = c;
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++) pool[i] = NewSource();
            musicA = NewSource();
            musicB = NewSource();
            musicA.loop = musicB.loop = true;
            listener = gameObject.AddComponent<AudioListener>();
            DisableSceneListeners();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => DisableSceneListeners();

        void DisableSceneListeners()
        {
            foreach (var l in FindObjectsByType<AudioListener>())
                if (l != listener) l.enabled = false;
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        void PlayCue(Sfx id, float volume)
        {
            if (!cues.TryGetValue(id, out var cue)) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < cue.cooldown) return;
            lastPlayed[id] = now;

            float pitch = Random.Range(cue.pitch.x, cue.pitch.y);
            Emit(cue.clips[Random.Range(0, cue.clips.Length)], cue.volume * volume, pitch);
            if (cue.layer != null && cue.layer.Length > 0)
                Emit(cue.layer[Random.Range(0, cue.layer.Length)], cue.layerVolume * volume, pitch);
        }

        void Emit(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            // Round-robin over the pool, skipping sources still playing when a free one exists.
            AudioSource src = null;
            for (int i = 0; i < PoolSize; i++)
            {
                var s = pool[(next + i) % PoolSize];
                if (!s.isPlaying) { src = s; next = (next + i + 1) % PoolSize; break; }
            }
            if (src == null) { src = pool[next]; next = (next + 1) % PoolSize; }
            src.clip = clip;
            src.volume = volume * bank.sfxVolume;
            src.pitch = pitch;
            src.Play();
        }

        void SwitchMusic(MusicTrack track)
        {
            if (track == currentTrack) return;
            var clip = ClipFor(track);
            currentTrack = track;
            if (clip != null && musicA.clip == clip && musicA.isPlaying) return; // same music under a new name: keep it going
            // The new track starts on the quiet source; Update fades it in and the old one out.
            (musicA, musicB) = (musicB, musicA);
            musicA.clip = clip;
            musicA.volume = 0f;
            if (clip != null) musicA.Play();
            else musicA.Stop();
            musicFade = 0f;
        }

        AudioClip ClipFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Lobby: return bank.lobbyMusic;
                case MusicTrack.Fight: return bank.fightMusic;
                case MusicTrack.FightPhase2: return bank.fightPhase2Music != null ? bank.fightPhase2Music : bank.fightMusic;
                case MusicTrack.Ending: return bank.endingMusic != null ? bank.endingMusic : bank.lobbyMusic;
                default: return null;
            }
        }

        void Update()
        {
            if (musicFade >= 1f) return;
            musicFade = Mathf.Min(1f, musicFade + Time.unscaledDeltaTime / MusicFadeSeconds);
            musicA.volume = bank.musicVolume * musicFade;
            musicB.volume = bank.musicVolume * (1f - musicFade);
            if (musicFade >= 1f) musicB.Stop();
        }
    }
}
