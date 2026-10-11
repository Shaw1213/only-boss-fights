using UnityEngine;

namespace DreamLayerJam
{
    public enum Team { Hero, Boss }

    /// <summary>The shared danger language every form of the villain uses (docs/design/boss-01.md §2).</summary>
    public enum TelegraphCue
    {
        None,
        /// <summary>Parryable.</summary>
        WhiteGlint,
        /// <summary>Unparryable: dash.</summary>
        RedGlow,
        /// <summary>Area attack: move out.</summary>
        FloorMarker,
    }

    public enum AttackPhase { None, Startup, Active, Recovery }

    public enum HitResult { Hit, Parried, Dodged, Ignored }

    /// <summary>Spell elements. Parrying an elemental attack lets the hero harvest it (docs/design/boss-01.md, Harvest).</summary>
    public enum Element { None, Fire, Ice, Wood }

    public static class ElementColors
    {
        public static readonly Color Fire = new Color(1f, 0.55f, 0.1f);   // orange-yellow, never the warning red
        public static readonly Color Ice = new Color(0.75f, 0.92f, 1f);
        public static readonly Color Wood = new Color(0.35f, 0.9f, 0.3f);
        /// <summary>The Unknown One's grip: his root on the hero is his tentacles, not the hero's green wood.</summary>
        public static readonly Color Grip = new Color(0.66f, 0.42f, 0.80f);

        public static Color Of(Element e) => e switch
        {
            Element.Fire => Fire,
            Element.Ice => Ice,
            Element.Wood => Wood,
            _ => Color.white,
        };
    }

    public struct HitInfo
    {
        public Team attackerTeam;
        public AttackData attack;
        /// <summary>Where the hit came from (used for knockback direction).</summary>
        public Vector2 sourcePosition;
        /// <summary>A harvested element this hit carries (hero → boss): burn, freeze or root on landing.</summary>
        public Element element;
        /// <summary>Added on top of the attack's damage (fire infusion).</summary>
        public int extraDamage;

        public int Damage => attack != null ? attack.damage + extraDamage : 0;
        public float StaggerDamage => attack != null ? attack.staggerDamage : 0f;
        public bool Parryable => attack != null && attack.parryable;
    }

    /// <summary>Anything an attack can land on.</summary>
    public interface IHittable
    {
        Team Team { get; }
        Rect Hurtbox { get; }
        HitResult ReceiveHit(in HitInfo hit);
    }

    public static class CombatBox
    {
        /// <summary>A world-space rect from an offset/size authored relative to the feet, facing right.</summary>
        public static Rect FromLocal(Vector2 feet, Vector2 offset, Vector2 size, int facing)
        {
            var center = feet + new Vector2(offset.x * facing, offset.y);
            return new Rect(center - size * 0.5f, size);
        }
    }

    public static class CombatDebug
    {
        /// <summary>Draw active hitboxes in the Game view (toggle with F1).</summary>
        public static bool ShowHitboxes = false;
    }
}
