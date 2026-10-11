using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// A soft dark ellipse under a character's feet, the cue that puts a sprite ON a painted floor instead of in
    /// front of it. Generated at runtime (no art asset); lives on a child of the character's root, which sits at the
    /// feet, so it stays on the ground. <see cref="SetSpread"/> widens it while the feet are apart (walking).
    /// </summary>
    public class ContactShadow : MonoBehaviour
    {
        const int TexWidth = 128, TexHeight = 32;
        static Sprite cached;

        SpriteRenderer sr;
        float width, height;
        float spread = 1f;

        public static ContactShadow Attach(Transform characterRoot, int sortingOrder, float width, float height, float opacity, Color color)
        {
            var go = new GameObject("ContactShadow");
            go.transform.SetParent(characterRoot, false);
            go.transform.localPosition = Vector3.zero;
            var s = go.AddComponent<ContactShadow>();
            s.sr = go.AddComponent<SpriteRenderer>();
            s.sr.sprite = Shape;
            s.sr.sortingOrder = sortingOrder;
            s.sr.color = new Color(color.r, color.g, color.b, opacity);
            s.width = width;
            s.height = height;
            s.Apply();
            return s;
        }

        /// <summary>1 = standing; above 1 widens it (feet apart). Eased by the caller.</summary>
        public void SetSpread(float k)
        {
            spread = k;
            Apply();
        }

        void Apply()
        {
            var b = sr.sprite.bounds.size;
            // The parent may be scaled (it isn't for the lobby hero's root); keep the shadow's world size.
            var ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(width * spread / b.x / Mathf.Max(0.0001f, ps.x), height / b.y / Mathf.Max(0.0001f, ps.y), 1f);
        }

        // Unity's null check: a sprite generated in an earlier Play session is destroyed when it ends, but with domain
        // reload off the static that points at it survives.
        static Sprite Shape => cached != null ? cached : cached = Build();

        static Sprite Build()
        {
            var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ContactShadow" };
            var px = new Color32[TexWidth * TexHeight];
            for (int y = 0; y < TexHeight; y++)
                for (int x = 0; x < TexWidth; x++)
                {
                    float u = (x + 0.5f) / TexWidth * 2f - 1f, v = (y + 0.5f) / TexHeight * 2f - 1f;
                    float r2 = u * u + v * v;
                    // A dense core under the feet, fading out to a soft edge.
                    float a = Mathf.Clamp01(1f - r2);
                    a = a * a * (3f - 2f * a);
                    px[y * TexWidth + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TexWidth, TexHeight), new Vector2(0.5f, 0.5f), TexWidth); // 1 unit wide
        }
    }
}
