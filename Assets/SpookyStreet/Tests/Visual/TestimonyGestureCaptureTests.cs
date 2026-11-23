using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Characters;
using SpookyStreet.Controls;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Save;
using SpookyStreet.Player;
using SpookyStreet.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S003a pass frames (docs/S003a-pass.md): 65 Bea shading her eyes on her testimony line, 66 Mr. Vale checking his
    /// watch on "Not one creak from that door all evening", 67 Pip's hint glance as the tier-1 breeze leaves her, and 68
    /// Bea's street greeting vignette after case 01 ("Pip's back! See my owl?"). Each main frame has the game's UI; the
    /// review frames (<c>r_*</c>) show the same frozen moment without it, closer. Game time is held while a frame
    /// renders, so a clip is caught on its pose.
    /// </summary>
    [Category("Visual")]
    public class TestimonyGestureCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const int ReviewWidth = 640;
        private const int ReviewHeight = 296;
        private const float MaxMagenta = 0.005f;

        private string _directory;
        private Canvas _blank;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _directory = CaseDirector.UseTemporarySaveDirectory();
            ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        /// <summary>A capture with game time held (several slow batchmode frames would carry the clip past its pose).</summary>
        private static IEnumerator Shot(string name, Canvas canvas, int width = Width, int height = Height)
        {
            var game = GameBootstrap.Instance;
            float scale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas, name, width, height);
            }
            finally
            {
                Time.timeScale = scale;
            }

            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        /// <summary>A canvas with nothing on it, for the review frames without the game's UI.</summary>
        private Canvas Blank()
        {
            if (_blank == null)
            {
                _blank = new GameObject("BlankCaptureCanvas").AddComponent<Canvas>();
            }

            return _blank;
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Load(bool newGame)
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            if (newGame)
            {
                GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
                yield return Wait(1.0f);
            }
        }

        /// <summary>Holds the camera at <paramref name="eye"/> looking at <paramref name="look"/> (the brain off).</summary>
        private static void Frame(Vector3 eye, Vector3 look)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = eye;
            camera.transform.LookAt(look);
        }

        private static void Release()
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = true;
            }
        }

        private static void Stand(Vector3 at, Vector3 look)
        {
            var game = GameBootstrap.Instance;
            game.Pip.Teleport(at + Vector3.up * 0.05f);
            Vector3 to = look - at;
            to.y = 0f;
            game.Pip.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            game.Ui.FirstPrompt.SetActive(false);
            game.CameraRig.Orbit.RequestRecenter();
        }

        private static IEnumerator CloseTalk()
        {
            Director.Dialogue.Skip();
            yield return Wait(0.3f);
            if (Director.Nonverbal.MemoryReplay.Active)
            {
                Director.Nonverbal.MemoryReplay.End(false);
            }

            yield return Wait(0.3f);
            Director.Captions.Clear();
        }

        /// <summary>A three-quarter view of a witness from in front of where they face now (they may have turned).</summary>
        private static void FrameWitness(Witness witness, float distance, float side, float eyeHeight, float lookHeight)
        {
            Transform t = witness.transform;
            Vector3 eye = t.position + Quaternion.Euler(0f, side, 0f) * t.forward * distance + Vector3.up * eyeHeight;
            Frame(eye, t.position + Vector3.up * lookHeight);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CapturesTheTestimonyGesturesAndThePipGlance()
        {
            yield return Load(true);
            var game = GameBootstrap.Instance;
            var layer = Director.Nonverbal;
            var bea = game.Witnesses.Bea;
            var vale = game.Witnesses.Vale;
            Assert.IsNotNull(bea);
            Assert.IsNotNull(vale);
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            Director.Captions.Clear();

            // 65: Bea's first account, "Something silver flashed up high, by the side of the house!": her hand over her
            // eyes, turned toward the side of the house (gesture 1.0 s in: hand on the brow, head back).
            var beaAt = bea.transform.position;
            Stand(beaAt + new Vector3(2.2f, 0f, -0.4f), beaAt);
            yield return Wait(1.2f);
            Director.Perform(CaseAction.Talk(Speaker.Bea));
            Assert.AreEqual(Lines.BeaSawCape, Director.Dialogue.CurrentLine.Key);
            yield return Wait(0.3f + 1.0f);
            Assert.AreEqual(CharacterClips.ShadeEyes, layer.LastTestimonyGesture, "65: Bea shades her eyes");
            Assert.IsTrue(bea.Gesturing, "65: mid-gesture");
            FrameWitness(bea, 3.4f, 40f, 1.5f, 0.55f);
            yield return Shot("65_bea_shade_eyes", Director.UiCanvas);
            FrameWitness(bea, 2.2f, 40f, 1.25f, 1.0f);
            yield return Shot("r_bea_shade_eyes_close", Blank(), ReviewWidth, ReviewHeight);
            FrameWitness(bea, 2.6f, -50f, 1.2f, 1.0f);
            yield return Shot("r_bea_shade_eyes_side", Blank(), ReviewWidth, ReviewHeight);
            Release();
            yield return CloseTalk();

            // 66: Mr. Vale, second talk, "Not one creak from that door all evening.": seated, he shows the listener his
            // watch (gesture 1.5 s in: the dial rolled toward Pip).
            var valeAt = vale.transform.position;
            // Pip on his other side from the camera, so she does not cover him.
            Stand(valeAt + vale.transform.forward * 1.8f - vale.transform.right * 1.1f, valeAt);
            yield return Wait(1.0f);
            Director.Perform(CaseAction.Talk(Speaker.Vale));
            yield return CloseTalk();
            yield return Wait(0.5f);
            Director.Perform(CaseAction.Talk(Speaker.Vale));
            Assert.AreEqual(Lines.ValeRepeat, Director.Dialogue.CurrentLine.Key);
            yield return Wait(0.6f + 1.0f);
            Assert.AreEqual(CharacterClips.CheckWatch, layer.LastTestimonyGesture, "66: Vale checks his watch");
            Assert.IsTrue(vale.Gesturing, "66: mid-gesture");
            FrameWitness(vale, 3.2f, 30f, 1.45f, 0.45f);
            yield return Shot("66_vale_check_watch", Director.UiCanvas);
            FrameWitness(vale, 1.7f, 25f, 1.15f, 0.85f);
            yield return Shot("r_vale_check_watch_close", Blank(), ReviewWidth, ReviewHeight);
            Release();
            yield return CloseTalk();

            // 67: the bench is found, so the hint leads to the side yard. Pip on the west sidewalk facing east; the radio's
            // first press: the breeze leaves her and she glances left, toward the side yard (glance 0.55 s in: peering).
            Director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            yield return Wait(0.3f);
            Director.Bench.End();
            Director.Captions.Clear();
            var pipAt = new Vector3(-8.9f, 0.1f, 1.4f);
            Stand(pipAt, pipAt + Vector3.right * 4f);
            yield return Wait(1.4f);
            var animator = game.Pip.GetComponent<PipAvatar>().Animator;
            layer.RadioButton.onClick.Invoke();
            Assert.IsTrue(layer.Guide.BreezeActive, "67: tier 1 nudges");
            if (animator != null)
            {
                Assert.AreEqual(PipClips.GlanceLeft, layer.Guide.PipGlance, "67: she glances toward the side yard");
            }

            yield return Wait(0.55f);
            var pip = game.Pip.transform;
            Vector3 left = -pip.right;
            Frame(pip.position + pip.forward * 2.3f + left * 2.0f + Vector3.up * 1.25f,
                pip.position + Vector3.up * 0.75f + left * 0.9f);
            yield return Shot("67_pip_hint_glance", Director.UiCanvas);
            Frame(pip.position - pip.forward * 3.4f - left * 1.4f + Vector3.up * 2.4f,
                pip.position + left * 2.6f + Vector3.up * 0.5f);
            yield return Shot("r_pip_hint_glance_wide", Blank(), ReviewWidth, ReviewHeight);
            Frame(pip.position + pip.forward * 1.3f + left * 1.1f + Vector3.up * 1.0f, pip.position + Vector3.up * 0.75f);
            yield return Shot("r_pip_hint_glance_close", Blank(), ReviewWidth, ReviewHeight);
            Release();
        }

        private static CaseAction A(CaseActionKind kind) => CaseAction.Of(kind);

        private static CaseAction[] CanonicalActions() => new[]
        {
            A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
            A(CaseActionKind.SitOnBench), A(CaseActionKind.ExamineCloth), A(CaseActionKind.EnterAttic),
            A(CaseActionKind.ExamineSill), CaseAction.Solve(N.QuestionEntry, new[] { N.ValeDoor, N.Cloth, N.SillSnag }),
            CaseAction.Talk(Speaker.June), A(CaseActionKind.EnterFoyer), A(CaseActionKind.LiftLatch),
            A(CaseActionKind.ExamineGrille), CaseAction.Solve(N.QuestionSource, new[] { N.FloorHum, N.WoodenPiece }),
            A(CaseActionKind.LinkArm), A(CaseActionKind.TurnCrank),
            CaseAction.Solve(N.QuestionMoved, new[] { N.LooseArm, N.OwlBooth }), A(CaseActionKind.ArriveAtPorch),
        };

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CapturesAStreetGreetingVignette()
        {
            // A closed case 01 save, then Continue: the street remembers, and Bea greets Pip with her changed line.
            var state = CaseState.Empty;
            foreach (var action in CanonicalActions())
            {
                state = state.Apply(action).State;
            }

            var store = new CaseSaveStore(new DiskSaveFileSystem(), _directory);
            store.Load();
            Assert.IsTrue(store.Save(state, CaseAnchors.ArrivalPorch));
            yield return Load(false);
            GameBootstrap.Instance.Ui.ContinueButton.onClick.Invoke();
            yield return Wait(1.5f);

            var game = GameBootstrap.Instance;
            var bea = game.Witnesses.Bea;
            var beaAt = bea.transform.position;
            var pipAt = beaAt + new Vector3(2.0f, 0f, -0.9f);
            Stand(pipAt, beaAt);
            yield return Wait(1.2f);
            Director.Perform(CaseAction.Talk(Speaker.Bea));
            var dialogue = Director.Dialogue;
            Assert.AreEqual(Lines.BeaRemembers, dialogue.CurrentLine.Key, "68: the changed greeting");
            Assert.IsTrue(dialogue.ShowsVignette, "68: with its vignette");
            Assert.AreEqual(C01Vignettes.For(Lines.BeaRemembers).VignetteId, dialogue.Vignette.Current.Id);
            Assert.AreEqual(C01Vignettes.BeaRemembers, dialogue.Vignette.Current.Id);

            // Over Pip's shoulder toward Bea and her bike, when the eye has opened over the bike (2.45 s).
            var pip = game.Pip.transform;
            Frame(pip.position - pip.forward * 1.6f + pip.right * 0.9f + Vector3.up * 1.5f, beaAt + Vector3.up * 0.7f);
            float timeout = Time.realtimeSinceStartup + 20f;
            while (dialogue.Vignette.LocalTime < 2.45f && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            var run = dialogue.Vignette.CanRun;
            dialogue.Vignette.CanRun = () => false;
            yield return Shot("68_street_greeting_bea", Director.UiCanvas);
            dialogue.Vignette.CanRun = run;
            Release();
            dialogue.Skip();
        }
    }
}
