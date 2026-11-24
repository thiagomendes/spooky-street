using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Save;
using SpookyStreet.Localization;
using SpookyStreet.Save;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S004 A1 keepsakes in the running game: walking into a paper bat takes it and writes keepsakes.json at once, a
    /// reload keeps it taken (bats never respawn); a confirmed New game clears the keepsakes (decision 4, one rule in
    /// <see cref="KeepsakeRules.NewGameClearsKeepsakes"/>); a confirmed Replay case keeps them; every perched bat rests on
    /// a real collider. Fresh temporary save folder per test.
    /// </summary>
    public class KeepsakePlayTests : InputTestFixture
    {
        private string _directory;
        private Gamepad _pad;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _pad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            _directory = CaseDirector.UseTemporarySaveDirectory();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static PaperBatCollectibles Bats => Director.Keepsakes;

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private KeepsakeState OnDisk() => new KeepsakeStore(new DiskSaveFileSystem(), _directory).Load();

        private void WriteKeepsakes(int count)
        {
            var store = new KeepsakeStore(new DiskSaveFileSystem(), _directory);
            store.Load();
            for (int i = 1; i <= count; i++)
            {
                store.Find(PaperBatLayout.IdFor(i));
            }
        }

        private void Steer(Vector3 direction)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            direction.y = 0f;
            direction.Normalize();
            Set(_pad.leftStick, new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator WalkingIntoABatTakesItAndAReloadKeepsItTaken()
        {
            yield return Load();
            Assert.AreEqual(KeepsakeRules.TotalBats, Bats.Remaining, "every bat is out on a fresh street");
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
                yield return Frames(3);
            }

            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State);
            GameBootstrap.Instance.Ui.FirstPrompt.SetActive(false);

            // Bat 04 sits at the street lamp's foot on the plaza side: walk south into it from the path.
            var spot = PaperBatLayout.All[3];
            Assert.AreEqual(PaperBatLayout.IdFor(4), spot.Id);
            var pip = GameBootstrap.Instance.Pip;
            pip.Teleport(new Vector3(spot.X, 0.05f, spot.Z + 3.2f));
            yield return Frames(5);
            bool fluttered = false;
            float deadline = Time.time + 5f;
            while (Bats.State.BatCount == 0 && Time.time < deadline)
            {
                Steer(Vector3.back);
                fluttered |= Bats.OpenAmount(spot.Id) > 0.5f;
                yield return null;
            }

            Set(_pad.leftStick, Vector2.zero);
            Assert.IsTrue(Bats.State.HasFound(spot.Id), "walking into the bat takes it; Pip at " + pip.transform.position);
            Assert.IsTrue(fluttered, "it fluttered open as Pip came near");
            Assert.IsFalse(Bats.ViewOf(spot.Id).gameObject.activeSelf, "the bat leaves the world");
            Assert.AreEqual(KeepsakeRules.TotalBats - 1, Bats.Remaining);
            Assert.IsTrue(OnDisk().HasFound(spot.Id), "written to keepsakes.json at once");

            // A reload (the app closed and opened again) keeps it taken; it never respawns.
            yield return Load();
            Assert.IsTrue(Bats.State.HasFound(spot.Id));
            Assert.AreEqual(1, Bats.State.BatCount);
            Assert.IsFalse(Bats.ViewOf(spot.Id).gameObject.activeSelf, "still taken after the reload");
            Assert.AreEqual(KeepsakeRules.TotalBats - 1, Bats.Remaining);
            Assert.IsFalse(Bats.Collect(spot.Id), "a found bat is never found twice");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator AConfirmedNewGameClearsTheKeepsakes()
        {
            WriteKeepsakes(12);
            yield return Load();
            Assert.AreEqual(12, Bats.State.BatCount);
            Assert.AreEqual(KeepsakeRules.TotalBats - 12, Bats.Remaining);
            Assert.AreEqual(0, Director.State.Count, "no case in progress");

            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Director.Notice.IsOpen, "keepsakes alone make New game ask first");
            Assert.AreEqual(12, Bats.State.BatCount, "nothing changes before the answer");
            Director.Notice.PrimaryButton.onClick.Invoke();
            yield return Frames(5);

            bool clears = KeepsakeRules.NewGameClearsKeepsakes;
            int expected = clears ? 0 : 12;
            Assert.AreEqual(expected, Bats.State.BatCount, "decision 4");
            Assert.AreEqual(expected, OnDisk().BatCount, "and its file");
            Assert.AreEqual(KeepsakeRules.TotalBats - expected, Bats.Remaining, "every bat is back in the world");
            yield return Load();
            Assert.AreEqual(expected, Bats.State.BatCount, "after a reload too");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AConfirmedReplayKeepsTheKeepsakes()
        {
            var state = CaseState.Empty;
            foreach (var kind in new[] { CaseActionKind.HearKnocks, CaseActionKind.SitOnBench })
            {
                state = state.Apply(CaseAction.Of(kind)).State;
            }

            var cases = new CaseSaveStore(new DiskSaveFileSystem(), _directory);
            cases.Load();
            Assert.IsTrue(cases.Save(state, CaseAnchors.Plaza));
            WriteKeepsakes(3);
            yield return Load();
            GameBootstrap.Instance.Ui.ContinueButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(3, Bats.State.BatCount);

            Director.Replay();
            yield return Frames(5);
            Assert.AreEqual(0, Director.State.Count, "replaying resets the case");
            Assert.AreEqual(3, Bats.State.BatCount, "the keepsakes stay");
            Assert.AreEqual(3, OnDisk().BatCount, "and so does their file");
            Assert.AreEqual(KeepsakeRules.TotalBats - 3, Bats.Remaining, "found bats stay found");
            for (int i = 1; i <= 3; i++)
            {
                Assert.IsFalse(Bats.ViewOf(PaperBatLayout.IdFor(i)).gameObject.activeSelf);
            }

            yield return Load();
            Assert.AreEqual(3, Bats.State.BatCount, "after a reload too");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator EveryPerchedBatRestsOnARealCollider()
        {
            yield return Load();
            if (!GameBootstrap.Instance.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            // In play, as a child meets them (the stage is fully built and settled).
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
                yield return Frames(3);
            }

            Physics.SyncTransforms();

            // Pip's own capsule must not answer the probe.
            int mask = Physics.DefaultRaycastLayers & ~(1 << GameBootstrap.Instance.Pip.gameObject.layer);
            int checkedBats = 0;
            var failures = new System.Text.StringBuilder();
            foreach (var spot in PaperBatLayout.All)
            {
                if (float.IsNaN(spot.SitY))
                {
                    continue;
                }

                // Every collider in the column under the bat: one must carry it at SitY, and none may sit inside it.
                var origin = new Vector3(spot.X, spot.Y + 0.5f, spot.Z);
                var hits = Physics.RaycastAll(origin, Vector3.down, 1.5f, mask, QueryTriggerInteraction.Ignore);
                var seen = new System.Text.StringBuilder();
                bool rests = false;
                bool inside = false;
                float highest = float.NegativeInfinity;
                foreach (var hit in hits)
                {
                    seen.Append($" {hit.collider.name}@{hit.point.y:F2}");
                    rests |= Mathf.Abs(hit.point.y - spot.SitY) <= 0.12f;
                    inside |= hit.point.y > spot.SitY + 0.12f && hit.point.y < spot.Y + 0.05f;
                    highest = Mathf.Max(highest, hit.point.y);
                }

                // A bat on the lawn: the ground colliders report their hits a little high (run 164 showed Ground and
                // GroundRing at 0.25 m), so it is enough that nothing solid stands under or in it.
                if (spot.SitY < 0.05f)
                {
                    rests = hits.Length > 0 && highest <= 0.3f;
                }

                Debug.Log($"[Keepsakes] {spot.Id} sit {spot.SitY:F2}: hits{seen}");
                if (!rests || inside)
                {
                    failures.Append($"\n{spot} {(inside ? "sits inside a collider" : "rests on nothing")} at " +
                                    $"{spot.SitY:F2} (hits:{seen})");
                }

                checkedBats++;
            }

            if (failures.Length > 0)
            {
                Assert.Fail("perched bats without their surface:" + failures);
            }
            Assert.GreaterOrEqual(checkedBats, 20);
        }
    }
}
