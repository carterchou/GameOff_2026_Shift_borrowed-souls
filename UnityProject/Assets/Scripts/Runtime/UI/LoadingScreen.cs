using BorrowedSouls.Localization;
using System.Collections;
using BorrowedSouls.Debugging;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>
    /// What the player sees between the rest room and the arena: one tip and a progress bar on black. It starts once the screen has
    /// faded out, so loading can't hitch the walk, and it stays up long enough to read the tip however fast the machine is.
    /// The page is fully drawn before the load begins: a slow load can hold the main thread for many seconds, and a page still waiting
    /// to fade in would never be seen. The next scene fades itself in from black. Saved inactive; only Load shows it.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _tip;
        [SerializeField] private Image _bar;
        [Tooltip("Keeps moving while there is a frame to draw, so the page reads as working rather than stuck.")]
        [SerializeField] private RectTransform _soul;
        [SerializeField, TextArea] private string[] _tips = new string[0];
        [Tooltip("Real seconds the page stays up, so a tip can be read.")]
        [SerializeField, Min(0.1f)] private float _minSeconds = 2.2f;
        [SerializeField, Min(0.01f)] private float _fadeInSeconds = 0.25f;

        private static int _lastTip = -1;
        private static float _activatedAt;
        private static bool _arriving;

        private const float SoulBobHeight = 10f;
        private const float SoulBobPerSecond = 2.4f;

        private Vector2 _soulRest;
        private bool _soulRestKnown;

        public bool IsShowing => gameObject.activeSelf;
        public string Tip => _tip.text;
        public float Progress => _bar.fillAmount;
        public float Alpha => _group.alpha;
        public bool LoadStarted { get; private set; }

        /// <summary>True once, in the scene a Load has just brought up: its own copy of this page takes over from the old scene's.</summary>
        public static bool TakeArrival()
        {
            bool arriving = _arriving;
            _arriving = false;
            return arriving;
        }

        /// <summary>Shows the page as the last scene left it, same tip and a full bar, so the change of scene doesn't show.</summary>
        public void Hold()
        {
            if (_lastTip >= 0 && _lastTip < _tips.Length) _tip.text = Loc.T(_tips[_lastTip]);
            _group.alpha = 1f;
            _bar.fillAmount = 1f;
            gameObject.SetActive(true);
        }

        public IEnumerator Lift()
        {
            for (float t = 0f; t < _fadeInSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - t / _fadeInSeconds;
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_soulRestKnown)
            {
                _soulRest = _soul.anchoredPosition;
                _soulRestKnown = true;
            }
            _soul.anchoredPosition = _soulRest + Vector2.up * (Mathf.Sin(Time.unscaledTime * SoulBobPerSecond) * SoulBobHeight);
        }

        /// <summary>Run this from a component that stays enabled: this object is off until it starts.</summary>
        public IEnumerator Load(string scene)
        {
            _tip.text = NextTip();
            _group.alpha = 0f;
            _bar.fillAmount = 0f;
            gameObject.SetActive(true);
            for (float t = 0f; t < _fadeInSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = t / _fadeInSeconds;
                yield return null;
            }
            _group.alpha = 1f;
            yield return null; // drawn once at full strength before the load starts

            LoadStarted = true;
            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            load.allowSceneActivation = false;
            float longestFrame = 0f;
            float waited = 0f;
            float loadedAfter = -1f;
            for (; waited < _minSeconds || load.progress < 0.9f; waited += Time.unscaledDeltaTime)
            {
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
                if (loadedAfter < 0f && load.progress >= 0.9f) loadedAfter = waited;
                _bar.fillAmount = Mathf.Min(load.progress / 0.9f, waited / _minSeconds); // Unity stops at 0.9 until activation is allowed
                yield return null;
            }
            _bar.fillAmount = 1f;
            if (loadedAfter < 0f) loadedAfter = waited;
            Debug.Log($"[Loading] {scene}: ready after {loadedAfter:0.00}s, longest frame {longestFrame:0.00}s");
            _arriving = true;
            _activatedAt = Time.realtimeSinceStartup;
            SceneManager.sceneLoaded += LogActivation;
            load.allowSceneActivation = true;
        }

        /// <summary>Runs once the new scene's Awake and OnEnable calls are done, which is where a heavy scene holds the frame.</summary>
        private static void LogActivation(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= LogActivation;
            Debug.Log($"[Loading] {scene.name}: activation took {Time.realtimeSinceStartup - _activatedAt:0.00}s");
            LoadFrameProbe.Watch(scene.name);
        }

        /// <summary>A different tip each time, since a player restarting will see this page often.</summary>
        private string NextTip()
        {
            if (_tips.Length == 0) return string.Empty;
            int next = Random.Range(0, _tips.Length);
            if (next == _lastTip) next = (next + 1) % _tips.Length;
            _lastTip = next;
            return Loc.T(_tips[next]);
        }
    }
}
