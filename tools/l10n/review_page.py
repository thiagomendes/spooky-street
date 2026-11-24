#!/usr/bin/env python3
"""Owner review page for pt-BR (S004 D2): python3 tools/l10n/review_page.py  ->  docs/S004-ptBR-review.md

Every key of pt-BR.json beside English, grouped by screen and character, with the +35 % length budget and notes.
Run it again after editing pt-BR.json so the page shows the shipped text.
"""
import json
import math
import os
import re
import sys

root = sys.argv[1] if len(sys.argv) > 1 else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
L = os.path.join(root, 'Assets/SpookyStreet/Content/Resources/Localization/')
en = json.load(open(L + 'en.json', encoding='utf-8'))['entries']
pt = {e['key']: e for e in json.load(open(L + 'pt-BR.json', encoding='utf-8'))['entries']}

NOTES = {
    "ui.title.game_name": "Product name and stitched logo art: kept.",
    "ui.title.case_name": "Owner decision 2a: Lantern Lane = Rua das Lanternas. Sentence case, as Portuguese titles usually are.",
    "ui.title.language_toggle_dev": "\"Idioma\" is the usual word on Brazilian settings screens.",
    "ui.title.continue_detail": "pt plural: 0 and 1 take the \"one\" form (CLDR).",
    "ui.title.recap_stickers": "\"Figurinha\" is what a Brazilian child calls a sticker (album stickers). Same word as the playtest kit.",
    "prompt.first.move_gamepad": "\"Analógico\" is the common Brazilian word for a stick.",
    "ui.hud.pause": "Infinitive for buttons, as in most Brazilian apps.",
    "ui.hud.jump": "Its label is also the button name in the tips, so they all say \"Pular\" (or \"Pule\").",
    "ui.hud.dive": "Owner decision (2026-11-24): the verb \"Mergulhar\", like \"Rolar\" and \"Pular\". One letter over the 8-letter budget: the only budget exception (PortugueseTableTests); the verb tag was widened so it fits at full size.",
    "tip.move.hold_jump": "\"Segure\" says that the finger stays on the button.",
    "tip.move.dive": "\"Use\" refers to the orange button. \"Aperte\" (press) is clearer in Portuguese, because the tag above it reads Rolar or Mergulhar, not Usar.",
    "tip.move.wall_jump": "\"Quicar\" is the word for a ball bouncing. Children know it.",
    "ui.pause.back_to_title": "\"Início\" = the title screen. It is shorter than \"tela inicial\".",
    "ui.settings.helper": "A noun for the helping hand.",
    "ui.settings.camera_speed": "\"Giro\" (turning) fits the 19-letter budget; \"Velocidade da câmera\" was one letter over.",
    "ui.settings.left_handed": "UNSURE. \"Controles canhotos\" is how Brazilian games usually say it; \"Modo canhoto\" is shorter.",
    "ui.settings.opaque_captions": "\"Sólidas\" = not see-through.",
    "ui.parent.answers": "\"A criança\" keeps the page neutral for any child; the page never names or genders the child.",
    "ui.parent.friends": "\"Amizades feitas\" fits the budget; \"Amigos encontrados\" was one letter over.",
    "ui.parent.day_mon": "Brazilian calendars write weekdays in lower case.",
    "ui.credits.fonts": "The licence texts themselves stay in English, as published (OFL).",
    "ui.memory.skip": "Owner decision (2026-11-24): skip is \"Passar\" everywhere, so it is never confused with Pip's Jump button (\"Pular\").",
    "ui.dialogue.skip": "See ui.memory.skip.",
    "ui.cutscene.skip": "See ui.memory.skip.",
    "ui.cutscene.skip_again": "See ui.memory.skip.",
    "c01.hint.check_view.locate": "Owner decision (2026-11-24): no compass points (a 6-year-old may not know \"leste\"). \"Cerquinha\" (the little picket fence) and \"árvore grande\" (the plaza maple) are what the child sees. It names the area only.",
    "ui.hint.move.jump": "\"Aperte\" works for touch, gamepad and keyboard (S004 accessibility pass); \"rapidinho\" keeps the short press, as opposed to \"Segure\" for the high jump.",
    "ui.parent_gate.hint": "Singular \"Adulto:\" plus the você imperative, as Brazilian parental gates read.",
    "ui.parent_gate.hint_gamepad": "\"Botões de ombro\" is the platform-neutral term in Brazilian game manuals (L/R, LB/RB).",
    "c01.caption.foyer_hum": "\"Toc\" is the Brazilian knock sound.",
    "c01.caption.crank_gap": "\"Tec\" for a single small tap, as in the playtest kit.",
    "c01.caption.owl_bows": "\"Se curva\" (bows down) is used here. Spoken lines use the noun \"reverência\" for \"bow\".",
    "c01.examine.streamer": "Changed from the kit (\"Papel-alumínio amassadinho e brilhante\" was over the length budget). It is still foil, not cloth.",
    "c01.examine.sill": "\"Beirada da janela\" instead of the kit's \"peitoril\": a 6-year-old may not know \"peitoril\". Kit updated.",
    "c01.examine.bowl_sweet": "\"Capacetinho\" = tiny helmet.",
    "c01.line.bea.closed": "\"Reverências\" for bows. It is the theatre word and keeps the \"três batidas, três reverências\" rhythm.",
    "c01.line.vale.door_shut": "\"Eu a via\" is old-fashioned and correct, which suits Mr. Vale. The kit had \"eu via ela\". Kit updated.",
    "c01.line.vale.invite_sit": "Polite \"Sente-se\" for Mr. Vale's voice. Pip is a girl, hence \"mesma\". Kit updated.",
    "c01.line.vale.open_now": "\"Ora, ora\" is a dry old man's \"well, well\".",
    "c01.line.vale.closed": "\"Jamais\" adds an old-fashioned touch.",
    "c01.line.june.listen_floor": "June speaks informally (\"Escuta\"). The radio hints use \"Escute\".",
    "c01.line.june.closed": "\"Batucando\" = drumming a rhythm with the fingers. It is a very Brazilian word.",
    "c01.line.kit.thanks": "\"Vocês duas\": Pip and June are both girls. Kit is a woman, so \"Obrigada\".",
    "c01.line.bea.remembers": "\"Vigia\" = lookout. The picture shows an eye.",
    "c01.line.june.remembers": "\"Psiu\" is the Brazilian \"psst\".",
    "c01.line.kit.remembers": "\"Bem-vinda\" because Pip is a girl. It hints at the friends and adds nothing.",
    "ui.notebook.case_closed_stamp": "Same as the kit. \"Resolvido\" was avoided because the conclusion lint bans it in captions.",
    "ui.notebook.replay": "\"Jogar o caso de novo\" did not fit the button.",
    "ui.notebook.replay_confirm": "\"Vai ficar em branco\" is gentler for a child than \"será apagado\".",
    "c01.notebook.a_moved": "\"Fazer ela se curvar\" is spoken Brazilian. The written form would be \"fazê-la\", which is too formal for a child.",
    "c01.notebook.note_bench": "Comma before \"e\" dropped (Portuguese punctuation). Kit updated.",
    "c01.notebook.note_streamer": "\"Alumínio\" for foil. The kit's \"papel-alumínio\" went over the length budget. Kit updated.",
    "c01.notebook.note_owl_booth": "\"Da Tia Kit\": a child says the title with the name. Shorter than the kit's line. Kit updated.",
    "c01.hint.check_view.locate": "\"Leste\" (east), as in English. Children may not know it, but the hint is not changed.",
    "c01.hint.find_way_in.observe": "\"Quintal lateral\" = side yard. \"Quintal do lado\" was avoided because it can mean the neighbour's yard.",
    "c01.hint.meet_june.observe": "\"Uma luz brilha\" for a lamp glowing. It is no more precise than the English.",
    "c01.hint.open_door.locate": "\"Desça com os morcegos\" keeps \"down\". The hall is \"corredor\" everywhere, as in the kit.",
    "c01.hint.find_source.observe": "\"Lá embaixo\" keeps \"low down\" just as vague. It does not say \"under the floor\".",
    "ui.hint.move.jump": "Mirrors the English \"Tap\"; F2 will make the prompts scheme-neutral in both languages.",
    "ui.speaker.vale": "Owner decision: Sr. Vale.",
    "ui.speaker.kit": "Owner decision: Tia Kit.",
    "ui.verb.connect": "\"Encaixar\" (snap into place) is a child's word. It says no more than \"connect\".",
    "ui.cutscene.watch_opening": "\"A Pip\": Brazilians put an article before first names.",
    "c01.cutscene.opening.town": "Candlebrook kept and the street translated (owner decision 2a). The English context was updated to say so.",
    "c01.cutscene.opening.window": "\"Acesa\" (lit) instead of \"brilha\", so the opening has no \"brilho\" (Bea's clue word), as English avoids \"flash\".",
    "c01.cutscene.reveal.reopen": "\"Teatrinho\" = little puppet theatre, a warmer picture than \"barraca\" (stall). The kit still says \"barraca\" in its own prose.",
    "ui.language.in_game": "\"Já vem no jogo\" = comes with the game.",
    "keepsake.page.lanterns": "\"Lanternas por toda a Rua das Lanternas\" keeps the English echo of the street's name.",
    "keepsake.page.friends": "\"Amigos\" (masculine plural) is the neutral form for a mixed group. It does not say who they are.",
    "ui.verb.knock": "\"Bater\" is what you do at a door. It names no result.",
    "c01.cutscene.seed.owl": "\"Reflete a luz\" = catches the light. It says nothing about why.",
    "c01.cutscene.seed.crest": "\"Brasão\" for crest. \"Teatrinho\" for the booth, as in c01.cutscene.reveal.reopen.",
    "c01.cutscene.street.hour": "\"Plim!\" is the Brazilian little-bell sound.",
    "c01.cutscene.street.show": "\"Se curva\", the same verb as the caption c01.caption.owl_bows.",
    "ui.save.newer_heading": "\"Progresso\" for a save. A child understands it better than \"jogo salvo\".",
}

GROUPS = [
    ("Title screen and recap", ["ui.title."]),
    ("First prompts, HUD and move tips", ["prompt.", "ui.hud.", "tip."]),
    ("Pause, settings, memories and parent gate", ["ui.pause.", "ui.settings.", "ui.memory.", "ui.parent_gate."]),
    ("Captions (sound off)", ["c01.caption.", "ui.caption."]),
    ("Pip looks (examine)", ["c01.examine."]),
    ("Bea (fast, excited)", ["c01.line.bea."]),
    ("Mr. Vale (slow, dry, old-fashioned)", ["c01.line.vale."]),
    ("June (shy, whispery, conspiratorial)", ["c01.line.june."]),
    ("Aunt Kit (theatrical)", ["c01.line.kit."]),
    ("Notebook: screen and questions", ["ui.notebook.", "c01.notebook.q_"]),
    ("Notebook: answers (shown only after the child solves)", ["c01.notebook.a_"]),
    ("Notebook: clue notes and sticker names", ["c01.notebook.note_", "c01.notebook.tag_"]),
    ("Radio hints (never more than the English)", ["c01.hint.", "ui.hint."]),
    ("Dialogue card and speaker names", ["ui.dialogue.", "ui.speaker."]),
    ("Verbs on the action button", ["ui.verb."]),
    ("Story scenes", ["ui.cutscene.", "c01.cutscene."]),
    ("Saves", ["ui.save."]),
    ("Language screen", ["ui.language."]),
    ("Keepsake pages (old drawings)", ["keepsake."]),
    ("For grown-ups and credits (behind the parent gate)", ["ui.parent.", "ui.credits."]),
]


def text(e):
    if 'plural' in e:
        return ' / '.join(f"{k}: {v}" for k, v in e['plural'].items())
    return e['value']


def budget(s):
    vis = len(re.sub(r"\{\d+\}", "", s))
    return len(s) + math.ceil(vis * 0.35) + 2


def cell(s):
    if s == '':
        return ''
    if s != s.strip():
        return '`' + s + '`'
    return s.replace('|', '\\|').replace('\n', ' ')


out = []
out.append("# S004 D2: Brazilian Portuguese, owner review")
out.append("")
out.append("Every user-facing line of `pt-BR.json` beside its English, grouped by screen and character, for the owner")
out.append("(about an hour). Branch `feature/s004-ptbr`, evidence in `docs/S004-ptbr.md`. This page is generated by")
out.append("`tools/l10n/review_page.py` from both tables, so it shows the shipped text exactly.")
out.append("")
out.append("- **Len** is the Portuguese length and the budget: the English text through the pseudo-localizer (+35 % plus two")
out.append("  brackets). Screen layouts are proven against that budget in the visual captures. Every line fits, and a Core")
out.append("  test enforces it.")
out.append("- **UNSURE** marks wording I would most like the owner to check. The other notes explain choices.")
out.append("- Names: Candlebrook, Pip, Bea, June and Kit are kept. Lantern Lane = Rua das Lanternas, Aunt Kit = Tia Kit and")
out.append("  Mr. Vale = Sr. Vale (open decision 2a). Pip is a girl, so lines addressed to her use feminine forms (\"você")
out.append("  mesma\", \"bem-vinda\").")
out.append("- Register: the UI and the radio use the standard você imperative (\"Siga\", \"Escute\", \"Segure\"). The")
out.append("  characters speak informal Brazilian (\"Vem\", \"Escuta\", \"Me dá uma mão?\"), except Mr. Vale, who is polite")
out.append("  and old-fashioned (\"Sente-se\", \"Eu a via\", \"jamais\").")
out.append("- Hints say exactly as much as the English, and the conclusion lint checks them in Portuguese.")
out.append("- To change a line, edit `Assets/SpookyStreet/Content/Resources/Localization/pt-BR.json` (its key) and")
out.append("  `docs/playtest/c01-playtest-kit.pt-BR.md` Appendix A if the line is there.")
out.append("")
seen = set()
for title, prefixes in GROUPS:
    rows = [e for e in en if any(e['key'].startswith(p) for p in prefixes) and e['key'] not in seen]
    if not rows:
        continue
    out.append(f"## {title}")
    out.append("")
    out.append("| Key | English | Português | Len | Note |")
    out.append("|---|---|---|---|---|")
    for e in rows:
        seen.add(e['key'])
        p = pt[e['key']]
        en_t, pt_t = text(e), text(p)
        if 'plural' in e:
            ln = max(len(v) for v in p['plural'].values())
            bd = budget(e['plural']['other'])
        else:
            ln, bd = len(p['value']), budget(e['value'])
        out.append(f"| `{e['key']}` | {cell(en_t)} | {cell(pt_t)} | {ln}/{bd} | {cell(NOTES.get(e['key'], ''))} |")
    out.append("")
left = [e for e in en if e['key'] not in seen]
if left:
    # Keys of a group not listed above still get a row, so the page always covers every key.
    out.append("## Other")
    out.append("")
    out.append("| Key | English | Português | Len | Note |")
    out.append("|---|---|---|---|---|")
    for e in left:
        p = pt[e['key']]
        ln = max(len(v) for v in p['plural'].values()) if 'plural' in p else len(p['value'])
        bd = budget(e['plural']['other'] if 'plural' in e else e['value'])
        out.append(f"| `{e['key']}` | {cell(text(e))} | {cell(text(p))} | {ln}/{bd} | {cell(NOTES.get(e['key'], ''))} |")
        seen.add(e['key'])
    out.append("")
assert all(k in pt for k in NOTES), [k for k in NOTES if k not in pt]
open(os.path.join(root, 'docs/S004-ptBR-review.md'), 'w', encoding='utf-8', newline='\n').write('\n'.join(out))
print(len(seen))
