namespace SpookyStreet.Core.Audio
{
    /// <summary>
    /// The S003 first sound set (D20), rendered by <c>tools/audio/build.py</c>. Ids, take counts and markers must match
    /// <c>tools/audio/spookyaudio/sounds.py</c> (checked by <c>tools/audio/tests/test_sounds.py</c>).
    /// The owner approved the effects (D20.1); they are imported (<see cref="AudioImportPlan.Approved"/>) and wired by
    /// <c>Runtime/Audio/GameAudio.cs</c>. The S003 formant voices and plucked motif below were rejected and are never
    /// imported; their v2 replacements (D20.2) are added by <see cref="VoiceMusicEvents.AddTo"/>.
    /// </summary>
    public static class AudioEvents
    {
        public const string ClueKnocksDistant = "clue_knocks_distant";
        public const string ClueFloorHum = "clue_floor_hum";
        public const string ClueCamTap = "clue_cam_tap";
        public const string ClueWindWheel = "clue_wind_wheel";
        public const string StepWood = "step_wood";
        public const string StepGrass = "step_grass";
        public const string StepStone = "step_stone";
        public const string MoveJump = "move_jump";
        public const string MoveLand = "move_land";
        public const string MoveGlideOpen = "move_glide_open";
        public const string MoveGlideLoop = "move_glide_loop";
        public const string UiPageTurn = "ui_page_turn";
        public const string NoteStickerPick = "note_sticker_pick";
        public const string NoteStickerDrop = "note_sticker_drop";
        public const string NoteStickerWobble = "note_sticker_wobble";
        public const string NoteCorrectGlow = "note_correct_glow";
        public const string UiTap = "ui_tap";
        public const string VoicePip = "voice_pip";
        public const string VoiceBea = "voice_bea";
        public const string VoiceVale = "voice_vale";
        public const string VoiceJune = "voice_june";
        public const string VoiceKit = "voice_kit";
        public const string AmbNight = "amb_night";
        public const string MusicMotifPluck = "music_motif_pluck";
        public const string MusicMotifWood = "music_motif_wood";

        /// <summary>
        /// Knock onsets inside every take of <see cref="ClueKnocksDistant"/>: short, short, pause, last (spec §6). The
        /// arrival beat's attic-light pulses and the paper bat (<c>KnockBeat</c>) run on these times, and the clip
        /// starts on the beat's first frame, so light and sound land together.
        /// </summary>
        public static readonly float[] KnockOnsets = { 0.02f, 0.28f, 1.00f };

        /// <summary>Knock onsets inside every take of <see cref="ClueFloorHum"/> (the hum swells first).</summary>
        public static readonly float[] HumKnockOnsets = { 1.10f, 1.36f, 2.08f };

        public static AudioCatalog CreateCatalog()
        {
            var c = new AudioCatalog();
            // The knocks must carry from the owl house to the arrival porch (about 25 m), so they roll off late.
            c.Add(new AudioEventDef(ClueKnocksDistant, AudioBus.Sfx, 4, spatial: true, markers: KnockOnsets,
                minDistance: 12f, maxDistance: 70f));
            c.Add(new AudioEventDef(ClueFloorHum, AudioBus.Sfx, 3, markers: HumKnockOnsets));
            c.Add(new AudioEventDef(ClueCamTap, AudioBus.Sfx, 5, pitchJitterSemitones: 0.3f, spatial: true,
                minDistance: 2.5f, maxDistance: 14f));
            c.Add(new AudioEventDef(ClueWindWheel, AudioBus.Sfx, 3, spatial: true, minDistance: 8f, maxDistance: 45f));
            c.Add(new AudioEventDef(StepWood, AudioBus.Sfx, 5, pitchJitterSemitones: 0.6f, minIntervalSeconds: 0.08f));
            c.Add(new AudioEventDef(StepGrass, AudioBus.Sfx, 5, pitchJitterSemitones: 0.6f, minIntervalSeconds: 0.08f));
            c.Add(new AudioEventDef(StepStone, AudioBus.Sfx, 5, pitchJitterSemitones: 0.6f, minIntervalSeconds: 0.08f));
            c.Add(new AudioEventDef(MoveJump, AudioBus.Sfx, 4, pitchJitterSemitones: 0.5f, minIntervalSeconds: 0.05f));
            c.Add(new AudioEventDef(MoveLand, AudioBus.Sfx, 4, pitchJitterSemitones: 0.4f, minIntervalSeconds: 0.1f));
            c.Add(new AudioEventDef(MoveGlideOpen, AudioBus.Sfx, 3, pitchJitterSemitones: 0.3f, minIntervalSeconds: 0.2f));
            c.Add(new AudioEventDef(MoveGlideLoop, AudioBus.Sfx, 3, loop: true));
            c.Add(new AudioEventDef(UiPageTurn, AudioBus.Sfx, 4, pitchJitterSemitones: 0.3f));
            c.Add(new AudioEventDef(NoteStickerPick, AudioBus.Sfx, 4, pitchJitterSemitones: 0.4f, minIntervalSeconds: 0.05f));
            c.Add(new AudioEventDef(NoteStickerDrop, AudioBus.Sfx, 4, pitchJitterSemitones: 0.4f, minIntervalSeconds: 0.05f));
            c.Add(new AudioEventDef(NoteStickerWobble, AudioBus.Sfx, 3, minIntervalSeconds: 0.2f));
            c.Add(new AudioEventDef(NoteCorrectGlow, AudioBus.Sfx, 3, minIntervalSeconds: 0.5f));
            c.Add(new AudioEventDef(UiTap, AudioBus.Sfx, 5, pitchJitterSemitones: 0.3f, minIntervalSeconds: 0.03f));
            c.Add(new AudioEventDef(VoicePip, AudioBus.Voice, 3));
            c.Add(new AudioEventDef(VoiceBea, AudioBus.Voice, 3));
            c.Add(new AudioEventDef(VoiceVale, AudioBus.Voice, 3));
            c.Add(new AudioEventDef(VoiceJune, AudioBus.Voice, 3));
            c.Add(new AudioEventDef(VoiceKit, AudioBus.Voice, 3));
            c.Add(new AudioEventDef(AmbNight, AudioBus.Ambience, 3, loop: true));
            c.Add(new AudioEventDef(MusicMotifPluck, AudioBus.Music, 3));
            c.Add(new AudioEventDef(MusicMotifWood, AudioBus.Music, 3));
            VoiceMusicEvents.AddTo(c); // D20.2: the v2 voices and music
            return c;
        }
    }
}
