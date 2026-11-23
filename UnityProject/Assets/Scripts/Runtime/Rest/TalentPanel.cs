using BorrowedSouls.Localization;
using System;
using BorrowedSouls.Core.Meta;
using BorrowedSouls.Data;
using BorrowedSouls.Meta;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BorrowedSouls.Rest
{
    /// <summary>Spends soul crystals in the rest room: one row per talent, no tree.
    public sealed class TalentPanel : MonoBehaviour
    {
        [SerializeField] private TalentCatalog _catalog;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _crystals;
        [SerializeField] private Button[] _rows = Array.Empty<Button>();
        [SerializeField] private Text[] _labels = Array.Empty<Text>();
        [SerializeField] private Button _back;

        public event Action Closed;

        private void Awake()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                int row = i;
                _rows[i].onClick.AddListener(() => Buy(row));
            }
            _back.onClick.AddListener(() => Closed?.Invoke());
        }

        public void Open()
        {
            _panel.SetActive(true);
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_rows[0].gameObject);
        }

        public void Hide() => _panel.SetActive(false);

        /// <summary>Tries to buy the next level of a row's talent. A row that can't be bought just stays as it is.</summary>
        public void Buy(int row)
        {
            ProfileState profile = ProfileStore.Current(_catalog);
            if (!profile.TryBuy(_catalog.Rules[row])) return;
            ProfileStore.Save(_catalog);
            Refresh();
        }

        private void Refresh()
        {
            ProfileState profile = ProfileStore.Current(_catalog);
            _crystals.text = Loc.F("魂晶  {0}", profile.Crystals);
            for (int i = 0; i < _labels.Length; i++)
            {
                TalentCatalog.Entry entry = _catalog.Entries[i];
                TalentRule rule = _catalog.Rules[i];
                int cost = profile.NextCost(rule);
                string price = cost < 0 ? Loc.T("已滿級") : Loc.F("{0} 魂晶", cost);
                _labels[i].text = $"{Loc.T(entry.DisplayName)}  Lv {profile.Level(entry.Id)}／{rule.MaxLevel}　　{price}\n<size=22>{Loc.T(entry.Effect)}</size>";
            }
        }
    }
}
