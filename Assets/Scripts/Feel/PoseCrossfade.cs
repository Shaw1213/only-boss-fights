using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// A short cross-fade between two pose sprites: the outgoing pose is copied onto a sibling renderer
    /// that follows the character and fades out over the new one, so a swap (idle ↔ walk) blends instead
    /// of popping. Scaled time, so hit-stop holds it.
    /// </summary>
    public class PoseCrossfade : MonoBehaviour
    {
        public float duration = 0.1f;

        SpriteRenderer sr;
        float age = -1f;
        float startAlpha;

        /// <summary>Adds a cross-fade renderer under <paramref name="parent"/> (the body's parent, so it follows the character).</summary>
        public static PoseCrossfade Create(Transform parent, int sortingOrder)
        {
            var go = new GameObject("PoseCrossfade");
            go.transform.SetParent(parent, false);
            var fade = go.AddComponent<PoseCrossfade>();
            fade.sr = go.AddComponent<SpriteRenderer>();
            fade.sr.sortingOrder = sortingOrder;
            fade.sr.enabled = false;
            return fade;
        }

        /// <summary>Starts fading out a copy of what <paramref name="from"/> shows right now.</summary>
        public void Begin(SpriteRenderer from)
        {
            if (from == null || from.sprite == null) return;
            sr.sprite = from.sprite;
            sr.flipX = from.flipX;
            sr.color = from.color;
            transform.localPosition = from.transform.localPosition;
            transform.localScale = from.transform.localScale;
            startAlpha = from.color.a;
            age = 0f;
            sr.enabled = true;
        }

        void Update()
        {
            if (age < 0f) return;
            age += Time.deltaTime;
            float p = duration > 0f ? age / duration : 1f;
            if (p >= 1f)
            {
                age = -1f;
                sr.enabled = false;
                return;
            }
            var c = sr.color;
            c.a = startAlpha * (1f - p);
            sr.color = c;
        }
    }
}
