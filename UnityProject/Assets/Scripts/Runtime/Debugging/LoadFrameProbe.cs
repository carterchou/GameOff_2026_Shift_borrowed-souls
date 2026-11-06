using UnityEngine;

namespace BorrowedSouls.Debugging
{
    /// <summary>
    /// Logs the longest frames for a few seconds after a scene arrives, so a freeze on the way into the arena shows up as a number in the log
    /// rather than as a feeling. Lives through the scene change by itself and removes itself when done.
    /// </summary>
    public sealed class LoadFrameProbe : MonoBehaviour
    {
        private const float WatchSeconds = 5f;

        private string _scene;
        private float _watched;
        private float _longest;
        private float _longestAt;
        private float _secondLongest;

        public static void Watch(string scene)
        {
            var go = new GameObject("LoadFrameProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<LoadFrameProbe>()._scene = scene;
        }

        private void Update()
        {
            float frame = Time.unscaledDeltaTime;
            if (frame > _longest)
            {
                _secondLongest = _longest;
                _longest = frame;
                _longestAt = _watched;
            }
            else if (frame > _secondLongest)
            {
                _secondLongest = frame;
            }
            _watched += frame;
            if (_watched < WatchSeconds) return;
            Debug.Log($"[Loading] first {WatchSeconds:0}s in {_scene}: longest frame {_longest:0.000}s at {_longestAt:0.00}s, next {_secondLongest:0.000}s");
            Destroy(gameObject);
        }
    }
}
