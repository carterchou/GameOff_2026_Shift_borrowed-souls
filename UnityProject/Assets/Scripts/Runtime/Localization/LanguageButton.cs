using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorrowedSouls.Localization
{
    /// <summary>
    /// Switches between English and Chinese. The label names the other language in its own script, and the scene
    /// reloads so every label wakes up again in the new language. Sits in the screen corner and only shows while the
    /// main menu does.
    /// </summary>
    [RequireComponent(typeof(Button), typeof(CanvasGroup))]
    public sealed class LanguageButton : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private GameObject _visibleWith;

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _label.text = Loc.English ? "中文" : "English";
            GetComponent<Button>().onClick.AddListener(Switch);
            LateUpdate();
        }

        private void LateUpdate()
        {
            bool show = _visibleWith == null || _visibleWith.activeInHierarchy;
            if (_group.interactable == show && _group.alpha == (show ? 1f : 0f)) return;
            _group.alpha = show ? 1f : 0f;
            _group.interactable = show;
            _group.blocksRaycasts = show;
        }

        private void Switch()
        {
            Loc.Set(Loc.English ? GameLanguage.Chinese : GameLanguage.English);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
