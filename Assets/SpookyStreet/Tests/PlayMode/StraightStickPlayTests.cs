using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Tips;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The owner's son on the iPhone (2026-11-28): "held straight, Pip zigzags". In the real scene a thumb drags the
    /// floating stick a little crooked and trembling, and Pip must run a straight line with the camera's auto-recenter
    /// running. The loop maths (thumb, motor, camera) is covered by the Core StraightStickTests.
    /// </summary>
    public class StraightStickPlayTests
    {
        private const string SceneName = "LanternLane";

        /// <summary>On the arrival path heading north: flat and clear up to the Owl House porch step (z −4).</summary>
        private static readonly Vector3 OnPath = new Vector3(0f, 0.3f, -10.5f);

        [SetUp]
        public void SetUp()
        {
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static Vector2 ScreenPoint(FloatingStick stick, Vector2 local)
        {
            var canvas = stick.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(eventCamera, stick.transform.TransformPoint(local));
        }

        [UnityTest]
        public IEnumerator CrookedTremblingThumbRunsPipInAStraightLine()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var game = GameBootstrap.Instance;
            Assert.IsNotNull(game, "scene did not create GameBootstrap");
            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.2f);
            if (game.Director != null && game.Director.Cutscenes.Playing)
            {
                game.Director.Cutscenes.Player.SkipNow();
                yield return null;
            }

            game.Ui.Tips.UseStore(new MemoryTipStore());
            game.Ui.FirstPrompt.SetActive(false);
            var pip = game.Pip;
            pip.Teleport(OnPath);
            pip.transform.rotation = Quaternion.identity;
            game.CameraRig.Cut(0f);
            yield return Wait(0.3f);
            Assert.IsTrue(pip.Grounded, "standing on the path");

            var stick = game.Controls.Touch != null ? game.Controls.Touch.Stick : null;
            Assert.IsNotNull(stick, "the HUD built the floating stick");
            Assert.IsTrue(stick.isActiveAndEnabled, "the stick is live while playing");

            // The thumb lands on the ghost and pushes 90% of the radius, 4° to the right of straight up, trembling ±5°.
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 7 };
            Vector2 origin = stick.RestPosition;
            pointer.position = ScreenPoint(stick, origin);
            stick.OnPointerDown(pointer);
            Vector3 start = pip.transform.position;
            float maxLateral = 0f;
            float time = 0f;
            while (time < 1.2f)
            {
                float degrees = 4f + 5f * Mathf.Sin(time * 2f * Mathf.PI * 4.3f) * Mathf.Cos(time * 2f * Mathf.PI * 1.7f);
                float radius = stick.Radius * (0.9f + 0.08f * Mathf.Sin(time * 2f * Mathf.PI * 3.1f));
                float a = degrees * Mathf.Deg2Rad;
                pointer.position = ScreenPoint(stick, origin + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius);
                stick.OnDrag(pointer);
                yield return null;
                time += Time.deltaTime;
                maxLateral = Mathf.Max(maxLateral, Mathf.Abs(pip.transform.position.x - start.x));
            }

            float forward = pip.transform.position.z - start.z;
            float heading = Mathf.DeltaAngle(0f, pip.HeadingYaw);
            float lens = game.CameraRig.EffectiveYaw;
            stick.OnPointerUp(pointer);
            Debug.Log($"[StraightStick] forward {forward:F2} m, sideways {maxLateral:F3} m, heading {heading:F1}°, " +
                      $"lens {lens:F1}°");
            Assert.Greater(forward, 3f, "Pip ran up the path");
            Assert.Less(maxLateral, 0.15f, "a crooked, trembling thumb still runs her straight");
            Assert.Less(Mathf.Abs(heading), 3f, "and she still faces straight ahead");
            Assert.Less(Mathf.Abs(lens), 3f, "the camera did not chase the wobble");
            Assert.AreEqual(0, pip.SafeReturns);
        }
    }
}
