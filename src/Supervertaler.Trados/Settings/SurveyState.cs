using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Supervertaler.Trados.Settings
{
    /// <summary>
    /// What the user has already been asked, and whether they want to be asked at
    /// all — the persistent side of the one-question dev survey (issue #43).
    ///
    /// Deliberately its OWN file rather than fields on <see cref="TermLensSettings"/>,
    /// for exactly the reason <see cref="AnnouncementState"/> is: settings.json is
    /// loaded into long-lived objects and written back wholesale from ~29 call
    /// sites, and AiAssistantViewPart caches a copy at startup — before the survey
    /// task runs — then saves it again on bank switches, prompt changes and chat
    /// turns. Each such save restored the pre-survey copy and dropped the record,
    /// so the question came back on the next start no matter what the user
    /// clicked. Two narrower fixes were tried first (re-load before applying, in
    /// 20.147; union the lists on every save, in 20.169) and users still reported
    /// the dialog returning, which is what moved this out of the contended file
    /// altogether. See issue #55.
    ///
    /// The legacy fields in settings.json are still honoured on read, so an answer
    /// that did survive under the old scheme is not undone.
    /// </summary>
    internal static class SurveyState
    {
        /// <summary>How often one question may be shown before it gives up.</summary>
        public const int MaxImpressions = 3;

        private static readonly object Sync = new object();

        private static string FilePath =>
            Path.Combine(UserDataPath.TradosSettingsDir, "surveys.json");

        /// <summary>
        /// True when the user has ticked "Don't ask again". That checkbox carries
        /// no qualifier, so it means what it says: no more surveys, not merely no
        /// more of this one question. Per-question suppression used to be its only
        /// effect, and the next question published simply asked again.
        /// </summary>
        public static bool IsOptedOut()
        {
            try { return Read().OptedOut; }
            catch { return false; }
        }

        /// <summary>Records a permanent opt-out from all surveys.</summary>
        public static void OptOut()
        {
            Mutate(s => s.OptedOut = true);
        }

        /// <summary>
        /// True when this question has been answered, dismissed, or shown its
        /// full allowance. Also honours the legacy list in settings.json.
        /// </summary>
        public static bool HasBeenAnswered(int surveyId)
        {
            try
            {
                if (Read().Answered.Contains(surveyId)) return true;
            }
            catch { }

            try
            {
                var legacy = TermLensSettings.Load()?.AnsweredSurveyIds;
                if (legacy != null && legacy.Contains(surveyId)) return true;
            }
            catch { }

            return false;
        }

        /// <summary>Records a question as finished with. Best-effort.</summary>
        public static void MarkAnswered(int surveyId)
        {
            Mutate(s =>
            {
                if (!s.Answered.Contains(surveyId)) s.Answered.Add(surveyId);
            });
        }

        /// <summary>
        /// How many times this question has been put in front of the user without
        /// an answer. Takes the higher of this file and the legacy settings.json
        /// counter: the larger number is the truthful one, and under-counting
        /// would show the dialog past its cap.
        /// </summary>
        public static int ShownCount(int surveyId)
        {
            int count = 0;
            try
            {
                int mine;
                if (Read().Shown.TryGetValue(Key(surveyId), out mine)) count = mine;
            }
            catch { }

            try
            {
                var legacy = TermLensSettings.Load()?.SurveyShownCounts;
                int old;
                if (legacy != null && legacy.TryGetValue(Key(surveyId), out old) && old > count)
                    count = old;
            }
            catch { }

            return count;
        }

        /// <summary>
        /// Records that the dialog was put on screen. Called before showing it, so
        /// a close without answering still counts toward the cap.
        /// </summary>
        public static void RecordShown(int surveyId)
        {
            Mutate(s =>
            {
                int current;
                s.Shown.TryGetValue(Key(surveyId), out current);
                s.Shown[Key(surveyId)] = current + 1;
            });
        }

        // ─── file access ────────────────────────────────────────────

        /// <summary>DataContractJsonSerializer dictionary keys must be strings.</summary>
        private static string Key(int surveyId) =>
            surveyId.ToString(CultureInfo.InvariantCulture);

        [DataContract]
        private sealed class State
        {
            [DataMember(Name = "optedOut")]
            public bool OptedOut { get; set; }

            [DataMember(Name = "answered")]
            public List<int> Answered { get; set; } = new List<int>();

            [DataMember(Name = "shown")]
            public Dictionary<string, int> Shown { get; set; } = new Dictionary<string, int>();

            [OnDeserialized]
            private void OnDeserialized(StreamingContext ctx)
            {
                if (Answered == null) Answered = new List<int>();
                if (Shown == null) Shown = new Dictionary<string, int>();
            }
        }

        private static DataContractJsonSerializer Serializer() =>
            new DataContractJsonSerializer(typeof(State),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        private static void Mutate(Action<State> change)
        {
            lock (Sync)
            {
                try
                {
                    var state = Read();
                    change(state);
                    Write(state);
                }
                catch
                {
                    // A survey that cannot record itself is a nuisance, not a
                    // failure worth surfacing to the user.
                }
            }
        }

        private static State Read()
        {
            try
            {
                var path = FilePath;
                if (!File.Exists(path)) return new State();

                var bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0) return new State();

                using (var ms = new MemoryStream(bytes))
                    return (State)Serializer().ReadObject(ms) ?? new State();
            }
            catch
            {
                // A corrupt file asks the question again rather than throwing at
                // startup. It is rewritten whole on the next answer.
                return new State();
            }
        }

        private static void Write(State state)
        {
            Directory.CreateDirectory(UserDataPath.TradosSettingsDir);

            using (var ms = new MemoryStream())
            {
                Serializer().WriteObject(ms, state);
                File.WriteAllText(FilePath,
                    Encoding.UTF8.GetString(ms.ToArray()),
                    new UTF8Encoding(false));
            }
        }
    }
}
