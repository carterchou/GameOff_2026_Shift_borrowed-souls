using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>One small bar placed by <see cref="WorldHealthBars"/>.</summary>
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private Image _fill;

        public void Show(Vector2 anchoredPosition, float hp01, Color color)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _rect.anchoredPosition = anchoredPosition;
            _fill.fillAmount = hp01;
            _fill.color = color;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
