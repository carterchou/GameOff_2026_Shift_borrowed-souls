using System;
using System.Collections.Generic;
using System.IO;
using BorrowedSouls.Core.Meta;
using BorrowedSouls.Data;
using UnityEngine;

namespace BorrowedSouls.Meta
{
    /// <summary>
    /// Keeps the profile in one JSON file in the player's data folder. Unreadable saves are set aside rather than deleted, and the game
    /// goes on with a fresh profile. Batch runs (tests, bots) neither read nor write it, so they can't depend on or disturb a real save.
    /// </summary>
    public static class ProfileStore
    {
        private const string FileName = "profile.json";

        [Serializable]
        private sealed class Saved
        {
            public int Crystals;
            public string Body;
            public List<SavedTalent> Talents = new List<SavedTalent>();
        }

        [Serializable]
        private struct SavedTalent
        {
            public string Id;
            public int Level;
        }

        private static ProfileState _current;

        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        private static bool UsesDisk => !Application.isBatchMode;

        public static ProfileState Current(TalentCatalog catalog)
        {
            if (_current == null) _current = UsesDisk ? Load(catalog) : new ProfileState();
            return _current;
        }

        /// <summary>Starts over with this profile in memory. For tests.</summary>
        public static void Use(ProfileState profile) => _current = profile;

        public static void Save(TalentCatalog catalog)
        {
            if (!UsesDisk || _current == null) return;
            var saved = new Saved { Crystals = _current.Crystals, Body = _current.Body.ToString() };
            foreach (TalentRule rule in catalog.Rules) saved.Talents.Add(new SavedTalent { Id = rule.Id.ToString(), Level = _current.Level(rule.Id) });
            try
            {
                string temporary = SavePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(saved, true));
                File.Copy(temporary, SavePath, true); // written whole first, so a crash mid-save can't damage the real one
                File.Delete(temporary);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Profile] could not save: " + e.Message);
            }
        }

        private static ProfileState Load(TalentCatalog catalog)
        {
            var profile = new ProfileState();
            if (!File.Exists(SavePath)) return profile;
            try
            {
                Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(SavePath));
                if (saved == null) throw new ArgumentException("empty save");
                profile.AddCrystals(saved.Crystals);
                if (Enum.TryParse(saved.Body, out StartingBody body) && Enum.IsDefined(typeof(StartingBody), body)) profile.ChooseBody(body);
                foreach (SavedTalent talent in saved.Talents)
                {
                    if (Enum.TryParse(talent.Id, out TalentId id)) profile.Restore(profile.Crystals, id, talent.Level, catalog.Rules);
                }
                return profile;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Profile] save unreadable, starting fresh: " + e.Message);
                try { File.Copy(SavePath, SavePath + ".bad", true); }
                catch (IOException) { }
                return new ProfileState();
            }
        }
    }

    /// <summary>What a finished run hands to the rest room across the scene change.</summary>
    public static class RunTransfer
    {
        public struct Summary
        {
            public bool Victory;
            public int WavesCleared;
            public float Seconds;
            public int Kills;
            public int Crystals;
        }

        private static Summary? _pending;

        public static void Hand(in Summary summary) => _pending = summary;

        /// <summary>The waiting summary, once; null when the player just opened the game.</summary>
        public static Summary? Take()
        {
            Summary? summary = _pending;
            _pending = null;
            return summary;
        }
    }

    /// <summary>The scene names both ends of the trip agree on.</summary>
    public static class GameScenes
    {
        public const string RestRoom = "RestRoom";
        public const string Arena = "Arena_Run";
    }
}
