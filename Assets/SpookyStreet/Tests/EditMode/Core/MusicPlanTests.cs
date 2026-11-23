using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Audio;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The score's rules (D20.2, <see cref="MusicPlan"/>): the theme first on the street, then exploration passes with
    /// crossfades on the loop seam; exploration only and lower indoors; the sting and the reveal over a faded loop that
    /// comes back after them; silence and resume; the exact loop lengths of the imported clips.
    /// </summary>
    public class MusicPlanTests
    {
        private static double Theme => VoiceMusicEvents.MusicSeconds(VoiceMusicEvents.MusicStreetTheme);
        private static double Explore => VoiceMusicEvents.MusicSeconds(VoiceMusicEvents.MusicExploration);

        private static List<MusicCommand> Run(MusicPlan plan, double seconds, MusicPlace place, double step = 0.1)
        {
            var all = new List<MusicCommand>();
            for (double t = 0; t < seconds - 1e-9; t += step)
            {
                plan.Update(step, place, all);
            }

            return all;
        }

        [Test]
        public void LoopLengthsAreTheExactSampleCountsOfTheMasters()
        {
            Assert.AreEqual(3637510, VoiceMusicData.StreetThemeSamples);
            Assert.AreEqual(75.781, Theme, 0.001, "music_edits.json: the theme's loop region is 75.781 s");
            Assert.AreEqual(76.8, Explore, 0.001, "24 bars of 4 at 75 bpm");
            Assert.AreEqual(5.778, VoiceMusicEvents.MusicSeconds(VoiceMusicEvents.MusicCaseClosed), 0.001);
            Assert.AreEqual(19.364, VoiceMusicEvents.MusicSeconds(VoiceMusicEvents.MusicReveal), 0.001);
        }

        [Test]
        public void TheStreetPlaysTheThemeOnceThenExplorationPassesThenTheThemeAgain()
        {
            var plan = new MusicPlan();
            var commands = new List<MusicCommand>();
            plan.Update(0.016, MusicPlace.Street, commands);
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Start, VoiceMusicEvents.MusicStreetTheme, MusicPlan.FirstFadeInSeconds),
                commands[0]);

            // Just before the crossfade point nothing changes; at it the theme hands over to exploration.
            commands = Run(plan, Theme - MusicPlan.LoopCrossfadeSeconds - 0.3, MusicPlace.Street);
            Assert.IsEmpty(commands);
            commands = Run(plan, 0.5, MusicPlace.Street);
            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(MusicCommandKind.Stop, commands[0].Kind);
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, commands[0].EventId);
            Assert.AreEqual(MusicPlan.LoopCrossfadeSeconds, commands[0].FadeSeconds);
            Assert.AreEqual(MusicCommandKind.Start, commands[1].Kind);
            Assert.AreEqual(VoiceMusicEvents.MusicExploration, commands[1].EventId);
            Assert.AreEqual(MusicPlan.LoopCrossfadeSeconds, commands[1].FadeSeconds, "a crossfade, not a cut");
            Assert.AreEqual(MusicPlan.ExplorationPassesPerTheme, plan.PassesLeft);

            // Two exploration passes (the clip loops by itself in between), then the theme again.
            commands = Run(plan, 2 * Explore - MusicPlan.LoopCrossfadeSeconds - 1.0, MusicPlace.Street);
            Assert.IsEmpty(commands, "the first pass wraps on the clip's own seam");
            Assert.AreEqual(1, plan.PassesLeft);
            commands = Run(plan, 1.5, MusicPlace.Street);
            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(VoiceMusicEvents.MusicExploration, commands[0].EventId);
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, commands[1].EventId);
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, plan.Loop);
        }

        [Test]
        public void IndoorsIsExplorationOnlyAndLower()
        {
            var plan = new MusicPlan();
            Run(plan, 10, MusicPlace.Street);
            var commands = Run(plan, 0.1, MusicPlace.Indoors);
            Assert.AreEqual(2, commands.Count, "the theme crosses into exploration at the threshold");
            Assert.AreEqual(MusicPlan.PlaceCrossfadeSeconds, commands[1].FadeSeconds);
            Assert.AreEqual(VoiceMusicEvents.MusicExploration, plan.Loop);

            commands = Run(plan, 4 * Explore, MusicPlace.Indoors, 0.25);
            Assert.IsEmpty(commands, "exploration loops for as long as Pip is inside");

            // Back outside, this pass finishes, then the theme comes back.
            double left = Explore - plan.LoopClock;
            commands = Run(plan, left - MusicPlan.LoopCrossfadeSeconds - 0.5, MusicPlace.Street);
            Assert.IsEmpty(commands);
            commands = Run(plan, 1.0, MusicPlace.Street);
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, plan.Loop);

            Assert.AreEqual(MusicPlan.IndoorTrimDb, MusicPlan.TrimDb(MusicPlace.Indoors, false, false));
            Assert.AreEqual(0f, MusicPlan.TrimDb(MusicPlace.Street, false, false));
            Assert.AreEqual(0f, MusicPlan.TrimDb(MusicPlace.Title, false, false));
            Assert.AreEqual(MusicPlan.PausedTrimDb, MusicPlan.TrimDb(MusicPlace.Street, true, false), "lower under the pause card");
            Assert.AreEqual(MusicPlan.IndoorTrimDb + MusicPlan.HeldDuckDb, MusicPlan.TrimDb(MusicPlace.Indoors, false, true));
            Assert.Less(MusicPlan.IndoorTrimDb, 0f);
        }

        [Test]
        public void StartingIndoorsStartsExploration()
        {
            var plan = new MusicPlan();
            var commands = new List<MusicCommand>();
            plan.Update(0.1, MusicPlace.Indoors, commands);
            Assert.AreEqual(VoiceMusicEvents.MusicExploration, commands[0].EventId);
        }

        [Test]
        public void TheStingPlaysOverAFadedLoopThatComesBackAfter()
        {
            var plan = new MusicPlan();
            Run(plan, 30, MusicPlace.Street);
            var commands = new List<MusicCommand>();
            Assert.IsTrue(plan.PlayCue(VoiceMusicEvents.MusicCaseClosed, commands));
            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Stop, VoiceMusicEvents.MusicStreetTheme, MusicPlan.CueInSeconds), commands[0]);
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Start, VoiceMusicEvents.MusicCaseClosed, 0f), commands[1]);
            Assert.AreEqual(VoiceMusicEvents.MusicCaseClosed, plan.Cue);
            Assert.IsNull(plan.Loop);

            commands = Run(plan, 5.5, MusicPlace.Street);
            Assert.IsEmpty(commands, "the sting plays out");
            commands = Run(plan, 0.5, MusicPlace.Street);
            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(MusicCommandKind.Stop, commands[0].Kind);
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Start, VoiceMusicEvents.MusicStreetTheme, MusicPlan.ResumeFadeSeconds), commands[1]);
            Assert.IsNull(plan.Cue);
            Assert.AreEqual(0.0, plan.LoopClock, 0.35, "the theme comes back from its start");
        }

        [Test]
        public void TheRevealReplacesTheStingAndCanBeStoppedEarly()
        {
            var plan = new MusicPlan();
            Run(plan, 3, MusicPlace.Street);
            var commands = new List<MusicCommand>();
            plan.PlayCue(VoiceMusicEvents.MusicCaseClosed, commands);
            commands.Clear();
            plan.PlayCue(VoiceMusicEvents.MusicReveal, commands);
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Stop, VoiceMusicEvents.MusicCaseClosed, MusicPlan.CueOutSeconds), commands[0]);
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Start, VoiceMusicEvents.MusicReveal, 0f), commands[1]);
            Assert.IsEmpty(Run(plan, 15, MusicPlace.Street), "the reveal is longer than the sting");

            commands.Clear();
            Assert.IsTrue(plan.StopCue(0.3f, commands));
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Stop, VoiceMusicEvents.MusicReveal, 0.3f), commands[0]);
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, commands[1].EventId);
            Assert.IsFalse(plan.StopCue(0.3f, commands), "nothing left to stop");
            Assert.IsFalse(plan.PlayCue("ui_tap", commands), "not music");
        }

        [Test]
        public void ALoopCueJumpsToThatLoopAndSilenceHoldsUntilResumed()
        {
            var plan = new MusicPlan();
            Run(plan, 3, MusicPlace.Street);
            var commands = new List<MusicCommand>();
            plan.PlayCue(VoiceMusicEvents.MusicStreetTheme, commands);
            Assert.AreEqual(MusicCommandKind.Stop, commands[0].Kind, "the opening restarts the theme from its start");
            Assert.AreEqual(MusicCommandKind.Start, commands[1].Kind);
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, plan.Loop);

            commands.Clear();
            plan.StopAll(1f, commands);
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(1f, commands[0].FadeSeconds);
            Assert.IsTrue(plan.Silenced);
            Assert.IsEmpty(Run(plan, 100, MusicPlace.Street), "silent until resumed");
            plan.Resume();
            commands = Run(plan, 0.1, MusicPlace.Street);
            Assert.AreEqual(new MusicCommand(MusicCommandKind.Start, VoiceMusicEvents.MusicStreetTheme, MusicPlan.ResumeFadeSeconds), commands[0]);
        }
    }
}
