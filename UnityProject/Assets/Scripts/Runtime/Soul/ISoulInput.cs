using UnityEngine;

namespace BorrowedSouls.Soul
{
    /// <summary>
    /// Semantic input the soul controller needs while flying free and to decide when to leave a body.
    /// Implemented by the player's input driver and by the autopilot bot, so automated runs exercise the
    /// exact same possession code as a human.
    /// </summary>
    public interface ISoulInput
    {
        Vector2 ReadMoveWorld();
        Vector3 ReadAimWorld(Vector3 origin);
        bool SoulShiftHeld { get; }
        bool SoulShiftPressed { get; }
    }
}
