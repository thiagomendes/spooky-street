using System;
using System.Collections.Generic;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Audio
{
    /// <summary>
    /// How each spoken case 01 line sounds: the babble emotion of its speaker (D20.2). Data, keyed by the line's
    /// localization key, so a translation never changes the voice and new lines only add a row. The emotion follows what
    /// the speaker feels as they say it (Bea sure and excited, Vale calm, June whispering from her hiding place), never
    /// what the line means for the case (D26). Captions have no voice. Every spoken line of <see cref="Case.CaseScript"/>
    /// has a row (tested); a key without one plays neutral.
    /// </summary>
    public static class LineVoices
    {
        private static readonly Dictionary<string, BabbleEmotion> Emotions = new Dictionary<string, BabbleEmotion>(StringComparer.Ordinal)
        {
            // Pip looking at things: curious musing, a smile at the candy.
            { Keys.ExamineStreamer, BabbleEmotion.Thinking },
            { Keys.ExamineCloth, BabbleEmotion.Thinking },
            { Keys.ExamineSill, BabbleEmotion.Thinking },
            { Keys.ExamineOwlPanel, BabbleEmotion.Thinking },
            { Keys.ExamineOwlBooth, BabbleEmotion.Thinking },
            { Keys.ExamineDoorStuck, BabbleEmotion.Neutral },
            { Keys.ExamineBowl, BabbleEmotion.Happy },
            { Keys.ExamineBowlSweet, BabbleEmotion.Happy },

            // Bea: fast and sure of what she saw.
            { Keys.BeaSawCape, BabbleEmotion.Surprised },
            { Keys.BeaRepeat, BabbleEmotion.Neutral },
            { Keys.BeaDoorOpen, BabbleEmotion.Surprised },
            { Keys.BeaClosed, BabbleEmotion.Happy },

            // Mr. Vale: slow and dry.
            { Keys.ValeDoorShut, BabbleEmotion.Neutral },
            { Keys.ValeInviteSit, BabbleEmotion.Neutral },
            { Keys.ValeRepeat, BabbleEmotion.Neutral },
            { Keys.ValeWasShut, BabbleEmotion.Neutral },
            { Keys.ValeOpenNow, BabbleEmotion.Thinking },
            { Keys.ValeClosed, BabbleEmotion.Happy },
            { Keys.ValeSavedSweet, BabbleEmotion.Happy },

            // June: whispers while she hides and listens, bright once the door gives.
            { Keys.JuneGreet, BabbleEmotion.Whisper },
            { Keys.JuneGreetShortcut, BabbleEmotion.Whisper },
            { Keys.JuneDoorSticks, BabbleEmotion.Neutral },
            { Keys.JuneDoorAlreadyOpen, BabbleEmotion.Surprised },
            { Keys.JuneLatchReminder, BabbleEmotion.Whisper },
            { Keys.JuneLatchThanks, BabbleEmotion.Happy },
            { Keys.JuneListenFloor, BabbleEmotion.Whisper },
            { Keys.JuneGoOutside, BabbleEmotion.Happy },
            { Keys.JuneResolve, BabbleEmotion.Surprised },
            { Keys.JuneClosed, BabbleEmotion.Happy },

            // Aunt Kit: warm and theatrical.
            { Keys.KitFirstShow, BabbleEmotion.Happy },
            { Keys.KitAsk, BabbleEmotion.Thinking },
            { Keys.KitThanks, BabbleEmotion.Happy },

            // The street remembers (S003a): glad to see Pip again; Kit a little wistful about the owl's old friends.
            { Keys.BeaRemembers, BabbleEmotion.Happy },
            { Keys.ValeRemembers, BabbleEmotion.Happy },
            { Keys.JuneRemembers, BabbleEmotion.Happy },
            { Keys.KitRemembers, BabbleEmotion.Thinking },
        };

        /// <summary>The line keys with a voice row.</summary>
        public static IEnumerable<string> LineKeys => Emotions.Keys;

        public static bool Has(string key) => key != null && Emotions.ContainsKey(key);

        /// <summary>The babble emotion of a line (neutral for a key without a row).</summary>
        public static BabbleEmotion For(string key) =>
            key != null && Emotions.TryGetValue(key, out var emotion) ? emotion : BabbleEmotion.Neutral;
    }
}
