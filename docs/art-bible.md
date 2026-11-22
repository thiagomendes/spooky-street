# Art bible v2 (identity locked, S000.2)

Supersedes the S000.1 greybox baseline.

The owner approved the cast (2026-11-22, "aprovar e seguir"). The environment corner and the UI language were then
decided by the orchestrator under the owner's overnight delegation and are open to the owner's review. Evidence lives
in `docs/evidence/s000.2/`.

## 1. Style statement

Spooky Street is a handmade Halloween neighbourhood after sunset, and every object is a crafted object:
- felt, knit and stitched costumes;
- painted timber with bevels that catch porch light, plus paint wear;
- paper cut-outs with curled edges.

Intensity is **pronounced**: stitches, felt fuzz and paper curl read at phone size.

The characters are **humans in Halloween costumes**, never creatures. The world is a real street dressed for the
night, never a haunted ruin. All art is original and authored as reviewable scripts (spec v1.3, D16).

## 2. Palette (spec §6 v1.3 families)

| Role | Hex | Use |
|---|---|---|
| Night violet | `#2A2550` | sky, scrims, felt UI discs |
| Haunted purple | `#5B3A7A` | roofs, costumes, secondary felt |
| Plum | `#7A3E6E` | doors, pressed states |
| Jack-o'-lantern orange | `#EE7F2D` | pumpkins, the primary UI action, warm accents |
| Candy corn | `#F6D365` | highlights, sparkles, collectible edge |
| Witch green | `#8DBF3F` | accent only |
| Bone / warm paper | `#F4E6C8` | trim, paper cards, labels |
| Ink | `#1E1A26` | outlines, text on paper |
| House indigo | `#394461` | Aunt Kit's owl house |
| Cloth silver | `#DAE2EE` | **June's cape and its clues only**: never Pip, props or UI chrome |

Character costume colours are vivid. Pip's palette is in `tools/blender/chars/costume.py`. The environment palette
is in `tools/blender/env/env_lib.py`.

## 3. Light and value order

- Cool moonlight and violet sky against warm practicals: the porch lamp, jack-o'-lanterns, street lamps and lit windows.
- The **brightest sustained value is the active point of interest**. In the opening this is the round attic window.
- Lamps stay amber and below the attic window's peak; they never clip to white.
- The door, attic light and hedge separate in greyscale. Check every key frame with the greyscale render
  (`keyframe.py` writes one).
- Shadows are indigo, never black.
- A dark screen still shows required footing.

### The sky (S000.7)

The sky is paper and felt like everything else, never photographic (benchmarks: `docs/evidence/s000.7/`).

- **Paint:** horizon mist rising through haunted purple and night violet to a deep violet zenith, on grained paper,
  with three faint paper-cut cloud strips near the horizon. No aurora.
- **Moon:** a big full paper moon with stitched felt craters and a soft halo, upper left of the arrival view. The
  moon, halo, stars and shooting star all stay **below the bloom threshold**; the attic window remains the brightest
  point.
- **Clouds:** cut-paper sheets in the night-violet family, two sheets per cloud, with a pale moonlit cut edge. They
  drift slowly on three rings; the middle ring passes in front of the moon.
- **Life:** stars twinkle, a shooting star falls every 20–40 s, two or three paper bats cross the moon now and then.
  Pausing freezes all of it. Reduced motion removes the shooting stars and bats and slows the clouds and twinkle.

## 4. Characters

Benchmarks: `pip_vampire_v5_fable.png`, `cast_v5_standard_fable.png` and `pip_game_review.png`.

- **Proportions:**
  - stylised-human: kids about 4.5 heads, adults about 6;
  - big friendly eyes for everyone, including adults;
  - adults read as adults through proportion, face and clothes.
- **Finished anatomy:**
  - separate fingers and thumb;
  - shoes with sole, toe cap and laces;
  - shaped limbs with elbow and knee folds.
- **Cast:**
  - Pip: vampire-bat costume; the cape doubles as the glide.
  - Bea: pumpkin costume, with her bicycle.
  - June: star witch with the silver comet cape and its triangular tear.
  - Mr. Vale: friendly mummy, with the cauldron of sweets.
  - Aunt Kit: fortune teller, with the enamel owl brooch and the crystal-ball lantern.
- **Game budget per hero character:**
  - about 30k triangles and 2048 atlas + normal, plus a 1024 face map on a separate `*_Face` mesh;
  - at most 4 bone influences per vertex;
  - in-place clips, named per `Core/Animation/PipClips.cs`.

## 5. Environment

Benchmarks: `env_owl_house_v2.png`, `env_props_v2.png` and `env_keyframe_v2.png`.

- **Modular porch kit** (floor, steps, post, rail, wall, window, door, roof, corner). Variety comes from roof pitch,
  rail rhythm, paint wear and decor. Bespoke hero pieces: the owl house, the rooftop owl puppet and wind wheel, the
  maple with treehouse, and later the cam linkage.
- **Density:** a meaningful detail every ~1.5 m of walkable path, such as jack-o'-lanterns, bunting, bat garlands,
  candy or mailboxes. Never dress dead ends with shapes that look climbable.
- **Shapes:**
  - rounded, smooth-shaded sweets;
  - recognisable paper maple leaves;
  - trimmed hedges with a value gradient, not green boxes;
  - no faceted "low-poly" shards anywhere.
- **Enterable spaces (D24):** every room, cabin or hideout the player can enter is a closed box (walls, ceiling and
  floor with inner faces on every side) sized for the third-person camera: a boom of about 2 m behind Pip fits, and no
  beams or dressing hang in the 1.6–2.6 m camera band over walkable paths. Rafters and garlands go higher. Never leave
  an open side or a single-sided face that vanishes from inside. Enlarge modestly; keep proportions believable.
- **Game budgets:**
  - house structure ≤ about 30k triangles, with dressing as a separate mesh;
  - props 0.5–5k triangles;
  - one shared environment atlas where possible;
  - emissive parts named `*_Glow`;
  - animated parts (owl, wheel, door) are separate named children with correct pivots.

## 6. UI

See `docs/ui-language.md`, which is the source of truth, and the mocks in `docs/evidence/s000.2/ui/`.

- Felt and paper craft UI: night-violet felt discs with bone icons in play, and paper cards on modal screens.
- Jack-o'-lantern orange marks the one primary action.
- In play there is no paper larger than a label.
- Modal screens dim the world first.

UI decisions made overnight (owner may revise):
1. Paper and felt UI textures and the logo are **baked from a reviewed script** to 9-slice PNGs, like every other
   asset.
2. The hint radio lives **in the notebook only**, so the HUD stays minimal.
3. The notebook closes with an **X**. "Back to the street" appears only on the case-closed card (spec §3).
4. The Continue sub-label comes from saved facts. *(D26: it shows the open question's chips and a picture recap,
   never a route instruction such as "Find June"; spec v1.4 §5.)*
5. The volume sliders mock is deferred to the settings implementation.

## 7. Production rules

- Every asset is a script under `tools/blender/` (or `tools/ui_mock/` for UI art).
- A review render is looked at before it enters the game. The game version is reviewed from the **re-imported FBX**.
- Game assets go under `Assets/SpookyStreet/Content/Resources/Models/`:
  - plain git, no LFS;
  - `.fbx` and `.png` only;
  - metas via `tools/unity_meta.py`.
- In-game verification is the CI visual capture (`SpookyStreet.Tests.Visual`), scored with the six-axis visual
  scorecard. Finished work targets 4/5 on every axis (spec v1.3).
