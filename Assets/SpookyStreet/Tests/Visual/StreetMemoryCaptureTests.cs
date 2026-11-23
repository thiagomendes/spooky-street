using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Save;
using SpookyStreet.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S002.5b "the street remembers" and the Continue recap: 62 Kit's booth open and lit after case 01, 63 June
    /// outside by her treehouse way, 64 the title's Continue with the picture recap of a mid-case save. Review frames:
    /// the recap of a closed save, Bea's owl sticker close up, and the same street during a replay (memories resting).
    /// </summary>
    [Category("Visual")]
    public class StreetMemoryCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const int ReviewWidth = 640;
        private const int ReviewHeight = 296;
        private const float MaxMagenta = 0.005f;

        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _directory = CaseDirector.UseTemporarySaveDirectory();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator Shot(string name, Canvas canvas, int width = Width, int height = Height)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas, name, width, height);
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

        private void WriteCaseSave(int actions, string anchor)
        {
            var state = CaseState.Empty;
            var all = CanonicalActions();
            for (int i = 0; i < actions && i < all.Length; i++)
            {
                state = state.Apply(all[i]).State;
            }

            var store = new CaseSaveStore(new DiskSaveFileSystem(), _directory);
            store.Load();
            Assert.IsTrue(store.Save(state, anchor));
        }

        /// <summary>Takes the lens off the follow camera and frames <paramref name="at"/> from <paramref name="eye"/>.</summary>
        private static Behaviour Frame(Vector3 eye, Vector3 at)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = eye;
            camera.transform.LookAt(at);
            return brain;
        }

        private static void PlacePip(Vector3 at, Vector3 lookAt)
        {
            var pip = GameBootstrap.Instance.Pip;
            pip.Teleport(at);
            Vector3 to = lookAt - at;
            to.y = 0f;
            pip.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CapturesTheRememberingStreetAndTheContinueRecap()
        {
            // 64: the title with a mid-case save (the attic reached, the entry question open).
            WriteCaseSave(7, CaseAnchors.Attic);
            yield return Load();
            Assert.IsTrue(Director.Recap.Visible);
            var titleCanvas = GameBootstrap.Instance.Ui.GetComponent<Canvas>();
            yield return Shot("64_continue_recap", titleCanvas);

            // The recap of a closed save, then the street that remembers.
            WriteCaseSave(CanonicalActions().Length, CaseAnchors.ArrivalPorch);
            yield return Load();
            Assert.IsTrue(Director.Recap.Stamp.activeSelf);
            yield return Shot("r_continue_recap_closed", GameBootstrap.Instance.Ui.GetComponent<Canvas>(), ReviewWidth, ReviewHeight);
            GameBootstrap.Instance.Ui.ContinueButton.onClick.Invoke();
            yield return Wait(1.5f); // the wind turns the owl now and then; let the street settle

            // 62: Kit's booth open and lit, the door open, the owl on the roof; Pip on the path looking up at it.
            var kit = CaseStage.KitBooth.Position;
            var booth = new Vector3(kit.X, kit.Y + 1.2f, kit.Z);
            PlacePip(new Vector3(0.9f, 0.05f, 6.2f), booth);
            yield return Wait(0.4f);
            var brain = Frame(new Vector3(3.6f, 2.0f, 3.4f), new Vector3(0.3f, 2.6f, kit.Z));
            yield return Shot("62_street_booth_open", Director.UiCanvas);

            // 63: June outside by the foot of her treehouse way, the side yard hedges behind her.
            var june = CaseStage.JuneTreehouse.Position;
            var juneAt = new Vector3(june.X, june.Y, june.Z);
            PlacePip(juneAt + new Vector3(3.0f, 0.05f, -1.6f), juneAt);
            if (brain != null)
            {
                brain.enabled = true;
            }

            yield return Wait(0.4f);
            Frame(juneAt + new Vector3(5.4f, 1.7f, -3.6f), juneAt + new Vector3(-0.6f, 1.0f, 0.4f));
            yield return Shot("63_street_june_outside", Director.UiCanvas);

            // Review: Bea's owl sticker on her basket, from the plaza side.
            var sticker = Director.Street.BeaSticker;
            if (sticker != null)
            {
                Vector3 s = sticker.transform.position;
                Frame(s - sticker.transform.forward * 1.3f + new Vector3(0f, 0.35f, 0f) + sticker.transform.right * 0.3f, s);
                yield return Shot("r_bea_sticker", Director.UiCanvas, ReviewWidth, ReviewHeight);
                Frame(s - sticker.transform.forward * 3.4f + new Vector3(0f, 1.1f, 0f), s + new Vector3(0f, 0.2f, 0f));
                yield return Shot("r_bea_bike", Director.UiCanvas, ReviewWidth, ReviewHeight);
            }

            // Review: the same booth shot during a replay (case 01's memories rest until its stamp).
            Director.Replay();
            yield return Wait(0.5f);
            Frame(new Vector3(3.6f, 2.0f, 3.4f), new Vector3(0.3f, 2.6f, kit.Z));
            yield return Shot("r_replay_booth_resting", Director.UiCanvas, ReviewWidth, ReviewHeight);
            if (brain != null)
            {
                brain.enabled = true;
            }
        }
    }
}
