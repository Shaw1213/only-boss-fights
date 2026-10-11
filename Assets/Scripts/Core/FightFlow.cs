using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DreamLayerJam
{
    /// <summary>
    /// Win / lose detection and what happens next. R (or Start) restarts any time; Esc returns to the
    /// lobby any time. After a win, J / click returns to the lobby; after a loss, J / click retries.
    /// A fight with an epilogue plays it after the win (a story beat after the Knight and the King, the ending after the
    /// final form); the first J / click shows every line.
    /// </summary>
    public class FightFlow : MonoBehaviour
    {
        public enum Result { Fighting, Victory, Defeat }

        public HeroController hero;
        public BossController boss;
        [Tooltip("Seconds after the result before J / click is accepted (so a held attack doesn't skip the result).")]
        public float retryDelay = 0.6f;

        [Header("Lobby")]
        [Tooltip("Marked cleared in GameProgress on a win; must match the lobby relic's fightId.")]
        public string fightId = "boss01";
        public string lobbySceneName = "Lobby";

        [Header("Story after the win")]
        [Tooltip("Shown line by line over black after the win, instead of the victory banner. Empty: the usual banner.")]
        [TextArea(1, 3)] public string[] epilogue;
        [Tooltip("The gold line under the story: the next relic waking, or \"Thank you for playing\". Empty: none.")]
        public string epilogueClosing;
        [Tooltip("Only on the first win (a story beat; replays show the victory banner). Off: every win (the ending).")]
        public bool epilogueFirstClearOnly;
        public MusicTrack epilogueMusic = MusicTrack.Ending;
        [Tooltip("Optional illustration behind the story (the ending): it fades in over the black with a slow push-in, and the lines play as subtitles under it.")]
        public Sprite epilogueArt;
        [Tooltip("Seconds after the killing blow before the story starts (lets the death land).")]
        public float epilogueDelay = 2f;
        public float epilogueLineSeconds = 2.6f;

        public Result Current { get; private set; }
        public float FightTime { get; private set; }
        /// <summary>True when this fight was started by a restart (R or retry), not from the lobby: intros are skipped.</summary>
        public static bool IsRetry { get; private set; }
        static bool restarting;

        /// <summary>This win plays the story instead of the victory banner.</summary>
        public bool ShowsEpilogue => Current == Result.Victory && epilogueThisWin;
        /// <summary>Real seconds since the fight ended.</summary>
        public float ResultAge => Time.unscaledTime - resultTime;
        /// <summary>How far the ending has played, in lines: line i shows from i (fading in) and is fully in by i + 0.4.</summary>
        public float EpilogueProgress => epilogueSkipped ? epilogue.Length + 1f : Mathf.Max(0f, (ResultAge - epilogueDelay) / epilogueLineSeconds);
        public bool EpilogueDone => EpilogueProgress >= epilogue.Length;

        Health heroHealth;
        Health bossHealth;
        float resultTime;
        bool epilogueThisWin;
        bool epilogueSkipped;

        void Awake()
        {
            IsRetry = restarting;
            restarting = false;
        }

        void OnEnable()
        {
            heroHealth = hero.GetComponent<Health>();
            bossHealth = boss.GetComponent<Health>();
            heroHealth.Died += OnHeroDied;
            bossHealth.Died += OnBossDied;
        }

        void OnDisable()
        {
            if (heroHealth != null) heroHealth.Died -= OnHeroDied;
            if (bossHealth != null) bossHealth.Died -= OnBossDied;
        }

        void OnHeroDied() => SetResult(Result.Defeat);

        void OnBossDied()
        {
            bool firstWin = !GameProgress.IsCleared(fightId);
            SetResult(Result.Victory);
            GameProgress.MarkCleared(fightId);
            epilogueThisWin = epilogue != null && epilogue.Length > 0 && (firstWin || !epilogueFirstClearOnly);
            if (ShowsEpilogue) GameAudio.PlayMusic(epilogueMusic);
        }

        void Start()
        {
            GameAudio.PlayMusic(MusicTrack.Fight);
            if (!IsRetry) GameAudio.Play(Sfx.NameCard); // the boss's name card lands with a boom
        }

        void SetResult(Result result)
        {
            if (Current != Result.Fighting) return;
            Current = result;
            resultTime = Time.unscaledTime;
            GameAudio.Play(result == Result.Victory ? Sfx.Victory : Sfx.Defeat);
        }

        void Update()
        {
            if (Current == Result.Fighting) FightTime += Time.deltaTime;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var mouse = Mouse.current;

            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
            {
                ReturnToLobby();
                return;
            }

            bool restart = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            bool confirm = Current != Result.Fighting && Time.unscaledTime - resultTime >= retryDelay &&
                           ((kb != null && kb.jKey.wasPressedThisFrame) ||
                            (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                            (pad != null && pad.buttonWest.wasPressedThisFrame));

            if (restart) Restart();
            else if (confirm && ShowsEpilogue && !EpilogueDone)
            {
                // The first press shows every line, but not while the player is still mashing from the killing blow.
                if (ResultAge > epilogueDelay + 1f) epilogueSkipped = true;
            }
            else if (confirm && Current == Result.Victory) ReturnToLobby();
            else if (confirm) Restart();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            restarting = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToLobby()
        {
            Time.timeScale = 1f;
            GameProgress.LastEntranceId = fightId;
            if (Application.CanStreamedLevelBeLoaded(lobbySceneName)) SceneManager.LoadScene(lobbySceneName);
            else Restart(); // no lobby in this build yet
        }
    }
}
