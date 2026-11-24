using System;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Parent;
using SpookyStreet.Core.Save;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.App
{
    /// <summary>
    /// The grown-ups' corner (S004 stream E), behind the parent gate: the title's and the pause card's gear open the
    /// gate, then the settings card; from there the "For grown-ups" page, the credits and the language screen. It
    /// applies every stored preference the moment the card changes it (reduced motion, caption size and backing,
    /// camera speed and inversion, left-handed controls, vibration; the volumes stay with the audio) and keeps the
    /// local playtime ledger (<c>playtime.json</c>) that only the page reads. Nothing here touches the network.
    /// </summary>
    public sealed class GrownUpCorner : MonoBehaviour
    {
        /// <summary>The ledger is written at most this often while playing (and on pause, title and quit).</summary>
        public const float SaveEverySeconds = 30f;

        private GameBootstrap _game;
        private PlaytimeStore _store;
        private float _sinceSave;
        private bool _dirty;
        private bool _languageFromSettings;

        public PlaytimeLedger Playtime { get; private set; } = new PlaytimeLedger();

        public ParentPage ParentPage { get; private set; }

        public CreditsPage Credits { get; private set; }

        public NonverbalMenus Menus { get; private set; }

        /// <summary>The local clock the ledger and the page use (tests may set it).</summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        /// <summary>True while any grown-up screen is up (the pause key is ignored then: Back closes the screen).</summary>
        public bool AnyOpen =>
            (Menus != null && (Menus.GateOpen || Menus.SettingsOpen)) ||
            (ParentPage != null && ParentPage.IsOpen) || (Credits != null && Credits.IsOpen) ||
            (_game != null && _game.LanguageScreen != null && _game.LanguageScreen.IsOpen);

        public static GrownUpCorner Create(GameBootstrap game)
        {
            var go = new GameObject("GrownUpCorner");
            go.transform.SetParent(game.transform, false);
            var corner = go.AddComponent<GrownUpCorner>();
            corner.Initialize(game);
            return corner;
        }

        private void Initialize(GameBootstrap game)
        {
            _game = game;
            string directory = game.Director != null ? game.Director.SaveDirectory : null;
            if (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    _store = new PlaytimeStore(new DiskSaveFileSystem(), directory);
                    Playtime = _store.Load();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[GrownUpCorner] playtime: " + e.Message);
                    Playtime = new PlaytimeLedger();
                }
            }

            ParentPage = ParentPage.Create(BuildProgress);
            ParentPage.transform.SetParent(transform, false);
            Credits = CreditsPage.Create();
            Credits.transform.SetParent(transform, false);

            var layer = game.Director != null ? game.Director.Nonverbal : null;
            Menus = layer != null ? layer.Menus : null;
            if (Menus != null)
            {
                Menus.OpenGrownUps = ParentPage.Open;
                Menus.OpenCredits = Credits.Open;
                Menus.OpenLanguage = OpenLanguageFromSettings;
                ParentPage.Closed += Menus.ReturnToSettings;
                Credits.Closed += Menus.ReturnToSettings;
                var audioHandler = Menus.PreferencesChanged;
                Menus.PreferencesChanged = p =>
                {
                    audioHandler?.Invoke(p);
                    Apply(p);
                };
                if (Menus.Preferences == null && game.Audio != null)
                {
                    Menus.Preferences = game.Audio.Preferences;
                }

                game.Ui.OpenSettings = layer.OpenSettings;
            }

            if (game.LanguageScreen != null)
            {
                game.LanguageScreen.Closed += OnLanguageClosed;
            }

            var preferences = game.Audio != null ? game.Audio.Preferences : null;
            if (preferences != null)
            {
                ApplyAtBoot(preferences);
            }

            game.Flow.StateChanged += OnStateChanged;
        }

        private void OpenLanguageFromSettings()
        {
            if (_game.LanguageScreen == null)
            {
                Menus.ReturnToSettings();
                return;
            }

            _languageFromSettings = true;
            _game.LanguageScreen.Open();
        }

        private void OnLanguageClosed()
        {
            if (_languageFromSettings)
            {
                _languageFromSettings = false;
                Menus?.ReturnToSettings();
            }
        }

        /// <summary>The page's model, computed now from the case, street and keepsake saves and the ledger.</summary>
        public ParentProgressPage BuildProgress()
        {
            var director = _game.Director;
            if (director == null)
            {
                return ParentProgress.Build(null, null, null, null, Playtime, Clock());
            }

            return ParentProgress.Build(director.State, director.Answers,
                director.Street != null ? director.Street.State : null,
                director.Keepsakes != null ? director.Keepsakes.State : null, Playtime, Clock());
        }

        /// <summary>
        /// Boot: the saved preferences take effect. Reduced motion and left-handed only switch on here, so a build's
        /// own defaults (the scene's left-handed flag, a test's motion setting) are never switched off by a default file.
        /// </summary>
        private void ApplyAtBoot(PreferencesData p)
        {
            if (p.ReducedMotion)
            {
                AmbientMotion.ReducedMotion = true;
            }

            ApplyRest(p, _game.Ui.LeftHanded || p.LeftHanded);
            if (_game.Director != null && _game.Director.Nonverbal != null)
            {
                _game.Director.Nonverbal.ApplyMotion();
            }
        }

        /// <summary>A setting changed on the card: every visual and control preference follows it at once.</summary>
        public void Apply(PreferencesData p)
        {
            if (p == null)
            {
                return;
            }

            AmbientMotion.ReducedMotion = p.ReducedMotion;
            if (_game.Director != null && _game.Director.Nonverbal != null)
            {
                _game.Director.Nonverbal.ApplyMotion();
            }

            ApplyRest(p, p.LeftHanded);
        }

        private void ApplyRest(PreferencesData p, bool leftHanded)
        {
            var director = _game.Director;
            if (director != null)
            {
                if (director.Captions != null)
                {
                    director.Captions.LargeText = p.LargeCaptions;
                    director.Captions.Opaque = p.OpaqueCaptions;
                }

                if (director.Dialogue != null)
                {
                    director.Dialogue.LargeText = p.LargeCaptions;
                }
            }

            if (_game.CameraRig != null)
            {
                _game.CameraRig.InvertY = p.InvertY;
                _game.CameraRig.Sensitivity = p.CameraSensitivity;
            }

            _game.Ui.SetLeftHanded(leftHanded);
            GameHaptics.Enabled = p.Haptics;
        }

        // ------------------------------------------------------------------------------------- playtime

        private void Update()
        {
            bool playing = _game != null && _game.Flow.State == GameState.Playing;
            if (Playtime.Tick(Time.unscaledDeltaTime, playing, Clock()))
            {
                _dirty = true;
                _sinceSave += Time.unscaledDeltaTime;
                if (_sinceSave >= SaveEverySeconds)
                {
                    SavePlaytime();
                }
            }
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current != GameState.Playing)
            {
                SavePlaytime();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SavePlaytime();
            }
        }

        /// <summary>Writes the ledger now if it changed (tests call it too).</summary>
        public void SavePlaytime()
        {
            _sinceSave = 0f;
            if (!_dirty || _store == null)
            {
                return;
            }

            _dirty = false;
            try
            {
                _store.Save(Playtime);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GrownUpCorner] could not save playtime: " + e.Message);
            }
        }

        private void OnDestroy()
        {
            SavePlaytime();
            if (_game != null && _game.Flow != null)
            {
                _game.Flow.StateChanged -= OnStateChanged;
            }

            if (_game != null && _game.LanguageScreen != null)
            {
                _game.LanguageScreen.Closed -= OnLanguageClosed;
            }
        }
    }

    /// <summary>
    /// The vibration setting (S004 stream E). Every haptic the game ever plays must check <see cref="Enabled"/>; the
    /// game plays none yet, so today the switch only records the grown-up's choice.
    /// </summary>
    public static class GameHaptics
    {
        public static bool Enabled { get; set; } = true;
    }
}
