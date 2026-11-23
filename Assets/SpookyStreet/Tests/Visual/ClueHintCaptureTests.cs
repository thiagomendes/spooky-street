using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S002.5b frames (D26, docs/S002.5b-clues-hints.md): the silver cloth's glint on the treehouse route (54), the
    /// tier-1 breeze and warm glow over the side yard (55), the tier-2 bats circling it (56), Bea's hazy memory with
    /// its silver blur (57), and the tier-3 move demo at the crate on touch (34) and with a keyboard (36), which replace
    /// the S002 Use demo frames of the same numbers.
    /// </summary>
    [Category("Visual")]
    public class ClueHintCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        /// <summary>
        /// Captures with game time held (a capture renders over several frames, longer than a glint or the memory's
        /// streak lasts in batchmode), then lets time run again.
        /// </summary>
        private static IEnumerator Shot(string name, Canvas canvas = null)
        {
            var game = GameBootstrap.Instance;
            float scale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                yield return Capture(game, name, canvas);
            }
            finally
            {
                Time.timeScale = scale;
            }
        }

        private static IEnumerator Capture(GameBootstrap game, string name, Canvas canvas)
        {
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

            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(1.0f);
        }

        /// <summary>Pip at <paramref name="at"/>, facing <paramref name="look"/>, the gameplay camera behind her.</summary>
        private static IEnumerator Stand(Vector3 at, Vector3 look, float settle = 1.4f)
        {
            var game = GameBootstrap.Instance;
            game.Pip.Teleport(at + Vector3.up * 0.05f);
            Vector3 to = look - at;
            to.y = 0f;
            game.Pip.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            game.Ui.FirstPrompt.SetActive(false);
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(settle);
        }

        /// <summary>Holds the camera at <paramref name="eye"/> looking at <paramref name="look"/> (the brain off); returns it.</summary>
        private static Behaviour Frame(Vector3 eye, Vector3 look)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = eye;
            camera.transform.LookAt(look);
            return brain;
        }

        private static void Release(Behaviour brain)
        {
            if (brain != null)
            {
                brain.enabled = true;
            }
        }

        private static void Do(CaseActionKind kind)
        {
            var director = GameBootstrap.Instance.Director;
            director.Perform(CaseAction.Of(kind));
            if (director.Dialogue.IsOpen)
            {
                director.Dialogue.Skip();
            }

            director.Bench.End();
            director.Captions.Clear();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureClueAndHintFrames()
        {
            yield return Load();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            var layer = director.Nonverbal;
            var view = layer.Attractions;

            // 50: on the shed roof, the silver cloth on the bridge-head post catches the light as a gust lifts it.
            Do(CaseActionKind.HearKnocks);
            var cloth = ClueAttractions.Get(ClueAttractions.Cloth);
            var clothAt = new Vector3(cloth.At.X, cloth.At.Y, cloth.At.Z);
            // Pip on the shed roof past the post; the view from the bridge looks back at the post, the cloth and Pip.
            yield return Stand(new Vector3(CaseRouteLayout.RoofMinX + 1.3f, CaseRouteLayout.TreehouseY, CaseRouteLayout.BridgeZ - 0.2f),
                clothAt, 1.2f);
            var brain = Frame(new Vector3(-4.3f, 3.55f, 13.15f), clothAt + new Vector3(-0.35f, -0.15f, 0f));
            Assert.IsTrue(view.IsPlaying(ClueAttractions.Cloth), "54_cloth_glint: the cloth calls from the route");
            float timeout = Time.time + cloth.Period + 1f;
            while (!view.GlintVisible(ClueAttractions.Cloth) && Time.time < timeout)
            {
                yield return null;
            }

            yield return Wait(0.1f);
            Assert.IsTrue(view.GlintVisible(ClueAttractions.Cloth), "54_cloth_glint: the glint should be up");
            yield return Shot("54_cloth_glint");
            Release(brain);

            // 51: the bench is found, so the hint leads to the side yard. From the west sidewalk, tier 1: leaves ride a
            // breeze toward the side yard and a warm glow rises over it; nothing picks out a thing.
            Do(CaseActionKind.SitOnBench);
            yield return Wait(0.3f);
            director.Bench.End();
            var yard = HintAreas.For(CaseObjective.FindWayIn);
            var yardAt = new Vector3(yard.Centre.X, yard.Centre.Y, yard.Centre.Z);
            yield return Stand(new Vector3(-8.9f, 0.1f, 1.4f), yardAt);
            // A raised view behind Pip, over the hedge and clear of the street maple, so the side yard is in the frame.
            brain = Frame(new Vector3(-8.2f, 4.8f, -2.4f), yardAt + new Vector3(0.6f, 0.4f, -0.6f));
            layer.RadioButton.onClick.Invoke();
            Assert.IsTrue(layer.Guide.BreezeActive, "55_hint_breeze: tier 1 should nudge");
            Assert.AreSame(yard, layer.Guide.BreezeArea, "55_hint_breeze: toward the side yard");
            yield return Wait(1.9f);
            Assert.Greater(layer.Guide.VisibleLeaves, 0, "55_hint_breeze: leaves should be in the air");
            Assert.IsTrue(layer.Guide.GlowVisible, "55_hint_breeze: the area should glow");
            yield return Shot("55_hint_breeze");

            // 52: tier 2 from the same spot: the paper bats fly to the side yard and circle it.
            layer.RadioButton.onClick.Invoke();
            director.Captions.Clear();
            Assert.IsTrue(layer.Guide.Active, "56_hint_bats: tier 2 should start the trail");
            Assert.AreSame(yard, layer.Guide.TrailArea, "56_hint_bats: to the side yard");
            yield return Wait(2.6f);
            Assert.Greater(layer.Guide.CirclingBats, 0, "56_hint_bats: bats should be circling the area");
            yield return Shot("56_hint_bats");
            layer.Guide.Stop();
            Release(brain);

            // 34: by the crate on the skilled ascent, tier 3 on touch: the ghost hand taps the Jump disc (a move, never Use).
            var crate = CaseRouteLayout.SkilledAscent[1];
            var stand = CaseRouteLayout.SkilledAscent[0];
            yield return Stand(new Vector3(stand.X, 0.1f, stand.Z), new Vector3(crate.X, crate.Y, crate.Z), 1.2f);
            ControlSchemeTracker.Override = ControlScheme.Touch;
            Assert.AreEqual(HintTier.Action, director.NextHintTier, "34_hint_ghost_hand: a move blocks here");
            layer.RadioButton.onClick.Invoke();
            Assert.IsTrue(layer.Demo.Active, "34_hint_ghost_hand: tier 3 should demonstrate");
            Assert.AreEqual(HintMove.Jump, layer.Demo.Demo, "34_hint_ghost_hand: the hop onto the crate");
            yield return Wait(0.55f); // the finger is down on the Jump disc
            Assert.IsTrue(layer.Demo.HandVisible, "34_hint_ghost_hand: the hand should be up");
            yield return Shot("34_hint_ghost_hand", game.Ui.GetComponentInParent<Canvas>());

            // 36: the same with a keyboard: no hand, the Space key cap pulses above the Jump control.
            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            layer.RadioButton.onClick.Invoke();
            director.Captions.Clear();
            yield return Wait(0.8f);
            Assert.IsTrue(layer.Demo.GlyphVisible, "36_hint_key_glyph: the key glyph should be up");
            yield return Shot("36_hint_key_glyph", game.Ui.GetComponentInParent<Canvas>());
            ControlSchemeTracker.Override = null;
            layer.Demo.Stop();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureBeasHazyMemory()
        {
            yield return Load();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            var replay = director.Nonverbal.MemoryReplay;

            // Bea speaks; her sticker's eye plays the memory on demand (nothing plays on its own).
            director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return Wait(0.4f);
            director.Dialogue.Skip();
            yield return Wait(0.6f);
            Assert.IsFalse(replay.Active, "57_replay_blur: no replay on its own");
            director.OpenNotebook();
            yield return Wait(0.3f);
            var notebook = director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(CaseNotebook.Question(CaseNotebook.QuestionEntry)));
            yield return Wait(0.3f);
            var eye = notebook.TraySticker(CaseNotebook.BeaFlash).transform.Find("WatchAgain");
            Assert.IsNotNull(eye, "57_replay_blur: Bea's sticker offers her memory");
            eye.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            float timeout = Time.time + 4f;
            while (!replay.StreakVisible && Time.time < timeout)
            {
                yield return null;
            }

            yield return Wait(0.15f); // mid-streak, echoes trailing
            Assert.IsTrue(replay.Active, "57_replay_blur: the memory plays");
            Assert.IsTrue(replay.StreakVisible, "57_replay_blur: the silver streak should be crossing");
            Assert.IsFalse(replay.Ghost.gameObject.activeSelf, "57_replay_blur: no June");
            yield return Shot("57_replay_blur");
        }
    }
}
