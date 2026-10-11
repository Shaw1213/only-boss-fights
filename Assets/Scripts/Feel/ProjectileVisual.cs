using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// What a travelling hit looks like, generated at runtime (no art asset), in the game's cel style: flat
    /// color bands with crisp edges.
    ///   Flame: a rolling wall of fire tongues (the knight's Flame Wave), flickering through 6 frames.
    ///   Shockwave: a red crest rolling along the floor (the unparryable slam), with afterimages.
    ///   Crescent: the hero's harvested wave in its element color, with afterimages.
    /// Scaled time, so hit-stop freezes it with everything else.
    /// </summary>
    public class ProjectileVisual : MonoBehaviour
    {
        public enum Style { Flame, Shockwave, Crescent }

        const float FlameFps = 14f;
        const float TrailInterval = 0.04f;

        Style style;
        SpriteRenderer sr;
        AfterimageTrail trail;
        Sprite[] frames;
        Vector3 baseScale;
        float age, trailTimer;

        static Sprite[] flameFrames, shockFrames;

        /// <summary>
        /// Replaces a projectile's placeholder square with a generated visual. <paramref name="groundY"/> is the
        /// floor; flames and the shockwave grow up from it whatever the hitbox height.
        /// </summary>
        public static void Attach(GameObject projectile, Style style, Color color, int direction, Vector2 hitSize, float groundY)
        {
            if (projectile.TryGetComponent<SpriteRenderer>(out var placeholder)) Destroy(placeholder);
            projectile.transform.localScale = Vector3.one; // the hitbox comes from the attack data, not the scale

            var go = new GameObject("Visual");
            go.transform.SetParent(projectile.transform, false);
            var v = go.AddComponent<ProjectileVisual>();
            v.style = style;
            v.sr = go.AddComponent<SpriteRenderer>();
            v.sr.sortingOrder = 4;
            v.sr.flipX = direction < 0; // drawn travelling right

            float centerY = projectile.transform.position.y;
            Vector2 size;
            switch (style)
            {
                case Style.Flame:
                    v.frames = FlameFrames;
                    v.sr.color = Color.white; // the fire carries its own colors
                    size = new Vector2(hitSize.x * 1.5f, (centerY + hitSize.y * 0.5f - groundY) + 0.5f);
                    go.transform.localPosition = new Vector3(0f, groundY - centerY + size.y * 0.5f, 0f);
                    break;
                case Style.Shockwave:
                    v.frames = ShockFrames;
                    v.sr.color = Color.white;
                    size = new Vector2(hitSize.x * 1.8f, hitSize.y * 1.6f);
                    go.transform.localPosition = new Vector3(0f, groundY - centerY + size.y * 0.5f, 0f);
                    v.trail = AfterimageTrail.Create("Shockwave Trail", 3);
                    v.trail.tint = new Color(1f, 0.3f, 0.2f, 0.5f);
                    v.trail.lifetime = 0.22f;
                    break;
                default:
                    v.frames = new[] { SlashArc.Shape };
                    v.sr.color = color;
                    size = new Vector2(hitSize.x * 1.3f, hitSize.y * 1.15f);
                    v.trail = AfterimageTrail.Create("Wave Trail", 3);
                    v.trail.tint = new Color(color.r, color.g, color.b, 0.45f);
                    v.trail.lifetime = 0.16f;
                    break;
            }
            v.sr.sprite = v.frames[0];
            var b = v.frames[0].bounds.size;
            v.baseScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
            go.transform.localScale = v.baseScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            if (frames.Length > 1) sr.sprite = frames[(int)(age * FlameFps) % frames.Length];
            // A little life: flames flicker taller and shorter, the shockwave swells as it rolls.
            float pulse = style == Style.Flame ? 1f + 0.06f * Mathf.Sin(age * 31f) : style == Style.Shockwave ? 1f + 0.08f * Mathf.Sin(age * 22f) : 1f;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * pulse, 1f);
            if (trail != null && (trailTimer -= dt) <= 0f)
            {
                trailTimer = TrailInterval;
                trail.Spawn(sr);
            }
        }

        void OnDestroy()
        {
            if (trail != null) Destroy(trail.gameObject, trail.lifetime + 0.05f);
        }

        // ------------------------------------------------------------------ generated shapes

        static Sprite[] FlameFrames => Alive(flameFrames) ? flameFrames : flameFrames = Build(6, 96, 128, Flame);
        static Sprite[] ShockFrames => Alive(shockFrames) ? shockFrames : shockFrames = Build(4, 128, 80, Shock);

        // Unity's null check, not C#'s: sprites generated in an earlier Play session are destroyed when it ends,
        // but with domain reload off (Enter Play Mode options) the static array that points at them survives.
        static bool Alive(Sprite[] cached) => cached != null && cached.Length > 0 && cached[0] != null;

        delegate Color32 Shade(float u, float v, int frame);

        static Sprite[] Build(int count, int w, int h, Shade shade)
        {
            var result = new Sprite[count];
            for (int f = 0; f < count; f++)
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = $"Projectile{f}" };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = shade((x + 0.5f) / w, (y + 0.5f) / h, f);
                tex.SetPixels32(px);
                tex.Apply();
                result[f] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w); // 1 unit wide
            }
            return result;
        }

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>Fire tongues over a crest that leans forward; yellow core, orange body, red tips, crisp bands.</summary>
        static Color32 Flame(float u, float v, int frame)
        {
            float t = frame / 6f * Mathf.PI * 2f;
            // The wall is tallest just behind its front edge and tapers toward the back.
            float crest = Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, u / 0.7f)) * (u > 0.85f ? Mathf.Lerp(1f, 0.55f, (u - 0.85f) / 0.15f) : 1f);
            float tongues = 0.62f + 0.22f * Mathf.PerlinNoise(u * 3.2f + Mathf.Cos(t) * 0.6f, frame * 0.53f)
                          + 0.16f * (1f - Mathf.Abs(Mathf.Sin(u * Mathf.PI * 4.5f + t)));
            float top = crest * tongues;
            if (v > top) return Clear;
            float k = v / Mathf.Max(0.01f, top);                       // 0 at the floor, 1 at the tip
            float core = Mathf.Abs(u - 0.6f) * 1.6f + k;                 // hottest low and toward the front
            if (top - v < 0.035f) return new Color32(150, 24, 12, 255);  // a thin dark rim keeps the edge crisp
            if (core < 0.62f) return new Color32(255, 240, 170, 255);
            if (core < 0.98f) return new Color32(255, 150, 40, 255);
            return new Color32(228, 54, 24, 255);
        }

        /// <summary>A rolling crest: a bright leading rim over a translucent red body, dust at its foot.</summary>
        static Color32 Shock(float u, float v, int frame)
        {
            float wobble = 0.05f * Mathf.Sin(u * 18f + frame * 1.7f);
            float top = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI * 0.92f + 0.12f), 1.6f) * (0.55f + 0.45f * u) + wobble;
            if (v > top || top <= 0f) return Clear;
            float edge = top - v;
            if (edge < 0.07f) return new Color32(255, 225, 205, 255);                 // white-hot rim
            if (edge < 0.18f) return new Color32(255, 70, 45, 235);                   // red band
            if (v < 0.16f && Mathf.PerlinNoise(u * 9f, frame * 0.8f) > 0.45f)
                return new Color32(120, 92, 78, 220);                                 // dust kicked up at the floor
            return new Color32(170, 24, 20, (byte)Mathf.Lerp(200f, 60f, 1f - u));     // body, fading toward the back
        }
    }
}
