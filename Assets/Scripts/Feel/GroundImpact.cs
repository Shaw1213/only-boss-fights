using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// A heavy body hitting the floor (the final form's Leap Smash): a dust ring spreads along the floor and fades,
    /// stone chips fly up and fall back, and a crack stays under the landing spot through the punish window.
    /// Generated at runtime like IceSpikes (no art asset, 0 credits).
    /// </summary>
    public class GroundImpact : MonoBehaviour
    {
        const float RingSeconds = 0.45f;
        const float CrackHold = 0.9f, CrackFade = 0.4f;
        const float Gravity = 26f;
        const int CrackOrder = 0, RingOrder = 1, ChipOrder = 4; // floor 0 < ring 1 < boss 2 < hero 3 < chips 4

        static Sprite ringSprite, chipSprite, crackSprite;

        struct Chip
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector2 velocity;
            public float spin;
        }

        SpriteRenderer ring, crack;
        Chip[] chips;
        float width, groundY, age;
        Color dust;

        /// <summary>A landing at <paramref name="x"/> on the floor at <paramref name="groundY"/>; the dust ring spreads to
        /// <paramref name="width"/> units.</summary>
        public static void Play(float x, float groundY, float width, Color dust)
        {
            var go = new GameObject("GroundImpact");
            go.transform.position = new Vector3(x, groundY, 0f);
            var g = go.AddComponent<GroundImpact>();
            g.width = width;
            g.groundY = groundY;
            g.dust = dust;
            g.ring = Part(go.transform, "Ring", RingShape, RingOrder);
            g.crack = Part(go.transform, "Crack", CrackShape, CrackOrder);
            var cb = CrackShape.bounds.size;
            g.crack.transform.localScale = new Vector3(width * 0.55f / cb.x, 0.32f / cb.y, 1f);
            g.crack.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            g.crack.flipX = Random.value < 0.5f;

            g.chips = new Chip[10];
            for (int i = 0; i < g.chips.Length; i++)
            {
                var sr = Part(go.transform, "Chip", ChipShape, ChipOrder);
                float side = i % 2 == 0 ? -1f : 1f;
                sr.transform.localPosition = new Vector3(side * Random.Range(0.1f, 0.6f), 0.05f, 0f);
                float size = Random.Range(0.1f, 0.22f);
                sr.transform.localScale = Vector3.one * size / ChipShape.bounds.size.x;
                sr.color = Color.Lerp(new Color(0.24f, 0.2f, 0.28f), new Color(0.5f, 0.46f, 0.54f), Random.value);
                g.chips[i] = new Chip
                {
                    t = sr.transform, sr = sr, spin = Random.Range(-720f, 720f),
                    velocity = new Vector2(side * Random.Range(1.5f, 6f), Random.Range(4f, 9f)),
                };
            }
            g.Update();
        }

        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, int order)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime; // 0 in hit-stop: the impact freezes with everything else
            age += dt;

            // The dust ring: fast out, then slowing; flat on the floor (seen from the side).
            float r = Mathf.Clamp01(age / RingSeconds);
            float spread = 1f - (1f - r) * (1f - r);
            var rb = RingShape.bounds.size;
            float w = Mathf.Lerp(width * 0.25f, width, spread);
            ring.transform.localScale = new Vector3(w / rb.x, w * 0.16f / rb.y, 1f);
            var c = dust;
            c.a = dust.a * (1f - r);
            ring.color = c;

            float crackAlpha = 1f - Mathf.Clamp01((age - CrackHold) / CrackFade);
            crack.color = new Color(1f, 1f, 1f, crackAlpha);

            for (int i = 0; i < chips.Length; i++)
            {
                var ch = chips[i];
                if (!ch.sr.enabled) continue;
                ch.velocity.y -= Gravity * dt;
                var p = ch.t.position + (Vector3)(ch.velocity * dt);
                if (p.y < groundY && ch.velocity.y < 0f) ch.sr.enabled = false; // back into the floor
                ch.t.position = p;
                ch.t.Rotate(0f, 0f, ch.spin * dt);
                chips[i] = ch;
            }

            if (age > CrackHold + CrackFade) Destroy(gameObject);
        }

        // Unity's null check: a sprite generated in an earlier Play session is destroyed when it ends, but with domain
        // reload off the static that points at it survives.
        static Sprite RingShape => ringSprite != null ? ringSprite : ringSprite = BuildRing();
        static Sprite ChipShape => chipSprite != null ? chipSprite : chipSprite = BuildChip();
        static Sprite CrackShape => crackSprite != null ? crackSprite : crackSprite = BuildCrack();

        static Sprite Make(Texture2D tex, Color32[] px, Vector2 pivot)
        {
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, tex.width);
        }

        /// <summary>A soft-edged ring (annulus), white: tinted by the dust color.</summary>
        static Sprite BuildRing()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ImpactRing" };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = new Vector2(x + 0.5f - s * 0.5f, y + 0.5f - s * 0.5f).magnitude / (s * 0.5f);
                    // A thick band near the edge, fading inward like a dust wave.
                    float a = Mathf.Clamp01((d - 0.55f) / 0.3f) * Mathf.Clamp01((1f - d) / 0.08f);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            return Make(tex, px, new Vector2(0.5f, 0.5f));
        }

        /// <summary>A small cel-shaded stone chip: a lit face, a shaded face, a dark outline.</summary>
        static Sprite BuildChip()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ImpactChip" };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - s * 0.5f), dy = Mathf.Abs(y + 0.5f - s * 0.5f);
                    float d = dx * 0.8f + dy; // a squat diamond
                    Color c = d > 7.5f ? Color.clear : d > 6f ? new Color(0.08f, 0.06f, 0.1f) : x + y < s ? Color.white : new Color(0.7f, 0.7f, 0.72f);
                    px[y * s + x] = c;
                }
            return Make(tex, px, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Dark jagged cracks branching out from the center, drawn flat (they lie on the floor).</summary>
        static Sprite BuildCrack()
        {
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ImpactCrack" };
            var px = new Color32[w * h];
            var ink = new Color32(18, 12, 24, 230);
            var rng = new System.Random(7); // the same crack every time
            for (int branch = 0; branch < 7; branch++)
            {
                float dir = branch % 2 == 0 ? 1f : -1f;
                float x = w * 0.5f, y = h * 0.5f + (float)(rng.NextDouble() - 0.5) * 8f;
                float slope = (float)(rng.NextDouble() - 0.5) * 0.9f;
                int length = 40 + rng.Next(80);
                for (int i = 0; i < length; i++)
                {
                    x += dir;
                    slope += (float)(rng.NextDouble() - 0.5) * 0.35f;
                    y += Mathf.Clamp(slope, -1.2f, 1.2f);
                    int thick = i < length * 0.4f ? 2 : 1; // thinner toward the tips
                    for (int oy = -thick; oy <= thick; oy++)
                    {
                        int px0 = Mathf.RoundToInt(x), py = Mathf.RoundToInt(y) + oy;
                        if (px0 < 0 || px0 >= w || py < 0 || py >= h) continue;
                        px[py * w + px0] = ink;
                    }
                }
            }
            return Make(tex, px, new Vector2(0.5f, 0.5f));
        }
    }
}
