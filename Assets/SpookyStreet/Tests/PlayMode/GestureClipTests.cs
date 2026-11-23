using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Characters;
using SpookyStreet.Core.Animation;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S003a: the testimony gestures (Bea ShadeEyes, Mr. Vale CheckWatch, June Listen, Aunt Kit Ponder) and Pip's
    /// hint glances exist on the imported models and play in the running game: over the rest loop and back to it,
    /// with the hand where the gesture puts it, turning the body only for a gesture that is aimed ahead. A gesture a
    /// model does not have is refused quietly, so a cutscene or a line can cue it before or without the clip.
    /// The import side (takes, lengths, loop flags) is in ModelImportRulesTests.
    /// </summary>
    public class GestureClipTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        /// <summary>Where the test cast stands: south of the block, beyond the boundary wall (as the climb test rig).</summary>
        private static readonly Vector3 Stage = new Vector3(0f, 0f, -45f);

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests; the game starts in control
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var game = GameBootstrap.Instance;
            Assert.IsNotNull(game, "scene did not create GameBootstrap");
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(1f, Time.timeScale);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        // model, the hand that makes the gesture, how far that hand must rise above where it rests (m), how near
        // the head joint it must come (m; Mr. Vale's stays at his chest)
        [UnityTest]
        public IEnumerator TestimonyGesturesPlayOverTheRestLoopAndComeBack(
            [Values("Bea", "Vale", "June", "Kit")] string model)
        {
            if (!NpcAvatar.Exists(model))
            {
                Assert.Ignore(model + ".fbx is not committed.");
            }

            yield return StartGame();
            var game = GameBootstrap.Instance;
            WitnessClipSet clips = model == "Vale" ? CharacterClips.Seated : CharacterClips.Standing;
            string hand = model == "Bea" || model == "June" ? "Hand_R" : "Hand_L";
            float reach = model == "Vale" ? 0.6f : 0.4f;

            var root = new GameObject("GestureStage").transform;
            root.position = Stage;
            var avatar = NpcAvatar.Spawn(game.Materials, model, root, Vector3.zero, 0f, clips.Rest);
            Assert.IsNotNull(avatar, model + " did not spawn");
            Assert.IsTrue(avatar.IsAnimated, model + "'s animation graph did not start");
            var witness = avatar.gameObject.AddComponent<Witness>();
            witness.Configure(clips, null);
            yield return Wait(0.4f);

            string gesture = CharacterClips.TestimonyGestureOf(model);
            Assert.IsNotNull(gesture);
            Assert.IsTrue(avatar.HasClip(gesture), $"{model}.fbx has no {gesture} clip ({string.Join(",", avatar.ClipNames)})");
            float length = avatar.LengthOf(gesture);
            Assert.That(length, Is.InRange(1.8f, 2.6f), gesture + " lasts about two seconds");
            Transform handBone = avatar.Find(hand);
            Transform head = avatar.Find(Witness.HeadBone);
            Assert.IsNotNull(handBone, hand);
            Assert.IsNotNull(head);
            float restHeight = handBone.position.y;
            float restYaw = witness.transform.eulerAngles.y;

            // Cued with a point off to one side, as a cutscene or a line would.
            Vector3 aim = witness.transform.position + witness.transform.right * 6f + Vector3.up * 3f;
            Assert.IsTrue(witness.PlayGesture(gesture, aim), gesture + " did not start");
            Assert.IsTrue(witness.Gesturing);
            Assert.AreEqual(gesture, avatar.OneShot);
            Assert.AreEqual(clips.Rest, avatar.Loop, "the rest loop stays underneath");

            yield return Wait(length * 0.42f);
            Assert.IsTrue(witness.Gesturing, gesture + " ended early");
            Assert.Greater(avatar.WeightOf(gesture), 0.6f, gesture + " is what shows");
            Assert.Greater(handBone.position.y, restHeight + 0.2f, "the hand is up");
            Assert.Less(Vector3.Distance(handBone.position, head.position), reach, "the hand is where the gesture puts it");
            float turned = Mathf.Abs(Mathf.DeltaAngle(witness.transform.eulerAngles.y, restYaw));
            if (CharacterClips.TurnsToAim(gesture))
            {
                Assert.Greater(turned, 60f, gesture + " looks along her forward: she turns to the place");
            }
            else
            {
                Assert.Less(turned, 0.5f, gesture + " aims nowhere: the body does not turn");
            }

            yield return Wait(length * 0.58f + 1.2f);
            Assert.IsFalse(witness.Gesturing);
            Assert.IsNull(avatar.OneShot);
            Assert.Greater(avatar.WeightOf(clips.Rest), 0.9f, "back on the rest loop");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(witness.transform.eulerAngles.y, restYaw)), 1f, "facing as before");
            Assert.Less(Mathf.Abs(handBone.position.y - restHeight), 0.15f, "the hand is back where it rests");

            // A gesture this model does not have (another character's, a misspelt or a missing one) is refused
            // quietly and changes nothing: content can cue it safely.
            string foreign = CharacterClips.TestimonyGestureOf(model == "Bea" ? "Vale" : "Bea");
            Assert.IsFalse(avatar.HasClip(foreign));
            Assert.IsFalse(witness.PlayGesture(foreign, aim));
            Assert.IsFalse(witness.PlayGesture("NoSuchGesture", aim));
            Assert.IsFalse(witness.PlayGesture(null, aim));
            Assert.IsFalse(witness.PlayGesture(string.Empty, null));
            Assert.IsFalse(witness.Gesturing);
            Assert.IsNull(avatar.OneShot);
            yield return Wait(0.3f);
            Assert.Greater(avatar.WeightOf(clips.Rest), 0.9f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(witness.transform.eulerAngles.y, restYaw)), 1f);
            Object.Destroy(root.gameObject);
        }

        [UnityTest]
        public IEnumerator PipGlancesTowardTheSideAPlaceIsOnAndComesBack()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var animator = pip.GetComponent<PipAvatar>().Animator;
            if (animator == null)
            {
                Assert.Ignore("Pip.fbx is not committed; the primitive avatar is in use.");
            }

            yield return Wait(0.6f);
            Assert.IsTrue(pip.Grounded, "Pip starts standing");
            Assert.IsTrue(animator.HasClip(PipClips.GlanceLeft), "Pip.fbx has no GlanceLeft clip");
            Assert.IsTrue(animator.HasClip(PipClips.GlanceRight), "Pip.fbx has no GlanceRight clip");
            Transform head = ModelKit.FindDeep(pip.transform, Witness.HeadBone);
            Assert.IsNotNull(head, "Pip's head bone");

            foreach (float side in new[] { -1f, 1f })
            {
                string clip = side < 0f ? PipClips.GlanceLeft : PipClips.GlanceRight;
                Quaternion restHead = head.rotation;
                Vector3 place = pip.transform.position + pip.transform.right * (side * 6f) + pip.transform.forward * 1.5f;
                Assert.IsTrue(animator.PlayGlance(place), clip + " did not start");
                Assert.AreEqual(clip, animator.OneShot);

                // Frame 16 of 40: peering.
                yield return Wait(0.53f);
                Assert.AreEqual(clip, animator.OneShot, "still glancing");
                Assert.Greater(animator.WeightOf(clip), 0.9f);
                Vector3 facing = (head.rotation * Quaternion.Inverse(restHead)) * pip.transform.forward;
                float yaw = Vector3.SignedAngle(pip.transform.forward, Vector3.ProjectOnPlane(facing, Vector3.up), Vector3.up);
                Assert.That(yaw * side, Is.InRange(55f, 115f), $"{clip}: her face turns to that side (measured {yaw:F0} degrees)");

                yield return Wait(1.2f);
                Assert.IsNull(animator.OneShot, clip + " ended");
                Assert.Greater(animator.WeightOf(PipClips.Idle), 0.9f, "back to Idle");
                float back = Quaternion.Angle(head.rotation, restHead);
                Assert.Less(back, 20f, "her head is back (Idle sways a little)");
            }

            // No direction, no glance; and an unknown one-shot is refused without an error.
            Assert.IsFalse(animator.PlayGlance(pip.transform.position));
            Assert.IsFalse(animator.PlayOneShot("NoSuchGlance"));
            Assert.IsNull(animator.OneShot);
        }
    }
}
