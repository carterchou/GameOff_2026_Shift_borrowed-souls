using BorrowedSouls.Bootstrap;
using BorrowedSouls.Soul;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Soul vision: fades a desaturating global Volume in within ~0.2 s when the soul leaves its
    /// body and out on possession; a second vignette Volume strengthens over the last 1.5 s of the soul timer.
    /// Only Volume weights are animated — profile assets are never modified at runtime.
    /// </summary>
    public sealed class SoulVision : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private Volume _visionVolume;
        [SerializeField] private Volume _lowTimeVolume;
        [SerializeField, Min(0.1f)] private float _lowTimeWindow = 1.5f;

        private void Awake()
        {
            _visionVolume.weight = 0f;
            if (_lowTimeVolume != null) _lowTimeVolume.weight = 0f;
        }

        private void LateUpdate()
        {
            float fade = _root.SoulSettings.VisionFadeTime;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fade);
            bool free = _soul.IsFree;
            _visionVolume.weight = Mathf.MoveTowards(_visionVolume.weight, free ? 1f : 0f, step);

            if (_lowTimeVolume == null) return;
            float low = 0f;
            if (free)
            {
                float left = _soul.Soul.FreeTimeLeft;
                low = Mathf.Clamp01(1f - left / _lowTimeWindow);
                // Gentle heartbeat pulse in the final second; amplitude kept low to avoid harsh flashing.
                if (left < 1f) low *= 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f);
            }
            _lowTimeVolume.weight = Mathf.MoveTowards(_lowTimeVolume.weight, low, step * 2f);
        }
    }
}
