using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The King's floor attacks (BossConfig.floorStyle = IceSpikes): a frost patch spreads where the floor will erupt
    /// (the telegraph, instead of the plain marker bar), then a cluster of jagged ice spikes shoots up across the
    /// hitbox, holds while it's live, and shatters. The spikes rise from the center outward; back spikes stand behind
    /// the fighters and front ones in front, for depth. Generated at runtime (no art asset, 0 credits).
    /// </summary>
    public class IceSpikes : MonoBehaviour
    {
        const float RiseSeconds = 0.07f;
        const float ShatterSeconds = 0.3f;
        const int BackOrder = 1, FrontOrder = 4; // floor 0 < back spikes < boss 2 < hero 3 < front spikes

        static Sprite spikeSprite, patchSprite;

        struct Spike
        {
            public Transform t;
            public SpriteRenderer sr;
            public float width, height, delay;
            public Color color;
        }

        Spike[] spikes;
        float age;
        float hold;

        /// <summary>Erupts across <paramref name="area"/> (world space; its bottom is the floor) and stays up for
        /// <paramref name="holdSeconds"/> before shattering.</summary>
        public static void Erupt(Rect area, float groundY, float holdSeconds)
        {
            var go = new GameObject("IceSpikes");
            var ice = go.AddComponent<IceSpikes>();
            ice.hold = holdSeconds;
            int n = Mathf.Max(3, Mathf.RoundToInt(area.width / 0.4f));
            ice.spikes = new Spike[n];
            for (int i = 0; i < n; i++)
            {
                float u = n > 1 ? i / (n - 1f) : 0.5f;
                float centered = 1f - Mathf.Abs(u - 0.5f) * 2f; // 1 in the middle, 0 at the ends
                bool front = i % 2 == 1;
                var t = new GameObject("Spike").transform;
                t.SetParent(go.transform, false);
                float x = Mathf.Lerp(area.xMin + 0.15f, area.xMax - 0.15f, u) + Random.Range(-0.06f, 0.06f);
                t.position = new Vector3(x, groundY, 0f);
                // Lean outward from the middle, like ice bursting up through the floor.
                t.localRotation = Quaternion.Euler(0f, 0f, (0.5f - u) * 24f + Random.Range(-4f, 4f));
                var sr = t.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = SpikeShape;
                sr.sortingOrder = front ? FrontOrder : BackOrder;
                var c = front ? Color.white : new Color(0.78f, 0.86f, 0.95f); // back row a shade darker
                sr.color = c;
                ice.spikes[i] = new Spike
                {
                    t = t, sr = sr, color = c,
                    width = Random.Range(0.42f, 0.6f) * (front ? 1f : 1.1f),
                    height = area.height * (0.55f + 0.45f * centered) * Random.Range(0.88f, 1.04f) * (front ? 0.9f : 1f),
                    delay = (1f - centered) * 0.05f,
                };
                ice.Apply(ref ice.spikes[i], 0f, 1f);
            }
        }

        void Update()
        {
            age += Time.deltaTime; // 0 in hit-stop: the eruption freezes with everything else
            float maxDelay = 0f;
            for (int i = 0; i < spikes.Length; i++)
            {
                maxDelay = Mathf.Max(maxDelay, spikes[i].delay);
                float t = age - spikes[i].delay;
                float rise = Mathf.Clamp01(t / RiseSeconds);
                // Overshoot a little and settle: the ice punches up, then sets.
                float k = rise < 1f ? EaseOutBack(rise) : 1f;
                float shatter = Mathf.Clamp01((age - hold) / ShatterSeconds);
                Apply(ref spikes[i], k * (1f - 0.5f * shatter), 1f - shatter);
            }
            if (age > hold + ShatterSeconds + maxDelay) Destroy(gameObject);
        }

        void Apply(ref Spike s, float heightK, float alpha)
        {
            var b = SpikeShape.bounds.size;
            s.t.localScale = new Vector3(s.width / b.x, Mathf.Max(0.0001f, s.height * heightK) / b.y, 1f);
            var c = s.color;
            c.a = alpha;
            s.sr.color = c;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // ---------------------------------------------------------------- the frost patch (telegraph)

        /// <summary>Places the frost patch for a windup: <paramref name="k"/> is the windup's progress (it spreads and
        /// brightens), <paramref name="cueOn"/> adds the pulse once the danger cue has sounded.</summary>
        public static void ShowPatch(SpriteRenderer patch, Rect area, float groundY, float k, bool cueOn)
        {
            patch.sprite = PatchShape;
            patch.enabled = true;
            float spread = Mathf.Lerp(0.6f, 1.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f)));
            var b = PatchShape.bounds.size;
            patch.transform.position = new Vector3(area.center.x, groundY + 0.03f, 0f);
            patch.transform.localScale = new Vector3(area.width * spread / b.x, 0.5f / b.y, 1f);
            float pulse = cueOn ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
            var c = Color.Lerp(new Color(0.55f, 0.85f, 1f), Color.white, 0.5f * pulse);
            c.a = Mathf.Lerp(0.45f, 0.95f, k);
            patch.color = c;
        }

        // Unity's null check: a sprite generated in an earlier Play session is destroyed when it ends, but with domain
        // reload off the static that points at it survives.
        static Sprite SpikeShape => spikeSprite != null ? spikeSprite : spikeSprite = BuildSpike();
        static Sprite PatchShape => patchSprite != null ? patchSprite : patchSprite = BuildPatch();

        /// <summary>A cel-shaded ice spike, pivot at its base: a light left facet, a darker right facet, a dark outline
        /// (the same line language as the characters) and a white glint down the lit edge.</summary>
        static Sprite BuildSpike()
        {
            const int w = 64, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "IceSpike" };
            var px = new Color32[w * h];
            Color outline = new Color(0.10f, 0.20f, 0.33f);
            Color lit = new Color(0.88f, 0.97f, 1f);
            Color mid = new Color(0.60f, 0.82f, 0.97f);
            Color shade = new Color(0.38f, 0.60f, 0.86f);
            for (int y = 0; y < h; y++)
            {
                float half = (1f - y / (h - 1f)) * (w * 0.5f - 1f);
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - w * 0.5f;
                    float inside = half - Mathf.Abs(dx); // px from the nearest side edge
                    Color c;
                    if (inside <= 0f) c = Color.clear;
                    else if (inside < 3f) { c = outline; c.a = Mathf.Clamp01(inside); }
                    else if (dx < 0f && inside < 7f) c = Color.white; // the glint down the lit edge
                    else c = dx < -half * 0.2f ? lit : dx < half * 0.35f ? mid : shade;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w); // 1 x 4 units
        }

        /// <summary>A flat frost ellipse: bright core, a crisp darker rim so it reads on a pale floor.</summary>
        static Sprite BuildPatch()
        {
            const int w = 128, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "FrostPatch" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c;
                    if (r > 1f) c = Color.clear;
                    else if (r > 0.86f) { c = new Color(0.30f, 0.52f, 0.78f); c.a = Mathf.Clamp01((1f - r) * 20f); }
                    else c = Color.Lerp(Color.white, new Color(0.80f, 0.93f, 1f), r);
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w); // 1 x 0.25 units
        }
    }
}
