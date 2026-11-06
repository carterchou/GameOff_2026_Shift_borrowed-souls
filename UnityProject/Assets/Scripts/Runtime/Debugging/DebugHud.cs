using System.Text;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Core.Soul;
using BorrowedSouls.Soul;
using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Debugging
{
    /// <summary>Developer overlay. Toggle with F1.</summary>
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private GameRoot _root;
        [SerializeField] private PlayerSoulController _soul;
        [SerializeField] private EmberSystem _embers;
        [SerializeField] private bool _visible = true;

        private readonly StringBuilder _sb = new StringBuilder(1024);
        private GUIStyle _style;
        private float _fps;

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame) _visible = !_visible;
#else
            _visible = false; // developer overlay is not part of release builds
#endif
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.1f);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = Color.white;
            }

            _sb.Clear();
            _sb.AppendFormat("<b>Borrowed Souls - grey-box</b>  FPS {0:0}  timeScale {1:0.00}\n", _fps, Time.timeScale);
            if (_soul != null && _soul.Soul != null)
            {
                PlayerSoul s = _soul.Soul;
                string mode = s.Mode == SoulMode.Free ? "<color=cyan>SOUL FREE</color>" : (s.Mode == SoulMode.Expired ? "<color=red>EXPIRED</color>" : "in vessel");
                _sb.AppendFormat("Soul {0}  time {1:0.00}s  target {2}  possessions {3}  invasion Lv{4}\n",
                    mode, s.FreeTimeLeft, _soul.Target != null ? _soul.Target.name : "-", s.Possessions, s.InvasionLevel);
            }
            if (_embers != null && _embers.Wallet != null)
            {
                var w = _embers.Wallet;
                _sb.AppendFormat("Embers {0}/{1}  banked upgrades {2}  total {3}  on field {4}\n", w.Progress, w.NextCost, w.BankedUpgrades, w.TotalCollected, _embers.LiveCount);
            }

            VesselRuntime p = _root.PlayerVessel;
            if (p != null)
            {
                VesselCombat c = p.Combat;
                _sb.AppendFormat("Player in {0} ({1}) [{2}]  HP {3:0}/{4:0}\n", p.name, p.Definition.DisplayName, p.Class, p.Hp, p.MaxHp.Value);
                _sb.AppendFormat("Attack {0} {1}  next combo {2}  special CD {3:0.0}\n",
                    c.CurrentAttack != null ? c.CurrentAttack.AttackId : "-", c.Phase, c.NextComboIndex + 1, Mathf.Max(0f, c.SpecialCooldownLeft));
                _sb.AppendFormat("Dodge {0}  i-frame {1}  CD {2:0.00}  stagger {3}\n",
                    c.IsDodging ? "ON" : "-", p.IsInvulnerable ? "<color=cyan>YES</color>" : "no", Mathf.Max(0f, c.DodgeCooldownLeft), p.IsStaggered);
            }

            var vessels = _root.Vessels;
            for (int i = 0; i < vessels.Count; i++)
            {
                VesselRuntime v = vessels[i];
                if (v == p) continue;
                VesselState st = v.State;
                string status = st.IsBroken ? "BROKEN " : st.IsFallenCorpse ? $"CORPSE {st.CorpseTimeLeft:0.0}s " : st.IsVacated ? "EMPTY " : st.EnemySoulSuppressed ? "SUPPRESSED " : "";
                _sb.AppendFormat("  {0} [{1}/{2}] HP {3:0}/{4:0} {5}{6}\n", v.name, v.Class, v.Team, v.Hp, v.MaxHp.Value,
                    status, v.Combat.IsAttacking ? v.Combat.Phase.ToString() : "");
            }
            _sb.Append("\nWASD move | Mouse aim | LMB weak | RMB strong | E special | Space dodge | hold Shift: soul | F1 HUD");

            GUI.Label(new Rect(12f, 12f, 1000f, 520f), _sb.ToString(), _style);
        }
    }
}
