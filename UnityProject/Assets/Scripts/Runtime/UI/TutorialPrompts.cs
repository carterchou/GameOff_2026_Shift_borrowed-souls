using BorrowedSouls.Bootstrap;
using BorrowedSouls.Data;
using BorrowedSouls.Vessels;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Run;
using BorrowedSouls.Soul;
using UnityEngine;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Contextual tutorial. Each prompt fires once per
    /// run, triggered by what the player actually does — first corpse, first soul flight, first body of each
    /// class, first living host. Short text plus input names; can be switched off.
    /// </summary>
    public sealed class TutorialPrompts : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private GameHud _hud;
        [SerializeField] private bool _enabled = true;
        [SerializeField, Min(1f)] private float _seconds = 7f;
        [SerializeField] private PlayerSoulController _soul;
        [Tooltip("Seconds between looks at the player's HP and the bodies nearby.")]
        [SerializeField, Min(0.05f)] private float _borrowCheckInterval = 0.5f;

        private float _borrowCheckLeft;
        private float _borrowCooldownLeft;

        private bool _corpseShown;
        private bool _flightShown;
        private bool _livingShown;
        private bool _returnShown;
        private int _classesShown; // bit per VesselClass

        private void OnEnable()
        {
            _root.Events.Subscribe<VesselDied>(OnVesselDied);
            _root.Events.Subscribe<SoulExited>(OnSoulExited);
            _root.Events.Subscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Subscribe<WaveStarted>(OnWaveStarted);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<VesselDied>(OnVesselDied);
            _root.Events.Unsubscribe<SoulExited>(OnSoulExited);
            _root.Events.Unsubscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Unsubscribe<WaveStarted>(OnWaveStarted);
        }

        /// <summary>Borrowing is the main way to heal, and a player can go a whole run without finding that out (2026-11-05 playtest).</summary>
        private void Update()
        {
            float dt = _root.Time.WorldDelta;
            _borrowCooldownLeft -= dt;
            _borrowCheckLeft -= dt;
            if (_borrowCheckLeft > 0f || _soul == null) return;
            _borrowCheckLeft = _borrowCheckInterval;

            VesselRuntime body = _root.PlayerVessel;
            if (body == null) return;
            SoulSettings settings = _root.SoulSettings;
            float hpShare = body.State.Hp / body.State.MaxHp;
            if (!BodyRecovery.ShouldHintBorrow(hpShare, settings.BorrowHintHpShare, LegalBodyWithin(body, settings.BorrowHintRange), _borrowCooldownLeft)) return;
            _borrowCooldownLeft = settings.BorrowHintCooldown;
            Show("HP 偏低：長按 Shift 離魂，借附近發光的身體就能恢復");
        }

        private bool LegalBodyWithin(VesselRuntime body, float range)
        {
            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime other = vessels[i];
                if (other == body || !_soul.IsLegalTarget(other)) continue;
                Vector3 to = other.transform.position - body.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude <= range * range) return true;
            }
            return false;
        }

        private void Show(string text)
        {
            if (_enabled && _root.Options.TutorialHints) _hud.ShowHint(text, _seconds);
        }

        private void OnVesselDied(VesselDied e)
        {
            if (_corpseShown || e.Vessel == null || !e.Vessel.State.IsFallenCorpse) return;
            _corpseShown = true;
            Show("敵人倒下的身體可以借用 10 秒：長按 Shift 離魂");
        }

        private void OnSoulExited(SoulExited e)
        {
            if (_flightShown) return;
            _flightShown = true;
            Show("靈魂可穿牆、無法被攻擊，但只能存在幾秒：飛到發光的身體，放開 Shift 侵入");
        }

        private void OnPossessed(PossessionSucceeded e)
        {
            if (e.SuppressedEnemySoul && !_livingShown)
            {
                _livingShown = true;
                Show("你壓制了活著的敵魂：離開這具身體時，敵人會以當下的 HP 奪回控制");
                return;
            }

            if (!e.FirstClaim && !_returnShown)
            {
                _returnShown = true;
                Show("回到留下的身體：HP 與狀態都保留，但不會再回血");
                return;
            }

            int bit = 1 << (int)e.To.Class;
            if ((_classesShown & bit) != 0) return;
            _classesShown |= bit;
            switch (e.To.Class)
            {
                case VesselClass.Mage:
                    Show("火法師：左鍵火球（燃燒）· 右鍵烈焰槍（貫穿）· E 焚風｜火焰剋長弓");
                    break;
                case VesselClass.Ranged:
                    Show("長弓手：按住左鍵連射 · 右鍵貫穿箭 · E 三連射｜距離剋長劍");
                    break;
                default:
                    Show("長劍戰士：左鍵三段斬 · 右鍵重斬（打斷施法）· E 突進斬｜長劍剋法師");
                    break;
            }
        }

        private void OnWaveStarted(WaveStarted e)
        {
            if (e.WaveNumber == 1) Show("WASD 移動 · 滑鼠瞄準 · 左鍵／右鍵／E 攻擊 · Space 閃避（有無敵時間）");
        }
    }
}
