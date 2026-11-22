using System;

namespace SpookyStreet.Core.Animation
{
    /// <summary>
    /// The clip contract between Pip's Blender export (tools/blender) and the game: clip names, which ones loop,
    /// how exporter take names are cleaned, and which clip stands in when an optional one is missing. Shared by
    /// the import rules (editor) and the runtime animator so both agree on one list.
    /// </summary>
    public static class PipClips
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Run = "Run";
        public const string JumpStart = "JumpStart";
        public const string JumpAir = "JumpAir";
        public const string Fall = "Fall";
        public const string Land = "Land";
        public const string Glide = "Glide";
        public const string WallSlide = "WallSlide";
        public const string LedgeHang = "LedgeHang";
        public const string Roll = "Roll";
        public const string Interact = "Interact";

        /// <summary>Every clip the game knows, in a stable order (the animator's mixer inputs follow it).</summary>
        public static readonly string[] All =
        {
            Idle, Walk, Run, JumpStart, JumpAir, Fall, Land, Glide, WallSlide, LedgeHang, Roll, Interact,
        };

        /// <summary>Clips that cycle; everything else plays once and holds its last frame.</summary>
        public static readonly string[] Looping =
        {
            Idle, Walk, Run, JumpAir, Fall, Glide, WallSlide, LedgeHang,
        };

        /// <summary>Without this clip the model cannot be animated at all; the rest have stand-ins.</summary>
        public const string Required = Idle;

        /// <summary>
        /// Blender's FBX exporter names takes "Armature|Action" (sometimes "Object|Armature|Action", or with a
        /// trailing "|BaseLayer"); the game looks clips up by the bare action name. Whitespace is trimmed.
        /// </summary>
        public static string CleanClipName(string takeName)
        {
            if (string.IsNullOrEmpty(takeName))
            {
                return string.Empty;
            }

            string[] parts = takeName.Split('|');
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string part = parts[i].Trim();
                if (part.Length > 0 && !string.Equals(part, "BaseLayer", StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return takeName.Trim();
        }

        public static bool IsLooping(string clipName) => Array.IndexOf(Looping, CleanClipName(clipName)) >= 0;

        public static bool IsKnown(string clipName) => Array.IndexOf(All, CleanClipName(clipName)) >= 0;

        /// <summary>
        /// The clip to try next when <paramref name="clipName"/> is missing, or null when nothing stands in
        /// (one-shots and Land are simply skipped). Chains always end at <see cref="Idle"/> or null.
        /// </summary>
        public static string Fallback(string clipName)
        {
            switch (clipName)
            {
                case Run: return Walk;
                case Walk: return Idle;
                case JumpStart: return JumpAir;
                case JumpAir: return Fall;
                case Fall: return Idle;
                case Glide: return Fall;
                case WallSlide: return Fall;
                case LedgeHang: return Idle;
                default: return null;
            }
        }

        /// <summary>
        /// Follows <see cref="Fallback"/> from <paramref name="clipName"/> until <paramref name="has"/> accepts a
        /// clip; returns null if none of the chain exists.
        /// </summary>
        public static string Resolve(string clipName, Func<string, bool> has)
        {
            if (has == null)
            {
                throw new ArgumentNullException(nameof(has));
            }

            for (string name = clipName; name != null; name = Fallback(name))
            {
                if (has(name))
                {
                    return name;
                }
            }

            return null;
        }
    }
}
