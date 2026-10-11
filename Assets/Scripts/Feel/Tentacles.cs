using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The Unknown One's tentacles, generated at runtime (no art asset, 0 credits) in the cel style of his DreamLayer
    /// art: dark purple with pale lilac suckers along the inner curve, a dark outline, two-tone shading lit from the
    /// upper left. A few curl variants are drawn once; a tentacle writhes by stepping through them (limited animation,
    /// like the anime it imitates).
    ///   Erupt: his floor attacks (BossConfig.floorStyle = Tentacles): a cluster bursts up across the hitbox, writhes
    ///          while it's live, and pulls back into the floor.
    ///   Wave:  his ground wave: tentacles burst up one after another where it passes, the newest glowing red (red = dash).
    ///   Bind:  the root on the hero: tentacles wrap his legs for as long as he's rooted.
    /// Scaled time, so hit-stop freezes them with everything else.
    /// </summary>
    public class Tentacles : MonoBehaviour
    {
        const int Curls = 6;                 // curl variants, from nearly straight to a tight hook
        const float RiseSeconds = 0.08f, RetractSeconds = 0.18f;
        const float SpriteLength = 3f;       // world units of a straight tentacle at scale 1
        const int BackOrder = 1, FrontOrder = 4; // floor 0 < back 1 < boss 2 < hero 3 < front 4

        static Sprite[] curlSprites;
        static Sprite patchSprite;

        struct Tendril
        {
            public Transform t;
            public SpriteRenderer sr;
            public float height, width, delay, phase, speed;
            public int curlLo, curlHi;
            public Color color;
        }

        Tendril[] tendrils;
        float age, hold;
        System.Func<bool> held; // Bind: stays up while this is true
        Transform follow;
        float retractAt = -1f;

        /// <summary>Draws the curl variants now (a load-time cost instead of a hitch at the first eruption).</summary>
        public static void Warm() { _ = CurlSprites; }

        // ---------------------------------------------------------------- the three uses

        /// <summary>A cluster bursts up across <paramref name="area"/> (world space; its bottom is the floor) and
        /// writhes for <paramref name="holdSeconds"/> before pulling back.</summary>
        public static void Erupt(Rect area, float groundY, float holdSeconds)
        {
            int n = Mathf.Max(3, Mathf.RoundToInt(area.width / 0.45f));
            var g = Make("Tentacles", groundY, holdSeconds, n);
            for (int i = 0; i < n; i++)
            {
                float u = n > 1 ? i / (n - 1f) : 0.5f;
                float centered = 1f - Mathf.Abs(u - 0.5f) * 2f;
                float x = Mathf.Lerp(area.xMin + 0.2f, area.xMax - 0.2f, u) + Random.Range(-0.08f, 0.08f);
                bool front = i % 2 == 1;
                g.Add(i, x, groundY, area.height * (0.7f + 0.5f * centered) * Random.Range(0.85f, 1.05f) * (front ? 0.85f : 1f),
                      front ? FrontOrder : BackOrder, flip: x > area.center.x, delay: (1f - centered) * 0.05f,
                      curlLo: 0, curlHi: 3, tint: front ? Color.white : new Color(0.78f, 0.74f, 0.82f), thick: 1.9f);
            }
        }

        /// <summary>One burst where a ground wave passes: a few tentacles, tinted toward <paramref name="glow"/> (the
        /// danger color) so the wave still reads as "dash".</summary>
        public static void WaveBurst(float x, float groundY, int direction, float height, Color glow)
        {
            var g = Make("WaveTentacles", groundY, 0.12f, 3);
            for (int i = 0; i < 3; i++)
            {
                bool front = i != 1;
                g.Add(i, x + (i - 1) * 0.22f * direction + Random.Range(-0.05f, 0.05f), groundY,
                      height * (i == 1 ? 1f : 0.75f) * Random.Range(0.9f, 1.08f), front ? FrontOrder : BackOrder,
                      flip: direction < 0, delay: i * 0.015f, curlLo: 1, curlHi: 4, tint: Color.Lerp(Color.white, glow, 0.5f), thick: 1.7f);
            }
        }

        /// <summary>The root: tentacles wrap the legs of <paramref name="target"/> (standing on <paramref name="groundY"/>)
        /// and hold while <paramref name="stillHeld"/> says so.</summary>
        public static void Bind(Transform target, float groundY, float legHeight, System.Func<bool> stillHeld)
        {
            var g = Make("RootTentacles", groundY, float.MaxValue, 3);
            g.held = stillHeld;
            g.follow = target;
            g.transform.position = new Vector3(target.position.x, groundY, 0f); // the tentacles are placed relative to him
            // One behind his legs, two in front, curling in toward him.
            g.Add(0, -0.3f, groundY, legHeight * 1.1f, BackOrder, flip: false, delay: 0f, curlLo: 3, curlHi: 5, tint: new Color(0.8f, 0.76f, 0.84f), thick: 2.3f);
            g.Add(1, 0.28f, groundY, legHeight, FrontOrder, flip: true, delay: 0.03f, curlLo: 3, curlHi: 5, tint: Color.white, thick: 2.3f);
            g.Add(2, -0.08f, groundY, legHeight * 0.8f, FrontOrder, flip: false, delay: 0.06f, curlLo: 4, curlHi: 5, tint: Color.white, thick: 2.3f);
        }

        /// <summary>A dark violet pool on the floor where tentacles will erupt (the telegraph): it spreads and darkens
        /// through the windup (<paramref name="k"/>), and pulses once the danger cue has sounded.</summary>
        public static void ShowPatch(SpriteRenderer patch, Rect area, float groundY, float k, bool cueOn)
        {
            patch.sprite = PatchShape;
            patch.enabled = true;
            float spread = Mathf.Lerp(0.6f, 1.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f)));
            var b = PatchShape.bounds.size;
            patch.transform.position = new Vector3(area.center.x, groundY + 0.03f, 0f);
            patch.transform.localScale = new Vector3(area.width * spread / b.x, 0.5f / b.y, 1f);
            float pulse = cueOn ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
            var c = Color.Lerp(Color.white, new Color(1f, 0.75f, 1f), 0.6f * pulse);
            c.a = Mathf.Lerp(0.5f, 1f, k);
            patch.color = c;
        }

        // ---------------------------------------------------------------- instance

        static Tentacles Make(string name, float groundY, float holdSeconds, int count)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(0f, groundY, 0f);
            var g = go.AddComponent<Tentacles>();
            g.hold = holdSeconds;
            g.tendrils = new Tendril[count];
            return g;
        }

        void Add(int i, float x, float groundY, float height, int order, bool flip, float delay, int curlLo, int curlHi, Color tint, float thick)
        {
            var t = new GameObject("Tentacle").transform;
            t.SetParent(transform, false);
            t.localPosition = new Vector3(x - (follow != null ? 0f : transform.position.x), 0f, 0f);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = CurlSprites[curlLo];
            sr.sortingOrder = order;
            sr.flipX = flip;
            sr.color = tint;
            tendrils[i] = new Tendril
            {
                t = t, sr = sr, height = height, width = thick * Random.Range(0.9f, 1.1f), delay = delay, color = tint,
                phase = Random.value * 10f, speed = Random.Range(7f, 11f), curlLo = curlLo, curlHi = curlHi,
            };
            Apply(ref tendrils[i], 0f, 0f);
        }

        void Update()
        {
            age += Time.deltaTime; // 0 in hit-stop
            if (held != null && retractAt < 0f && (!held() || follow == null)) retractAt = age;
            float end = held != null ? (retractAt < 0f ? float.MaxValue : retractAt) : hold;

            float maxDelay = 0f;
            for (int i = 0; i < tendrils.Length; i++)
            {
                ref var d = ref tendrils[i];
                if (d.t == null) continue;
                maxDelay = Mathf.Max(maxDelay, d.delay);
                float t = age - d.delay;
                float rise = Mathf.Clamp01(t / RiseSeconds);
                float k = rise < 1f ? EaseOutBack(rise) : 1f;
                float retract = Mathf.Clamp01((age - end) / RetractSeconds);
                Apply(ref d, k * (1f - retract * retract), t);
            }
            if (follow != null) transform.position = new Vector3(follow.position.x, transform.position.y, 0f);
            if (age > end + RetractSeconds + maxDelay) Destroy(gameObject);
        }

        void Apply(ref Tendril d, float heightK, float t)
        {
            float s = d.height / SpriteLength;
            d.t.localScale = new Vector3(s * d.width, Mathf.Max(0.0001f, s * heightK), 1f);
            // Writhe: step back and forth through the curl variants, with a slight sway at the base.
            float w = 0.5f + 0.5f * Mathf.Sin(t * d.speed + d.phase);
            int curl = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(d.curlLo, d.curlHi, w)), 0, Curls - 1);
            d.sr.sprite = CurlSprites[curl];
            d.t.localRotation = Quaternion.Euler(0f, 0f, (d.sr.flipX ? 1f : -1f) * 6f * Mathf.Sin(t * d.speed * 0.5f + d.phase));
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // ---------------------------------------------------------------- drawing

        // Unity's null check: sprites generated in an earlier Play session are destroyed when it ends, but with domain
        // reload off the static that points at them survives.
        static Sprite[] CurlSprites
        {
            get
            {
                if (curlSprites == null || curlSprites[0] == null)
                {
                    curlSprites = new Sprite[Curls];
                    for (int i = 0; i < Curls; i++) curlSprites[i] = BuildTentacle(Mathf.Lerp(0.25f, 2.6f, i / (Curls - 1f)));
                }
                return curlSprites;
            }
        }

        static Sprite PatchShape => patchSprite != null ? patchSprite : patchSprite = BuildPatch();

        /// <summary>
        /// One tentacle, its base at the bottom, bending right as it rises and hooking at the tip by
        /// <paramref name="curl"/> radians. Drawn by stamping capsules along the centerline into distance buffers
        /// (nearest centerline point, how far along, which side), then shading per pixel.
        /// </summary>
        static Sprite BuildTentacle(float curl)
        {
            const int w = 160, h = 320, segments = 48;
            const float baseX = 46f, baseY = 4f, length = 300f, r0 = 21f;
            var dist = new float[w * h];
            var along = new float[w * h];
            var side = new float[w * h];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.MaxValue;

            // The centerline: heading from straight up, turning right faster toward the tip.
            var pts = new Vector2[segments + 1];
            pts[0] = new Vector2(baseX, baseY);
            for (int i = 1; i <= segments; i++)
            {
                float s = (i - 0.5f) / segments;
                float a = curl * Mathf.Pow(s, 2.2f) + 0.12f * Mathf.Sin(s * 5f); // a slight S before the hook
                pts[i] = pts[i - 1] + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (length / segments);
            }

            for (int i = 0; i < segments; i++)
            {
                Vector2 p0 = pts[i], p1 = pts[i + 1], dir = (p1 - p0).normalized;
                float rMax = Radius(i / (float)segments, r0) + 2f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.x, p1.x) - rMax)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(p0.x, p1.x) + rMax));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.y, p1.y) - rMax)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(p0.y, p1.y) + rMax));
                float segLen = (p1 - p0).magnitude;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float tt = Mathf.Clamp01(Vector2.Dot(p - p0, dir) / segLen);
                        var q = p0 + dir * segLen * tt;
                        float d = (p - q).magnitude;
                        int k = y * w + x;
                        if (d < dist[k])
                        {
                            dist[k] = d;
                            along[k] = (i + tt) / segments;
                            side[k] = dir.x * (p.y - q.y) - dir.y * (p.x - q.x); // > 0: left of the heading (outer, lit)
                        }
                    }
            }

            var ink = new Color(0.08f, 0.03f, 0.10f);
            var shade = new Color(0.26f, 0.11f, 0.31f);
            var lit = new Color(0.42f, 0.21f, 0.49f);
            var shine = new Color(0.66f, 0.50f, 0.74f);
            var under = new Color(0.80f, 0.64f, 0.87f);
            var underShade = new Color(0.62f, 0.45f, 0.70f);
            var px = new Color32[w * h];
            for (int k = 0; k < px.Length; k++)
            {
                float d = dist[k];
                if (d == float.MaxValue) continue;
                float s = along[k], r = Radius(s, r0);
                if (d > r + 0.5f) continue;
                float edge = Mathf.Clamp01(r + 0.5f - d);
                float rel = d / r; // 0 on the centerline, 1 at the edge
                bool outer = side[k] > 0f;
                Color c;
                if (d > r - 2.6f) c = ink;
                else if (!outer && rel > 0.3f)
                {
                    // The underside: a lilac band with round suckers along it.
                    float suckerPhase = Mathf.Repeat(s * 15f, 1f);
                    float sx = (suckerPhase - 0.5f) * 2f, sy = (rel - 0.65f) / 0.22f;
                    float sd = sx * sx + sy * sy;
                    c = sd < 0.45f ? underShade : sd < 0.75f ? ink : under;
                    if (s > 0.9f) c = under; // no suckers on the thin tip
                }
                else if (outer && rel > 0.55f && rel < 0.75f && s > 0.1f && s < 0.7f) c = shine; // the hard highlight
                else c = outer ? lit : shade;
                c.a = edge;
                px[k] = c;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Tentacle" };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(baseX / w, baseY / h), length / SpriteLength);
        }

        static float Radius(float s, float r0) => r0 * (1f - 0.86f * s) + 1.6f;

        /// <summary>A flat dark violet pool with a crisp rim, so it reads on the pale floor.</summary>
        static Sprite BuildPatch()
        {
            const int w = 128, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "TentaclePatch" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c;
                    if (r > 1f) c = Color.clear;
                    else if (r > 0.86f) { c = new Color(0.10f, 0.03f, 0.12f); c.a = Mathf.Clamp01((1f - r) * 20f); }
                    else c = Color.Lerp(new Color(0.16f, 0.06f, 0.20f), new Color(0.36f, 0.16f, 0.42f), r);
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w); // 1 x 0.25 units
        }
    }
}
