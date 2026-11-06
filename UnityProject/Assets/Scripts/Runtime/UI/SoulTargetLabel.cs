using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>The name over one legal soul target; placed by <see cref="SoulTargetMarkers"/>.</summary>
    public sealed class SoulTargetLabel : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private Text _text;
        [SerializeField] private Color _color = new Color(0.75f, 0.97f, 1f);
        [SerializeField] private Color _targetColor = Color.white;
        [SerializeField, Min(1)] private int _fontSize = 26;
        [SerializeField, Min(1)] private int _targetFontSize = 34;

        public void Show(Vector2 anchoredPosition, string displayName, bool targeted)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _rect.anchoredPosition = anchoredPosition;
            if (_text.text != displayName) _text.text = displayName;
            _text.color = targeted ? _targetColor : _color;
            _text.fontSize = targeted ? _targetFontSize : _fontSize;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
