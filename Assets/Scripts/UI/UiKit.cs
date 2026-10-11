using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The game's UI look, shared by the fight HUD and the lobby (IMGUI): outlined text that stays readable over the
    /// paintings, dark bands that fade at their edges, framed bars with a gold rim, diamond pips and key caps.
    /// Sizes are in 960x540 reference pixels times <c>s</c> (the caller's UI scale). A font placed at
    /// Resources/UI/DisplayFont is used for titles if present; otherwise Unity's default font.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Gold = new Color(1f, 0.85f, 0.6f);
        public static readonly Color GoldDeep = new Color(0.82f, 0.62f, 0.3f);
        public static readonly Color Ink = new Color(0.03f, 0.035f, 0.06f);
        public static readonly Color Muted = new Color(0.86f, 0.88f, 0.94f);
        public static readonly Color Blood = new Color(0.86f, 0.24f, 0.22f);

        static Texture2D bandTex, edgeTex;
        static Font displayFont;
        static bool fontLooked;

        /// <summary>Optional title font (Resources/UI/DisplayFont); null = Unity's default.</summary>
        public static Font DisplayFont
        {
            get
            {
                if (!fontLooked) { displayFont = Resources.Load<Font>("UI/DisplayFont"); fontLooked = true; }
                return displayFont;
            }
        }

        // ------------------------------------------------------------------ text

        /// <summary>
        /// Text that reads on light and dark paintings alike: big text (<paramref name="outline"/> 2 or more) gets a
        /// dark outline (8 offsets) and a drop shadow; small text only a crisp drop shadow, since an outline muddies
        /// thin glyphs. <paramref name="color"/>'s alpha fades all of it.
        /// </summary>
        public static void Text(Rect r, string text, GUIStyle style, Color color, float s, float outline = 1.5f)
        {
            if (string.IsNullOrEmpty(text) || color.a <= 0.003f) return;
            var old = GUI.color;
            float o = Mathf.Max(1f, outline * s);
            if (outline >= 2f)
            {
                GUI.color = new Color(0f, 0f, 0f, color.a * 0.6f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    GUI.Label(new Rect(r.x + Mathf.Cos(a) * o, r.y + Mathf.Sin(a) * o, r.width, r.height), text, style);
                }
                GUI.color = new Color(0f, 0f, 0f, color.a * 0.45f);
                GUI.Label(new Rect(r.x, r.y + o * 2f, r.width, r.height), text, style);
            }
            else
            {
                float d = Mathf.Max(1f, Mathf.Round(s));
                GUI.color = new Color(0f, 0f, 0f, color.a * 0.85f);
                GUI.Label(new Rect(r.x + d, r.y + d, r.width, r.height), text, style);
            }
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ shapes

        public static void Fill(Rect r, Color c)
        {
            if (c.a <= 0f) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>A 1-unit frame drawn inside <paramref name="r"/>.</summary>
        public static void Frame(Rect r, Color c, float thickness)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), c);
            Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            Fill(new Rect(r.x, r.y, thickness, r.height), c);
            Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }

        /// <summary>A full-width dark band whose top and bottom fade out, with thin gold rules near its edges.</summary>
        public static void Band(float centerY, float height, float alpha, float s, bool rules = true)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(0f, centerY - height * 0.5f, Screen.width, height), BandTexture);
            GUI.color = old;
            if (!rules) return;
            float w = Mathf.Min(Screen.width * 0.7f, 620f * s);
            Rule(new Rect((Screen.width - w) * 0.5f, centerY - height * 0.36f, w, Mathf.Max(1f, s)), alpha * 0.55f);
            Rule(new Rect((Screen.width - w) * 0.5f, centerY + height * 0.36f, w, Mathf.Max(1f, s)), alpha * 0.55f);
        }

        /// <summary>A gold line that fades out at both ends.</summary>
        public static void Rule(Rect r, float alpha)
        {
            var old = GUI.color;
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, alpha);
            GUI.DrawTexture(r, EdgeTexture);
            GUI.color = old;
        }

        /// <summary>
        /// A framed bar: dark rim, dim trough, a pale "ghost" showing damage just taken (it drains after the hit),
        /// the fill with a light top edge, and a thin gold frame.
        /// </summary>
        public static void Bar(Rect r, float value, float ghost, Color fill, float s, float alpha = 1f)
        {
            float px = Mathf.Max(1f, s);
            Fill(new Rect(r.x - 2f * px, r.y - 2f * px, r.width + 4f * px, r.height + 4f * px), new Color(Ink.r, Ink.g, Ink.b, 0.85f * alpha));
            Fill(r, new Color(0.12f, 0.12f, 0.16f, 0.9f * alpha));
            value = Mathf.Clamp01(value);
            ghost = Mathf.Clamp01(Mathf.Max(ghost, value));
            if (ghost > value) Fill(new Rect(r.x, r.y, r.width * ghost, r.height), new Color(1f, 0.93f, 0.8f, 0.85f * alpha));
            var main = new Rect(r.x, r.y, r.width * value, r.height);
            Fill(main, new Color(fill.r, fill.g, fill.b, alpha));
            Fill(new Rect(main.x, main.y, main.width, Mathf.Max(px, r.height * 0.3f)), new Color(1f, 1f, 1f, 0.22f * alpha));
            Fill(new Rect(main.x, main.yMax - Mathf.Max(px, r.height * 0.2f), main.width, Mathf.Max(px, r.height * 0.2f)), new Color(0f, 0f, 0f, 0.2f * alpha));
            Frame(new Rect(r.x - px, r.y - px, r.width + 2f * px, r.height + 2f * px), new Color(GoldDeep.r, GoldDeep.g, GoldDeep.b, 0.9f * alpha), px);
        }

        /// <summary>A diamond (a square turned 45 degrees) centered on <paramref name="center"/>, with a dark rim.</summary>
        public static void Diamond(Vector2 center, float size, Color fill, Color rim, float s)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, center);
            float rimW = Mathf.Max(1f, 2f * s);
            Fill(new Rect(center.x - size * 0.5f - rimW, center.y - size * 0.5f - rimW, size + 2f * rimW, size + 2f * rimW), rim);
            Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), fill);
            if (fill.a > 0.5f) // a light facet on the upper half
                Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size * 0.45f, size), new Color(1f, 1f, 1f, 0.22f * fill.a));
            GUI.matrix = m;
        }

        // ------------------------------------------------------------------ key caps

        /// <summary>
        /// One line of control hints, "key caps" (framed keys) followed by what they do, centered on
        /// <paramref name="centerX"/> (or ending at it if <paramref name="rightAlign"/>). Pairs are (keys, action).
        /// </summary>
        public static void KeyHints(float centerX, float y, (string keys, string action)[] pairs, GUIStyle capStyle, GUIStyle textStyle,
                                    float alpha, float s, bool rightAlign = false)
        {
            float h = 18f * s, padX = 5f * s, gapKey = 4f * s, gapAction = 5f * s, gapPair = 14f * s;
            // Measure first, then draw.
            float total = 0f;
            foreach (var (keys, action) in pairs)
            {
                foreach (var k in keys.Split(' ')) total += CapWidth(k, capStyle, padX, h) + gapKey;
                total += gapAction - gapKey + textStyle.CalcSize(new GUIContent(action)).x + gapPair;
            }
            total -= gapPair;
            float x = rightAlign ? centerX - total : centerX - total * 0.5f;
            foreach (var (keys, action) in pairs)
            {
                foreach (var k in keys.Split(' '))
                {
                    float w = CapWidth(k, capStyle, padX, h);
                    var cap = new Rect(x, y, w, h);
                    Fill(cap, new Color(0.08f, 0.09f, 0.13f, 0.85f * alpha));
                    Fill(new Rect(cap.x, cap.yMax - 2f * s, cap.width, 2f * s), new Color(0f, 0f, 0f, 0.5f * alpha)); // key depth
                    Frame(cap, new Color(Gold.r, Gold.g, Gold.b, 0.6f * alpha), Mathf.Max(1f, s));
                    var old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.Label(new Rect(cap.x, cap.y - 1f * s, cap.width, cap.height), k, capStyle);
                    GUI.color = old;
                    x += w + gapKey;
                }
                x += gapAction - gapKey;
                float aw = textStyle.CalcSize(new GUIContent(action)).x;
                Text(new Rect(x, y, aw + 4f * s, h), action, textStyle, new Color(Muted.r, Muted.g, Muted.b, alpha), s, 1f);
                x += aw + gapPair;
            }
        }

        static float CapWidth(string key, GUIStyle capStyle, float padX, float h) =>
            Mathf.Max(h, capStyle.CalcSize(new GUIContent(key)).x + padX * 2f);

        // ------------------------------------------------------------------ textures (built once; Unity's null check
        // rebuilds them if a Play session destroyed them while the static survived)

        static Texture2D BandTexture => bandTex != null ? bandTex : bandTex = BuildBand();
        static Texture2D EdgeTexture => edgeTex != null ? edgeTex : edgeTex = BuildEdge();

        /// <summary>The shader smoothstep: 0 below <paramref name="e0"/>, 1 above <paramref name="e1"/>, eased between.
        /// (Mathf.SmoothStep is an eased lerp between its first two arguments, not this.)</summary>
        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>1 x 64, opaque in the middle, fading to clear at top and bottom.</summary>
        static Texture2D BuildBand()
        {
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiBand" };
            for (int y = 0; y < 64; y++)
            {
                float v = Mathf.Abs(y / 63f * 2f - 1f);
                t.SetPixel(0, y, new Color(1f, 1f, 1f, 1f - Smooth(0.55f, 1f, v)));
            }
            t.Apply();
            return t;
        }

        /// <summary>64 x 1, opaque in the middle, fading to clear at the left and right ends.</summary>
        static Texture2D BuildEdge()
        {
            var t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiEdge" };
            for (int x = 0; x < 64; x++)
            {
                float u = Mathf.Abs(x / 63f * 2f - 1f);
                t.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - Smooth(0.3f, 1f, u)));
            }
            t.Apply();
            return t;
        }
    }
}
