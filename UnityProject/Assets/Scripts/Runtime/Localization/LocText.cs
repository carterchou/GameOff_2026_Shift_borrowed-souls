using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.Localization
{
    /// <summary>A fixed label written in Chinese in the scene; shows it in the current language when it wakes.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocText : MonoBehaviour
    {
        private void Awake()
        {
            var text = GetComponent<Text>();
            text.text = Loc.T(text.text);
        }
    }
}
