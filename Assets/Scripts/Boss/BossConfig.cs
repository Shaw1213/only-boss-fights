using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>How a boss's floor attacks look (their telegraph and their eruption).</summary>
    public enum FloorStyle { Marker, IceSpikes, Tentacles }

    /// <summary>A boss form: health, stagger meter, movement and the move list for each phase.</summary>
    [CreateAssetMenu(menuName = "DreamLayer Jam/Boss Config", fileName = "BossConfig")]
    public class BossConfig : ScriptableObject
    {
        public string displayName = "Boss";

        [Header("Health")]
        [Min(1)] public int maxHealth = 40;

        [Header("Stagger meter")]
        public float staggerMax = 100f;
        [Tooltip("Added when the hero parries one of the boss's hits.")]
        public float staggerPerParry = 25f;
        [Tooltip("The meter starts draining this long after the last stagger damage.")]
        public float staggerDecayDelaySeconds = 2f;
        public float staggerDecayPerSecond = 10f;
        [Tooltip("How long the boss stays down when the meter fills (the big punish window).")]
        public float staggerDurationSeconds = 2.2f;
        [Tooltip("How far a parry pushes the boss back.")]
        public float parryPushback = 0.3f;

        [Header("Movement")]
        [Tooltip("The hero can't walk through him, and his lunges stop at the hero (the dash still goes through).")]
        public bool solidBody = true;
        public float walkSpeed = 2.5f;
        [Tooltip("Give up walking into range after this long and attack anyway.")]
        public float approachMaxSeconds = 1.5f;
        public float firstAttackDelaySeconds = 1.2f;

        [Header("Hurtbox (relative to the feet)")]
        public Vector2 hurtboxOffset = new Vector2(0f, 1.9f);
        public Vector2 hurtboxSize = new Vector2(1.8f, 3.8f);

        [Header("Floor attacks (the floor-marker cue)")]
        [Tooltip("Marker: a plain bar on the floor during the windup. IceSpikes (the King): a frost patch, then ice spikes erupt.")]
        public FloorStyle floorStyle = FloorStyle.Marker;

        [Header("Phase 1")]
        public BossMove[] phase1Moves;
        [Tooltip("Random pause between moves (min, max seconds).")]
        public Vector2 phase1IdleSeconds = new Vector2(0.6f, 1.0f);

        [Header("Phase 2")]
        [Range(0f, 1f)] public float phase2AtHealth = 0.5f;
        public BossMove[] phase2Moves;
        public Vector2 phase2IdleSeconds = new Vector2(0.25f, 0.5f);
        public float phaseTransitionSeconds = 1.2f;
        [Tooltip("A combo-breaking hit (the flash counter): he flinches this long, then waits out the idle below.")]
        public float comboBreakFlinchSeconds = 0.45f;
        public float comboBreakIdleSeconds = 0.9f;

        [Header("Finale (optional)")]
        [Tooltip("In phase 2, the first time health drops to this fraction the boss is held still for Finale Hold Seconds: the final form's last stand, where the knight inside holds the god for the final blow. 0 = off.")]
        [Range(0f, 1f)] public float finaleAtHealth;
        public float finaleHoldSeconds = 4f;
        [Tooltip("Shown over the fight while he's held.")]
        public string finaleText = "The knight holds him. Strike!";

        [Header("Move selection")]
        public bool avoidRepeats = true;
    }
}
