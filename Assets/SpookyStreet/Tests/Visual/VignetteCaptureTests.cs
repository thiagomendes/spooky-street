using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// Every case 01 line has a vignette (S002, docs/S002-vignettes.md), shown with its words (one hybrid reading mode,
    /// D23.1). Frames, numbered 40+ so they never clash with the hint captures (33+): Mr. Vale's invitation to sit
    /// (40_vale_bubble, the hybrid bubble outside), June's greeting in the attic (41_june_attic_line), Pip's sill clue
    /// (42_pip_sill_line), a caption's mini stage in the hall (43_caption_stage) and Aunt Kit's finale line
    /// (44_kit_finale_line). Each waits for a mid-animation beat, like 24_bea_vignette.
    /// </summary>
    [Category("Visual")]
    public class VignetteCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private static IEnumerator Shot(string name)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, game.Director.UiCanvas, name, Width, Height);
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

        private static void Face(Vector3 from, Vector3 to)
        {
            var game = GameBootstrap.Instance;
            Vector3 d = to - from;
            d.y = 0f;
            game.Pip.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
        }

        /// <summary>Waits until the bubble's vignette for <paramref name="lineKey"/> reaches <paramref name="localTime"/>.</summary>
        private static IEnumerator BubbleAt(string lineKey, float localTime, string frame)
        {
            var dialogue = GameBootstrap.Instance.Director.Dialogue;
            float timeout = Time.realtimeSinceStartup + 20f;
            while ((!dialogue.IsOpen || dialogue.CurrentLine.Key != lineKey) && Time.realtimeSinceStartup < timeout)
            {
                if (dialogue.IsOpen && dialogue.CurrentLine.Key != lineKey && dialogue.Vignette.LocalTime > 0.3f)
                {
                    dialogue.Advance();
                }

                yield return null;
            }

            Assert.AreEqual(lineKey, dialogue.CurrentLine.Key, frame + ": the line is on screen");
            Assert.IsTrue(dialogue.ShowsVignette, frame + ": the line plays its vignette");
            Assert.AreEqual(C01Vignettes.For(lineKey).VignetteId, dialogue.Vignette.Current.Id, frame);
            Assert.IsTrue(dialogue.TextLineVisible, frame + ": the words stay under the pictures (D23.1)");
            while (dialogue.Vignette.LocalTime < localTime && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        /// <summary>Walks Pip into a threshold's exit zone from just outside it and waits for the page to go.</summary>
        private static IEnumerator Cross(Vector3 approach, Vector3 inZone)
        {
            var game = GameBootstrap.Instance;
            var travel = game.Director.Travel;
            int before = travel.Crossings;
            game.Pip.Teleport(approach);
            yield return Wait(0.8f);
            game.Pip.Teleport(inZone);
            float until = Time.time + 3f;
            while ((travel.Active || travel.Crossings == before) && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(before + 1, travel.Crossings, "a crossing at " + inZone);
            yield return Wait(0.3f);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CaptureVignetteFrames()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            var director = game.Director;
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.5f);
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Captions.Clear();

            // 40: Mr. Vale's second line, "Sit down and see for yourself.": Pip hops onto the bench, framed like 24.
            var vale = CaseStage.Find(CaseStage.PointVale).Position;
            var valeAt = new Vector3(vale.X, 0f, vale.Z);
            var pipAt = valeAt + new Vector3(2.0f, 0.1f, -1.2f);
            game.Pip.Teleport(pipAt);
            Face(pipAt, valeAt);
            yield return Wait(1.2f);
            director.Perform(CaseAction.Talk(Speaker.Vale));
            if (brain != null)
            {
                brain.enabled = false;
            }

            var pip = game.Pip.transform;
            camera.transform.position = pip.position - pip.right * 0.9f + Vector3.up * 1.3f - pip.forward * 1.4f;
            camera.transform.LookAt(valeAt + Vector3.up * 1.0f);
            yield return BubbleAt(Lines.ValeInviteSit, 1.05f, "40_vale_bubble");
            yield return Shot("40_vale_bubble");
            director.Dialogue.Skip();
            if (brain != null)
            {
                brain.enabled = true;
            }

            yield return Wait(3.2f); // Vale's memory replay (first hearing) plays and ends
            director.Nonverbal.MemoryReplay.End(false);
            director.Captions.Clear();
            if (director.House == null)
            {
                Assert.Ignore("Environment models are not committed: the interior is not staged.");
            }

            // 41: in by June's window; June's greeting in the attic, on the wave.
            yield return Cross(new Vector3(-4.3f, 2.55f, CaseRouteLayout.ExteriorWindowZ),
                new Vector3(-3.36f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            var june = CaseStage.JuneAttic.Position;
            var atticAt = new Vector3(-0.3f, CaseRouteLayout.LoftY + 0.05f, Ifz + 3.6f);
            game.Pip.Teleport(atticAt);
            Face(atticAt, new Vector3(june.X + 0.5f, june.Y, june.Z - 0.6f));
            yield return Wait(2.2f);
            director.Captions.Clear();
            director.Perform(CaseAction.Talk(Speaker.June));
            yield return BubbleAt(Lines.JuneGreet, 0.85f, "41_june_attic_line");
            yield return Shot("41_june_attic_line");
            director.Dialogue.Skip();
            yield return Wait(0.3f);

            // 42: Pip's sill clue, the ring pulsing on the silver snag.
            director.Perform(CaseAction.Of(CaseActionKind.ExamineSill));
            yield return BubbleAt(Lines.ExamineSill, 1.3f, "42_pip_sill_line");
            yield return Shot("42_pip_sill_line");
            director.Dialogue.Skip();

            // 43: down in the hall, the floor-hum caption plays its mini stage above the strip.
            var hall = new Vector3(0f, 0.65f, Ifz + 2.4f);
            game.Pip.Teleport(hall);
            director.Perform(CaseAction.Of(CaseActionKind.LiftLatch));
            director.Dialogue.Skip();
            Face(hall, hall + Vector3.back);
            yield return Wait(1.5f);
            director.Captions.Clear();
            director.Captions.ShowKey(Lines.CaptionFoyerHum);
            yield return null;
            Assert.IsTrue(director.Captions.ShowsVignette, "43_caption_stage: the caption plays its vignette");
            Assert.AreEqual(C01Vignettes.CaptionFoyerHum, director.Captions.Vignette.Current.Id);
            Assert.IsNotNull(director.Captions.CurrentText, "43_caption_stage: the words stay on the strip");
            float timeout = Time.realtimeSinceStartup + 10f;
            while (director.Captions.Vignette.LocalTime < 0.85f && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            yield return Shot("43_caption_stage");
            director.Captions.Clear();

            // 44: the machine, out the front door, back to the porch: Aunt Kit's first finale line.
            var crankStand = new Vector3(CaseRouteLayout.NookX - 0.7f, 0.65f, CaseRouteLayout.MechanismZ + 0.1f);
            game.Pip.Teleport(crankStand);
            Face(crankStand, crankStand + Vector3.right);
            director.Perform(CaseAction.Of(CaseActionKind.ExamineGrille));
            director.Perform(CaseAction.Of(CaseActionKind.LinkArm));
            yield return Wait(MechanismMotion.CycleSeconds + 0.3f);
            director.Perform(CaseAction.Of(CaseActionKind.TurnCrank));
            yield return Wait(0.5f);
            director.Cutaway.Skip();
            director.Dialogue.Skip();
            yield return Wait(0.5f);
            game.Pip.Teleport(new Vector3(0f, 0.65f, Ifz + 1.2f));
            Face(new Vector3(0f, 0.65f, Ifz + 1.2f), new Vector3(0f, 0.65f, Ifz));
            yield return Wait(1.2f);
            game.Pip.Teleport(new Vector3(0f, 0.65f, Ifz + 0.05f));
            var travel = director.Travel;
            float until = Time.time + 4f;
            while ((travel.Active || CaseRouteLayout.Inside(game.Pip.transform.position.x, game.Pip.transform.position.z)) &&
                   Time.time < until)
            {
                yield return null;
            }

            yield return Wait(0.5f);
            game.Pip.Teleport(new Vector3(0f, 0.7f, -13.4f));
            Face(new Vector3(0f, 0.7f, -13.4f), new Vector3(0f, 0.7f, 0f));
            yield return BubbleAt(Lines.KitFirstShow, 1.85f, "44_kit_finale_line");
            Assert.IsTrue(director.FinalePlaying, "44_kit_finale_line: the finale framing holds");
            yield return Shot("44_kit_finale_line");
            director.Dialogue.Skip();
        }
    }
}
