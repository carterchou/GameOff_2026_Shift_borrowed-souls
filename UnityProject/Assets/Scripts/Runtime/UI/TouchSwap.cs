using BorrowedSouls.InputHandling;
using UnityEngine;

namespace BorrowedSouls.UI
{
    /// <summary>Shows one set of objects with a keyboard and another on a touch device, e.g. the controls page's key card.</summary>
    public sealed class TouchSwap : MonoBehaviour
    {
        [SerializeField] private GameObject[] _keyboardOnly = new GameObject[0];
        [SerializeField] private GameObject[] _touchOnly = new GameObject[0];

        private void Awake()
        {
            bool touch = TouchControls.Active;
            foreach (GameObject go in _keyboardOnly) if (go != null) go.SetActive(!touch);
            foreach (GameObject go in _touchOnly) if (go != null) go.SetActive(touch);
        }
    }
}
