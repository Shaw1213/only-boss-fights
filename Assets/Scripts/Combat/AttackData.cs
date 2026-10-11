using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// One attack's frame data, damage, hitbox and feel. Used by both the hero and the boss.
    /// Frames are authored at 60 fps. In a multi-hit boss move, a step's recovery plus the next
    /// step's startup sets the rhythm between hits; the last step's recovery is the punish window.
    /// </summary>
    [CreateAssetMenu(menuName = "DreamLayer Jam/Attack Data", fileName = "NewAttack")]
    public class AttackData : ScriptableObject
    {
        [Header("Frame data (60 fps)")]
        [Tooltip("Windup before the hitbox is live. For the boss this is the telegraph.")]
        [Min(0)] public int startupFrames = 5;
        [Min(1)] public int activeFrames = 3;
        [Tooltip("Time after the hit before the attacker can act. On a boss move's last step this is the hero's punish window.")]
        [Min(0)] public int recoveryFrames = 12;

        [Header("Damage")]
        [Min(0)] public int damage = 1;
        [Tooltip("Added to the boss's stagger meter when this hits the boss.")]
        [Min(0f)] public float staggerDamage;
        public bool parryable = true;
        [Tooltip("Landing this cancels the rest of the boss's combo and makes him flinch (the hero's flash counter).")]
        public bool breaksCombo;
        public TelegraphCue cue = TelegraphCue.WhiteGlint;
        [Tooltip("Boss only: the cue (glint / full red glow) appears this many frames before the hit goes active — the 'press now' signal. The windup pose shows before it.")]
        [Min(0)] public int cueLeadFrames = 18;
        [Tooltip("Boss spells: parrying or clashing this attack lets the hero harvest the element.")]
        public Element element = Element.None;

        [Header("Hitbox (relative to the feet, facing right)")]
        public Vector2 hitboxOffset = new Vector2(1f, 1f);
        public Vector2 hitboxSize = new Vector2(1.4f, 1f);
        [Tooltip("Boss area attacks: the hitbox lands where the hero stood when the windup began (Hitbox Offset is then relative to the hero). With the floor marker cue: 'move out'.")]
        public bool atHero;

        [Header("Movement")]
        [Tooltip("Forward distance travelled during the attack (lunges). 0 = stand still.")]
        public float lungeDistance;
        [Tooltip("Lunge only during active frames (otherwise across startup + active).")]
        public bool lungeDuringActiveOnly = true;
        [Tooltip("Boss leap: rises this high during startup (out of reach) and slams down on the first active frame. With At Hero, the landing spot follows the hero until Leap Lock Frames before the hit, then locks: the 'move now' moment.")]
        [Min(0f)] public float leapHeight;
        [Min(0)] public int leapLockFrames = 20;

        [Header("Projectile (optional)")]
        public bool spawnsProjectile;
        public float projectileSpeed = 9f;
        public Vector2 projectileSize = new Vector2(1f, 0.7f);
        public float projectileLifetime = 2.5f;
        [Tooltip("Height of the projectile's bottom edge above the ground (0 = travels along the floor).")]
        public float projectileHeight;
        [Tooltip("Also send one the other way (a landing shockwave in both directions).")]
        public bool projectileBothWays;

        [Header("Feel (when it hits)")]
        [Min(0)] public int hitStopFrames = 4;
        [Range(0f, 1f)] public float shakeTrauma = 0.15f;

        [Header("Poses (art; empty falls back to the character's idle)")]
        public Sprite startupPose;
        public Sprite activePose;
        [Tooltip("Empty = hold the active pose through recovery.")]
        public Sprite recoveryPose;

        [Header("Animation (optional: a sprite sequence instead of the poses above)")]
        [Tooltip("Frames are retimed onto this attack's frame data: the ones before Hit Frame spread over startup, the rest over active + recovery.")]
        public Flipbook animation;
        [Tooltip("Index of the first frame that should show on the first active frame (the impact).")]
        [Min(0)] public int animationHitFrame;
        [Tooltip("Off = the animation ends with the active frames and recovery shows the poses above (for a sheet whose tail is unusable).")]
        public bool animationThroughRecovery = true;

        public bool HasAnimation => animation != null && animation.IsValid;

        bool AnimationAt(float t) => HasAnimation && (animationThroughRecovery || t < ActiveEnd);

        /// <summary>The pose for time t into the attack: the retimed animation if set, else the phase poses with fallbacks.</summary>
        public Sprite PoseAt(float t)
        {
            if (AnimationAt(t)) return AnimationFrameAt(t);
            return PhaseAt(t) switch
            {
                AttackPhase.Startup => startupPose,
                AttackPhase.Active => PoseSet.First(activePose, startupPose),
                _ => PoseSet.First(recoveryPose, activePose, startupPose),
            };
        }

        /// <summary>Size multiplier for the sprite shown at t (sprite sheets come back at their own resolution).</summary>
        public float PoseScaleAt(float t) => AnimationAt(t) ? animation.scale : 1f;

        Sprite AnimationFrameAt(float t)
        {
            var frames = animation.frames;
            int hit = Mathf.Clamp(animationHitFrame, 0, frames.Length - 1);
            if (t < ActiveStart && hit > 0)
                return frames[Mathf.Clamp(Mathf.FloorToInt(t / ActiveStart * hit), 0, hit - 1)];
            float after = (animationThroughRecovery ? TotalTime : ActiveEnd) - ActiveStart;
            int rest = frames.Length - hit;
            float u = after > 0f ? Mathf.Clamp01((t - ActiveStart) / after) : 1f;
            return frames[hit + Mathf.Min(rest - 1, Mathf.FloorToInt(u * rest))];
        }

        public float StartupTime => Frames.ToSeconds(startupFrames);
        public float ActiveTime => Frames.ToSeconds(activeFrames);
        public float RecoveryTime => Frames.ToSeconds(recoveryFrames);
        public float ActiveStart => StartupTime;
        public float ActiveEnd => StartupTime + ActiveTime;
        public float TotalTime => StartupTime + ActiveTime + RecoveryTime;
        /// <summary>When the telegraph cue appears (seconds into the attack).</summary>
        public float CueStart => Mathf.Max(0f, ActiveStart - Frames.ToSeconds(cueLeadFrames));

        public AttackPhase PhaseAt(float t)
        {
            if (t < ActiveStart) return AttackPhase.Startup;
            if (t < ActiveEnd) return AttackPhase.Active;
            if (t < TotalTime) return AttackPhase.Recovery;
            return AttackPhase.None;
        }

        /// <summary>
        /// True if the active window was live at any point between two sample times, so a slow frame
        /// (common in WebGL) can't jump straight from startup to recovery and skip the hit check.
        /// </summary>
        public bool ActiveBetween(float from, float to) => from < ActiveEnd && to >= ActiveStart;

        public Rect HitboxAt(Vector2 feet, int facing) => CombatBox.FromLocal(feet, hitboxOffset, hitboxSize, facing);

        /// <summary>Forward lunge speed (units per second) at time t; 0 outside the lunge window.</summary>
        public float LungeSpeedAt(float t)
        {
            if (lungeDistance == 0f) return 0f;
            float start = lungeDuringActiveOnly ? ActiveStart : 0f;
            float end = ActiveEnd;
            if (end <= start || t < start || t >= end) return 0f;
            return lungeDistance / (end - start);
        }
    }
}
