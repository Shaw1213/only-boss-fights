using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The arena's painted background (DreamLayer): the calm hall, and its phase-2 edit ("same scene, now
    /// burning") fading in while the boss transforms. A retry reloads the scene, so it always starts calm.
    /// </summary>
    public class ArenaBackdrop : MonoBehaviour
    {
        public BossController boss;
        public SpriteRenderer calm;
        [Tooltip("The phase-2 version, drawn over the calm one.")]
        public SpriteRenderer phase2;
        [Tooltip("Seconds to fade the phase-2 version in (the boss's phase transition is 1.2 s).")]
        public float fadeSeconds = 1.2f;

        float amount;

        void Start() => Apply();

        void Update()
        {
            bool on = boss != null && (boss.Phase == 2 || boss.CurrentState == BossController.State.PhaseTransition);
            amount = Mathf.MoveTowards(amount, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            Apply();
        }

        void Apply()
        {
            if (phase2 == null) return;
            phase2.enabled = amount > 0f;
            var c = phase2.color;
            c.a = Mathf.SmoothStep(0f, 1f, amount);
            phase2.color = c;
        }
    }
}
