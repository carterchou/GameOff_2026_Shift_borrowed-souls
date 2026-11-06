using BorrowedSouls.Bootstrap;
using BorrowedSouls.Soul;
using Unity.Cinemachine;
using UnityEngine;

namespace BorrowedSouls.CameraRig
{
    /// <summary>
    /// Points the follow camera at the player's current vessel, or at the soul avatar while it flies free.
    /// Cinemachine damping smooths the hand-off so possession never snaps the view.
    /// </summary>
    public sealed class CameraFollowTarget : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private Transform _soulAvatar;

        private void LateUpdate()
        {
            Transform target = _root.PlayerVessel != null ? _root.PlayerVessel.transform : (_soul.IsFree ? _soulAvatar : null);
            if (target != null && _camera.Follow != target) _camera.Follow = target;
        }
    }
}
