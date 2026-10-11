using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The per-event juice table (docs/design/boss-01.md §4). Keep the sum in the Medium–High band;
    /// the boss-kill slow-mo is the only top-tier effect.
    /// Per-attack hit-stop and shake live on each <see cref="AttackData"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "DreamLayer Jam/Feedback Config", fileName = "FeedbackConfig")]
    public class FeedbackConfig : ScriptableObject
    {
        [System.Serializable]
        public struct Bundle
        {
            [Min(0)] public int hitStopFrames;
            [Range(0f, 1f)] public float trauma;
            public Color flashColor;
            [Min(0)] public int flashFrames;

            public Bundle(int hitStopFrames, float trauma, Color flashColor, int flashFrames)
            {
                this.hitStopFrames = hitStopFrames;
                this.trauma = trauma;
                this.flashColor = flashColor;
                this.flashFrames = flashFrames;
            }
        }

        [Header("Hero hits boss (hit-stop and shake come from the attack)")]
        public Color hitFlashColor = Color.white;
        [Min(0)] public int hitFlashFrames = 2;

        [Header("Events")]
        [Tooltip("Must look and feel unlike any other event.")]
        public Bundle parry = new Bundle(9, 0.35f, new Color(0.4f, 1f, 1f), 6);
        public Bundle heroHurt = new Bundle(6, 0.45f, new Color(1f, 0.25f, 0.25f), 4);
        public Bundle bossStaggered = new Bundle(6, 0.35f, new Color(1f, 0.9f, 0.4f), 6);
        public Bundle phaseChange = new Bundle(30, 0.5f, Color.white, 10);

        [Header("Boss defeated (top tier, used once)")]
        public float bossKillSlowScale = 0.25f;
        public float bossKillSlowSeconds = 0.6f;
        [Range(0f, 1f)] public float bossKillTrauma = 0.8f;
    }
}
