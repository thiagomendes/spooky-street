namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>An axis-aligned box in one UI space (canvas or world units, y up).</summary>
    public readonly struct UiBox
    {
        public UiBox(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public float XMin { get; }
        public float YMin { get; }
        public float XMax { get; }
        public float YMax { get; }

        public float CenterX => (XMin + XMax) * 0.5f;

        /// <summary>A square of side <paramref name="size"/> centred on (x, y).</summary>
        public static UiBox Around(float x, float y, float size) =>
            new UiBox(x - size * 0.5f, y - size * 0.5f, x + size * 0.5f, y + size * 0.5f);

        public bool Overlaps(UiBox other) =>
            XMin < other.XMax && other.XMin < XMax && YMin < other.YMax && other.YMin < YMax;

        public override string ToString() => $"({XMin:F0}, {YMin:F0})-({XMax:F0}, {YMax:F0})";
    }

    /// <summary>
    /// Where the notebook's Inspect glyph rides the focus outline (D32, docs/S004-sticker-memory.md §6): on a corner the
    /// open memory card cannot cover. The card hangs over the page from above, so the outline's top corners are the ones
    /// it hides (frame 92 showed only the lit West button peeking out); the bottom-left corner comes first, then the top
    /// corners. The bottom-right corner belongs to the Submit glyph (NotebookView.Prompts), so Inspect never takes it. When the whole outline is under the card (a slot sticker's memory), the
    /// glyph sits just under the card's lower edge, level with the outline, so the button that puts the memory away
    /// still shows.
    /// </summary>
    public static class GlyphPlacement
    {
        /// <summary>Space kept between the glyph and the card's edge in the fallback.</summary>
        public const float CardGap = 4f;

        /// <summary>
        /// The glyph's centre for an outline <paramref name="outline"/>, with the memory card at <paramref name="cover"/>
        /// (null when no memory is open) and a glyph <paramref name="glyphSize"/> wide.
        /// </summary>
        public static (float X, float Y) Inspect(UiBox outline, UiBox? cover, float glyphSize)
        {
            var corners = new[]
            {
                (outline.XMin, outline.YMin), // bottom-left
                (outline.XMin, outline.YMax), // top-left
                (outline.XMax, outline.YMax), // top-right
            };
            if (cover == null)
            {
                return corners[0];
            }

            foreach (var corner in corners)
            {
                if (!UiBox.Around(corner.Item1, corner.Item2, glyphSize).Overlaps(cover.Value))
                {
                    return corner;
                }
            }

            return (outline.CenterX, cover.Value.YMin - CardGap - glyphSize * 0.5f);
        }
    }
}
