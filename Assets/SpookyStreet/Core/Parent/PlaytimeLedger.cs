using System;
using System.Collections.Generic;
using System.Globalization;
using SpookyStreet.Core.Save;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;

namespace SpookyStreet.Core.Parent
{
    /// <summary>
    /// Local play time per local calendar day, for the grown-ups' page only (S004 §2.5, engagement §2.9): it counts
    /// only unpaused play, caps every tick so a suspended app or a stalled frame never inflates it, and keeps the last
    /// <see cref="KeptDays"/> local days. Days after "today" (the clock was set backwards) are dropped from the week
    /// and from the file on the next tick. It is never shown to the child and drives nothing else: no goals, no
    /// limits, no rewards. Nothing here leaves the device.
    /// </summary>
    public sealed class PlaytimeLedger
    {
        public const int KeptDays = 14;
        public const int WeekDays = 7;

        /// <summary>The longest single tick counted: a frame after a suspend or a hitch adds at most this.</summary>
        public const double MaxTickSeconds = 0.5;

        private readonly SortedDictionary<int, double> _seconds = new SortedDictionary<int, double>();

        /// <summary>Days kept, oldest first (day number, seconds).</summary>
        public IReadOnlyList<KeyValuePair<int, double>> Days => new List<KeyValuePair<int, double>>(_seconds);

        /// <summary>The local calendar day of <paramref name="localTime"/> as a day number (days since 0001-01-01).</summary>
        public static int DayNumber(DateTime localTime) => (int)(localTime.Date.Ticks / TimeSpan.TicksPerDay);

        public static DateTime DateOf(int day) => new DateTime(day * TimeSpan.TicksPerDay);

        /// <summary>
        /// One frame of the running game: <paramref name="elapsedSeconds"/> (unscaled) counts only while
        /// <paramref name="playing"/> (not paused, not on the title, app in front), capped at <see cref="MaxTickSeconds"/>,
        /// on the local day of <paramref name="localNow"/>. Returns true when time was added.
        /// </summary>
        public bool Tick(double elapsedSeconds, bool playing, DateTime localNow)
        {
            if (!playing || double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0)
            {
                return false;
            }

            double add = Math.Min(elapsedSeconds, MaxTickSeconds);
            int today = DayNumber(localNow);
            _seconds.TryGetValue(today, out double sum);
            _seconds[today] = sum + add;
            Trim(today);
            return true;
        }

        /// <summary>Drops every day outside the kept window ending on <paramref name="today"/> (older, or "future").</summary>
        public void Trim(int today)
        {
            var drop = new List<int>();
            foreach (int day in _seconds.Keys)
            {
                if (day > today || day <= today - KeptDays)
                {
                    drop.Add(day);
                }
            }

            foreach (int day in drop)
            {
                _seconds.Remove(day);
            }
        }

        public double SecondsOn(DateTime localDay) => _seconds.TryGetValue(DayNumber(localDay), out double s) ? s : 0;

        /// <summary>The seven local days ending today (oldest first) and their total; computed, never stored.</summary>
        public PlaytimeWeek Week(DateTime localNow)
        {
            int today = DayNumber(localNow);
            var days = new PlaytimeDay[WeekDays];
            double total = 0;
            for (int i = 0; i < WeekDays; i++)
            {
                int day = today - (WeekDays - 1) + i;
                _seconds.TryGetValue(day, out double s);
                total += s;
                days[i] = new PlaytimeDay(DateOf(day), s, day == today);
            }

            return new PlaytimeWeek(days, total);
        }

        // ------------------------------------------------------------------------------------- file

        public const int CurrentSchemaVersion = 1;

        /// <summary><c>playtime.json</c>: the schema and one "yyyy-MM-dd=seconds" entry per kept day.</summary>
        public static string Serialize(PlaytimeLedger ledger)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var days = new List<string>(ledger._seconds.Count);
            foreach (var pair in ledger._seconds)
            {
                days.Add(DateOf(pair.Key).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "=" +
                         Math.Round(pair.Value, 1).ToString("0.0", CultureInfo.InvariantCulture));
            }

            return new SaveJsonWriter()
                .Number("schemaVersion", CurrentSchemaVersion)
                .Strings("days", days)
                .ToString();
        }

        /// <summary>False (and an empty ledger) for a broken, foreign or newer file; bad entries are skipped.</summary>
        public static bool TryRead(string json, out PlaytimeLedger ledger, out bool newerSchema)
        {
            ledger = new PlaytimeLedger();
            newerSchema = false;
            var root = SaveJsonReader.ParseObject(json);
            if (root == null || !SaveJsonReader.TryInt(root, "schemaVersion", out int version) || version < 1)
            {
                return false;
            }

            if (version > CurrentSchemaVersion)
            {
                newerSchema = true;
                return false;
            }

            if (!SaveJsonReader.TryStrings(root, "days", out var entries))
            {
                return false;
            }

            foreach (string entry in entries)
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0 ||
                    !DateTime.TryParseExact(entry.Substring(0, eq), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date) ||
                    !double.TryParse(entry.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ||
                    double.IsNaN(s) || double.IsInfinity(s) || s <= 0)
                {
                    continue;
                }

                // A day can never hold more than a day.
                ledger._seconds[DayNumber(date)] = Math.Min(s, 86400);
            }

            return true;
        }

        public static SaveCheck Check(string json)
        {
            if (TryRead(json, out _, out bool newer))
            {
                return SaveCheck.Valid;
            }

            return newer ? SaveCheck.Newer : SaveCheck.Invalid;
        }
    }

    /// <summary>One local day of the week view.</summary>
    public sealed class PlaytimeDay
    {
        internal PlaytimeDay(DateTime date, double seconds, bool today)
        {
            Date = date;
            Seconds = seconds;
            IsToday = today;
        }

        public DateTime Date { get; }

        public double Seconds { get; }

        public bool IsToday { get; }

        public DayOfWeek Weekday => Date.DayOfWeek;

        /// <summary>The short weekday name under the dot.</summary>
        public string WeekdayKey => PlaytimeWeek.WeekdayKey(Weekday);

        /// <summary>The dot's size: 0 none, 1 under 15 min, 2 under 45 min, 3 longer. A dot, never a bar to fill.</summary>
        public int Level => Seconds <= 0 ? 0 : Seconds < 15 * 60 ? 1 : Seconds < 45 * 60 ? 2 : 3;
    }

    /// <summary>The week's seven days and one rounded total ("about 1 h 20 min"); no goal and no comparison.</summary>
    public sealed class PlaytimeWeek
    {
        internal PlaytimeWeek(IReadOnlyList<PlaytimeDay> days, double totalSeconds)
        {
            Days = days;
            TotalSeconds = totalSeconds;
            TotalKey = DescribeKey(totalSeconds, out var args);
            TotalArgs = args;
        }

        public IReadOnlyList<PlaytimeDay> Days { get; }

        public double TotalSeconds { get; }

        /// <summary>The total's text key; format it with <see cref="TotalArgs"/>.</summary>
        public string TotalKey { get; }

        public object[] TotalArgs { get; }

        /// <summary>
        /// Rounds a total for reading: none, under a minute, whole minutes under 10, then 5-minute steps, then hours
        /// and minutes ("about 1 h 20 min", "about 2 h").
        /// </summary>
        public static string DescribeKey(double seconds, out object[] args)
        {
            args = Array.Empty<object>();
            if (seconds <= 0)
            {
                return Keys.TimeNone;
            }

            if (seconds < 60)
            {
                return Keys.TimeUnderMinute;
            }

            int minutes = (int)Math.Round(seconds / 60, MidpointRounding.AwayFromZero);
            if (minutes >= 10)
            {
                minutes = (int)Math.Round(minutes / 5.0, MidpointRounding.AwayFromZero) * 5;
            }

            if (minutes < 60)
            {
                args = new object[] { minutes };
                return Keys.TimeMinutes;
            }

            int hours = minutes / 60;
            int rest = minutes % 60;
            if (rest == 0)
            {
                args = new object[] { hours };
                return Keys.TimeHours;
            }

            args = new object[] { hours, rest };
            return Keys.TimeHoursMinutes;
        }

        public static string WeekdayKey(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return Keys.Monday;
                case DayOfWeek.Tuesday: return Keys.Tuesday;
                case DayOfWeek.Wednesday: return Keys.Wednesday;
                case DayOfWeek.Thursday: return Keys.Thursday;
                case DayOfWeek.Friday: return Keys.Friday;
                case DayOfWeek.Saturday: return Keys.Saturday;
                default: return Keys.Sunday;
            }
        }
    }

    /// <summary><c>playtime.json</c> beside the other saves, with their atomic write and recovery.</summary>
    public sealed class PlaytimeStore
    {
        private readonly SaveSlotStore _slot;

        public PlaytimeStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, PathsFor(directory), PlaytimeLedger.Check);
        }

        public static SaveSlotPaths PathsFor(string directory) => new SaveSlotPaths(directory, "playtime");

        public SaveSlotPaths Paths => _slot.Paths;

        public bool WritesBlocked => _slot.WritesBlocked;

        /// <summary>The kept days (an empty ledger when there is no readable file; a newer file stays untouched).</summary>
        public PlaytimeLedger Load()
        {
            var load = _slot.Load();
            return load.HasContents && PlaytimeLedger.TryRead(load.Contents, out var ledger, out _) ? ledger : new PlaytimeLedger();
        }

        /// <summary>Writes the ledger unless a newer build's file is being kept.</summary>
        public bool Save(PlaytimeLedger ledger)
        {
            if (_slot.WritesBlocked)
            {
                return false;
            }

            _slot.Write(PlaytimeLedger.Serialize(ledger));
            return true;
        }
    }
}
