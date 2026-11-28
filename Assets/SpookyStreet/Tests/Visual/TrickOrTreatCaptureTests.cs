using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.LivingStreet;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// Trick-or-treating on the living street (owner, 2026-11-28; docs/living-street-slice.md §6.2), in the running game:
    /// 111 the hall light on and the neighbour's shadow crossing the lit window between rings, 112 a door mid-way open on
    /// its hinge curve, 113 a neighbour handing a treat into a child's pail at the release, 114 a family waiting its turn a
    /// few metres back while the door is busy, 115 Pip handing a treat at her porch, 116a–c a family walking round the
    /// lane's east corner together and 116d the strollers stopping to admire a porch's jack-o'-lanterns. Each moment is
    /// held still (game time stopped) while it is rendered, and the log says what it shows.
    /// </summary>
    [Category("Visual")]
    public class TrickOrTreatCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

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

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static LivingStreetStage Stage => Director.Living;

        private static IEnumerator Shot(string name)
        {
            var game = GameBootstrap.Instance;
            float scale = Time.timeScale;
            Time.timeScale = 0f; // the moment holds still while it renders
            Director.Captions.Clear();
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, Director.UiCanvas, name, Width, Height);
            Time.timeScale = scale;
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                yield return null;
            }
        }

        /// <summary>Runs the game (sped up by <paramref name="speed"/>) until <paramref name="ready"/>; then normal speed.</summary>
        private static IEnumerator Until(System.Func<bool> ready, float realSeconds, float speed = 1f)
        {
            Time.timeScale = speed;
            float until = Time.realtimeSinceStartup + realSeconds;
            while (!ready() && Time.realtimeSinceStartup < until)
            {
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                yield return null;
            }

            Time.timeScale = 1f;
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            if (Director.Cutscenes != null && Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
            }

            yield return Wait(0.3f);
            GameBootstrap.Instance.Ui.FirstPrompt.SetActive(false);
        }

        private static void Frame(Vector3 eye, Vector3 at)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = eye;
            camera.transform.LookAt(at);
        }

        private static void PlacePip(Vector3 at, Vector3 lookAt)
        {
            var pip = GameBootstrap.Instance.Pip;
            pip.Teleport(at);
            Vector3 to = lookAt - at;
            to.y = 0f;
            pip.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        private static LivingStreetSet.DoorRig Rig(string id)
        {
            foreach (var rig in Stage.Set.Doors)
            {
                if (rig.Door.Id == id)
                {
                    return rig;
                }
            }

            return null;
        }

        private static Vector3 World(NeighbourDoor door, float outward, float across, float y)
        {
            DoorStoops.Point(door, outward, across, out float x, out float z);
            return new Vector3(x, y, z);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CapturesTrickOrTreating()
        {
            yield return StartGame();
            Assert.IsNotNull(Stage);
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            var rig = Rig(fogg.Id);

            // 111: Pip has rung Mr. Fogg's bell twice: the hall light is on and his shadow crosses the lit window.
            PlacePip(new Vector3(fogg.StandX + 0.3f, 0.1f, fogg.StandZ - 0.4f), new Vector3(fogg.DoorX, 1f, fogg.DoorZ));
            yield return Until(() => Stage.Doors.CanRing(fogg.Id, Ringer.Pip), 40f, 4f);
            Assert.IsTrue(Stage.Ring(fogg), "ring 1");
            yield return Wait(NeighbourDoors.RingBeat + 0.1f);
            Assert.IsTrue(Stage.Ring(fogg), "ring 2");
            // Taken while the shadow is in the window's right half, clear of the paper ghost in the middle.
            yield return Until(() => Stage.Doors.Silhouette(fogg.Id) >= 0.72f, 4f);
            DoorStoops.HallWindow(fogg, out float window, out _, out _, out _, out _, out _);
            var windowAt = rig.Hinge.parent.TransformPoint(new Vector3((window + fogg.DoorLocalX) * 0.5f, 1.6f, fogg.WallLocalZ));
            Frame(World(fogg, 5.6f, -2.4f, 2.0f), windowAt);
            Debug.Log($"[TrickOrTreatCaptureTests] 111: hall light {Stage.Doors.HallLight(fogg.Id):F2} (lamp on {rig.HallLight.enabled}), " +
                      $"window cover {rig.WindowCover.enabled}, shadow at {Stage.Doors.Silhouette(fogg.Id):F2}, shown {rig.Silhouette.enabled}");
            Assert.IsTrue(rig.Silhouette.enabled, "the shadow is in the window");
            var card = rig.House != null ? SpookyStreet.World.ModelKit.FindDeep(rig.House, SpookyStreet.Core.World.LaneHouseArt.RoomGlow) : null;
            var cardRenderer = card != null ? card.GetComponent<Renderer>() : null;
            Debug.Log($"[TrickOrTreatCaptureTests] 111: shadow bounds {rig.Silhouette.bounds}, cover bounds {rig.WindowCover.bounds}, " +
                      $"window centre {rig.Hinge.parent.TransformPoint(new Vector3(window, 1.75f, -2.75f))}, room cards " +
                      $"{(cardRenderer != null ? cardRenderer.bounds.ToString() : "none")}, house at {(rig.House != null ? rig.House.position + " yaw " + rig.House.eulerAngles.y : "none")}, " +
                      $"door frame yaw {rig.Hinge.parent.eulerAngles.y}");
            yield return Shot("111_doorbell_hall_light_shadow");

            // 112: the third ring: his hand on the door's edge, the leaf mid-way on its hinge curve.
            yield return Until(() => Stage.Doors.CanRing(fogg.Id, Ringer.Pip), 4f);
            Assert.IsTrue(Stage.Ring(fogg), "ring 3 opens the door");
            yield return Until(() => Stage.Doors.SwingDegrees(fogg.Id) >= fogg.Gag.OpenDegrees * 0.45f, 4f);
            Frame(World(fogg, 3.6f, 2.2f, 1.8f), World(fogg, 0.2f, -0.1f, 1.3f));
            Debug.Log($"[TrickOrTreatCaptureTests] 112: swing {Stage.Doors.SwingDegrees(fogg.Id):F0} of {fogg.Gag.OpenDegrees:F0} deg, " +
                      $"phase {Stage.Doors.Phase(fogg.Id)}");
            yield return Shot("112_door_mid_open");
            yield return Until(() => Stage.Doors.Phase(fogg.Id) == DoorPhase.Closed, 20f);

            // 113: a family at a door: the neighbour lets a sweet go over a child's pail.
            PlacePip(new Vector3(-12f, 0.1f, -8f), Vector3.zero); // out of the way
            string door113 = null;
            yield return Until(() =>
            {
                foreach (var d in LivingStreetLayout.Doors)
                {
                    if (Stage.Doors.Holder(d.Id) >= 0 && Stage.Doors.Phase(d.Id) == DoorPhase.Giving)
                    {
                        door113 = d.Id;
                        return true;
                    }
                }

                return false;
            }, 90f, 6f);
            Assert.IsNotNull(door113, "a family visits a door");
            var d113 = LivingStreetLayout.Door(door113);
            yield return Until(() =>
                Stage.Doors.Figure(door113, 0, out _, out _, out _, out _, out _, out var clip, out float t, out _) &&
                clip == SpookyStreet.Core.LivingStreet.WalkerClip.GiveTreat && t >= WalkerClips.GiveRelease + 0.03f, 25f);
            Stage.Doors.Figure(door113, 0, out float gx, out float gz, out _, out _, out _, out var gclip, out float gt, out _);
            Frame(World(d113, DoorStoops.TreatOut(d113) + 0.4f, 2.3f, 1.25f), new Vector3(gx, 0.75f, gz) +
                  (World(d113, DoorStoops.ReceiveOut(d113), 0f, 0f) - new Vector3(gx, 0f, gz)) * 0.5f);
            Debug.Log($"[TrickOrTreatCaptureTests] 113: {door113}, giver clip {gclip} at {gt:F2} s, sweets shown {Stage.SweetsShown}, " +
                      $"giver has GiveTreat {Rig(door113).Figures[0].HasClip(SpookyStreet.Core.Crowd.CrowdClipTimings.GiveTreat)}");
            yield return Shot("113_neighbour_treat_into_pail");

            // 114: Pip holds Mr. Fogg's door (one ring) and a family waits its turn a few metres back on the sidewalk.
            PlacePip(new Vector3(fogg.StandX + 0.3f, 0.1f, fogg.StandZ - 0.4f), new Vector3(fogg.DoorX, 1f, fogg.DoorZ));
            yield return Until(() => Stage.Doors.CanRing(fogg.Id, Ringer.Pip) && Stage.Doors.Phase(fogg.Id) == DoorPhase.Closed, 60f, 6f);
            Assert.IsTrue(Stage.Ring(fogg));
            int queued = -1;
            yield return Until(() =>
            {
                for (int g = 0; g < Stage.Crowd.Clock.GroupCount; g++)
                {
                    if (Stage.Crowd.Clock.IsQueued(g) && Stage.Crowd.Clock.DoorOf(g) == fogg.Id)
                    {
                        queued = g;
                    }
                }

                return queued >= 0;
            }, 120f, 6f);
            Assert.GreaterOrEqual(queued, 0, "a family waits its turn");
            yield return Wait(1.0f);
            var waiting = Stage.Crowd.Bodies[Stage.Crowd.Clock.FirstWalker(queued)].Root.position;
            // From the sidewalk beyond Pip, looking back past her at the family waiting a few metres back.
            var pipAt = new Vector3(fogg.StandX, 0.6f, fogg.StandZ);
            var along = waiting - pipAt;
            along.y = 0f;
            Frame(pipAt - along.normalized * 3.2f + new Vector3(0f, 1.7f, -1.6f), Vector3.Lerp(pipAt, waiting, 0.55f) + Vector3.up * 0.3f);
            Debug.Log($"[TrickOrTreatCaptureTests] 114: family {queued} waits at ({waiting.x:F1}, {waiting.z:F1}), Pip at the stand");
            yield return Shot("114_group_waits_turn");
            yield return Wait(NeighbourDoors.RingBeat);
            Stage.Ring(fogg);
            yield return Wait(NeighbourDoors.RingBeat + 0.1f);
            Stage.Ring(fogg);
            yield return Until(() => Stage.Doors.Phase(fogg.Id) == DoorPhase.Closed, 20f);

            // 115: Pip's lantern hangs; a family rings her bell and she hands a treat to each child.
            Stage.MakeLantern(new PipLantern(LanternShape.Bat, LanternColour.Orange));
            PlacePip(new Vector3(0.35f, 0.1f, -12.35f), new Vector3(0.35f, 0f, -10f));
            yield return Until(() => Stage.Crowd.Clock.WaitingAtPorch >= 0, 150f, 6f);
            Assert.GreaterOrEqual(Stage.Crowd.Clock.WaitingAtPorch, 0, "a family came to Pip's bell");
            bool handing = false;
            System.Action<string> watch = what => handing |= what.StartsWith("Pip hands a treat", System.StringComparison.Ordinal);
            Stage.Happened += watch;
            Assert.IsTrue(Stage.GiveTreat());
            yield return Until(() => handing, 10f);
            Stage.Happened -= watch;
            yield return Wait(WalkerClips.HandRelease + 0.04f);
            Frame(new Vector3(2.6f, 1.4f, -10.6f), new Vector3(0.35f, 0.75f, -11.9f));
            Debug.Log($"[TrickOrTreatCaptureTests] 115: sweets shown {Stage.SweetsShown}, treats given {Stage.TreatsGiven}");
            yield return Shot("115_pip_hands_treat_porch");
            yield return Wait(8f);

            // 116a-c: a family turns the lane's east corner together (three moments 0.5 s apart), on a curve.
            PlacePip(new Vector3(-12f, 0.1f, -8f), Vector3.zero);
            yield return Until(() =>
            {
                var p = Stage.Crowd.Bodies[0].Root.position;
                return !Stage.Crowd.Clock.IsStopped(0) && Mathf.Abs(p.x - (CrowdRoutes.EastTurnX - 1.6f)) < 0.3f && p.z > 1.2f;
            }, 150f, 6f);
            Frame(new Vector3(CrowdRoutes.EastTurnX - 3.4f, 2.4f, -4.6f), new Vector3(CrowdRoutes.EastTurnX - 0.4f, 0.7f, 0.2f));
            foreach (string letter in new[] { "a", "b", "c" })
            {
                var lead = Stage.Crowd.Bodies[0].Root;
                Debug.Log($"[TrickOrTreatCaptureTests] 116{letter}: lead child at ({lead.position.x:F2}, {lead.position.z:F2}) facing {lead.eulerAngles.y:F0}");
                yield return Shot("116" + letter + "_family_turns_corner");
                yield return Wait(0.5f);
            }

            // 116d: the strollers stop to admire Mr. Fogg's stacked jack-o'-lanterns (the grown-up chats, the child points).
            int strollers = Stage.Crowd.Clock.GroupCount - 1;
            yield return Until(() =>
            {
                var p = Stage.Crowd.Bodies[Stage.Crowd.Clock.FirstWalker(strollers)].Root.position;
                return Stage.Crowd.Clock.IsStopped(strollers) && Mathf.Abs(p.x - 31.2f) < 1.5f && Mathf.Abs(p.z - 0.4f) < 1.5f;
            }, 150f, 6f);
            yield return Wait(1.0f);
            var admire = Stage.Crowd.Bodies[Stage.Crowd.Clock.FirstWalker(strollers)].Root.position;
            // From behind and beside them, looking where they look (the porch they admire is in the frame).
            var gaze = Stage.Crowd.Bodies[Stage.Crowd.Clock.FirstWalker(strollers)].Root.forward;
            gaze.y = 0f;
            gaze.Normalize();
            var beside = new Vector3(gaze.z, 0f, -gaze.x);
            Frame(admire - gaze * 3.2f + beside * 1.6f + Vector3.up * 1.6f, admire + gaze * 3.5f + Vector3.up * 0.9f);
            Debug.Log($"[TrickOrTreatCaptureTests] 116d: strollers stopped at ({admire.x:F1}, {admire.z:F1})");
            yield return Shot("116d_admire_decoration");
        }
    }
}
