#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.IO;
using BorrowedSouls.Bootstrap;
using BorrowedSouls.Combat;
using BorrowedSouls.Run;
using BorrowedSouls.Soul;
using UnityEngine;

namespace BorrowedSouls.Debugging
{
    /// <summary>
    /// Development builds only: "-capture DIR" writes every frame as a JPEG at a fixed 30 fps of game time, for the
    /// trailer, and logs the frame of each wave, death, soul exit and possession so the cut can find them.
    /// </summary>
    public sealed class FrameCapture : MonoBehaviour
    {
        private const int Fps = 30;
        private GameRoot _root;
        private string _dir;
        private int _frame;

        public static void StartIfRequested(GameObject host, GameRoot root)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-capture");
            if (i < 0 || i + 1 >= args.Length || host.GetComponent<FrameCapture>() != null) return;
            var capture = host.AddComponent<FrameCapture>();
            capture._root = root;
            capture._dir = args[i + 1];
            Directory.CreateDirectory(capture._dir);
            Time.captureFramerate = Fps;
            Application.runInBackground = true; // keep recording when the window loses focus
            capture.StartCoroutine(capture.Capture());
            Debug.Log("[CAP] start " + capture._dir);
        }

        private void OnEnable()
        {
            if (_root == null) return;
            Subscribe(true);
        }

        private void Start() => Subscribe(true);

        private void OnDisable() => Subscribe(false);

        private bool _subscribed;

        private void Subscribe(bool on)
        {
            if (_root == null || _subscribed == on) return;
            _subscribed = on;
            if (on)
            {
                _root.Events.Subscribe<WaveStarted>(OnWave);
                _root.Events.Subscribe<VesselDied>(OnDied);
                _root.Events.Subscribe<SoulExited>(OnExit);
                _root.Events.Subscribe<PossessionSucceeded>(OnPossess);
            }
            else
            {
                _root.Events.Unsubscribe<WaveStarted>(OnWave);
                _root.Events.Unsubscribe<VesselDied>(OnDied);
                _root.Events.Unsubscribe<SoulExited>(OnExit);
                _root.Events.Unsubscribe<PossessionSucceeded>(OnPossess);
            }
        }

        private void OnWave(WaveStarted e) => Debug.Log($"[CAP] f={_frame} wave {e.WaveNumber}");
        private void OnDied(VesselDied e) => Debug.Log($"[CAP] f={_frame} died {(e.Vessel != null ? e.Vessel.name : "?")}");
        private void OnExit(SoulExited e) => Debug.Log($"[CAP] f={_frame} soul exit");
        private void OnPossess(PossessionSucceeded e) => Debug.Log($"[CAP] f={_frame} possess");

        private IEnumerator Capture()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(_dir, $"f{_frame:D6}.jpg"), shot.EncodeToJPG(92));
                Destroy(shot);
                _frame++;
            }
        }
    }
}
#endif
