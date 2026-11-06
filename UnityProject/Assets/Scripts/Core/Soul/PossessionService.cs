using System.Collections.Generic;

namespace BorrowedSouls.Core.Soul
{
    public enum SoulMode
    {
        InVessel,
        Free,
        Expired
    }

    public enum PossessionCheck
    {
        Ok,
        NotFree,
        Null,
        AlreadyPlayers,
        Broken,
        Boss,
        LivingAboveThreshold,
        OutOfRange
    }

    /// <summary>Tunables for the soul system. Values come from a ScriptableObject at runtime.</summary>
    public sealed class SoulConfig
    {
        public float SoulFreeLifetime = 5f;
        public float CorpseClaimLifetime = 10f;
        public float SoulInteractRange = 2.5f;
        public float EliteThresholdMultiplier = 0.5f;
        public float[] InvasionThresholdByLevel = { 0.15f, 0.25f, 0.35f, 0.45f, 0.55f };
        public float[] RestoreRatioByLevel = { 0.65f, 0.75f, 0.85f, 0.95f };
        public float SoulDurationBonusPerLevel = 1f;
        public float SoulRangeBonusPerLevel = 0.5f;
        public float EmberTimeBonus = 0.25f;
        public float EmberTimeBonusCap = 2f;
    }

    /// <summary>The single player consciousness: upgrades, soul timer, current vessel.</summary>
    public sealed class PlayerSoul
    {
        public SoulMode Mode { get; internal set; } = SoulMode.InVessel;
        public VesselState Current { get; internal set; }
        public float FreeTimeLeft { get; internal set; }
        public float EmberBonusThisFlight { get; internal set; }

        public int InvasionLevel;
        public int RestorationLevel;
        public int SoulDurationLevel;
        public int SoulRangeLevel;
        public int VitalityLevel;
        public int MightLevel;
        public int HasteLevel;
        public int BurstLevel;

        public int Possessions { get; internal set; }
    }

    public struct PossessionResult
    {
        public bool Success;
        public PossessionCheck Check;
        public bool FirstClaim;
        public bool SuppressedEnemySoul;
        public float HealedAmount;
        public VesselState From;
    }

    /// <summary>
    /// The only system allowed to change vessel ownership. Every transfer validates, then commits atomically,
    /// so there is never a frame with two player-owned vessels.
    /// Frame order expected from the runtime: resolve damage → possession input → <see cref="Tick"/>.
    /// </summary>
    public sealed class PossessionService
    {
        private readonly SoulConfig _config;

        public PossessionService(SoulConfig config)
        {
            _config = config;
        }

        public SoulConfig Config => _config;

        public float InvasionThreshold(PlayerSoul soul, VesselKind kind)
        {
            if (kind == VesselKind.Boss) return -1f;
            float t = _config.InvasionThresholdByLevel[Clamp(soul.InvasionLevel, _config.InvasionThresholdByLevel.Length)];
            return kind == VesselKind.Elite ? t * _config.EliteThresholdMultiplier : t;
        }

        public float RestoreRatio(PlayerSoul soul)
        {
            return _config.RestoreRatioByLevel[Clamp(soul.RestorationLevel, _config.RestoreRatioByLevel.Length)];
        }

        public float SoulLifetime(PlayerSoul soul)
        {
            return _config.SoulFreeLifetime + soul.SoulDurationLevel * _config.SoulDurationBonusPerLevel;
        }

        public float InteractRange(PlayerSoul soul)
        {
            return _config.SoulInteractRange + soul.SoulRangeLevel * _config.SoulRangeBonusPerLevel;
        }

        /// <summary>Legality only (no range): used by soul vision highlighting and by <see cref="TryPossess"/>.</summary>
        public PossessionCheck CheckTarget(PlayerSoul soul, VesselState target)
        {
            if (target == null) return PossessionCheck.Null;
            if (target.Kind == VesselKind.Boss) return PossessionCheck.Boss;
            if (target.IsBroken || target.Hp <= 0f && !target.IsFallenCorpse) return PossessionCheck.Broken;
            if (target.Owner == VesselOwner.Player) return PossessionCheck.AlreadyPlayers;
            if (target.Owner == VesselOwner.EnemyAI)
            {
                float threshold = InvasionThreshold(soul, target.Kind);
                if (target.Hp > target.MaxHp * threshold) return PossessionCheck.LivingAboveThreshold;
            }
            return PossessionCheck.Ok;
        }

        /// <summary>Run start: the soul begins inside its own body, which never grants a first-claim heal.</summary>
        public void AssignInitialVessel(PlayerSoul soul, VesselState vessel)
        {
            vessel.Owner = VesselOwner.Player;
            vessel.HasEnemySoul = false;
            vessel.EnemySoulSuppressed = false;
            vessel.ClaimedByPlayer = true;
            vessel.CorpseTimeLeft = -1f;
            soul.Current = vessel;
            soul.Mode = SoulMode.InVessel;
        }

        /// <summary>Hold Shift/LT while conscious. Leaves the current body in place.</summary>
        public bool ExitVoluntary(PlayerSoul soul, bool conscious)
        {
            if (soul.Mode != SoulMode.InVessel || soul.Current == null || !conscious) return false;
            Release(soul.Current);
            BeginFlight(soul);
            return true;
        }

        /// <summary>Call when the player's current body reached 0 HP (it is already Broken).</summary>
        public void OnCurrentVesselDestroyed(PlayerSoul soul)
        {
            if (soul.Mode != SoulMode.InVessel) return;
            BeginFlight(soul);
        }

        public PossessionResult TryPossess(PlayerSoul soul, VesselState target, float distance)
        {
            var result = new PossessionResult { From = soul.Current };
            if (soul.Mode != SoulMode.Free)
            {
                result.Check = PossessionCheck.NotFree;
                return result;
            }

            result.Check = CheckTarget(soul, target);
            if (result.Check != PossessionCheck.Ok) return result;
            if (distance > InteractRange(soul))
            {
                result.Check = PossessionCheck.OutOfRange;
                return result;
            }

            // Commit (atomic): suppress enemy soul, first-claim heal, ownership, soul state.
            if (target.Owner == VesselOwner.EnemyAI)
            {
                target.EnemySoulSuppressed = true;
                result.SuppressedEnemySoul = true;
            }

            // A corpse is always revived with the restore heal, even if this body was invaded alive before: death
            // resets it, and a 0 HP host could neither act nor die (soft lock). Returning to a vacated shell or
            // re-invading a living host never heals.
            bool corpse = target.IsFallenCorpse;
            target.RevivedFromCorpse = corpse;
            if (corpse || !target.ClaimedByPlayer)
            {
                result.FirstClaim = !target.ClaimedByPlayer;
                float before = target.Hp;
                float floor = target.MaxHp * RestoreRatio(soul);
                if (target.Hp < floor) target.SetHp(floor);
                target.ClaimedByPlayer = true;
                result.HealedAmount = target.Hp - before;
            }

            target.CorpseTimeLeft = -1f;
            target.Owner = VesselOwner.Player;
            soul.Current = target;
            soul.Mode = SoulMode.InVessel;
            soul.FreeTimeLeft = 0f;
            soul.Possessions++;
            result.Success = true;
            return result;
        }

        /// <summary>Soul flight picked up an ember: extend the timer up to the per-flight cap.</summary>
        public float AddEmberTime(PlayerSoul soul, int embers)
        {
            if (soul.Mode != SoulMode.Free || embers <= 0) return 0f;
            float room = _config.EmberTimeBonusCap - soul.EmberBonusThisFlight;
            float add = embers * _config.EmberTimeBonus;
            if (add > room) add = room;
            if (add <= 0f) return 0f;
            soul.EmberBonusThisFlight += add;
            soul.FreeTimeLeft += add;
            return add;
        }

        /// <summary>Advance soul and corpse timers on the GameplayUnscaled clock (pause stops it; slow-mo does not).</summary>
        public void Tick(PlayerSoul soul, IReadOnlyList<VesselState> vessels, float gameplayUnscaledDt)
        {
            if (gameplayUnscaledDt <= 0f) return;
            for (int i = 0; i < vessels.Count; i++) vessels[i].TickCorpse(gameplayUnscaledDt);

            if (soul.Mode != SoulMode.Free) return;
            soul.FreeTimeLeft -= gameplayUnscaledDt;
            if (soul.FreeTimeLeft <= 0f)
            {
                soul.FreeTimeLeft = 0f;
                soul.Mode = SoulMode.Expired;
            }
        }

        private void BeginFlight(PlayerSoul soul)
        {
            soul.Current = null;
            soul.Mode = SoulMode.Free;
            soul.FreeTimeLeft = SoulLifetime(soul);
            soul.EmberBonusThisFlight = 0f;
        }

        private static void Release(VesselState vessel)
        {
            if (vessel.HasEnemySoul && vessel.EnemySoulSuppressed)
            {
                // Living host: the original enemy soul takes back the same body with its current HP/status.
                vessel.EnemySoulSuppressed = false;
                vessel.Owner = VesselOwner.EnemyAI;
            }
            else if (vessel.RevivedFromCorpse)
            {
                vessel.Owner = VesselOwner.Broken; // a corpse is borrowed once: leaving it lets it fall apart
                vessel.HasEnemySoul = false;
            }
            else
            {
                vessel.Owner = VesselOwner.Empty; // vacated: returnable, never heals again
            }
        }

        private static int Clamp(int level, int length)
        {
            if (level < 0) return 0;
            return level >= length ? length - 1 : level;
        }
    }
}
