using UnityEngine;

namespace VirtualInspection
{
    public sealed class MarkerPulse : MonoBehaviour
    {
        private Vector3 _initialScale;
        private float _phase;
        private float _amplitude = 0.08f;
        private float _speed = 3.4f;

        /// <summary>由标记工厂按优先级配置：高优先级跳得更明显，低优先级几乎静止。</summary>
        public void Configure(float amplitude, float speed)
        {
            _amplitude = Mathf.Max(0f, amplitude);
            _speed = Mathf.Max(0f, speed);
        }

        private void Awake()
        {
            _initialScale = transform.localScale;
            _phase = transform.position.x * 1.7f + transform.position.z;
        }

        private void Update()
        {
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * _speed + _phase) * _amplitude;
            transform.localScale = _initialScale * pulse;
        }
    }
}
