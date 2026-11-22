using System.Collections.Generic;
using SpookyStreet.Core.Art;
using UnityEngine;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Loads the baked UI craft sprites (felt, paper, stitches, icons, logo) from <c>Resources/UI</c> by their
    /// <see cref="UiSpriteCatalog"/> names. A missing sprite falls back to the procedural <see cref="IconFactory"/>
    /// shape so the game stays usable, and is recorded in <see cref="Missing"/> so tests fail loudly.
    /// </summary>
    public static class UiSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly List<string> MissingNames = new List<string>();

        /// <summary>Catalogued sprites that failed to load since start-up (fallbacks were drawn instead).</summary>
        public static IReadOnlyList<string> Missing => MissingNames;

        /// <summary>The baked sprite, or null when it is not available (no fallback, nothing recorded).</summary>
        public static Sprite TryLoad(string name)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(UiSpriteCatalog.ResourcePath(name));
            if (sprite != null)
            {
                Cache[name] = sprite;
            }

            return sprite;
        }

        /// <summary>The baked sprite, or a procedural stand-in (recorded in <see cref="Missing"/>).</summary>
        public static Sprite Get(string name)
        {
            var sprite = TryLoad(name);
            if (sprite != null)
            {
                return sprite;
            }

            if (!MissingNames.Contains(name))
            {
                MissingNames.Add(name);
                Debug.LogWarning($"[UiSprites] Resources/{UiSpriteCatalog.ResourcePath(name)} missing; drawing a " +
                                 "procedural fallback. Run tools/ui_mock/ui_mock.py --export.");
            }

            return Fallback(name);
        }

        private static Sprite Fallback(string name)
        {
            switch (name)
            {
                case UiSpriteCatalog.FeltDiscNight:
                case UiSpriteCatalog.FeltDiscOrange:
                case UiSpriteCatalog.FeltDiscBone:
                    return IconFactory.Disc;
                case UiSpriteCatalog.StitchRing:
                    return IconFactory.Ring(0.04f);
                case UiSpriteCatalog.StitchRect:
                    return null;
                case UiSpriteCatalog.StickChevron:
                    return IconFactory.StickArrow;
                case UiSpriteCatalog.IconJump:
                    return IconFactory.ChevronUp;
                case UiSpriteCatalog.IconPause:
                    return IconFactory.PauseBars;
                case UiSpriteCatalog.IconGlobe:
                    return IconFactory.Globe;
                case UiSpriteCatalog.PaperCard:
                case UiSpriteCatalog.PaperTag:
                case UiSpriteCatalog.FeltButtonOrange:
                    return IconFactory.RoundedRect;
                default:
                    return IconFactory.Disc;
            }
        }
    }
}
