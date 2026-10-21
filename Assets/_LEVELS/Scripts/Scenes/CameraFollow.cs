using UnityEngine;

namespace Levels.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset;

        private float cameraZ;

        public Transform Target => target;

        private void Awake()
        {
            cameraZ = transform.position.z;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 position = target.position;
            transform.position = new Vector3(position.x + offset.x, position.y + offset.y, cameraZ);
        }
    }
}
