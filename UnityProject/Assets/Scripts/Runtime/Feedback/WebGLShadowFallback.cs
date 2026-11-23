using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// In the browser, spot and point lights that cast shadows come out fully shadowed (WebGL2, URP Forward), so a
    /// room lit by them goes dark. The browser build turns those shadows off; other builds keep them.
    /// </summary>
    public sealed class WebGLShadowFallback : MonoBehaviour
    {
        [SerializeField] private Light[] _lights = new Light[0];

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetShadows(0);
#endif
        }

        /// <summary>0 turns the shadows off, 1 restores them. Also reachable from the page with SendMessage for testing.</summary>
        public void SetShadows(int on)
        {
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i] != null) _lights[i].shadows = on != 0 ? LightShadows.Soft : LightShadows.None;
        }
    }
}
