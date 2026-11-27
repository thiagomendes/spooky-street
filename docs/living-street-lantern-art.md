# Living street: the paper lantern table's art (Phase 1 slice)

Branch `feature/living-street-lantern-art`. Design: `docs/living-street.md` §4.2 (the paper lantern table), §3.4
rule 6 and §6 (coherence rules); D34 and D35 in `docs/decisions.md`. Standards: `docs/art-bible.md`,
`docs/graphics-agent-standards.md`.

At a table on the Green, Pip picks a shape and a colour from pictures and folds a paper lantern. It then hangs lit on
her porch, and from then on trick-or-treat groups ring her bell. This branch makes the art for that and nothing else:
the table's interaction, saving and the groups are the slice's code (`feature/living-street-slice`), which can drop
these pieces in at the swap point below.

## What is ready

| Piece | What a child sees | Model | Triangles |
|---|---|---|---|
| Bat lantern | a little bat with ink eyes, one bone fang and a smile, two accordion-folded tissue wings on ink ribs, a tassel | `PaperLanternBat` | 1,404 |
| Cat lantern | a cat's head with pointed ears, almond eyes, nose, a "w" mouth, whiskers drawn on and wire whiskers past the cheeks, a tassel | `PaperLanternCat` | 1,692 |
| Ghost lantern | a round-topped ghost with a scalloped hem, two arm bumps, oval eyes and an "o" mouth | `PaperLanternGhost` | 1,528 |
| Leaf lantern | a maple leaf (big top lobe, two side lobes, two small ones by the stem) with ink veins like stained glass, a paper stem | `PaperLanternLeaf` | 1,844 |
| The table | a child's folding craft table (painted top, a plum edge band, orange crossed legs with hinge bolts), sheets of paper fanned in the four colours, a paste pot with its brush, a pleated strip half folded on kraft paper, a bat and a leaf cut out, and behind it two poles with a string of the four finished lanterns lit (bat plum, cat orange, ghost yellow, leaf green) | `LanternTable` | 4,913 |
| The porch hook | a painted post clamped to the porch rail's front corner, a fretwork bracket, a curled ink iron arm with a hook eye, and a round orange doorbell plate with a bone push button at a child's reach | `LanternHook` | 1,168 |
| Picker pictures | one per shape (the lantern lit, in neutral paper, ink face and outline) and one per colour (a lit square of that tissue with a folded corner) | `UI/LanternPicker/*.png` | 256 × 256 each |

Every lantern is a figure lantern folded from tissue on a thin frame: two domed paper faces joined by a pleated paper
band, an ink card rim along both edges (the art bible's outline), the face cut from ink card on both sides (so it
reads from the street and from the porch), a wire bail to a ring at the top. The paper glows softly from inside:
brightest a little below the middle, where the candle stands, darker toward the rim, and each pleat a shade darker
than the next, so the folds read even when the lantern is only lit from within.

## Files

`Assets/SpookyStreet/Content/Resources/Models/Environment/`: the six FBX files and one shared atlas,
`Lanterns_BaseColor.png`, `Lanterns_Normal.png`, `Lanterns_Emission.png` (1024; lanterns about 480 px/m, the table
and hook about 220–240 px/m). `Assets/SpookyStreet/Content/Resources/UI/LanternPicker/`: `lantern_shape_<bat|cat|ghost|leaf>.png`
and `lantern_colour_<orange|plum|green|yellow>.png`.

The contract in code is `Assets/SpookyStreet/Core/World/LanternArt.cs` (engine-free, tested): shape and colour IDs,
model and child names, the paper and light colours, sizes, the hook's hang point, the light point per shape, the
picture paths. The runtime helper is `Assets/SpookyStreet/Runtime/World/PaperLanternArt.cs`.

## Swap point (for the slice)

The shape and colour IDs are the slice's own (`bat`, `cat`, `ghost`, `leaf`; `orange`, `plum`, `green`, `yellow`),
so `LanternCraft.ShapeId` / `ColourId` map straight onto `LanternArt`.

- **Kit.** `var kit = PaperLanternArt.Kit(materials);` (that is
  `new ModelKit(materials, "Models/Environment") { FallbackAtlas = "Lanterns" }`).
- **A lantern.** `PaperLanternArt.SpawnLantern(kit, shapeId, colourId, parent, hangPoint, yaw)`. Its origin is the top
  of its hang ring, so place it at the hook's hang point; it hangs down from there. It replaces `BuildLantern` in
  `LivingStreetSet`. A refold of the same shape is `PaperLanternArt.Tint(kit, lantern, shapeId, colourId)`; a new
  shape is a new spawn. The lantern may sway: turn its root about its origin.
- **Colour approach: tint by material.** The paper is one child, `PaperLantern_Glow`, baked as neutral warm-white
  paper with its glow. `PaperLanternArt` gives that one renderer a copy of the atlas glow material with
  `_BaseColor` = `LanternArt.PaperRgb(colour)` and `_EmissionColor` = `LanternArt.LightRgb(colour)` (linear) ×
  `GlowLevels.PaperLantern`. The ink, wire and tassel are the root mesh on the plain atlas material and are never
  tinted. Materials are cached per colour (at most 16 for all lanterns).
- **The table.** `kit.Spawn(LanternArt.Table, parent, position, yaw, solid: true)` replaces the `PartyTable` at the
  Green. Origin on the ground under the table's middle; Pip folds at its front, the model's -Z, so yaw 180 turns the
  front north (the slice's `TableYaw` 200 does that). 1.3 × 0.6 m, top at 0.62 m, poles to 1.78 m. Its lit samples
  are `PaperLanternSamples_Glow` (colours baked in; `ModelKit` lights them at `GlowLevels.PaperLantern`). Nothing on it
  moves.
- **The hook.** `kit.Spawn(LanternArt.Hook, parent, (PorchHookX, PorchHookY, PorchHookZ), PorchHookYaw)`: on the
  deck at the slice's crook post spot (−1.95, 0.6, −13.15), yaw 180 like the kit pieces, so the bell plate faces the
  plaza and the arm reaches west over the rail: the lantern hangs over the lawn at about (−2.30, 2.59, −13.13), its
  middle at about 2.4 m (the slice used 2.35). Read the hang point from the spawned hook,
  `PaperLanternArt.HangPoint(hook)` (the child `LanternHang`), not from constants. It replaces the greybox
  `LanternPost`, `LanternArm` and `PipBell`.
- **The bell.** `BellButton` is the push button on the post's front, 0.98 m over the deck; press it along the model's
  +Z (about 8 mm) when a group or Pip rings. A round push button rather than a hanging bell, on purpose: a bell shape is
  one of the Players' puppet motifs (§3.4 rule 6).
- **Where the light goes.** The glow is in the paper (`*_Glow`, emission); no real-time light is needed, and the CI
  frames use none. If the porch wants a warm spill on the boards, one small point light at
  `LanternArt.DropToCentre(shape)` under the lantern's origin, in the light colour, range about 2 m, intensity under the
  porch lamp's, no shadows, on the porch lantern only (never on the table's samples).
- **The picker's pictures.** `Resources.Load<Sprite>(LanternArt.ShapeTile(shape))` is neutral near-white paper with an
  ink face and outline: set `Image.color` to the chosen paper colour (as `LanternView.Refresh` already does) and the
  picture becomes that lantern, ink staying ink. `LanternArt.ColourTile(colour)` is already in its colour: draw it with
  `Image.color` white. Both import as single sprites (`KeepsakeArtImportRules`).

## Rules held

- **D35 shapes.** Bat, cat, ghost and leaf only; no moon, star or pumpkin anywhere (the table's samples and cut-outs
  included). A round orange glowing shape read as a pumpkin in the crowd kit's first review, so every lantern has a
  silhouette that is not round (ears, wings, a hem, lobes) and none has ribs around a ball. `LanternArtTests` checks
  the names.
- **No clue materials (§3.4 rule 6).** No silver and no lavender foil: the metal is ink-painted iron, every paper and
  light colour is saturated and none is blue-violet (tested). The bell is a push button, not a bell shape.
- **Glow under the attic window.** `GlowLevels.PaperLantern` = 0.9, the crowd kit's lantern level, under the bloom
  threshold and far under the attic window (4.0) and the street lamps (1.3).
- **Pip is a girl** in every text here and in the code comments.
- **Original, made by code.** `tools/blender/env/lanterns.py` (builders), `env_export_lanterns.py` (export, about
  40 s), `lanterns_review.py` (the sheet from the FBX), `lanterns_tiles.py` (the pictures),
  `lanterns_build_review.py` (a quick as-built look while shaping).
- **Phone cost.** 1.4–1.8k triangles per lantern, the table 4.9k (props 0.5–5k), the hook 1.2k; one 1024 atlas for
  all six (about 0.6 MB colour + normal + emission in ASTC); one draw call per renderer (a lantern is two: ink and
  paper).

## Review

`blender -b -t 2 --python tools/blender/env/lanterns_review.py -- out.png final` renders from the exported FBX and the
committed atlas, the paper tinted as the game tints it, beside the shipped porch, lamp, jack-o'-lanterns and Pip at
their game glow levels.

| Sheet | What it shows |
|---|---|
| `docs/evidence/living-street/lantern_review.jpg` | the four shapes in neutral paper and the four colours, front and three-quarter; the gameplay camera behind Pip at the table, the table from the side and close; the hook at the porch corner and close with a lit cat; Pip's porch from the arrival camera and from the plaza, and its greyscale |
| `docs/evidence/living-street/lantern_picker_tiles.jpg` | the shape pictures as committed, then multiplied by each paper colour on a bone card (as the picker shows them), and the colour pictures on night violet and on bone |
| `docs/evidence/living-street/ci_95*.jpg` | the CI capture frames in the game's light (below) |

What the iterations fixed, by looking:

1. The pleated band bowed out past the ink rim and the outline became a dashed zigzag: the band's puff and folds are
   smaller and the rim reaches further past the edge, so from the front the ink outline is whole and the pleats show
   from the side.
2. The leaf's side veins were half hidden under the domed paper (straight ribbons over a curved face): ribbons now
   follow the dome every 1.5 cm. The first leaf outline read as a cabbage; it is now a clear maple with rounded sinuses.
3. The bat's body was too small for its wings and read as a moth: the body and ears are larger.
4. The table's samples were small from the gameplay camera and the bat's wing hid behind a pole: the samples are
   larger, the poles taller and the bat hangs further in. The table is wider.
5. The lit plum read as hot pink: its light is a deeper berry.
6. The bell plate was modelled lying flat; it now faces the plaza.

## In the game's light

Filled in after the CI run.

## Known limits

- The lanterns' texel density (about 480 px/m) is fine for the gameplay camera and the picker; the table and hook
  (about 220–240 px/m) are soft in a close-up. The atlas is half full because the ink rims are ring-shaped islands; a
  tighter pack would only sharpen the table.
- The faces are the same on both sides; seen from behind, the cat's single-sided details (the bat's one fang) are
  mirrored.
- The table's samples are part of the table and always lit; to show unlit samples the slice would hide
  `PaperLanternSamples_Glow`.
- In the CI capture the table stands beside the path in the case block, because this branch's scene has no Green.
