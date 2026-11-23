using BorrowedSouls.InputHandling;
using BorrowedSouls.Localization;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// Hint text in the current language. Hints name keys; on a touch device they name the on-screen buttons instead.
    /// Callers pass the Chinese source line.
    /// </summary>
    public static class InputHints
    {
        private static readonly string[] TouchZh =
        {
            // whole lines first, then the key names inside other lines
            "WASD 移動 · 滑鼠瞄準 · 左鍵／右鍵／E 攻擊 · Space 閃避（有無敵時間）",
            "左邊拖曳移動 · 自動瞄準附近的敵人 · 【攻擊】【重擊】【技能】 · 【閃避】有無敵時間 · 【目標】換鎖定對象",
            "Shift 離魂", "【離魂】",
            "Shift", "【離魂】",
            "按住左鍵", "按住【攻擊】",
            "左鍵", "【攻擊】",
            "右鍵", "【重擊】",
            " E ", " 【技能】",
        };

        private static readonly string[] TouchEn =
        {
            "WASD move · Mouse aim · Left click / Right click / E attack · Space dodge (invincible)",
            "Drag on the left to move · Enemies are auto-targeted · [Target] switches",
            "Shift", "[Soul]",
            "Left click", "[Attack]",
            "Right click", "[Strong]",
            " E ", " [Skill] ",
        };

        public static string For(string zh)
        {
            string text = Loc.T(zh);
            if (string.IsNullOrEmpty(text) || !TouchControls.Active) return text;
            string[] swaps = Loc.English ? TouchEn : TouchZh;
            for (int i = 0; i < swaps.Length; i += 2) text = text.Replace(swaps[i], swaps[i + 1]);
            return text;
        }
    }
}
