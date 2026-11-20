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
        [SerializeField] private Rect sceneryBounds;

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

            Vector2 position = (Vector2)target.position + offset;
            if (sceneryBounds.width > 0f && sceneryBounds.height > 0f)
            {
                float halfHeight = sceneCamera.orthographicSize;
                position.x = ClampView(position.x, sceneryBounds.xMin, sceneryBounds.xMax, halfHeight * sceneCamera.aspect);
                position.y = ClampView(position.y, sceneryBounds.yMin, sceneryBounds.yMax, halfHeight);
            }

            transform.position = new Vector3(position.x, position.y, cameraZ);
        }

        private static float ClampView(float position, float minimum, float maximum, float halfView) =>
            halfView * 2f < maximum - minimum
                ? Mathf.Clamp(position, minimum + halfView, maximum - halfView)
                : (minimum + maximum) * .5f;
    }
}
