using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S002.5b cutscene frames (D28): the opening (50 the street at dusk, 51 the moving boxes as Pip steps out, 52 the
    /// owl house's round window) and the reveal staged from the child's stickers (53 a sticker shot with the child's
    /// sticker, 54 June crossing the bridge, 55 the owl bowing). Every other beat, and every other sticker's shot through
    /// a second reveal built from other stickers, is captured small ("r_*") for review only.
    /// </summary>
    [Category("Visual")]
    public class CutsceneCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const int ReviewWidth = 640;
        private const int ReviewHeight = 296;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory(playOpening: true);
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            CaseCutscenes.AutoPlayOpening = false;
            base.TearDown();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static CutscenePlayer Player => Director.Cutscenes.Player;

        private static IEnumerator Shot(string name, int width = Width, int height = Height)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, game.Director.UiCanvas, name, width, height);
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        /// <summary>Waits for <paramref name="beatId"/> at <paramref name="time"/> s, freezes the cutscene and captures it.</summary>
        private static IEnumerator At(string beatId, float time, string name, bool review = false)
        {
            float until = Time.time + 60f;
            while (Player.Active && Time.time < until)
            {
                var playback = Player.Playback;
                var beat = playback.Beat;
                if (beat != null && beat.Id == beatId && (playback.BeatTime >= time || playback.Waiting))
                {
                    break;
                }

                int index = -1;
                for (int i = 0; i < playback.Cutscene.Beats.Count; i++)
                {
                    if (playback.Cutscene.Beats[i].Id == beatId)
                    {
                        index = i;
                    }
                }

                Assert.GreaterOrEqual(index, 0, beatId + " is in " + playback.Cutscene.Id);
                if (!playback.Playing || playback.BeatIndex > index)
                {
                    Assert.Fail(name + ": passed " + beatId);
                }

                yield return null;
            }

            Assert.IsTrue(Player.Active, name + ": the cutscene is playing");
            Player.Hold = true;
            var move = GameBootstrap.Instance.Controls.ScriptedMove;
            GameBootstrap.Instance.Controls.ScriptedMove = Vector2.zero;
            Debug.Log($"[CutsceneCapture] {name}: {Player.Playback.Beat?.Id} at {Player.Playback.BeatTime:F2}s, camera " +
                      $"{Player.Playback.Camera}, sticker {(Player.Sticker != null)}, line '{Player.Line.text}'");
            yield return review ? Shot(name, ReviewWidth, ReviewHeight) : Shot(name);
            GameBootstrap.Instance.Controls.ScriptedMove = move;
            Player.Hold = false;
        }

        private static IEnumerator PlayToTheStamp(string[] entry, string[] source, string[] moved)
        {
            var director = Director;
            foreach (var action in new[]
                     {
                         CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
                         CaseAction.Of(CaseActionKind.SitOnBench), CaseAction.Of(CaseActionKind.ExamineCloth),
                         CaseAction.Of(CaseActionKind.TryFrontDoor), CaseAction.Of(CaseActionKind.EnterAttic),
                         CaseAction.Of(CaseActionKind.ExamineSill), CaseAction.Talk(Speaker.June),
                         CaseAction.Of(CaseActionKind.EnterFoyer), CaseAction.Of(CaseActionKind.LiftLatch),
                         CaseAction.Of(CaseActionKind.ExamineGrille), CaseAction.Of(CaseActionKind.ExamineOwlPanel),
                         CaseAction.Of(CaseActionKind.LinkArm), CaseAction.Of(CaseActionKind.TurnCrank),
                     })
            {
                director.Perform(action);
                director.Dialogue.Skip();
                director.Cutaway.Skip();
                yield return null;
                yield return null;
            }

            director.Captions.Clear();
            yield return Wait(0.3f);
            director.Perform(CaseAction.Of(CaseActionKind.ArriveAtPorch));
            yield return Wait(FinaleView.IntroSeconds + 0.3f);
            while (director.Dialogue.IsOpen)
            {
                director.Dialogue.Skip();
                yield return null;
            }

            yield return Wait(FinaleView.HoldAfterLines + 0.5f);
            director.Notebook.Close();
            yield return null;
            Assert.IsNotNull(director.SolveQuestion(N.QuestionEntry, entry));
            Assert.IsNotNull(director.SolveQuestion(N.QuestionSource, source));
            Assert.IsNotNull(director.SolveQuestion(N.QuestionMoved, moved));
            Director.Captions.Clear();
            yield return null;
            yield return null;
            Assert.IsTrue(Director.Cutscenes.Playing, "the reveal plays after the third deduction");
        }

        [UnityTest]
        [Timeout(400000)]
        public IEnumerator CapturesTheOpeningAndTheReveal()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(Director.Cutscenes.Playing, "the opening plays");

            // The opening.
            yield return At("town", 4.4f, "50_opening_town");
            yield return At("boxes", 1.3f, "51_opening_boxes");
            yield return At("window", 3.0f, "52_opening_window");
            yield return At("wave", 1.6f, "r_opening_wave", review: true);
            yield return At("porch", 3.2f, "r_opening_porch", review: true);
            while (Player.Active)
            {
                yield return null;
            }

            yield return Wait(0.5f);
            yield return Shot("r_opening_control", ReviewWidth, ReviewHeight);

            // The reveal, from the child's stickers: Bea's flash, Vale's door, the sill; the hum and the piece; the booth owl and the arm.
            var entry = new[] { N.SillSnag, N.ValeDoor, N.BeaFlash };
            var source = new[] { N.FloorHum, N.WoodenPiece };
            var moved = new[] { N.OwlBooth, N.LooseArm };
            yield return PlayToTheStamp(entry, source, moved);
            var reveal = Player.Current;
            foreach (var beat in reveal.Beats)
            {
                string id = beat.Id;
                if (id == "sticker:" + N.ValeDoor)
                {
                    yield return At(id, 1.1f, "53_reveal_sticker_vale");
                }
                else if (id == "story:entry")
                {
                    yield return At(id, 1.6f, "54_reveal_june_bridge");
                }
                else if (id == "story:moved")
                {
                    yield return At(id, 1.3f, "55_reveal_owl_bows");
                }
                else
                {
                    yield return At(id, beat.Duration * 0.6f, "r_reveal_" + id.Replace("c01.sticker.", "").Replace(':', '_'), review: true);
                }
            }

            while (Player.Active)
            {
                yield return null;
            }

            // The other stickers' shots, through a reveal built from a different (equally right) set.
            var alternative = CaseAnswers.Empty
                .With(N.QuestionEntry, new[] { N.StuckDoor, N.Cloth, N.BenchView })
                .With(N.QuestionSource, source)
                .With(N.QuestionMoved, new[] { N.OwlPanel, N.LooseArm });
            var other = C01Cutscenes.Reveal(Director.State, alternative);
            Assert.IsNotNull(other);
            Player.Play(other, false, true, false);
            foreach (string sticker in new[] { N.StuckDoor, N.Cloth, N.BenchView, N.OwlPanel })
            {
                yield return At("sticker:" + sticker, 1.0f, "r_reveal_alt_" + sticker.Replace("c01.sticker.", ""), review: true);
            }

            Player.SkipNow();
            yield return Wait(0.6f);
            Assert.IsFalse(Player.Active);
        }
    }
}
