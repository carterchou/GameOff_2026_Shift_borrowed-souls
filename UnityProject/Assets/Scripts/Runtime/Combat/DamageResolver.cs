using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Events;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Combat
{
    /// <summary>
    /// Who dealt an attack, captured when the attack was launched. Projectiles keep this snapshot so an arrow
    /// fired by an enemy stays an enemy arrow even if the player possesses the archer mid-flight.
    /// </summary>
    public readonly struct AttackSource
    {
        public readonly VesselRuntime Vessel;
        public readonly Team Team;
        public readonly VesselClass Class;
        public readonly float DamageMultiplier;

        public AttackSource(VesselRuntime vessel, Team team, VesselClass cls, float damageMultiplier)
        {
            Vessel = vessel;
            Team = team;
            Class = cls;
            DamageMultiplier = damageMultiplier;
        }

        public static AttackSource From(VesselRuntime v) => new AttackSource(v, v.Team, v.Class, v.DamageMultiplier.Value);
    }

    public enum HitOutcome
    {
        Ignored,
        Blocked,
        Hit
    }

    /// <summary>
    /// Single entry point for damage: hostility, invulnerability, mage projectile shield,
    /// counter bonus, status payload, application to the vessel, and the HitConfirmed event.
    /// Never assumes the attacker is the player.
    /// </summary>
    public sealed class DamageResolver
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        // "-botgod" on a development build: the player's body takes no damage, so long bot runs can observe AI
        // behaviour (e.g. elites smashing corpses). Not compiled into the editor or release builds.
        private static readonly bool DevPlayerInvulnerable = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-botgod") >= 0;
#endif
        private readonly EventBus _events;
        private readonly CombatSettings _settings;

        public DamageResolver(EventBus events, CombatSettings settings)
        {
            _events = events;
            _settings = settings;
        }

        public HitOutcome Resolve(in AttackSource source, VesselRuntime target, AttackDefinition attack, Vector3 point, Vector3 direction, bool isProjectile)
        {
            if (target == null || target == source.Vessel) return HitOutcome.Ignored;
            if (target.IsDead) return HitOutcome.Ignored;
            if (!CombatRules.IsHostile(source.Team, target.Team)) return HitOutcome.Ignored;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (DevPlayerInvulnerable && target.Team == Team.Player) return HitOutcome.Ignored;
#endif

            if (target.IsInvulnerable || (isProjectile && target.TryConsumeProjectileShield()))
            {
                _events.Publish(new HitEvaded { Attacker = source.Vessel, Target = target, Point = point });
                return HitOutcome.Blocked;
            }

            float counter = CombatRules.CounterMultiplier(source.Class, target.Class, _settings.CounterBonus);
            float damage = CombatRules.ComputeDamage(attack.Damage, source.DamageMultiplier, counter, 1f);

            VesselHitResult result = target.ReceiveHit(attack, damage, direction);
            if (!result.Killed && attack.BurnDuration > 0f) target.State.ApplyBurn(attack.BurnDuration, attack.BurnDamage);

            _events.Publish(new HitConfirmed
            {
                Attacker = source.Vessel,
                AttackerTeam = source.Team,
                Target = target,
                Attack = attack,
                Damage = damage,
                Point = point,
                Direction = direction,
                Tier = attack.Tier,
                IsKill = result.Killed,
                Interrupted = result.Interrupted
            });

            if (result.Killed)
            {
                _events.Publish(new VesselDied { Vessel = target, Killer = source.Vessel, LastHitDirection = direction });
            }
            return HitOutcome.Hit;
        }
    }
}
