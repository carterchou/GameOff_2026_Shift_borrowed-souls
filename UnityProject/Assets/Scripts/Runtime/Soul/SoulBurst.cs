using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Soul
{
    /// <summary>
    /// SoulBurst upgrade: a successful possession releases a small soul blast around the new body.
    /// It cannot chain into another possession — possession always needs the player's input — and it never
    /// hurts the body just entered.
    /// </summary>
    public sealed class SoulBurst : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private AttackDefinition _burst;
        [SerializeField, Min(0f)] private float _radius = 3.5f;
        [SerializeField] private LayerMask _vesselMask = ~0;

        private readonly Collider[] _hits = new Collider[24];

        private void OnEnable() => _root.Events.Subscribe<PossessionSucceeded>(OnPossessed);
        private void OnDisable() => _root.Events.Unsubscribe<PossessionSucceeded>(OnPossessed);

        private void OnPossessed(PossessionSucceeded e)
        {
            if (_soul.Soul.BurstLevel <= 0 || e.To == null || _burst == null) return;
            var source = new AttackSource(e.To, Team.Player, e.To.Class, 1f + 0.1f * _soul.Soul.MightLevel);
            Vector3 center = e.To.transform.position;
            int count = Physics.OverlapSphereNonAlloc(center + Vector3.up, _radius, _hits, _vesselMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!_hits[i].TryGetComponent(out VesselRuntime v) || v == e.To) continue;
                Vector3 dir = v.transform.position - center;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                _root.Damage.Resolve(source, v, _burst, v.transform.position + Vector3.up, dir, isProjectile: false);
            }
        }
    }
}
