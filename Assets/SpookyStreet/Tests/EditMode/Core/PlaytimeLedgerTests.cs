using System;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Parent;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S004 stream E: the local playtime ledger behind the grown-ups' page. Only unpaused play counts, every tick is
    /// capped (a suspend never inflates it), days roll over at local midnight, 14 days are kept, the week is seven
    /// days ending today, and a clock set backwards never shows "future" days.
    /// </summary>
    public class PlaytimeLedgerTests
    {
        private static readonly DateTime Tuesday = new DateTime(2026, 11, 24, 18, 30, 0);

        private static void Play(PlaytimeLedger ledger, double seconds, DateTime at, double frame = 1.0 / 60)
        {
            for (double t = 0; t < seconds - 1e-9; t += frame)
            {
                ledger.Tick(Math.Min(frame, seconds - t), true, at.AddSeconds(t));
            }
        }

        [Test]
        public void OnlyUnpausedPlayCounts()
        {
            var ledger = new PlaytimeLedger();
            Assert.IsTrue(ledger.Tick(0.2, true, Tuesday));
            Assert.IsFalse(ledger.Tick(0.2, false, Tuesday), "paused, title or background");
            Assert.IsFalse(ledger.Tick(0, true, Tuesday));
            Assert.IsFalse(ledger.Tick(-3, true, Tuesday));
            Assert.IsFalse(ledger.Tick(double.NaN, true, Tuesday));
            Assert.AreEqual(0.2, ledger.SecondsOn(Tuesday), 1e-9);
        }

        [Test]
        public void ASuspendedAppAddsAtMostOneCappedTick()
        {
            var ledger = new PlaytimeLedger();
            ledger.Tick(3 * 3600, true, Tuesday); // the first frame after coming back from the background
            Assert.AreEqual(PlaytimeLedger.MaxTickSeconds, ledger.SecondsOn(Tuesday), 1e-9);
            ledger.Tick(double.PositiveInfinity, true, Tuesday);
            Assert.AreEqual(2 * PlaytimeLedger.MaxTickSeconds, ledger.SecondsOn(Tuesday), 1e-9);
        }

        [Test]
        public void TimeRollsOverAtLocalMidnight()
        {
            var ledger = new PlaytimeLedger();
            var beforeMidnight = new DateTime(2026, 11, 24, 23, 59, 0);
            Play(ledger, 120, beforeMidnight);
            Assert.AreEqual(60, ledger.SecondsOn(beforeMidnight), 0.05);
            Assert.AreEqual(60, ledger.SecondsOn(beforeMidnight.AddDays(1)), 0.05);
            var week = ledger.Week(beforeMidnight.AddMinutes(2));
            Assert.IsTrue(week.Days.Last().IsToday);
            Assert.AreEqual(120, week.TotalSeconds, 0.1);
        }

        [Test]
        public void KeepsFourteenDaysAndTheWeekIsSevenEndingToday()
        {
            var ledger = new PlaytimeLedger();
            for (int d = 20; d >= 0; d--)
            {
                ledger.Tick(0.5, true, Tuesday.AddDays(-d));
            }

            Assert.AreEqual(PlaytimeLedger.KeptDays, ledger.Days.Count);
            Assert.AreEqual(PlaytimeLedger.DayNumber(Tuesday.AddDays(-13)), ledger.Days.First().Key);
            var week = ledger.Week(Tuesday);
            Assert.AreEqual(7, week.Days.Count);
            Assert.AreEqual(Tuesday.Date.AddDays(-6), week.Days[0].Date);
            Assert.AreEqual(Tuesday.Date, week.Days[6].Date);
            Assert.AreEqual(3.5, week.TotalSeconds, 1e-9);
            Assert.AreEqual(DayOfWeek.Wednesday, week.Days[0].Weekday);
            Assert.AreEqual(Keys.Tuesday, week.Days[6].WeekdayKey);
        }

        [Test]
        public void AClockSetBackwardsNeverShowsFutureDays()
        {
            var ledger = new PlaytimeLedger();
            ledger.Tick(0.5, true, Tuesday);
            ledger.Tick(0.5, true, Tuesday.AddDays(3)); // the clock was ahead
            var back = Tuesday.AddDays(-1);
            var week = ledger.Week(back);
            Assert.AreEqual(0, week.TotalSeconds, "neither the 'future' days nor anything after today counts");
            ledger.Tick(0.25, true, back);
            Assert.AreEqual(1, ledger.Days.Count, "the next tick drops the days after today");
            Assert.AreEqual(0.25, ledger.Week(back).TotalSeconds, 1e-9);
        }

        [Test]
        public void TheFileRoundTripsAndSkipsBadEntries()
        {
            var ledger = new PlaytimeLedger();
            ledger.Tick(0.5, true, Tuesday.AddDays(-2));
            Play(ledger, 90, Tuesday);
            string json = PlaytimeLedger.Serialize(ledger);
            StringAssert.Contains("2026-11-24=90.0", json);
            Assert.IsTrue(PlaytimeLedger.TryRead(json, out var back, out _));
            Assert.AreEqual(90, back.SecondsOn(Tuesday), 0.1);
            Assert.AreEqual(0.5, back.SecondsOn(Tuesday.AddDays(-2)), 0.1);

            string odd = "{\"schemaVersion\":1,\"days\":[\"2026-11-24=30\",\"nonsense\",\"2026-13-40=5\",\"2026-11-23=-4\",\"2026-11-22=999999\"]}";
            Assert.IsTrue(PlaytimeLedger.TryRead(odd, out var read, out _));
            Assert.AreEqual(30, read.SecondsOn(Tuesday));
            Assert.AreEqual(0, read.SecondsOn(Tuesday.AddDays(-1)));
            Assert.AreEqual(86400, read.SecondsOn(Tuesday.AddDays(-2)), "a day never holds more than a day");

            Assert.IsFalse(PlaytimeLedger.TryRead("{\"schemaVersion\":2,\"days\":[]}", out _, out bool newer));
            Assert.IsTrue(newer);
            Assert.IsFalse(PlaytimeLedger.TryRead("garbage", out _, out _));
        }

        [Test]
        public void TheStoreWritesAndReloadsPlaytimeJson()
        {
            var fs = new MemorySaveFileSystem();
            var store = new PlaytimeStore(fs, "/save");
            var ledger = store.Load();
            Assert.AreEqual(0, ledger.Days.Count);
            ledger.Tick(0.5, true, Tuesday);
            Assert.IsTrue(store.Save(ledger));
            Assert.IsTrue(fs.Exists(store.Paths.Latest));
            StringAssert.EndsWith("playtime.json", store.Paths.Latest);
            Assert.AreEqual(0.5, new PlaytimeStore(fs, "/save").Load().SecondsOn(Tuesday), 1e-9);
        }

        [TestCase(0, "ui.parent.time_none")]
        [TestCase(30, "ui.parent.time_under_minute")]
        [TestCase(7 * 60 + 20, "ui.parent.time_minutes", 7)]
        [TestCase(23 * 60, "ui.parent.time_minutes", 25)]
        [TestCase(80 * 60 + 40, "ui.parent.time_hours_minutes", 1, 20)]
        [TestCase(119 * 60, "ui.parent.time_hours", 2)]
        public void TheTotalReadsAsAboutHoursAndMinutes(double seconds, string key, params int[] args)
        {
            Assert.AreEqual(key, PlaytimeWeek.DescribeKey(seconds, out var values));
            CollectionAssert.AreEqual(args, values.Cast<int>().ToArray());
        }

        [Test]
        public void DotsAreSizesNotGoals()
        {
            var ledger = new PlaytimeLedger();
            Play(ledger, 10 * 60, Tuesday.AddDays(-2), 0.5);
            Play(ledger, 30 * 60, Tuesday.AddDays(-1), 0.5);
            Play(ledger, 50 * 60, Tuesday, 0.5);
            var levels = ledger.Week(Tuesday).Days.Select(d => d.Level).ToArray();
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 2, 3 }, levels);
        }
    }
}
