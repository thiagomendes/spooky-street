using SpookyStreet.Core.World;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Case
{
    /// <summary>Where a sound caption's direction cue points.</summary>
    public enum SoundSource
    {
        /// <summary>No direction cue.</summary>
        None,

        /// <summary>The owl house's lit attic window, seen from the street (the arrival knocks).</summary>
        AtticWindow,

        /// <summary>The workshop under the hall floor (the floor hum, and the knocks heard from inside the house).</summary>
        Workshop,
    }

    /// <summary>
    /// Captions describe what Pip perceives where she is (D26). The arrival knocks are a street sound: on the street
    /// they come "from the blue house", from its attic window. Inside the owl house's rooms (a separate place up the
    /// lane, D24.1) the same three knocks are felt through the floor from the workshop below, so they are captioned as
    /// the floor hum, pointing at the workshop, never as a street sound "to the right" (S002 attic framing, frames
    /// 37–39). A sound caption still showing when Pip crosses a threshold belongs to the place she left and is dropped.
    /// The story beat itself (the knocks are heard once, raising "Where are the knocks coming from?") is unchanged:
    /// only the perception of it follows Pip. Pure.
    /// </summary>
    public static class CaptionPerception
    {
        /// <summary>True when Pip at (x, z) is in the owl house's rooms (the closed interior, D24.1).</summary>
        public static bool Indoors(float x, float z) => CaseRouteLayout.Inside(x, z);

        /// <summary>The caption key a sound gets where Pip hears it.</summary>
        public static string KeyFor(string key, bool indoors) =>
            indoors && key == Lines.CaptionArrivalKnocks ? Lines.CaptionFoyerHum : key;

        /// <summary>Where the direction cue of the (perceived) caption <paramref name="key"/> points.</summary>
        public static SoundSource SourceFor(string key, bool indoors)
        {
            string heard = KeyFor(key, indoors);
            if (heard == Lines.CaptionArrivalKnocks)
            {
                return SoundSource.AtticWindow;
            }

            return heard == Lines.CaptionFoyerHum ? SoundSource.Workshop : SoundSource.None;
        }

        /// <summary>Whether the caption <paramref name="key"/> still holds once Pip has crossed a threshold.</summary>
        public static bool HeardAcrossThreshold(string key) => !CaptionDirection.IsDirectionalSound(key);
    }
}
