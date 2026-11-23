"""Cartoon babble (D20.1): the cast, their source voices and how each one is cut, sped up and pitched.

Source speech comes from a Gemini TTS prebuilt voice reading *made-up syllable strings* with a character direction.
The post-process (`babble.py`) cuts it into syllables and runs it 1.4–2.2x faster and several semitones higher while
keeping (or only slightly raising) the formants, so the result is rhythm and emotion with no words and no
recognisable stock voice. Everything here is data: change it and rebuild.
"""
from dataclasses import dataclass, field

TTS_MODEL = "gemini-3.8-flash-tts"

EMOTIONS = ("neutral", "happy", "surprised", "thinking", "whisper")

# How each emotion is directed (appended to the character direction). No bracketed tags: one audition read a
# tag aloud, so emotion lives in the direction only.
EMOTION_DIRECTION = {
    "neutral": "Friendly and matter-of-fact, just chatting.",
    "happy": "Delighted and bubbly, smiling all the way through, with a little laugh in the voice.",
    "surprised": "Amazed and surprised: gasps, big rising 'oh!' moments, wide-eyed wonder.",
    "thinking": "Puzzling something out: hesitant, musing, little pauses and 'hmm' sounds, rising unsure endings.",
    "whisper": "Whispering a secret, close and quiet, breathy and conspiratorial.",
}


@dataclass(frozen=True)
class Character:
    id: str
    name: str
    voice: str                 # Gemini prebuilt voice (chosen by audition, see docs/S003.1-voices-music.md)
    direction: str             # who is speaking (never read aloud)
    onsets: tuple              # consonant palette for the made-up syllables
    vowels: tuple
    codas: tuple = ("",)       # optional closing sounds ("m", "n")
    speed: float = 1.9         # playback speed-up of the source (time compression)
    f0_hz: float = 380.0       # target median pitch of the neutral babble; each take is shifted to land on
                               # f0_hz x EMOTION_PITCH[emotion] (the TTS voices change register a lot per emotion)
    formant_follow: float = 0.5  # formants rise by p ** formant_follow for a pitch rise p (0 keeps the source
                                 # formants, 1 is a plain chipmunk; about half is the cleanest and most child-like)
    max_shift_st: float = 10.0  # clamp on the per-take pitch shift
    gap_s: tuple = (0.015, 0.05)   # pause between syllable runs inside a phrase
    syllable_rate: float = 9.0  # syllables per second after processing (used by the runtime planner)
    emotion_overrides: dict = field(default_factory=dict)


# Pitch of each emotion relative to the character's neutral pitch.
EMOTION_PITCH = {"neutral": 1.0, "happy": 1.1, "surprised": 1.2, "thinking": 0.95, "whisper": 0.95}

CAST = (
    Character("pip", "Pip", "Laomedeia",
              "a curious, friendly little girl, about eight, new in town, asking questions with wonder",
              ("b", "d", "m", "n", "p", "t", "w", "l"), ("a", "ee", "o", "oo", "i"),
              speed=1.85, f0_hz=400.0, formant_follow=0.5, syllable_rate=8.5),
    Character("bea", "Bea", "Zephyr",
              "a fast, confident little girl, about seven, very sure of herself, rapid-fire and bouncy",
              ("b", "d", "p", "t", "k", "g", "z"), ("a", "i", "e", "oo"),
              speed=2.15, f0_hz=450.0, formant_follow=0.55, gap_s=(0.01, 0.03), syllable_rate=10.5),
    Character("vale", "Mr. Vale", "Sadaltager",
              "a slow, kind, dry-humoured old grandfather on a park bench, warm, gentle and unhurried",
              ("m", "w", "h", "r", "l", "b"), ("o", "oo", "a", "aw"), ("", "", "m", "n"),
              speed=1.4, f0_hz=125.0, formant_follow=0.0, gap_s=(0.05, 0.12), syllable_rate=5.5),
    Character("june", "June", "Achernar",
              "a mischievous girl, about nine, whispering secrets and trying not to giggle",
              ("sh", "ts", "p", "n", "m", "ch", "s"), ("i", "ee", "a", "u"),
              speed=1.9, f0_hz=370.0, formant_follow=0.45, syllable_rate=9.0),
    Character("kit", "Aunt Kit", "Sulafat",
              "a theatrical, warm, grand aunt who used to act on stage, sweeping and dramatic but kind",
              ("l", "d", "m", "v", "r", "t", "b"), ("a", "ah", "o", "ee", "ay"),
              speed=1.65, f0_hz=245.0, formant_follow=0.3, gap_s=(0.03, 0.08), syllable_rate=7.0),
)

TAKES_PER_EMOTION = 2


# Real words (English and Portuguese, the owner's language) that the syllable generator must not produce.
AVOID = {"no", "bo", "do", "to", "go", "lo", "mo", "so", "ho", "ha", "hi", "ma", "pa", "da", "ta", "na", "la",
         "be", "me", "we", "he", "she", "see", "bee", "tea", "tee", "pee", "dee", "nee", "lee", "wee", "zee",
         "boo", "moo", "too", "two", "who", "woo", "loo", "noo", "poo", "doo", "goo", "shoe", "chew", "sue",
         "hobo", "mama", "papa", "dada", "baba", "nana", "lala", "mimi", "pipi", "titi", "bebe", "tutu", "bob",
         "mom", "dad", "ban", "bun", "man", "mum", "run", "rum", "ram", "ran", "ron", "lam", "lan", "ham",
         "him", "hum", "hon", "wan", "won", "one", "sun", "son", "tin", "ten", "bin", "pin", "pan", "nun", "gun",
         "kid", "kit", "bat", "cat", "dog", "pig", "boom", "doom", "moon", "noon", "soon", "room", "loom", "tomb",
         "pipi", "cocô", "coco", "bunda", "pinto", "pica", "caca", "xixi", "chichi", "teta", "puta", "bobo",
         "boba", "mala", "bola", "lata", "pato", "gato", "rato", "mato", "bota", "bala", "data", "mana"}
