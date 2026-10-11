using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Dash afterimages: faint copies of the character's current sprite left behind and fading out.
    /// A small pool in world space (not parented to the moving character). Scaled time, so hit-stop
    /// holds them.
    /// </summary>
    public class AfterimageTrail : MonoBehaviour
    {
        public int poolSize = 10;
        public float lifetime = 0.18f;
        public Color tint = new Color(0.45f, 0.9f, 1f, 0.45f);

        SpriteRenderer[] pool;
        float[] ages;
        int next;

        /// <summary>Creates a trail (and its pool) drawn just behind <paramref name="sortingOrder"/>.</summary>
        public static AfterimageTrail Create(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var trail = go.AddComponent<AfterimageTrail>();
            trail.pool = new SpriteRenderer[trail.poolSize];
            trail.ages = new float[trail.poolSize];
            for (int i = 0; i < trail.poolSize; i++)
            {
                var g = new GameObject($"Ghost {i}");
                g.transform.SetParent(go.transform, false);
                trail.pool[i] = g.AddComponent<SpriteRenderer>();
                trail.pool[i].sortingOrder = sortingOrder;
                trail.pool[i].enabled = false;
                trail.ages[i] = -1f;
            }
            return trail;
        }

        /// <summary>Leaves a copy of <paramref name="source"/> where it is now.</summary>
        public void Spawn(SpriteRenderer source)
        {
            if (source == null || source.sprite == null || pool == null) return;
            var g = pool[next];
            next = (next + 1) % pool.Length;
            g.sprite = source.sprite;
            g.flipX = source.flipX;
            g.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            g.transform.localScale = source.transform.lossyScale;
            g.color = tint;
            g.enabled = true;
            ages[System.Array.IndexOf(pool, g)] = 0f;
        }

        void Update()
        {
            if (pool == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < pool.Length; i++)
            {
                if (ages[i] < 0f) continue;
                ages[i] += dt;
                float p = ages[i] / lifetime;
                if (p >= 1f)
                {
                    ages[i] = -1f;
                    pool[i].enabled = false;
                    continue;
                }
                var c = tint;
                c.a *= 1f - p;
                pool[i].color = c;
            }
        }
    }
}
