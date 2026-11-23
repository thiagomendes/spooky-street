using System;
using System.Collections.Generic;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Keepsakes;

namespace SpookyStreet.Core.Keepsakes
{
    /// <summary>One old sketchbook page: every 10 paper bats piece one together (S004 §2.1).</summary>
    public sealed class SketchPage
    {
        internal SketchPage(string id, int order, string captionKey, string picture)
        {
            Id = id;
            Order = order;
            CaptionKey = captionKey;
            Picture = picture;
        }

        /// <summary>Stable ID (never display text).</summary>
        public string Id { get; }

        /// <summary>1–3: completed at 10, 20 and 30 bats.</summary>
        public int Order { get; }

        /// <summary>The caption under the drawing (describes what is drawn, never what it means).</summary>
        public string CaptionKey { get; }

        /// <summary>The drawing's texture name under <c>Resources/UI/SketchPages</c> (stream B2 bakes it).</summary>
        public string Picture { get; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// The three pages, in order. Spoiler rule (S004 §2.1): a page can be earned before any question is solved, so no
    /// page shows the owl machine, the cam or arm, the treehouse route, the side yard or the bowing owl; pages are about
    /// the Players, the festival and the next place, and their art carries no text.
    /// </summary>
    public static class SketchPages
    {
        public static readonly SketchPage Lanterns =
            new SketchPage("keepsake.page.c01.lanterns", 1, Keys.PageLanterns, "page_lanterns");

        public static readonly SketchPage Friends =
            new SketchPage("keepsake.page.c01.friends", 2, Keys.PageFriends, "page_friends");

        public static readonly SketchPage Moon = new SketchPage("keepsake.page.c01.moon", 3, Keys.PageMoon, "page_moon");

        public static readonly IReadOnlyList<SketchPage> All = new[] { Lanterns, Friends, Moon };

        public static bool TryGet(string id, out SketchPage page)
        {
            foreach (var p in All)
            {
                if (string.Equals(p.Id, id, StringComparison.Ordinal))
                {
                    page = p;
                    return true;
                }
            }

            page = null;
            return false;
        }
    }
}
