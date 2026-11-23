using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Audio
{
    /// <summary>One babble to play: the voice event, its take (1-based phrase) and the length the caption asked for.</summary>
    public readonly struct BabblePlan
    {
        public BabblePlan(string eventId, int take, float targetSeconds, float takeSeconds, BabbleEmotion emotion)
        {
            EventId = eventId;
            Take = take;
            TargetSeconds = targetSeconds;
            TakeSeconds = takeSeconds;
            Emotion = emotion;
        }

        public string EventId { get; }

        public int Take { get; }

        public float TargetSeconds { get; }

        /// <summary>Length of the chosen phrase take.</summary>
        public float TakeSeconds { get; }

        public BabbleEmotion Emotion { get; }

        public bool IsValid => EventId != null && Take > 0;

        public override string ToString() => $"{EventId} t{Take} ({TakeSeconds:0.00} s for {TargetSeconds:0.00} s)";
    }

    /// <summary>
    /// The runtime babble rule (D20.2), ported from the Python reference <c>babble.plan_sequence</c> and
    /// <c>babble.caption_syllables</c> (tools/audio/spookyaudio/babble.py; the golden captions in the tests are checked
    /// against it by tools/audio/tests/test_import.py):
    /// <list type="number">
    /// <item>count the caption's syllables (vowel groups per word, a silent final e dropped, at least one per word);</item>
    /// <item>length = 0.6 × syllables ÷ the character's syllable rate, clamped to 0.6–3 s (babble is a flavour; the
    /// text line carries the words, D23.1);</item>
    /// <item>play the phrase take of the line's emotion closest to that length (the main path).</item>
    /// </list>
    /// Variety (<see cref="BabblePicker"/>): when the closest take is the one this voice played last and another take
    /// is nearly as close, the other plays, so a repeated line or two short lines in a row never sound identical. The
    /// syllable banks are not used at runtime (phrase takes were rated more natural, docs/S003.1-voices-music.md).
    /// </summary>
    public static class BabblePlanner
    {
        public const float LengthRatio = 0.6f;
        public const float MinSeconds = 0.6f;
        public const float MaxSeconds = 3f;

        /// <summary>A take this much further from the target than the best one still counts as "nearly as close".</summary>
        public const float VarietyToleranceSeconds = 0.35f;

        /// <summary>Rough spoken-syllable count of a caption (babble.caption_syllables): at least 1.</summary>
        public static int CaptionSyllables(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1;
            }

            int n = 0;
            int i = 0;
            while (i < text.Length)
            {
                if (!IsWordChar(text[i]))
                {
                    i++;
                    continue;
                }

                int start = i;
                while (i < text.Length && IsWordChar(text[i]))
                {
                    i++;
                }

                n += WordSyllables(text.Substring(start, i - start));
            }

            return Math.Max(1, n);
        }

        /// <summary>The babble length for a caption: 0.6 × syllables ÷ rate, clamped to 0.6–3 s.</summary>
        public static float TargetSeconds(string text, float syllableRate)
        {
            if (syllableRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(syllableRate));
            }

            float target = LengthRatio * CaptionSyllables(text) / syllableRate;
            return target < MinSeconds ? MinSeconds : target > MaxSeconds ? MaxSeconds : target;
        }

        /// <summary>The 1-based take whose length is closest to <paramref name="target"/> (the first on a tie).</summary>
        public static int ClosestTake(IReadOnlyList<float> takeSeconds, float target) => ClosestTake(takeSeconds, target, 0);

        /// <summary>
        /// As <see cref="ClosestTake(IReadOnlyList{float}, float)"/>, but when the best take is <paramref name="avoid"/>
        /// and the runner-up is within <see cref="VarietyToleranceSeconds"/> of the best distance, the runner-up wins.
        /// </summary>
        public static int ClosestTake(IReadOnlyList<float> takeSeconds, float target, int avoid)
        {
            if (takeSeconds == null || takeSeconds.Count == 0)
            {
                throw new ArgumentException("no takes", nameof(takeSeconds));
            }

            int best = -1, second = -1;
            for (int i = 0; i < takeSeconds.Count; i++)
            {
                float d = Math.Abs(takeSeconds[i] - target);
                if (best < 0 || d < Math.Abs(takeSeconds[best] - target))
                {
                    second = best;
                    best = i;
                }
                else if (second < 0 || d < Math.Abs(takeSeconds[second] - target))
                {
                    second = i;
                }
            }

            if (avoid == best + 1 && second >= 0 &&
                Math.Abs(takeSeconds[second] - target) - Math.Abs(takeSeconds[best] - target) <= VarietyToleranceSeconds)
            {
                return second + 1;
            }

            return best + 1;
        }

        /// <summary>The phrase lengths of a character's emotion (null for an unknown character).</summary>
        public static IReadOnlyList<float> PhraseSeconds(string characterId, BabbleEmotion emotion)
        {
            int c = VoiceMusicEvents.CharacterIndex(characterId);
            return c < 0 ? null : VoiceMusicData.PhraseSeconds[c][(int)emotion];
        }

        /// <summary>The syllable rate of a character (syllables per second), or 0 for an unknown one.</summary>
        public static float SyllableRate(string characterId)
        {
            int c = VoiceMusicEvents.CharacterIndex(characterId);
            return c < 0 ? 0f : VoiceMusicData.SyllableRates[c];
        }

        /// <summary>The main path, with no variety: the closest take of the emotion for this caption.</summary>
        public static BabblePlan Plan(Speaker speaker, string text, BabbleEmotion emotion) => Plan(speaker, text, emotion, 0);

        internal static BabblePlan Plan(Speaker speaker, string text, BabbleEmotion emotion, int avoid)
        {
            string character = VoiceMusicEvents.CharacterId(speaker);
            if (character == null)
            {
                return default;
            }

            float target = TargetSeconds(text, SyllableRate(character));
            return PlanFor(speaker, target, emotion, avoid);
        }

        /// <summary>The take for a wanted length (clamped to 0.6–3 s), avoiding take <paramref name="avoid"/> when another is nearly as close.</summary>
        public static BabblePlan PlanFor(Speaker speaker, float targetSeconds, BabbleEmotion emotion, int avoid)
        {
            string character = VoiceMusicEvents.CharacterId(speaker);
            if (character == null)
            {
                return default;
            }

            float target = targetSeconds < MinSeconds ? MinSeconds : targetSeconds > MaxSeconds ? MaxSeconds : targetSeconds;
            var takes = PhraseSeconds(character, emotion);
            int take = ClosestTake(takes, target, avoid);
            return new BabblePlan(VoiceMusicEvents.VoiceId(speaker, emotion), take, target, takes[take - 1], emotion);
        }

        private static bool IsWordChar(char c) =>
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= 'À' && c <= 'ÿ') || c == '\'';

        private static bool IsVowel(char c) => "aeiouyàáâãéêíóôõúü".IndexOf(c) >= 0;

        private static int WordSyllables(string word)
        {
            string w = word.ToLowerInvariant();
            int groups = 0;
            bool inGroup = false;
            foreach (char c in w)
            {
                bool vowel = IsVowel(c);
                if (vowel && !inGroup)
                {
                    groups++;
                }

                inGroup = vowel;
            }

            if (w.EndsWith("e", StringComparison.Ordinal) && groups > 1 && !w.EndsWith("le", StringComparison.Ordinal) &&
                !w.EndsWith("ee", StringComparison.Ordinal))
            {
                groups--;
            }

            return Math.Max(1, groups);
        }
    }

    /// <summary>
    /// Picks the babble for each line (<see cref="BabblePlanner"/>), remembering the last take of every voice event so
    /// the same phrase does not play twice in a row when another is nearly as long.
    /// </summary>
    public sealed class BabblePicker
    {
        private readonly Dictionary<string, int> _last = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The babble for a line: <paramref name="text"/> is the caption as shown (its length sets the babble's).</summary>
        public BabblePlan Next(Speaker speaker, string text, BabbleEmotion emotion)
        {
            string id = VoiceMusicEvents.VoiceId(speaker, emotion);
            if (id == null)
            {
                return default;
            }

            _last.TryGetValue(id, out int last);
            var plan = BabblePlanner.Plan(speaker, text, emotion, last);
            _last[id] = plan.Take;
            return plan;
        }

        /// <summary>A babble of a given length with no caption (cutscene cues).</summary>
        public BabblePlan NextFor(Speaker speaker, float seconds, BabbleEmotion emotion)
        {
            string id = VoiceMusicEvents.VoiceId(speaker, emotion);
            if (id == null)
            {
                return default;
            }

            _last.TryGetValue(id, out int last);
            var plan = BabblePlanner.PlanFor(speaker, seconds, emotion, last);
            _last[id] = plan.Take;
            return plan;
        }
    }
}
