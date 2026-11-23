using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// Non-verbal UX v2 frames (D23, docs/nonverbal-ux.md, mocks in docs/evidence/s002/nonverbal_v2): the tier-2 bat
    /// trail from the arrival porch (27) and Bea's vignette bubble mid-animation (24: since D26, only the silver flash she
    /// saw). The memory replay (25) and notebook page (26) frames were retired by S002.5a. The first-launch picture cards (28) were retired with D23.1
    /// (one hybrid reading mode).
    /// </summary>
    [Category("Visual")]
    public class NonverbalCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        private static IEnumerator Shot(string name, Canvas canvas = null)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas != null ? canvas : game.Director.UiCanvas,
                name, Width, Height);
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

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureNonverbalFrames()
        {
            yield return Load();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            var layer = director.Nonverbal;
            game.Ui.NewGameButton.onClick.Invoke();
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(1.5f);

            // 27: the knocks are heard, so the goal is Mr. Vale's bench (off to the right): the radio at tier 2 flies
            // the paper-bat trail there from the arrival porch, mid-wave, with the goal sticker up.
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return Wait(0.2f);
            director.Captions.Clear();
            layer.RadioButton.onClick.Invoke();
            layer.RadioButton.onClick.Invoke();
            director.Captions.Clear();
            Assert.IsTrue(layer.Guide.Active, "27_bat_trail: tier 2 should start the trail");
            Assert.AreEqual(CaseObjective.CheckView, layer.Guide.Objective, "27_bat_trail: the bench is the goal");
            yield return Wait(1.85f);
            Assert.Greater(layer.Guide.VisibleBats, 0, "27_bat_trail: bats should be flying");
            Assert.IsTrue(layer.Guide.BeaconVisible, "27_bat_trail: the goal sticker should be up");
            yield return Shot("27_bat_trail");
            layer.Guide.Stop();
            director.Captions.Clear();

            // 24: Bea's bubble, framed like 14_dialogue, on the beat where her hand points and the ring pulses.
            var bea = CaseStage.Find(CaseStage.PointBea).Position;
            var bike = CaseStage.Find(CaseStage.PointBicycle).Position;
            var beaAt = new Vector3(bea.X, 0f, bea.Z);
            Vector3 away = beaAt - new Vector3(bike.X, 0f, bike.Z);
            away = (away.normalized + Vector3.right).normalized;
            game.Pip.Teleport(beaAt + away * 2.4f + Vector3.up * 0.1f);
            game.Pip.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(1.5f);

            director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return null;
            Assert.IsTrue(director.Dialogue.ShowsVignette, "24_bea_vignette: Bea's key line should play its vignette");
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            var pip = game.Pip.transform;
            camera.transform.position = pip.position + pip.right * 0.9f + Vector3.up * 1.3f - pip.forward * 1.4f;
            camera.transform.LookAt(beaAt + Vector3.up * 1.05f);
            float timeout = Time.realtimeSinceStartup + 20f;
            while (director.Dialogue.Vignette.LocalTime < 2.95f && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            yield return Shot("24_bea_vignette");
            if (brain != null)
            {
                brain.enabled = true;
            }

            // 25 and 26 were retired with D26 (S002.5a): memory replays no longer auto-play, and the notebook page is
            // captured by DeductionCaptureTests (the tray, a wrong-try wobble, the solved comic, the case card).
            director.Dialogue.Advance();
            yield return null;
            Assert.IsFalse(layer.MemoryReplay.Active, "D26: no replay auto-plays after the line");
        }

        /// <summary>
        /// S002 hints close: the tier-1 sparkle on Mr. Vale's bench with Vale turning to look (33), the tier-3 ghost
        /// hand tapping Use on touch (34) and the same hint with a keyboard, where the key glyph pulses (36).
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureHintFrames()
        {
            yield return Load();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            var layer = director.Nonverbal;
            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(1.0f);
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return Wait(0.2f);
            director.Captions.Clear();

            // 33: a few steps from the bench, looking at it (Vale beside it): tier 1 sparkles on the bench.
            var bench = CaseStage.Find(CaseStage.PointBench).Position;
            var benchAt = new Vector3(bench.X, 0f, bench.Z);
            var pip = game.Pip.transform.position;
            Vector3 away = new Vector3(pip.x - bench.X, 0f, pip.z - bench.Z).normalized;
            // Pip a step to the side of the line to the bench, so the bench and Vale stay clear of Pip in the frame.
            Vector3 side = Vector3.Cross(Vector3.up, away).normalized;
            game.Pip.Teleport(benchAt + away * 4.6f + side * 1.3f + Vector3.up * 0.1f);
            game.Pip.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            game.Ui.FirstPrompt.SetActive(false);
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(1.6f);
            layer.RadioButton.onClick.Invoke(); // the hint line stays: it carries the star badge
            Assert.IsTrue(layer.Guide.SparkleActive, "33_hint_sparkle: tier 1 should sparkle");
            yield return Wait(0.9f);
            Assert.Greater(layer.Guide.VisibleFlecks, 0, "33_hint_sparkle: flecks should be drawn");
            Debug.Log("[NonverbalCaptureTests] 33 looker: " + (layer.Guide.Looker != null ? layer.Guide.Looker.name : "none"));
            yield return Shot("33_hint_sparkle");

            // 34: next to the bench (Use in reach), tier 3 on touch: the ghost hand taps the Use disc.
            layer.RadioButton.onClick.Invoke(); // tier 2
            layer.Guide.Stop();
            game.Pip.Teleport(benchAt + away * 1.3f + Vector3.up * 0.1f);
            game.Pip.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(1.2f);
            SpookyStreet.Controls.ControlSchemeTracker.Override = SpookyStreet.Core.Nonverbal.ControlScheme.Touch;
            layer.RadioButton.onClick.Invoke(); // tier 3
            director.Captions.Clear();
            Assert.IsTrue(layer.Demo.Active, "34_hint_ghost_hand: tier 3 should demonstrate");
            Debug.Log("[NonverbalCaptureTests] 34 demo: " + layer.Demo.Demo);
            game.Ui.FirstPrompt.SetActive(false);
            yield return Wait(0.55f); // the finger is down on the control
            Assert.IsTrue(layer.Demo.HandVisible, "34_hint_ghost_hand: the hand should be up");
            yield return Shot("34_hint_ghost_hand", game.Ui.GetComponentInParent<Canvas>());

            // 36: the same hint with a keyboard: no hand, the key glyph pulses above the control.
            SpookyStreet.Controls.ControlSchemeTracker.Override = SpookyStreet.Core.Nonverbal.ControlScheme.KeyboardMouse;
            layer.RadioButton.onClick.Invoke();
            director.Captions.Clear();
            yield return Wait(0.8f);
            Assert.IsTrue(layer.Demo.GlyphVisible, "36_hint_key_glyph: the key glyph should be up");
            yield return Shot("36_hint_key_glyph", game.Ui.GetComponentInParent<Canvas>());
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;
            layer.Demo.Stop();
        }
    }
}
