using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Audio;
using SpookyStreet.Case;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CaseLines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The approved effects wired into the real scene (S003): a walk on each surface plays its footsteps, jump, land
    /// and glide (cape, then the flutter loop) play, pausing holds the effects bus while the pause menu still taps,
    /// the arrival knocks start at the owl house on the attic light's beat, the wind wheel whirrs from the roof, a
    /// threshold plays the page turn and muffles the night bed indoors, the mechanism taps on its knocks, and the
    /// volume sliders drive the buses and persist. Batchmode has no audio device, so every check reads the rig's log
    /// and source state, never the output. Every test runs against a fresh save folder.
    /// </summary>
    public class GameAudioTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        /// <summary>Open sky above the plaza path (MovesetTests), clear of the maple, the pumpkins and the table.</summary>
        private static readonly Vector3 AbovePlaza = new Vector3(0f, 5.5f, -0.5f);

        private Keyboard _keyboard;
        private readonly List<GameObject> _patches = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            foreach (var patch in _patches)
            {
                if (patch != null)
                {
                    Object.Destroy(patch);
                }
            }

            _patches.Clear();
            AmbientMotion.ReducedMotion = false;
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static AudioRig Rig => GameBootstrap.Instance.Audio.Rig;

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
            Assert.IsNotNull(GameBootstrap.Instance.Audio, "the game builds its audio");
        }

        private static IEnumerator StartGame()
        {
            yield return LoadGame();
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
        }

        private static void RequireModels()
        {
            if (Game.Environment == null || !Game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed: the house is not staged.");
            }
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

        private IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        private static int Plays(string id) => Rig.CountPlays(id);

        private static AudioLogEntry? LastPlay(string id)
        {
            var log = Rig.Log;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                if (log[i].Kind == AudioLogKind.Play && log[i].EventId == id)
                {
                    return log[i];
                }
            }

            return null;
        }

        /// <summary>A flat 30 × 30 m floor far from the street, named like the colliders of a real surface.</summary>
        private Vector3 Patch(string name, float z)
        {
            var go = new GameObject(name);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(30f, 1f, 30f);
            go.transform.position = new Vector3(200f, -0.5f, z);
            _patches.Add(go);
            return new Vector3(200f, 0.05f, z);
        }

        [UnityTest]
        public IEnumerator AWalkOnEachSurfacePlaysItsFootsteps()
        {
            yield return StartGame();
            var audio = Game.Audio;

            // The real ground: the path is stone, the lawn grass, the arrival porch wood (and the sidewalk stone).
            Assert.AreEqual(FootstepSurface.Stone, audio.ProbeSurface(new Vector3(0f, 0.02f, -6f)), "the path");
            Assert.AreEqual(FootstepSurface.Grass, audio.ProbeSurface(new Vector3(8f, 0f, -8f)), "the lawn");
            Assert.AreEqual(FootstepSurface.Wood, audio.ProbeSurface(new Vector3(0f, 0.6f, -14.2f)), "the arrival porch");
            if (Game.Environment != null && Game.Environment.StreetSetAvailable)
            {
                Assert.AreEqual(FootstepSurface.Stone, audio.ProbeSurface(new Vector3(6f, 0.05f, EnvironmentLayout.SidewalkZ)),
                    "the sidewalk slabs");
            }

            var walks = new[]
            {
                (Name: "Lawn", Surface: FootstepSurface.Grass),
                (Name: "PathStones", Surface: FootstepSurface.Stone),
                (Name: "Porch_Deck", Surface: FootstepSurface.Wood),
            };
            float z = 0f;
            foreach (var walk in walks)
            {
                Vector3 start = Patch(walk.Name, z);
                z += 60f;
                Game.Pip.Teleport(start);
                yield return Wait(0.4f);
                Rig.ClearLog();
                int before = audio.Footsteps;
                Press(_keyboard.wKey);
                yield return Wait(1.2f);
                Release(_keyboard.wKey);
                yield return Frames(2);

                string expected = FootstepSurfaces.EventFor(walk.Surface);
                Assert.Greater(Game.Pip.DistanceWalked, 0f);
                Assert.GreaterOrEqual(Plays(expected), 3, $"steps on {walk.Name}");
                Assert.AreEqual(audio.Footsteps - before, Plays(expected), $"every step on {walk.Name} is {expected}");
                foreach (var other in new[] { AudioEvents.StepWood, AudioEvents.StepGrass, AudioEvents.StepStone })
                {
                    if (other != expected)
                    {
                        Assert.AreEqual(0, Plays(other), $"no {other} on {walk.Name}");
                    }
                }

                Assert.AreEqual(walk.Surface, audio.LastSurface);
                Assert.AreEqual(walk.Name, audio.LastGround.name);

                // Standing still (after the last stride of slowing down): no more steps.
                yield return Wait(0.4f);
                int standing = Plays(expected);
                yield return Wait(0.5f);
                Assert.AreEqual(standing, Plays(expected), "no steps while standing");
            }
        }

        [UnityTest]
        public IEnumerator JumpLandAndGlidePlayAndPauseHoldsTheEffectsBus()
        {
            yield return StartGame();
            var pip = Game.Pip;

            // Glide: the cape snaps open, then the flutter loop runs while gliding.
            pip.Teleport(AbovePlaza);
            pip.transform.rotation = Quaternion.identity;
            yield return Wait(0.25f);
            Rig.ClearLog();
            Press(_keyboard.spaceKey, queueEventOnly: true);
            yield return Wait(0.7f);
            Assert.IsTrue(pip.Gliding, "holding Jump while falling glides");
            Assert.AreEqual(1, Plays(AudioEvents.MoveGlideOpen), "the cape opens once");
            Assert.IsTrue(Rig.IsLooping(AudioEvents.MoveGlideLoop), "the flutter loop follows the snap");
            var open = LastPlay(AudioEvents.MoveGlideOpen).Value;
            var loop = LastPlay(AudioEvents.MoveGlideLoop).Value;
            Assert.GreaterOrEqual(loop.GameTime - open.GameTime, GameAudio.GlideLoopDelay - 0.02f);

            // Pause holds the effects (and the bed) where they are; the pause menu's own buttons still tap.
            Game.Flow.Pause();
            yield return null;
            Assert.IsTrue(Rig.GamePaused);
            Assert.IsTrue(Rig.IsBusPaused(AudioBus.Sfx), "pausing silences the effects bus");
            Assert.IsTrue(Rig.IsBusPaused(AudioBus.Ambience));
            Assert.IsTrue(Rig.IsHeld(AudioEvents.MoveGlideLoop), "the glide loop holds mid-flutter");
            Assert.IsTrue(Rig.IsHeld(AudioEvents.AmbNight), "the night bed holds");
            Assert.IsTrue(AudioListener.pause);
            Assert.IsNull(Rig.Play(AudioEvents.StepWood), "no game sound starts while paused");
            int taps = Plays(AudioEvents.UiTap);
            Game.Ui.ResumeButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(taps + 1, Plays(AudioEvents.UiTap), "the Resume button taps");
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
            Assert.IsFalse(Rig.IsBusPaused(AudioBus.Sfx));
            Assert.IsFalse(Rig.IsHeld(AudioEvents.MoveGlideLoop), "nothing is held after the pause");
            // Pausing disables gameplay input, so the held Jump may read released on resume: the loop follows the cape.
            Assert.AreEqual(pip.Gliding, Rig.IsLooping(AudioEvents.MoveGlideLoop), "the flutter follows the cape");

            // Folding the cape stops the loop; touching down plays the landing.
            Release(_keyboard.spaceKey, queueEventOnly: true);
            yield return Wait(0.3f);
            Assert.IsFalse(pip.Gliding);
            Assert.IsFalse(Rig.IsLooping(AudioEvents.MoveGlideLoop), "the loop stops with the glide");
            float deadline = Time.time + 3f;
            while (!pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            yield return Frames(2);
            Assert.IsTrue(pip.Grounded);
            Assert.AreEqual(1, Plays(AudioEvents.MoveLand), "one landing");

            // A jump from the ground: the push, then the landing after it.
            yield return Wait(0.4f);
            Rig.ClearLog();
            yield return Tap(_keyboard.spaceKey);
            Assert.AreEqual(1, Plays(AudioEvents.MoveJump), "the jump plays on take-off");
            deadline = Time.time + 3f;
            yield return Wait(0.2f);
            while (!pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            yield return Frames(2);
            Assert.AreEqual(1, Plays(AudioEvents.MoveLand), "and the landing");
            Assert.Greater(LastPlay(AudioEvents.MoveLand).Value.GameTime, LastPlay(AudioEvents.MoveJump).Value.GameTime);
            Assert.AreEqual(0, Plays(AudioEvents.MoveGlideOpen), "a tapped jump does not glide");
        }

        [UnityTest]
        public IEnumerator KnocksStartAtTheOwlHouseOnTheAtticLightsBeat()
        {
            yield return LoadGame();
            Rig.ClearLog();
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(1, Plays(AudioEvents.UiTap), "the New game button taps");
            Assert.IsTrue(Rig.IsLooping(AudioEvents.AmbNight), "the night bed plays outdoors");
            Assert.AreEqual(0f, Rig.BusTrimDb(AudioBus.Ambience), "full and open outdoors");

            var beat = Director.Knocks;
            CollectionAssert.AreEqual(AudioEvents.KnockOnsets, KnockBeat.KnockTimes, "light and sound share the onsets");
            float startTime = -1f;
            int startFrame = -1;
            beat.Started += () =>
            {
                startTime = Time.time;
                startFrame = Time.frameCount;
            };

            Press(_keyboard.wKey);
            float until = Time.time + 3f;
            while (startTime < 0f && Time.time < until)
            {
                yield return null;
            }

            Release(_keyboard.wKey);
            Assert.GreaterOrEqual(startTime, 0f, "the knock cue plays during the first steps");
            Assert.IsTrue(Director.State.Has(CaseFact.HeardKnocks));
            Assert.AreEqual(CaseLines.CaptionArrivalKnocks, Director.Captions.CurrentKey, "the caption stays");
            var knocks = LastPlay(AudioEvents.ClueKnocksDistant);
            Assert.IsTrue(knocks.HasValue, "the knocks play");
            Assert.AreEqual(1, Plays(AudioEvents.ClueKnocksDistant));
            Assert.AreEqual(startFrame, knocks.Value.Frame, "the sound starts on the beat's first frame");
            Assert.IsTrue(knocks.Value.Spatial, "a 3D source");
            Assert.Less(Vector3.Distance(beat.SoundPosition, knocks.Value.Position), 0.01f, "at the attic light");
            Assert.Less(Mathf.Abs(knocks.Value.Position.z - EnvironmentLayout.HouseZ), 6f, "at the owl house");
            Assert.Greater(knocks.Value.Position.y, 2f, "up at the attic");

            // The attic light pulses on each knock onset of the sound, and is calm in the pause before the last.
            foreach (float onset in AudioEvents.KnockOnsets)
            {
                while (Time.time - startTime < onset + 0.03f)
                {
                    yield return null;
                }

                if (Time.time - startTime < onset + 0.3f)
                {
                    Assert.Greater(beat.WarmLight.intensity, KnockBeat.SettledIntensity + 0.5f, $"pulse on the knock at {onset} s");
                }
            }

            // The wind keeps turning the wheel after the knocks: its whirr comes from the rooftop wheel.
            if (Game.Environment != null && Game.Environment.UsesModels && Game.Environment.WindWheel != null)
            {
                Assert.IsTrue(Director.Workshop.WindEnabled, "the wind cycles start with the knocks");
                until = Time.time + WorkshopView.WindIntervalSeconds + 3f;
                while (Plays(AudioEvents.ClueWindWheel) == 0 && Time.time < until)
                {
                    yield return null;
                }

                var wheel = LastPlay(AudioEvents.ClueWindWheel);
                Assert.IsTrue(wheel.HasValue, "a wind cycle whirrs the wheel");
                Assert.IsTrue(wheel.Value.Spatial);
                Assert.Less(Vector3.Distance(Game.Environment.WindWheel.position, wheel.Value.Position), 0.01f, "at the wheel");
                Assert.AreEqual(0, Plays(AudioEvents.ClueCamTap), "the mechanism inside is not heard from the street");
            }
        }

        [UnityTest]
        public IEnumerator AThresholdTurnsThePageAndMufflesTheBedIndoors()
        {
            yield return StartGame();
            RequireModels();
            var travel = Director.Travel;
            Assert.IsNotNull(travel);

            // In by June's window (CaseInteriorTests.EnterByWindow).
            Game.Pip.Teleport(new Vector3(-4.3f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            yield return Wait(0.8f);
            Assert.IsTrue(travel.Tracker.Armed);
            Rig.ClearLog();
            Game.Pip.Teleport(new Vector3(-3.36f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            float until = Time.time + 1f;
            while (!travel.Active && Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(travel.Active, "the page turns");
            Assert.AreEqual(1, Plays(AudioEvents.UiPageTurn), "the page-turn sound plays with it");
            until = Time.time + 2.5f;
            while (travel.Active && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(1, Plays(AudioEvents.UiPageTurn), "once per crossing");
            Assert.IsTrue(Game.Audio.Indoors, "Pip is inside the owl house");
            yield return Wait(1.0f);
            Assert.AreEqual(AudioMix.IndoorAmbienceDb, Rig.BusTrimDb(AudioBus.Ambience), 0.01f, "the bed is quieter inside");
            Assert.AreEqual(AudioMix.IndoorAmbienceLowPassHz, Rig.BusLowPassHz(AudioBus.Ambience), 5f, "and muffled");
            Assert.IsTrue(Rig.IsLooping(AudioEvents.AmbNight), "but still there");
            Assert.AreEqual(FootstepSurface.Wood, Game.Audio.ProbeSurface(Game.Pip.transform.position), "boards indoors");

            // The mechanism taps on its knock contacts, from the cam, in its rhythm.
            var workshop = Director.Workshop;
            Assert.IsNotNull(workshop);
            yield return Wait(Mathf.Max(0f, MechanismMotion.CycleSeconds - 0.1f)); // let any wind cycle finish
            while (workshop.Playing)
            {
                yield return null;
            }

            Rig.ClearLog();
            workshop.WindEnabled = false;
            workshop.PlayCycle();
            yield return Wait(MechanismMotion.CycleSeconds + 0.2f);
            var taps = new List<AudioLogEntry>();
            foreach (var entry in Rig.Log)
            {
                if (entry.Kind == AudioLogKind.Play && entry.EventId == AudioEvents.ClueCamTap)
                {
                    taps.Add(entry);
                }
            }

            Assert.AreEqual(MechanismMotion.KnockTimes.Length, taps.Count, "one tap per knock contact");
            for (int i = 1; i < taps.Count; i++)
            {
                float expected = MechanismMotion.KnockTimes[i] - MechanismMotion.KnockTimes[i - 1];
                Assert.AreEqual(expected, taps[i].GameTime - taps[i - 1].GameTime, 0.1f, $"tap {i} on its knock");
            }

            Assert.IsTrue(taps[0].Spatial);
            if (workshop.Cam != null)
            {
                Assert.Less(Vector3.Distance(workshop.Cam.position, taps[0].Position), 0.01f, "at the cam");
            }

            Assert.AreEqual(0, Plays(AudioEvents.ClueWindWheel), "the rooftop wheel is not heard indoors");
        }

        /// <summary>Drives the world through the reducer (DeductionNotebookTests.Do).</summary>
        private static IEnumerator Do(params CaseAction[] actions)
        {
            foreach (var action in actions)
            {
                Director.Perform(action);
                Director.Dialogue.Skip();
                Director.Cutaway.Skip();
                yield return Frames(2);
            }

            Director.Captions.Clear();
            yield return Wait(0.4f);
        }

        private static Vector2 ScreenOf(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.position);

        /// <summary>A touch drag through the sticker's own handlers (DeductionNotebookTests.Drag).</summary>
        private static IEnumerator Drag(string stickerId, int slot)
        {
            var notebook = Director.Notebook;
            var sticker = notebook.TraySticker(stickerId);
            Assert.IsNotNull(sticker, stickerId + " is in the tray");
            var data = new PointerEventData(EventSystem.current) { position = ScreenOf(sticker.Rect), button = PointerEventData.InputButton.Left };
            data.pressPosition = data.position;
            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.beginDragHandler);
            yield return null;
            Vector2 target = ScreenOf(notebook.Slots[slot].rectTransform);
            for (int i = 1; i <= 4; i++)
            {
                data.position = Vector2.Lerp(data.pressPosition, target, i / 4f);
                ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.dragHandler);
                yield return null;
            }

            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.endDragHandler);
            yield return Frames(2);
        }

        private static IEnumerator Verdict()
        {
            float until = Time.time + 6f;
            while (Director.Notebook.Checking && Time.time < until)
            {
                yield return null;
            }

            Assert.IsFalse(Director.Notebook.Checking, "the verdict arrives");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NotebookStickersPageAndVerdictsPlayTheirSounds()
        {
            yield return StartGame();
            yield return Do(CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
                CaseAction.Of(CaseActionKind.SitOnBench), CaseAction.Of(CaseActionKind.ExamineStreamer),
                CaseAction.Of(CaseActionKind.ExamineCloth), CaseAction.Of(CaseActionKind.EnterAttic),
                CaseAction.Of(CaseActionKind.ExamineSill));
            Director.OpenNotebook();
            yield return Frames(2);
            var notebook = Director.Notebook;
            Assert.IsTrue(notebook.IsOpen);
            Assert.GreaterOrEqual(notebook.PageCount, 2);
            int entry = notebook.PageQuestions.IndexOf(N.Question(N.QuestionEntry));
            Assert.GreaterOrEqual(entry, 0);

            // The page flip: once per turn, never for the page it is already on.
            Rig.ClearLog();
            int other = entry == 0 ? 1 : 0;
            notebook.ShowPage(other);
            yield return Wait(0.3f);
            Assert.AreEqual(1, Plays(AudioEvents.UiPageTurn), "turning a page flips paper");
            notebook.ShowPage(entry);
            yield return Wait(0.5f);
            Assert.AreEqual(2, Plays(AudioEvents.UiPageTurn));
            notebook.ShowPage(entry);
            yield return null;
            Assert.AreEqual(2, Plays(AudioEvents.UiPageTurn), "no flip without a turn");

            // A wrong set by tap-tap: a peel per pick, a press per placement, then one wobble home.
            Rig.ClearLog();
            foreach (string id in new[] { N.ValeDoor, N.Cloth, N.Streamer })
            {
                notebook.TapSticker(id);
                yield return Wait(0.1f);
                notebook.TapSlot(notebook.CurrentBoard.FirstEmpty());
                yield return Wait(0.1f);
            }

            Assert.AreEqual(3, Plays(AudioEvents.NoteStickerPick), "lifting a sticker peels");
            Assert.AreEqual(3, Plays(AudioEvents.NoteStickerDrop), "placing one presses it in");
            Assert.AreEqual(0, Plays(AudioEvents.NoteStickerWobble), "nothing before the verdict");
            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Wrong, notebook.LastCheck.Verdict);
            Assert.AreEqual(1, Plays(AudioEvents.NoteStickerWobble), "the wrong set wobbles home");
            Assert.AreEqual(0, Plays(AudioEvents.NoteCorrectGlow));
            Assert.AreEqual(0, Plays(AudioEvents.UiTap), "slots and stickers play their own sounds, not the tap");

            // The right set, the first sticker by a touch drag: the glow comes before the comic.
            Rig.ClearLog();
            yield return Drag(N.SillSnag, notebook.CurrentBoard.FirstEmpty());
            Assert.AreEqual(1, Plays(AudioEvents.NoteStickerPick), "the drag lifts it");
            Assert.AreEqual(1, Plays(AudioEvents.NoteStickerDrop), "and drops it on the slot");
            yield return Wait(0.1f);
            foreach (string id in new[] { N.ValeDoor, N.BeaFlash })
            {
                notebook.TapSticker(id);
                yield return Wait(0.1f);
                notebook.TapSlot(notebook.CurrentBoard.FirstEmpty());
                yield return Wait(0.1f);
            }

            float placedAt = Time.time;
            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Right, notebook.LastCheck.Verdict);
            Assert.AreEqual(1, Plays(AudioEvents.NoteCorrectGlow), "the right set glows");
            Assert.AreEqual(0, Plays(AudioEvents.NoteStickerWobble));
            Assert.Greater(LastPlay(AudioEvents.NoteCorrectGlow).Value.GameTime, placedAt - 0.01f);
            Assert.Greater(notebook.ComicPanels.Count, 0, "then the comic");
        }

        [UnityTest]
        public IEnumerator VolumeSlidersDriveTheBusesAndPersist()
        {
            yield return LoadGame();
            var audio = Game.Audio;
            var menus = Director.Nonverbal.Menus;
            Assert.AreSame(audio.Preferences, menus.Preferences, "the sliders edit the game's preferences");
            menus.OpenSettings();
            Assert.AreEqual(1f, menus.SoundsSlider.value);
            Assert.AreEqual(0f, Rig.GetVolume(AudioMix.SfxVol));

            menus.SoundsSlider.value = 0.5f;
            menus.MusicSlider.value = 0.25f;
            menus.VoicesSlider.value = 0f;
            Assert.AreEqual(AudioMix.SliderToDb(0.5f), Rig.GetVolume(AudioMix.SfxVol), 1e-3f);
            Assert.AreEqual(AudioMix.SliderToDb(0.5f), Rig.GetVolume(AudioMix.AmbienceVol), 1e-3f, "the bed follows the sounds slider");
            Assert.AreEqual(AudioMix.SliderToDb(0.25f), Rig.GetVolume(AudioMix.MusicVol), 1e-3f);
            Assert.AreEqual(AudioMix.MinDb, Rig.GetVolume(AudioMix.VoiceVol));

            // Reduced motion is a visual setting: it changes nothing in the mix.
            AmbientMotion.ReducedMotion = true;
            yield return Frames(3);
            Assert.AreEqual(AudioMix.SliderToDb(0.5f), Rig.GetVolume(AudioMix.SfxVol), 1e-3f);
            Assert.AreEqual(0f, Rig.BusTrimDb(AudioBus.Ambience));
            AmbientMotion.ReducedMotion = false;

            menus.CloseSettings();
            yield return new WaitForSecondsRealtime(GameAudio.SaveDelay + 0.4f);

            yield return LoadGame();
            Assert.AreEqual(0.5f, Game.Audio.Preferences.EffectsVolume, 1e-4f, "saved through the preferences store");
            Assert.AreEqual(0.25f, Game.Audio.Preferences.MusicVolume, 1e-4f);
            Assert.AreEqual(0f, Game.Audio.Preferences.DialogueVolume, 1e-4f);
            Assert.AreEqual(AudioMix.SliderToDb(0.5f), Rig.GetVolume(AudioMix.SfxVol), 1e-3f, "applied on boot");
            Assert.AreEqual(AudioMix.MinDb, Rig.GetVolume(AudioMix.VoiceVol));
            Director.Nonverbal.Menus.OpenSettings();
            Assert.AreEqual(0.5f, Director.Nonverbal.Menus.SoundsSlider.value, 1e-4f, "the slider shows the saved level");
            Assert.AreEqual(0.25f, Director.Nonverbal.Menus.MusicSlider.value, 1e-4f);
        }
    }
}
