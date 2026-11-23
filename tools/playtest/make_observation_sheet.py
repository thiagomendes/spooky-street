#!/usr/bin/env python3
"""Draw the printable case 01 observation sheet (pt-BR, A4 landscape, one page per child).

The content follows docs/playtest/c01-playtest-kit.pt-BR.md section 9 and 10, condensed so it fits one page: moments
that usually happen together share a row. Uses only reportlab's built-in Helvetica (no network, no font files).

    pip install -r tools/playtest/requirements.txt
    python3 tools/playtest/make_observation_sheet.py            # writes docs/playtest/c01-observation-sheet.pdf
    python3 tools/playtest/make_observation_sheet.py --out x.pdf

The facilitator keeps the sheet out of the child's sight: it names the clues and the questions.
"""

import argparse
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.units import mm
from reportlab.pdfgen import canvas

REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO / "docs" / "playtest" / "c01-observation-sheet.pdf"

FONT = "Helvetica"
BOLD = "Helvetica-Bold"
INK = colors.HexColor("#222222")
SOFT = colors.HexColor("#666666")
RULE = colors.HexColor("#9a9a9a")
SHADE = colors.HexColor("#ececec")
QUESTION = colors.HexColor("#fbe7cf")
HINT = colors.HexColor("#e3ecf7")

# (row id, moment, kind) — kind: "q" notebook question (deduction column open), "t" theory prompt,
# "h" radio log (hint column holds the tier), "" ordinary moment (deduction column shaded).
ROWS = [
    ("1", "Abertura (cena): assistiu / pulou? · chegada: 1º movimento para onde?", ""),
    ("2", "Três batidas: reage, olha a casa? · disco do caderno balança: viu? abriu?", ""),
    ("3", "Fala com a Bea (brilho) · fala com o Sr. Vale (porta do Vale)", ""),
    ("4", "Pergunte 1 vez: “O que você acha que aconteceu?” (anote a teoria)", "t"),
    ("5", "Banco (vista) · porta da frente emperrada · fita na bicicleta (pista falsa)", ""),
    ("6", "Acha o quintal lateral (tempo) · pano prateado na rampa: achou sem rádio?", ""),
    ("7", "Casa da árvore, ponte (dicas de pulo/planar?) · fio no peitoril", ""),
    ("8", "Janela do sótão (virada de página) · encontra a June", ""),
    ("9", "1º uso do caderno com 2+ figurinhas: arrasta ou toca sem ajuda?", ""),
    ("10", "Como a June entrou? — tentativas ___ · usou a fita? · rosto no balanço", "q"),
    ("11", "Trinco com a June, porta abre · corredor: o chão treme", ""),
    ("12", "Grade: peça de madeira · painel da coruja", ""),
    ("13", "De onde vêm as batidas? — tentativas ___ · rosto no balanço", "q"),
    ("14", "Braço solto: manivela antes de encaixar? · encaixa · coruja se curva no telhado", ""),
    ("15", "O que as batidas deveriam mover? — tentativas ___ · rosto", "q"),
    ("16", "Varanda: show da Kit · “So, detective?” · carimbo Case closed", ""),
    ("17", "Revelação: assistiu/pulou? reação às figurinhas dela? · “__ of __ clues”", ""),
    ("18", "Exploração livre: pede para continuar sem ninguém perguntar? · doce do Sr. Vale", ""),
    ("D1", "Rádio nível 1 (brilho + olhar): cada toque, tempo e passo", "h1"),
    ("D2", "Rádio nível 2 (trilha de morcegos): cada toque, tempo e passo", "h2"),
    ("D3", "Rádio nível 3 (mão transparente): cada toque, tempo e passo", "h3"),
    ("D4", "Rádio no caderno (Pip pensando + “?” com lugar): cada toque, qual pergunta", "hM"),
    ("X", "Perdeu câmera/controle, caiu, se assustou, quis parar", ""),
]

# Column titles and widths (mm). Total = 281 mm (A4 landscape 297 mm minus 8 mm margins).
COLUMNS = [
    ("#", 8),
    ("Momento", 92),
    ("Tempo", 13),
    ("O que a criança fez", 56),
    ("Empacou?\n(min) P/P2", 15),
    ("Dica\n1 2 3 M", 14),
    ("Falou em voz alta (R = lido)", 47),
    ("Emoção", 20),
    ("Deduziu\nsozinha?", 16),
]

CHILD_CRITERIA = [
    "Terminou (carimbo) com até 1 dica pedida",
    "\u201cComo a June entrou?\u201d com até 1 toque no rádio",
    "Não desistiu no caderno",
    "70 % das pistas de uma pergunta antes do rádio",
    "\u201cAha!\u201d numa colocação certa",
    "Errou com curiosidade, sem frustração",
    "Explica a entrada pelo lado ou a origem das batidas",
    "Pediu para continuar jogando",
    "Teoria própria depois da Bea e do Sr. Vale",
    "Dia seguinte: reconta o que houve e quem ajudou",
]

GROUP_GATE = (
    "Meta do grupo: 4 de 5 terminam com até 1 dica e explicam \u00b7 3 de 5 resolvem \u201cComo a June entrou?\u201d "
    "com até 1 rádio \u00b7 ninguém desiste no caderno \u00b7 4 de 5 pedem para continuar \u00b7 confusão vista em 2 "
    "sessões vai para revisão."
)

LEGEND = (
    "Tempo mm:ss desde que pegou o celular \u00b7 P/P2 = você fez pergunta neutra \u00b7 Dica: 1 brilho, 2 morcegos, "
    "3 mão, M Pip pensando \u00b7 R = você leu a fala (apêndice A) \u00b7 Emoção: A alegria, C confusão, "
    "F frustração, T tédio \u00b7 Deduziu: S sozinha, M após Pip pensando, T tentativa e erro, N não resolveu"
)

MIN_SIZE = 5.6


def fit_size(c, s, max_w, size, font=FONT):
    """The largest size up to `size` at which `s` fits `max_w` (never below MIN_SIZE)."""
    while size > MIN_SIZE and c.stringWidth(s, font, size) > max_w:
        size -= 0.1
    return size


def text(c, x, y, s, size=7, font=FONT, color=INK, max_w=None):
    if max_w is not None:
        size = fit_size(c, s, max_w, size, font)
        if c.stringWidth(s, font, size) > max_w:
            raise ValueError(f"text does not fit {max_w / mm:.0f} mm: {s!r}")
    c.setFont(font, size)
    c.setFillColor(color)
    c.drawString(x, y, s)


def wrap2(c, s, max_w, size, font):
    """Split `s` into at most two lines that fit `max_w`, or None."""
    words = s.split(" ")
    for i in range(len(words) - 1, 0, -1):
        a, b = " ".join(words[:i]), " ".join(words[i:])
        if c.stringWidth(a, font, size) <= max_w and c.stringWidth(b, font, size) <= max_w:
            return a, b
    return None


def checkbox(c, x, y, size=2.6 * mm):
    c.setStrokeColor(INK)
    c.setLineWidth(0.6)
    c.rect(x, y, size, size, stroke=1, fill=0)


def blank(c, x, y, label, width, size=7.5):
    """A label followed by a write-on line."""
    text(c, x, y, label, size, BOLD)
    lx = x + c.stringWidth(label, BOLD, size) + 1.2 * mm
    c.setStrokeColor(RULE)
    c.setLineWidth(0.5)
    c.line(lx, y - 0.8 * mm, x + width, y - 0.8 * mm)


def draw(out: Path) -> None:
    width, height = landscape(A4)
    margin = 8 * mm
    right = width - margin
    c = canvas.Canvas(str(out), pagesize=landscape(A4))
    c.setTitle("Spooky Street - caso 01 - ficha de observação")
    c.setAuthor("Spooky Street playtest kit")
    c.setSubject("Ficha de observação do teste em família, caso 01 (pt-BR)")

    top = height - margin
    # Title line.
    text(c, margin, top - 4.2 * mm, "Spooky Street \u00b7 Caso 01 \u00b7 Ficha de observação", 12, BOLD)
    note_x = margin + 108 * mm
    text(c, note_x, top - 4.2 * mm,
         "Não mostre à criança. Neutro: \u201cO que você está pensando?\u201d \u00b7 \u201cNão sei, o que você acha?\u201d "
         "\u00b7 nunca dica, resposta, lugar ou toque na tela.", 7.2, FONT, SOFT, max_w=right - note_x)

    # Header fields, two lines.
    y1 = top - 10 * mm
    fields1 = [("Criança (código):", 40), ("Idade:", 20), ("Lê inglês:  sim / pouco / não", 52),
               ("Só figuras?  sim / não", 38), ("Dupla?  sim / não", 32), ("Data:", 52), ("Início:", 47)]
    y2 = top - 15.5 * mm
    fields2 = [("Build (versão \u00b7 build):", 80), ("Responsável presente:", 60), ("Mudanças na preparação:", 141)]
    for y, fields in ((y1, fields1), (y2, fields2)):
        x = margin
        for label, w in fields:
            blank(c, x, y, label, (w - 3) * mm)
            x += w * mm

    # Table.
    table_top = top - 18.5 * mm
    head_h = 6.6 * mm
    row_h = 6.1 * mm
    xs = [margin]
    for _, w in COLUMNS:
        xs.append(xs[-1] + w * mm)
    assert abs(xs[-1] - right) < 0.5, "columns must fill the printable width"
    table_bottom = table_top - head_h - row_h * len(ROWS)

    c.setFillColor(SHADE)
    c.rect(margin, table_top - head_h, xs[-1] - margin, head_h, stroke=0, fill=1)
    for i, (title, _) in enumerate(COLUMNS):
        lines = title.split("\n")
        for j, line in enumerate(lines):
            ly = table_top - (3.9 if len(lines) == 1 else 2.6 + j * 2.8) * mm
            text(c, xs[i] + 1.2 * mm, ly, line, 7, BOLD, max_w=xs[i + 1] - xs[i] - 2 * mm)

    moment_w = xs[2] - xs[1] - 2.4 * mm
    for r, (rid, moment, kind) in enumerate(ROWS):
        y_top = table_top - head_h - r * row_h
        y_bot = y_top - row_h
        if kind == "q":
            c.setFillColor(QUESTION)
            c.rect(margin, y_bot, xs[-1] - margin, row_h, stroke=0, fill=1)
        elif kind.startswith("h"):
            c.setFillColor(HINT)
            c.rect(margin, y_bot, xs[-1] - margin, row_h, stroke=0, fill=1)
        if kind not in ("q", "t"):
            c.setFillColor(SHADE)  # the deduction column does not apply
            c.rect(xs[8], y_bot, xs[9] - xs[8], row_h, stroke=0, fill=1)
        base = y_bot + 2.1 * mm
        text(c, xs[0] + 1.2 * mm, base, rid, 7.5, BOLD)
        font = BOLD if kind == "q" else FONT
        if c.stringWidth(moment, font, 7) <= moment_w:
            text(c, xs[1] + 1.2 * mm, base, moment, 7, font)
        else:
            lines = wrap2(c, moment, moment_w, 6.3, font)
            if lines is None:
                raise ValueError(f"moment does not fit two lines: {moment!r}")
            text(c, xs[1] + 1.2 * mm, y_bot + 3.45 * mm, lines[0], 6.3, font)
            text(c, xs[1] + 1.2 * mm, y_bot + 1.0 * mm, lines[1], 6.3, font)
        text(c, xs[7] + 1.6 * mm, base, "A   C   F   T", 7, FONT, SOFT)
        if kind == "q":
            text(c, xs[8] + 1.4 * mm, base, "S  M  T  N", 7, FONT, SOFT)
        elif kind == "t":
            text(c, xs[8] + 1.4 * mm, base, "teoria S / N", 6.5, FONT, SOFT)
        if kind.startswith("h"):
            text(c, xs[5] + 5 * mm, base, kind[1], 8, BOLD)

    c.setStrokeColor(RULE)
    c.setLineWidth(0.5)
    for r in range(1, len(ROWS)):
        y = table_top - head_h - r * row_h
        c.line(margin, y, xs[-1], y)
    for x in xs[1:-1]:
        c.line(x, table_bottom, x, table_top)
    c.setStrokeColor(INK)
    c.setLineWidth(0.8)
    c.rect(margin, table_bottom, xs[-1] - margin, table_top - table_bottom, stroke=1, fill=0)
    c.line(margin, table_top - head_h, xs[-1], table_top - head_h)

    # Legend.
    ly = table_bottom - 3.4 * mm
    text(c, margin, ly, LEGEND, 6.4, FONT, SOFT, max_w=right - margin)

    # Counts line.
    cy = ly - 4.6 * mm
    counts = [("Pistas antes do 1º rádio: ___ de 12", 54), ("Toques no rádio: ___", 33),
              ("Erros: entrada ___ batidas ___ mover ___", 62),
              ("Fita enganou?  sim / não  \u00b7  se corrigiu?  sim / não", 76),
              ("Pedidos de leitura: ___", 34), ("Tempo: ___ min", 22)]
    x = margin
    for label, w in counts:
        text(c, x, cy, label, 7.5, BOLD, max_w=(w - 2) * mm)
        x += w * mm

    # Criteria checklist: three columns of four, then a notes box.
    cy -= 4.8 * mm
    text(c, margin, cy, "Critérios", 7.5, BOLD)
    text(c, margin, cy - 3.5 * mm, "(por criança):", 7, FONT, SOFT)
    col_w = 72 * mm
    start_x = margin + 19 * mm
    step = 3.75 * mm
    for i, crit in enumerate(CHILD_CRITERIA):
        bx = start_x + (i // 4) * col_w
        by = cy - (i % 4) * step
        checkbox(c, bx, by - 0.6 * mm)
        text(c, bx + 3.8 * mm, by, crit, 7, max_w=col_w - 5 * mm)
    box_x = start_x + 3 * col_w + 1 * mm
    box_top = cy + 3.2 * mm
    box_bottom = cy - 3 * step - 1.6 * mm
    c.setStrokeColor(RULE)
    c.setLineWidth(0.5)
    c.rect(box_x, box_bottom, right - box_x, box_top - box_bottom, stroke=1, fill=0)
    text(c, box_x + 1.5 * mm, box_top - 3 * mm, "O que me surpreendeu:", 7, BOLD, SOFT)

    gate_y = margin - 1 * mm
    assert box_bottom - gate_y > 3 * mm, "footer overlaps the group gate line"
    text(c, margin, gate_y, GROUP_GATE, 6.6, FONT, SOFT, max_w=right - margin)
    c.showPage()
    c.save()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="PDF path to write")
    args = parser.parse_args()
    args.out.parent.mkdir(parents=True, exist_ok=True)
    draw(args.out)
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()
