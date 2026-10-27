using UnityEngine;

namespace VirtualInspection
{
    public sealed class MarkerPulse : MonoBehaviour
    {
        private Vector3 _initialScale;
        private float _phase;

        private void Awake()
        {
            _initialScale = transform.localScale;
            _phase = transform.position.x * 1.7f + transform.position.z;
        }

        private void Update()
        {
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.4f + _phase) * 0.08f;
            transform.localScale = _initialScale * pulse;
        }
    }
}
