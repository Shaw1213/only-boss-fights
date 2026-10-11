using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Keeps the whole arena width on screen at any aspect ratio by growing the orthographic size
    /// when the view is narrower than the arena needs (never shrinking below the base size).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        public Arena arena;
        [Tooltip("Orthographic size at 16:9 and wider.")]
        public float baseOrthographicSize = 5f;
        [Tooltip("Extra world units kept visible beyond each wall.")]
        public float horizontalMargin = 0.6f;

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (arena == null || cam == null || cam.aspect <= 0f) return;
            float halfWidth = (arena.maxX - arena.minX) * 0.5f + horizontalMargin;
            cam.orthographicSize = Mathf.Max(baseOrthographicSize, halfWidth / cam.aspect);
        }
    }
}
