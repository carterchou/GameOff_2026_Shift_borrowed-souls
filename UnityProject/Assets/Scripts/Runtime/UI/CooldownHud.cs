using BorrowedSouls.Bootstrap;
using BorrowedSouls.Data;
using BorrowedSouls.InputHandling;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Dodge and special cooldowns of the current body. Cooldowns live on the body, so
    /// possessing another one shows that body's. Weak and strong attacks have none. Hidden while the soul flies.
    /// </summary>
    public sealed class CooldownHud : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private GameObject _panel;
        [SerializeField] private CooldownSlot _dodge;
        [SerializeField] private CooldownSlot _special;
        [Header("Touch: the cooldowns show on the buttons themselves, and the key slots hide")]
        [SerializeField] private TouchButton _touchDodge;
        [SerializeField] private TouchButton _touchSpecial;

        private void LateUpdate()
        {
            VesselRuntime body = _root.PlayerVessel;
            bool touch = TouchControls.Active && _touchDodge != null && _touchSpecial != null;
            bool show = body != null && !touch;
            if (_panel.activeSelf != show) _panel.SetActive(show);
            if (touch)
            {
                ShowOnButtons(body);
                return;
            }
            if (!show) return;

            _dodge.Show(body.Combat.DodgeCooldownLeft, body.Definition.DodgeCooldown);
            AttackDefinition special = body.Definition.Weapon.Special;
            _special.Show(body.Combat.SpecialCooldownLeft, special != null ? special.Cooldown : 0f);
        }

        private void ShowOnButtons(VesselRuntime body)
        {
            if (body == null)
            {
                _touchDodge.SetCooldown(0f);
                _touchSpecial.SetCooldown(0f);
                return;
            }
            _touchDodge.SetCooldown(Share(body.Combat.DodgeCooldownLeft, body.Definition.DodgeCooldown));
            AttackDefinition special = body.Definition.Weapon.Special;
            _touchSpecial.SetCooldown(special != null ? Share(body.Combat.SpecialCooldownLeft, special.Cooldown) : 0f);
        }

        private static float Share(float left, float total) => total > 0f ? left / total : 0f;
    }
}
