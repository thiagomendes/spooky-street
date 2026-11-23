"""Music cues generated with Google's Lyria (D20.1), and how each is cut into a game cue.

All cues share one motif so the score feels like one piece: the case 01 knock (short, short, long) as a rising
D-F-A figure, answered by a gentle falling phrase, in D minor with a warm turn to D major for resolutions (the same
notes as the synthesised S003 motif). Lyria ignores requested durations (the pro model writes 1-3 minute pieces,
the clip model about 30 s), so lengths are made in post: loops are cut bar to bar, stings and reveals are taken
from the natural ending of a clip. Seeds are sent, but the service does not promise identical audio for a seed,
so the source outputs are kept in tools/audio/generated/music/.
"""
from dataclasses import dataclass

MOTIF = ("The main motif is a little three-note figure in the rhythm of a door knock: short, short, long (two "
         "eighth notes then a held quarter note), rising D, F, A, answered by a gentle falling phrase. ")

PALETTE = ("Instrumentation: pizzicato strings, celesta, glockenspiel, soft clarinet and flute, a bassoon on the "
           "bass line, harp, light woodblock and brushed percussion. ")

GUARD = ("Mood: Halloween-cosy, curious and gentle, warm and a little mischievous, like a storybook night walk on "
         "a friendly street; spooky-not-scary for children aged 6 to 9. No vocals, no choir, no organ, no heavy "
         "brass, no horror stingers, no dissonant clusters, no sudden loud hits. Instrumental only.")


@dataclass(frozen=True)
class Cue:
    id: str
    title: str
    title_pt: str
    model: str
    prompt: str
    seeds: tuple
    kind: str            # "loop", "sting" or "reveal"
    length_s: tuple      # target length range after editing
    listen_pt: str       # what the owner should listen for (Portuguese, for the page)
    purpose_pt: str


CUES = (
    Cue("music_street_theme", "Street theme", "Tema da rua", "lyria-3-pro-preview",
        "A gentle, curious, cosy theme for the street of a children's mystery game at Halloween dusk. "
        "D minor, 96 BPM, 4/4, a steady light pulse from start to end. " + MOTIF +
        "The motif is stated on celesta and pizzicato, then the clarinet sings a warm answering melody. " + PALETTE +
        "Keep the same tempo and texture throughout so it can loop: no slow intro, no big ending, no fade out. "
        + GUARD,
        (101, 202, 303, 404), "loop", (55.0, 95.0),
        "O motivo da batida (curta, curta, longa) logo no começo; aconchegante e curioso, nunca assustador; "
        "a emenda do loop não deve 'pular'.",
        "Música da rua enquanto a criança explora o bairro."),
    Cue("music_exploration", "Exploration", "Exploração", "lyria-3-pro-preview",
        "A sparse, airy underscore for free play in a children's mystery game at Halloween dusk, a quieter "
        "variation of the street theme. D minor, 96 BPM, 4/4. " + MOTIF +
        "Only fragments of the motif, with lots of space between phrases: solo celesta and soft pizzicato, a "
        "sustained warm string pad, an occasional clarinet or flute answer, very light brushed percussion. "
        "Calm and unhurried, it must stay under gameplay sounds and never draw attention. "
        "Same tempo and texture throughout so it can loop: no intro, no ending, no fade out. " + GUARD,
        (111, 222, 333), "loop", (55.0, 95.0),
        "Mais leve e espaçada que o tema; deve ficar 'por baixo' dos sons do jogo sem cansar; emenda do loop.",
        "Fundo musical discreto durante o jogo livre."),
    Cue("music_case_closed", "Case-closed sting", "Caso resolvido (vinheta)", "lyria-3-clip-preview",
        "A short, warm, joyful piece for the moment a child solves a mystery in a cosy Halloween children's game. "
        "D minor turning to D major, 96 BPM. " + MOTIF +
        "Celesta and pizzicato play the knock motif, the woodwinds answer, and the piece ends with a big, warm, "
        "satisfying final cadence on a D major chord with a harp glissando and a soft glockenspiel sparkle that "
        "rings out. " + GUARD,
        (121, 242, 363, 484), "sting", (5.0, 8.0),
        "Calor e resolução: deve soar como 'conseguimos!', terminando num acorde maior que se apaga suave.",
        "Toca quando o caso é fechado (carimbo 'Caso resolvido')."),
    Cue("music_reveal", "Cutscene reveal", "Revelação (cena)", "lyria-3-clip-preview",
        "Music for a magical 'aha!' reveal in a children's mystery game at Halloween: wonder and discovery. "
        "It begins hushed and curious with soft celesta and harp, builds with a rising flute and string swell, "
        "and blooms into a warm statement of the motif in D major. " + MOTIF +
        "Ends on a glowing, resolved D major chord that rings out. " + PALETTE + GUARD,
        (131, 262, 393, 524), "reveal", (10.0, 20.0),
        "Sensação de 'ahá!': começa em suspense suave e abre num acorde maior luminoso; nada de susto.",
        "Cena da revelação depois de uma dedução certa."),
)
