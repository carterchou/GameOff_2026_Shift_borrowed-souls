using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Wave-end 3-choice cards: mouse click or number keys 1/2/3.
    /// Ignores input for a short guard after opening so a held attack button cannot pick a card by accident
    ///. Runs on real time because the game is paused while it is open.
    /// </summary>
    public sealed class UpgradeSelectionPanel : MonoBehaviour
    {
        public struct Card
        {
            public string Title;
            public string Body;
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _header;
        [SerializeField] private Button[] _buttons = new Button[0];
        [SerializeField] private Text[] _titles = new Text[0];
        [SerializeField] private Text[] _bodies = new Text[0];
        [SerializeField, Min(0f)] private float _inputGuard = 0.35f;

        private Action<int> _onPick;
        private float _openedAt;
        private int _count;

        public bool IsOpen => _panel.activeSelf;

        /// <summary>Automation (autopilot balance runs): pick a random card as soon as the input guard allows.</summary>
        public bool AutoPick { get; set; }

        private void Awake()
        {
            _panel.SetActive(false);
            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                _buttons[i].onClick.AddListener(() => Pick(index));
            }
        }

        public void Show(string header, IReadOnlyList<Card> cards, Action<int> onPick)
        {
            _onPick = onPick;
            _count = Mathf.Min(cards.Count, _buttons.Length);
            _header.text = header;
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool used = i < _count;
                _buttons[i].gameObject.SetActive(used);
                if (!used) continue;
                _titles[i].text = $"[{i + 1}] {cards[i].Title}";
                _bodies[i].text = cards[i].Body;
            }
            _panel.SetActive(true);
            _openedAt = Time.unscaledTime;
            if (_count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_buttons[0].gameObject);
        }

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime - _openedAt < _inputGuard) return;
            if (AutoPick)
            {
                Pick(UnityEngine.Random.Range(0, _count));
                return;
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Pick(0);
            else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
            else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
        }

        private void Pick(int index)
        {
            if (!IsOpen || index >= _count || Time.unscaledTime - _openedAt < _inputGuard) return;
            _panel.SetActive(false);
            Action<int> cb = _onPick;
            _onPick = null;
            cb?.Invoke(index);
        }
    }
}
