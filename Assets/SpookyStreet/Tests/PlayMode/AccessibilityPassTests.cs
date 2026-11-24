using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Parent;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S004 stream F2, the accessibility pass (A11Y-01, D25; docs/S004-a11y.md). One whole case by gamepad only, one by
    /// keyboard only and one by touch only: the Core route's stand points (CaseStage, CaseRouteLayout; the walk itself
    /// is MAP-01's), every Use, line, skip, cutscene skip and deduction through that scheme alone, the deductions placed
    /// by tap-tap or the cursor. The keyboard and touch runs play under the A11Y-01 settings (sound muted, vibration off,
    /// large captions, solid caption boxes, reduced motion) and check that every required signal still shows. Then the
    /// per-screen checks: the pause card keeps focus under every card it opens, the title and the gate by keys, Escape
    /// on the notebook never pauses, the reading pages say they scroll, and "Mergulhar" fits the Use disc's tag.
    /// </summary>
    public class AccessibilityPassTests : InputTestFixture
    {
        private enum Scheme
        {
            Gamepad,
            Keyboard,
            Touch,
        }

        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private Scheme _scheme;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(playOpening: true);
            ControlSchemeTracker.Override = null;
            AmbientMotion.ReducedMotion = false;
            GameHaptics.Enabled = true;
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            GameHaptics.Enabled = true;
            ControlSchemeTracker.Override = null;
            CaseCutscenes.AutoPlayOpening = false;
            LanguageController.DeviceLocalesOverride = null;
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => Game.Director;

        private static NonverbalMenus Menus => Director.Nonverbal.Menus;

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        /// <summary>Real time: the waits that span a pause (time scale 0).</summary>
        private static IEnumerator WaitReal(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

        // ------------------------------------------------------------------------------------- one control per scheme

        private void UseScheme(Scheme scheme)
        {
            _scheme = scheme;
            ControlSchemeTracker.Override = scheme == Scheme.Gamepad ? ControlScheme.Gamepad
                : scheme == Scheme.Keyboard ? ControlScheme.KeyboardMouse : ControlScheme.Touch;
        }

        /// <summary>A tap on an on-screen touch control (the Use disc), through its own pointer handlers.</summary>
        private static IEnumerator TouchPress(GameObject control)
        {
            var data = new PointerEventData(EventSystem.current) { pointerId = 7, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(control, data, ExecuteEvents.pointerDownHandler);
            yield return null;
            yield return null;
            ExecuteEvents.Execute(control, data, ExecuteEvents.pointerUpHandler);
            yield return null;
        }

        private IEnumerator PressUse()
        {
            switch (_scheme)
            {
                case Scheme.Gamepad:
                    yield return Tap(_gamepad.buttonWest);
                    break;
                case Scheme.Keyboard:
                    yield return Tap(_keyboard.eKey);
                    break;
                default:
                    yield return TouchPress(Game.Ui.UseButton);
                    break;
            }
        }

        private IEnumerator AdvanceLine()
        {
            switch (_scheme)
            {
                case Scheme.Gamepad:
                    yield return Tap(_gamepad.buttonSouth);
                    break;
                case Scheme.Keyboard:
                    yield return Tap(_keyboard.enterKey);
                    break;
                default:
                    Director.Dialogue.transform.Find("TapCatcher").GetComponent<Button>().onClick.Invoke();
                    yield return Frames(2);
                    break;
            }
        }

        private IEnumerator SkipConversation()
        {
            switch (_scheme)
            {
                case Scheme.Gamepad:
                    yield return Tap(_gamepad.buttonEast);
                    break;
                case Scheme.Keyboard:
                    yield return Tap(_keyboard.tabKey);
                    break;
                default:
                    Director.Dialogue.SkipButton.onClick.Invoke();
                    yield return Frames(2);
                    break;
            }
        }

        /// <summary>Reads every line of the open conversation with the scheme's advance control.</summary>
        private IEnumerator ReadAll()
        {
            for (int i = 0; i < 24 && Director.Dialogue.IsOpen; i++)
            {
                yield return WaitReal(DialogueAdvanceGate.MinShowSeconds + 0.1f);
                yield return AdvanceLine();
            }

            Assert.IsFalse(Director.Dialogue.IsOpen, "every line advances with " + _scheme);
            yield return Wait(0.3f);
        }

        /// <summary>Skips the open conversation (if any) with the scheme's skip control.</summary>
        private IEnumerator Settle()
        {
            if (Director.Dialogue.IsOpen)
            {
                yield return WaitReal(0.3f);
                yield return SkipConversation();
                Assert.IsFalse(Director.Dialogue.IsOpen, "the conversation skips with " + _scheme);
            }

            yield return Wait(0.4f);
        }

        /// <summary>Skips every cutscene that is playing, with the scheme: a key or button twice, or the touch disc.</summary>
        private IEnumerator SkipCutscenes()
        {
            var cutscenes = Director.Cutscenes;
            for (int i = 0; i < 4 && cutscenes.Playing; i++)
            {
                yield return WaitReal(0.6f);
                switch (_scheme)
                {
                    case Scheme.Gamepad:
                    case Scheme.Keyboard:
                        // A key or button asks first ("Press again to skip"); the next press skips. A press in a
                        // cutscene's first moments (the seed chained after the reveal) is ignored, so press until it goes.
                        var key = _scheme == Scheme.Gamepad ? (ButtonControl)_gamepad.buttonSouth : _keyboard.spaceKey;
                        bool skippedBefore = cutscenes.Player.LastSkipped; // still the last one's outro
                        yield return Tap(key);
                        if (!skippedBefore)
                        {
                            Assert.IsFalse(cutscenes.Player.LastSkipped, "one stray press never skips");
                        }
                        for (int press = 0; press < 5 && !cutscenes.Player.LastSkipped; press++)
                        {
                            yield return WaitReal(0.3f);
                            yield return Tap(key);
                        }

                        break;
                    default:
                        // The disc skips at once; a press in the first moments is ignored, so tap again, as a child would.
                        for (int tap = 0; tap < 4 && !cutscenes.Player.LastSkipped; tap++)
                        {
                            cutscenes.Player.Frame.SkipButton.onClick.Invoke();
                            if (!cutscenes.Player.LastSkipped)
                            {
                                yield return WaitReal(0.5f);
                            }
                        }

                        break;
                }

                Assert.IsTrue(cutscenes.Player.LastSkipped, "the cutscene skips with " + _scheme);
                yield return WaitReal(1.0f);
            }

            Assert.IsFalse(cutscenes.Playing, "every cutscene skipped with " + _scheme);
        }

        private IEnumerator Direction(int dx, int dy)
        {
            if (_scheme == Scheme.Gamepad)
            {
                yield return Tap(dx < 0 ? _gamepad.dpad.left : dx > 0 ? _gamepad.dpad.right : dy < 0 ? _gamepad.dpad.up : _gamepad.dpad.down);
            }
            else
            {
                yield return Tap(dx < 0 ? _keyboard.leftArrowKey : dx > 0 ? _keyboard.rightArrowKey
                    : dy < 0 ? _keyboard.upArrowKey : _keyboard.downArrowKey);
            }
        }

        private IEnumerator Submit() => _scheme == Scheme.Gamepad ? Tap(_gamepad.buttonSouth) : Tap(_keyboard.enterKey);

        private IEnumerator Back() => _scheme == Scheme.Gamepad ? Tap(_gamepad.buttonEast) : Tap(_keyboard.escapeKey);

        private IEnumerator PageRight() => _scheme == Scheme.Gamepad ? Tap(_gamepad.rightShoulder) : Tap(_keyboard.rKey);

        private IEnumerator PageLeft() => _scheme == Scheme.Gamepad ? Tap(_gamepad.leftShoulder) : Tap(_keyboard.qKey);

        // ------------------------------------------------------------------------------------- the route

        private static Vector3 At(StagePoint p) => new Vector3(p.X, p.Y, p.Z);

        private static Vector3 Point(string id) => At(CaseStage.Find(id).Position);

        /// <summary>Puts Pip at <paramref name="stand"/> facing the target, camera behind; the Use disc names it.</summary>
        private static IEnumerator StandFor(string pointId, Vector3 stand)
        {
            var target = Point(pointId);
            Game.Pip.Teleport(stand + Vector3.up * 0.05f);
            Vector3 to = target - stand;
            to.y = 0f;
            Game.Pip.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            Game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(0.6f);
            var current = Director.Targets.Current;
            Assert.IsNotNull(current, $"no Use target at {stand} (expected {pointId})");
            Assert.AreEqual(pointId, current.Id, "wrong Use target selected");
        }

        private IEnumerator Use(string pointId, Vector3 stand)
        {
            yield return StandFor(pointId, stand);
            yield return PressUse();
            yield return Frames(2);
        }

        private static IEnumerator Cross(ThresholdEnd exit, Vector3 approach, Vector3 inZone)
        {
            var travel = Director.Travel;
            int before = travel.Crossings;
            Game.Pip.Teleport(approach + Vector3.up * 0.05f);
            yield return Wait(0.8f);
            Game.Pip.Teleport(inZone + Vector3.up * 0.05f);
            float until = Time.time + 3f;
            while ((travel.Active || travel.Crossings == before) && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(before + 1, travel.Crossings, "crossed " + exit);
            yield return Frames(3);
        }

        private static float BenchFacing => (EnvironmentLayout.BenchYaw + EnvironmentLayout.ModelFacingOffset) * Mathf.Deg2Rad;

        private static Vector3 BenchFront => new Vector3(Mathf.Sin(BenchFacing), 0f, Mathf.Cos(BenchFacing));

        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        private static Vector3 Workshop(float along) =>
            new Vector3(CaseRouteLayout.NookX - 0.7f, CaseRouteLayout.FloorY, CaseRouteLayout.MechanismZ + along);

        /// <summary>The A11Y-01 settings, set the way the settings card sets them (applied live).</summary>
        private static void ApplyA11y01()
        {
            var p = Game.Audio.Preferences;
            p.MusicVolume = 0f;
            p.EffectsVolume = 0f;
            p.DialogueVolume = 0f;
            p.Haptics = false;
            p.LargeCaptions = true;
            p.OpaqueCaptions = true;
            p.ReducedMotion = true;
            Menus.PreferencesChanged(p);
            Assert.IsTrue(AmbientMotion.ReducedMotion, "reduced motion");
            Assert.IsFalse(GameHaptics.Enabled, "vibration off");
            Assert.IsTrue(Director.Captions.LargeText, "large captions");
            Assert.IsTrue(Director.Dialogue.LargeText, "large speech text");
        }

        /// <summary>
        /// The whole case with one scheme: new game, the opening skipped, the witnesses, the bench, the cloth, the
        /// window (the ramp route), the sill, June, the hall's hum, the latch, the workshop, the door, the finale, the
        /// three deductions and the reveal and seed skipped. Every press is the scheme's own.
        /// </summary>
        private IEnumerator PlayWholeCase(Scheme scheme, bool a11y)
        {
            yield return Load();
            if (Game.Environment == null || !Game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed: the interior is not staged.");
            }

            UseScheme(scheme);
            if (a11y)
            {
                ApplyA11y01();
            }

            // The title: New game by the scheme (no save, so it is the focused, only button).
            yield return Frames(3);
            if (scheme == Scheme.Touch)
            {
                Game.Ui.NewGameButton.onClick.Invoke();
            }
            else
            {
                Assert.AreEqual(ScreenFocus.TitleNewGame, Game.Ui.TitleNavigation.Focus, "the title focuses New game");
                Assert.IsTrue(Game.Ui.TitleNavigation.Outline != null && Game.Ui.TitleNavigation.Outline.Showing,
                    "the candy outline rings it");
                yield return Submit();
            }

            yield return Frames(3);
            Assert.AreEqual(GameState.Playing, Game.Flow.State, "New game by " + scheme);
            Assert.IsTrue(Director.Cutscenes.Playing, "the opening plays");
            yield return SkipCutscenes();
            if (!Director.State.Has(CaseFact.HeardKnocks))
            {
                Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks)); // the arrival cue fires on the first steps
            }

            yield return Frames(2);
            Assert.AreEqual(LocalizationKeys.CaseLines.CaptionArrivalKnocks, Director.Captions.CurrentKey,
                "the knocks show as a caption (sound off needs it)");
            yield return Wait(0.6f);

            // The witnesses and the bench: Bea's lines read to the end, Mr. Vale's skipped.
            yield return Use(CaseStage.PointBea, Point(CaseStage.PointBea) +
                (Point(CaseStage.PointBea) - Point(CaseStage.PointBicycle)).normalized * 1.0f - Vector3.up * Point(CaseStage.PointBea).y);
            Assert.IsTrue(Director.Dialogue.IsOpen, "Bea talks");
            yield return ReadAll();
            Assert.IsTrue(Director.State.Has(CaseFact.BeaSpoke));
            yield return Use(CaseStage.PointVale, Point(CaseStage.PointVale) + BenchFront * 1.1f - Vector3.up * Point(CaseStage.PointVale).y);
            yield return Settle();
            Assert.IsTrue(Director.State.Has(CaseFact.ValeSpoke));
            yield return Use(CaseStage.PointBench, Point(CaseStage.PointBench) + BenchFront * 1.2f - Vector3.up * Point(CaseStage.PointBench).y);
            Assert.IsTrue(Director.State.Has(CaseFact.CheckedView), "sitting by " + scheme);
            yield return Wait(BenchView.HoldSeconds + 0.4f);

            // Up the ramp route (MAP-01 walks it): the cloth, then in by June's window.
            yield return Use(CaseStage.PointCloth, new Vector3(-6.75f, CaseRouteLayout.TreehouseY, CaseRouteLayout.ClothZ));
            Assert.IsTrue(Director.State.Has(CaseFact.ExaminedCloth));
            yield return Settle();
            yield return Cross(Thresholds.DormerOutside, new Vector3(-4.3f, 2.5f, CaseRouteLayout.ExteriorWindowZ),
                new Vector3(-3.36f, 2.5f, CaseRouteLayout.ExteriorWindowZ));
            Assert.IsTrue(Director.State.Has(CaseFact.EnteredByWindow));
            yield return Use(CaseStage.PointSill, new Vector3(-2.6f, CaseRouteLayout.LoftY, Ifz + 1.75f));
            Assert.IsTrue(Director.State.Has(CaseFact.ExaminedSill));
            yield return Settle();
            yield return Use(CaseStage.PointJuneAttic, new Vector3(-0.4f, CaseRouteLayout.LoftY, Ifz + 2.4f));
            Assert.IsTrue(Director.State.Has(CaseFact.FoundJune));
            yield return Settle();
            float until = Time.time + 22f;
            while (Director.Cast.JuneWalking && Time.time < until)
            {
                yield return null;
            }

            // The hall hums: with the sound off, the caption says so.
            CaseStage.TryGetAnchor(CaseAnchors.Foyer, out var foyer);
            Game.Pip.Teleport(At(foyer));
            yield return Wait(0.4f);
            Assert.AreEqual(LocalizationKeys.CaseLines.CaptionFoyerHum, Director.Captions.CurrentKey, "the floor hum shows");
            Assert.IsTrue(Director.State.Has(CaseFact.FloorHum));
            yield return Use(CaseStage.PointLatch, new Vector3(0.35f, CaseRouteLayout.FloorY, Ifz + 1.0f));
            Assert.IsTrue(Director.State.Has(CaseFact.FrontOpen));
            yield return Settle();
            yield return Wait(1.4f);

            // The workshop: the grille, the arm, the crank (the cutaway plays and ends by itself).
            yield return Use(CaseStage.PointGrille, Workshop(0.8f));
            Assert.IsTrue(Director.State.Has(CaseFact.SawCam));
            yield return Settle();
            yield return Wait(MechanismMotion.CycleSeconds + 0.2f);
            yield return Use(CaseStage.PointLinkArm, Workshop(-0.8f));
            Assert.IsTrue(Director.State.Has(CaseFact.LinkedArm));
            yield return Settle();
            yield return Use(CaseStage.PointCrank, Workshop(0.1f));
            Assert.IsTrue(Director.State.Has(CaseFact.TestedOwl));
            until = Time.time + CutawayView.MaxSeconds + 3f;
            while (Director.Cutaway.Active && Time.time < until)
            {
                yield return null;
            }

            yield return Settle();

            // Out by the door to the arrival porch: the finale, every line read with the scheme.
            yield return Cross(Thresholds.DoorInside, new Vector3(0f, CaseRouteLayout.FloorY, Ifz + 1.0f),
                new Vector3(0f, CaseRouteLayout.FloorY, Ifz + 0.05f));
            Game.Pip.Teleport(new Vector3(0f, 0.7f, -13.4f));
            yield return Frames(3);
            Assert.IsTrue(Director.State.Has(CaseFact.CaseClosed));
            until = Time.time + FinaleView.IntroSeconds + 3f;
            while (!Director.Dialogue.IsOpen && Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(Director.Dialogue.IsOpen, "the finale talks");
            yield return ReadAll();
            until = Time.time + FinaleView.HoldAfterLines + 3f;
            while (!Director.Notebook.IsOpen && Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(Director.Notebook.IsOpen, "the notebook opens on the open questions");

            // The child's deductions, placed with the scheme alone.
            yield return Deduce(N.QuestionSource, N.WoodenPiece, N.FloorHum);
            yield return Deduce(N.QuestionEntry, N.SillSnag, N.ValeDoor, N.BeaFlash);
            yield return Deduce(N.QuestionMoved, N.LooseArm, N.OwlBooth);
            Assert.IsTrue(Director.State.Has(CaseFact.CaseStamped), "the case is stamped by " + scheme);

            // Close the notebook with the scheme; the reveal and the seed follow, each skipped with the scheme.
            if (scheme == Scheme.Touch)
            {
                Director.Notebook.CloseButton.onClick.Invoke();
                yield return Frames(2);
            }
            else
            {
                yield return Back();
            }

            yield return Frames(3);
            Assert.IsFalse(Director.Notebook.IsOpen);
            Assert.AreEqual(GameState.Playing, Game.Flow.State, "Back closed the notebook and never paused the game");
            until = Time.time + 2f;
            while (!Director.Cutscenes.Playing && Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(Director.Cutscenes.Playing, "the reveal follows the stamp");
            yield return SkipCutscenes();
            yield return Frames(3);
            Assert.IsTrue(Game.Controls.GameplayEnabled, "the player keeps control after the finale");
            if (a11y)
            {
                Assert.IsTrue(AmbientMotion.ReducedMotion && !GameHaptics.Enabled && Director.Captions.LargeText,
                    "the A11Y-01 settings held for the whole case");
                Assert.AreEqual(0f, Game.Audio.Preferences.MusicVolume);
            }
        }

        /// <summary>Turns to the question's page and places its stickers: tap-tap on touch, the cursor otherwise.</summary>
        private IEnumerator Deduce(string questionId, params string[] stickers)
        {
            var notebook = Director.Notebook;
            int page = notebook.PageQuestions.IndexOf(N.Question(questionId));
            Assert.GreaterOrEqual(page, 0, questionId + " is asked");
            for (int i = 0; i < 6 && notebook.CurrentPage != page; i++)
            {
                if (_scheme == Scheme.Touch)
                {
                    notebook.ShowPage(page); // the page dots and arrows are taps (their own tests cover them)
                }
                else
                {
                    yield return notebook.CurrentPage < page ? PageRight() : PageLeft();
                }

                yield return Wait(0.3f);
            }

            Assert.AreEqual(page, notebook.CurrentPage, questionId + ": the page turns with " + _scheme);
            yield return Wait(0.4f);
            foreach (string id in stickers)
            {
                if (_scheme == Scheme.Touch)
                {
                    notebook.TapSticker(id);
                    notebook.TapSlot(notebook.CurrentBoard.FirstEmpty());
                    yield return Frames(2);
                    continue;
                }

                // Down to the tray, then left or right to the sticker; Submit picks, Submit places.
                yield return Direction(0, 1);
                Assert.AreEqual(CursorRow.Tray, notebook.CursorRow, "down reaches the tray");
                var tray = new List<string>();
                foreach (var s in notebook.TrayStickers)
                {
                    tray.Add(s.StickerId);
                }

                int index = tray.IndexOf(id);
                Assert.GreaterOrEqual(index, 0, id + " is in the tray");
                for (int i = 0; i < 12 && notebook.CursorIndex != index; i++)
                {
                    yield return Direction(notebook.CursorIndex < index ? 1 : -1, 0);
                }

                Assert.AreEqual(index, notebook.CursorIndex, id);
                Assert.IsTrue(notebook.FocusOutline != null && notebook.FocusOutline.Showing, "the outline rings the cursor");
                Assert.IsTrue(notebook.SubmitPrompt.HasGlyph, "the Submit glyph rides the outline on " + _scheme);
                yield return Submit();
                Assert.AreEqual(id, notebook.HeldSticker, "Submit picks " + id);
                yield return Submit();
                Assert.IsNull(notebook.HeldSticker, "Submit places " + id);
            }

            float until = Time.time + 6f;
            while (notebook.Checking && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(DeductionVerdict.Right, notebook.LastCheck.Verdict, questionId + " by " + _scheme);
            yield return Wait(4.5f); // the comic plays twice and holds
        }

        // ------------------------------------------------------------------------------------- A11Y-01: whole cases

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator AWholeCaseByGamepadOnly()
        {
            yield return PlayWholeCase(Scheme.Gamepad, false);
        }

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator AWholeCaseByKeyboardOnlyUnderTheA11y01Settings()
        {
            yield return PlayWholeCase(Scheme.Keyboard, true);
        }

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator AWholeCaseByTouchOnlyUnderTheA11y01Settings()
        {
            yield return PlayWholeCase(Scheme.Touch, true);
        }

        // ------------------------------------------------------------------------------------- per screen

        /// <summary>
        /// The known issue: after the settings card or the language screen closed over the pause card, nothing on the
        /// pause card had gamepad focus. Now the pause card sits under every card it opens and gets its focus back.
        /// </summary>
        [UnityTest]
        [Timeout(90000)]
        public IEnumerator ThePauseCardGetsFocusBackAfterEveryCardItOpens()
        {
            CaseCutscenes.AutoPlayOpening = false;
            yield return Load();
            UseScheme(Scheme.Gamepad);
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(3);
            Game.Flow.Pause();
            yield return Frames(3);
            var nav = Game.Ui.PauseNavigation;
            Assert.IsTrue(nav.IsTop);
            Assert.AreEqual(ScreenFocus.PauseResume, nav.Focus, "the pause card opens on Resume");
            Assert.IsTrue(nav.Outline != null && nav.Outline.Showing, "the candy outline rings Resume");

            // The globe: the language screen opens on top; East closes it; focus is back on the globe.
            yield return Tap(_gamepad.dpad.up);
            Assert.AreEqual(ScreenFocus.PauseLanguage, nav.Focus);
            yield return Tap(_gamepad.buttonSouth);
            yield return Frames(2);
            Assert.IsTrue(Game.LanguageScreen.IsOpen, "South opens the language screen");
            Assert.IsFalse(nav.IsTop, "the pause card waits under it");
            Assert.IsFalse(nav.Outline.Showing, "one outline at a time");
            yield return Tap(_gamepad.buttonEast);
            yield return Frames(2);
            Assert.IsFalse(Game.LanguageScreen.IsOpen);
            Assert.IsTrue(nav.IsTop, "the pause card takes input back");
            Assert.AreEqual(ScreenFocus.PauseLanguage, nav.Focus, "focus where it was");
            Assert.IsTrue(nav.Outline.Showing && nav.Outline.Target == (RectTransform)Game.Ui.PauseLanguageButton.transform);
            Assert.AreEqual(GameState.Paused, Game.Flow.State, "the same press never resumed the game");

            // The gear: the gate, cancelled by East, then passed with both bumpers; the card closes with East.
            yield return Tap(_gamepad.dpad.right);
            Assert.AreEqual(ScreenFocus.PauseSettings, nav.Focus, "the gear is on the pause card's graph");
            yield return Tap(_gamepad.buttonSouth);
            yield return Frames(2);
            Assert.IsTrue(Menus.GateOpen);
            Assert.IsTrue(Menus.GateNavigation.IsTop, "the gate is on top of the stack");
            yield return Tap(_gamepad.buttonEast);
            yield return Frames(2);
            Assert.IsFalse(Menus.GateOpen, "Back cancels the gate");
            Assert.IsTrue(nav.IsTop);
            Assert.AreEqual(ScreenFocus.PauseSettings, nav.Focus);
            yield return Tap(_gamepad.buttonSouth);
            yield return Frames(2);
            Assert.IsTrue(Menus.GateOpen, "a fresh gate for the gear");
            Press(_gamepad.leftShoulder);
            yield return WaitReal(0.5f);
            Assert.IsTrue(Menus.Gate.IsZoneHeld(0), "the left bumper holds the first spot");
            Press(_gamepad.rightShoulder);
            yield return WaitReal(0.5f);
            Assert.IsTrue(Menus.Gate.IsZoneHeld(1), "the right bumper holds the second spot");
            yield return WaitReal(ParentGate.DefaultHoldSeconds + 0.3f);
            Release(_gamepad.leftShoulder);
            Release(_gamepad.rightShoulder);
            yield return Frames(2);
            Assert.IsTrue(Menus.SettingsOpen, "both bumpers pass the gate");
            Assert.IsTrue(Menus.SettingsNavigation.IsTop);
            yield return Tap(_gamepad.buttonEast);
            yield return Frames(2);
            Assert.IsFalse(Menus.SettingsOpen);
            Assert.IsTrue(nav.IsTop, "back on the pause card");
            Assert.AreEqual(ScreenFocus.PauseSettings, nav.Focus, "on the gear that opened the card");
            Assert.IsTrue(nav.Outline.Showing);
            Assert.AreEqual(GameState.Paused, Game.Flow.State);

            // Down to Resume, South: play.
            yield return Tap(_gamepad.dpad.down);
            Assert.AreEqual(ScreenFocus.PauseResume, nav.Focus);
            yield return Tap(_gamepad.buttonSouth);
            yield return Frames(2);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheTitleTheNoticeAndThePagesWorkByKeyboard()
        {
            CaseCutscenes.AutoPlayOpening = false;
            yield return Load();
            UseScheme(Scheme.Keyboard);
            yield return Frames(3);
            var title = Game.Ui.TitleNavigation;
            Assert.IsTrue(title.IsTop);
            Assert.AreEqual(ScreenFocus.TitleNewGame, title.Focus, "no save: New game");
            Assert.IsTrue(title.Outline != null && title.Outline.Showing);
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual(ScreenFocus.TitleLanguage, title.Focus);
            yield return Tap(_keyboard.escapeKey);
            Assert.AreEqual(ScreenFocus.TitleNewGame, title.Focus, "Back goes home to the first button, starts nothing");
            Assert.AreEqual(GameState.Title, Game.Flow.State);
            yield return Tap(_keyboard.upArrowKey);
            Assert.AreEqual(ScreenFocus.TitleSettings, title.Focus);

            // The gear by Enter: the gate (Escape cancels it), then Q and P held: the card.
            yield return Tap(_keyboard.enterKey);
            yield return Frames(2);
            Assert.IsTrue(Menus.GateOpen);
            yield return Tap(_keyboard.escapeKey);
            yield return Frames(2);
            Assert.IsFalse(Menus.GateOpen, "Escape cancels the gate");
            Assert.AreEqual(GameState.Title, Game.Flow.State);
            yield return Tap(_keyboard.enterKey);
            yield return Frames(2);
            Press(_keyboard.qKey);
            yield return WaitReal(0.3f);
            Press(_keyboard.pKey);
            yield return WaitReal(ParentGate.DefaultHoldSeconds + 0.5f);
            Release(_keyboard.qKey);
            Release(_keyboard.pKey);
            yield return Frames(2);
            Assert.IsTrue(Menus.SettingsOpen);

            // Credits by Enter: the sheet says it scrolls (fade, chevron and the page-down key), then Escape returns to
            // the card with focus on Credits.
            var card = Menus.SettingsNavigation;
            card.FocusOn(SettingsCard.Credits);
            yield return Tap(_keyboard.enterKey);
            yield return Frames(3);
            var sheet = Game.GrownUps.Credits.Sheet;
            Assert.IsTrue(sheet.IsOpen);
            Assert.Greater(sheet.ScrollRange, 0f, "the credits are taller than the screen");
            Assert.IsTrue(sheet.MoreBelowShowing, "the bottom edge says there is more");
            Assert.IsFalse(sheet.MoreAboveShowing);
            Assert.AreEqual("R", sheet.MoreBelowPrompt.KeyLabel, "the page-down key is shown");
            sheet.ScrollBy(sheet.ScrollRange + 100f);
            yield return Frames(2);
            Assert.IsFalse(sheet.MoreBelowShowing, "at the end the chevron goes");
            Assert.IsTrue(sheet.MoreAboveShowing, "and the top edge fades");
            yield return Tap(_keyboard.escapeKey);
            yield return Frames(2);
            Assert.IsFalse(sheet.IsOpen);
            Assert.IsTrue(Menus.SettingsOpen);
            Assert.AreEqual(SettingsCard.Credits, card.Focus, "back on the entry that opened the page");
            yield return Tap(_keyboard.escapeKey);
            yield return Frames(2);
            Assert.IsFalse(Menus.SettingsOpen);
            Assert.IsTrue(title.IsTop, "the title takes input back");
            Assert.AreEqual(ScreenFocus.TitleSettings, title.Focus);

            // New game by Enter; then the notebook: Escape closes it and never pauses the game.
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual(ScreenFocus.TitleNewGame, title.Focus);
            yield return Tap(_keyboard.enterKey);
            yield return Frames(3);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return Frames(2);
            Director.OpenNotebook();
            yield return Frames(2);
            Assert.IsTrue(Director.Notebook.IsOpen);
            Assert.IsTrue(Director.Notebook.PageRightPrompt.HasGlyph || Director.Notebook.PageCount < 2,
                "the page discs carry the page keys");
            yield return Tap(_keyboard.escapeKey);
            yield return Frames(3);
            Assert.IsFalse(Director.Notebook.IsOpen, "Escape closes the notebook");
            Assert.AreEqual(GameState.Playing, Game.Flow.State, "and does not pause the game in the same press");
            yield return Tap(_keyboard.escapeKey);
            yield return Frames(2);
            Assert.AreEqual(GameState.Paused, Game.Flow.State, "the next Escape pauses, as always");
        }

        /// <summary>Owner, 2026-11-24: the air verb is "Mergulhar"; it fits the Use disc's tag at full size.</summary>
        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheDiveVerbFitsTheUseDiscInPortuguese()
        {
            CaseCutscenes.AutoPlayOpening = false;
            LanguageController.DeviceLocalesOverride = new[] { "pt-BR" };
            yield return Load();
            Assert.AreEqual("pt-BR", Localizer.ActiveLocale);
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.5f);
            Game.Pip.Teleport(new Vector3(0f, 7f, -3f));
            float until = Time.time + 1f;
            while (Game.Ui.UseVerbKey != LocalizationKeys.HudDive && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(LocalizationKeys.HudDive, Game.Ui.UseVerbKey, "in the air the disc reads Dive");
            yield return null;
            var text = Game.Ui.UseButton.transform.Find("VerbTag/Text").GetComponent<Text>();
            Assert.AreEqual("Mergulhar", text.text);
            Canvas.ForceUpdateCanvases();
            var generator = new TextGenerator();
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            generator.Populate(text.text, settings);
            Assert.GreaterOrEqual(generator.characterCountVisible, 9, "every letter shows");
            Assert.AreEqual(1, generator.lineCount, "on one line");
            // Sizes here are in screen pixels (the canvas scale): best fit kept the largest size the label allows.
            Assert.AreEqual(UiFactory.CaptionSize, text.resizeTextMaxSize);
            Assert.AreEqual(UiFactory.CaptionSize, generator.fontSizeUsedForBestFit / settings.scaleFactor, 1.5f,
                "at the full caption size");

            // Every other verb the tag can show fits the same way (Levantar, Encaixar, Fantasiar…).
            foreach (string key in new[]
                     {
                         LocalizationKeys.HudRoll, LocalizationKeys.HudUse, "ui.verb.talk", "ui.verb.look", "ui.verb.sit",
                         "ui.verb.turn", "ui.verb.lift", "ui.verb.connect", "ui.verb.open", "ui.verb.knock", "ui.verb.dress_up",
                     })
            {
                string verb = Localizer.Get(key);
                var g = new TextGenerator();
                g.Populate(verb, text.GetGenerationSettings(text.rectTransform.rect.size));
                Assert.GreaterOrEqual(g.characterCountVisible, verb.Length, key + " (" + verb + ") shows whole");
                Assert.AreEqual(1, g.lineCount, key + " on one line");
            }
        }
    }
}
