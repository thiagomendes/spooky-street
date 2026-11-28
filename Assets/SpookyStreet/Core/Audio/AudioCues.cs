using System;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Audio
{
    /// <summary>What a music hook asks for (<see cref="AudioCues.MusicRequested"/>).</summary>
    public enum MusicRequestKind
    {
        /// <summary>Play a cue now (<see cref="MusicRequest.CueId"/>): the sting, the reveal or a loop to jump to.</summary>
        PlayCue,

        /// <summary>End the sting or reveal early; the loop comes back.</summary>
        StopCue,

        /// <summary>Fade all music out and keep it silent until <see cref="Resume"/> or a cue.</summary>
        StopAll,

        /// <summary>End a <see cref="StopAll"/> silence.</summary>
        Resume,

        /// <summary>Hold the music lower (<see cref="MusicRequest.Ducked"/>), e.g. under a cutscene's babble.</summary>
        Duck,
    }

    /// <summary>One music request.</summary>
    public readonly struct MusicRequest
    {
        public MusicRequest(MusicRequestKind kind, string cueId = null, float fadeSeconds = 0f, bool ducked = false)
        {
            Kind = kind;
            CueId = cueId;
            FadeSeconds = fadeSeconds;
            Ducked = ducked;
        }

        public MusicRequestKind Kind { get; }

        /// <summary>The music event id (resolved, see <see cref="AudioCues.ResolveMusic"/>).</summary>
        public string CueId { get; }

        public float FadeSeconds { get; }
        public bool Ducked { get; }

        public override string ToString() => Kind + " " + CueId;
    }

    /// <summary>One babble request with no caption (<see cref="AudioCues.BabbleRequested"/>).</summary>
    public readonly struct BabbleRequest
    {
        public BabbleRequest(Speaker speaker, BabbleEmotion emotion, float seconds)
        {
            Speaker = speaker;
            Emotion = emotion;
            Seconds = seconds;
        }

        public Speaker Speaker { get; }
        public BabbleEmotion Emotion { get; }

        /// <summary>The wanted length (the closest phrase take plays; clamped to 0.6–3 s).</summary>
        public float Seconds { get; }
    }

    /// <summary>
    /// Fire-and-forget sound hooks for code that should not know about the audio rig (S003): the notebook rebuild on
    /// <c>feature/s002.5a-deduction</c> calls these when a sticker is picked up, pressed into a slot, wobbles back from
    /// a wrong set, or a question glows solved. The game's <c>GameAudio</c> listens to <see cref="Requested"/> and
    /// plays the event through the rig; with nothing listening (EditMode tests, a scene without the rig) the calls do
    /// nothing. Engine-free, so Core code may call it too.
    /// <para>
    /// Music and babble hooks for the cutscene system (docs/S003-voices-music-wiring.md §"Hooks for cutscenes"):
    /// <see cref="PlayCue"/> takes a music id (<c>music_reveal</c>, <c>music_case_closed</c>, <c>music_street_theme</c>,
    /// <c>music_exploration</c>), a cutscene placeholder id (<c>music.c01.reveal</c>, <c>music.c01.opening</c>…) or a
    /// babble placeholder (<c>babble.c01.bea.hello</c>); <see cref="StopCue"/>, <see cref="StopMusic"/>,
    /// <see cref="ResumeMusic"/> and <see cref="DuckMusic"/> steer the score; <see cref="Babble"/> plays a speaker's
    /// babble of a given emotion and length.
    /// </para>
    /// </summary>
    public static class AudioCues
    {
        /// <summary>Raised with the event id of every cue (see <see cref="AudioEvents"/>).</summary>
        public static event Action<string> Requested;

        /// <summary>Raised for every music hook.</summary>
        public static event Action<MusicRequest> MusicRequested;

        /// <summary>Raised for every babble hook.</summary>
        public static event Action<BabbleRequest> BabbleRequested;

        /// <summary>A babble cue with no length plays about this long (a short exclamation).</summary>
        public const float DefaultBabbleSeconds = 1.2f;

        /// <summary>A sticker lifts off the tray or a slot (peel).</summary>
        public static void StickerPick() => Raise(AudioEvents.NoteStickerPick);

        /// <summary>A sticker is pressed into a question slot.</summary>
        public static void StickerDrop() => Raise(AudioEvents.NoteStickerDrop);

        /// <summary>A wrong set: the stickers wobble back (friendly, never a buzzer; D26).</summary>
        public static void StickerWobble() => Raise(AudioEvents.NoteStickerWobble);

        /// <summary>A question is answered: the warm glow.</summary>
        public static void CorrectGlow() => Raise(AudioEvents.NoteCorrectGlow);

        /// <summary>A notebook page turns (the same paper turn as the thresholds).</summary>
        public static void PageTurn() => Raise(AudioEvents.UiPageTurn);

        /// <summary>A tap on a control that is not a uGUI Button (buttons get their tap automatically).</summary>
        public static void UiTap() => Raise(AudioEvents.UiTap);

        /// <summary>A doorbell on the living street rings (a neighbour's door or Pip's own button): "ding-dong".</summary>
        public static void Doorbell() => Raise(AudioEvents.StreetDoorbell);

        /// <summary>Any catalog event by id (for hooks not listed above).</summary>
        public static void Play(string eventId) => Raise(eventId);

        /// <summary>
        /// Plays a music cue or a babble placeholder by id (see the class summary). Returns false (and does nothing)
        /// for an id that is neither.
        /// </summary>
        public static bool PlayCue(string cueId)
        {
            string music = ResolveMusic(cueId);
            if (music != null)
            {
                MusicRequested?.Invoke(new MusicRequest(MusicRequestKind.PlayCue, music));
                return true;
            }

            if (TryResolveBabble(cueId, out var speaker, out var emotion))
            {
                Babble(speaker, emotion, DefaultBabbleSeconds);
                return true;
            }

            return false;
        }

        /// <summary>Ends the sting or the reveal early (the loop comes back in).</summary>
        public static void StopCue(float fadeSeconds = MusicPlan.CueOutSeconds) =>
            MusicRequested?.Invoke(new MusicRequest(MusicRequestKind.StopCue, fadeSeconds: fadeSeconds));

        /// <summary>Fades all music out and keeps it silent until <see cref="ResumeMusic"/> or <see cref="PlayCue"/>.</summary>
        public static void StopMusic(float fadeSeconds = 1f) =>
            MusicRequested?.Invoke(new MusicRequest(MusicRequestKind.StopAll, fadeSeconds: fadeSeconds));

        /// <summary>Ends a <see cref="StopMusic"/> silence: the loop for where Pip is fades back in.</summary>
        public static void ResumeMusic() => MusicRequested?.Invoke(new MusicRequest(MusicRequestKind.Resume));

        /// <summary>Holds the music <see cref="MusicPlan.HeldDuckDb"/> lower while <paramref name="ducked"/> (voice lines duck it anyway).</summary>
        public static void DuckMusic(bool ducked) =>
            MusicRequested?.Invoke(new MusicRequest(MusicRequestKind.Duck, ducked: ducked));

        /// <summary>A speaker's babble with no caption, about <paramref name="seconds"/> long (cutscenes).</summary>
        public static void Babble(Speaker speaker, BabbleEmotion emotion, float seconds = DefaultBabbleSeconds)
        {
            if (VoiceMusicEvents.CharacterId(speaker) != null)
            {
                BabbleRequested?.Invoke(new BabbleRequest(speaker, emotion, seconds));
            }
        }

        /// <summary>
        /// The music event for an id: a music event id as is, or a cutscene placeholder (<c>music.c01.reveal</c> → the
        /// reveal, <c>music.c01.case_closed</c> → the sting, <c>music.c01.opening</c> and <c>music.c01.theme</c> → the
        /// street theme from its start, <c>music.c01.explore</c> → exploration). Null for anything else.
        /// </summary>
        public static string ResolveMusic(string cueId)
        {
            if (string.IsNullOrEmpty(cueId)) return null;
            if (VoiceMusicEvents.IsMusic(cueId)) return cueId;
            if (!cueId.StartsWith("music.", StringComparison.Ordinal)) return null;
            string last = cueId.Substring(cueId.LastIndexOf('.') + 1);
            switch (last)
            {
                case "reveal": return VoiceMusicEvents.MusicReveal;
                case "case_closed":
                case "closed":
                case "sting": return VoiceMusicEvents.MusicCaseClosed;
                case "opening":
                case "theme":
                case "street": return VoiceMusicEvents.MusicStreetTheme;
                case "explore":
                case "exploration": return VoiceMusicEvents.MusicExploration;
                default: return null;
            }
        }

        /// <summary>
        /// A babble placeholder <c>babble.&lt;case&gt;.&lt;character&gt;.&lt;word&gt;</c> (as the cutscenes write them) to its
        /// speaker and emotion: hello, cheer, yay, laugh → happy; gasp, wow, oh → surprised; hmm → thinking; psst,
        /// shh → whisper; anything else neutral.
        /// </summary>
        public static bool TryResolveBabble(string cueId, out Speaker speaker, out BabbleEmotion emotion)
        {
            speaker = Speaker.Caption;
            emotion = BabbleEmotion.Neutral;
            if (string.IsNullOrEmpty(cueId) || !cueId.StartsWith("babble.", StringComparison.Ordinal)) return false;
            string[] parts = cueId.Split('.');
            if (parts.Length < 3) return false;
            string character = parts[parts.Length >= 4 ? parts.Length - 2 : parts.Length - 1];
            string word = parts.Length >= 4 ? parts[parts.Length - 1] : string.Empty;
            switch (character)
            {
                case "pip": speaker = Speaker.Pip; break;
                case "bea": speaker = Speaker.Bea; break;
                case "vale": speaker = Speaker.Vale; break;
                case "june": speaker = Speaker.June; break;
                case "kit": speaker = Speaker.Kit; break;
                default: return false;
            }

            switch (word)
            {
                case "hello":
                case "hi":
                case "cheer":
                case "yay":
                case "laugh":
                case "happy":
                    emotion = BabbleEmotion.Happy;
                    break;
                case "gasp":
                case "wow":
                case "oh":
                case "surprised":
                    emotion = BabbleEmotion.Surprised;
                    break;
                case "hmm":
                case "think":
                case "thinking":
                    emotion = BabbleEmotion.Thinking;
                    break;
                case "psst":
                case "shh":
                case "whisper":
                    emotion = BabbleEmotion.Whisper;
                    break;
            }

            return true;
        }

        /// <summary>Removes every listener (test teardown, scene unload).</summary>
        public static void ClearListeners()
        {
            Requested = null;
            MusicRequested = null;
            BabbleRequested = null;
        }

        private static void Raise(string eventId) => Requested?.Invoke(eventId);
    }
}
