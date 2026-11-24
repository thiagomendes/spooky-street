using System;
using UnityEngine;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Credits (S004 §2.5), behind the parent gate beside the grown-ups' page: the original-art note (every picture,
    /// model and sound made for the game by script), where the voices and music come from (D20.2,
    /// docs/asset-provenance.md), the two fonts under the SIL Open Font License with the licence texts in full, as
    /// published (English, from <c>Resources/Licenses</c>), and the tools. No links: addresses in the licence are plain text.
    /// </summary>
    public sealed class CreditsPage : MonoBehaviour
    {
        /// <summary>The licence files shipped in the app (Resources paths), shown verbatim.</summary>
        public static readonly string[] Licences = { "Licenses/Andika-OFL", "Licenses/Sniglet-OFL" };

        private bool _built;

        public ReadingSheet Sheet { get; private set; }

        public bool IsOpen => Sheet != null && Sheet.IsOpen;

        /// <summary>How many licence texts were found and shown (tests).</summary>
        public int LicencesShown { get; private set; }

        public event Action Closed;

        public static CreditsPage Create()
        {
            var sheet = ReadingSheet.Create("CreditsPage", Keys.CreditsHeading);
            var page = sheet.gameObject.AddComponent<CreditsPage>();
            page.Sheet = sheet;
            sheet.Closed += () => page.Closed?.Invoke();
            return page;
        }

        public void Open()
        {
            if (!_built)
            {
                Build();
            }

            Sheet.Open();
        }

        public void Close() => Sheet.Close();

        private void Build()
        {
            _built = true;
            Sheet.Paragraph(Keys.CreditsArt);
            Sheet.Paragraph(Keys.CreditsVoices, 30, null, null, 8f);
            Sheet.Paragraph(Keys.CreditsTools, 30, null, null, 8f);
            Sheet.Paragraph(Keys.CreditsFonts, 30, null, null, 8f);
            LicencesShown = 0;
            foreach (string path in Licences)
            {
                var asset = Resources.Load<TextAsset>(path);
                if (asset == null)
                {
                    Debug.LogWarning("[CreditsPage] missing Resources/" + path);
                    continue;
                }

                Sheet.Spacer(16f);
                var rule = UiFactory.Rect(Sheet.Content, "Rule");
                rule.gameObject.AddComponent<LayoutElement>().preferredHeight = 3f;
                rule.gameObject.AddComponent<Image>().color = UiFactory.WithAlpha(Palette.HauntedPurple, 0.35f);
                // Licence text is shown as published, untranslated (it is a legal text, not UI copy).
                var text = Sheet.Paragraph(null, 26, ReadingSheet.Graphite);
                text.text = asset.text.Replace("\r\n", "\n").Trim();
                LicencesShown++;
            }
        }
    }
}
