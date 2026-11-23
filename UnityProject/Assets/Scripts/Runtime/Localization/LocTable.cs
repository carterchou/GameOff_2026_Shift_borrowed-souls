using System.Collections.Generic;

namespace BorrowedSouls.Localization
{
    /// <summary>
    /// Chinese source text → English. Keys must match the Chinese exactly (scenes, ScriptableObjects and code).
    /// Key names in English lines use "Shift", "Left click", "Right click" and " E " so <c>InputHints</c> can swap
    /// them for the touch buttons.
    /// </summary>
    public static class LocTable
    {
        public static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            // ── Rest room menu ──
            { "你的力量，來自另一具身體。", "Your power comes from another body." },
            { "入場軀殼", "Starting body" },
            { "劍士", "Warrior" },
            { "弓手", "Archer" },
            { "法師", "Mage" },
            { "近身連斬，正面硬碰", "Close-range combos, head-on" },
            { "遠距離射箭，邊退邊打", "Shoots from afar, fights while backing off" },
            { "火焰範圍攻擊，一次燒一片", "Fire that burns a whole area" },
            { "開始", "Start" },
            { "天賦", "Talents" },
            { "操作說明", "Controls" },
            { "製作名單", "Credits" },
            { "離開", "Quit" },
            { "返回", "Back" },
            { "繼續", "Continue" },
            { "載入中", "Loading" },
            { "魂晶  {0}", "Soul Crystals  {0}" },
            { "魂祭場解放", "The Soul Arena Is Free" },
            { "靈魂消散", "Your Soul Faded" },
            { "通過波次  {0}\n時間  {1}:{2:00}\n擊殺  {3}", "Waves cleared  {0}\nTime  {1}:{2:00}\nKills  {3}" },
            { "\n\n獲得魂晶  {0}", "\n\nSoul Crystals earned  {0}" },

            // ── Talents ──
            { "已滿級", "MAX" },
            { "{0} 魂晶", "{0} crystals" },
            { "厚實軀殼", "Sturdy Vessel" },
            { "每級：所有身體 MaxHP +10%", "Per level: MaxHP +10% for every body" },
            { "喘息", "Breather" },
            { "每級：波間回復再 +10% MaxHP", "Per level: heal another +10% MaxHP between waves" },
            { "魂之餘裕", "Soul to Spare" },
            { "每級：遊魂時間 +1 秒", "Per level: soul time +1 s" },
            { "拾魂", "Soul Gatherer" },
            { "每級：魂燼吸取範圍 +25%", "Per level: ember pickup range +25%" },
            { "先手祝福", "Head Start" },
            { "第一波結束時多選一次升級", "One extra upgrade pick after wave 1" },

            // ── Controls page ──
            { "鍵盤滑鼠", "Keyboard & mouse" },
            { "觸控", "Touch" },
            { "移動", "Move" },
            { "瞄準（角色面向游標）", "Aim (you face the cursor)" },
            { "輕攻擊", "Light attack" },
            { "強攻擊", "Strong attack" },
            { "大招", "Special" },
            { "閃避", "Dodge" },
            { "鎖定", "Lock on" },
            { "暫停", "Pause" },
            { "提示", "Tips" },
            { "長按：離魂", "Hold: leave your body" },
            { "打倒敵人\n屍體只留 10 秒", "Defeat an enemy\nits corpse lasts 10 s" },
            { "長按 Shift\n靈魂離開身體", "Hold Shift\nto leave your body" },
            { "飛進屍體\n借用這具身體", "Fly into a corpse\nand borrow the body" },
            {
                "左半邊拖曳：移動（手指放哪裡，搖桿就出現在哪裡）\n自動鎖定附近的敵人；【目標】換一個\n【攻擊】輕攻擊（按住連續）\n【重擊】強攻擊\n【技能】大招\n【閃避】翻滾閃避，有無敵時間\n按住【離魂】：靈魂離開身體\n右上角齒輪：暫停",
                "Drag on the left half: move (the stick appears under your thumb)\nNearby enemies are locked on automatically; [Target] switches\n[Attack] light attack (hold for a combo)\n[Strong] strong attack\n[Skill] special\n[Dodge] roll, invincible while rolling\nHold [Soul]: your soul leaves the body\nGear at top right: pause"
            },

            // ── Touch buttons ──
            { "攻擊", "Attack" },
            { "重擊", "Strong" },
            { "技能", "Skill" },
            { "離魂", "Soul" },
            { "目標", "Target" },

            // ── Pause menu ──
            { "本局升級", "This run's upgrades" },
            { "重新開始", "Restart" },
            { "返回休息室", "Back to rest room" },
            { "鏡頭震動", "Screen shake" },
            { "減少閃光", "Reduce flashes" },
            { "教學提示", "Tutorial hints" },
            { "傷害數字", "Damage numbers" },
            { "音量", "Volume" },
            { "鏡頭震動：{0}", "Screen shake: {0}" },
            { "減少閃光：{0}", "Reduce flashes: {0}" },
            { "教學提示：{0}", "Tutorial hints: {0}" },
            { "傷害數字：{0}", "Damage numbers: {0}" },
            { "音量：{0}%", "Volume: {0}%" },
            { "開", "On" },
            { "關", "Off" },
            { "低", "Low" },
            { "標準", "Normal" },
            { "高", "High" },
            { "這局還沒選過升級。\n清完一波就能選。", "No upgrades yet this run.\nClear a wave to pick one." },

            // ── HUD ──
            { "特殊", "Special" },
            { "WAVE {0} / {1}   敵人 {2}", "WAVE {0} / {1}   Enemies {2}" },
            { "魂燼", "Soul Embers" },
            { "魂燼  本波已存入升級 ×{0}", "Soul Embers  upgrades banked this wave ×{0}" },
            { "離開後敵人將以此 HP 復活", "Leave it and the enemy returns with this HP" },
            { "放開／再按 Shift 侵入發光的身體", "Release / press Shift again to enter a glowing body" },
            { "離開活體：原本的敵魂奪回了身體", "You left a living host: its own soul took the body back" },
            { "勝利", "Victory" },
            { "{0} 秒後返回休息區", "Returning to the rest room in {0}s" },
            { "監魂者倒下了，它召來的軀殼也隨之倒地", "The Soul Warden has fallen, and the bodies it summoned fall with it" },
            { "魂環衝擊\n閃避穿過衝擊波", "Soul Ring\nDodge through the shockwave" },
            { "靈魂強化", "Soul Upgrade" },
            { "靈魂強化（之後還有 {0} 次）", "Soul Upgrade ({0} more to come)" },

            // ── Bodies and weapons ──
            { "長劍戰士", "Warrior" },
            { "長弓手", "Archer" },
            { "火法師", "Fire Mage" },
            { "監魂者", "Soul Warden" },
            { "長劍", "Longsword" },
            { "長弓", "Longbow" },
            { "火法杖", "Fire Staff" },
            { "魂刃", "Soul Blade" },

            // ── Waves ──
            { "魂祭場的第一戰", "First Battle of the Soul Arena" },
            { "長劍剋法師：先擊倒火法師，然後長按 Shift 離魂，侵入它倒下的身體。", "Longsword beats Mage: take down the Fire Mage first, then hold Shift and enter its fallen body." },
            { "距離與遮蔽", "Range and Cover" },
            { "火焰剋長弓：用火球對付弓手；石柱擋得住箭。長按 Shift 也能回到你留下的身體。", "Fire beats Longbow: burn the archers with fireballs; stone pillars block arrows. Hold Shift to go back to a body you left behind." },
            { "選擇你的身體", "Choose Your Body" },
            { "精英", "Elites" },
            { "金色的是精英：更耐打，可侵入門檻減半，倒下後的遺體仍可借用。", "Gold enemies are elites: tougher, possessable at half the usual HP threshold, and their corpses can still be borrowed." },
            { "混戰", "Free-for-All" },
            { "火力壓制", "Suppressing Fire" },
            { "最後的準備", "Final Preparations" },
            { "監魂者無法被侵入：閃避重斬與魂環，借用它召喚的身體戰鬥。", "The Soul Warden can't be possessed: dodge its heavy slash and soul ring, and fight in the bodies it summons." },

            // ── Upgrades ──
            { "靈魂侵蝕", "Soul Erosion" },
            { "可侵入 HP ≤ 25% 的活敵\n（精英 12.5%）", "Possess living enemies at HP ≤ 25%\n(elites 12.5%)" },
            { "活敵門檻 35%（精英 17.5%）", "Living-enemy threshold 35% (elites 17.5%)" },
            { "活敵門檻 45%（精英 22.5%）", "Living-enemy threshold 45% (elites 22.5%)" },
            { "活敵門檻 55%（精英 27.5%）", "Living-enemy threshold 55% (elites 27.5%)" },
            { "靈魂修復", "Soul Mending" },
            { "首次借魂恢復至 75% MaxHP\n（返回舊身體不回血）", "First borrow heals to 75% MaxHP\n(going back to an old body doesn't heal)" },
            { "首次借魂恢復至 85%", "First borrow heals to 85%" },
            { "首次借魂恢復至 95%", "First borrow heals to 95%" },
            { "靈魂延續", "Soul Endurance" },
            { "遊魂時間 5 → 6 秒", "Soul time 5 → 6 s" },
            { "遊魂時間 6 → 7 秒", "Soul time 6 → 7 s" },
            { "靈魂感應", "Soul Sense" },
            { "侵入距離 2.5 → 3.0 m", "Possession range 2.5 → 3.0 m" },
            { "侵入距離 3.0 → 3.5 m", "Possession range 3.0 → 3.5 m" },
            { "侵入距離 3.5 → 4.0 m", "Possession range 3.5 → 4.0 m" },
            { "肉體強化", "Sturdy Flesh" },
            { "寄宿容器 MaxHP +10%\n（依比例，不額外回血）", "Host body MaxHP +10%\n(scales current HP, no extra heal)" },
            { "力量增幅", "Power Surge" },
            { "所有職業攻擊傷害 +10%", "Attack damage +10% for every class" },
            { "攻擊傷害 +20%", "Attack damage +20%" },
            { "攻擊傷害 +30%", "Attack damage +30%" },
            { "迅捷", "Swiftness" },
            { "寄宿容器移速 +8%", "Host body move speed +8%" },
            { "移速 +16%", "Move speed +16%" },
            { "魂爆", "Soul Burst" },
            { "成功侵入時，對周圍 3.5 m 的敵人造成魂能傷害", "Possessing a body deals soul damage to enemies within 3.5 m" },

            // ── Tutorial ──
            { "WASD 移動 · 滑鼠瞄準 · 左鍵／右鍵／E 攻擊 · Space 閃避（有無敵時間）", "WASD move · Mouse aim · Left click / Right click / E attack · Space dodge (invincible)" },
            { "HP 偏低：長按 Shift 離魂，借附近發光的身體就能恢復", "Low HP: hold Shift to leave your body; borrow a glowing body nearby to heal" },
            { "敵人倒下的身體可以借用 10 秒：長按 Shift 離魂", "A fallen enemy's body can be borrowed for 10 seconds: hold Shift to leave yours" },
            { "靈魂可穿牆、無法被攻擊，但只能存在幾秒：飛到發光的身體，放開 Shift 侵入", "Your soul passes through walls and can't be hit, but lasts only seconds: fly to a glowing body and release Shift to enter it" },
            { "你壓制了活著的敵魂：離開這具身體時，敵人會以當下的 HP 奪回控制", "You suppressed a living enemy soul: when you leave this body, the enemy takes it back with its current HP" },
            { "回到留下的身體：HP 與狀態都保留，但不會再回血", "Back in a body you left: its HP and status stay as they were, but it won't heal again" },
            { "火法師：左鍵火球（燃燒）· 右鍵烈焰槍（貫穿）· E 焚風｜火焰剋長弓", "Fire Mage: Left click Fireball (burns) · Right click Flame Lance (pierces) · E Scorching Wind | Fire beats Longbow" },
            { "長弓手：按住左鍵連射 · 右鍵貫穿箭 · E 三連射｜距離剋長劍", "Archer: hold Left click to rapid-fire · Right click Piercing Arrow · E Triple Shot | Range beats Longsword" },
            { "長劍戰士：左鍵三段斬 · 右鍵重斬（打斷施法）· E 突進斬｜長劍剋法師", "Warrior: Left click 3-hit combo · Right click Heavy Slash (interrupts casting) · E Dash Slash | Longsword beats Mage" },

            // ── Loading tips ──
            { "只有被你打倒的身體，才會留下可以借用的屍體。", "Only bodies you defeat leave a corpse you can borrow." },
            { "屍體只留 10 秒，趁它消散前飛進去。", "A corpse lasts only 10 seconds. Fly in before it fades." },
            { "每具身體只有第一次借用時會回血，回到舊身體不會。", "A body heals only the first time you borrow it. Going back to an old one won't." },
            { "遊魂的時間有限。倒數結束前，一定要找到下一具身體。", "Your soul can't last long on its own. Find the next body before the countdown ends." },
            { "撿魂燼會回一點血，清完一波也會回血。", "Soul embers heal a little, and clearing a wave heals too." },
            { "每一局都會拿到魂晶，回休息室可以買天賦，下一局就生效。", "Every run earns Soul Crystals. Spend them on talents in the rest room; they apply next run." },
            { "血量夠低的活敵也能直接侵入，升級可以放寬門檻。", "A living enemy with low enough HP can be possessed directly. Upgrades raise the threshold." },
            { "精英會走去踩碎地上的屍體，想借要快。", "Elites stomp on corpses lying around. Borrow them fast." },
            { "閃避途中不會受傷，看準敵人出手再按。", "You can't be hurt mid-dodge. Wait for the enemy's swing, then roll." },
            { "劍士近戰、弓手遠射、法師範圍。換一具身體，就換一種打法。", "Warrior up close, Archer at range, Mage for crowds. A new body is a new way to fight." },
        };
    }
}
