using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// A sword trail drawn over an attack's active frames: a crescent stretched over the hitbox that
    /// flares and fades. It sells a fast swing the way anime does, with a smear instead of extra frames.
    /// The shape is generated once at runtime (no art asset). Scaled time, so hit-stop freezes it on
    /// the impact frame.
    /// </summary>
    public class SlashArc : MonoBehaviour
    {
        [Tooltip("Fade-out time after the swing starts.")]
        public float duration = 0.16f;

        SpriteRenderer sr;
        Rect rect;
        int facing = 1;
        Color color;
        float age = -1f;

        static Sprite shape;

        /// <summary>A crescent bulging to the right (the facing direction), bright along its outer rim.</summary>
        public static Sprite Shape
        {
            get
            {
                if (shape != null) return shape;
                const int w = 256, h = 160;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SlashArc" };
                var px = new Color32[w * h];
                // Half-ellipse centered on the left edge, in normalized space: d = 1 touches the right edge
                // and the top/bottom. The band between d 0.62 and 0.98 is the trail, brightest near the rim.
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w;
                        float v = (y + 0.5f) / h * 2f - 1f;
                        float d = Mathf.Sqrt(u * u + v * v);
                        float ang = Mathf.Abs(Mathf.Atan2(v, u) * Mathf.Rad2Deg);
                        float rim = Smooth(0.62f, 0.9f, d) * (1f - Smooth(0.9f, 0.98f, d));
                        float tips = 1f - Smooth(55f, 85f, ang);
                        float a = Mathf.Clamp01(rim * tips);
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                shape = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w); // 1 x 0.625 units
                return shape;
            }
        }

        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        /// <summary>Places a renderer as the arc over a hitbox at progress 0..1 (0 = the swing, 1 = gone).</summary>
        public static void Place(SpriteRenderer r, Rect hit, int facing, Color c, float progress)
        {
            float p = Mathf.Clamp01(progress);
            var size = Shape.bounds.size;
            float grow = 1f + 0.15f * p;
            r.sprite = Shape;
            r.flipX = facing < 0;
            r.color = new Color(c.r, c.g, c.b, c.a * Mathf.Pow(1f - p, 1.5f));
            r.transform.position = new Vector3(hit.center.x + 0.1f * p * facing, hit.center.y, 0f);
            r.transform.localScale = new Vector3(hit.width / size.x * grow, hit.height / size.y * grow, 1f);
        }

        /// <summary>Adds an arc renderer under <paramref name="owner"/> (world-space, drawn above characters).</summary>
        public static SlashArc Create(Transform owner, int sortingOrder)
        {
            var go = new GameObject("SlashArc");
            go.transform.SetParent(owner, false);
            var arc = go.AddComponent<SlashArc>();
            arc.sr = go.AddComponent<SpriteRenderer>();
            arc.sr.sortingOrder = sortingOrder;
            arc.sr.enabled = false;
            return arc;
        }

        public void Play(Rect hit, int dir, Color c)
        {
            rect = hit;
            facing = dir;
            color = c;
            age = 0f;
            sr.enabled = true;
            Place(sr, rect, facing, color, 0f);
        }

        void Update()
        {
            if (age < 0f) return;
            age += Time.deltaTime; // scaled: hit-stop holds the arc on the impact frame
            float p = duration > 0f ? age / duration : 1f;
            if (p >= 1f)
            {
                age = -1f;
                sr.enabled = false;
                return;
            }
            Place(sr, rect, facing, color, p);
        }
    }
}
