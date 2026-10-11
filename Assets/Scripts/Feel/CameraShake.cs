using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Trauma-based screen shake (Squirrel Eiserloh, GDC 2016): events add trauma, shake = trauma²,
    /// trauma decays over time. Uses unscaled time so the shake keeps moving during hit-stop.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Range(0f, 1f)] public float trauma;
        [Tooltip("Max offset in world units at full trauma.")]
        public float maxOffset = 0.3f;
        [Tooltip("Max roll in degrees at full trauma.")]
        public float maxAngle = 2f;
        [Tooltip("Trauma lost per second.")]
        public float decayPerSecond = 1.3f;
        public float frequency = 22f;

        Vector3 basePosition;
        Quaternion baseRotation;
        float seed;

        void Awake()
        {
            Instance = this;
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            seed = Random.Range(0f, 100f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Add(float amount)
        {
            if (Instance != null && amount > 0f) Instance.trauma = Mathf.Clamp01(Instance.trauma + amount);
        }

        void LateUpdate()
        {
            float shake = trauma * trauma;
            float t = Time.unscaledTime * frequency;
            float ox = maxOffset * shake * (Mathf.PerlinNoise(seed, t) * 2f - 1f);
            float oy = maxOffset * shake * (Mathf.PerlinNoise(seed + 1f, t) * 2f - 1f);
            float roll = maxAngle * shake * (Mathf.PerlinNoise(seed + 2f, t) * 2f - 1f);

            transform.localPosition = basePosition + new Vector3(ox, oy, 0f);
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, roll);
            trauma = Mathf.Max(0f, trauma - decayPerSecond * Time.unscaledDeltaTime);
        }
    }
}
