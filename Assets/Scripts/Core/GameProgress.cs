using System.Collections.Generic;
using UnityEngine;

namespace DreamLayerJam
{
    /// <summary>
    /// Which fights are cleared and which lessons the first-time coach has taught, saved with PlayerPrefs (IndexedDB in
    /// WebGL), plus session-only flow state for the lobby.
    /// </summary>
    public static class GameProgress
    {
        const string Key = "dlj.cleared";
        const string LessonsKey = "dlj.lessons";
        static HashSet<string> cleared;
        static HashSet<string> lessons;

        /// <summary>The title screen shows once per session; coming back from a fight skips it.</summary>
        public static bool TitleShown { get; set; }

        /// <summary>The entrance the player last used, so the lobby puts them back next to it.</summary>
        public static string LastEntranceId { get; set; }

        static HashSet<string> Cleared
        {
            get
            {
                if (cleared == null) Load();
                return cleared;
            }
        }

        public static bool AnyCleared => Cleared.Count > 0;

        public static bool IsCleared(string fightId) => !string.IsNullOrEmpty(fightId) && Cleared.Contains(fightId);

        public static void MarkCleared(string fightId)
        {
            if (string.IsNullOrEmpty(fightId) || !Cleared.Add(fightId)) return;
            Save();
        }

        /// <summary>The player has shown they know this mechanic (FirstTimeCoach): it isn't taught again.</summary>
        public static bool IsLearned(string lesson) => Lessons.Contains(lesson);

        public static bool AnyLearned => Lessons.Count > 0;

        public static void MarkLearned(string lesson)
        {
            if (string.IsNullOrEmpty(lesson) || !Lessons.Add(lesson)) return;
            Save();
        }

        /// <summary>Clears the fights and the lessons: the next run is a first run again.</summary>
        public static void ResetAll()
        {
            Cleared.Clear();
            Lessons.Clear();
            Save();
        }

        static HashSet<string> Lessons
        {
            get
            {
                if (lessons == null) Load();
                return lessons;
            }
        }

        static void Load()
        {
            cleared = Split(PlayerPrefs.GetString(Key, ""));
            lessons = Split(PlayerPrefs.GetString(LessonsKey, ""));
        }

        static HashSet<string> Split(string list)
        {
            var set = new HashSet<string>();
            foreach (var id in list.Split(','))
                if (!string.IsNullOrWhiteSpace(id)) set.Add(id.Trim());
            return set;
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, string.Join(",", Cleared));
            PlayerPrefs.SetString(LessonsKey, string.Join(",", Lessons));
            PlayerPrefs.Save();
        }

        // Statics survive between Play sessions when domain reload is off; start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSessionState()
        {
            cleared = null;
            lessons = null;
            TitleShown = false;
            LastEntranceId = null;
        }
    }
}
