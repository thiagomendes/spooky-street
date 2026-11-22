"""The owner listening page (index.html): a felt-and-paper list with a player per sound, its takes, what it is
for in the game, what to listen for, and its measured loudness. Static HTML, no external scripts."""
from html import escape

from .master import CATEGORIES

GROUPS = ("Clues", "Pip moving", "Notebook and UI", "Voices", "Ambience and music")

CSS = """
:root {
  --felt: #2A2550; --felt-2: #342d63; --stitch: #F6D365; --paper: #F4E6C8; --paper-2: #EAD8B2;
  --ink: #1E1A26; --ink-soft: #4A4458; --accent: #EE7F2D; --plum: #7A3E6E; --green: #8DBF3F;
  --bg: #EFE2C4; --bg-dot: rgba(122, 62, 110, 0.07); --text: #1E1A26; --shadow: rgba(30, 26, 38, 0.6);
  --warn: #B4381E;
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --bg: #1d1a33; --bg-dot: rgba(246, 211, 101, 0.05); --paper: #EDDDBC; --paper-2: #E0CDA6;
    --shadow: rgba(0, 0, 0, 0.7);
  }
}
:root[data-theme="dark"] {
  --bg: #1d1a33; --bg-dot: rgba(246, 211, 101, 0.05); --paper: #EDDDBC; --paper-2: #E0CDA6;
  --shadow: rgba(0, 0, 0, 0.7);
}
* { box-sizing: border-box; }
html { -webkit-text-size-adjust: 100%; }
body {
  margin: 0; background: var(--bg); color: var(--text);
  background-image: radial-gradient(var(--bg-dot) 1px, transparent 1.4px); background-size: 7px 7px;
  font: 16px/1.45 "Andika", system-ui, sans-serif;
}
header {
  background: var(--felt); color: var(--paper);
  background-image: radial-gradient(rgba(255,255,255,0.05) 1px, transparent 1.5px), radial-gradient(rgba(0,0,0,0.18) 1px, transparent 1.5px);
  background-size: 5px 5px, 7px 7px; background-position: 0 0, 2px 3px;
  padding: 28px 16px 26px; border-bottom: 3px dashed var(--stitch); outline: 6px solid var(--felt);
}
.wrap { max-width: 820px; margin: 0 auto; }
h1 { font-family: "Sniglet", "Andika", sans-serif; font-weight: 800; font-size: 34px; line-height: 1.1; margin: 0 0 8px; letter-spacing: 0.3px; }
h1 span { color: var(--stitch); }
header p { margin: 6px 0; max-width: 640px; }
.how { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 14px; padding: 0; list-style: none; }
.how li { background: var(--felt-2); border: 2px dashed rgba(246, 211, 101, 0.55); border-radius: 14px; padding: 6px 12px; font-size: 14px; }
main { padding: 8px 16px 48px; }
h2 { font-family: "Sniglet", sans-serif; font-weight: 800; font-size: 24px; margin: 34px 0 12px; display: inline-block;
     background: var(--plum); color: var(--paper); padding: 4px 16px 6px; border-radius: 10px 14px 9px 13px;
     box-shadow: 3px 3px 0 var(--shadow); transform: rotate(-1deg); }
.card {
  background: var(--paper); color: var(--ink); border: 2px solid var(--ink); margin: 14px 0; padding: 14px 16px 12px;
  border-radius: 10px 14px 12px 9px; box-shadow: 3px 4px 0 var(--shadow); position: relative;
  background-image: linear-gradient(transparent 96%, rgba(122, 62, 110, 0.06) 96%); background-size: 100% 24px;
}
.card:nth-child(odd) { border-radius: 13px 9px 11px 14px; }
.card h3 { font-family: "Sniglet", sans-serif; font-weight: 800; font-size: 20px; margin: 0 0 4px; line-height: 1.2; }
.id { font: 12px/1.2 ui-monospace, Menlo, monospace; color: var(--ink-soft); }
.desc { margin: 6px 0 4px; }
.for, .listen { margin: 4px 0; font-size: 15px; }
.for b, .listen b { color: var(--plum); }
.listen { background: var(--paper-2); border-left: 4px solid var(--accent); padding: 4px 10px; border-radius: 0 8px 8px 0; }
audio { width: 100%; margin: 8px 0 4px; height: 40px; }
.takes { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; margin: 2px 0 6px; }
.takes span { font-size: 14px; color: var(--ink-soft); margin-right: 2px; }
button {
  font: 700 14px/1 "Andika", sans-serif; color: var(--ink); background: var(--paper);
  border: 2px solid var(--ink); border-radius: 999px; padding: 8px 12px; min-height: 36px; cursor: pointer;
  box-shadow: 2px 2px 0 var(--shadow);
}
button[aria-pressed="true"] { background: var(--accent); }
button:active { transform: translate(1px, 1px); box-shadow: 1px 1px 0 var(--shadow); }
button:focus-visible { outline: 3px solid var(--stitch); outline-offset: 2px; }
.demo { margin-top: 6px; padding-top: 6px; border-top: 2px dashed rgba(30, 26, 38, 0.25); font-size: 14px; }
.meta { display: flex; flex-wrap: wrap; gap: 6px 14px; font-size: 13px; color: var(--ink-soft); margin-top: 6px; }
.meta .bad { color: var(--warn); font-weight: 700; }
details { margin-top: 6px; font-size: 14px; }
summary { cursor: pointer; color: var(--plum); font-weight: 700; }
details img { width: 100%; height: auto; border: 2px solid var(--ink); border-radius: 6px; margin-top: 6px; image-rendering: auto; }
.legend { font-size: 13px; color: var(--ink-soft); }
footer { padding: 18px 16px 40px; font-size: 14px; }
footer .wrap { background: var(--paper); color: var(--ink); border: 2px solid var(--ink); border-radius: 12px; padding: 12px 16px; box-shadow: 3px 3px 0 var(--shadow); }
table { border-collapse: collapse; font-size: 14px; margin: 6px 0; }
td, th { padding: 2px 10px 2px 0; text-align: left; }
@media (max-width: 520px) { h1 { font-size: 28px; } .card { padding: 12px; } }
"""

JS = """
document.querySelectorAll('.card').forEach(function (card) {
  var audio = card.querySelector('audio.main');
  var buttons = card.querySelectorAll('.takes button[data-src]');
  buttons.forEach(function (b) {
    b.addEventListener('click', function () {
      buttons.forEach(function (o) { o.setAttribute('aria-pressed', o === b ? 'true' : 'false'); });
      audio.src = b.dataset.src; audio.currentTime = 0;
      var p = audio.play(); if (p && p.catch) p.catch(function () {});
    });
  });
  var all = card.querySelector('button.all');
  if (all) all.addEventListener('click', function () {
    var i = 0;
    function next() {
      if (i >= buttons.length) { audio.onended = null; return; }
      buttons[i].click(); i += 1;
      audio.onended = function () { setTimeout(next, 450); };
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


def _card(sound_id, entry):
    takes = entry["takes"]
    first = takes[0]
    loop = " loop" if entry.get("loop") else ""
    bad = [f for t in takes for f in t.get("failures", [])]
    lufs = [t["lufs"] for t in takes]
    tps = [t["true_peak_dbtp"] for t in takes]
    durs = [t["duration_s"] for t in takes]
    parts = [f'<article class="card" id="{escape(sound_id)}">',
             f'<h3>{escape(entry["title"])}</h3><div class="id">{escape(sound_id)}</div>',
             f'<p class="desc">{escape(entry["description"])}</p>',
             f'<p class="for"><b>In the game:</b> {escape(entry["purpose"])}</p>']
    if entry.get("listen_for"):
        parts.append(f'<p class="listen"><b>Listen for:</b> {escape(entry["listen_for"])}</p>')
    parts.append(f'<audio class="main" controls preload="none"{loop} src="{escape(first["file"])}"></audio>')
    if len(takes) > 1:
        chips = "".join(
            f'<button type="button" data-src="{escape(t["file"])}" aria-pressed="{"true" if i == 0 else "false"}" '
            f'aria-label="Take {i + 1}">{i + 1}</button>' for i, t in enumerate(takes))
        parts.append(f'<div class="takes"><span>Takes:</span>{chips}'
                     f'<button type="button" class="all">Play all</button></div>')
    if entry.get("demo"):
        parts.append(f'<div class="demo">In context: a walk across this surface (the takes mixed in turn)'
                     f'<audio controls preload="none" src="{escape(entry["demo"])}"></audio></div>')
    meta = [f'{min(durs):.2f}–{max(durs):.2f} s' if len(set(durs)) > 1 else f'{durs[0]:.2f} s',
            f'{min(lufs):.1f} to {max(lufs):.1f} LUFS (target {entry["target_lufs"]:.0f})',
            f'peak ≤ {max(tps):.1f} dBTP', escape(entry["category"])]
    limited = max(t.get("limiter_db", 0.0) for t in takes)
    if limited >= 0.5:
        meta.append(f"limiter up to {limited:.1f} dB")
    if entry.get("markers_s"):
        meta.append("sync points " + ", ".join(f"{m:.2f}" for m in entry["markers_s"]) + " s")
    if bad:
        meta.append(f'<span class="bad">check failed: {escape("; ".join(bad))}</span>')
    else:
        meta.append("all checks passed")
    parts.append('<div class="meta">' + "".join(f"<span>{m}</span>" for m in meta) + "</div>")
    parts.append(f'<details><summary>Spectrogram (take 1)</summary><img loading="lazy" '
                 f'src="{escape(entry["spectrogram"])}" alt="Waveform and spectrogram of {escape(entry["title"])}">'
                 f'</details>')
    parts.append("</article>")
    return "\n".join(parts)


def render(report):
    entries = report["sounds"]
    total_takes = sum(len(e["takes"]) for e in entries.values())
    sections = []
    for group in GROUPS:
        cards = [_card(k, e) for k, e in entries.items() if e.get("group") == group]
        if cards:
            sections.append(f'<section><h2>{escape(group)}</h2>\n' + "\n".join(cards) + "</section>")
    targets = "".join(f"<tr><td>{escape(c.name)}</td><td>{c.lufs:.0f} LUFS</td></tr>" for c in CATEGORIES.values())
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Spooky Street Sounds</title>
<meta name="description" content="Owner listening review of the first Spooky Street sound set, all synthesised from code.">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Andika:wght@400;700&family=Sniglet:wght@800&display=swap" rel="stylesheet">
<style>{CSS}</style>
</head>
<body>
<header><div class="wrap">
<h1>Spooky Street <span>sound check</span></h1>
<p>{len(entries)} sounds, {total_takes} takes. Every one is synthesised from code (wood modes, cloth and paper
noise, formant voices); no recordings or sound packs. Please approve or reject each by ear.</p>
<ul class="how">
<li>Listen on the iPhone speaker first, then headphones</li>
<li>Tap the take numbers to hear the variations</li>
<li>Loops repeat on their own: listen for the seam</li>
</ul>
</div></header>
<main><div class="wrap">
{"".join(sections)}
</div></main>
<footer><div class="wrap">
<b>How these were made and checked.</b> Rendered at 48 kHz by <code>tools/audio/build.py</code>, mastered for a phone
speaker (sub-bass removed, gentle compression, true peak at or under {-1.0:.0f} dBTP) and loudness-matched per category:
<table>{targets}</table>
<span class="legend">Each sound passed automatic checks for loudness, peak, clipping, DC offset, sub-bass, clicks
and length. Machines cannot judge whether it sounds good: that is your call.</span>
</div></footer>
<script>{JS}</script>
</body>
</html>
"""
