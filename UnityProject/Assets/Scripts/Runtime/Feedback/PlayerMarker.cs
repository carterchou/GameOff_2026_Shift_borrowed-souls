using BorrowedSouls.Bootstrap;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>Ground ring under whichever vessel the player controls. It turns with the aim, so its pointer shows where the body faces.</summary>
    public sealed class PlayerMarker : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private Transform _ring;

        private void LateUpdate()
        {
            var vessel = _root.PlayerVessel;
            bool show = vessel != null && !vessel.IsDead;
            if (_ring.gameObject.activeSelf != show) _ring.gameObject.SetActive(show);
            if (!show) return;
            _ring.position = vessel.transform.position + Vector3.up * 0.03f;
            _ring.rotation = Quaternion.LookRotation(vessel.transform.forward, Vector3.up);
        }
    }
}
