using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// A modal paper card for save notices and confirmations (resume notice, "Start a new case?", "Replay case?"):
    /// night-violet scrim, stitched card, optional heading, body, one orange primary action and an optional paper
    /// secondary action. Lives on its own canvas above the title and the notebook.
    /// </summary>
    public sealed class NoticeCard : MonoBehaviour
    {
        private Image _card;
        private Text _heading;
        private Text _body;
        private Button _primary;
        private Button _secondary;
        private UnityAction _onPrimary;
        private UnityAction _onSecondary;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Body key of the notice on screen (tests).</summary>
        public string BodyKey { get; private set; }

        public Button PrimaryButton => _primary;

        public Button SecondaryButton => _secondary;

        public static NoticeCard Create(Transform canvas)
        {
            var scrim = UiFactory.Panel(canvas, "NoticeBackdrop", UiFactory.WithAlpha(Palette.NightViolet, 0.8f));
            var notice = scrim.gameObject.AddComponent<NoticeCard>();
            notice.Build(scrim.rectTransform);
            scrim.gameObject.SetActive(false);
            return notice;
        }

        private void Build(RectTransform root)
        {
            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            _card = UiFactory.PaperCard(content, "NoticeCard", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(700f, 400f), -1f);
            var top = new Vector2(0.5f, 1f);
            _heading = UiFactory.Label(_card.transform, "Heading", null, 44, TextAnchor.MiddleCenter, UiFactory.Ink,
                UiFactory.DisplayFont);
            UiFactory.Place(_heading.rectTransform, top, new Vector2(0f, -66f), new Vector2(620f, 80f));
            _heading.gameObject.AddComponent<LocalizedText>();
            _body = UiFactory.Label(_card.transform, "Body", null, UiFactory.BodySize, TextAnchor.MiddleCenter,
                UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(_body.rectTransform, top, new Vector2(0f, -170f), new Vector2(620f, 130f));
            _body.gameObject.AddComponent<LocalizedText>();

            var bottom = new Vector2(0.5f, 0f);
            _secondary = UiFactory.SecondaryButton(_card.transform, "Secondary", null, bottom, new Vector2(-160f, 78f),
                new Vector2(290f, 96f), () => _onSecondary?.Invoke());
            _secondary.GetComponent<CraftButton>().Label.gameObject.AddComponent<LocalizedText>();
            _primary = UiFactory.PrimaryButton(_card.transform, "Primary", null, bottom, new Vector2(160f, 78f),
                new Vector2(290f, 104f), () => _onPrimary?.Invoke());
            _primary.GetComponent<CraftButton>().Label.gameObject.AddComponent<LocalizedText>();
        }

        /// <summary>
        /// Shows the card. <paramref name="headingKey"/> and <paramref name="secondaryKey"/> may be null. Each action
        /// closes the card first, then runs its callback.
        /// </summary>
        public void Show(string headingKey, string bodyKey, string primaryKey, UnityAction onPrimary,
            string secondaryKey = null, UnityAction onSecondary = null)
        {
            BodyKey = bodyKey;
            _onPrimary = () =>
            {
                Hide();
                onPrimary?.Invoke();
            };
            _onSecondary = () =>
            {
                Hide();
                onSecondary?.Invoke();
            };

            bool hasHeading = !string.IsNullOrEmpty(headingKey);
            _heading.gameObject.SetActive(hasHeading);
            if (hasHeading)
            {
                _heading.GetComponent<LocalizedText>().SetKey(headingKey);
            }

            _body.GetComponent<LocalizedText>().SetKey(bodyKey);
            _body.rectTransform.anchoredPosition = new Vector2(0f, hasHeading ? -170f : -120f);
            _card.rectTransform.sizeDelta = new Vector2(700f, hasHeading ? 400f : 330f);

            _primary.GetComponent<CraftButton>().Label.GetComponent<LocalizedText>().SetKey(primaryKey);
            bool hasSecondary = !string.IsNullOrEmpty(secondaryKey);
            _secondary.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                _secondary.GetComponent<CraftButton>().Label.GetComponent<LocalizedText>().SetKey(secondaryKey);
            }

            ((RectTransform)_primary.transform).anchoredPosition = new Vector2(hasSecondary ? 160f : 0f, 78f);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_primary.gameObject);
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
