using UnityEngine;

namespace SuperTowerDefense
{
    /// <summary>A movable, world-aligned rectangle for the camera's ground focus.</summary>
    public sealed class CameraMovementBounds : MonoBehaviour
    {
        [SerializeField, Tooltip("World-space X/Z size. Move the transform to reposition the rectangle and ground height. Rotation and scale are ignored.")]
        private Vector2 size = new Vector2(18, 18);

        public Vector2 Size
        {
            get => size;
            set => size = new Vector2(Mathf.Max(0, value.x), Mathf.Max(0, value.y));
        }

        public Vector3 Clamp(Vector3 point)
        {
            var center = transform.position;
            return new Vector3(
                Mathf.Clamp(point.x, center.x - size.x * 0.5f, center.x + size.x * 0.5f),
                center.y,
                Mathf.Clamp(point.z, center.z - size.y * 0.5f, center.z + size.y * 0.5f));
        }

        private void OnValidate() => Size = size;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0.75f, 0.15f, 0.8f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.05f, size.y));
        }
    }
}
