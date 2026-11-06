using System.Collections.Generic;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Feedback;
using BorrowedSouls.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// While the soul flies, every body it could possess gets a light on the floor around it, and a name on the UI layer so
    /// it is clear what it is. Pooled; runs late for the camera's final position.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SoulTargetMarkers : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private Camera _camera;
        [SerializeField] private RectTransform _labelContainer;
        [SerializeField] private SoulTargetGlow _glowPrefab;
        [SerializeField] private SoulTargetLabel _labelPrefab;
        [Tooltip("Light height above the feet of a standing body.")]
        [SerializeField, Min(0f)] private float _standingGlowHeight = 1f;
        [Tooltip("Light height above the feet of a body lying down.")]
        [SerializeField, Min(0f)] private float _fallenGlowHeight = 0.5f;
        [SerializeField, Min(0f)] private float _standingLabelHeight = 2.5f;
        [SerializeField, Min(0f)] private float _fallenLabelHeight = 1.2f;

        private readonly List<SoulTargetGlow> _glows = new List<SoulTargetGlow>(8);
        private readonly List<SoulTargetLabel> _labels = new List<SoulTargetLabel>(8);
        private Canvas _canvas;

        /// <summary>Glows showing this frame.</summary>
        public int GlowCount { get; private set; }

        /// <summary>Names on screen this frame.</summary>
        public int LabelCount { get; private set; }

        private void Awake() => _canvas = _labelContainer.GetComponentInParent<Canvas>();

        private void LateUpdate()
        {
            int glows = 0;
            int labels = 0;
            if (_soul.IsFree)
            {
                Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
                float time = Time.unscaledTime;
                IReadOnlyList<VesselRuntime> vessels = _root.Vessels;
                for (int i = 0; i < vessels.Count; i++)
                {
                    VesselRuntime v = vessels[i];
                    if (!_soul.IsLegalTarget(v)) continue;

                    bool targeted = v == _soul.Target;
                    float scale = v.transform.lossyScale.y;
                    if (glows == _glows.Count) _glows.Add(Instantiate(_glowPrefab, transform));
                    float glowHeight = v.IsDown ? _fallenGlowHeight : _standingGlowHeight;
                    _glows[glows++].Show(v.transform.position + Vector3.up * (glowHeight * scale), targeted, time);

                    float labelHeight = v.IsDown ? _fallenLabelHeight : _standingLabelHeight;
                    Vector3 screen = _camera.WorldToScreenPoint(v.transform.position + Vector3.up * (labelHeight * scale));
                    if (screen.z <= 0f || !_camera.pixelRect.Contains(screen)) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelContainer, screen, uiCamera, out Vector2 local)) continue;
                    if (labels == _labels.Count) _labels.Add(Instantiate(_labelPrefab, _labelContainer));
                    _labels[labels++].Show(local, v.DisplayName, targeted);
                }
            }
            for (int i = glows; i < _glows.Count; i++) _glows[i].Hide();
            for (int i = labels; i < _labels.Count; i++) _labels[i].Hide();
            GlowCount = glows;
            LabelCount = labels;
        }
    }
}
