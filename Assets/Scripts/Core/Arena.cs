using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>Flat side-view arena: a ground line and two walls.</summary>
    public class Arena : MonoBehaviour
    {
        public float minX = -8f;
        public float maxX = 8f;
        public float groundY = -3f;

        public float Clamp(float x, float halfWidth) => Mathf.Clamp(x, minX + halfWidth, maxX - halfWidth);

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(minX, groundY), new Vector3(maxX, groundY));
            Gizmos.DrawLine(new Vector3(minX, groundY), new Vector3(minX, groundY + 6f));
            Gizmos.DrawLine(new Vector3(maxX, groundY), new Vector3(maxX, groundY + 6f));
        }
    }
}
