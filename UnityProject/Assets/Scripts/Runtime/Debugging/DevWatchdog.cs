using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BorrowedSouls.Debugging
{
    /// <summary>
    /// Development-build hang finder for headless bot runs. Gameplay code drops breadcrumbs with
    /// <see cref="Mark"/> (stripped from release and editor builds by <c>[Conditional]</c>); this component counts
    /// finished frames in LateUpdate, and a background thread logs the last breadcrumb when the main thread stops
    /// finishing frames for <see cref="StallSeconds"/>. "frame end" as the last mark means the stall is outside
    /// scripts (physics / engine). Started by <see cref="AutoPilot"/> for <c>-autopilot</c> launches only.
    /// </summary>
    public sealed class DevWatchdog : MonoBehaviour
    {
        private const float StallSeconds = 3f;

        private static volatile string _mark = "start";
        private static volatile int _markId;
        private static volatile int _frames;
        private static Thread _thread;
        private static string _reportPath;

        [Conditional("DEVELOPMENT_BUILD")]
        public static void Mark(string where, int id = 0)
        {
            _mark = where;
            _markId = id;
        }

        public static void Begin(GameObject host)
        {
            if (_thread != null) return;
            host.AddComponent<DevWatchdog>();
            // Reports go to their own file: if the main thread hangs inside Debug.Log, Unity's log lock is held.
            _reportPath = Application.consoleLogPath + ".watchdog.txt";
            Write($"armed (stall threshold {StallSeconds}s)");
            Debug.Log($"[WATCHDOG] armed -> {_reportPath}");
            _thread = new Thread(Watch) { IsBackground = true, Name = "DevWatchdog" };
            _thread.Start();
        }

        private void LateUpdate()
        {
            Mark("frame end");
            _frames++;
        }

        private static void Write(string line)
        {
            try { System.IO.File.AppendAllText(_reportPath, System.DateTime.Now.ToString("HH:mm:ss ") + line + System.Environment.NewLine); }
            catch (System.Exception) { /* diagnostics must never take the game down */ }
        }

        private static void Watch()
        {
            int lastFrames = -1;
            var still = Stopwatch.StartNew();
            double nextReport = StallSeconds;
            while (true)
            {
                Thread.Sleep(250);
                int f = _frames;
                if (f != lastFrames)
                {
                    lastFrames = f;
                    still.Restart();
                    nextReport = StallSeconds;
                    continue;
                }
                if (still.Elapsed.TotalSeconds < nextReport) continue;
                nextReport += 10.0; // repeat: a changing mark during one stall means a loop spanning several steps
                Write($"main thread stalled {still.Elapsed.TotalSeconds:0.0}s after frame {f}; last mark '{_mark}' id={_markId}");
            }
        }
    }
}
