using System.Collections.Generic;
using UnityEngine;

namespace BorrowedSouls.Localization
{
    public enum GameLanguage
    {
        English = 0,
        Chinese = 1
    }

    /// <summary>
    /// The game is written in Chinese; every player-facing string goes through <see cref="T"/>, which swaps in the
    /// English line from <see cref="LocTable"/> when English is chosen. English is the default.
    /// </summary>
    public static class Loc
    {
        private const string PrefKey = "BS.Language";

        private static bool _loaded;
        private static GameLanguage _current;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static readonly HashSet<string> Missing = new HashSet<string>();
#endif

        public static GameLanguage Current
        {
            get
            {
                if (!_loaded)
                {
                    _current = (GameLanguage)PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.English);
                    _loaded = true;
                }
                return _current;
            }
        }

        public static bool English => Current == GameLanguage.English;

        public static void Set(GameLanguage language)
        {
            _current = language;
            _loaded = true;
            PlayerPrefs.SetInt(PrefKey, (int)language);
            PlayerPrefs.Save();
        }

        /// <summary>The line in the current language. <paramref name="zh"/> is the Chinese source text.</summary>
        public static string T(string zh)
        {
            if (string.IsNullOrEmpty(zh) || !English) return zh;
            if (LocTable.English.TryGetValue(zh, out string en)) return en;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (HasChinese(zh) && Missing.Add(zh)) Debug.LogWarning($"[LOC] no English for: {zh}");
#endif
            return zh;
        }

        /// <summary><see cref="T"/> on a format string, then <c>string.Format</c>.</summary>
        public static string F(string zhFormat, object a) => string.Format(T(zhFormat), a);

        public static string F(string zhFormat, object a, object b) => string.Format(T(zhFormat), a, b);

        public static string F(string zhFormat, object a, object b, object c) => string.Format(T(zhFormat), a, b, c);

        public static string F(string zhFormat, params object[] args) => string.Format(T(zhFormat), args);

        public static bool HasChinese(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '㐀' && c <= '鿿') return true;
            }
            return false;
        }
    }
}
