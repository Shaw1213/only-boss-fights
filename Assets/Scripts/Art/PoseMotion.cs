using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The procedural motion layered on top of pose sprites (squash/stretch and lean per attack phase).
    /// One source of truth shared by the fighters and the Animation Gym, so the gym shows exactly what
    /// plays in the fight.
    /// </summary>
    public static class PoseMotion
    {
        /// <summary>Art mode: state tints are a light wash over the drawing (greybox boxes take the full color).</summary>
        public const float ArtTint = 0.45f;
        /// <summary>Art mode: squash/stretch is toned down so the drawing doesn't smear.</summary>
        public const float ArtDeform = 0.5f;

        public struct Motion
        {
            public Vector2 squash;
            /// <summary>Horizontal offset toward the facing direction (world units).</summary>
            public float lean;

            public Motion(float x, float y, float lean)
            {
                squash = new Vector2(x, y);
                this.lean = lean;
            }

            public static readonly Motion None = new Motion(1f, 1f, 0f);
        }

        /// <summary>Hero attacks: coil back in startup, stretch forward on the hit.</summary>
        public static Motion HeroAttack(AttackPhase phase) => phase switch
        {
            AttackPhase.Startup => new Motion(0.9f, 1.05f, -0.1f),
            AttackPhase.Active => new Motion(1.2f, 0.92f, 0.2f),
            _ => Motion.None,
        };

        /// <summary>
        /// Boss attacks: the windup builds over the startup (<paramref name="windup01"/> 0..1), the hit
        /// lunges forward, and the last step's recovery (the punish window) sags.
        /// </summary>
        public static Motion BossAttack(AttackPhase phase, float windup01, bool lastStep) => phase switch
        {
            AttackPhase.Startup => new Motion(1f + 0.08f * windup01, 1f - 0.06f * windup01, -0.3f * windup01),
            AttackPhase.Active => new Motion(0.92f, 1.06f, 0.4f),
            AttackPhase.Recovery when lastStep => new Motion(1.04f, 0.94f, 0f),
            _ => Motion.None,
        };

        /// <summary>A boss leap's beats (seconds): a crouch, the jump up out of reach, then the hang, and a fast drop
        /// that lands on the first active frame.</summary>
        public const float LeapCrouch = 0.12f, LeapRise = 0.25f, LeapFall = 0.1f, LeapLandSquash = 0.14f;

        /// <summary>
        /// A boss leap at <paramref name="t"/> seconds into the step (the hit lands at <paramref name="land"/>):
        /// it gathers in a crouch, stretches on the way up and on the drop, and squashes flat when it lands.
        /// </summary>
        public static Motion BossLeap(float t, float land)
        {
            if (t >= land)
            {
                float l = 1f - Mathf.Clamp01((t - land) / LeapLandSquash);
                return new Motion(1f + 0.3f * l, 1f - 0.3f * l, 0f);
            }
            if (t < LeapCrouch)
            {
                float k = t / LeapCrouch;
                return new Motion(1f + 0.14f * k, 1f - 0.2f * k, 0f);
            }
            if (t < LeapCrouch + LeapRise) return new Motion(0.88f, 1.14f, 0f);
            if (land - t < LeapFall) return new Motion(0.86f, 1.18f, 0f);
            return Motion.None;
        }

        /// <summary>Applies a squash to a base scale; art mode tones it down.</summary>
        public static Vector3 Deform(Vector3 baseScale, float x, float y, bool art) => art
            ? Vector3.Scale(baseScale, new Vector3(Mathf.Lerp(1f, x, ArtDeform), Mathf.Lerp(1f, y, ArtDeform), 1f))
            : Vector3.Scale(baseScale, new Vector3(x, y, 1f));
    }
}
