using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Shows what is afflicting a body: flames on it, so it reads from across the arena, and an icon over its head, so it reads
    /// in a crowd. Burning is the only status in the game so far (VesselState); a new one adds a pair of effects here.
    /// </summary>
    public sealed class VesselStatusView : MonoBehaviour
    {
        [SerializeField] private VesselRuntime _vessel;
        [SerializeField] private ParticleSystem _burnFlames;
        [SerializeField] private ParticleSystem _burnIcon;

        private bool _burning;

        private void LateUpdate()
        {
            bool burning = _vessel.State != null && _vessel.State.IsBurning;
            if (burning == _burning) return;
            _burning = burning;
            if (burning)
            {
                _burnFlames.Play(false);
                _burnIcon.Play(false);
            }
            else
            {
                _burnFlames.Stop(false, ParticleSystemStopBehavior.StopEmitting); // flames die out on their own
                _burnIcon.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
