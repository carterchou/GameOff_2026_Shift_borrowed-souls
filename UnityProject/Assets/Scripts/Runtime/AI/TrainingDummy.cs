using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.AI
{
    /// <summary>
    /// Grey-box helper: idle driver that restores its body once the corpse has dissolved (after the 10 s claim
    /// window), so feel and possession testing never runs out of targets.
    /// </summary>
    [RequireComponent(typeof(VesselRuntime))]
    public sealed class TrainingDummy : MonoBehaviour, IVesselDriver
    {
        [SerializeField, Min(0f)] private float _restoreDelay = 1.5f;

        private VesselRuntime _vessel;
        private float _brokenTime;

        private void Awake()
        {
            _vessel = GetComponent<VesselRuntime>();
        }

        public void BuildIntent(VesselRuntime vessel, ref VesselIntent intent)
        {
        }

        private void Update()
        {
            if (!_vessel.State.IsBroken)
            {
                _brokenTime = 0f;
                return;
            }
            _brokenTime += _vessel.Root.Time.WorldDelta;
            if (_brokenTime >= _restoreDelay) _vessel.Restore();
        }
    }
}
