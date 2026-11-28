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
    /// The east lane's three porch houses in the game's light (docs/living-street-houses-art.md), before the slice swaps
    /// them in: this test puts the art where the slice's placeholders stand, the way the swap will (each house on its
    /// door's placement, its leaf on the door rig's hinge, its lamp in the rig's lamp spot and driven by the rig, the
    /// placeholder house, leaf, knob, lamp, bell and flat doorway card hidden). The neighbours are the slice's own
    /// figures: their looks resolve to the crowd kit's neighbour recipes (<c>CrowdKit.Neighbours</c>).
    /// 98_lane_houses: the gameplay camera behind Pip at the hedge gap, looking down the lane at the three houses.
    /// 98b_lane_houses_back: from the Green's corner looking back west. 99_fogg_door, 100_pearl_door,
    /// 101_abbotts_door: Pip rings each bell with the gameplay camera behind her, and the door answers with its gag
    /// (Mr. Fogg's fling, Nana Pearl's slow door and warm lamp, the Abbotts' hop).
    /// </summary>
    [Category("Visual")]
    public class LaneHouseArtCaptureTests : InputTestFixture
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

        private static void Hide(Transform root, string name)
        {
            var t = ModelKit.FindDeep(root, name);
            if (t != null)
            {
                t.gameObject.SetActive(false);
            }
        }

        /// <summary>Puts one house's art in place of the slice's placeholder, as the swap will.</summary>
        private static void SwapIn(LivingStreetSet set, LivingStreetSet.DoorRig rig, MaterialLibrary materials, float correction)
        {
            var door = rig.Door;
            string id = door.Id;
            var houses = new ModelKit(materials, LaneHouseArt.Folder) { FallbackAtlas = null };
            var parts = new ModelKit(materials, LaneHouseArt.Folder) { FallbackAtlas = LaneHouseArt.Atlas(id) };
            Hide(set.StaticRoot, door.House);
            var house = houses.Spawn(LaneHouseArt.House(id), set.transform, new Vector3(door.X, 0f, door.Z), door.Yaw + correction);
            Assert.IsNotNull(house, id + ": house");
            Assert.IsNotNull(house.GetComponentInChildren<LODGroup>(), id + ": the house has its LOD group");

            // the leaf on the rig's hinge (the slice's swing turns the hinge), the placeholder leaf and knob hidden
            rig.Leaf.gameObject.SetActive(false);
            Hide(rig.Hinge, "Knob");
            var leaf = parts.Spawn(LaneHouseArt.DoorLeaf(id), rig.Hinge, Vector3.zero, correction);
            Assert.IsNotNull(leaf, id + ": leaf");
            rig.Leaf = leaf.transform;

            // the lamp where the slice's lamp is, driven by the rig (Nana Pearl's warms while she answers)
            var frame = rig.Hinge.parent;
            var lamp = parts.Spawn(LaneHouseArt.PorchLamp(id), frame, rig.Lamp.transform.localPosition, correction);
            Assert.IsNotNull(lamp, id + ": lamp");
            rig.Lamp.gameObject.SetActive(false);
            var glass = ModelKit.FindDeep(lamp.transform, LaneHouseArt.LampGlow).GetComponent<Renderer>();
            rig.Lamp = glass;
            rig.LampRest = parts.GlowMaterialFor(LaneHouseArt.PorchLamp(id), 0.9f);
            rig.LampBright = parts.GlowMaterialFor(LaneHouseArt.PorchLamp(id), 0.9f + door.Gag.LampBoost);
            glass.sharedMaterial = rig.LampRest;

            // the house has its own bell button and its own lit hall behind the door
            Hide(frame, "Doorbell");
            Hide(frame, "Doorway");
            foreach (var r in house.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
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

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CaptureTheLaneHouses()
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
            float correction = game.Environment.ModelYawCorrection;
            foreach (var rig in stage.Set.Doors)
            {
                SwapIn(stage.Set, rig, game.Materials, correction);
                foreach (var body in rig.Figures)
                {
                    Assert.IsTrue(body.IsKit, rig.Door.Id + ": the neighbour is a crowd kit body");
                }
            }

            // 98: down the lane from the hedge gap, the gameplay camera behind Pip.
            Face(new Vector3(23.4f, 0.1f, -0.6f), new Vector3(34f, 0f, 1.5f));
            yield return Wait(2.0f);
            yield return Shot("98_lane_houses");

            // 98b: from the Green's corner, looking back west along the lane.
            Face(new Vector3(43.2f, 0.1f, 0.4f), new Vector3(30f, 0f, 0.6f));
            yield return Wait(2.0f);
            yield return Shot("98b_lane_houses_back");

            string[] names = { "99_fogg_door", "100_pearl_door", "101_abbotts_door" };
            string[] ids = { LivingStreetLayout.Fogg, LivingStreetLayout.Pearl, LivingStreetLayout.Abbotts };
            for (int i = 0; i < ids.Length; i++)
            {
                var door = LivingStreetLayout.Door(ids[i]);
                Vector3 stand = new Vector3(door.StandX, 0.1f, door.StandZ);
                Vector3 doorAt = new Vector3(door.DoorX, 0f, door.DoorZ);
                Face(stand + (stand - doorAt).normalized * 0.25f, doorAt);
                yield return Wait(1.6f);
                Assert.IsTrue(stage.Ring(door), door.Id + " answers");
                // Mr. Fogg's fling peaks early; Nana Pearl's door eases open; the Abbotts hop in the doorway
                yield return Wait(door.Gag.OpenSeconds + 0.5f);
                Debug.Log($"[LaneHouseArtCaptureTests] {door.Id}: swing {stage.Doors.SwingDegrees(door.Id):F0} deg, " +
                          $"neighbour shown {stage.Doors.NeighbourShown(door.Id)}");
                yield return Shot(names[i], keepCaptions: true);
                yield return Wait(NeighbourDoors.HoldSeconds + NeighbourDoors.CloseSeconds + 0.3f);
                Director.Captions.Clear();
            }
        }
    }
}
