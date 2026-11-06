using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Real point light under a legal soul target, so the floor around it brightens too. The body's own
    /// glow is the aura and emission in <see cref="VesselView"/>.
    /// </summary>
    public sealed class SoulTargetGlow : MonoBehaviour
    {
        [SerializeField] private Light _light;
        [SerializeField] private Color _color = new Color(0.45f, 0.95f, 1f);
        [SerializeField, Min(0f)] private float _lightIntensity = 1f;
        [SerializeField, Min(0f)] private float _targetLightIntensity = 2f;
        [SerializeField, Min(0f)] private float _pulseSpeed = 3f;
        [Tooltip("How far the pulse swings around the base brightness.")]
        [SerializeField, Range(0f, 1f)] private float _pulseDepth = 0.2f;

        private void Awake() => _light.color = _color;

        public void Show(Vector3 position, bool targeted, float time)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.position = position;
            float pulse = 1f + _pulseDepth * Mathf.Sin(time * _pulseSpeed);
            _light.intensity = (targeted ? _targetLightIntensity : _lightIntensity) * pulse;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
