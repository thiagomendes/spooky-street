# Living street: the three trick-or-treat houses (art for the slice's east lane)

Branch `feature/living-street-houses-art` (from `feature/living-street-slice`). Design: `docs/living-street.md` §4.1
(doors), §7 "Houses", §3.4 rule 6 and §6 rule 1 (reserved looks, no clue language), §8 (iPhone budget); the slice:
`docs/living-street-slice.md`. Standards: `docs/art-bible.md`, `docs/graphics-agent-standards.md`.

## Resumo para o dono

**O que a criança vê:** as três casas da rua nova agora são casas de verdade, cada uma de uma família diferente.
O Sr. Fogg mora numa casa alta, verde-garrafa, com telhado de mansarda e uma torrezinha em cima da porta laranja; na
porta tem um esqueleto de papel pendurado, na marquise uma aranha de feltro na teia, e na escada uma pilha de
abóboras sorrindo. A Vovó Pearl mora num chalé baixo e cor-de-rosa com uma varanda comprida, cadeira de balanço com
manta de tricô, floreiras, uma cesta de maçãs, uma guirlanda de folhas de papel na porta amarela e um lampião grande
que esquenta a luz quando ela abre. Os Abbott moram na casa de dois telhados lado a lado (um mostarda, um roxo), com
toldo listrado, fantasminhas e um gato de papel nas janelas acesas e uma porta roxa arredondada com dois fantasminhas
de papel. Quem atende: o Sr. Fogg de suéter creme, gorro laranja e barbão; a Vovó Pearl de cardigã rosa, avental,
óculos e coque; os dois Abbott de suéteres listrados iguais (um laranja, um roxo) e pantufas iguais.

**O que não muda:** as campainhas são botões (ninguém bate na porta), as janelas acesas e os lampiões ficam bem mais
fracos que a janela redonda da casa da coruja, não há janela de sótão acesa nem janela redonda, nada de prata, lavanda,
coruja, raposa, mariposa, lua, estrela ou sino, e nenhum vizinho usa fantasia.

**Para ligar no jogo:** o agente do jogo troca os nomes (seção "The swap"); os vizinhos novos já aparecem sozinhos.

## What is ready

| Piece | Model | What a child sees |
|---|---|---|
| Mr. Fogg's house | `LaneHouseFogg` | tall bottle-green clapboard house under a mansard roof with iron cresting and two round-shouldered dark dormers, a square entrance tower over his door with a pyramid cap and an orange finial, orange-and-plum bunting, a felt spider on a paper web under the door hood, three jack-o'-lanterns stacked by the steps, a lit window with a paper ghost |
| His door | `LaneDoorFogg` | orange four-panel door with plum panels, a big brass knob and kick plate, a silly paper skeleton pinned on it |
| Nana Pearl's house | `LaneHousePearl` | low, wide rose cottage under one big roof that sweeps down over a long porch, a gabled dormer (dark), a fieldstone chimney, picket rails, flower boxes, a rocking chair with a knitted blanket, a basket of apples, a striped welcome mat, two lit windows (a paper cat in one) |
| Her door | `LaneDoorPearl` | candy-yellow door with a little four-pane window and lace, a wreath of paper maple leaves with a plum bow |
| Her lamp | `LanePorchLampPearl` | a bigger six-sided brass carriage lantern; the slice warms it while she answers |
| The Abbotts' house | `LaneHouseAbbotts` | the twin-gable house: a mustard gable and a taller plum gable side by side, an orange-and-plum striped awning over a big lit window with paper ghosts and a cat, a paper chain, two matching jack-o'-lanterns |
| Their door | `LaneDoorAbbotts` | round-topped plum plank door with iron straps and two little paper ghosts side by side |
| Lamps | `LanePorchLamp<House>` | Fogg's square iron lantern with an orange cap, Pearl's brass one, the Abbotts' plum one with a candy cap |
| The neighbours | `CrowdNeighbours` (crowd kit) | Mr. Fogg: cream cable jumper, orange bobble beanie, big brown beard and moustache, plum slippers, a tin of sweets. Nana Pearl: rose cardigan with a bone placket and candy buttons, a bone apron with a pocket, round glasses, a grey bun, berry slippers, a basket of sweets. The Abbotts: matching striped jumpers (orange with plum, plum with orange) and matching green slippers; one with a beard and a tin, one with glasses, freckles and a candy apron |

Every house fits the placement, door, wall, sill and footprint the slice already uses for its placeholder (the
`LivingStreetLayout.Doors` numbers and `EnvironmentLayout.Size` of the house it replaces), and every piece stays
behind the visitor's stand, so the slice's route, clearance and sightline tests hold as they are.

## Files

`Assets/SpookyStreet/Content/Resources/Models/Environment/`

| File | What |
|---|---|
| `LaneHouseFogg.fbx`, `LaneHousePearl.fbx`, `LaneHouseAbbotts.fbx` | empty root at the ground under the placement point, front toward -Z; children `<Model>_LOD0`, `<Model>_LOD1` (one `LODGroup`, built by `ModelImportRules`), `RoomCard_Glow` (lit rooms and the lit hall behind the door, both levels), `Face_Glow` (jack-o'-lantern faces) |
| `LaneDoorFogg.fbx`, `LaneDoorPearl.fbx`, `LaneDoorAbbotts.fbx` | a leaf: origin on its hinge edge at the sill, closed along +X (1.14 x 2.12 m), front toward -Z, 4.5 cm thick behind the hinge's front plane |
| `LanePorchLampFogg.fbx`, `...Pearl.fbx`, `...Abbotts.fbx` | a lamp: origin in its glass, the bracket back to the wall (+Z 0.12); child `Lamp_Glow` |
| `LaneHouse<House>_BaseColor.png` (2048), `_Normal.png` (1024), `_Emission.png` (512) | one atlas per house, shared by its two levels, its leaf and its lamp |

`Assets/SpookyStreet/Content/Resources/Models/Characters/Crowd/`: `CrowdNeighbours.fbx` (the grown-up body's skeleton,
bind pose and clips with the neighbours' 24 parts and their `_Lo` copies) and `CrowdNeighbours_BaseColor.png` (2048),
`_Normal.png` (1024), `_Emission.png` (512).

The contract in code: `Assets/SpookyStreet/Core/World/LaneHouseArt.cs` (names, atlas, LOD heights, where the leaf,
lamp and bell go, footprints) and `CrowdKit.NeighbourModel`, `CrowdKit.NeighbourParts`, `CrowdKit.Neighbours` in
`Assets/SpookyStreet/Core/Crowd/CrowdKit.cs`. The scripts: `tools/blender/env/lane_houses.py` (builders),
`env_export_lane.py` (export, manifest `lane_manifest.json`), `lane_review.py` and `lane_sheet.py` (review);
`tools/blender/crowd/crowd_kit.py` (`NEIGHBOUR_PARTS`, `NEIGHBOURS`), `crowd_build.py` (slippers, beanie, apron, tin,
basket), `crowd_paint.py` (glasses, the big moustache, the cardigan and apron prints), `crowd_export.py --bodies
CrowdNeighbours`, `crowd_review.py --neighbours`.

## The swap (for the game agent)

Nothing in the runtime was changed. `tests/Visual/LaneHouseArtCaptureTests.SwapIn` does the swap below in the running
game and is the reference. In `LivingStreetSet`:

1. **Houses.** Spawn `LaneHouseArt.House(door.Id)` where `door.House` is spawned now (same position and yaw, same
   bounds collider), with any `ModelKit` on `LaneHouseArt.Folder`: the house finds its atlas by its own name. Keep
   `door.House` for the Core bounds checks (`EnvironmentLayout.Size`), or switch them to `LaneHouseArt.Footprint`,
   which gives the same sizes. The houses are not readable meshes, so `StaticBatchingUtility.Combine` leaves them out,
   like the lane's props; each house is one material (two draw calls with its lit parts).
2. **Leaves.** `ArtNames.DoorLeaf` becomes a name per door: spawn `LaneHouseArt.DoorLeaf(door.Id)` on `rig.Hinge` at
   zero with a `ModelKit` whose `FallbackAtlas = LaneHouseArt.Atlas(door.Id)`, set `rig.Leaf` to it, and drop the
   placeholder knob (the leaf has its own).
3. **Lamps.** Spawn `LaneHouseArt.PorchLamp(door.Id)` (same kit) where the `PorchLamp` sphere is now, set `rig.Lamp`
   to its `Lamp_Glow` renderer and `LampRest` / `LampBright` to `kit.GlowMaterialFor(model, 0.9f)` and
   `kit.GlowMaterialFor(model, 0.9f + door.Gag.LampBoost)`; `ApplyDoors` then warms Nana Pearl's lamp as today.
4. **Drop** the `Doorbell` cylinder (each house has its push button at `BellAcross`, `BellHeight`, where the slice's
   was) and the flat `Doorway` card (each house has its own lit hall behind the opening, `RoomCard_Glow`).
5. **Neighbours: nothing to do.** `CrowdKit.Find` now also returns the four neighbour recipes, under the slice's ids
   (`Neighbour_Fogg`, `Neighbour_Pearl`, `Neighbour_AbbottA`, `Neighbour_AbbottB`), so `WalkerLook.KitRecipe` and
   `CrowdKitBodyFactory` already build them from `CrowdNeighbours.fbx` (its own atlas, found by the model's name;
   `IsGrownUp` is true). `LivingStreetLayout.NeighbourRecipes` is now only the fallback and can go.

## Budget (living-street §8; the heart's houses: the skyline houses about 5k, the owl house 25.9k)

| Model | LOD0 triangles | LOD1 triangles | Lit parts | Texels (walls / roof / LOD1) |
|---|---|---|---|---|
| LaneHouseFogg | 14,077 | 3,325 | 229 | 73 / 40 / 12 px/m |
| LaneHousePearl | 17,944 | 3,959 | 200 | 72 / 40 / 12 px/m |
| LaneHouseAbbotts | 18,681 | 3,851 | 228 | 81 / 45 / 13 px/m |
| Leaves | 854-1,350 | | | about 140 px/m |
| Lamps | 324-458 | | | about 150 px/m |
| Neighbours (merged) | Fogg 5,805, Pearl 5,812, Abbott A 5,065, Abbott B 5,046 (budget 7k) | 1,608-1,884 (budget 2.5k) | | one 2048 atlas for 24 parts |

- LOD0 shows while a house fills more than 35 % of the screen's height (about 24 m away at the gameplay camera's
  50 degrees); beyond that LOD1, the same house in broad boards and tabs without small dressing. The lit windows, hall
  and jack-o'-lantern faces are shared, so the far house stays lit.
- One atlas per house: one 2048 for all three gave about 35 px/m on the walls (measured), half the owl house's 69; one
  per house gives 72-77 px/m on walls and porches, 40-43 on roofs (seen from below and afar), 12 on LOD1. Normal maps
  are 1024 and emission masks 512. Phone memory (ASTC 6x6 colour, 5x5 normal, with mips): about 2.5 MB per house
  and 2.5 MB for the neighbours.
- Each house is one material plus its glow variant; each neighbour one merged skinned mesh (one draw call), as the
  walkers.

## Rules held

- **Reserved looks (§3.4 rule 6, §6 rule 1).** No cloth silver (Pearl's grey hair is a warm cream grey), no lavender
  foil, no costume at all on the neighbours (house clothes), no hero theme, no owl, fox, moth, moon, star or bell
  motif, no bats (the keepsakes are paper bats), porch pumpkins only. The aprons cover the front only: a first
  version wrapped round to the arms and showed a flap from behind, too near a cape (Pip's), and was narrowed.
- **No clue language.** Doorbells are push buttons (an orange plate, a bone button with a candy top, as on Pip's
  porch hook); no knockers anywhere. No lit attic or gable window (every dormer and gable window is dark glass) and no
  round window: the owl house's attic window stays the only one and the brightest point.
- **Light.** Lit windows and the hall are `RoomCard_Glow` (1.2), jack-o'-lantern faces `Face_Glow` (1.8), lamps
  `Lamp_Glow` (the slice drives them at 0.9, Nana Pearl's to 1.7): all well under the attic window's 4. No signs, so
  nothing needs lit paint.
- **Fits the street kit.** Built from the porch kit's modules (clapboard wall, sash window, fish-scale shingles,
  corner trim, porch posts) in the owl house's craft and the art bible's families; each family has its own body
  colour, roof and silhouette (mansard and tower, one long low roof, twin gables).
- **Never rebuilt a shipped model.** The walkers' `CrowdChild.fbx`, `CrowdAdult.fbx` and `Crowd_*` atlas are not
  touched: the neighbours are the grown-up body exported on its own with its own atlas (`crowd_export.py --bodies
  CrowdNeighbours`). The skyline `NeighbourHouseA/B/C` stay as they are.

## Review

Rendered from the exported FBX files and the committed textures, in an approximation of the game's night (the
trilight ambient and the moon of `Atmosphere.cs`, no porch point lights, glow at the game's levels):

| Sheet | What it shows |
|---|---|
| `docs/evidence/living-street/houses/lane_houses_review.jpg` | the lane from the hedge gap (CI frame 94's camera) and from the Green's corner, its greyscale, each door shut and answering at the gameplay camera behind Pip, each doorway close |
| `docs/evidence/living-street/houses/lane_houses_lod.jpg` | each house's LOD0 beside its LOD1 at 22 m; each leaf open |
| `docs/evidence/living-street/houses/neighbour_lineup.jpg` | the four neighbours beside Pip at the gameplay camera, from behind, greyscale, LOD1 at 12 m, close front and back |

What the looks fixed:

1. The first unwrap gave one atlas for the three houses 14 px/m in the draft: every inside face of a house counted as
   seen through its doorway. The lit hall is now closed for the visibility test, and each house has its own atlas.
2. Mr. Fogg's hall was dark: the main block's wall stood behind the tower door; it now has its opening.
3. The roof decks of the mansard and the tower cap were built down the slope instead of up it, leaving gaps.
4. Pearl's chimney and the Abbotts' eaves poked past the slice's footprints; both now fit, and a pumpkin moved off
   the visitor's stand.
5. Mr. Fogg's white beard vanished on his pale skin; it is brown and big now.
6. The aprons (see Rules held).

## In the game's light

CI_SECTION

## Known limits

LIMITS_SECTION
