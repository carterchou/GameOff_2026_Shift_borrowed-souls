using UnityEngine.InputSystem;

namespace BorrowedSouls.UI
{
    /// <summary>The "go back" press on menus. Read directly, since it means the same on every page and no gameplay action is live there.</summary>
    public static class MenuInput
    {
        public static bool Back => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }
}
