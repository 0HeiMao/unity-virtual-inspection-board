using UnityEngine;

namespace VirtualInspection
{
    public sealed class InspectionCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.2f;
        [SerializeField] private float sprintMultiplier = 1.8f;
        [SerializeField] private float lookSensitivity = 3f;
        [SerializeField] private float minPitch = -55f;
        [SerializeField] private float maxPitch = 65f;

        private float _yaw;
        private float _pitch;
        private bool _startedLook;
        private Vector3 _lastMousePosition;

        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            var angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = NormalizePitch(angles.x);
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                _startedLook = false;
                return;
            }

            Look();
            Move();
        }

        private void Look()
        {
            if (Input.GetMouseButtonDown(1))
            {
                _startedLook = true;
                _lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButton(1) && _startedLook)
            {
                var delta = Input.mousePosition - _lastMousePosition;
                _lastMousePosition = Input.mousePosition;
                _yaw += delta.x * lookSensitivity;
                _pitch -= delta.y * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            if (Input.GetMouseButtonUp(1))
            {
                _startedLook = false;
            }
        }

        private void Move()
        {
            var input = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) input.z += 1f;
            if (Input.GetKey(KeyCode.S)) input.z -= 1f;
            if (Input.GetKey(KeyCode.D)) input.x += 1f;
            if (Input.GetKey(KeyCode.A)) input.x -= 1f;

            if (input.sqrMagnitude > 0.001f)
            {
                input.Normalize();
                var speed = moveSpeed;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    speed *= sprintMultiplier;
                }

                var planarForward = transform.forward;
                planarForward.y = 0f;
                planarForward.Normalize();
                var planarRight = transform.right;
                planarRight.y = 0f;
                planarRight.Normalize();
                var displacement = (planarForward * input.z + planarRight * input.x) * speed * Time.deltaTime;
                var next = transform.position + displacement;
                next.x = Mathf.Clamp(next.x, -7.1f, 7.1f);
                next.z = Mathf.Clamp(next.z, -6.1f, 6.3f);
                next.y = 1.7f;
                transform.position = next;
            }
        }

        private static float NormalizePitch(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
