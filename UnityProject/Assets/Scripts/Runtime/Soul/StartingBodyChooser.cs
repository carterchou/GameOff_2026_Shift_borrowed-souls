using BorrowedSouls.Core.Meta;
using BorrowedSouls.Data;
using BorrowedSouls.Debugging;
using BorrowedSouls.Meta;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Soul
{
    /// <summary>
    /// Wakes the body the player chose on the bench. The three wait in the scene at the same spot, and the other two are never switched
    /// on, so nothing else in the run has to know there was a choice. Runs before every body's own Awake. A bot always takes the first.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class StartingBodyChooser : MonoBehaviour
    {
        [SerializeField] private TalentCatalog _catalog;
        [SerializeField] private PlayerSoulController _soul;
        [Tooltip("One per StartingBody, in that order.")]
        [SerializeField] private VesselRuntime[] _bodies = new VesselRuntime[0];

        private void Awake()
        {
            int chosen = AutoPilot.RequestedAtLaunch ? (int)StartingBody.Swordsman : (int)ProfileStore.Current(_catalog).Body;
            for (int i = 0; i < _bodies.Length; i++) _bodies[i].gameObject.SetActive(i == chosen);
            _soul.UseStartingVessel(_bodies[chosen]);
        }
    }
}
