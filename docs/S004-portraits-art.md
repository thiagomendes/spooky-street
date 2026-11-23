# S004 stream G, art: dialogue portraits

Branch `feature/s004-portraits-art`. Plan: `docs/S004-plan.md` §2.6 and §3 "Stream G". This is the art half: the
25 portraits, their atlas, the name table and the tests. **Nothing is wired into the dialogue UI**; that is the
wave-2 wiring stream (`DialogueView`, the PlayMode check and capture frame 79).

## What is in the game now

| File | What |
|---|---|
| `Assets/SpookyStreet/Content/Resources/UI/Portraits/portraits.png` | 2048 × 2048 atlas: 5 speakers (rows: Pip, Bea, Mr. Vale, June, Aunt Kit) × 5 babble emotions (columns: neutral, happy, surprised, thinking, whisper), 400 px cells on a 408 px pitch |
| `Assets/SpookyStreet/Core/Nonverbal/PortraitAtlasLayout.cs` | engine-free rect table, written by the script |
| `Assets/SpookyStreet/Editor/UiSpriteImportRules.cs` | the atlas imports with the other UI sprites' settings, at its full 2048 px |
| `tools/blender/export/portraits.py` | the whole pipeline: render, badges, atlas, C# table, review sheets |

## The mapping the wiring stream uses

```csharp
// SpookyStreet.Core.Nonverbal, engine-free
PortraitAtlasLayout.TryGet(line.Speaker, LineVoices.For(line.Key), out PortraitRect rect)   // false for captions
PortraitAtlasLayout.SpriteName(Speaker.Bea, BabbleEmotion.Surprised)                        // "portrait.bea.surprised"
```

- Sprite names are `portrait.<speaker>.<emotion>` in lower case: `pip`, `bea`, `vale`, `june`, `kit` ×
  `neutral`, `happy`, `surprised`, `thinking`, `whisper` (the `Speaker` and `Audio.BabbleEmotion` enum names).
- `PortraitRect` holds the pixel rect with the origin at the bottom left, as `PuppetRect` does, so
  `Sprite.Create(texture, new Rect(X, Y, Width, Height) * k, ...)` with `k = texture.width / AtlasSize` works the
  way `PuppetAtlas.Get` does. `ResourcePath` is `UI/Portraits/portraits`.
- The emotion is the line's existing `LineVoices` row, so a translation never changes the face, and a line without
  a row shows the neutral portrait.

### How a cell is laid out

Each cell is a finished badge, not a bare bust:

- a night-sky paper disc with an ink rim, centred at (`DiscCenterX`, `DiscCenterY`) = (0.50, 0.43) of the cell
  from the bottom left, radius `DiscRadius` = 0.42 of the cell;
- the bust clipped to the disc, with an ink outline (the puppet look);
- the head, hair and hat free to rise above the disc, up to the top of the cell (Bea's pumpkin stalk, June's whole
  witch hat). Shoulders and hands never leave the disc.

To put a badge on a round frame of diameter `D`, draw the cell `D / (2 × DiscRadius)` wide and move it so the
disc's centre is on the frame's centre; the image is not square around the disc, so do not centre the cell. The
review sheets do exactly this with the badge's disc at 80 % of the felt disc, inside the stitch ring, which is
where the puppet face sits today. The sky backing is baked in because Bea on her orange felt and Pip on night
felt had no contrast without it; the speaker's felt colour stays as the frame around it.

## How the portraits are made

Everything comes from the game's own FBX and baked maps (`Resources/Models/Characters/<Name>/`), imported the way
the witness and gesture reviews import them, so the portrait is the character the child sees in the street. Mr.
Vale's wristwatch is in his FBX; it shows in his whisper portrait, on the hand by his mouth.

The faces are not texture-painted and have no shape keys: in every `<Name>_Face` mesh the eyes, lashes, brows and
mouth are separate islands of geometry, and the 1024 px face map carries only skin, blush and baked contact
shadows. The script therefore works on the geometry:

1. **Reads the face.** It finds the skull, scleras, irises, pupils, glints, lashes, brows, nose and mouth parts by
   where they sit and by their colour in the face map, and stops with an error if a face lacks one.
2. **Moves what exists.** Gaze (iris, pupil and glints slide over the eye and keep their lift), eye size, pupil
   size.
3. **Adds what the model lacks** (plan §2.6):
   - brows redrawn as bolder felt lines in the model's own colour and place (the originals are about 2 mm thick
     and vanish at phone size), raised and tilted per emotion; where a fringe hangs between a brow and the camera
     the brow is drawn on the fringe, the usual cartoon convention, so the expression does not disappear under a
     haircut (a hat brim further out still hides it);
   - eyelids in skin sampled from the face map and a closed-eye arc for the happy face;
   - a mouth per emotion (wide open smile with tongue, a round "oh", a closed "hm" pushed to one side), in the
     colours of the model's own mouth, with Bea's teeth and Pip's fangs where the model has them.
4. **Repairs the face map in memory** where a moved brow or mouth would leave its baked shadow behind. The file
   on disk is never touched, and nothing is written back to any model.
5. **Poses.** The first frame of the rest clip (`Idle`, `SitIdle` for Mr. Vale), the head upright to the camera,
   then a small nod, turn and lean per emotion. For the whisper the left arm is brought up by IK and the hand set
   edge-on beside the mouth.
6. **Renders** through one camera (90 mm, level with the face, slightly to the character's right) and one light
   rig for all 25 (warm key from the upper left, cool fill, violet rim, a low warm bounce under hat brims, violet
   ambient so shadows stay indigo: art bible §3), Eevee, Standard view transform so the palette matches the game.
7. **Cuts the badge** (Pillow): sky disc, bust, pop-out head, ink outline, reduced from 800 px to the 400 px cell.

| Emotion | Head | Brows | Eyes | Mouth | Hand |
|---|---|---|---|---|---|
| neutral | straight | as modelled | as modelled | the model's own | |
| happy | a small lean | raised | shut in two arcs | wide open smile, tongue (Bea's teeth, Pip's fangs) | |
| surprised | back a little | high | larger, small pupils | a round "oh" | |
| thinking | turned and tilted | one up, one down | looking up and aside | closed, pushed to one side | |
| whisper | leaning in | inner ends up | glancing aside | small "oh" | cupped by the mouth |

The emotions show what the speaker feels while speaking (`LineVoices`), never what a line means for the case
(D26): there is no "lying" or "guilty" face, and no portrait marks a clue.

## Re-running

```
blender -b -t 1 --python tools/blender/export/portraits.py -- --cache <dir>     # 50 Eevee frames, 15 to 30 minutes
python3 tools/blender/export/portraits.py --pack-only --cache <dir>             # badges, atlas, table, sheets only
```

`--only Bea,Pip`, `--emotions happy,whisper`, `--size 480 --samples 16` and `--no-atlas` make quick partial looks
(a partial run never writes the atlas). Run one Blender at a time.

## Evidence

Review sheets, written by the script from the atlas cells (`docs/evidence/s004/portraits/`):

| File | What |
|---|---|
| `portraits_phone_1x.png` | all 25 on their felt discs at phone pixels: the bubble's face disc is 150 reference units = 234 px on an iPhone at 3x |
| `portraits_phone_2x.jpg` | the same at twice the size |
| `portraits_96px.png` | the plan's legibility bar: 96 px discs |
| `portraits_cells.png` | the raw atlas cells on a checker (edges, pop-out, gutter) |

What the sheets show, and what was changed on the way (each round was rendered and looked at):

- **Round 1** (Pip and Bea): the faces were washed out to near white, raised brows vanished under the fringes, and
  the whisper (half-closed lids, sideways glance, small mouth) read as "unimpressed". Lights cut by half; brows
  drawn over a fringe; the whisper got wide eyes and a hand by the mouth instead of lids.
- **Round 2**: the first hand sat flat over one eye. It was lowered to the mouth and turned edge-on. A freckle
  next to Bea's eye had been taken for part of her iris and moved with the gaze; irises are now told apart by size.
- **Round 3** (all 25): June's hat was cut flat by the top of the cell and her brim by its sides when her head
  leaned. Her frame is wider (her face is about a quarter smaller than the others' as a result) and her head moves half
  as much.
- **Final**: at 234 px every face and expression reads at a glance. At 96 px the five are still distinct for
  Pip, Bea, Mr. Vale and Aunt Kit; for June happy, surprised and whisper are clear, and thinking differs from
  neutral only by the eyes and the mouth line, which is small under the hat.

Known limits, left as they are:

- Bea's raised brows (happy, surprised) stay hidden under her pumpkin brim; the eyes and mouth carry those two.
- Mr. Vale's left eye is under his wraps in the model, so his expressions are one-eyed; his neutral mouth is the
  model's own closed smile, mostly behind the moustache. His arm does not quite reach (the wrist stops 2.4 cm
  short), so his whisper hand sits over the nose and mouth rather than beside them.
- Pip's brows are the colour of her hair, so on the fringe they show only as short dark strokes.
- The light skin tones are bright under the warm key; they keep their blush and shape but sit near the top of the
  range.

## Tests

- `Tests/EditMode/Core/PortraitAtlasLayoutTests` (Unity and `tools/core-tests`): every speaker × emotion present
  with its name, captions have none, cells inside the atlas without overlap, the disc inside the cell with room
  above, every `LineVoices` line resolves to a portrait of its emotion.
- `Tests/EditMode/Project/PortraitAtlasImportTests` (Unity only): the atlas is a 2D sprite texture, no mipmaps,
  clamp, sRGB with alpha, not readable, 2048 px, ASTC 4×4 on iOS, loads from `Resources`; every cell has a drawn
  disc where the table says, cuts into a sprite, and nothing bleeds into the gutters.

## Checks

CHECKS

## Left for the wiring stream (wave 2)

- `DialogueView`: show the badge on the card's felt frame and on the bubble's face disc by the line's emotion; the
  initial stays as the fallback when the atlas is missing. The head rises above the frame by up to 18 % of the
  disc's diameter, so the name tab and the bubble edge need that much room (June's hat is the tallest).
- A small runtime cutter like `PuppetAtlas` (one cached sprite per name).
- The PlayMode check that a line picks its emotion's portrait, and capture frame 79.
- The second `BabbleEmotion` enum in `Core/Cutscenes/CutsceneAudio.cs` has the same members; the table uses the
  `Core.Audio` one that `LineVoices` returns.
