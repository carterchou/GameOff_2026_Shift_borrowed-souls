using UnityEngine;

namespace BorrowedSouls.Combat
{
    /// <summary>
    /// One way a projectile can look (arrow, fire bolt...). When the projectile ends, the head disappears at once but the trail
    /// and embers are left to die out, so a shot never blinks out of existence.
    /// </summary>
    public sealed class ProjectileLookView : MonoBehaviour
    {
        [Tooltip("Seconds the trail and embers need to die out after the projectile ends.")]
        [SerializeField, Min(0f)] private float _lingerSeconds = 0.2f;

        private MeshRenderer[] _heads;
        private TrailRenderer[] _trails;
        private ParticleSystem[] _particles;

        public float LingerSeconds => _lingerSeconds;

        public bool HeadVisible => _heads != null && _heads.Length > 0 && _heads[0].enabled;

        private void Awake()
        {
            _heads = GetComponentsInChildren<MeshRenderer>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        /// <summary>Call after the projectile is at its muzzle, so the trail doesn't streak in from its last position.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            foreach (MeshRenderer head in _heads) head.enabled = true;
            foreach (TrailRenderer trail in _trails)
            {
                trail.Clear();
                trail.emitting = true;
            }
            foreach (ParticleSystem particle in _particles)
            {
                particle.Clear(false);
                particle.Play(false);
            }
        }

        public void Linger()
        {
            foreach (MeshRenderer head in _heads) head.enabled = false;
            foreach (TrailRenderer trail in _trails) trail.emitting = false;
            foreach (ParticleSystem particle in _particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
