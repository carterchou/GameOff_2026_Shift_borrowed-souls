using BorrowedSouls.Localization;
using BorrowedSouls.Run;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>Says the run is won and counts down to the way home (<see cref="ReturnToRestRoom"/>). Text only changes when the second does.</summary>
    public sealed class VictoryBanner : MonoBehaviour
    {
        [SerializeField] private ReturnToRestRoom _return;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _countdown;

        private int _shownSeconds = -1;

        private void LateUpdate()
        {
            float left = _return.VictoryLeft;
            bool show = left > 0f;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (!show) return;
            int seconds = Mathf.CeilToInt(left);
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            _countdown.text = Loc.F("{0} 秒後返回休息區", seconds);
        }
    }
}
