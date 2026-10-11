using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Owns a SpriteRenderer's color: controllers set <see cref="BaseColor"/> each frame (state tint),
    /// and <see cref="Flash"/> overrides it briefly. Unscaled time, so flashes show during hit-stop.
    /// </summary>
    public class SpriteFlash : MonoBehaviour
    {
        public SpriteRenderer target;

        Color baseColor = Color.white;
        Color flashColor;
        float flashUntil = float.NegativeInfinity;

        public Color BaseColor
        {
            get => baseColor;
            set => baseColor = value;
        }

        void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target != null) baseColor = target.color;
        }

        public void Flash(Color color, int frames)
        {
            flashColor = color;
            flashUntil = Time.unscaledTime + Frames.ToSeconds(Mathf.Max(1, frames));
            if (target != null) target.color = color;
        }

        void LateUpdate()
        {
            if (target != null) target.color = Time.unscaledTime < flashUntil ? flashColor : baseColor;
        }
    }
}
