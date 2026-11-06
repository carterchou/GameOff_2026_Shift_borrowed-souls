using BorrowedSouls.Core.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Flame of a cone attack. Burns for exactly the attack's active window and is sized from the
    /// attack data, so what the player sees is the area that hits. Additive, so bodies stay readable through it.
    /// </summary>
    public sealed class ConeFlameView : MonoBehaviour
    {
        [SerializeField] private VesselRuntime _vessel;
        [SerializeField] private ParticleSystem _flame;
        [Tooltip("Seconds a flame puff takes to reach the attack's full range.")]
        [SerializeField, Min(0.05f)] private float _puffLifetime = 0.45f;

        private bool _burning;

        private void LateUpdate()
        {
            VesselCombat combat = _vessel.Combat;
            AttackDefinition attack = combat.CurrentAttack;
            bool burning = attack != null && attack.Delivery == AttackDelivery.Cone && combat.Phase == AttackPhase.Active;
            if (burning == _burning) return;
            _burning = burning;

            if (burning) Ignite(attack);
            else _flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Ignite(AttackDefinition attack)
        {
            ParticleSystem.MainModule main = _flame.main;
            main.startLifetime = _puffLifetime;
            main.startSpeed = Mathf.Max(0f, attack.Range - _flame.transform.localPosition.z) / _puffLifetime;
            main.startColor = attack.ProjectileColor;
            ParticleSystem.ShapeModule shape = _flame.shape;
            shape.angle = attack.ArcDegrees * 0.5f; // a cone's angle is measured from its centre line
            _flame.Play(true);
        }
    }
}
