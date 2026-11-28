using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;
using SpookyStreet.LivingStreet;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// The east lane's three porch houses as the game builds them (docs/living-street-lane-houses.md): nothing is staged,
    /// the slice spawns the houses, leaves, lamps and porch lights itself, and the neighbours are the crowd kit's
    /// (<c>CrowdKit.Neighbours</c>). 102_lane_houses_ingame: the gameplay camera behind Pip at the hedge gap, looking
    /// down the lane. 103_lane_houses_back_ingame: from the Green's corner looking back west. 104_fogg_door_ingame,
    /// 105_pearl_door_ingame, 106_abbotts_door_ingame: Pip rings each bell with the gameplay camera behind her and the
    /// door answers with its gag. 107_fogg_wave_ingame: a group rings Mr. Fogg's bell and he waves; the frame is taken
    /// near the wave's highest point, and the log says whether his hand cleared the door head and the hood.
    /// (Frames 98 to 101 were the art pass's staged swap of the same houses.)
    /// </summary>
    [Category("Visual")]
    public class LaneHouseArtCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        // Mr. Fogg's door head, in the house's frame from the door's centre (tools/blender/env/lane_houses.py: the casing
        // head stands 0.13 m proud up to 2.75 m; the hood is 0.87 m deep, its trim's lowest edge at 2.70 m, ±1.08 m wide).
        private const float CasingOut = 0.13f;
        private const float CasingBottom = 2.56f;
        private const float CasingTop = 2.75f;
        private const float CasingHalf = 0.77f;
        private const float HoodOut = 0.87f;
        private const float HoodBottom = 2.70f;
        private const float HoodTop = 3.44f;
        private const float HoodHalf = 1.08f;
        private const float HandTop = 0.1f; // the hand above its wrist bone

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

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Shot(string name, bool keepCaptions = false)
        {
            var game = GameBootstrap.Instance;
            if (!keepCaptions)
            {
                Director.Captions.Clear();
            }

            Time.timeScale = 0f;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, Director.UiCanvas, name, Width, Height);
            Time.timeScale = 1f;
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static void Face(Vector3 at, Vector3 lookAt)
        {
            var game = GameBootstrap.Instance;
            game.Pip.Teleport(at);
            Vector3 to = lookAt - at;
            to.y = 0f;
            game.Pip.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
        }

        private static IEnumerator FaceDoor(NeighbourDoor door)
        {
            Vector3 stand = new Vector3(door.StandX, 0.1f, door.StandZ);
            Vector3 doorAt = new Vector3(door.DoorX, 0f, door.DoorZ);
            Face(stand + (stand - doorAt).normalized * 0.25f, doorAt);
            yield return Wait(1.6f);
        }

        /// <summary>True when the camera's line to the top of Mr. Fogg's hand passes through his door head or his hood.</summary>
        private static bool HandHidden(Transform frame, NeighbourDoor door, Vector3 handWorld, Vector3 eyeWorld)
        {
            Vector3 hand = frame.InverseTransformPoint(handWorld + Vector3.up * HandTop);
            Vector3 eye = frame.InverseTransformPoint(eyeWorld);
            return Blocks(hand, eye, door, CasingOut, CasingBottom, CasingTop, CasingHalf)
                   || Blocks(hand, eye, door, HoodOut, HoodBottom, HoodTop, HoodHalf);
        }

        private static bool Blocks(Vector3 hand, Vector3 eye, NeighbourDoor door, float outFromWall, float bottom, float top, float half)
        {
            float plane = door.WallLocalZ - outFromWall;
            if (hand.z <= plane || eye.z >= plane)
            {
                return false; // the hand is already in front of this piece
            }

            float t = (plane - hand.z) / (eye.z - hand.z);
            Vector3 at = hand + (eye - hand) * t;
            return at.y > bottom && at.y < top && Mathf.Abs(at.x - door.DoorLocalX) < half;
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CaptureTheLaneHousesInTheGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            if (!game.Environment.UsesModels || Resources.Load<GameObject>(LaneHouseArt.Folder + "/" + LaneHouseArt.House(LaneHouseArt.Fogg)) == null)
            {
                Assert.Ignore("Environment or lane house models are not committed.");
            }

            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);
            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
                yield return Wait(0.2f);
            }

            game.Ui.FirstPrompt.SetActive(false);
            var stage = Director.Living;
            Assert.IsNotNull(stage, "the living street slice");
            foreach (var rig in stage.Set.Doors)
            {
                Assert.IsNotNull(rig.House, rig.Door.Id + ": the lane house is the game's own");
                Assert.AreEqual(LaneHouseArt.House(rig.Door.Id), rig.House.name);
                Assert.AreEqual(LaneHouseArt.DoorLeaf(rig.Door.Id), rig.Leaf.name);
                Assert.AreEqual(LaneHouseArt.LampGlow, rig.Lamp.name);
                foreach (var body in rig.Figures)
                {
                    Assert.IsTrue(body.IsKit, rig.Door.Id + ": the neighbour is a crowd kit body");
                }
            }

            // 102: down the lane from the hedge gap, the gameplay camera behind Pip.
            Face(new Vector3(23.4f, 0.1f, -0.6f), new Vector3(34f, 0f, 1.5f));
            yield return Wait(2.0f);
            yield return Shot("102_lane_houses_ingame");

            // 103: from the Green's corner, looking back west along the lane.
            Face(new Vector3(43.2f, 0.1f, 0.4f), new Vector3(30f, 0f, 0.6f));
            yield return Wait(2.0f);
            yield return Shot("103_lane_houses_back_ingame");

            string[] names = { "104_fogg_door_ingame", "105_pearl_door_ingame", "106_abbotts_door_ingame" };
            string[] ids = { LivingStreetLayout.Fogg, LivingStreetLayout.Pearl, LivingStreetLayout.Abbotts };
            for (int i = 0; i < ids.Length; i++)
            {
                var door = LivingStreetLayout.Door(ids[i]);
                yield return FaceDoor(door);
                Assert.IsTrue(stage.Ring(door), door.Id + " answers");
                // Mr. Fogg's fling peaks early; Nana Pearl's door eases open; the Abbotts hop in the doorway
                yield return Wait(door.Gag.OpenSeconds + 0.5f);
                Debug.Log($"[LaneHouseArtCaptureTests] {door.Id}: swing {stage.Doors.SwingDegrees(door.Id):F0} deg, " +
                          $"neighbour shown {stage.Doors.NeighbourShown(door.Id)}");
                yield return Shot(names[i], keepCaptions: true);
                yield return Wait(NeighbourDoors.HoldSeconds + NeighbourDoors.CloseSeconds + 0.3f);
                Director.Captions.Clear();
            }

            // 107: a group rings Mr. Fogg's bell and he waves (the Wave clip, left hand high). Measure one wave's highest
            // point and whether the camera sees the hand, then shoot near that point on the next swing.
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            var foggRig = stage.Set.Doors[0];
            Assert.AreEqual(fogg.Id, foggRig.Door.Id);
            yield return FaceDoor(fogg);
            float until = Time.time + 8f;
            while (stage.Doors.Busy(fogg.Id) && Time.time < until)
            {
                yield return null; // a walking group may be at his door
            }

            Assert.IsNotNull(stage.Doors.Ring(fogg.Id, Ringer.Group), "a group rings Mr. Fogg's bell");
            until = Time.time + 1.5f;
            while (!stage.Doors.NeighbourShown(fogg.Id) && Time.time < until)
            {
                yield return null;
            }

            var frame = foggRig.Hinge.parent;
            var hand = ModelKit.FindDeep(foggRig.Figures[0].Root, "Hand_L");
            var eye = game.CameraRig.OutputCamera.transform;
            if (hand == null)
            {
                Debug.LogWarning("[LaneHouseArtCaptureTests] Mr. Fogg has no Hand_L bone; the wave is not measured");
                yield return Wait(0.6f);
            }
            else
            {
                float peak = float.MinValue, peakOut = 0f;
                int frames = 0, hidden = 0;
                until = Time.time + 1.6f;
                while (Time.time < until && stage.Doors.NeighbourShown(fogg.Id))
                {
                    yield return null;
                    Vector3 local = frame.InverseTransformPoint(hand.position);
                    frames++;
                    hidden += HandHidden(frame, fogg, hand.position, eye.position) ? 1 : 0;
                    if (local.y > peak)
                    {
                        peak = local.y;
                        peakOut = fogg.WallLocalZ - local.z;
                    }
                }

                Debug.Log($"[LaneHouseArtCaptureTests] fogg wave: wrist peak {peak:F2} m over the ground, {peakOut:F2} m out from " +
                          $"the wall (stands {LivingStreetLayout.NeighbourOut(fogg):F2} m out); hand hidden by the door head or hood " +
                          $"in {hidden} of {frames} frames; camera at {frame.InverseTransformPoint(eye.position)}");
                until = Time.time + 1.6f;
                while (Time.time < until && frame.InverseTransformPoint(hand.position).y < peak - 0.03f)
                {
                    yield return null;
                }
            }

            yield return Shot("107_fogg_wave_ingame", keepCaptions: true);
            yield return Wait(NeighbourDoors.HoldSeconds + NeighbourDoors.CloseSeconds + 0.3f);
        }
    }
}
