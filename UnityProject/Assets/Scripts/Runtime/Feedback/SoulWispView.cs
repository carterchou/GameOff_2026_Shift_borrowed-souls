using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Keeps the soul alive on screen: the flame bobs and sways on real time, since the soul moves on its own clock while the
    /// world is slowed. The trail restarts when the soul appears, so it doesn't streak in from where it last was.
    /// </summary>
    public sealed class SoulWispView : MonoBehaviour
    {
        [SerializeField] private Transform _flame;
        [SerializeField] private TrailRenderer _trail;
        [SerializeField, Min(0f)] private float _bobMetres = 0.09f;
        [SerializeField, Min(0f)] private float _bobPerSecond = 0.9f;
        [SerializeField, Min(0f)] private float _swayDegrees = 7f;
        [SerializeField, Min(0f)] private float _spinDegreesPerSecond = 50f;

        private Vector3 _rest;
        private float _spin;

        private void Awake()
        {
            _rest = _flame.localPosition;
        }

        private void OnEnable()
        {
            _trail.Clear();
        }

        private void Update()
        {
            float now = Time.unscaledTime * _bobPerSecond * Mathf.PI * 2f;
            _spin += _spinDegreesPerSecond * Time.unscaledDeltaTime;
            _flame.localPosition = _rest + Vector3.up * (Mathf.Sin(now) * _bobMetres);
            _flame.localRotation = Quaternion.Euler(Mathf.Sin(now * 0.7f) * _swayDegrees, _spin, Mathf.Cos(now * 0.53f) * _swayDegrees);
        }
    }
}
