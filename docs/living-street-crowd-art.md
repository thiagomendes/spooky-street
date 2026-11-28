# Living street: the crowd costume kit (art for the Phase 1 slice)

Branch `feature/living-street-crowd-art`. Design: `docs/living-street.md` §7 (crowd kit), §3.4 and §6 (coherence
rules), §8 (iPhone budget), Phase 1 in §9; approved as D34. Standards: `docs/art-bible.md`,
`docs/graphics-agent-standards.md`.

This is the art of the costumed trick-or-treat children and their grown-ups who walk Lantern Lane on Halloween
night. It is made for the dev-jogo agent who wires the walkers next: everything it needs to load, merge, animate
and budget them is named below. Nothing in the game spawns them yet.

## What is ready

Two base bodies on Pip's skeleton (same bone names and axes, `char_export.human_bones`), each with its own bind
proportions, and 59 swappable parts on one shared atlas. Nine walkers are authored from them: six costume themes
on two bodies.

| Walker id | Body | What a child sees | Parts |
|---|---|---|---|
| `GhostKid` | child | a bone-white sheet ghost down to the wrists, painted eye holes, a plum running stitch on the scalloped hem, hands and a plum pail peeking out under it | `Body_Indigo`, `Hands_T2`, `Shoes_Orange`, `Ghost_Sheet`, `Pail_Plum` |
| `SkeletonKid` | child | ink suit with painted bones all round (front: ribs, sternum, pelvis; back: spine, shoulder blades, ribs, hip bones; ribs on the flanks; limb bones front, back and outer side), white skull face paint with dark sockets, an ink hood printed as a bone skull dome with an ink rim round the face, bone gloves | `Body_Skeleton`, `Hands_Bone`, `Shoes_Ink`, `Head_Skull`, `Skeleton_Hood`, `Pail_Indigo` |
| `CatKid` | child | black cat onesie (soft charcoal felt) with a bone belly patch and bone wrist cuffs, hood with felt ears, a candy-yellow ribbon collar tied in a big bow at the nape, whiskers and nose painted on, a swinging tail with a big bone tip, paper bag | `Body_Cat`, `Hands_Ink`, `Shoes_Plum`, `Head_Cat`, `Cat_Hood`, `Cat_Tail`, `Bag_Paper` |
| `RobotKid` | child | cardboard box robot: box body with a paper panel, three paint buttons, a grille, tape and two knobs; box helmet with a face window and an antenna | `Body_Purple`, `Hands_T5`, `Shoes_Candy`, `Head_Robot`, `Robot_Helmet`, `Robot_Box`, `Pail_Plum` |
| `CandyCornKid` | child | a felt candy-corn cone (bone, orange, candy yellow, stitched bands), two hair puffs, striped tights | `Body_Night`, `Hands_T3`, `Shoes_Green`, `Head_Corn`, `Hair_Puffs`, `CandyCorn_Tunic`, `Bag_Paper` |
| `QueenKid` | child | little paper-crown queen: purple felt dress with a candy trim, long auburn hair, freckles, a paper lantern | `Body_Plum`, `Hands_T1`, `Shoes_Plum`, `Head_Queen`, `Hair_Long`, `Crown_Paper`, `Queen_Dress`, `Lantern_Paper` |
| `KingGrownUp` | grown-up | paper-crown king: plum tunic with an orange belt and a bone "ermine" trim, bone sleeves, a short beard, a bone paper lantern | `Body_King`, `Hands_T4`, `Shoes_Ink`, `Head_King`, `Hair_Short`, `Crown_Paper`, `King_Tunic`, `Lantern_Paper` |
| `CatGrownUp` | grown-up | a mum in a purple knit jumper with a cat-ear headband, whiskers, a tail and a paper bag | `Body_CatMum`, `Hands_T2`, `Shoes_Orange`, `Head_CatMum`, `Hair_Bun`, `Cat_Ears`, `Cat_Tail`, `Bag_Paper` |
| `SkeletonGrownUp` | grown-up | a dad in the painted skeleton suit (bones front, back and sides, as the child's) with skull face paint, carrying the family's pail | `Body_Skeleton`, `Hands_T6`, `Shoes_Bone`, `Head_Skull`, `Hair_Crop`, `Pail_Indigo` |

Skin tones vary across six (`t1`…`t6`); every face has the cast's big friendly eyes (two glints), brows, a small
open smile and blush, painted on the atlas.

## Files (for the wiring)

`Assets/SpookyStreet/Content/Resources/Models/Characters/Crowd/`

| File | What |
|---|---|
| `CrowdChild.fbx` | child body (1.05 m, 4.5 heads): skeleton `CrowdChild_Rig`, clips, 37 parts and their `_Lo` copies |
| `CrowdAdult.fbx` | grown-up body (1.70 m, 6 heads): skeleton `CrowdAdult_Rig`, clips, 22 parts and their `_Lo` copies |
| `Crowd_BaseColor.png` | the shared atlas, 2048 |
| `Crowd_Normal.png` | felt and paper relief, 1024 (LOD0 detail only, per the budget) |
| `Crowd_Emission.png` | the paper lanterns' glow, 512 |

The contract in code is `Assets/SpookyStreet/Core/Crowd/CrowdKit.cs` (engine-free): `CrowdKit.Walkers` (the nine
recipes), `ChildParts` / `AdultParts` (every part with its slot), `MeshNames(recipe, lod)`, `CostumeBones`, `Clips`,
`WalkSpeed(model)`, the budget constants and `ReservedWords`. The same table is
`tools/blender/crowd/crowd_kit.py`; the import tests check that the two agree with the FBX files.

- **Import.** `ModelImportRules` treats the folder as characters (Generic rig, clips cleaned and looped) and, new,
  imports these two models **readable** (`IsCrowdPath`), because the parts are merged on the CPU at spawn.
- **Slots.** Body, Hands, Shoes, Head, Hair, HeadPiece, Costume, Tail, Prop: one part per slot at most. The ghost has
  no Head (its sheet is the head).
- **Merging into one draw call.** Instantiate the body's prefab once per walker, keep the recipe's meshes
  (`CrowdKit.MeshNames(recipe, 0)` for LOD0, `…, 1` for LOD1), and combine them into one `SkinnedMeshRenderer`.
  Every part of a body is skinned to the same skeleton with the same bind pose for every bone (an import test checks
  this), so the merge is a concatenation remapped onto one bone array; each part has one sub-mesh and one material.
  Disable or destroy the unused part renderers.
- **Material.** `new ModelKit(materials, CrowdKit.Folder) { FallbackAtlas = CrowdKit.Atlas }`: the bodies have no
  texture set of their own, so `MaterialFor` gives the Crowd atlas. Walkers carrying `Lantern_Paper` need the glow
  variant, `GlowMaterialFor(model, intensity)`: the emission map is black everywhere but the lantern paper, so the
  whole walker can use it. Keep the intensity at or under about 0.9 so the lanterns stay under the attic window's
  level (art bible §3; the CI frames used 0.9).
- **Facing.** As every character export, the models face -Z: turn the model by `NpcAvatar.ModelYawOffset`.
- **Clips** (names from `CharacterClips`): `Idle` (90 frames, loops), `Walk` (loops; child 24 frames, grown-up 41),
  `Wave` (45, one-shot, starts and ends on Idle frame 0), `Talk` (72, loops). All in place. Idle is Pip's idle and
  Walk is Pip's walk, made by the same pose maths (`pip_anims.idle`, `pip_anims.walk`) fitted to each skeleton.
  Pip's own clips cannot drive these bodies directly: the cast's Generic clips key every bone's position, so a
  different body would take Pip's bone lengths.
- **Walk speed.** Move a walker at `CrowdKit.WalkSpeed(model)` (child 1.167 m/s, grown-up 1.173 m/s), or scale the
  Walk clip's speed by actual / that, so the feet do not slide. The grown-up's slower cadence keeps a parent and a
  child side by side without hand-holding (§7).
- **Costume bones.** `Pail` (child of `Hand_R`): every prop hangs from it and stays plumb, as Pip's pail.
  `Blink` (child of `Head`): the lids wait inside the skull and the clips bring them out for two frames (Idle frame
  52, Talk frame 30); Walk and Wave have no blink. `Tail_1` / `Tail_2` (children of `Hips`) swing the tail in every
  clip; they move nothing on walkers without a tail.
- **Height for spacing and standees:** `CrowdKit.ChildHeight` 1.05 m, `AdultHeight` 1.70 m (top of the skull).

## Budget (living-street §8)

| Walker | LOD0 triangles (budget ≈ 7k) | LOD1 triangles (budget ≈ 2.5k) |
|---|---|---|
| GhostKid | 5,146 | 1,677 |
| SkeletonKid | 5,150 | 1,639 |
| CatKid | 5,620 | 1,807 |
| RobotKid | 5,375 | 1,721 |
| CandyCornKid | 5,960 | 1,932 |
| QueenKid | 6,474 | 2,114 |
| KingGrownUp | 6,791 | 2,225 |
| CatGrownUp | 5,767 | 1,856 |
| SkeletonGrownUp | 5,226 | 1,662 |

Where the triangles go (child): body 1,700, both hands about 700 (five fingers each, finished anatomy at LOD0),
both sneakers about 600 (sole, toe cap, heel counter, tongue, collar), head about 640 (with ears, nose and lids),
costume 300–1,500, prop 150–520. LOD1 keeps the same atlas and bones at 22–36 % of each part. Every vertex has at
most four bone influences.

| Texture | Size | Note |
|---|---|---|
| `Crowd_BaseColor.png` | 2048 | the whole kit: 59 parts of both bodies; faces at 2.4x the texel density of the rest |
| `Crowd_Normal.png` | 1024 | baked at 2048 and halved; LOD0 detail only |
| `Crowd_Emission.png` | 512 | black except the lantern paper |

Atlas memory on the phone (ASTC 6x6 colour, 5x5 normal, with mips): about 1.6 MB for the 2048 colour, 0.6 MB for
the 1024 normal and 0.03 MB for the 512 emission, shared by every walker. One draw call per merged walker. The
paper standees (§8, beyond 18 m) are not part of this kit: the review lineup at 12 m shows the LOD1 meshes.

## Rules held

- **Reserved looks (§3.4 rule 6, D26).** No silver or foil, no capes at all (the king wears a tunic), no bat,
  pumpkin, star witch, mummy or fortune teller, no future resident's theme, no owl, fox, moth, moon or bell motif.
  `CrowdKitTests.NoPartUsesAReservedLook` checks the part names. The brief's candidate "pumpkin" and "pirate" are
  reserved by §3.4 (Bea's costume and a future resident's theme), so they are not in the kit. Witches are left out
  of the slice too, though §7 allows a green or plum one: a pointed hat is June's silhouette at phone size. The
  first king lantern was a round orange ball and read as a pumpkin on the first review; lanterns are now tall,
  ribbed and bone or candy yellow.
- **Never a clue language (§3.4 rules 1–2).** Talk is a chat with the free hand turning in front of the chest; it
  points at nothing and looks only along the walker's own forward. The first Wave (Bea's, on these narrower
  shoulders) stood level and read as a point; it is now raised high over the shoulder.
- **Background, a step simpler than the cast.** The art bible's calmer families (haunted purple, plum, night
  violet, indigo, bone, ink) with orange, candy yellow and witch green as accents; painted faces instead of
  modelled eyes; no stitch geometry (stitches are painted). Pip keeps the saturated violet and the red-lined cape,
  and stays the only cape on the street, so a child finds her first in the gameplay frames.
- **Felt and paper.** Every surface is baked from the shared craft materials (`style/craft.py`: felt, knit,
  cardboard, paper), with the cast's tinted ambient occlusion; prints are painted where they belong in 3D from a
  baked rest-position map (`crowd_paint.py`).
- **Original and made by code.** `tools/blender/crowd/` (data `crowd_kit.py`, parts `crowd_build.py`, clips
  `crowd_anims.py`, painting `crowd_paint.py`, pipeline `crowd_export.py`, sheets `crowd_review.py`).

## Review (rendered from the re-imported FBX files and the committed textures)

`blender -b -t 2 --python tools/blender/crowd/crowd_review.py -- <out_dir>`

| Sheet | What it shows |
|---|---|
| `docs/evidence/living-street/crowd_lineup.jpg` | the nine walkers beside Pip at the gameplay camera (3.8 m, 50°, phone frame) from the front and from behind, its greyscale, the LOD1 meshes at 12 m; the close lineup front and back |
| `docs/evidence/living-street/walk_<Walker>.jpg` (nine) | eight Walk frames from the front three-quarter and eight in profile, four from behind, two at the gameplay camera crossing in front of Pip, Wave and Talk key frames, the face open and mid-blink, each clip's last frame beside Idle 0 |
| `docs/evidence/living-street/ci_93_crowd_street.jpg`, `ci_93b_crowd_lineup.jpg`, `ci_93c_crowd_lineup_back.jpg` | the CI capture frames in the game's light (below) |

What the iterations fixed, by looking:

1. All walkers were stacked on one spot in the first lineup: the imported takes key the rig object, so the review
   moves each walker through an empty above it.
2. Hands and shoes stood still while the legs walked: the part names contain `_Hands` / `_Shoes`, so the weight
   rule found the wrong prefix and weighted them to bones that do not exist. Now they follow the hands and feet.
3. The ghost's arms poked through the sheet as dark patches. The sheet is now a wide bell down to the wrists; the
   hands and the pail peek out under the hem, the classic sheet-ghost read.
4. The king's round orange lantern read as a pumpkin (reserved); see Rules held.
5. Wave read as a point; see Rules held.
6. The cat ears were too small to read at the gameplay camera; they are larger and more upright. The grown-up's
   tail was scaled with her height and looked huge; it is now only a little longer than the child's.
7. The blink lids came out darker than the face (the occlusion pass found them inside the skull); they skip it.

## In the game's light (CI run 46067820415, green, commit `3713e22`)

`CrowdArtCaptureTests` (Visual) poses every walker in the game scene, as the slice will show it (its recipe's
parts only, the shared atlas through `ModelKit`, the glow material for lantern carriers at 0.9, Walk or Idle
sampled), and captures three frames: `ci_93_crowd_street.jpg` (the gameplay camera behind Pip on the path, the
walkers crossing on the sidewalk), `ci_93b_crowd_lineup.jpg` (the nine with Pip in the middle, close) and
`ci_93c_crowd_lineup_back.jpg` (everyone turned to the house). In the same run `CrowdArtImportTests` (7) and
`CrowdKitTests` (6) passed.

What the frames show: the costumes read by silhouette at the gameplay camera (sheet ghost, painted skeleton, box
robot, candy-corn cone, crowns, the cats' tails); the crowd sits in the night's calm purples and inks under the
porch light, so the lit attic window stays the brightest thing and Pip, in the middle, is still the one the eye
finds; the paper lanterns glow warm and well under the window; nothing is magenta and no part is missing. In the
game's light the crowd is darker than in the Blender sheets (moonlight and porch light only), which is right for
background; the faces are small at the gameplay camera and read as skin and two eyes.

## Known limits

- Texel density is about half the cast's: felt fibre and the painted stitches are soft in the close views. At the
  gameplay camera the costumes read by silhouette and colour, which is the bar for background.
- Painted features cross a UV seam in a few places (the ghost's right eye hole, the skull paint's lower edge on the
  grown-up); at phone size the seam does not show, close up a faint line does.
- The ghost's free arm passes through the sheet in Talk and Wave (as an arm through a slit); in Walk and Idle the
  arms stay under it (measured: `crowd_clip_check.py`, below). In Wave the side of the sheet now lifts a little with
  the raised arm, as a real sheet does.
- The cat hood's ears are modest from straight in front at the gameplay camera; the tail carries the cat read from
  the side and from behind.
- Walk and Wave have no blink (Idle and Talk do). A blink while walking would need the game to move the `Blink`
  bone after the animator; it is not needed for background walkers.
- The cat child is still a dark cat from behind (it is a black cat): at night her back reads by the candy bow, the
  bone tail tip, the cuffs and the ears' silhouette, not by the cloth. Against the dark hedge in 93c it is the
  darkest walker after Pip's cape.
- No hand-holding pair clip, by the design's choice (§7).

## Weak points fixed (branch `feature/crowd-weak-points`)

The review of the shipped kit found three weak points, seen by a child from the gameplay camera, often from behind:
the skeleton costume and the cat costume read too dark from behind and melted into the night street, and the
ghost's arm clipped through the sheet during the walk. The same pipeline re-exported both bodies and the shared
atlas (`crowd_export.py --bodies CrowdChild,CrowdAdult`); the door neighbours' file and atlas are untouched. Part
names, slots, skeleton, bind poses and clips are unchanged (the four clips and every bone head compare equal to
the shipped FBX); every walker stays inside the budget.

| Weak point | What the child sees now | How |
|---|---|---|
| Skeleton too dark from behind (child and, the same defect, the grown-up) | the skeleton reads from every side: bones on the back (spine, shoulder blades, three ribs a side, hip bones, sacrum), ribs round the flanks, limb bones front, back and outer side; the child's hood is a bone skull dome with two soft suture lines and an ink rim round the face paint | `crowd_paint.paint_skeleton` and `paint_skull_hood` (the hood's `skull` option); paint only, no new geometry |
| Cat too dark from behind | a candy-yellow ribbon collar tied in a big bow at the nape, bone wrist cuffs, a bigger bone tail tip, and the felt a step lighter than ink (soft charcoal `soot`, 0x3D3644), still a black cat | `Cat_Hood` `collar` (painted band and stitch, modelled bow: +136 vertices), `Body_Cat` `cuff`, `Cat_Tail` `tip_scale` 1.4; CatKid LOD0 5,368 → 5,620 triangles |
| Ghost's arm through the sheet in the walk | the arms swing under the sheet and the sheet's sides sway with them; the hands and pail still peek out under the hem | weight rule `sheet` (`crowd_export.sheet_weights`): the lower sides of the bell take up to half of that side's arm (`SHEET_ARM`), nothing at the chest or the middle of the front and back |

Glow: nothing new glows. The collar and bow are felt (no emission); the emission map is still black but for the
lantern paper. No reserved look: no silver (the bone is the kit's warm bone), no bell on the collar, no moon, star,
bat, owl, fox or moth.

The ghost's clipping is measured on the exported FBX, every frame of every clip
(`blender -b -t 2 --python tools/blender/crowd/crowd_clip_check.py -- [--crowd DIR] [--report FILE]`): arm and hand
vertices above the hem that lie outside the sheet's outer surface. Walk: before 20 of 25 frames (up to 60 mm, the
free hand swinging forward through the front of the sheet, the forearm through its side), after 0 of 25 (the
check also passed at a pull of 0.42, so 0.5 keeps a margin; 0.28 failed). Talk and Wave keep the known limit
above. Reports: `docs/evidence/living-street/crowd-fixes/clip_check_before.json`, `clip_check_after.json`.

Before/after sheets, rendered from the shipped files and the new files side by side, in the review light and in a
night light matched to the capture frames (a dim violet sky light that puts white felt at about sRGB 51, 44, 87 as
in 93b and 93c, a weak moon and a warm porch light beyond the walkers)
(`blender -b -t 2 --python tools/blender/crowd/crowd_fix_review.py -- <out> --before <shipped dir>`):

| Sheet | What it shows |
|---|---|
| `docs/evidence/living-street/crowd-fixes/fix_lineup.jpg` | the nine walkers with Pip from behind at the gameplay camera, review light and night light, close from behind and from the front in the night light; before left, after right |
| `docs/evidence/living-street/crowd-fixes/fix_SkeletonKid.jpg`, `fix_SkeletonGrownUp.jpg`, `fix_CatKid.jpg` | front, back, profile (review light), back and front (night light), the gameplay camera crossing (night light); before above, after below |
| `docs/evidence/living-street/crowd-fixes/fix_GhostKid.jpg` | the same, plus eight Walk frames from the front three-quarter, in profile and from behind, and Wave and Talk, before and after |

The regular sheets (`crowd_lineup.jpg`, `walk_<Walker>.jpg`) are re-rendered from the new files; the walkers that
did not change look as before.

What the iterations changed, by looking: the first ghost weights (pull near the arm only) left the free hand
through the front of the sheet, because the sheet is a wide bell and the hand meets its front panel only when it
swings; the pull now follows the sides of the bell. A first night light was far too dark against the capture
frames and was matched to them. The first collar was orange and thin and did not read at night (orange is dark in
the game's violet light); it is candy yellow, wider, with a bow 1.7 times larger. The skeleton's profile was still
dark, so the ribs carry on round the flanks and the limb bones down the outer side.

## Not done in this branch, and why

- **The three trick-or-treat porch houses and the door pieces** (§9: "East lane: three porch houses from the kit…",
  artista-3d 2 days + dev-jogo 1) and **the paper lantern table pieces** (§9: artista-3d 1 day) are not in this
  branch. The crowd kit was the brief's goal and took this session; the porch houses need the east lane's layout,
  which does not exist yet; the door gags are not designed yet; and §4.2 lists lantern shapes "moon, bat, star,
  pumpkin", where the moon is one of the Players' motifs that §3.4 rule 6 reserves before its case and the star is
  June's star-witch language. That conflict needs the main session's call before the lantern pieces are modelled.
- **Paper standees** (§8, the impostors beyond 18 m) are listed under the slice's performance work, not the kit.
- **The older-child body** and the rest of the 11 themes are the scale phase (§7 Targets).
