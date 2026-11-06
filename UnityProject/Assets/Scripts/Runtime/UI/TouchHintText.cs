using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>A fixed label that names a key, renamed to the on-screen button on a touch device.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class TouchHintText : MonoBehaviour
    {
        private void Awake()
        {
            Text text = GetComponent<Text>();
            text.text = InputHints.For(text.text);
        }
    }
}
