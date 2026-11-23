using BorrowedSouls.Localization;
using System;
using System.Text;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Core.Time;
using BorrowedSouls.Data;
using BorrowedSouls.Run;
using BorrowedSouls.Soul;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Esc / Menu pause (: 設定、操作說明、重新開始、返回休息室; the soul timer is paused too because
    /// TimeService pauses the gameplay clock). Never opens over the upgrade cards or once the run is on its way out.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private GameObject _panel;
        [SerializeField] private UpgradeSelectionPanel _upgradePanel;
        [SerializeField] private ReturnToRestRoom _return;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private UpgradeCatalog _upgradeTexts;
        [SerializeField] private Button _resume;
        [SerializeField] private Button _upgrades;
        [SerializeField] private Button _controls;
        [SerializeField] private Button _shake;
        [SerializeField] private Button _flashes;
        [SerializeField] private Button _hints;
        [SerializeField] private Button _numbers;
        [SerializeField] private Button _volume;
        [SerializeField] private Button _restart;
        [SerializeField] private Button _quit;
        [SerializeField] private Text _shakeLabel;
        [SerializeField] private Text _flashesLabel;
        [SerializeField] private Text _hintsLabel;
        [SerializeField] private Text _numbersLabel;
        [SerializeField] private Text _volumeLabel;

        [Header("Pages")]
        [SerializeField] private GameObject _upgradesPage;
        [SerializeField] private Text _upgradesList;
        [SerializeField] private Button _upgradesBack;
        [SerializeField] private GameObject _controlsPage;
        [SerializeField] private Button _controlsBack;

        private InputAction _pause;
        private TimeRequestHandle _handle;

        public bool IsOpen => _panel.activeSelf;

        private bool PageOpen => _upgradesPage.activeSelf || _controlsPage.activeSelf;

        private void Awake()
        {
            _pause = _actions.FindActionMap("Gameplay", true).FindAction("Pause", true);
            _panel.SetActive(false);
            HidePages();
            _resume.onClick.AddListener(Close);
            _upgrades.onClick.AddListener(OpenUpgrades);
            _controls.onClick.AddListener(() => OpenPage(_controlsPage, _controlsBack));
            _upgradesBack.onClick.AddListener(() => ClosePage(_upgrades));
            _controlsBack.onClick.AddListener(() => ClosePage(_controls));
            _shake.onClick.AddListener(() => { _root.Options.CycleShake(); Refresh(); });
            _flashes.onClick.AddListener(() => { _root.Options.ToggleReduceFlashes(); Refresh(); });
            _hints.onClick.AddListener(() => { _root.Options.ToggleHints(); Refresh(); });
            _numbers.onClick.AddListener(() => { _root.Options.ToggleDamageNumbers(); Refresh(); });
            _volume.onClick.AddListener(() => { _root.Options.CycleVolume(); Refresh(); });
            _restart.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            _quit.onClick.AddListener(() => { Close(); _return.LeaveNow(); });
        }

        private void Update()
        {
            bool back = _pause.WasPressedThisFrame();
            if (!back) return;
            if (PageOpen) ClosePage(_upgradesPage.activeSelf ? _upgrades : _controls);
            else Toggle();
        }

        /// <summary>What the pause button does: closes the menu, or opens it unless another screen is up.</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else if (!_upgradePanel.IsOpen && (_return == null || !_return.Leaving)) Open();
        }

        private void Open()
        {
            HidePages();
            _panel.SetActive(true);
            if (!_handle.IsValid) _handle = _root.Time.Stack.Push(TimeRequestKind.Pause);
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_resume.gameObject);
        }

        private void Close()
        {
            HidePages();
            _panel.SetActive(false);
            if (_handle.IsValid)
            {
                _root.Time.Stack.Pop(_handle);
                _handle = default;
            }
        }

        private void HidePages()
        {
            _upgradesPage.SetActive(false);
            _controlsPage.SetActive(false);
        }

        private void OpenPage(GameObject page, Selectable back)
        {
            page.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(back.gameObject);
        }

        private void ClosePage(Selectable returnTo)
        {
            HidePages();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(returnTo.gameObject);
        }

        private void OpenUpgrades()
        {
            _upgradesList.text = DescribeUpgrades();
            OpenPage(_upgradesPage, _upgradesBack);
        }

        /// <summary>Each upgrade this run has taken, with what its level does now.</summary>
        private string DescribeUpgrades()
        {
            var text = new StringBuilder();
            foreach (UpgradeId id in Enum.GetValues(typeof(UpgradeId)))
            {
                int level = UpgradeService.GetLevel(_soul.Soul, id);
                if (level <= 0) continue;
                string effect = _upgradeTexts.Describe(id, level - 1).Replace('\n', ' ');
                text.Append($"<color=#9FE8FF>{_upgradeTexts.NameOf(id)}</color>  Lv {level}\n<size=25>{effect}</size>\n\n");
            }
            return text.Length > 0 ? text.ToString() : Loc.T("這局還沒選過升級。\n清完一波就能選。");
        }

        private void Refresh()
        {
            GameOptions o = _root.Options;
            _shakeLabel.text = Loc.F("鏡頭震動：{0}", Loc.T(o.ShakeName));
            _flashesLabel.text = Loc.F("減少閃光：{0}", OnOff(o.ReduceFlashes));
            _hintsLabel.text = Loc.F("教學提示：{0}", OnOff(o.TutorialHints));
            _numbersLabel.text = Loc.F("傷害數字：{0}", OnOff(o.DamageNumbers));
            _volumeLabel.text = Loc.F("音量：{0}%", Mathf.RoundToInt(o.MasterVolume * 100f));
        }

        private static string OnOff(bool on) => Loc.T(on ? "開" : "關");
    }
}
