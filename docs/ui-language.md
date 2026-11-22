# UI language (identity stage 3, draft for owner lock)

The UI is made of the world's craft materials: **paper cards, felt cut-outs, running stitches and an ink outline**.
Mocks: `tools/ui_mock/ui_mock.py` (Pillow, procedural, over `env_keyframe_v2.png`) renders title, HUD, dialogue,
notebook, pause and a contact sheet at 2532×1170 (iPhone landscape @3x). `--guides` draws the safe areas.

## Materials
| Material | Look | Used for |
|---|---|---|
| Bone paper `#F4E6C8` | deckled edge (±1–3 pt wobble), fibre mottling, soft inner edge shade, 1–1.5 pt ink outline, hard ink shadow (offset 2/3 pt, 55–65 %) | cards, secondary buttons, labels, verb tags, notebook pages |
| Felt | fuzzy edge, fine nap noise, cushion lip at the bottom | primary buttons, thumb discs, counter, caption backing, notebook cover |
| Running stitch | 3–3.5 pt dashes, 2.5 pt gaps, inset 4–5 pt, small hole shadow | every card and disc edge; candy-corn on felt, purple on paper, orange = selected/pressed |
| Pencil | graphite `#4A4458`, double jittered stroke | notebook sketches, unknown silhouettes |

Cards sit at −2°…+2° rotation; never rotate thumb controls or text in the HUD.

## Colour
| Token | Hex | Use |
|---|---|---|
| Night violet | `#2A2550` | felt discs (88–92 % opacity in play), caption backing, scrims (75–85 % behind modals) |
| Haunted purple | `#5B3A7A` | stitches on paper, pressed felt disc, cue discs |
| Plum | `#7A3E6E` | notebook cover, tab backs |
| Jack-o'-lantern | `#EE7F2D` | the one primary action per screen; contextual Use; clue underline |
| Candy corn | `#F6D365` | stitches on felt, focus ring, "new" badges, Jump ring |
| Bone | `#F4E6C8` | cards; icons/text on felt |
| Ink | `#1E1A26` | outlines, text on paper and orange, shadows |
| Witch green | `#8DBF3F` | accent only: toggle on, "Found it!" stamp |
| Cloth silver | `#DAE2EE` | **never chrome**; only as a clue swatch (torn cape) inside the notebook |

**Brightness rule.** In play, no paper surface is larger than a label; chrome is felt at ≤ 92 % opacity with bone
icons, so the attic window/active point of interest stays the brightest value (checked on `ui_hud.png`: window
peaks at 255, bone labels at ~240 on a few pixels). Modal screens (notebook, pause, dialogue) dim the world first.
Colour is never the only signal: states also change stitch colour, icon, position or text.

## Type (pt at the 1334×750 reference; ref units = 2 × pt)
| Role | Font | pt (ref) |
|---|---|---|
| Logo | Sniglet ExtraBold, cut-out letters (baked art) | 52–70 |
| Screen heading | Sniglet ExtraBold | 28–32 (56–64) |
| Dialogue line | Andika Regular | 26 (52) |
| Button | Andika Bold | 19–22 (38–44) |
| Body, captions (default / large) | Andika Bold | 17 / 22 (34 / 44) |
| Labels, verb tags, evidence tags | Andika Bold | 13 minimum (26) |

Ink on paper/orange, bone on felt. Left-aligned reading text; centred only in buttons and tags. Never shrink below 13 pt; let
cards grow with text (localization needs ~35 % headroom).

## Layout, safe areas and targets
- Safe insets iPhone landscape: 47 pt left/right, 21 pt bottom. All controls inside; scrims and the world bleed.
- Spacing unit 4 pt; card padding 16–20 pt; gaps between tappables ≥ 8 pt.
- Touch targets: thumb discs Jump 78 pt (ref 150), Use 65 pt (ref 130), system discs 50 pt (Pause, Notebook,
  Close); buttons ≥ 37 pt tall, never under 44×44 pt hit area (keep `UiFactory.MinTouchTarget`).
- HUD map: counter top-left, Notebook + Pause top-right, stick ghost bottom-left (ref 215,195), Use + Jump bottom-right,
  captions bottom-centre between the clusters. Centre stays clear. Left-handed mirrors both clusters.

## States
Normal: shadow 2/3 pt. Pressed: fill −14 % value, shadow 1 pt, content shifts 0.7 pt down, stitch turns orange (paper)
or felt turns purple (discs). Focus (gamepad/keyboard): candy-corn stitch + 2 pt candy outline. Disabled: 45 %
opacity, no stitch. Selected/new: small candy tag ("New note").

## Motion
- Press: squash to 0.94 in 60 ms, release with a 120 ms overshoot to 1.02 → 1.0.
- Paper in: cards flutter in 220 ms (from +12 pt below, rotation ±4° → resting angle, ease-out-back), staggered 40 ms.
  Out: 150 ms drop and fade. Notebook pages turn with a 250 ms skew.
- Ambient: counter bat flaps once on pickup; interaction marker bobs 2 pt at 1.2 Hz; "Tap to continue" arrow nudges.
- **Reduced motion:** all of the above become 120 ms cross-fades; no bob, sway, rotation or overshoot.

## Icons
One stroke weight (≈ 7 % of the icon box), round caps and joins, no fills except pause bars, play/next and the
paper bat. Bone on felt, ink on paper. Set: jump, examine, talk, pause, notebook, bat, radio, gear, globe, star,
close, play, skip, next, home, arrow, knock, captions, motion, vibrate, hand, sound, check. Use always shows
icon + verb tag ("Examine", "Talk", "Sit", "Turn").

## Captions and accessibility
- Captions: bracketed sound text on opaque night felt (backing toggle on by default), bone Andika Bold 17 pt, large 22 pt.
  Direction cue: a purple disc with a candy arrow rotated toward the source, plus direction words in the text.
- Dialogue: speaker portrait + name tab, two lines max per card, line pips, "Tap to continue"; holding Use never
  advances. Skip top-right, away from the reading path. Clue words get a hand-drawn orange underline.
- Contrast: ink on bone 13.8:1, bone on night 11.5:1, ink on orange 6.3:1 (WCAG AA for all text sizes).

## Mapping to the Unity helpers (implementation notes)
| Component | Today | Change |
|---|---|---|
| `IconFactory.RoundedRect` | plain SDF rounded rect | add `PaperSlice` / `FeltSlice`: 9-slice textures with deckle, texture noise and stitched border baked in (procedural at runtime, or baked by this script, see open question) |
| `UiFactory.PaperCard` | rounded paper + `InkShadow(6)` | use `PaperSlice`, ink outline, stitch colour param, shadow 4/6 ref, rotation kept |
| `UiFactory.PrimaryButton` | amber card, ink label | orange `FeltSlice` + candy stitch; optional icon + sub-label (Continue shows chapter and bat count); pressed sprite swap, not tint only |
| `UiFactory.SecondaryButton` | ink card with inset paper fill | `PaperSlice` with purple stitch; pressed = darker paper + orange stitch; optional leading icon |
| `UiFactory.InkDisc` | ink disc, paper ring, caption inside | `FeltDisc`: night felt + stitched ring (candy for Jump, bone for system); Use = orange disc, ink icon, verb shown on a separate paper `VerbTag` below |
| `PressScale` | 0.9, linear | 0.94 + overshoot; reduced-motion path |
| Stick ghost (`GameUi.BuildHud`) | ink disc 45 %, paper ring, arrows | felt base 40 %, bone stitch ring + chevrons, bone felt knob at 80 % |
| First-prompt tag | `PaperCard` | unchanged role; new card style |
| Title (`BuildTitle`) | Sniglet label + ink fade | baked logo sprite (product name, not localized); case-name strip; violet fade; settings disc + language pill replace the dev-only pill |
| Pause (`BuildPause`) | card + 2 buttons | Resume (primary), Notebook, Settings, Language, Credits, quiet "Back to title" link; Settings opens a second card |
| New helpers | — | `IconFactory`: the icon set above; `UiFactory`: `CaptionStrip`, `BatCounter`, `DialogueCard`, `NotebookSpread`, `EvidenceTag`, `Toggle`, `InteractionMarker`, `VerbTag` |

New English strings used in the mocks need keys with translator context in `en.json` (e.g. `ui.hud.examine`,
`ui.hud.notebook`, `caption.knocks.blue_house`, `dialogue.bea.saw_cape`, `ui.dialogue.continue`, `ui.dialogue.skip`,
`ui.notebook.title`, `notebook.q.*`, `ui.notebook.not_found`, `ui.notebook.hint_radio`, `ui.notebook.back_to_street`,
`ui.pause.notebook|settings|language|credits`, `ui.settings.*`, `ui.title.continue_detail`).
