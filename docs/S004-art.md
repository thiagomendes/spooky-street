# S004 stream B: art for the seeds and the keepsakes

Branch `feature/s004-art`. Contract: `docs/S004-plan.md` §2.1, §2.2 and §3 "Stream B"; `docs/art-bible.md`.
Everything here is original and scripted (`tools/blender/`, headless Blender 4.0.2). Every asset was rendered and
looked at before it went into the game folder, and the game versions were reviewed again from the re-imported FBX.

This stream delivers art, names and places. Collecting, counting, saving, the notebook page, the trunk's Use
target and the cutscene beats belong to streams A and C; they build on `Core/World/KeepsakeArt.cs`.

## 1. What was made

| Asset | Files (under `Assets/SpookyStreet/Content/Resources/`) | Triangles | Texels | In the game now |
|---|---|---|---|---|
| Troupe photo | `Models/Environment/TroupePhoto.fbx` + `TroupePhoto_BaseColor/_Normal/_Emission.png` (1024) | 6,591 | 791 px/m | yes: on the hall partition |
| Owl crest sign | `Models/Environment/OwlCrest.fbx` + `OwlCrest_*.png` (512) | 2,040 | 466 px/m | yes: over the booth |
| Workshop mechanism (re-export, without the old photo) | `Models/Environment/WorkshopMechanism.fbx` + its three textures (1024) | 4,710 (was 5,420) | 222 px/m | yes |
| Collectible paper bat | `Models/Environment/PaperBatKeepsake.fbx` (atlas `Keepsakes_*.png`, 1024) | 474 | 795 px/m | model only (stream A) |
| Bat mobile | `Models/Environment/BatMobile.fbx` (atlas `Keepsakes`) | 2,866 | 373 px/m | model only (stream A) |
| Dress-up trunk | `Models/Environment/DressUpTrunk.fbx` (atlas `Keepsakes`) | 2,429 | 249 px/m | model only (stream A) |
| Star-lined cape | `Models/Costume/PipStarCape_BaseColor.png` (2048, Pip's atlas with the lining repainted) | — | as Pip | texture only (stream A) |
| Lantern pail | `Models/Costume/PipLanternPail.fbx` (atlas `PipCostume_*.png`, 512) | 924 | 652 px/m | model only (stream A) |
| Showman's hat | `Models/Costume/PipShowmanHat.fbx` (atlas `PipCostume`) | 1,267 | 652 px/m | model only (stream A) |
| Sketch pages 1–3 | `UI/SketchPages/sketch_page_1..3.png` (1024 × 768, RGBA) | — | — | sprites only (stream A) |

Manifests: `tools/blender/env/keepsakes_manifest.json`, `tools/blender/chars/costume_manifest.json`,
`tools/blender/env/interior_manifest.json` (mechanism entry updated).

### Scripts

| Script | What it does |
|---|---|
| `tools/blender/env/env_lib.py` `crest()` | the shared owl crest, at any size |
| `tools/blender/env/interior.py` `photo_motif`, `troupe_photo`, `build_troupe_photo` | the troupe photo; `build_mechanism(photo=False)` is the game mechanism |
| `tools/blender/env/owl_house.py` `crest_sign`, `CREST_AT` | the booth's sign and where it hangs |
| `tools/blender/env/keepsakes.py` | the paper bat, the mobile, the trunk |
| `tools/blender/env/env_export_keepsakes.py` | game export of the photo, the crest and the three keepsakes (about 1 minute) |
| `tools/blender/env/keepsakes_review.py`, `keepsakes_game_review.py` | review sheets: as built, and from the FBX in place |
| `tools/blender/chars/costume_pieces.py` | the three costume pieces: `export`, and `review` on the re-imported Pip |
| `tools/blender/pages/sketch_pages.py` | the three pencil pages (Freestyle lines, then graphite and old paper in numpy) |

## 2. Review sheets (`docs/evidence/s004/art/`)

| Sheet | Shows |
|---|---|
| `keepsakes_game_review.jpg` | **from the FBX, in place:** the hall at the play camera on a phone-shaped frame, then closer; the photo; the owl and moon puppets and the mount crest as the seed will push in on them; the crest on the house from the plaza and from the gate; the bat shut, fluttering, open; the mobile empty and with 30 bats; the trunk shut and open |
| `keepsakes_build_review.jpg` | the same assets as built (the approved-look pass before export), and the decorative ink-black bat beside the collectible for comparison |
| `crest_on_house_build.jpg` | the crest on the built owl house: the plaza view, phone shaped, and close |
| `costume_pieces_review.jpg` | on the re-imported `Pip.fbx`: each piece alone (front, side, back, glide front, glide side, fall), all three together, close-ups, and Pip as she is today |
| `sketch_pages_review.jpg` | the three pages at full size and at a third (about a notebook page on a phone) |

## 3. Decisions and how they differ from the plan

1. **The photo and the crest are their own models**, not parts of `WorkshopMechanism` and `OwlHouse`. The mechanism
   atlas has 222 px/m and the house atlas 69 px/m; a 0.35 m crest would have been 24 texels wide, and the seed
   cutscene pushes in on puppets 6 cm tall. With their own textures the photo has 791 px/m and the crest 466 px/m,
   and the house FBX and its atlas are untouched (no re-bake of approved art). The crest model can also be hung on
   the Players' later machines. The builders still live where the plan says (`photo_motif` in `interior.py`,
   `crest()` in `env_lib.py`, placement in `owl_house.py`), and the review builds show them in place.
2. **The crest hangs from the porch beam, on the door's axis**, 2.3–2.65 m up, in front of the bunting. From the
   plaza it sits right over the ticket shutter. On the door head itself (the plan's "over the shutter") the porch
   bunting hides it from the plaza, and the door leaf swings. It is dressing with no collider and no Use target.
   **Its painted face is lit paint** (`CrestSign_Glow`, `GlowLevels.CrestSign` = 0.6, far under the bloom
   threshold): CI run 153 showed the sign as a dark disc against the lit porch behind it (frames `70*` of that
   run), because it hangs under the eave facing the night. The backing board and hangers stay unlit.
3. **The photo** is 0.7 × 0.45 m, centred 1.76 m over the hall floor, between the dado rail and the frieze rail and
   left (north) of the service panel, so the push rod no longer crosses it. The game spawns `TroupePhoto` as a child
   of the mechanism named `KitPhoto`, so `CaseCutscenes.FindPhoto` works unchanged and the photo is shown and hidden
   with the mechanism as before. `CutsceneAnchors.Default(KitPhoto)` moved with it. **For stream C:** the reveal's
   photo push was framed for the old 0.4 × 0.5 m photo 0.7 m lower; it needs re-anchoring, as the plan expects.
4. **The flutter is two hinged wings, not a shape key.** `ModelImportRules` turns blend shapes off and the export
   pipeline writes plain meshes with named, pivoted children. `Wing_L` and `Wing_R` turn about their pivots' up
   axis; the model is exported open (see §4).
5. **Kit is the only recognisable Player.** The fifth (the bell) stands half out of the frame. Faces are paper
   discs with two dots. The puppets are gilt paper and do not glow: the glint is the game's.
6. **Sepia with one accent.** The photo is warm paper tones only, except the crest on its mount (night violet,
   orange, bone), which is what the seed's match cut to the booth needs.
7. **Star cape as a full atlas variant.** `PipStarCape_BaseColor.png` is `Pip_BaseColor.png` with only the
   lining's 132,603 texels repainted (3.2 % of the atlas), the baked fold shading kept. The stand-up collar is part
   of Pip's body mesh and stays berry.
8. **No cloth silver anywhere** (art bible): the palette here is ink, cream, pencil grey, night violet, plum,
   orange, candy, bone, brass and timber.

## 4. For streams A and C: how to use the art

All names and numbers are in `Assets/SpookyStreet/Core/World/KeepsakeArt.cs` (engine-free, tested).

- **Frames.** A model's root carries the Z-up to Y-up turn, so read a child's place with `transform.position`
  relative to the spawned root (or `root.InverseTransformPoint`), never with `localPosition` as if it were Y-up.
- **Kits.** Keepsake props: `new ModelKit(materials, "Models/Environment") { FallbackAtlas = KeepsakeArt.KeepsakesAtlas }`.
  Costume pieces: `new ModelKit(materials, KeepsakeArt.CostumeFolder) { FallbackAtlas = KeepsakeArt.CostumeAtlas }`.
  `ModelKit.Spawn` gives `*_Glow` children their emissive material; `GlowLevels.KeepsakeLantern` (0.9, under the
  bloom threshold) applies to every part named `Keepsake*_Glow`.
- **Paper bat** (`PaperBatKeepsake`). Origin at the body's lowest point, front toward the model's -Z, 0.34 m span.
  Exported open. Shut: `Wing_L.localRotation = rest * Quaternion.AngleAxis(-WingShutDegrees, Vector3.forward)`
  and `Wing_R.localRotation = rest * Quaternion.AngleAxis(+WingShutDegrees, Vector3.forward)` (150°). The hinge is
  the wing's local **Z**: the children of an imported model keep the builder's Z-up axes under the root's turn
  (this is what CI run 153 taught; `KeepsakeArtImportTests` proves the turn on the import). The flutter is a blend
  between shut and 0. Shut, the bat is ink black with a small cream belly; open, the cream inner fold
  and its pencil lines show. That is the difference from the decorative `PaperBat`, which keeps its glowing edge.
- **Bat mobile** (`BatMobile`). Origin at the hook on the eave; it hangs 1.02 m and is 0.66 m across. Children:
  `Ring1..3` (hoop with a gap), `Ring1..3_Close` (show at 10, 20, 30), `KeepsakeLantern1..3_Glow` (the lantern lit:
  show with the closing piece), `Hook_01..30` (show hook *n* with bat *n*; its pivot is where the bat hangs). A bat
  on a hook: `PaperBatKeepsake` at scale `MobileBatScale`, origin `MobileBatHang × scale` under the hook, wings
  nearly open, turned outward.
- **Dress-up trunk** (`DressUpTrunk`). The porch crate's size (0.6 m) and build, so it can replace the crate on
  the arrival porch. `TrunkLid` is hinged on the back edge; open is
  `rest * Quaternion.AngleAxis(TrunkLidOpenDegrees, Vector3.right)` (-105°, tested on the import).
  Inside lie the folded star cloth, the hat and the lantern (always there: showing only earned pieces is stream
  A's choice of when the lid opens, or a later split of the contents).
- **Costume pieces.** The hat and the lantern pail are meshes in Pip's model space in her rest pose. Instantiate
  under Pip's model root with an identity local transform while she is in her bind pose (right after
  instantiation), then `SetParent(bone, true)`: `HatBone` = `Head`, `PailBone` = `Pail`. To attach later, take the
  same relation from the prefab asset: `local = prefabBone.worldToLocalMatrix × prefabRoot.localToWorldMatrix`.
  With the lantern pail, hide the `Pip_Pail` renderer. The star cape is a texture swap on the material Pip's atlas
  meshes share: `Resources.Load<Texture2D>(KeepsakeArt.StarCapeTexture)` in place of `Pip_BaseColor`.
- **Sketch pages.** `Resources.Load<Sprite>(KeepsakeArt.SketchPage(n))`, 1024 × 768 with a ragged transparent
  edge. Each shows the creases of ten folds (5 × 2), so the page can assemble from ten pieces cut on that grid.
- **Troupe photo.** `OwlHouseStage.TroupePhoto`; children `Puppet_Owl`, `Puppet_Fox`, `Puppet_Moth`,
  `Puppet_Moon`, `Puppet_Bell` (pivot at each puppet's centre, for the glints) and `PhotoCrest` (pivot at the
  crest on the mount). **Crest:** `EnvironmentDressing.Crest`.

## 5. Narrative checks (plan §2.1, §2.2; bible §5, §6)

- No page shows the owl machine, the cam or arm, the treehouse route, the side yard or the bowing owl. Page 3
  shows washing lines between windows with washing on them, roof lamps on brackets and the moon puppet on a
  chimney; no trolley, no wire across the roofs.
- No art contains text (spec §7). The crest is wordless.
- The crest is not on the workshop mechanism and is not an examine target.
- The collectible bat does not glow and carries no mark that could read as a clue.

## 6. Tests

- `Tests/EditMode/Core/KeepsakeArtTests.cs` (also in `tools/core-tests`): the photo's place against the panel and
  rails, the cutscene anchor default, five puppets, the crest's place and size, 30 hooks in order, the
  `GlowLevels` test for the lantern pail and the mobile's lanterns (under 1, below every practical), sketch page
  paths, costume bones.
- `Tests/EditMode/Project/KeepsakeArtImportTests.cs`: the seven models import as static props without imported
  materials; the four texture sets import with their roles and sizes; the photo's size, puppets and order; the
  crest's size; the bat's hinges; the mobile's rings, lanterns and hooks; the trunk's hinge; the sketch pages as
  full-size sprites; the star cape at Pip's atlas size; the costume pieces where Pip's `Head` and `Pail` bones are;
  the wings shut over the front and the lid opens up and back by the documented turns.
- `Tests/Visual/KeepsakeArtCaptureTests.cs`: frames `70_booth_crest` (plaza, gameplay camera) and
  `69_troupe_photo` (hall, gameplay camera), with review frames `70r_booth_crest_gate` and
  `69r_troupe_photo_close`; asserts both models are spawned where `KeepsakeArt` says.
- `./tools/check-local.sh`: passed (632 Core tests).

## 7. Scorecard (art bible §7, six axes, target 4/5; from the Blender review sheets)

| Piece | Silhouette | Palette | Craft detail | Readable on a phone | Fits the world | Finish |
|---|---|---|---|---|---|---|
| Troupe photo | 4 | 5 | 4 | 4 (a framed group photo at play distance; the content is for the cutscene's push) | 5 | 4 |
| Owl crest | 5 | 5 | 4 | 4 | 5 | 4 |
| Paper bat | 4 | 4 | 4 | 4 | 5 | 4 |
| Bat mobile | 4 | 4 | 4 | 4 | 4 | 4 |
| Dress-up trunk | 4 | 4 | 4 | 4 | 5 | 4 |
| Costume pieces | 4 | 4 | 4 | 4 (hat and pail); 3 (star lining: it shows from the front and below, little from the play camera behind her) | 5 | 4 |
| Sketch pages | 4 | 4 | 4 | 4 | 4 | 4 |

These are the agent's scores from Blender renders. The in-game scores need the CI frames (§8) and the phone.

## 8. CI evidence

CIEVIDENCE

## 9. What is left

- **Stream A:** wire the bat, mobile, trunk, costume pieces and pages (this stream only spawns the photo and the
  crest).
- **Stream C:** re-anchor the reveal's photo push to the new frame; place the glints on `Puppet_*`.
- **Owner review** (plan: "shown as renders and frames for review before they enter the game"): the costume
  pieces, page subjects, crest design and the mobile are in the sheets above.
- **Open art points:** the star lining is hard to see from the play camera in a glide (the lining faces down and
  forward); if the owner wants it to read from behind, the stars would have to go on the cape's hem or outside,
  which is a design change. The trunk always holds all three things. The mobile is not yet placed on the arrival
  porch eave (stream A's `BatMobile.cs`).
