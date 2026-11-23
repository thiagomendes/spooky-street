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

        /// <summary>Kick off a wall that is behind Pip (S003); ends on the JumpAir pose.</summary>
        public const string WallKick = "WallKick";

        /// <summary>Catching a ledge (S003); ends on <see cref="LedgeHang"/> frame 0.</summary>
        public const string LedgeGrab = "LedgeGrab";

        /// <summary>Pulling up and over a ledge in place, timed to the motor's climb (S003); ends on Idle frame 0.</summary>
        public const string LedgeClimb = "LedgeClimb";

        /// <summary>The hint glance to Pip's left (S003a, D26): head and shoulders turn that way, she peers, and
        /// comes back to Idle frame 0. It says "that way", toward an area; it never points at a thing.</summary>
        public const string GlanceLeft = "GlanceLeft";

        /// <summary>The hint glance to Pip's right (S003a); see <see cref="GlanceLeft"/>.</summary>
        public const string GlanceRight = "GlanceRight";

        /// <summary>How far her face turns from straight ahead in a glance clip, degrees (pip_anims.GLANCE_TURN).</summary>
        public const float GlanceTurn = 89f;

        /// <summary>Ground speed above which a glance does not start, and at which a playing one ends (m/s).</summary>
        public const float GlanceMaxSpeed = 0.6f;

        /// <summary>Every clip the game knows, in a stable order (the animator's mixer inputs follow it).</summary>
        public static readonly string[] All =
        {
            Idle, Walk, Run, JumpStart, JumpAir, Fall, Land, Glide, WallSlide, LedgeHang, Roll, Interact,
            WallKick, LedgeGrab, LedgeClimb, GlanceLeft, GlanceRight,
        };

        /// <summary>True for the two hint glance clips (exporter take prefixes are ignored).</summary>
        public static bool IsGlance(string clipName)
        {
            string name = CleanClipName(clipName);
            return name == GlanceLeft || name == GlanceRight;
        }

        /// <summary>
        /// The glance clip for a place seen from Pip: <see cref="GlanceLeft"/> when it lies to her left,
        /// <see cref="GlanceRight"/> when to her right (dead ahead and dead behind count as right). Ground-plane
        /// vectors in Unity's axes (x right, z forward): her facing, and the offset from her to the place. Null when
        /// either vector is too short to have a direction.
        /// </summary>
        public static string GlanceToward(float forwardX, float forwardZ, float toX, float toZ)
        {
            const float tiny = 1e-6f;
            if (forwardX * forwardX + forwardZ * forwardZ < tiny || toX * toX + toZ * toZ < tiny)
            {
                return null;
            }

            // y of forward x to: negative when the place is to her left (Unity is left-handed, y up)
            float side = forwardZ * toX - forwardX * toZ;
            return side < 0f ? GlanceLeft : GlanceRight;
        }

        /// <summary>
        /// The signed angle from Pip's facing to a place, degrees in (-180, 180], positive to her right; 0 when either
        /// vector is too short. A glance reads best between about 30 and 150 degrees to one side: nearer ahead the
        /// place is already in view, and behind her she should turn around first.
        /// </summary>
        public static float GlanceAngle(float forwardX, float forwardZ, float toX, float toZ)
        {
            const float tiny = 1e-6f;
            if (forwardX * forwardX + forwardZ * forwardZ < tiny || toX * toX + toZ * toZ < tiny)
            {
                return 0f;
            }

            double side = forwardZ * toX - forwardX * toZ;
            double along = forwardX * toX + forwardZ * toZ;
            return (float)(Math.Atan2(side, along) * 180.0 / Math.PI);
        }

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
        /// (one-shots and Land are simply skipped; a missing wall kick or climb replays the take-off, as before those
        /// clips existed; a missing catch goes straight to the hang). Chains always end at <see cref="Idle"/> or null.
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
                case WallKick: return JumpStart;
                case LedgeClimb: return JumpStart;
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
