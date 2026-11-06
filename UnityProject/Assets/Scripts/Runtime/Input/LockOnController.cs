using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowedSouls.InputHandling
{
    /// <summary>
    /// Tab / R3 auto lock-on. Picks a visible enemy favouring the aim direction,
    /// then distance, and sticks to it; mouse wheel / right-stick flick cycles. Brief pillar occlusion keeps the
    /// lock; death, distance or long occlusion re-selects. Suspended while the soul flies (the soul target is a
    /// separate query and the lock cannot bypass the possession threshold). Lock only aims — it never homes.
    /// </summary>
    public sealed class LockOnController : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private Transform _indicator;
        [Tooltip("On a touch device the lock starts on and its button switches target instead of toggling.")]
        [SerializeField] private TouchControls _touch;
        [SerializeField, Min(1f)] private float _acquireRange = 15f;
        [SerializeField, Min(1f)] private float _loseRange = 18f;
        [SerializeField, Min(0f)] private float _occlusionGrace = 1.5f;
        [SerializeField] private LayerMask _sightBlockers = 1;

        private InputAction _toggle;
        private InputAction _cycle;
        private VesselRuntime _target;
        private bool _enabled;
        private float _occludedFor;
        private Vector3 _lastAim = Vector3.forward;

        public VesselRuntime Target => _enabled ? _target : null;
        public bool IsLocked => _enabled && _target != null;

        private void Awake()
        {
            InputActionMap map = _actions.FindActionMap("Gameplay", true);
            _toggle = map.FindAction("ToggleLock", true);
            _cycle = map.FindAction("CycleTarget", true);
            if (_indicator != null) _indicator.gameObject.SetActive(false);
            if (_touch != null && TouchControls.Active) _enabled = true; // aiming with a thumb is too coarse; aim assist instead
        }

        /// <summary>Direction from <paramref name="origin"/> to the locked target, if a lock is active.</summary>
        public bool TryGetAim(Vector3 origin, out Vector3 dir)
        {
            dir = default;
            if (!IsLocked || _soul.IsFree) return false;
            Vector3 to = _target.transform.position - origin;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return false;
            dir = to.normalized;
            return true;
        }

        public void NoteAim(Vector3 aim)
        {
            if (aim.sqrMagnitude > 0.01f) _lastAim = aim;
        }

        private void Update()
        {
            VesselRuntime self = _root.PlayerVessel;
            bool touch = _touch != null && TouchControls.Active;
            if (touch && _touch.TargetPressed && self != null && !_soul.IsFree)
            {
                VesselRuntime next = _target != null ? Pick(self, _target, 1) : null;
                _target = next != null ? next : Pick(self, null, 0);
            }
            else if (!touch && _toggle.WasPressedThisFrame())
            {
                _enabled = !_enabled;
                _target = _enabled && self != null ? Pick(self, null, 0) : null;
                if (_enabled && _target == null) _enabled = false; // nothing to lock onto
            }

            if (_enabled && self != null && !_soul.IsFree)
            {
                if (!IsValid(self, _target)) _target = Pick(self, null, 0);
                HandleCycle(self);
            }

            UpdateIndicator();
        }

        private void HandleCycle(VesselRuntime self)
        {
            int dirSign = 0;
            float wheel = _cycle.ReadValue<float>();
            if (Mathf.Abs(wheel) > 0.01f) dirSign = wheel > 0f ? 1 : -1;

            if (dirSign != 0)
            {
                VesselRuntime next = Pick(self, _target, dirSign);
                if (next != null) _target = next;
            }
        }

        private bool IsValid(VesselRuntime self, VesselRuntime t)
        {
            if (t == null || t.IsDead || t.Team != Team.Enemy) return false;
            Vector3 to = t.transform.position - self.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > _loseRange * _loseRange) return false;
            if (Visible(self, t)) _occludedFor = 0f;
            else _occludedFor += _root.Time.WorldDelta;
            return _occludedFor <= _occlusionGrace;
        }

        /// <summary>
        /// side == 0: best by aim alignment then distance. side ±1: the nearest-angle enemy to the right (+1) or
        /// left (-1) of the current target, measured around the player.
        /// </summary>
        private VesselRuntime Pick(VesselRuntime self, VesselRuntime current, int side)
        {
            Vector3 origin = self.transform.position;
            Vector3 reference = current != null ? Planar(current.transform.position - origin).normalized : _lastAim;
            VesselRuntime best = null;
            float bestScore = float.MaxValue;
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                if (v == current || v.IsDead || v.Team != Team.Enemy) continue;
                Vector3 to = Planar(v.transform.position - origin);
                float d = to.magnitude;
                if (d > _acquireRange || d < 0.01f || !Visible(self, v)) continue;
                Vector3 dir = to / d;
                float score;
                if (side == 0)
                {
                    score = (1f - Vector3.Dot(reference, dir)) * 10f + d * 0.25f;
                }
                else
                {
                    float signed = Vector3.SignedAngle(reference, dir, Vector3.up);
                    if (Mathf.Sign(signed) != side || Mathf.Abs(signed) < 1f) continue;
                    score = Mathf.Abs(signed);
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = v;
                }
            }
            if (best != null) _occludedFor = 0f;
            return best;
        }

        private bool Visible(VesselRuntime self, VesselRuntime t)
        {
            return !Physics.Linecast(self.transform.position + Vector3.up, t.transform.position + Vector3.up, _sightBlockers, QueryTriggerInteraction.Ignore);
        }

        private void UpdateIndicator()
        {
            if (_indicator == null) return;
            bool show = IsLocked && !_soul.IsFree;
            if (_indicator.gameObject.activeSelf != show) _indicator.gameObject.SetActive(show);
            if (!show) return;
            _indicator.position = _target.transform.position + Vector3.up * 0.05f;
            _indicator.Rotate(Vector3.up, 90f * Time.unscaledDeltaTime, Space.World);
            float s = _target.transform.localScale.x * 1.6f;
            _indicator.localScale = new Vector3(s, 0.01f, s);
        }

        private static Vector3 Planar(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
