using System;
using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// A travelling hit: the boss's Shockwave Slam and Flame Wave, and the hero's elemental crescent.
    /// Spawned at runtime by its owner.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        AttackData data;
        Team team;
        IHittable target;
        Arena arena;
        int direction;
        float age;
        Element element;
        int extraDamage;
        Action<HitResult> onHit;

        public AttackData Attack => data;
        public Team Owner => team;
        public int Direction => direction;

        /// <param name="element">Harvested element carried to the target (hero waves).</param>
        /// <param name="extraDamage">Added to the attack's damage (fire infusion).</param>
        /// <param name="onHit">Called once when the projectile connects (hit or parried).</param>
        public void Launch(AttackData attack, Team owner, IHittable targetHittable, Vector2 feetPosition, int dir, Arena bounds,
            Element element = Element.None, int extraDamage = 0, Action<HitResult> onHit = null)
        {
            data = attack;
            team = owner;
            target = targetHittable;
            direction = dir;
            arena = bounds;
            this.element = element;
            this.extraDamage = extraDamage;
            this.onHit = onHit;
            transform.position = feetPosition + new Vector2(0f, attack.projectileHeight + attack.projectileSize.y * 0.5f);
            transform.localScale = new Vector3(attack.projectileSize.x, attack.projectileSize.y, 1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || data == null) return;

            age += dt;
            transform.position += new Vector3(direction * data.projectileSpeed * dt, 0f, 0f);

            if (target != null)
            {
                var rect = new Rect((Vector2)transform.position - data.projectileSize * 0.5f, data.projectileSize);
                if (rect.Overlaps(target.Hurtbox))
                {
                    var source = (Vector2)transform.position - new Vector2(direction, 0f);
                    var result = target.ReceiveHit(new HitInfo
                    {
                        attackerTeam = team, attack = data, sourcePosition = source, element = element, extraDamage = extraDamage,
                    });
                    if (result == HitResult.Hit || result == HitResult.Parried)
                    {
                        onHit?.Invoke(result);
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            bool outOfArena = arena != null && (transform.position.x < arena.minX - 2f || transform.position.x > arena.maxX + 2f);
            if (age >= data.projectileLifetime || outOfArena) Destroy(gameObject);
        }
    }
}
