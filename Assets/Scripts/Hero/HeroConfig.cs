using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>The hero kit (docs/design/boss-01.md §1). Frames at 60 fps.</summary>
    [CreateAssetMenu(menuName = "DreamLayer Jam/Hero Config", fileName = "HeroConfig")]
    public class HeroConfig : ScriptableObject
    {
        [Header("Health")]
        [Min(1)] public int maxHealth = 5;
        public float hurtInvulnerableSeconds = 1f;
        [Min(0)] public int hitstunFrames = 12;
        public float hurtKnockbackSpeed = 7f;

        [Header("Movement")]
        public float moveSpeed = 6f;
        public float acceleration = 90f;
        public float deceleration = 70f;

        [Header("Dash")]
        [Min(0)] public int dashStartupFrames = 2;
        [Tooltip("The dash moves during these frames and is invulnerable.")]
        [Min(1)] public int dashInvulnerableFrames = 12;
        [Min(0)] public int dashRecoveryFrames = 8;
        public float dashDistance = 3.5f;
        public float dashCooldownSeconds = 0.4f;
        [Tooltip("The dash goes through the boss's body (the way out of a corner). Off: it stops at him like walking does.")]
        public bool dashPassesThroughBoss = true;

        [Header("Parry button")]
        public bool dedicatedParryEnabled = true;
        [Min(1)] public int parryWindowFrames = 14;
        [Tooltip("Vulnerable recovery when the parry catches nothing.")]
        [Min(0)] public int parryWhiffRecoveryFrames = 16;

        [Header("Attack-parry (clash)")]
        [Tooltip("Attack into a hit to parry it. Only attacks started from neutral count (not combo follow-ups), so mashing doesn't parry everything.")]
        public bool attackParryEnabled = true;
        [Tooltip("A neutral attack parries any parryable hit landing within this many frames of the press.")]
        [Min(0)] public int attackParryWindowFrames = 10;

        [Header("Riposte")]
        [Tooltip("After a successful parry, an attack press within this time becomes a riposte — but only while the boss is open (not mid-combo).")]
        public float riposteWindowSeconds = 0.5f;

        [Header("Flash Counter (the clash's own reward)")]
        [Tooltip("After a clash (attack-parry), a dash within this time (neutral or toward the boss) flash-steps through him and slashes from behind; the dash also cancels a swing, charge or parry started after the clash. Unlike the riposte it works mid-combo, so clashing is the expert answer. Dashing away stays a normal dash.")]
        public float flashCounterWindowSeconds = 0.7f;
        public AttackData flashCounter;
        [Tooltip("The boss must be this close (x) for the flash step; a clashed projectile from far away gives no counter.")]
        public float flashCounterRange = 5f;
        [Tooltip("Space between the boss's hurtbox and the hero's after the step.")]
        public float flashCounterGap = 0.35f;

        [Header("Attacks")]
        public AttackData[] lightCombo;
        [Tooltip("Idle time after which the light combo starts over from the first hit.")]
        public float comboResetSeconds = 0.3f;
        public AttackData chargedHeavy;
        [Tooltip("Hold the attack button this long to start charging; release to swing.")]
        public float chargeHoldSeconds = 0.35f;
        public AttackData riposte;
        public bool dashAttackEnabled = true;
        public AttackData dashAttack;
        [Tooltip("An attack pressed this soon after a dash ends becomes a dash attack.")]
        public float dashAttackWindowSeconds = 0.12f;

        [Header("Harvest (parry or clash a spell to load its element into the blade)")]
        public bool harvestEnabled = true;
        [Tooltip("Fired by a charged heavy while an element is held: a crescent that carries the element. Uses this attack's damage, stagger, feel and projectile fields.")]
        public AttackData elementWave;
        [Tooltip("Fire: the infused hit deals this multiple of its damage (rounded up)...")]
        public float fireDamageMultiplier = 1.5f;
        [Tooltip("...then the boss burns: this many ticks of damage.")]
        [Min(0)] public int fireBurnTicks = 3;
        [Min(0)] public int fireBurnDamage = 1;
        public float fireBurnIntervalSeconds = 0.8f;
        [Tooltip("Ice: the boss's clock stops (no movement, no attacks, windups hold) for this long.")]
        public float iceFreezeSeconds = 1.5f;
        [Tooltip("Wood: the boss can't move (no approach, lunges stay in place) for this long...")]
        public float woodRootSeconds = 2.5f;
        [Tooltip("...and the hero heals this much.")]
        [Min(0)] public int woodHeal = 1;

        [Header("Boss spells on the hero (a hit that isn't parried)")]
        [Tooltip("Fire: burn ticks after the hit. A dash puts it out.")]
        [Min(0)] public int heroBurnTicks = 2;
        [Min(0)] public int heroBurnDamage = 1;
        public float heroBurnIntervalSeconds = 1f;
        [Tooltip("Ice: chilled for this long: slower walk, slower dash recharge.")]
        public float chillSeconds = 2.5f;
        [Range(0.1f, 1f)] public float chillMoveMultiplier = 0.5f;
        public float chillDashCooldownMultiplier = 2f;
        [Tooltip("Wood: rooted for this long: no walking or dashing (attack and parry still work).")]
        public float rootSeconds = 1.2f;

        [Header("Input")]
        [Tooltip("An early press fires on the first frame it is allowed, if within this time.")]
        public float inputBufferSeconds = 0.12f;

        [Header("Hurtbox (relative to the feet)")]
        public Vector2 hurtboxOffset = new Vector2(0f, 0.9f);
        public Vector2 hurtboxSize = new Vector2(0.8f, 1.8f);
    }
}
