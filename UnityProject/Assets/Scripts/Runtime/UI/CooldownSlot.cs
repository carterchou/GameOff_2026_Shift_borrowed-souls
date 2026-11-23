using BorrowedSouls.Localization;
using BorrowedSouls.Core.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>One cooldown tile: a mask that drains as it recovers, seconds while cooling, the input keys when ready.</summary>
    public sealed class CooldownSlot : MonoBehaviour
    {
        [SerializeField] private Image _mask;
        [SerializeField] private Text _text;
        [SerializeField] private string _label = "";
        [SerializeField] private string _keys = "";
        [SerializeField] private Color _readyColor = Color.white;
        [SerializeField] private Color _coolingColor = new Color(0.7f, 0.7f, 0.7f);

        private int _lastTenths = -1;

        public void Show(float left, float total)
        {
            _mask.fillAmount = HudRules.CooldownRemaining01(left, total);
            int tenths = HudRules.CooldownTenths(left);
            if (tenths == _lastTenths) return; // text only changes when the shown value does
            _lastTenths = tenths;
            _text.text = tenths > 0 ? $"{Loc.T(_label)}\n{tenths / 10f:0.0}s" : $"{Loc.T(_label)}\n{_keys}";
            _text.color = tenths > 0 ? _coolingColor : _readyColor;
        }
    }
}
