using System.Collections.Generic;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.InputHandling;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Small bars over hurt or locked bodies. Drawn on the HUD canvas rather than in the
    /// world, so soul vision does not grey them and HP stays readable when judging a living possession.
    /// Runs late so bars are placed with the camera's final position for the frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class WorldHealthBars : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private Camera _camera;
        [SerializeField] private LockOnController _lockOn;
        [SerializeField] private RectTransform _container;
        [SerializeField] private WorldHealthBar _prefab;
        [Tooltip("Bar height above the body's feet, multiplied by the body's scale so larger elites stay clear.")]
        [SerializeField, Min(0f)] private float _headHeight = 2.3f;
        [Tooltip("Same, for a body the player left, which lies on the ground.")]
        [SerializeField, Min(0f)] private float _fallenHeight = 1f;
        [SerializeField] private Color _enemyColor = new Color(0.9f, 0.25f, 0.25f);
        [Tooltip("A body the player left and can return to.")]
        [SerializeField] private Color _vacatedColor = new Color(0.45f, 0.9f, 1f);

        private readonly List<WorldHealthBar> _bars = new List<WorldHealthBar>(16);
        private Canvas _canvas;

        /// <summary>Bars on screen this frame.</summary>
        public int VisibleCount { get; private set; }

        /// <summary>The world point a body's bar sits on.</summary>
        public Vector3 HeadPoint(VesselRuntime vessel) =>
            vessel.transform.position + Vector3.up * ((vessel.IsDown ? _fallenHeight : _headHeight) * vessel.transform.lossyScale.y);

        private void Awake() => _canvas = _container.GetComponentInParent<Canvas>();

        private void LateUpdate()
        {
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            VesselRuntime player = _root.PlayerVessel;
            VesselRuntime locked = _lockOn != null ? _lockOn.Target : null;
            IReadOnlyList<VesselRuntime> vessels = _root.Vessels;

            int used = 0;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                float maxHp = v.MaxHp.Value;
                float hp01 = maxHp > 0f ? v.Hp / maxHp : 0f;
                if (!HudRules.ShowWorldHealthBar(v == player, !v.IsDead, v.State.Kind, hp01, v == locked)) continue;

                Vector3 screen = _camera.WorldToScreenPoint(HeadPoint(v));
                if (screen.z <= 0f || !_camera.pixelRect.Contains(screen)) continue; // off screen: no bar to place
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, uiCamera, out Vector2 local)) continue;

                if (used == _bars.Count) _bars.Add(Instantiate(_prefab, _container));
                _bars[used].Show(local, hp01, v.State.IsVacated ? _vacatedColor : _enemyColor);
                used++;
            }
            for (int i = used; i < _bars.Count; i++) _bars[i].Hide();
            VisibleCount = used;
        }
    }
}
