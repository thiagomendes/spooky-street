"""Owner listening page v2 ("Vozes e música v2"): the cartoon babble per character (phrases, emotions and the
runtime-sequenced examples) and the generated music cues with their options and loop seams. Portuguese labels,
the same felt-and-paper look as page.py. Static HTML, no external scripts."""
from html import escape

from .page import CSS

EMOTION_PT = {"neutral": "Neutro", "happy": "Feliz", "surprised": "Surpresa", "thinking": "Pensando",
              "whisper": "Sussurro"}

VOICE_NOTES = {
    "pip": ("A menina nova na rua: curiosa, perguntadeira, alegre.",
            "Soa como uma criança (não esquilo) e nenhuma sílaba vira palavra; as perguntas sobem no fim."),
    "bea": ("Rápida e cheia de certeza, a irmã mais nova.",
            "A mais rápida e mais aguda do elenco; deve parecer confiante, não estridente."),
    "vale": ("Senhor gentil do banco: devagar, seco, bondoso.",
             "Grave e calmo, sem 'robô'; deve contrastar bem com as crianças."),
    "june": ("Cochicha segredos e segura o riso.",
             "O sussurro com risadinha deve ser charmoso e claro no alto-falante do iPhone."),
    "kit": ("Tia teatral e calorosa, ex-atriz.",
            "Grandes curvas de entonação, calor de adulta; nada de 'personagem fofinho'."),
}

PAGE_JS = """
document.querySelectorAll('.card').forEach(function (card) {
  var audio = card.querySelector('audio.main');
  var cap = card.querySelector('.caption');
  var buttons = card.querySelectorAll('.takes button[data-src]');
  buttons.forEach(function (b) {
    b.addEventListener('click', function () {
      buttons.forEach(function (o) { o.setAttribute('aria-pressed', o === b ? 'true' : 'false'); });
      audio.src = b.dataset.src; audio.loop = b.dataset.loop === '1'; audio.currentTime = 0;
      if (cap) { cap.textContent = b.dataset.caption || ''; cap.hidden = !b.dataset.caption; }
      var p = audio.play(); if (p && p.catch) p.catch(function () {});
    });
  });
  var all = card.querySelector('button.all');
  if (all) all.addEventListener('click', function () {
    var list = card.querySelectorAll('.takes.seqall button[data-src]');
    var i = 0;
    function next() {
      if (i >= list.length) { audio.onended = null; return; }
      list[i].click(); audio.loop = false; i += 1;
      audio.onended = function () { setTimeout(next, 350); };
    }
    next();
  });
});
document.querySelectorAll('audio').forEach(function (a) {
  a.addEventListener('play', function () {
    document.querySelectorAll('audio').forEach(function (o) { if (o !== a) o.pause(); });
  });
});
"""

EXTRA_CSS = """
.row-label { font-size: 13px; color: var(--ink-soft); min-width: 92px; }
.caption { margin: 2px 0 4px; font-size: 15px; background: #fff8e6; border: 2px dashed rgba(30,26,38,0.3);
           border-radius: 10px; padding: 4px 10px; }
.opt { border-top: 2px dashed rgba(30, 26, 38, 0.25); margin-top: 8px; padding-top: 6px; }
.opt h4 { margin: 0; font-family: "Sniglet", sans-serif; font-size: 16px; }
.opt p { margin: 2px 0; font-size: 14px; }
.badge { display: inline-block; font-size: 12px; background: var(--green); color: var(--ink); border-radius: 8px;
         padding: 1px 8px; margin-left: 6px; font-weight: 700; }
"""


def _chip(label, src, aria, caption="", loop=False, pressed=False):
    cap = f' data-caption="{escape(caption)}"' if caption else ""
    lp = ' data-loop="1"' if loop else ""
    return (f'<button type="button" data-src="{escape(src)}"{cap}{lp} aria-pressed="{"true" if pressed else "false"}" '
            f'aria-label="{escape(aria)}">{escape(label)}</button>')


def _voice_card(cid, v):
    desc, listen = VOICE_NOTES[cid]
    first = v["files"]["phrases"][0]
    parts = [f'<article class="card" id="voz-{cid}">',
             f'<h3>{escape(v["name"])}</h3><div class="id">voice_{cid} · voz-fonte Gemini "{escape(v["voice"])}" '
             f'(irreconhecível depois do processamento)</div>',
             f'<p class="desc">{escape(desc)}</p>',
             f'<p class="listen"><b>O que ouvir:</b> {escape(listen)}</p>',
             f'<audio class="main" controls preload="none" src="{escape(first["file"])}"></audio>',
             '<p class="caption" hidden></p>']
    chips = "".join(_chip(f'{p["duration_s"]:.1f} s', p["file"], f'Frase de {p["duration_s"]:.1f} segundos',
                          pressed=(i == 0)) for i, p in enumerate(v["files"]["phrases"]))
    parts.append(f'<div class="takes seqall"><span class="row-label">Frases:</span>{chips}</div>')
    chips = "".join(_chip(EMOTION_PT[e["emotion"]], e["file"], f'Emoção: {EMOTION_PT[e["emotion"]]}')
                    for e in v["files"]["emotions"])
    parts.append(f'<div class="takes seqall"><span class="row-label">Emoções:</span>{chips}</div>')
    chips = "".join(_chip(f'Fala {i + 1}', s["file"], f'Fala montada {i + 1}', caption=s["caption"])
                    for i, s in enumerate(v["files"]["sequenced"]))
    parts.append(f'<div class="takes seqall"><span class="row-label">Montada na hora:</span>{chips}'
                 f'<button type="button" class="all">Tocar tudo</button></div>')
    m = v["meta"]
    parts.append('<div class="meta">' + "".join(f"<span>{x}</span>" for x in [
        f'tom médio ≈ {m["f0_median_hz"]:.0f} Hz', f'{m["speed"]:.2f}× mais rápida que a fonte',
        f'{m["lufs_min"]:.1f} a {m["lufs_max"]:.1f} LUFS (alvo −19)', f'pico ≤ {m["tp_max"]:.1f} dBTP',
        "todas as verificações ok" if not m["failures"] else
        f'<span class="bad">falhou: {escape("; ".join(m["failures"]))}</span>']) + "</div>")
    parts.append(f'<details><summary>Espectrograma (frase de 1.8 s, neutra)</summary><img loading="lazy" '
                 f'src="{escape(v["spectrogram"])}" alt="Espectrograma da voz de {escape(v["name"])}"></details>')
    parts.append("</article>")
    return "\n".join(parts)


def _music_card(cue):
    opts = cue["options"]
    first = opts[0]
    loop = cue["kind"] == "loop"
    parts = [f'<article class="card" id="{escape(cue["id"])}">',
             f'<h3>{escape(cue["title_pt"])}</h3><div class="id">{escape(cue["id"])} · {escape(cue["model"])}</div>',
             f'<p class="for"><b>No jogo:</b> {escape(cue["purpose_pt"])}</p>',
             f'<p class="listen"><b>O que ouvir:</b> {escape(cue["listen_pt"])}</p>',
             f'<audio class="main" controls preload="none"{" loop" if loop else ""} src="{escape(first["file"])}">'
             f'</audio>']
    chips = "".join(_chip(f'Opção {i + 1}', o["file"], f'Opção {i + 1}', loop=loop, pressed=(i == 0))
                    for i, o in enumerate(opts))
    parts.append(f'<div class="takes"><span class="row-label">Opções:</span>{chips}</div>')
    if loop:
        chips = "".join(_chip(f'Emenda {i + 1}', o["seam_file"], f'Emenda do loop da opção {i + 1}')
                        for i, o in enumerate(opts))
        parts.append(f'<div class="takes"><span class="row-label">Emenda (5 s + 5 s):</span>{chips}</div>')
    for i, o in enumerate(opts):
        facts = [f'{o["duration_s"]:.1f} s', f'{o["bpm"]:.0f} BPM']
        if loop:
            facts += [f'{o["bars"]} compassos de {o["beats_per_bar"]}', f'tom ≈ {o["key"]}',
                      f'loop da fonte {o["loop_start_s"]:.1f}–{o["loop_end_s"]:.1f} s']
        else:
            facts += [f'final em {o["final_key"]}'
                      + (f' (transposto {o["transposed_st"]:+d} st)' if o["transposed_st"] else ""),
                      f'trecho final da fonte {o["cut_start_s"]:.1f}–{o["cut_end_s"]:.1f} s']
        facts += [f'{o["lufs"]:.1f} LUFS', f'pico {o["true_peak_dbtp"]:.1f} dBTP', f'semente {o["seed"]}']
        badge = '<span class="badge">sugestão</span>' if i == 0 else ""
        parts.append(f'<div class="opt"><h4>Opção {i + 1}{badge}</h4><p>{escape(o["note_pt"])}</p>'
                     f'<div class="meta">' + "".join(f"<span>{escape(f)}</span>" for f in facts) + "</div></div>")
    parts.append(f'<details><summary>Espectrograma (opção 1)</summary><img loading="lazy" '
                 f'src="{escape(first["spectrogram"])}" alt="Espectrograma de {escape(cue["title_pt"])}"></details>')
    parts.append("</article>")
    return "\n".join(parts)


def render(report):
    voices = "\n".join(_voice_card(c, v) for c, v in report["voices"].items())
    music = "\n".join(_music_card(c) for c in report["music"])
    calls = report["api_calls"]
    return f"""<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Vozes e música v2</title>
<meta name="description" content="Revisão por ouvido das novas vozes em balbucio e da música gerada para Spooky Street.">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Andika:wght@400;700&family=Sniglet:wght@800&display=swap" rel="stylesheet">
<style>{CSS}{EXTRA_CSS}</style>
</head>
<body>
<header><div class="wrap">
<h1>Vozes e música <span>v2</span></h1>
<p>As vozes agora são um balbucio de desenho animado: sílabas inventadas, sem palavras, só ritmo e emoção
(a frase escrita continua na tela). A música foi gerada com o modelo de música do Google e editada para o jogo.
Nada disto entra no jogo antes da sua aprovação.</p>
<ul class="how">
<li>Ouça primeiro no alto-falante do iPhone, depois com fone</li>
<li>Toque nos botões para trocar frase, emoção ou opção</li>
<li>Nas músicas, "Emenda" toca o fim do loop emendado no começo</li>
</ul>
</div></header>
<main><div class="wrap">
<section><h2>Vozes</h2>
<p class="legend">"Frases" são trechos prontos de vários tamanhos (o jogo escolhe pelo tamanho da fala). "Emoções" são
as cinco variações. "Montada na hora" mostra a outra técnica: o jogo monta o balbucio sílaba por sílaba a partir da
frase escrita (mostrada embaixo do player).</p>
{voices}
</section>
<section><h2>Música</h2>
<p class="legend">Todas as peças partem do mesmo pedido de motivo: a batida da porta (curta, curta, longa) subindo
ré–fá–lá. O modelo nem sempre obedece tom e andamento; as vinhetas foram transpostas para terminar em ré (ou lá)
quando estavam perto.</p>
{music}
</section>
</div></main>
<footer><div class="wrap">
<b>Como foi feito e verificado.</b> Vozes: fala-fonte do Gemini TTS ({escape(report["tts_model"])}) lendo sílabas
inventadas com direção de personagem; depois cortada em sílabas, acelerada e afinada por PSOLA com formantes
parcialmente preservados, em <code>tools/audio/</code>. Música: {escape(", ".join(report["music_models"]))}; loops
cortados de compasso a compasso com emenda alinhada; vinhetas tiradas do final natural da peça. Tudo masterizado
pelas regras de loudness do jogo (voz −19 LUFS, música −20 LUFS, pico ≤ −1 dBTP). Chamadas à API até aqui:
{calls["total"]} (detalhes em <code>tools/audio/generated/manifest.json</code>).
<span class="legend">Um modelo de escuta (Gemini) foi usado como segunda opinião, mas só o seu ouvido decide.</span>
</div></footer>
<script>{PAGE_JS}</script>
</body>
</html>
"""
