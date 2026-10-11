using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Solid bodies (side view): the hero and the boss can't walk through each other. Whoever moved into the other this
    /// frame is stopped at the contact line (walking into him, his lunge into the hero); if neither did (he landed on
    /// the hero, a dash ended inside him), the hero is eased out. A wall never traps anyone: what one side can't give,
    /// the other does. The hero's dash and the flash counter's step pass through (they're the ways past him), and a boss
    /// in the air is out of the way. Bodies are the hurtbox widths.
    /// </summary>
    public class BodyBlock
    {
        /// <summary>Units per second the hero is eased out when nobody walked into anybody.</summary>
        const float EaseOutSpeed = 10f;

        float prevHeroX, prevBossX;
        int side; // +1: the hero belongs on the boss's right
        bool primed;

        public void Resolve(HeroController hero, BossController boss, Arena arena, float dt)
        {
            if (hero == null || boss == null || hero.config == null || boss.config == null) return;
            var ht = hero.transform;
            var bt = boss.transform;
            float hx = ht.position.x, bx = bt.position.x;
            if (!primed)
            {
                prevHeroX = hx;
                prevBossX = bx;
                side = hx >= bx ? 1 : -1;
                primed = true;
            }

            float heroHalf = hero.config.hurtboxSize.x * 0.5f, bossHalf = boss.config.hurtboxSize.x * 0.5f;
            float contact = heroHalf + bossHalf;
            bool solid = boss.config.solidBody && !hero.PassesThroughBoss &&
                         hero.CurrentState != HeroController.State.Dead && boss.CurrentState != BossController.State.Dead &&
                         boss.Hurtbox.yMin < hero.Hurtbox.yMax; // up in a leap: out of the way

            if (!solid || Mathf.Abs(hx - bx) >= contact)
            {
                if (Mathf.Abs(hx - bx) > 0.001f) side = hx > bx ? 1 : -1;
                prevHeroX = hx;
                prevBossX = bx;
                return;
            }

            // Measured from the side the hero belongs on, so a fast lunge that crossed his center still gets put back.
            float overlap = contact - (hx - bx) * side;
            float heroIn = Mathf.Max(0f, (prevHeroX - hx) * side); // the hero's move toward him this frame
            float bossIn = Mathf.Max(0f, (bx - prevBossX) * side); // his move toward the hero
            bool someoneWalkedIn = heroIn + bossIn > 1e-5f;
            float push = someoneWalkedIn ? overlap : Mathf.Min(overlap, EaseOutSpeed * dt);
            float heroShare = someoneWalkedIn ? heroIn / (heroIn + bossIn) : 1f;

            float newHx = hx + side * push * heroShare;
            float newBx = bx - side * push * (1f - heroShare);
            if (arena != null)
            {
                float c = arena.Clamp(newHx, heroHalf); // the hero is at a wall: he gives instead
                newBx -= side * Mathf.Abs(newHx - c);
                newHx = c;
                c = arena.Clamp(newBx, bossHalf); // he's at a wall: the hero gives
                newHx += side * Mathf.Abs(newBx - c);
                newBx = c;
                newHx = arena.Clamp(newHx, heroHalf);
            }
            SetX(ht, newHx);
            SetX(bt, newBx);
            prevHeroX = newHx;
            prevBossX = newBx;
        }

        static void SetX(Transform t, float x)
        {
            var p = t.position;
            if (p.x == x) return;
            p.x = x;
            t.position = p;
        }
    }
}
