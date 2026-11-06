using BorrowedSouls.Bootstrap;
using BorrowedSouls.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowedSouls.InputHandling
{
    /// <summary>
    /// Turns Input System actions into a <see cref="VesselIntent"/> for whichever vessel the player soul is in,
    /// and exposes the same semantic input to the soul controller while the soul flies free.
    /// Movement is camera-relative; aim comes from the mouse projected onto the ground plane or from the right
    /// stick, whichever device was used last. On a touch device the on-screen <see cref="TouchControls"/> replace
    /// mouse and keys: aim follows the move stick and the lock-on stays on, so attacks find their target.
    /// </summary>
    public sealed class PlayerInputDriver : MonoBehaviour, IVesselDriver, ISoulInput
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private Camera _camera;
        [SerializeField] private LockOnController _lockOn;
        [SerializeField] private TouchControls _touch;
        [SerializeField, Range(0f, 1f)] private float _touchAimDeadZone = 0.2f;

        private InputAction _move;
        private InputAction _point;
        private InputAction _weak;
        private InputAction _strong;
        private InputAction _special;
        private InputAction _dodge;
        private InputAction _soulShift;

        private Vector3 _lastAim = Vector3.forward;

        // A phone browser also turns taps into mouse clicks; reading those would attack on every button tap.
        private bool Touch => _touch != null && TouchControls.Active;

        public bool SoulShiftHeld => Touch ? _touch.SoulShiftHeld : _soulShift != null && _soulShift.IsPressed();
        public bool SoulShiftPressed => Touch ? _touch.SoulShiftPressed : _soulShift != null && _soulShift.WasPressedThisFrame();

        private void Awake()
        {
            if (_actions == null || _camera == null || _root == null)
            {
                Debug.LogError($"{nameof(PlayerInputDriver)} on '{name}' is missing references (actions/camera/root); disabling.", this);
                enabled = false;
                return;
            }

            InputActionMap map = _actions.FindActionMap("Gameplay", true);
            _move = map.FindAction("Move", true);
            _point = map.FindAction("Point", true);
            _weak = map.FindAction("Weak", true);
            _strong = map.FindAction("Strong", true);
            _special = map.FindAction("Special", true);
            _dodge = map.FindAction("Dodge", true);
            _soulShift = map.FindAction("SoulShift", true);
        }

        private void OnEnable()
        {
            if (_actions != null) _actions.FindActionMap("Gameplay", true).Enable();
        }

        private void OnDisable()
        {
            if (_actions != null) _actions.FindActionMap("Gameplay", true).Disable();
        }

        public void BuildIntent(VesselRuntime vessel, ref VesselIntent intent)
        {
            if (_move == null) return;
            intent.Move = ReadMoveWorld();
            Vector3 freeAim = ReadAimWorld(vessel.transform.position);
            if (_lockOn != null)
            {
                _lockOn.NoteAim(freeAim);
                if (_lockOn.TryGetAim(vessel.transform.position, out Vector3 lockAim)) freeAim = lockAim;
            }
            intent.Aim = freeAim;
            if (Touch)
            {
                intent.WeakPressed = _touch.WeakPressed;
                intent.WeakHeld = _touch.WeakHeld;
                intent.StrongPressed = _touch.StrongPressed;
                intent.SpecialPressed = _touch.SpecialPressed;
                intent.DodgePressed = _touch.DodgePressed;
                intent.SoulShiftHeld = _touch.SoulShiftHeld;
                return;
            }
            intent.WeakPressed = _weak.WasPressedThisFrame();
            intent.WeakHeld = _weak.IsPressed();
            intent.StrongPressed = _strong.WasPressedThisFrame();
            intent.SpecialPressed = _special.WasPressedThisFrame();
            intent.DodgePressed = _dodge.WasPressedThisFrame();
            intent.SoulShiftHeld = _soulShift.IsPressed();
        }

        /// <summary>Camera-relative planar move (x, z) in world space, magnitude 0..1.</summary>
        public Vector2 ReadMoveWorld()
        {
            if (_move == null) return Vector2.zero;
            GetCameraBasis(out Vector3 camForward, out Vector3 camRight);
            Vector2 move = Touch ? _touch.Move : _move.ReadValue<Vector2>();
            Vector3 world = camRight * move.x + camForward * move.y;
            return new Vector2(world.x, world.z);
        }

        /// <summary>World aim direction from <paramref name="origin"/>: the mouse on the ground plane.</summary>
        public Vector3 ReadAimWorld(Vector3 origin)
        {
            if (_point == null) return _lastAim;
            if (Touch)
            {
                Vector2 stick = _touch.Move;
                if (stick.sqrMagnitude > _touchAimDeadZone * _touchAimDeadZone)
                {
                    GetCameraBasis(out Vector3 camForward, out Vector3 camRight);
                    _lastAim = (camRight * stick.x + camForward * stick.y).normalized;
                }
                return _lastAim;
            }
            if (TryMouseAim(origin, _point.ReadValue<Vector2>(), out Vector3 dir)) _lastAim = dir;
            return _lastAim;
        }

        private void GetCameraBasis(out Vector3 forward, out Vector3 right)
        {
            forward = _camera.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            right = new Vector3(forward.z, 0f, -forward.x);
        }

        private bool TryMouseAim(Vector3 origin, Vector2 pointer, out Vector3 dir)
        {
            dir = default;
            Ray ray = _camera.ScreenPointToRay(pointer);
            var plane = new Plane(Vector3.up, origin);
            if (!plane.Raycast(ray, out float enter)) return false;
            Vector3 to = ray.GetPoint(enter) - origin;
            to.y = 0f;
            if (to.sqrMagnitude < 0.04f) return false;
            dir = to.normalized;
            return true;
        }
    }
}
