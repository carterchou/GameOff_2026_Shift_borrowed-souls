using BorrowedSouls.Localization;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Run;
using BorrowedSouls.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Minimal player HUD: top-left body/HP or SOUL FREE countdown, top-centre wave and
    /// remaining enemies, ember bar with banked upgrades, contextual hint line. Text only changes when values do.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private EmberSystem _embers;
        [SerializeField] private WaveDirector _waves;
        [SerializeField] private Text _bodyText;
        [SerializeField] private Image _hpFill;
        [SerializeField] private Text _waveText;
        [SerializeField] private Image _emberFill;
        [SerializeField] private Text _emberText;
        [SerializeField] private Text _hintText;
        [SerializeField] private Text _centerText;
        [SerializeField] private GameObject _bossBar;
        [SerializeField] private Image _bossFill;
        [Tooltip("Real seconds a boss's announced attack stays named on screen.")]
        [SerializeField, Min(0f)] private float _calloutSeconds = 1.6f;

        private float _hintUntil;
        private float _centerUntil;
        private int _lastHp = -1;
        private int _lastSoulTenths = -1;
        private int _lastWaveKey = -1;
        private int _lastEmberKey = -1;
        private VesselRuntime _lastVessel;
        private bool _won;

        private void OnEnable()
        {
            _root.Events.Subscribe<WaveStarted>(OnWaveStarted);
            _root.Events.Subscribe<WaveCleared>(OnWaveCleared);
            _root.Events.Subscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Subscribe<SoulExited>(OnSoulExited);
            _root.Events.Subscribe<BossAttackAnnounced>(OnBossAttack);
            _root.Events.Subscribe<RunEnded>(OnRunEnded);
        }

        private void OnDisable()
        {
            _root.Events.Unsubscribe<WaveStarted>(OnWaveStarted);
            _root.Events.Unsubscribe<WaveCleared>(OnWaveCleared);
            _root.Events.Unsubscribe<PossessionSucceeded>(OnPossessed);
            _root.Events.Unsubscribe<SoulExited>(OnSoulExited);
            _root.Events.Unsubscribe<BossAttackAnnounced>(OnBossAttack);
            _root.Events.Unsubscribe<RunEnded>(OnRunEnded);
        }

        /// <summary>Shows a hint line; if another hint is still visible, stacks them (max two lines).</summary>
        public void ShowHint(string text, float seconds)
        {
            if (_won) return; // advice is no use any more, and the bodies falling around the player would set it off
            text = InputHints.For(text);
            bool visible = _hintText.text.Length > 0 && Time.unscaledTime < _hintUntil;
            if (visible && _hintText.text != text && _hintText.text.IndexOf('\n') < 0) _hintText.text = _hintText.text + "\n" + text;
            else _hintText.text = text;
            _hintUntil = Mathf.Max(_hintUntil, Time.unscaledTime + seconds);
        }

        public void ShowCenter(string text, float seconds)
        {
            _centerText.text = text;
            _centerUntil = Time.unscaledTime + seconds;
        }

        private void OnWaveStarted(WaveStarted e)
        {
            ShowCenter($"WAVE {e.WaveNumber} / {e.TotalWaves}\n{e.Title}", 2f);
            if (!string.IsNullOrEmpty(e.Hint)) ShowHint(e.Hint, 8f);
        }

        private void OnWaveCleared(WaveCleared e)
        {
            if (_waves == null || e.WaveNumber < _waves.TotalWaves) ShowCenter("CLEARED", 1.5f); // the last one has the victory banner
        }

        private void OnRunEnded(RunEnded e)
        {
            if (!e.Victory) return;
            _won = true;
            _hintText.text = "";
        }

        private void OnBossAttack(BossAttackAnnounced e)
        {
            if (!string.IsNullOrEmpty(e.Attack.Callout)) ShowCenter($"<color=#e8b4ff>{e.Attack.Callout}</color>", _calloutSeconds);
        }

        private void OnPossessed(PossessionSucceeded e)
        {
            string extra = e.FirstClaim ? $"  +{e.Healed:0} HP" : "";
            ShowCenter($"{e.To.Definition.DisplayName} — {e.To.Definition.Weapon.DisplayName}{extra}", 1.2f);
        }

        private void OnSoulExited(SoulExited e)
        {
            if (e.EnemySoulResumed) ShowHint("離開活體：原本的敵魂奪回了身體", 3f);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (_hintText.text.Length > 0 && now > _hintUntil) _hintText.text = "";
            if (_centerText.text.Length > 0 && now > _centerUntil) _centerText.text = "";

            UpdateBody();
            UpdateWave();
            UpdateEmbers();
            UpdateBoss();
        }

        private void UpdateBody()
        {
            VesselRuntime v = _root.PlayerVessel;
            if (v != null)
            {
                int hp = Mathf.CeilToInt(v.Hp);
                if (hp != _lastHp || v != _lastVessel)
                {
                    string ghost = v.State.EnemySoulSuppressed ? "\n<color=#b77cff>" + Loc.T("離開後敵人將以此 HP 復活") + "</color>" : "";
                    _bodyText.text = $"{v.Definition.DisplayName} · {v.Definition.Weapon.DisplayName}\nHP {hp} / {Mathf.CeilToInt(v.MaxHp.Value)}{ghost}";
                    _hpFill.fillAmount = v.MaxHp.Value > 0f ? v.Hp / v.MaxHp.Value : 0f;
                    _lastHp = hp;
                    _lastVessel = v;
                    _lastSoulTenths = -1;
                }
                return;
            }

            _lastVessel = null;
            _lastHp = -1;
            PlayerSoul s = _soul.Soul;
            int tenths = Mathf.CeilToInt(s.FreeTimeLeft * 10f);
            if (tenths == _lastSoulTenths) return;
            _lastSoulTenths = tenths;
            _bodyText.text = s.Mode == SoulMode.Expired
                ? "<color=#ff5050>" + Loc.T("靈魂消散") + "</color>"
                : $"<color=#7fe8ff>SOUL FREE  {s.FreeTimeLeft:0.0}s</color>\n" + InputHints.For("放開／再按 Shift 侵入發光的身體");
            _hpFill.fillAmount = 0f;
        }

        private void UpdateWave()
        {
            if (_waves == null) return;
            int key = _waves.WaveNumber * 1000 + _waves.RemainingEnemies;
            if (key == _lastWaveKey) return;
            _lastWaveKey = key;
            _waveText.text = _waves.WaveNumber > 0 ? Loc.F("WAVE {0} / {1}   敵人 {2}", _waves.WaveNumber, _waves.TotalWaves, _waves.RemainingEnemies) : "";
        }

        private void UpdateBoss()
        {
            if (_bossBar == null || _waves == null) return;
            VesselRuntime boss = _waves.Boss;
            bool show = boss != null;
            if (_bossBar.activeSelf != show) _bossBar.SetActive(show);
            if (show) _bossFill.fillAmount = boss.Hp / boss.MaxHp.Value;
        }

        private void UpdateEmbers()
        {
            var w = _embers.Wallet;
            int key = w.Progress * 100 + w.BankedUpgrades;
            if (key == _lastEmberKey) return;
            _lastEmberKey = key;
            _emberFill.fillAmount = w.Fill01;
            _emberText.text = w.BankedUpgrades > 0 ? Loc.F("魂燼  本波已存入升級 ×{0}", w.BankedUpgrades) : Loc.T("魂燼");
        }
    }
}
