using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// Which dialogue portrait a line shows (S004 §2.6): the speaker's badge in the line's babble emotion
    /// (<see cref="LineVoices"/>), so a translation never changes the face and a line without a voice row shows the
    /// neutral face. Captions and narration have no portrait. Engine-free; the UI cuts the sprite from the rect.
    /// </summary>
    public static class LinePortraits
    {
        /// <summary>The portrait of <paramref name="line"/>, or false for a caption (or a speaker without art).</summary>
        public static bool TryGet(ScriptLine line, out PortraitRect rect) => TryGet(line.Speaker, line.Key, out rect);

        public static bool TryGet(Speaker speaker, string key, out PortraitRect rect)
        {
            if (CaseSpeakers.IsCaption(speaker))
            {
                rect = default;
                return false;
            }

            return PortraitAtlasLayout.TryGet(speaker, LineVoices.For(key), out rect);
        }

        /// <summary>The portrait sprite name of a line (<c>portrait.bea.surprised</c>), or null for a caption.</summary>
        public static string SpriteName(ScriptLine line) => TryGet(line, out var rect) ? rect.Id : null;
    }
}
