using UnityEngine;

namespace Levels.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset;
        [SerializeField, Min(0f)] private float fixedFrameWidth;

        private float cameraZ;
        private Camera sceneCamera;
        private float minimumSize;

        public Transform Target => target;

        private void Awake()
        {
            cameraZ = transform.position.z;
            sceneCamera = GetComponent<Camera>();
            minimumSize = sceneCamera.orthographicSize;
        }

        private void LateUpdate()
        {
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (fixedFrameWidth > 0f)
            {
                sceneCamera.orthographicSize = Mathf.Max(minimumSize, fixedFrameWidth / (2f * sceneCamera.aspect));
                return;
            }
            if (target == null) return;
            Vector3 position = target.position;
            transform.position = new Vector3(position.x + offset.x, position.y + offset.y, cameraZ);
        }
    }
}
