using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemySpawnArea : MonoBehaviour
    {
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 size = new Vector2(22f, 12f);
        public Vector2 Center => center;
        public Vector2 Size => size;

        public Vector2 RandomPosition()
        {
            return transform.TransformPoint(center + new Vector2(
                Random.Range(-size.x * .5f, size.x * .5f),
                Random.Range(-size.y * .5f, size.y * .5f)));
        }

        public bool Contains(Bounds footprint)
        {
            if (size.x <= 0f || size.y <= 0f) return false;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 point = footprint.center + Vector3.Scale(footprint.extents, new Vector3(x, y, 0f));
                Vector2 local = (Vector2)transform.InverseTransformPoint(point) - center;
                if (Mathf.Abs(local.x) > size.x * .5f || Mathf.Abs(local.y) > size.y * .5f) return false;
            }
            return true;
        }

        private void OnValidate() => size = Vector2.Max(size, Vector2.one * .01f);

        private void OnDrawGizmos()
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.2f, 1f, .35f, .08f);
            Gizmos.DrawCube(center, new Vector3(size.x, size.y, .01f));
            Gizmos.color = new Color(.2f, 1f, .35f, .9f);
            Gizmos.DrawWireCube(center, new Vector3(size.x, size.y, .01f));
            Gizmos.matrix = previous;
        }
    }
}
