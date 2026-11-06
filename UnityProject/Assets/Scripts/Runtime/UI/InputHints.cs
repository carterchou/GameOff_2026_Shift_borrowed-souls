using BorrowedSouls.InputHandling;

namespace BorrowedSouls.UI
{
    /// <summary>Hint text names keys; on a touch device it names the on-screen buttons instead.</summary>
    public static class InputHints
    {
        private static readonly string[] Touch =
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

        public static string For(string text)
        {
            if (string.IsNullOrEmpty(text) || !TouchControls.Active) return text;
            for (int i = 0; i < Touch.Length; i += 2) text = text.Replace(Touch[i], Touch[i + 1]);
            return text;
        }
    }
}
