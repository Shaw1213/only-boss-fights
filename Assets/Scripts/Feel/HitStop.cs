using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Global hit-stop and slow motion through Time.timeScale. Freezes never stack: a new freeze only
    /// extends the current one to the longer of the two (stacked hit-stop reads as lag). A hold (the first-time
    /// coach's teaching pause) stops time until it's released; freezes and slow motion wait under it.
    /// Runs early so a freeze requested this frame isn't shortened by this frame's delta.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        float freezeRemaining;
        float slowRemaining;
        float slowScale = 1f;
        bool held;

        public static bool IsFrozen => Instance != null && Instance.freezeRemaining > 0f;

        /// <summary>Time stands still while this is set (the first-time coach waits for a key).</summary>
        public static bool Held
        {
            get => Instance != null && Instance.held;
            set
            {
                if (Instance == null) return;
                Instance.held = value;
                Time.timeScale = value ? 0f : Instance.CurrentScale();
            }
        }

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        public static void Freeze(int frames) => Freeze(Frames.ToSeconds(frames));

        public static void Freeze(float seconds)
        {
            if (Instance == null || seconds <= 0f) return;
            Instance.freezeRemaining = Mathf.Max(Instance.freezeRemaining, seconds);
            Time.timeScale = 0f;
        }

        public static void SlowMotion(float scale, float seconds)
        {
            if (Instance == null || seconds <= 0f) return;
            Instance.slowScale = Mathf.Clamp(scale, 0.01f, 1f);
            Instance.slowRemaining = Mathf.Max(Instance.slowRemaining, seconds);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (held)
            {
                Time.timeScale = 0f;
                return;
            }
            if (freezeRemaining > 0f)
            {
                freezeRemaining -= dt;
                Time.timeScale = freezeRemaining > 0f ? 0f : CurrentScale();
                return;
            }
            if (slowRemaining > 0f) slowRemaining -= dt;
            Time.timeScale = CurrentScale();
        }

        float CurrentScale() => freezeRemaining > 0f ? 0f : slowRemaining > 0f ? slowScale : 1f;
    }
}
