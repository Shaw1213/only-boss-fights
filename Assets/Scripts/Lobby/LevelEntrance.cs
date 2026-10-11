using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DreamLayerJam
{
    /// <summary>
    /// A relic in the lobby (the Knight's sword, the King's crown, the Unknown One's relic).
    /// Touching an open relic enters its fight.
    /// </summary>
    public class LevelEntrance : MonoBehaviour
    {
        public enum EntranceState
        {
            /// <summary>Can be entered.</summary>
            Open,
            /// <summary>Fight won; can be re-entered.</summary>
            Cleared,
            /// <summary>Waiting on other fights to be cleared.</summary>
            Sealed,
            /// <summary>The fight isn't built yet.</summary>
            NotBuilt,
        }

        [Header("Identity")]
        public string fightId = "boss01";
        public string title = "The Knight's Sword";
        [Tooltip("Scene to load when the hero touches the relic (must be in Build Settings).")]
        public string sceneName = "Boss01";
        [Tooltip("Fights that must be cleared before this relic opens.")]
        public string[] requires;
        [Tooltip("No fight built yet.")]
        public bool notBuiltYet;
        [Tooltip("Shown when the hero touches it while sealed.")]
        public string sealedMessage = "Sealed";
        [Tooltip("His story so far, shown over black as the hero enters (click skips). Empty: straight into the fight.")]
        [TextArea(2, 4)] public string story;

        [Header("Touch")]
        [Tooltip("How close (in x) the hero must get to touch the relic.")]
        public float touchRadius = 0.9f;

        [Header("Greybox visuals")]
        [Tooltip("The relic's shape; it hovers and glows while open.")]
        public Transform visual;
        public SpriteRenderer[] tintedParts;
        public Color color = new Color(0.8f, 0.82f, 0.9f);
        [Tooltip("Optional 2D light on the relic: pulses while open, dims when cleared, nearly off when sealed.")]
        public Light2D glow;
        public float glowIntensity = 1f;

        Vector3 visualBasePos;
        Vector3 visualBaseScale;
        float bobPhase;

        /// <summary>0–1 while the hero stands at the relic (set by the LobbyDirector); grows and brightens it.</summary>
        public float Charge { get; set; }

        public EntranceState State
        {
            get
            {
                if (GameProgress.IsCleared(fightId)) return EntranceState.Cleared;
                if (requires != null)
                    foreach (var id in requires)
                        if (!GameProgress.IsCleared(id)) return EntranceState.Sealed;
                if (notBuiltYet || string.IsNullOrEmpty(sceneName)) return EntranceState.NotBuilt;
                return EntranceState.Open;
            }
        }

        public bool CanEnter => State == EntranceState.Open || State == EntranceState.Cleared;
        public Vector3 VisualCenter => visual != null ? visual.position : transform.position;

        void Awake()
        {
            if (visual != null)
            {
                visualBasePos = visual.localPosition;
                visualBaseScale = visual.localScale;
            }
            bobPhase = transform.position.x; // de-sync the hovering
        }

        void Update()
        {
            var state = State;
            float t = Time.time + bobPhase;
            bool alive = state == EntranceState.Open || state == EntranceState.Cleared;

            if (visual != null)
            {
                float shake = Charge > 0f ? Mathf.Sin(Time.time * 60f) * 0.03f * Charge : 0f; // trembles as it charges
                visual.localPosition = visualBasePos + new Vector3(shake, alive ? Mathf.Sin(t * 2f) * 0.08f : 0f, 0f);
                visual.localScale = visualBaseScale * (1f + 0.3f * Charge);
            }

            Color c = state switch
            {
                EntranceState.Open => Color.Lerp(color, Color.white, 0.15f + 0.15f * Mathf.Sin(t * 3f)), // pulsing glow
                EntranceState.Cleared => Color.Lerp(color, new Color(0.5f, 0.5f, 0.5f), 0.55f),         // spent
                EntranceState.Sealed => Color.Lerp(color, Color.black, 0.5f),
                _ => new Color(0.35f, 0.35f, 0.38f, 0.6f),                                               // not built
            };
            if (Charge > 0f) c = Color.Lerp(c, Color.white, 0.6f * Charge);
            if (glow != null)
            {
                float k = state switch
                {
                    EntranceState.Open => 0.85f + 0.15f * Mathf.Sin(t * 3f),
                    EntranceState.Cleared => 0.45f,
                    EntranceState.Sealed => 0.25f,
                    _ => 0f,
                };
                glow.intensity = glowIntensity * (k + 0.8f * Charge);
            }
            if (tintedParts != null)
                foreach (var sr in tintedParts)
                    if (sr != null) sr.color = c;
        }
    }
}
