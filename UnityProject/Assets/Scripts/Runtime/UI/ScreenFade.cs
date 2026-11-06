using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BorrowedSouls.UI
{
    /// <summary>A full-screen black layer for scene changes. It runs on real time, because a run can end with the world paused.</summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField, Min(0f)] private float _fadeInSeconds = 0.7f;

        private void Awake()
        {
            Set(1f);
            StartCoroutine(FadeTo(0f, _fadeInSeconds));
        }

        public IEnumerator FadeTo(float alpha, float seconds)
        {
            float from = _image.color.a;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Set(Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            Set(alpha);
        }

        private void Set(float alpha)
        {
            Color c = _image.color;
            c.a = alpha;
            _image.color = c;
            _image.raycastTarget = alpha > 0.01f;
        }
    }
}
