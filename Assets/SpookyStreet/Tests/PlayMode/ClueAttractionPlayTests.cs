using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S002.5b in the running game: each clue's diegetic attraction plays near its clue and stops once it is found
    /// (and its model goes back to its own pose); attractions freeze with the pause and shrink under reduced motion;
    /// Bea's memory is a hazy silver blur with no June, offered only on demand from her sticker. Fresh temporary save
    /// folder per test.
    /// </summary>
    public class ClueAttractionPlayTests : InputTestFixture
    {
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => Game.Director;

        private static ClueAttractionView View => Director.Nonverbal.Attractions;

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.6f);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
            Assert.IsNotNull(View, "the attractions are built with the case");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Put(Vector3 at)
        {
            Game.Pip.Teleport(at + Vector3.up * 0.05f);
            yield return Frames(4);
        }

        private static void Do(CaseActionKind kind)
        {
            Director.Perform(CaseAction.Of(kind));
            if (Director.Dialogue.IsOpen)
            {
                Director.Dialogue.Skip();
            }

            Director.Captions.Clear();
        }

        /// <summary>Waits up to <paramref name="seconds"/> for <paramref name="condition"/>.</summary>
        private static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            float until = Time.time + seconds;
            while (!condition() && Time.time < until)
            {
                yield return null;
            }
        }

        /// <summary>The idle attraction plays here, its glint shows within a loop, and finding the clue stops it.</summary>
        private static IEnumerator IdleClue(string id, CaseActionKind find)
        {
            var a = ClueAttractions.Get(id);
            Assert.IsTrue(View.IsPlaying(id), id + " plays near its clue");
            bool glint = false;
            float until = Time.time + a.Period + 0.5f;
            while (Time.time < until && !glint)
            {
                glint = View.GlintVisible(id);
                yield return null;
            }

            Assert.IsTrue(glint, id + " catches the light within one loop");
            Do(find);
            yield return Frames(2);
            Assert.IsFalse(View.IsPlaying(id), id + " stops once found");
            Assert.IsFalse(View.GlintVisible(id));
            Assert.IsFalse(View.Offset(id), id + ": the model is back on its own pose");
            yield return Wait(a.Period);
            Assert.IsFalse(View.IsPlaying(id), id + " never plays again");
        }

        [UnityTest]
        public IEnumerator TheStreetCluesCallNearbyAndStopOnceFound()
        {
            yield return StartGame();
            Assert.IsFalse(View.IsPlaying(ClueAttractions.Cloth), "the cloth does not call from the arrival porch");

            // The decoy streamer twinkles like every clue.
            var streamer = ClueAttractions.Get(ClueAttractions.Streamer);
            yield return Put(new Vector3(streamer.At.X + 1.5f, 0.1f, streamer.At.Z));
            yield return IdleClue(ClueAttractions.Streamer, CaseActionKind.ExamineStreamer);

            // The wooden owl, from the front yard.
            yield return Put(new Vector3(1.2f, 0.1f, 7.5f));
            yield return IdleClue(ClueAttractions.WoodenOwl, CaseActionKind.ExamineOwlBooth);
            Assert.IsTrue(Director.State.Has(CaseFact.OwlBooth), "the porch look earns the wooden-owl sticker");

            // The silver cloth on the route: it flutters in a gust (the model leaves its pose) and glints.
            var cloth = ClueAttractions.Get(ClueAttractions.Cloth);
            var snag = View.Model(ClueAttractions.Cloth);
            Assert.IsNotNull(snag, "the cloth snag model is bound");
            Quaternion rest = snag.rotation;
            yield return Put(new Vector3(CaseRouteLayout.RoofMinX + 1f, CaseRouteLayout.TreehouseY, CaseRouteLayout.BridgeZ - 0.4f));
            Assert.IsTrue(View.IsPlaying(ClueAttractions.Cloth), "the cloth calls from the shed roof");
            bool fluttered = false;
            float until = Time.time + cloth.Period + 0.5f;
            while (Time.time < until && !fluttered)
            {
                fluttered = View.Offset(ClueAttractions.Cloth) && Quaternion.Angle(snag.rotation, rest) > 2f;
                yield return null;
            }

            Assert.IsTrue(fluttered, "the cloth flutters in a gust");
            yield return IdleClue(ClueAttractions.Cloth, CaseActionKind.ExamineCloth);
            Assert.Less(Quaternion.Angle(snag.rotation, rest), 0.01f, "the cloth hangs back at rest");
        }

        [UnityTest]
        public IEnumerator TheWindWheelWhirsUpAsAGustStartsAKnockCycle()
        {
            yield return StartGame();
            Do(CaseActionKind.HearKnocks);
            yield return Put(new Vector3(0f, 0.1f, 2f));
            Assert.IsTrue(View.IsPlaying(ClueAttractions.WindWheel));
            var wheel = View.Model(ClueAttractions.WindWheel);
            Assert.IsNotNull(wheel, "the wind wheel is bound");
            View.GustForTests();
            yield return Wait(0.4f);
            Assert.Greater(View.PeakStrength(ClueAttractions.WindWheel), 0.8f, "it whirs up before the first knock");
            Assert.IsTrue(View.Offset(ClueAttractions.WindWheel), "spun past its ambient turn");

            // Paused: the whir holds still with the game.
            float clock = View.Clock;
            Game.Flow.Pause();
            yield return Frames(10);
            Assert.AreEqual(clock, View.Clock, 1e-4f, "the attractions freeze with the pause");
            Game.Flow.Resume();
            yield return Frames(2);

            // Solving the knocks' question ends the wheel's call (the wheel still turns on its own).
            Do(CaseActionKind.EnterFoyer);
            Do(CaseActionKind.ExamineGrille);
            yield return Put(new Vector3(0f, 0.1f, 2f));
            var step = Director.SolveQuestion(CaseNotebook.QuestionSource, new[] { CaseNotebook.FloorHum, CaseNotebook.WoodenPiece });
            Assert.AreEqual(CaseOutcome.Progressed, step.Outcome);
            yield return Frames(2);
            Assert.IsFalse(View.IsPlaying(ClueAttractions.WindWheel));
            Assert.IsFalse(View.Offset(ClueAttractions.WindWheel));
        }

        [UnityTest]
        public IEnumerator TheHouseCluesCallNearbyAndStopOnceFound()
        {
            yield return StartGame();
            Do(CaseActionKind.HearKnocks);

            // The attic sill thread, from the alcove.
            yield return Put(new Vector3(-2.4f, CaseRouteLayout.LoftY, Ifz + 1.6f));
            yield return IdleClue(ClueAttractions.Sill, CaseActionKind.ExamineSill);

            // Dust hops off the hall boards on each knock, seen from the stair landing (before the hum is found).
            yield return Put(new Vector3(-2.85f, CaseRouteLayout.LandingY, Ifz + 5.3f));
            Assert.IsFalse(Director.State.Has(CaseFact.FloorHum));
            Assert.IsTrue(View.IsPlaying(ClueAttractions.FloorHum), "the boards call from the stair");
            View.KnockForTests();
            yield return Frames(3);
            Assert.Greater(View.VisibleDust, 0, "dust hops on the knock");
            Do(CaseActionKind.EnterFoyer);
            yield return Frames(2);
            Assert.IsFalse(View.IsPlaying(ClueAttractions.FloorHum), "the hum is found: the dust settles");
            Assert.AreEqual(0, View.VisibleDust);

            // The workshop partition: the owl panel rattles and the grille's slats flicker on each knock.
            yield return Put(new Vector3(0.9f, CaseRouteLayout.FloorY, CaseRouteLayout.MechanismZ));
            Assert.IsTrue(View.IsPlaying(ClueAttractions.OwlPanel));
            Assert.IsTrue(View.IsPlaying(ClueAttractions.Grille));
            Assert.IsFalse(View.IsPlaying(ClueAttractions.LooseArm), "the arm is behind the closed panel");
            var panel = View.Model(ClueAttractions.OwlPanel);
            Assert.IsNotNull(panel, "the service panel is bound");
            Quaternion shut = panel.rotation;
            View.KnockForTests();
            bool rattled = false;
            for (int i = 0; i < 20 && !rattled; i++)
            {
                yield return null;
                rattled = Quaternion.Angle(panel.rotation, shut) > 0.2f;
            }

            Assert.IsTrue(rattled, "the owl panel rattles softly on the knock");
            Assert.Less(Quaternion.Angle(panel.rotation, shut), 3f, "softly");
            yield return Wait(ClueAttractions.KnockRingSeconds + 0.1f);
            var arm = View.Model(ClueAttractions.LooseArm);
            Assert.IsNotNull(arm, "the slipped arm is bound");
            Quaternion hang = arm.rotation; // at rest: its attraction waits for the panel to open
            Do(CaseActionKind.ExamineOwlPanel);
            yield return Frames(2);
            Assert.IsFalse(View.IsPlaying(ClueAttractions.OwlPanel), "the owl panel is found");
            Assert.IsFalse(View.IsPlaying(ClueAttractions.Grille), "the panel opened on the wooden piece: seen");

            // Behind the open panel the loose arm swings when the floor hums.
            // Let the panel's own knock cycle ring out first, so the arm is measured at rest.
            yield return Until(() => !Director.Workshop.Playing, 4f);
            yield return Wait(ClueAttractions.KnockRingSeconds + 0.2f);
            Assert.IsTrue(View.IsPlaying(ClueAttractions.LooseArm));
            View.KnockForTests();
            bool swung = false;
            for (int i = 0; i < 30 && !swung; i++)
            {
                yield return null;
                swung = Quaternion.Angle(arm.rotation, hang) > 1f;
            }

            Assert.IsTrue(swung, "the loose arm wobbles");
            Do(CaseActionKind.TurnCrank); // before linking: the gap, and the loose-arm sticker
            yield return Frames(2);
            Assert.IsTrue(Director.State.Has(CaseFact.LooseArm));
            Assert.IsFalse(View.IsPlaying(ClueAttractions.LooseArm), "the loose arm is found");
            Assert.Less(Quaternion.Angle(arm.rotation, hang), 0.01f, "the arm hangs back at rest");
        }

        [UnityTest]
        public IEnumerator ReducedMotionKeepsTheGlintAndShrinksTheFlutter()
        {
            yield return StartGame();
            AmbientMotion.ReducedMotion = true;
            var snag = View.Model(ClueAttractions.Cloth);
            Quaternion rest = snag.rotation;
            yield return Put(new Vector3(CaseRouteLayout.RoofMinX + 1f, CaseRouteLayout.TreehouseY, CaseRouteLayout.BridgeZ - 0.4f));
            float maxAngle = 0f;
            bool glint = false;
            float until = Time.time + ClueAttractions.Get(ClueAttractions.Cloth).Period + 0.5f;
            while (Time.time < until)
            {
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(snag.rotation, rest));
                glint |= View.GlintVisible(ClueAttractions.Cloth);
                yield return null;
            }

            Assert.IsTrue(glint, "the light still catches the cloth");
            Assert.Less(maxAngle, ClueAttractionView.FlutterDegrees * ClueAttractions.ReducedMotionScale + 0.5f,
                "the flutter is small under reduced motion");
        }

        [UnityTest]
        public IEnumerator BeasMemoryIsAHazyBlurOnDemandOnly()
        {
            yield return StartGame();
            var replay = Director.Nonverbal.MemoryReplay;
            Assert.IsFalse(MemoryReplayView.DefaultStaging(ReplayPlace.TreehouseWindow).HasGhost, "no June in Bea's memory");
            Assert.IsTrue(MemoryReplayView.DefaultStaging(ReplayPlace.TreehouseWindow).HasStreak, "only a silver streak");

            Director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return Wait(0.4f);
            Director.Dialogue.Skip();
            yield return Wait(1.0f);
            Assert.IsFalse(replay.Active, "never on its own after her line");
            Assert.AreEqual(0, replay.PlayCount);

            Director.OpenNotebook();
            yield return Frames(3);
            var notebook = Director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(CaseNotebook.Question(CaseNotebook.QuestionEntry)));
            yield return Frames(2);
            var eye = notebook.TraySticker(CaseNotebook.BeaFlash).transform.Find("WatchAgain");
            Assert.IsNotNull(eye, "her sticker offers the memory");
            eye.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(replay.Active, "on demand from her sticker");
            Assert.AreEqual(MemoryReplays.BeaSawCape, replay.Last.Id);
            bool ghost = false;
            float maxHaze = 0f;
            float until = Time.time + MemoryReplayTiming.Default.Total + 0.5f;
            while (replay.Active && Time.time < until)
            {
                ghost |= replay.Ghost.gameObject.activeSelf;
                maxHaze = Mathf.Max(maxHaze, replay.Haze);
                yield return null;
            }

            Assert.IsTrue(replay.StreakShown, "a silver streak crossed high by the house");
            Assert.IsFalse(ghost, "June never appears");
            Assert.Greater(maxHaze, 0.1f, "the memory is hazy");
            Assert.IsFalse(replay.StreakVisible, "and it is gone");
            Assert.AreEqual(1, replay.PlayCount);
        }
    }
}
