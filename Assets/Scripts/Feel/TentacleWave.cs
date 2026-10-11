using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// The Unknown One's ground wave: rides on the projectile and bursts tentacles up from the floor every
    /// <see cref="Spacing"/> units it travels, so something seems to swim under the floor toward the hero. The bursts
    /// glow red (the danger color: dash), on top of the red crest the wave already draws.
    /// </summary>
    public class TentacleWave : MonoBehaviour
    {
        const float Spacing = 0.6f;

        float groundY, height, lastX;
        int direction;
        Color glow;
        bool started;

        public static void Attach(GameObject projectile, float groundY, int direction, float height, Color glow)
        {
            var w = projectile.AddComponent<TentacleWave>();
            w.groundY = groundY;
            w.direction = direction;
            w.height = height;
            w.glow = glow;
        }

        void Update()
        {
            float x = transform.position.x;
            if (!started || Mathf.Abs(x - lastX) >= Spacing)
            {
                started = true;
                lastX = x;
                Tentacles.WaveBurst(x, groundY, direction, height, glow);
            }
        }
    }
}
