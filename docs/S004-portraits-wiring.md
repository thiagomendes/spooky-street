# S004 stream G, wiring: dialogue portraits in the dialogue UI

Branch `feature/s004-portraits-wiring`. Plan: `docs/S004-plan.md` §2.6 and §3 "Stream G" (G-wiring scope). The art
half is `docs/S004-portraits-art.md`.

## What changed

| File | What |
|---|---|
| `Core/Nonverbal/LinePortraits.cs` | engine-free: a line's portrait is its speaker's badge in its `LineVoices` emotion (`TryGet(line, out rect)`, `SpriteName(line)`); false or null for captions |
| `Runtime/UI/Nonverbal/PortraitAtlas.cs` | the runtime cutter, the `PuppetAtlas` pattern: loads `Resources/UI/Portraits/portraits`, cuts and caches one sprite per `portrait.<speaker>.<emotion>`, scales rects if the import shrinks the atlas; `AnchorsOnFrame` puts a cell's disc on a round frame's centre |
| `Runtime/UI/DialogueView.cs` | the felt face disc (on the v1 card and on the bubble's left edge) shows the badge |

How the face disc looks now:

- The felt frame keeps its speaker colour and stitch ring (Bea orange, Vale bone, Kit plum, Pip and June night).
- The badge's sky disc sits on the frame's centre at 80 % of its diameter, inside the stitch ring, as on the review
  sheet. The cell is not centred: its anchors follow `DiscCenterX/Y` and `DiscRadius`, so the head and hat rise over the
  ring and the frame's top edge (Bea's stalk, June's hat).
- Fallbacks unchanged: without the atlas, the puppet face (`pg.<speaker>`), then the coloured initial.
- When the face changes between lines it fades in from 35 % to full over 0.15 s; with reduced motion it swaps at once.
- Captions and narration never reach `DialogueView` (the director sends them to `CaptionStrip`), and the strip is
  unchanged. A caption line given to the view directly shows no badge. Cutscenes only show captions, so they are
  unchanged too. Street greetings (`StreetGreetings`) are ordinary dialogue lines and get their speaker's face.
- Layout, sizes and safe area are unchanged: the badge is a child of the existing disc.
- No new strings.

## Tests

- **Core** `LinePortraitsTests` (Unity EditMode and `tools/core-tests`):
  - every spoken key in `CaseScript.AllLineKeys` (speaker from its stable key) has a `LineVoices` row and resolves to
    its own speaker's portrait in that emotion;
  - every conversation line (initial and stamped state) and every street greeting has a portrait;
  - spot checks (Bea surprised, June whisper, Pip thinking), neutral for a key without a row, none for captions.
- **PlayMode** `NonverbalUiTests.EachLineShowsItsEmotionsPortraitAndCaptionsShowNone`: five lines across four
  emotions each show their badge (sprite name and `PortraitId`), fully opaque once settled; reduced motion swaps with
  no fade; a caption line shows no badge and the caption strip holds no portrait sprite.
- **Capture** `NonverbalCaptureTests`: frame 79 on the same beat as frame 24 (Bea's key line, the bubble in the plaza),
  asserting `portrait.bea.surprised`, at the phone aspect (1278 × 590, as every capture) and again at iPad 4:3
  (1024 × 768).

## CI evidence

Run **#167** (`ci.yml`, id 45373047143) on `27ef36e`: **success**. Static checks green; Unity compile + EditMode/PlayMode
tests green, including `LinePortraitsTests` 3/3, `NonverbalUiTests` 8/8 and `NonverbalCaptureTests` 1/1. Frames
decoded from the job log:

- `docs/evidence/s004/portraits_wiring/79_bea_portrait.jpg` (magenta 0.00 %, mean luminance 0.272)
- `docs/evidence/s004/portraits_wiring/79_bea_portrait_disc_3x.png` (crop of the face disc, 3×)
- `docs/evidence/s004/portraits_wiring/79b_bea_portrait_4x3.jpg` (magenta 0.00 %, mean luminance 0.308)

`./tools/check-local.sh` was run before the push: everything passed, including core-tests (717).

### What I saw in the frames

- **79 (phone).** Bea's surprised badge (round eyes, "oh" mouth) is on the bubble's left edge, inside the orange felt
  and its stitch ring. The pumpkin stalk rises over the felt's top edge. It reads clearly at phone size, and the
  bubble, vignette, text line, next disc and Skip are where they were. Frame 24 now shows the same badge as well,
  because it is the same beat.
- **79b (iPad 4:3).** The badge renders correctly, but the 1060-unit bubble already filled the 4:3 width, so the face
  disc and the next disc are cut off at the left and right edges. This comes from the bubble's fixed width, not from
  the portraits: this stream only changes what is inside the disc. It is left for the portability and accessibility
  pass (F2, D25 "any aspect ratio"). No other capture tests 4:3.

### Note

The synthetic caption check in the PlayMode test logs one harmless `[Localization] Missing key:` warning, because
the view looks up a caption's (empty) name key. Real play never sends captions to the view.
