using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Cutscenes;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Rewatching cutscenes from the notebook (D28, narrative bible §5): a felt "watch" disc with Pip's picture in the
    /// notebook's free top-left corner plays the opening again from any page, and once the case is stamped the case
    /// page (the final card) gets a second disc with the owl's picture that plays the reveal. Keyboard V and gamepad
    /// North watch too (the reveal on the case page, the opening elsewhere), so every control scheme can rewatch (D25).
    /// A companion of <see cref="NotebookView"/>: it reads the view and adds its controls without changing the pages.
    /// </summary>
    public sealed class NotebookRewatch : MonoBehaviour
    {
        private NotebookView _view;
        private GameObject _finalCard;
        private InputActionMap _input;
        private InputAction _watch;

        /// <summary>The cutscenes the case page offers now (<see cref="CutscenePolicy.Rewatchable"/>).</summary>
        public Func<IReadOnlyList<string>> Rewatchable { get; set; }

        /// <summary>Play this cutscene again (the director closes the notebook and reopens it after).</summary>
        public Action<string> Watch { get; set; }

        public Button OpeningButton { get; private set; }

        /// <summary>The reveal's disc on the case page (null until the final card is built).</summary>
        public Button RevealButton { get; private set; }

        public static NotebookRewatch Attach(NotebookView view)
        {
            var rewatch = view.gameObject.AddComponent<NotebookRewatch>();
            rewatch._view = view;
            var content = view.transform.Find("Content");
            rewatch.OpeningButton = Disc(content != null ? content : view.transform, "WatchOpening", "pg.pip",
                new Vector2(0f, 1f), new Vector2(62f, -58f), Keys.WatchOpening, () => rewatch.Play(C01Cutscenes.OpeningId));
            rewatch._input = new InputActionMap("NotebookRewatch");
            rewatch._watch = rewatch._input.AddAction("Watch", InputActionType.Button, "<Keyboard>/v");
            rewatch._watch.AddBinding("<Gamepad>/buttonNorth");
            return rewatch;
        }

        private static Button Disc(Transform parent, string name, string picture, Vector2 anchor, Vector2 position, string key,
            UnityEngine.Events.UnityAction onClick)
        {
            var disc = UiFactory.FeltDisc(parent, name, UiSpriteCatalog.FeltDiscNight, Palette.CandyCornYellow,
                UiSpriteCatalog.IconPlay, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, anchor, position, 1f);
            // The picture says which story it is; the small play triangle in the corner says "watch".
            disc.transform.Find("Icon")?.gameObject.SetActive(false);
            var art = UiFactory.Sprite(disc.transform, "Picture", PuppetAtlas.Get(picture), Color.white, new Vector2(0.5f, 0.5f),
                new Vector2(-6f, 4f), new Vector2(62f, 62f));
            art.preserveAspect = true;
            art.raycastTarget = false;
            var play = UiFactory.Sprite(disc.transform, "Play", UiFactory.Baked(UiSpriteCatalog.IconPlay), Palette.CandyCornYellow,
                new Vector2(1f, 0f), new Vector2(-22f, 22f), new Vector2(34f, 34f));
            play.raycastTarget = false;
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            AccessibleName.Set(disc.gameObject, key);
            return button;
        }

        private bool Offers(string id)
        {
            var list = Rewatchable != null ? Rewatchable() : null;
            if (list == null)
            {
                return false;
            }

            foreach (string s in list)
            {
                if (s == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Plays <paramref name="id"/> again when the case page offers it (tests call this like a press).</summary>
        public bool Play(string id)
        {
            if (!Offers(id) || Watch == null)
            {
                return false;
            }

            Watch(id);
            return true;
        }

        private void Update()
        {
            bool open = _view != null && _view.IsOpen;
            if (open != _input.enabled)
            {
                if (open)
                {
                    _input.Enable();
                }
                else
                {
                    _input.Disable();
                }
            }

            if (!open)
            {
                return;
            }

            bool opening = Offers(C01Cutscenes.OpeningId);
            if (OpeningButton.gameObject.activeSelf != opening)
            {
                OpeningButton.gameObject.SetActive(opening);
            }

            if (_view.FinalCard != _finalCard)
            {
                // The notebook rebuilt its pages: the new case page gets its own reveal disc.
                _finalCard = _view.FinalCard;
                RevealButton = null;
                if (_finalCard != null && Offers(C01Cutscenes.RevealId))
                {
                    RevealButton = Disc(_finalCard.transform, "WatchReveal", "pg.owl", new Vector2(1f, 0f), new Vector2(-74f, 70f),
                        Keys.WatchReveal, () => Play(C01Cutscenes.RevealId));
                }
            }

            if (_watch.WasPressedThisFrame())
            {
                bool onCasePage = _view.FinalCard != null && _view.CurrentQuestion == null && _view.CurrentPage == _view.PageCount - 1;
                Play(onCasePage ? C01Cutscenes.RevealId : C01Cutscenes.OpeningId);
            }
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }
    }
}
